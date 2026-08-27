using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class LOPManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    // Steam/Xbox zips and APWorld file (URLs populated from remote config)
    public FileDownloader.FileData steamFiles;
    public FileDownloader.FileData xboxFiles;
    public FileDownloader.FileData apworld;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button xboxButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Lies of P";
    public string xboxGameFolderName = "Lies of P";

    [Header("FEATURE TOGGLES")]
    // Single toggle for installing the AP mod package (Steam or Xbox chosen by platform buttons)
    public Toggle installAPModToggle;
    public Toggle installAPWorldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;

    // platform flag: false = Steam, true = Xbox
    private bool isXbox = false;

    [System.Serializable]
    public class GameConfig
    {
        public string lopApworld;
        public string lopAPSteam;
        public string lopAPXBOX;
        public string[] steamSearchPaths;
        public string[] xboxSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
    }

    void Start()
    {
        // Platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);
        if (xboxButton != null)
            xboxButton.onClick.AddListener(OnXboxButtonClicked);

        // default to Steam
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

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        steamFiles.url = remoteConfig.lopAPSteam;
        xboxFiles.url = remoteConfig.lopAPXBOX;
        apworld.url = remoteConfig.lopApworld;
    }

    // PLATFORM selection handlers
    void OnSteamButtonClicked() { SelectSteam(); }
    void OnXboxButtonClicked() { SelectXbox(); }

    void SelectSteam()
    {
        isXbox = false;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + gamePath);
    }

    void SelectXbox()
    {
        isXbox = true;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Xbox - Path: " + gamePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isXbox ? "Xbox" : "Steam";
            string status = string.IsNullOrEmpty(gamePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install the selected components?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove all mods and files installed by this tool?", "Revert");
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
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool wantAPWorld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool wantAPMod = installAPModToggle == null || installAPModToggle.isOn;

        // If installing AP mod we need a game path (Steam or Xbox) depending on selected platform
        bool needsGamePath = wantAPMod;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            string platform = isXbox ? "Xbox" : "Steam";
            ShowInfo("Game path not found for " + platform + ". Please check your installation.");
            return;
        }

        int count = (wantAPWorld ? 1 : 0) + (wantAPMod ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        // If only APWorld selected -> APWorldOnlyFlow
        if (wantAPWorld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        // If only APMod selected -> ModsOnlyFlow
        if (wantAPMod && count == 1)
        {
            StartCoroutine(ModsOnlyFlow());
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Lies of P...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupWithTracking()
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow();

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_LOP.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found. Cannot revert.\nPlease reinstall the game if needed.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_LOP.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found.");
            yield break;
        }

        ShowInfo("Reverting modifications...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string json = File.ReadAllText(manifestPath);
            InstalledFilesManifest manifest = JsonUtility.FromJson<InstalledFilesManifest>(json);

            if (string.IsNullOrEmpty(manifest.gameInstallPath))
            {
                ShowInfo("Error: Game install path not found in manifest.");
                yield break;
            }

            int successCount = 0;
            int errorCount = 0;

            foreach (string filePath in manifest.installedFiles)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        successCount++;
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete: " + filePath + " - " + e.Message);
                    errorCount++;
                }
            }

            // Remove any empty directories under the game path that may have been created
            RemoveEmptyDirectories(manifest.gameInstallPath);

            // Delete version files
            DeleteOldVersionFiles(manifest.gameInstallPath);

            ShowInfo($"Mods removed successfully!\n({successCount} files deleted)");
            UnityEngine.Debug.Log($"Revert complete: {successCount} files deleted, {errorCount} errors");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
        finally
        {
            try
            {
                File.Delete(manifestPath);
            }
            catch { }
        }
    }

    void RemoveEmptyDirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return;

        try
        {
            // Walk directories deepest-first
            foreach (string dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
            {
                try
                {
                    if (Directory.Exists(dir) &&
                        Directory.GetFiles(dir).Length == 0 &&
                        Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    IEnumerator ModsOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        // Install the selected platform's AP mod
        if (!isXbox)
            yield return InstallSteamVersion();
        else
            yield return InstallXboxVersion();

        SaveInstalledFilesManifest(currentManifest);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        // Install platform-specific AP mod if selected
        if (installAPModToggle != null && installAPModToggle.isOn)
        {
            if (!isXbox)
                yield return InstallSteamVersion();
            else
                yield return InstallXboxVersion();
        }

        // Use the URL we actually installed for the version file (choose based on platform)
        string usedModUrl = (installAPModToggle != null && installAPModToggle.isOn) ? (isXbox ? xboxFiles.url : steamFiles.url) : "";

        CreateVersionFile(usedModUrl, apworld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallSteamVersion()
    {
        // Wait for config to be loaded so URL is populated
        yield return new WaitUntil(() => configLoaded);

        if (string.IsNullOrEmpty(steamFiles.url))
        {
            ShowInfo("ERROR: Steam package URL is empty!");
            UnityEngine.Debug.LogError("Steam package URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "LOP_Steam_Temp");
        yield return downloader.DownloadAndExtract(steamFiles, Application.persistentDataPath, extractPath);

        string steamVersionFolder = Path.Combine(extractPath, "SteamVersion");
        if (!Directory.Exists(steamVersionFolder))
        {
            ShowInfo("ERROR: SteamVersion folder not found in the extracted archive!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Move all files from SteamVersion into the game directory, tracking each file
        MoveDirectoryAndTrack(steamVersionFolder, gamePath);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Steam version (AP mod) installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallXboxVersion()
    {
        // Wait for config to be loaded so URL is populated
        yield return new WaitUntil(() => configLoaded);

        if (string.IsNullOrEmpty(xboxFiles.url))
        {
            ShowInfo("ERROR: Xbox package URL is empty!");
            UnityEngine.Debug.LogError("Xbox package URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "LOP_Xbox_Temp");
        yield return downloader.DownloadAndExtract(xboxFiles, Application.persistentDataPath, extractPath);

        string xboxVersionFolder = Path.Combine(extractPath, "XboxVersion");
        if (!Directory.Exists(xboxVersionFolder))
        {
            ShowInfo("ERROR: XboxVersion folder not found in the extracted archive!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Move all files from XboxVersion into the game directory, tracking each file
        MoveDirectoryAndTrack(xboxVersionFolder, gamePath);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Xbox version (AP mod) installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + apworld.url);

        if (string.IsNullOrEmpty(apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

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
            yield break;
        }

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
        // Remote config location in the repository (raw)
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
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

        configLoaded = true;

        gamePath = GetGamePath();
        UpdatePlatformStatus();
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

        string[] possiblePaths = new string[]
        {
            Path.Combine(currentGamePath, "LOP.exe"),
            Path.Combine(currentGamePath, "Binaries", "Win64", "LOP.exe"),
        };

        string exePath = "";
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                exePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            ShowInfo("Game executable not found. Checked:\n" + string.Join("\n", possiblePaths));
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            gameProcess = Process.Start(exePath);
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
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
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

    void MoveDirectoryAndTrack(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        // Ensure target exists
        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(target, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);
        }
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_LOP.json");
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

    // Return path depending on selected platform
    string GetGamePath()
    {
        return isXbox ? GetXboxPath() : GetSteamPath();
    }

    string GetSteamPath()
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

    string GetXboxPath()
    {
        if (remoteConfig != null && remoteConfig.xboxSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.xboxSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            // Build: drive + relativePath + xboxGameFolderName
                            string path = Path.Combine(drive.Name, relativePath, xboxGameFolderName);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Game (Xbox, via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Xbox) not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string modUrl, string apworldUrl)
    {
        try
        {
            string modVersion = ExtractVersionFromUrl(modUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "LiesOfP APMod Version " + (modVersion != "Unknown" ? modVersion : apworldVersion) + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + modUrl + "\n";
            content += "Version: " + modVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            // Clean old version files first
            DeleteOldVersionFiles(gamePath);

            string coreVersionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(coreVersionPath, content);
            UnityEngine.Debug.Log("Version file created: " + coreVersionPath);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(coreVersionPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles(string targetGamePath)
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"LiesOfP APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(targetGamePath);
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
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        // Pattern for Thunderstore: https://thunderstore.io/package/download/Author/Package/VERSION/
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        // Pattern for GitHub releases: /releases/download/VERSION/
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        // Try to extract a filename or folder-based version token
        try
        {
            string last = url.TrimEnd('/').Split('/').Last();
            if (!string.IsNullOrEmpty(last))
            {
                // If it's a version-looking string, return it; otherwise return filename
                return last;
            }
        }
        catch { }

        return "Unknown";
    }
}