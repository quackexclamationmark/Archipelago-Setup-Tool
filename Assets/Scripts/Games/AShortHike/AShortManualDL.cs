using System.Collections;
using System.Diagnostics;
using System.IO;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public class AShortHikeManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("A SHORT HIKE FILES")]
    public FileDownloader.FileData ashorthikeBepInEx;
    public FileDownloader.FileData ashorthikeAP;
    public FileDownloader.FileData ashorthikeModdingAPI;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installAPToggle;
    public Toggle installModdingAPIToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchAfterSetupToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearToggle;
    public Toggle clearAPModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process ashorthikeProcess;
    private string ashorthikePath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private AShortHikeConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    private static readonly string[] AP_MOD_WHITELIST = new string[]
{
        "Archipelago.MultiClient.Net.dll",
        "ModdingAPI.dll",
        "Randomizer.dll",
        "Randomizer",
        "ap-item.png",
        "locations.json",
        "AShortHike APMod Version"
};

    [System.Serializable]
    public class AShortHikeConfig
    {
        public string ashorthikeBepInEx;
        public string ashorthikeAP;
        public string ashorthikeModdingAPI;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        ashorthikePath = GetAShortHikePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchAfterSetupToggle != null)
            launchAfterSetupToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked()
    {
        SelectSteam();
    }

    void OnEpicButtonClicked()
    {
        SelectEpic();
    }

    void SelectSteam()
    {
        isEpic = false;
        ashorthikePath = GetAShortHikePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + ashorthikePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        ashorthikePath = GetAShortHikePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + ashorthikePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(ashorthikePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplyAShortHikeConfig()
    {
        if (remoteConfig == null)
            return;

        ashorthikeBepInEx.url = remoteConfig.ashorthikeBepInEx;
        ashorthikeAP.url = remoteConfig.ashorthikeAP;
        ashorthikeModdingAPI.url = remoteConfig.ashorthikeModdingAPI;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
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

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;

            case "Revert":
                ExecuteRevert();
                break;

            case "ForceFullClear":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        ashorthikePath = GetAShortHikePath();

        if (string.IsNullOrEmpty(ashorthikePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("A Short Hike not found in " + platform + ". Please check installation.");
            return;
        }

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool api = installModdingAPIToggle != null && installModdingAPIToggle.isOn;

        int count = (bep ? 1 : 0) + (ap ? 1 : 0) + (api ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {

        ashorthikePath = GetAShortHikePath();

        DeleteOldVersionFiles();

        if (string.IsNullOrEmpty(ashorthikePath))
            return;

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(ashorthikePath, "Archipelago.MultiClient.Net.dll"));

            // Remove Archipelago.MultiClient.Net.dll
            SafeDeleteFile(Path.Combine(ashorthikePath, "Modding", "data", "Archipelago.MultiClient.Net.dll"));

            // Remove Randomizer folder from Modding\data
            SafeDeleteDirectory(Path.Combine(ashorthikePath, "Modding", "data", "Randomizer"));

            // Remove ModdingAPI.dll and Randomizer.dll from Modding\plugins
            SafeDeleteFile(Path.Combine(ashorthikePath, "Modding", "plugins", "ModdingAPI.dll"));
            SafeDeleteFile(Path.Combine(ashorthikePath, "Modding", "plugins", "Randomizer.dll"));

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (hasOtherMods && !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other files were detected in Modding folder.\nDo you want to continue?",
                "ForceFullClear"
            );

            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        ShowInfo("Clearing all mods...");

        // Remove BepInEx and Modding folders
        SafeDeleteDirectory(Path.Combine(ashorthikePath, "BepInEx"));
        SafeDeleteDirectory(Path.Combine(ashorthikePath, "Modding"));

        // Remove all related files
        SafeDeleteFile(Path.Combine(ashorthikePath, ".modtools_version"));
        SafeDeleteFile(Path.Combine(ashorthikePath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(ashorthikePath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(ashorthikePath, ".doorstop_version"));

        ShowInfo("Full clear completed!");
    }

    // =========================================================
    // INFO
    // =========================================================

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

    // =========================================================
    // OTHER LOGIC
    // =========================================================

    bool IsWhitelisted(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string folderName = new DirectoryInfo(Path.GetDirectoryName(filePath)).Name;

        // Check if filename or folder is in whitelist (allow starts-with to match version files)
        foreach (string whitelisted in AP_MOD_WHITELIST)
        {
            if (fileName.StartsWith(whitelisted, StringComparison.OrdinalIgnoreCase))
                return true;

            if (folderName.Equals(whitelisted, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    bool HasOtherMods()
    {
        string moddingPath = Path.Combine(ashorthikePath, "Modding");

        // Only check Modding folder for other files (BepInEx is considered standard content)
        if (Directory.Exists(moddingPath))
        {
            string[] files = Directory.GetFiles(moddingPath, "*", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                bool hasNonWhitelisted = false;
                foreach (string file in files)
                {
                    if (!IsWhitelisted(file))
                    {
                        if (!hasNonWhitelisted)
                        {
                            UnityEngine.Debug.LogWarning("Found non-whitelisted file(s) in Modding:");
                            hasNonWhitelisted = true;
                        }
                        UnityEngine.Debug.LogWarning("  - " + file);
                    }
                }

                return hasNonWhitelisted;
            }
        }

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing Mod Loader...");
            yield return InstallBepInEx();
        }

        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing AP mod...");
            yield return InstallAPMod();
        }

        if (installModdingAPIToggle != null && installModdingAPIToggle.isOn)
        {
            ShowInfo("Installing Modding API...");
            yield return InstallModdingAPI();
        }

        // Create version file after installs (root + Modding)
        CreateVersionFile(ashorthikeAP.url, ashorthikeModdingAPI.url, ashorthikeBepInEx.url);

        if (launchAfterSetupToggle != null && launchAfterSetupToggle.isOn)
        {
            ShowInfo("Launching A Short Hike...");
            LaunchAShortHike();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(ashorthikeBepInEx, Application.persistentDataPath, extractPath);

        // Copy all contents from extractPath to ashorthikePath
        CopyDirectory(extractPath, ashorthikePath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "AShortHikeAPTemp");

        yield return downloader.DownloadAndExtract(ashorthikeAP, Application.persistentDataPath, extractPath);

        string moddingPath = Path.Combine(ashorthikePath, "Modding");
        Directory.CreateDirectory(moddingPath);

        // Copy data folder
        string sourceDataPath = Path.Combine(extractPath, "data");
        if (Directory.Exists(sourceDataPath))
        {
            string targetDataPath = Path.Combine(moddingPath, "data");
            CopyDirectory(sourceDataPath, targetDataPath);
            UnityEngine.Debug.Log("AP data copied successfully!");
        }
        else
        {
            UnityEngine.Debug.LogWarning("Data folder not found in extraction!");
        }

        // Copy plugins folder
        string sourcePluginsPath = Path.Combine(extractPath, "plugins");
        if (Directory.Exists(sourcePluginsPath))
        {
            string targetPluginsPath = Path.Combine(moddingPath, "plugins");
            Directory.CreateDirectory(targetPluginsPath);
            CopyDirectory(sourcePluginsPath, targetPluginsPath);
            UnityEngine.Debug.Log("AP plugins copied successfully!");
        }
        else
        {
            UnityEngine.Debug.LogWarning("Plugins folder not found in extraction!");
        }

        UnityEngine.Debug.Log("AP Mod installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallModdingAPI()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "ModdingAPITemp");

        yield return downloader.DownloadAndExtract(ashorthikeModdingAPI, Application.persistentDataPath, extractPath);

        string sourcePluginsPath = Path.Combine(extractPath, "plugins");

        if (!Directory.Exists(sourcePluginsPath))
        {
            UnityEngine.Debug.LogError("plugins folder not found in ModdingAPI extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string targetModdingPath = Path.Combine(ashorthikePath, "Modding");
        string targetPluginsPath = Path.Combine(targetModdingPath, "plugins");

        Directory.CreateDirectory(targetPluginsPath);

        // Copy all plugins from source to target
        CopyDirectory(sourcePluginsPath, targetPluginsPath);

        UnityEngine.Debug.Log("Modding API installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

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
            remoteConfig = JsonUtility.FromJson<AShortHikeConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyAShortHikeConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchAShortHike()
    {
        string exePath = Path.Combine(ashorthikePath, "AShortHike.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("A Short Hike executable not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = exePath;
        startInfo.WorkingDirectory = ashorthikePath;

        try
        {
            ashorthikeProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("A Short Hike launched successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching A Short Hike:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseAShortHike()
    {
        try
        {
            if (ashorthikeProcess != null && !ashorthikeProcess.HasExited)
            {
                ashorthikeProcess.Kill();
                ashorthikeProcess.Dispose();
                ashorthikeProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseAShortHike();
    }

    void SafeDeleteFile(string path)
    {
        StartCoroutine(DeleteFileForce(path));
    }

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;

        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);

                if (!File.Exists(path))
                    yield break;
            }
            catch { }

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
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

    void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
        }
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetAShortHikePath()
    {
        if (isEpic)
            return GetAShortHikeEpicPath();
        else
            return GetAShortHikeSteamPath();
    }

    string GetAShortHikeSteamPath()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "A Short Hike"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "A Short Hike"),
        @"D:\Steam\steamapps\common\A Short Hike",
        @"D:\SteamLibrary\steamapps\common\A Short Hike",
        @"D:\steamapps\common\A Short Hike",
        @"E:\Steam\steamapps\common\A Short Hike",
        @"E:\SteamLibrary\steamapps\common\A Short Hike",
        @"E:\steamapps\common\A Short Hike",
        @"E:\Program Files (x86)\steamapps\common\A Short Hike",
        @"E:\Program Files\steamapps\common\A Short Hike",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    // Search Steam\steamapps
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "A Short Hike");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "A Short Hike");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search steamapps at drive root
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "A Short Hike");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "A Short Hike");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "A Short Hike");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("A Short Hike (Steam) not found.");
        return "";
    }

    string GetAShortHikeEpicPath()
    {
        string[] quickPaths = new string[]
        {
        @"C:\Program Files\Epic Games\AShortHike",
        @"D:\Epic Games\AShortHike",
        @"E:\Epic Games\AShortHike",
        @"C:\Games\Epic\AShortHike",
        @"D:\Games\Epic\AShortHike",
        @"E:\Games\Epic\AShortHike",
        @"C:\Epic\AShortHike",
        @"D:\Epic\AShortHike",
        @"E:\Epic\AShortHike",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found A Short Hike (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Search in Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Search for AShortHike manifest
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("AShortHike") || content.Contains("A Short Hike"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found A Short Hike (Epic) at: " + epicPath);
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

        // Scan all drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "AShortHike");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "AShortHike");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found A Short Hike (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("A Short Hike (Epic) not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE (added)
    // =========================================================

    void CreateVersionFile(string apUrl, string modApiUrl, string bepinexUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string modApiVersion = ExtractVersionFromUrl(modApiUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "AShortHike APMod Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== MODDING API ===\n";
            content += "Downloaded from: " + modApiUrl + "\n";
            content += "Version: " + modApiVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";

            DeleteOldVersionFiles();

            // Write to root
            if (!string.IsNullOrEmpty(ashorthikePath))
            {
                string rootVersionPath = Path.Combine(ashorthikePath, versionFileName);
                File.WriteAllText(rootVersionPath, content);
                UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);
            }

            // Write to Modding folder
            string moddingPath = Path.Combine(ashorthikePath, "Modding");
            if (Directory.Exists(moddingPath))
            {
                string moddingVersionPath = Path.Combine(moddingPath, versionFileName);
                File.WriteAllText(moddingVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Modding: " + moddingVersionPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            Regex pattern = new Regex(@"AShortHike APMod Version .+\.txt");

            if (!string.IsNullOrEmpty(ashorthikePath))
            {
                string[] rootFiles = Directory.GetFiles(ashorthikePath);
                foreach (string file in rootFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in root: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message);
                        }
                    }
                }
            }

            string moddingPath = Path.Combine(ashorthikePath, "Modding");
            if (Directory.Exists(moddingPath))
            {
                string[] modFiles = Directory.GetFiles(moddingPath);
                foreach (string file in modFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in Modding: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Modding: " + e.Message);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        try
        {
            Regex regex = new Regex(pattern);
            Match match = regex.Match(url);

            if (match.Success)
                return match.Groups[1].Value;

            try
            {
                Uri uri = new Uri(url);
                string last = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(last))
                    return last;
            }
            catch { }

            return "Unknown";
        }
        catch { return "Unknown"; }
    }
}