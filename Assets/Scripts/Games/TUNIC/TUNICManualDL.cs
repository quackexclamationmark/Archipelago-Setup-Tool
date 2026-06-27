using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class TUNICManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("TUNIC FILES")]
    public FileDownloader.FileData tunicBepInEx;
    public FileDownloader.FileData tunicAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process tunicProcess;
    private string tunicPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private TunicConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class TunicConfig
    {
        public string tunicBepInEx;
        public string tunicAP;
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

        tunicPath = GetTunicPath();
        StartCoroutine(LoadRemoteConfig());

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

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

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.isOn = false;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);
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
        tunicPath = GetTunicPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + tunicPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        tunicPath = GetTunicPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + tunicPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(tunicPath) ? "Not Found" : "Found";
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

    void ApplyTunicConfig()
    {
        if (remoteConfig == null)
            return;

        tunicBepInEx.url = remoteConfig.tunicBepInEx;
        tunicAP.url = remoteConfig.tunicAP;
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
        tunicPath = GetTunicPath();

        if (string.IsNullOrEmpty(tunicPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("TUNIC not found in " + platform + ". Please check installation.");
            return;
        }

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;

        int count =
            (bep ? 1 : 0) +
            (ap ? 1 : 0);

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        tunicPath = GetTunicPath();

        if (string.IsNullOrEmpty(tunicPath))
            return;

        string pluginsPath = Path.Combine(tunicPath, "BepInEx", "plugins");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(pluginsPath, "Tunic Randomizer"));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClear &&
            hasOtherMods &&
            !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        if (fullClear)
        {
            ShowInfo("Clearing BepInEx...");

            SafeDeleteDirectory(Path.Combine(tunicPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(tunicPath, "mono"));
            SafeDeleteFile(Path.Combine(tunicPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(tunicPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(tunicPath, "winhttp.dll"));
            DeleteOldVersionFiles();

            ShowInfo("Full clear completed!");
            return;
        }

        ShowInfo("Revert completed!");
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

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "Tunic Randomizer")
                return true;
        }

        string[] files = Directory.GetFiles(pluginsPath);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            // Whitelist version files
            if (fileName.StartsWith("TUNIC Randomizer Version") && fileName.EndsWith(".txt"))
                continue;

            return true;
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
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing AP Randomizer...");
            yield return InstallAP();
        }

        CreateVersionFile(tunicAP.url, tunicBepInEx.url);

        ShowInfo("Launching TUNIC...");
        LaunchTunic();

        yield return WaitForConfigFiles();

        CloseTunic();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchTunic();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "TunicAPTemp");
        yield return downloader.DownloadAndExtract(tunicAP, Application.persistentDataPath, extractPath);

        string modFolder = Path.Combine(extractPath, "Tunic Randomizer");
        string pluginsPath = Path.Combine(tunicPath, "BepInEx", "plugins");

        Directory.CreateDirectory(pluginsPath);

        if (Directory.Exists(modFolder))
        {
            string targetMod = Path.Combine(pluginsPath, "Tunic Randomizer");

            if (Directory.Exists(targetMod))
                Directory.Delete(targetMod, true);

            CopyDirectory(modFolder, targetMod);
            UnityEngine.Debug.Log("AP Randomizer installed successfully!");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(tunicBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, tunicPath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APOnlyFlow()
    {
        ShowInfo("Installing AP Randomizer...");
        yield return InstallAP();

        CreateVersionFile(tunicAP.url, tunicBepInEx.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        ShowInfo("Launching TUNIC...");
        LaunchTunic();
        yield return WaitForConfigFiles();
        CloseTunic();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchTunic();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(tunicPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfg))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
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
            remoteConfig = JsonUtility.FromJson<TunicConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyTunicConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchTunic()
    {
        string exePath = Path.Combine(tunicPath, "Tunic.exe");

        if (File.Exists(exePath))
            tunicProcess = Process.Start(exePath);
    }

    void CloseTunic()
    {
        try
        {
            if (tunicProcess != null && !tunicProcess.HasExited)
            {
                tunicProcess.Kill();
                tunicProcess.Dispose();
                tunicProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseTunic();
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        Directory.Delete(source, true);
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apUrl, string bepinexUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string bepinexVersion = ExtractBepInExVersion(bepinexUrl);

            string versionFileName = "TUNIC Randomizer Version " + apVersion + ".txt";
            string content = "TUNIC Randomizer Setup Tool\n";
            content += "https://github.com/yourusername/TUNIC-Setup-Tool\n";
            content += "\n";
            content += "=== AP RANDOMIZER ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(tunicPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(tunicPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"TUNIC Randomizer Version .+\.txt");

            // Supprimer les fichiers à la racine
            string[] rootFiles = Directory.GetFiles(tunicPath);
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

            // Supprimer les fichiers dans plugins (mais ignorer les version files)
            string pluginsPath = Path.Combine(tunicPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string[] pluginsFiles = Directory.GetFiles(pluginsPath);
                foreach (string file in pluginsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in plugins: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in plugins: " + e.Message);
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
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string ExtractBepInExVersion(string url)
    {
        // Cherche le pattern BepInEx: BepInEx_UnityIL2CPP_x64_VERSION.zip
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"BepInEx_UnityIL2CPP_x64_([^/]+)");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetTunicPath()
    {
        if (isEpic)
            return GetTunicEpicPath();
        else
            return GetTunicSteamPath();
    }

    string GetTunicSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "TUNIC"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "TUNIC"),
            @"D:\Steam\steamapps\common\TUNIC",
            @"D:\SteamLibrary\steamapps\common\TUNIC",
            @"D:\steamapps\common\TUNIC",
            @"E:\Steam\steamapps\common\TUNIC",
            @"E:\SteamLibrary\steamapps\common\TUNIC",
            @"E:\steamapps\common\TUNIC",
            @"E:\Program Files (x86)\steamapps\common\TUNIC",
            @"E:\Program Files\steamapps\common\TUNIC",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + path);
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
                    // Cherche Steam\steamapps
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "TUNIC");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Cherche SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "TUNIC");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Cherche directement steamapps à la racine du disque
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "TUNIC");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Cherche dans Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "TUNIC");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Cherche dans Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "TUNIC");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("TUNIC (Steam) not found.");
        return "";
    }

    string GetTunicEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\tunic",
            @"D:\Epic Games\tunic",
            @"E:\Epic Games\tunic",
            @"C:\Games\Epic\tunic",
            @"D:\Games\Epic\tunic",
            @"E:\Games\Epic\tunic",
            @"C:\Epic\tunic",
            @"D:\Epic\tunic",
            @"E:\Epic\tunic",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found TUNIC (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Cherche dans Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Cherche le manifest pour TUNIC
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("TUNIC"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found TUNIC (Epic) at: " + epicPath);
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
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "TUNIC");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "TUNIC");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found TUNIC (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("TUNIC (Epic) not found.");
        return "";
    }
}
