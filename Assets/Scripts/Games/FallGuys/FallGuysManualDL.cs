using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FallGuysManualDL : MonoBehaviour
{
    [Header("FALL GUYS FILES")]
    public FileDownloader.FileData fallguysApworld;

    [Header("REMOTE CONFIG")]
    public string remoteConfigUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Fall Guys";
    public string epicGameFolderName = "FallGuys";

    [Header("GAME EXECUTABLE")]
    public string gameExeName = "FallGuys_client.exe";

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process fallguysProcess;
    private string fallguysPath;
    private string pendingAction;
    private FallGuysConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class FallGuysConfig
    {
        public string fallguysApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        fallguysPath = GetFallGuysPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        fallguysPath = GetFallGuysPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + fallguysPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        fallguysPath = GetFallGuysPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + fallguysPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(fallguysPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyConfig()
    {
        if (remoteConfig == null) return;
        if (!string.IsNullOrEmpty(remoteConfig.fallguysApworld))
            fallguysApworld.url = remoteConfig.fallguysApworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup?", "Setup");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        if (pendingAction == "Setup")
            ExecuteSetup();

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        fallguysPath = GetFallGuysPath();
        StartCoroutine(APWorldFlow());
    }

    // =========================================================
    // INSTALL FLOW
    // =========================================================

    IEnumerator APWorldFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete!\nLaunching Fall Guys...");
            yield return new WaitForSeconds(2f);
            LaunchFallGuys();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + fallguysApworld.url);

        if (string.IsNullOrEmpty(fallguysApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        // The apworld is always saved/installed as fall_guys.apworld
        string fileName = string.IsNullOrEmpty(fallguysApworld.fileName) ? "fall_guys.apworld" : fallguysApworld.fileName;

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + fallguysApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(fallguysApworld.url, localPath);

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
                UnityEngine.Debug.LogError("Download error: " + request.error);
            else
                UnityEngine.Debug.Log("Download complete! " + savePath);
        }
    }

    // =========================================================
    // LAUNCH
    // =========================================================

    public void LaunchFallGuys()
    {
        if (string.IsNullOrEmpty(fallguysPath))
        {
            UnityEngine.Debug.LogWarning("Fall Guys path not found, cannot launch.");
            return;
        }

        string exePath = Path.Combine(fallguysPath, gameExeName);
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    WorkingDirectory = fallguysPath,
                    UseShellExecute = true
                };

                fallguysProcess = Process.Start(psi);
                UnityEngine.Debug.Log("Fall Guys launched.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Fall Guys executable not found: " + exePath);
        }
    }

    // =========================================================
    // UI
    // =========================================================

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

    IEnumerator LoadRemoteConfig()
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(remoteConfigUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<FallGuysConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyConfig();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
            }
        }

        configLoaded = true;

        fallguysPath = GetFallGuysPath();
        UpdatePlatformStatus();
    }

    // =========================================================
    // PATH FINDING
    // =========================================================

    string GetFallGuysPath()
    {
        return isEpic ? GetFallGuysEpicPath() : GetFallGuysSteamPath();
    }

    string GetFallGuysSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", steamGameFolderName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", steamGameFolderName),
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Steam) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        if (remoteConfig != null && remoteConfig.steamSearchPaths != null)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.steamSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, steamGameFolderName);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Game (Steam, via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Steam) not found.");
        return "";
    }

    string GetFallGuysEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\" + "FallGuys",
            @"C:\Games\Epic\" + "FallGuys",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                foreach (string manifest in Directory.GetFiles(epicBaseDir, "*.item"))
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("FallGuys") || content.Contains("Fall Guys"))
                        {
                            var regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":\s*""([^""]+)""");
                            var match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value.Replace("\\\\", "\\");
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Game (Epic) at: " + epicPath);
                                    return epicPath;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        if (remoteConfig != null && remoteConfig.epicSearchPaths != null)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.epicSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string epicPath = Path.Combine(drive.Name, relativePath, epicGameFolderName);
                            if (Directory.Exists(epicPath))
                            {
                                UnityEngine.Debug.Log("Found Game (Epic, via remote config) at: " + epicPath);
                                return epicPath;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
    }

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed)
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