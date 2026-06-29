using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class PikunikuManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData pikunikuApworld;
    public FileDownloader.FileData pikunikuAP;
    public FileDownloader.FileData pikunikuBepInEx;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearBepInExToggle;
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

    private string gamePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;
    private bool isEpic = false;

    [System.Serializable]
    public class GameConfig
    {
        public string pikunikuAP;
        public string pikunikuApworld;
        public string pikunikuBepInEx;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
        public List<string> installedDirectories = new List<string>();
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

        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (installBepInExToggle != null)
            installBepInExToggle.isOn = true;

        if (fullClearBepInExToggle != null)
        {
            fullClearBepInExToggle.isOn = false;
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);
        }

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
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
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + gamePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + gamePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(gamePath) ? "Not Found" : "Found";
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
            clearAPModsToggle.isOn = !value ? clearAPModsToggle.isOn : false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        pikunikuAP.url = remoteConfig.pikunikuAP;
        pikunikuApworld.url = remoteConfig.pikunikuApworld;
        pikunikuBepInEx.url = remoteConfig.pikunikuBepInEx;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void RevertAll()
    {
        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;
        bool clearAPMods = clearAPModsToggle != null && clearAPModsToggle.isOn;

        if (!fullClear && !clearAPMods)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

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

            case "ForceFullClean":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            ShowInfo("Pikuniku not found in " + platform + ". Please check installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;
        bool bepinex = installBepInExToggle == null || installBepInExToggle.isOn;

        int count = (apworld ? 1 : 0) + (apmod ? 1 : 0) + (bepinex ? 1 : 0);

        StartCoroutine(SetupWithTracking(apworld, apmod, bepinex));
    }

    IEnumerator SetupWithTracking(bool installAPWorld, bool installAPMod, bool installBepInEx)
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow(installAPWorld, installAPMod, installBepInEx);

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            return;

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClean = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing Pikuniku AP mods...");

            string pikunikuModPath = Path.Combine(pluginsPath, "PikunikuAPMod");
            SafeDeleteDirectory(pikunikuModPath);

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // At this point fullClean == true
        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation(
                "Other mods were detected in BepInEx\\plugins.\nDo you REALLY want to fully delete BepInEx and related files?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Removing AP mods...");

        string pikunikuModPath2 = Path.Combine(pluginsPath, "PikunikuAPMod");
        SafeDeleteDirectory(pikunikuModPath2);

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx"));
            SafeDeleteFile(Path.Combine(gamePath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(gamePath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(gamePath, "winhttp.dll"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning BepInEx (no other mods detected)...");

            SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx"));
            SafeDeleteFile(Path.Combine(gamePath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(gamePath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(gamePath, "winhttp.dll"));

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);

            if (dirName == "PikunikuAPMod")
                continue;

            // If any other directory found => other mod present
            return true;
        }

        return false;
    }

    IEnumerator InstallFlow(bool installAPWorld, bool installAPMod, bool installBepInEx)
    {
        if (installBepInEx)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPMod)
        {
            ShowInfo("Installing Pikuniku AP Mod...");
            yield return InstallAPMod();
        }

        if (installAPWorld)
        {
            ShowInfo("Installing Pikuniku APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(pikunikuAP.url, pikunikuApworld.url, pikunikuBepInEx.url);

        // Create steam_appid.txt if launch after setup is enabled
        if (launchGameToggle != null && launchGameToggle.isOn)
        {
            CreateSteamAppIdFile();
        }

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallBepInEx()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "PikunikuBepInExTemp");

        yield return downloader.DownloadAndExtract(pikunikuBepInEx, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: BepInEx extraction failed!");
            yield break;
        }

        // Move all content from extractPath to gamePath
        try
        {
            CopyAllFromExtract(extractPath, gamePath);
            ShowInfo("BepInEx installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install BepInEx\n" + e.Message);
            UnityEngine.Debug.LogError("BepInEx installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "PikunikuAPModTemp");

        yield return downloader.DownloadAndExtract(pikunikuAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: AP Mod extraction failed!");
            yield break;
        }

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        string pikunikuModPath = Path.Combine(extractPath, "PikunikuAPMod");

        try
        {
            // Create plugins directory if it doesn't exist
            Directory.CreateDirectory(pluginsPath);

            // Check if PikunikuAPMod folder exists in extraction
            if (!Directory.Exists(pikunikuModPath))
            {
                ShowInfo("ERROR: PikunikuAPMod folder not found in extraction!");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            // Move all content from PikunikuAPMod to plugins folder
            string pikunikuTargetPath = Path.Combine(pluginsPath, "PikunikuAPMod");

            // Delete if already exists
            if (Directory.Exists(pikunikuTargetPath))
                SafeDeleteDirectory(pikunikuTargetPath);

            // Copy PikunikuAPMod folder to plugins
            CopyDirectory(pikunikuModPath, pikunikuTargetPath);

            if (currentManifest != null)
                currentManifest.installedDirectories.Add(pikunikuTargetPath);

            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
            UnityEngine.Debug.LogError("AP Mod installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + pikunikuApworld.url);

        if (string.IsNullOrEmpty(pikunikuApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pikunikuApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = pikunikuApworld.url.Substring(pikunikuApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pikunikuApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pikunikuApworld.url, localPath);

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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

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
            remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGameConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchGame()
    {
        string currentGamePath = GetGamePath();
        UnityEngine.Debug.Log("LaunchGame called. GamePath: " + currentGamePath);

        if (string.IsNullOrEmpty(currentGamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string exePath = Path.Combine(currentGamePath, "Pikuniku.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found at:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            Process.Start(psi);
            UnityEngine.Debug.Log("Game launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseGame()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("Pikuniku");
            foreach (Process p in processes)
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.Dispose();
                }
            }
        }
        catch { }
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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(destination);
        }
    }

    void CopyAllFromExtract(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyAllFromExtract");

        if (!Directory.Exists(extractPath))
            return;

        foreach (string dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destDir = Path.Combine(targetPath, rel);
            try
            {
                if (Directory.Exists(destDir))
                    SafeDeleteDirectory(destDir);
                Directory.CreateDirectory(destDir);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to create dest dir: " + destDir + " - " + e.Message);
            }
        }

        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(targetPath, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest, true);
                UnityEngine.Debug.Log("Copied file: " + dest);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(dest);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to copy file: " + file + " - " + e.Message);
            }
        }

        UnityEngine.Debug.Log("END CopyAllFromExtract");
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "PikunikuInstalledFilesManifest.json");
        string json = JsonUtility.ToJson(manifest, true);

        try
        {
            File.WriteAllText(manifestPath, json);
            UnityEngine.Debug.Log("Installation manifest saved: " + manifestPath);
            UnityEngine.Debug.Log("Tracked " + manifest.installedFiles.Count + " files for future revert");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save manifest: " + e.Message);
        }
    }

    void CreateSteamAppIdFile()
    {
        try
        {
            string steamAppIdPath = Path.Combine(gamePath, "steam_appid.txt");
            File.WriteAllText(steamAppIdPath, "572890");
            UnityEngine.Debug.Log("steam_appid.txt created at: " + steamAppIdPath);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(steamAppIdPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating steam_appid.txt: " + e.Message);
        }
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetGamePath()
    {
        if (isEpic)
            return GetGameEpicPath();
        else
            return GetGameSteamPath();
    }

    string GetGameSteamPath()
    {
        string gameName = "Pikuniku";

        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", gameName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", gameName),
            Path.Combine(@"D:\Steam", "steamapps", "common", gameName),
            Path.Combine(@"D:\SteamLibrary", "steamapps", "common", gameName),
            Path.Combine(@"D:\steamapps", "common", gameName),
            Path.Combine(@"E:\Steam", "steamapps", "common", gameName),
            Path.Combine(@"E:\SteamLibrary", "steamapps", "common", gameName),
            Path.Combine(@"E:\steamapps", "common", gameName),
            Path.Combine(@"E:\Program Files (x86)", "steamapps", "common", gameName),
            Path.Combine(@"E:\Program Files", "steamapps", "common", gameName),
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + path);
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
                    string currentGamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", gameName);
                    if (Directory.Exists(currentGamePath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + currentGamePath);
                        return currentGamePath;
                    }

                    currentGamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", gameName);
                    if (Directory.Exists(currentGamePath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + currentGamePath);
                        return currentGamePath;
                    }

                    currentGamePath = Path.Combine(drive.Name, "steamapps", "common", gameName);
                    if (Directory.Exists(currentGamePath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + currentGamePath);
                        return currentGamePath;
                    }

                    currentGamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", gameName);
                    if (Directory.Exists(currentGamePath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + currentGamePath);
                        return currentGamePath;
                    }

                    currentGamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", gameName);
                    if (Directory.Exists(currentGamePath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Steam) at: " + currentGamePath);
                        return currentGamePath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Pikuniku (Steam) not found.");
        return "";
    }

    string GetGameEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Pikuniku",
            @"D:\Epic Games\Pikuniku",
            @"E:\Epic Games\Pikuniku",
            @"C:\Games\Epic\Pikuniku",
            @"D:\Games\Epic\Pikuniku",
            @"E:\Games\Epic\Pikuniku",
            @"C:\Epic\Pikuniku",
            @"D:\Epic\Pikuniku",
            @"E:\Epic\Pikuniku",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Pikuniku (Epic) at: " + path);
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
                // Search for Pikuniku manifest
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Pikuniku"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Pikuniku (Epic) at: " + epicPath);
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
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Pikuniku");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Pikuniku");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Pikuniku (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Pikuniku (Epic) not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apModUrl, string apworldUrl, string bepinexUrl)
    {
        try
        {
            string apModVersion = ExtractVersionFromUrl(apModUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, "");

            string versionFileName = "Pikuniku AP Version " + apModVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apModUrl + "\n";
            content += "Version: " + apModVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(versionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Pikuniku AP Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (pattern.IsMatch(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted old version file: " + fileName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete old version file: " + e.Message);
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
        // Thunderstore pattern: https://thunderstore.io/package/download/Author/Package/VERSION/
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        // GitHub releases pattern: /releases/download/VERSION/
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }
}
