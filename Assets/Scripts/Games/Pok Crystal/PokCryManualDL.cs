using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System;
using System.IO.Compression;

public class PokeCryManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData pokemoncrystalBizHawk;
    public FileDownloader.FileData pokemoncrystalApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installBizToggle;
    public Toggle installApworldToggle;

    [Header("INSTALL BUTTON")]
    public Button runSetupButton;

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
        public string pokemoncrystalBizHawk;
        public string pokemoncrystalApworld;
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

        // Toggles defaults
        if (installBizToggle != null)
            installBizToggle.isOn = true;

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        // Setup button
        if (runSetupButton != null)
            runSetupButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to run setup with the selected options?", "Setup"));

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
            ExecuteSetup();
        }

        pendingAction = "";
    }

    void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    void ExecuteSetup()
    {
        bool biz = installBizToggle != null && installBizToggle.isOn;
        bool apworld = installApworldToggle != null && installApworldToggle.isOn;

        int count = (biz ? 1 : 0) + (apworld ? 1 : 0);

        if (!biz && !apworld)
        {
            ShowInfo("Please select at least one install option.");
            return;
        }

        if (biz && count == 1)
        {
            StartCoroutine(BizOnlyFlow());
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(InstallAPWorld());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        if (installBizToggle != null && installBizToggle.isOn)
        {
            ShowInfo("Installing BizHawk...");
            yield return RunInstallBiz();
        }

        if (installApworldToggle != null && installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator BizOnlyFlow()
    {
        ShowInfo("Installing BizHawk...");
        yield return RunInstallBiz();
        ShowInfo("Installation complete!");
    }

    IEnumerator RunInstallBiz()
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

        if (pokemoncrystalBizHawk == null)
        {
            ShowInfo("ERROR: pokemoncrystalBizHawk not configured!");
            yield break;
        }

        if (string.IsNullOrEmpty(pokemoncrystalBizHawk.url))
        {
            ShowInfo("ERROR: pokemoncrystalBizHawk URL is empty!");
            yield break;
        }

        string url = pokemoncrystalBizHawk.url;
        string fileName = pokemoncrystalBizHawk.fileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = url.Substring(url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "BizHawkTemp_Crystal");

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
                yield return downloader.DownloadAndExtract(pokemoncrystalBizHawk, Application.persistentDataPath, extractPath);
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

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Crystal APWorld URL: " + pokemoncrystalApworld.url);

        if (string.IsNullOrEmpty(pokemoncrystalApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pokemoncrystalApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            // For Pokemon Crystal, default filename is pokemon_crystal.apworld
            fileName = "pokemon_crystal.apworld";
            UnityEngine.Debug.Log("Using default APWorld filename: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pokemoncrystalApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pokemoncrystalApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // Target paths
        string[] targetPaths = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", fileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", fileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", fileName),
        };

        string target = "";
        foreach (string path in targetPaths)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                target = path;
                UnityEngine.Debug.Log("Using target path: " + target);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            UnityEngine.Debug.LogError("No valid target directory found!");
            yield break;
        }

        UnityEngine.Debug.Log("Target path: " + target);

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                UnityEngine.Debug.Log("Deleted old apworld file");
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);

            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }
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

        pokemoncrystalBizHawk.url = remoteConfig.pokemoncrystalBizHawk;
        pokemoncrystalApworld.url = remoteConfig.pokemoncrystalApworld;
        UnityEngine.Debug.Log("Applied pokemoncrystalBizHawk URL: " + pokemoncrystalBizHawk.url);
        UnityEngine.Debug.Log("Applied pokemoncrystalApworld URL: " + pokemoncrystalApworld.url);
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