using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System;
using System.IO.Compression;

public class PokeEmeManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData pokemonemeraldBizHawk;

    [Header("INSTALL BUTTON")]
    public Button installButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private bool configLoaded = false;
    private RemoteConfig remoteConfig;
    private string pendingAction = "";

    [Serializable]
    public class RemoteConfig
    {
        public string pokemonemeraldBizHawk;
    }

    void Start()
    {
        // Info panel
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        // Confirmation panel
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        // Install button shows confirmation
        if (installButton != null)
            installButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to install BizHawk (Pokemon Emerald)?", "Setup"));

        StartCoroutine(LoadRemoteConfig());
    }

    void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingAction))
            return;

        if (pendingAction == "Setup")
        {
            StartCoroutine(RunInstall());
        }

        pendingAction = "";
    }

    void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    IEnumerator RunInstall()
    {
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            while (!configLoaded)
                yield return new WaitForSeconds(0.1f);
            CloseInfoPanel();
        }

        ShowInfo("Starting BizHawk installation...");
        yield return new WaitForSeconds(0.5f);

        if (pokemonemeraldBizHawk == null)
        {
            ShowInfo("ERROR: pokemonemeraldBizHawk not configured!");
            yield break;
        }

        if (string.IsNullOrEmpty(pokemonemeraldBizHawk.url))
        {
            ShowInfo("ERROR: pokemonemeraldBizHawk URL is empty!");
            yield break;
        }

        string url = pokemonemeraldBizHawk.url;
        string fileName = pokemonemeraldBizHawk.fileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = url.Substring(url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "BizHawkTemp");

        // Download
        yield return DownloadFile(url, localPath);

        if (!File.Exists(localPath))
        {
            ShowInfo("ERROR: Download failed!");
            yield break;
        }

        // Clean any existing temp
        SafeDeleteDirectory(extractPath);
        yield return null;

        // Try to extract using ZipFile
        bool extractionFailed = false;
        string extractionError = null;

        try
        {
            ShowInfo("Extracting archive...");
            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(localPath, extractPath);
        }
        catch (Exception e)
        {
            extractionFailed = true;
            extractionError = e.Message;
            UnityEngine.Debug.LogWarning("Zip extraction failed: " + e.Message);
        }

        // If extraction failed, attempt fallback extraction using downloader (but do this OUTSIDE the catch)
        if (extractionFailed)
        {
            UnityEngine.Debug.LogWarning("Attempting fallback extraction using downloader if available.");
            if (downloader != null)
            {
                yield return downloader.DownloadAndExtract(pokemonemeraldBizHawk, Application.persistentDataPath, extractPath);
            }
            else
            {
                ShowInfo("ERROR: Extraction failed and no fallback available.\n" + extractionError);
                yield break;
            }
        }

        // Verify extraction produced something
        if (!Directory.Exists(extractPath) || (Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories).Length == 0 && Directory.GetDirectories(extractPath).Length == 0))
        {
            ShowInfo("ERROR: Extraction produced no files.");
            UnityEngine.Debug.LogError("Extraction produced no files at " + extractPath);
            yield break;
        }

        // Determine extracted root
        string[] topDirs = Directory.GetDirectories(extractPath);
        string[] topFiles = Directory.GetFiles(extractPath);
        string sourcePath = extractPath;

        // If the zip produced a single root folder, use it
        if (topDirs.Length == 1 && topFiles.Length == 0)
            sourcePath = topDirs[0];

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string targetFolderName = "BizHawk Latest Version";
        string targetPath = Path.Combine(docs, targetFolderName);

        // Remove old target if exists
        try
        {
            if (Directory.Exists(targetPath))
            {
                SafeDeleteDirectory(targetPath);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to delete existing target: " + e.Message);
        }

        // Move or copy extracted files to Documents
        try
        {
            if (Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? docs);

                try
                {
                    Directory.Move(sourcePath, targetPath);
                }
                catch
                {
                    CopyDirectoryRecursive(sourcePath, targetPath);
                }
            }
            else if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(targetPath);
                File.Copy(sourcePath, Path.Combine(targetPath, Path.GetFileName(sourcePath)), true);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to place BizHawk in Documents: " + e.Message);
            ShowInfo("ERROR: Failed to install BizHawk\n" + e.Message);
            yield break;
        }

        // Cleanup local temp and zip
        SafeDeleteDirectory(extractPath);
        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        ShowInfo("BizHawk installed to: " + targetPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new System.IO.FileInfo(savePath).Length + " bytes");
            }
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (var request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Config load failed: " + request.error);
                configLoaded = true; // mark to avoid blocking installs, but no URL applied
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<RemoteConfig>(request.downloadHandler.text);
                ApplyRemoteConfig();
                UnityEngine.Debug.Log("Remote config loaded successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
            }
        }

        configLoaded = true;
        UnityEngine.Debug.Log("Config marked as loaded");
    }

    void ApplyRemoteConfig()
    {
        if (remoteConfig == null)
            return;

        pokemonemeraldBizHawk.url = remoteConfig.pokemonemeraldBizHawk;
        UnityEngine.Debug.Log("Applied pokemonemeraldBizHawk URL: " + pokemonemeraldBizHawk.url);
    }

    void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            string destSub = Path.Combine(destDir, Path.GetFileName(directory));
            CopyDirectoryRecursive(directory, destSub);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null)
            return;

        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}