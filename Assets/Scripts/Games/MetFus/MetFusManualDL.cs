using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System;
using System.IO.Compression;

public class MetFusManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData metfusBizHawk;
    public FileDownloader.FileData metfusApworld;

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
    private bool lastApWorldInstallSuccess = false;

    [Serializable]
    public class RemoteConfig
    {
        public string metfusBizHawk;
        public string metfusApworld;
        public string[] apSearchPaths;
    }

    void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (installBizToggle != null)
            installBizToggle.isOn = true;

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (runSetupButton != null)
            runSetupButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to setup?", "Setup"));

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
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
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

        if (metfusBizHawk == null)
        {
            ShowInfo("ERROR: metfusBizHawk not configured!");
            yield break;
        }

        if (string.IsNullOrEmpty(metfusBizHawk.url))
        {
            ShowInfo("ERROR: metfusBizHawk URL is empty!");
            yield break;
        }

        string url = metfusBizHawk.url;
        string fileName = metfusBizHawk.fileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = url.Substring(url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "BizHawkTemp_MetFus");

        // Download
        yield return DownloadFile(url, localPath);

        if (!File.Exists(localPath))
        {
            ShowInfo("ERROR: Download failed!");
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        yield return null;

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
        if (extractionFailed)
        {
            UnityEngine.Debug.LogWarning("Attempting fallback extraction using downloader if available.");
            if (downloader != null)
            {
                yield return downloader.DownloadAndExtract(metfusBizHawk, Application.persistentDataPath, extractPath);
            }
            else
            {
                ShowInfo("ERROR: Extraction failed and no fallback available.\n" + extractionError);
                yield break;
            }
        }

        if (!Directory.Exists(extractPath) || (Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories).Length == 0 && Directory.GetDirectories(extractPath).Length == 0))
        {
            ShowInfo("ERROR: Extraction produced no files.");
            UnityEngine.Debug.LogError("Extraction produced no files at " + extractPath);
            yield break;
        }

        string[] topDirs = Directory.GetDirectories(extractPath);
        string[] topFiles = Directory.GetFiles(extractPath);
        string sourcePath = extractPath;

        if (topDirs.Length == 1 && topFiles.Length == 0)
            sourcePath = topDirs[0];

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string targetFolderName = "BizHawk Latest Version";
        string targetPath = Path.Combine(docs, targetFolderName);

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

        SafeDeleteDirectory(extractPath);
        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        ShowInfo("BizHawk installed to: " + targetPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + metfusApworld.url);

        if (string.IsNullOrEmpty(metfusApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = metfusApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = metfusApworld.url.Substring(metfusApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + metfusApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(metfusApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            UnityEngine.Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);
        UnityEngine.Debug.Log("Using target path: " + target);

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
            lastApWorldInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary APWorld file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
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
                configLoaded = true;
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

        metfusBizHawk.url = remoteConfig.metfusBizHawk;
        metfusApworld.url = remoteConfig.metfusApworld;
        UnityEngine.Debug.Log("Applied metfusBizHawk URL: " + metfusBizHawk.url);
        UnityEngine.Debug.Log("Applied metfusApworld URL: " + metfusApworld.url);
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

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.apSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, "custom_worlds");
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Archipelago custom_worlds (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Archipelago custom_worlds directory not found.");
        return "";
    }
}