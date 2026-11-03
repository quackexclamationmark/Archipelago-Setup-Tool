using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class TFTManualDL : MonoBehaviour
{
    [Header("FILES")]
    public FileDownloader.FileData tftApworld;
    public FileDownloader.FileData tftAP;

    [Header("DEFAULT FILE NAMES")]
    public string defaultApworldFileName = "tft_archipelago.apworld";
    public string defaultInstallerFileName = "TFT-Archipelago-Setup-0.5.5.exe";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastDownloadSuccess = false;
    private bool lastInstallerSuccess = false;

    [System.Serializable]
    public class GameConfig
    {
        public string tftApworld;
        public string tftAP;
        public string[] apSearchPaths;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installAPToggle != null)
            installAPToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        tftApworld.url = remoteConfig.tftApworld;
        tftAP.url = remoteConfig.tftAP;
    }

    // ---------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
    }

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        confirmationMessage.text = message;
        confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        confirmationPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingAction))
            return;

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;

            case "Revert":
                ExecuteRevert();
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
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

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;

        if (!apworld && !ap)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && !ap)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (ap && !apworld)
        {
            StartCoroutine(APOnlyFlow());
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

    IEnumerator APOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        yield return InstallAP();
    }

    IEnumerator InstallFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        yield return new WaitForSeconds(1f);

        yield return InstallAP();
    }

    // ---------------------------------------------------------------
    // TFT AP (installer .exe)
    // ---------------------------------------------------------------

    IEnumerator InstallAP()
    {
        lastInstallerSuccess = false;

        UnityEngine.Debug.Log("Config loaded. TFT AP URL: " + tftAP.url);

        if (string.IsNullOrEmpty(tftAP.url))
        {
            ShowInfo("ERROR: TFT AP URL is empty!");
            UnityEngine.Debug.LogError("TFT AP URL not set!");
            yield break;
        }

        string downloadsPath = GetDownloadsPath();

        if (string.IsNullOrEmpty(downloadsPath) || !Directory.Exists(downloadsPath))
        {
            ShowInfo("Downloads folder not found.");
            UnityEngine.Debug.LogError("Downloads folder not found: " + downloadsPath);
            yield break;
        }

        string fileName = ResolveFileName(tftAP, defaultInstallerFileName);
        string installerPath = Path.Combine(downloadsPath, fileName);

        // Remove a previous copy so the download can overwrite it
        if (File.Exists(installerPath))
        {
            try
            {
                File.SetAttributes(installerPath, FileAttributes.Normal);
                File.Delete(installerPath);
                UnityEngine.Debug.Log("Deleted old installer: " + installerPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete old installer: " + e.Message);
            }
        }

        ShowInfo("Downloading TFT Archipelago...");
        UnityEngine.Debug.Log("Downloading TFT AP from: " + tftAP.url);
        UnityEngine.Debug.Log("Saving to: " + installerPath);

        yield return DownloadFile(tftAP.url, installerPath);

        if (!lastDownloadSuccess || !File.Exists(installerPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + installerPath);
            ShowInfo("ERROR: TFT AP download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + installerPath);

        ShowInfo("Launching TFT Archipelago installer...");
        yield return new WaitForSeconds(1f);

        LaunchInstaller(installerPath);

        if (lastInstallerSuccess)
            ShowInfo("TFT Archipelago installer launched!");
    }

    void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            ShowInfo("Installer not found at:\n" + installerPath);
            UnityEngine.Debug.LogError("Installer not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                WorkingDirectory = Path.GetDirectoryName(installerPath),
                UseShellExecute = true
            };
            Process.Start(startInfo);
            lastInstallerSuccess = true;
            UnityEngine.Debug.Log("Installer launched: " + installerPath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching installer:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    string GetDownloadsPath()
    {
        try
        {
            string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Downloads");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Could not resolve Downloads folder: " + e.Message);
            return "";
        }
    }

    // ---------------------------------------------------------------
    // APWORLD
    // ---------------------------------------------------------------

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + tftApworld.url);

        if (string.IsNullOrEmpty(tftApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = ResolveFileName(tftApworld, defaultApworldFileName);
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + tftApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(tftApworld.url, localPath);

        if (!lastDownloadSuccess || !File.Exists(localPath))
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

    // ---------------------------------------------------------------
    // REVERT
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting TFT Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            // 1) Installer left in Downloads
            string installerPath = Path.Combine(GetDownloadsPath(), ResolveFileName(tftAP, defaultInstallerFileName));

            if (File.Exists(installerPath))
            {
                try
                {
                    File.SetAttributes(installerPath, FileAttributes.Normal);
                    File.Delete(installerPath);
                    UnityEngine.Debug.Log("Deleted installer: " + installerPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete installer: " + e.Message);
                }
            }

            // 2) Launch the app's uninstaller
            string uninstallerPath = GetUninstallerPath();

            if (File.Exists(uninstallerPath))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = uninstallerPath,
                        WorkingDirectory = Path.GetDirectoryName(uninstallerPath),
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    ShowInfo("Installer removed.\nLaunching TFT Archipelago uninstaller...");
                    UnityEngine.Debug.Log("Uninstaller launched: " + uninstallerPath);
                }
                catch (System.Exception e)
                {
                    ShowInfo("Error launching uninstaller:\n" + e.Message);
                    UnityEngine.Debug.LogError("Uninstaller launch error: " + e);
                }
            }
            else
            {
                ShowInfo("Installer removed.\nUninstaller not found (TFT Archipelago may not be installed).");
                UnityEngine.Debug.LogWarning("Uninstaller not found at: " + uninstallerPath);
            }

            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    string GetAppFolderPath()
    {
        string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", "tft-archipelago");
    }

    string GetUninstallerPath()
    {
        return Path.Combine(GetAppFolderPath(), "Uninstall TFT Archipelago.exe");
    }

    public void LaunchAPTFT()
    {
        string exePath = Path.Combine(GetAppFolderPath(), "TFT Archipelago.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("TFT Archipelago.exe not found at:\n" + exePath + "\nPlease run the setup first.");
            UnityEngine.Debug.LogError("TFT Archipelago.exe not found: " + exePath);
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            Process.Start(startInfo);
            UnityEngine.Debug.Log("TFT Archipelago launched: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching TFT Archipelago:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    string ResolveFileName(FileDownloader.FileData file, string fallback)
    {
        string fileName = file.fileName;

        if (string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(file.url))
        {
            fileName = file.url.Substring(file.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName))
            fileName = fallback;

        return fileName;
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        lastDownloadSuccess = false;
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
                lastDownloadSuccess = true;
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
        }
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
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
                remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyGameConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
            }
        }

        configLoaded = true;
        UnityEngine.Debug.Log("Config marked as loaded");
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