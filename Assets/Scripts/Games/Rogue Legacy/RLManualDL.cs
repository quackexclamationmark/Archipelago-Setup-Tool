using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RLManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ROGUE LEGACY FILES")]
    public FileDownloader.FileData roguelegacyApworld;
    public FileDownloader.FileData roguelegacyRandomizer;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Rogue Legacy";
    public string epicGameFolderName = "RogueLegacy";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installRandomizerToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private const string RandomizerFolderName = "Rogue Legacy Randomizer";
    private const string RandomizerExeName = "Rogue Legacy Randomizer.exe";
    private const string RandomizerProcessName = "Rogue Legacy Randomizer";

    private string rlPath;
    private string pendingAction;
    private RLConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastDownloadSuccess = false;
    private bool lastRandomizerSuccess = false;
    private Process installerProcess;

    [System.Serializable]
    public class RLConfig
    {
        public string roguelegacyApworld;
        public string roguelegacyRandomizer;
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

        StartCoroutine(LoadRemoteConfig());

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installRandomizerToggle != null)
            installRandomizerToggle.isOn = true;
    }

    // =====================================================================
    // PLATFORM SELECTION
    // =====================================================================

    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        rlPath = GetRLPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + rlPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        rlPath = GetRLPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + rlPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(rlPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    void ApplyRLConfig()
    {
        if (remoteConfig == null)
            return;

        roguelegacyApworld.url = remoteConfig.roguelegacyApworld;
        roguelegacyRandomizer.url = remoteConfig.roguelegacyRandomizer;
    }

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
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

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
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // =====================================================================
    // SETUP
    // =====================================================================

    private void ExecuteSetup()
    {
        rlPath = GetRLPath();

        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool randomizer = installRandomizerToggle != null && installRandomizerToggle.isOn;

        if (!apworld && !randomizer)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool randomizer = installRandomizerToggle != null && installRandomizerToggle.isOn;

        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();

            if (!lastApWorldInstallSuccess)
                yield break;
        }

        if (randomizer)
        {
            ShowInfo("Downloading Rogue Legacy Randomizer...");
            yield return InstallRandomizer();

            if (!lastRandomizerSuccess)
                yield break;
        }

        ShowInfo("Installation complete!");
    }

    // =====================================================================
    // RANDOMIZER
    // =====================================================================

    string GetRandomizerFolderPath()
    {
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
            RandomizerFolderName);
    }

    string GetRandomizerExePath()
    {
        return Path.Combine(GetRandomizerFolderPath(), RandomizerExeName);
    }

    string GetDownloadsPath()
    {
        string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
    }

    IEnumerator InstallRandomizer()
    {
        lastRandomizerSuccess = false;
        installerProcess = null;

        if (string.IsNullOrEmpty(roguelegacyRandomizer.url))
        {
            ShowInfo("ERROR: Randomizer URL is empty!");
            UnityEngine.Debug.LogError("Randomizer URL not set!");
            yield break;
        }

        string fileName = roguelegacyRandomizer.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = GetFileNameFromUrl(roguelegacyRandomizer.url, "Rogue.Legacy.Randomizer.1.1.0.exe");

        string localPath = Path.Combine(GetDownloadsPath(), fileName);

        UnityEngine.Debug.Log("Downloading Randomizer from: " + roguelegacyRandomizer.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(roguelegacyRandomizer.url, localPath);

        if (!lastDownloadSuccess || !File.Exists(localPath))
        {
            ShowInfo("ERROR: Randomizer download failed!");
            yield break;
        }

        ShowInfo("Running Rogue Legacy Randomizer installer...");

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(localPath);
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = GetDownloadsPath();
            installerProcess = Process.Start(startInfo);
            lastRandomizerSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to run Randomizer: " + e.Message);
            ShowInfo("ERROR: Failed to run the Randomizer.\n" + e.Message);
        }
    }

    public void LaunchRandomizer()
    {
        string exePath = GetRandomizerExePath();

        try
        {
            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = GetRandomizerFolderPath();
                Process.Start(startInfo);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Randomizer exe not found: " + exePath);
                ShowInfo("Randomizer exe not found. Was the installation completed?");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch Randomizer: " + e.Message);
        }
    }

    string GetFileNameFromUrl(string url, string fallback)
    {
        try
        {
            string name = url.Substring(url.LastIndexOf('/') + 1);

            if (name.Contains("?"))
                name = name.Substring(0, name.IndexOf("?"));

            if (!string.IsNullOrEmpty(name))
                return name;
        }
        catch { }

        return fallback;
    }

    // =====================================================================
    // REVERT
    // =====================================================================

    private void ExecuteRevert()
    {
        StartCoroutine(RevertRoutine());
    }

    IEnumerator RevertRoutine()
    {
        ShowInfo("Removing Rogue Legacy Randomizer...");

        CleanupProcesses();
        yield return new WaitForSeconds(1f);

        string folder = GetRandomizerFolderPath();

        if (!Directory.Exists(folder))
        {
            ShowInfo("Rogue Legacy Randomizer folder not found. Nothing to remove.");
            yield break;
        }

        try
        {
            SetDirectoryNormal(folder);
            Directory.Delete(folder, true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Normal delete failed, trying elevated: " + e.Message);
        }

        if (Directory.Exists(folder))
        {
            ShowInfo("Administrator permission is required to remove the folder...");

            Process adminProcess = null;
            bool cancelled = false;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.Arguments = "/c rmdir /s /q \"" + folder + "\"";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                adminProcess = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                cancelled = true;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Elevated delete failed: " + e.Message);
                ShowInfo("ERROR: Failed to remove the Randomizer folder.\n" + e.Message);
                yield break;
            }

            if (cancelled || adminProcess == null)
            {
                ShowInfo("Administrator permission was denied. The folder was not removed.");
                yield break;
            }

            float timer = 0f;
            while (!adminProcess.HasExited && timer < 15f)
            {
                timer += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
        }

        if (Directory.Exists(folder))
            ShowInfo("ERROR: Failed to remove the Randomizer folder.");
        else
            ShowInfo("Rogue Legacy Randomizer removed successfully!");
    }

    void SetDirectoryNormal(string path)
    {
        try
        {
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
        }
        catch { }
    }

    // =====================================================================
    // UI
    // =====================================================================

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

    public void QuitLauncher()
    {
        Application.Quit();
    }

    // =====================================================================
    // APWORLD
    // =====================================================================

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + roguelegacyApworld.url);

        if (string.IsNullOrEmpty(roguelegacyApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = roguelegacyApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = GetFileNameFromUrl(roguelegacyApworld.url, "rogue_legacy.apworld");

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + roguelegacyApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(roguelegacyApworld.url, localPath);

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

    IEnumerator DownloadFile(string url, string savePath)
    {
        lastDownloadSuccess = false;

        UnityEngine.Debug.Log("Starting download from: " + url);

        try
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(savePath))
                File.Delete(savePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not prepare download path: " + e.Message);
        }

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
                UnityEngine.Debug.Log("Download complete! File size: " + new System.IO.FileInfo(savePath).Length + " bytes");
            }
        }
    }

    // =====================================================================
    // PROCESSES
    // =====================================================================

    void CleanupProcesses()
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName(RandomizerProcessName))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
    }

    // =====================================================================
    // GAME PATHS (STEAM / EPIC)
    // =====================================================================

    string GetRLPath()
    {
        return isEpic ? GetRLEpicPath() : GetRLSteamPath();
    }

    string GetRLSteamPath()
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
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
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

    string GetRLEpicPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(@"C:\Program Files\Epic Games", epicGameFolderName),
            Path.Combine(@"C:\Games\Epic", epicGameFolderName),
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

        // Epic Games Launcher manifests
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

                        if (!content.Contains(epicGameFolderName))
                            continue;

                        Match match = Regex.Match(content, "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");

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
                    catch { }
                }
            }
        }
        catch { }

        if (remoteConfig != null && remoteConfig.epicSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
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

    // =====================================================================
    // REMOTE CONFIG
    // =====================================================================

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<RLConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyRLConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        rlPath = GetRLPath();
        UpdatePlatformStatus();
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