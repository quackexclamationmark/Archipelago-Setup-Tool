using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class ONIManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ONI FILES")]
    public FileDownloader.FileData oniApworld;
    public FileDownloader.FileData oniAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Oxygen Not Included";
    public string epicGameFolderName = "OxygenNotIncluded";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;

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

    private string oniPath;
    private string modPath;
    private string pendingAction;
    private ONIConfig remoteConfig;
    private bool configLoaded = false;
    private Process gameProcess;
    private bool isEpic = false;

    [System.Serializable]
    public class ONIConfig
    {
        public string oniApworld;
        public string oniAP;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        oniPath = GetONIPath();
        modPath = GetModPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void OnDestroy()
    {
        CloseGame();
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
        oniPath = GetONIPath();
        modPath = GetModPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + oniPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        oniPath = GetONIPath();
        modPath = GetModPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + oniPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(oniPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyONIConfig()
    {
        if (remoteConfig == null)
            return;

        oniApworld.url = remoteConfig.oniApworld;
        oniAP.url = remoteConfig.oniAP;
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
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        oniPath = GetONIPath();
        modPath = GetModPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;

        bool needsGamePath = apmod || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(oniPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        oniPath = GetONIPath();
        modPath = GetModPath();

        CleanupProcesses();

        ShowInfo("Removing AP mods...");
        RemoveAPMods();
        ShowInfo("AP mods removed successfully!");
    }

    void RemoveAPMods()
    {
        if (string.IsNullOrEmpty(modPath))
            return;

        string archipelagoModPath = Path.Combine(modPath, "ArchipelagoNotIncluded");
        SafeDeleteDirectory(archipelagoModPath);
        DeleteOldVersionFiles();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        CreateVersionFile(oniApworld.url, oniAP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching ONI...");
            LaunchGame();
            yield return WaitForGameClose();
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator WaitForGameClose()
    {
        if (gameProcess == null)
            yield break;

        while (!gameProcess.HasExited)
        {
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("ONI has closed");
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + oniApworld.url);

        if (string.IsNullOrEmpty(oniApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = oniApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = oniApworld.url.Substring(oniApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + oniApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(oniApworld.url, localPath);

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
        request.redirectLimit = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Download error: " + request.error);
            UnityEngine.Debug.LogError("Response code: " + request.responseCode);
        }
        else
        {
            long fileSize = new System.IO.FileInfo(savePath).Length;
            UnityEngine.Debug.Log("Download complete! File size: " + fileSize + " bytes");
            
            if (fileSize < 100)
            {
                UnityEngine.Debug.LogWarning("File seems too small, might be an error page");
            }
        }
    }
}

    IEnumerator InstallAPMod()
    {
        UnityEngine.Debug.Log("START InstallAPMod");
        ShowInfo("Extracting AP Mod...");

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(modPath))
        {
            ShowInfo("ERROR: Mod path not found!");
            UnityEngine.Debug.LogError("Mod path is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "ONIAPTemp");

        if (!Directory.Exists(extractPath))
            Directory.CreateDirectory(extractPath);

        string fileName = "ArchipelagoNotIncluded.zip";
        string downloadPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading from: " + oniAP.url);
        UnityEngine.Debug.Log("Saving to: " + downloadPath);

        yield return DownloadFile(oniAP.url, downloadPath);

        if (!File.Exists(downloadPath))
        {
            ShowInfo("ERROR: Download failed!");
            UnityEngine.Debug.LogError("File not downloaded: " + downloadPath);
            yield break;
        }

        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(downloadPath, extractPath, true);
            UnityEngine.Debug.Log("Extracted ZIP file successfully");

            string sourceModPath = Path.Combine(extractPath, "ArchipelagoNotIncluded");

            if (!Directory.Exists(sourceModPath))
            {
                string[] dirs = Directory.GetDirectories(extractPath);
                if (dirs.Length > 0)
                {
                    sourceModPath = dirs[0];
                }
            }

            if (!Directory.Exists(sourceModPath))
            {
                UnityEngine.Debug.LogWarning("ArchipelagoNotIncluded folder not found in archive");
                ShowInfo("ERROR: ArchipelagoNotIncluded folder not found in the archive!");
                yield break;
            }

            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
                UnityEngine.Debug.Log("Created mod directory: " + modPath);
            }

            string targetModPath = Path.Combine(modPath, "ArchipelagoNotIncluded");

            if (Directory.Exists(targetModPath))
            {
                SafeDeleteDirectory(targetModPath);
            }

            CopyDirectory(sourceModPath, targetModPath);
            UnityEngine.Debug.Log("AP Mod copied to: " + targetModPath);

            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing AP Mod: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod!\n" + e.Message);
        }

        try
        {
            SafeDeleteDirectory(extractPath);
            SafeDeleteFile(downloadPath);
        }
        catch { }

        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    IEnumerator APModOnlyFlow()
    {
        oniPath = GetONIPath();

        if (string.IsNullOrEmpty(oniPath))
            yield break;

        modPath = GetModPath();

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(oniApworld.url, oniAP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching ONI...");
            LaunchGame();
            yield return WaitForGameClose();
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<ONIConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyONIConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        oniPath = GetONIPath();
        UpdatePlatformStatus();
    }

    void LaunchGame()
    {
        try
        {
            string exePath = Path.Combine(oniPath, "OxygenNotIncluded.exe");

            if (File.Exists(exePath))
            {
                gameProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("ONI launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("OxygenNotIncluded.exe not found at: " + exePath);
                ShowInfo("ERROR: OxygenNotIncluded.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch ONI: " + e.Message);
            ShowInfo("ERROR: Failed to launch ONI!");
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

    void CleanupProcesses()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("OxygenNotIncluded");
            foreach (Process p in processes)
            {
                try
                {
                    p.Kill();
                }
                catch { }
            }
        }
        catch { }
    }

    void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                UnityEngine.Debug.Log("Deleted file: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete file: " + path + " - " + e.Message);
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
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                UnityEngine.Debug.Log("Copied file: " + Path.GetFileName(file));
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
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

    void CreateVersionFile(string apworldUrl, string apUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(modPath))
                return;

            string apVersion = ExtractVersionFromUrl(apUrl);

            string versionFileName = "ONI Archipelago Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "\n";
            content += "=== ONI AP ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(modPath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
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
            if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ONI Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(modPath);
            foreach (string file in files)
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

    string ExtractVersionFromUrl(string url)
    {
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetONIPath()
    {
        if (isEpic)
            return GetONIEpicPath();
        else
            return GetONISteamPath();
    }

    string GetModPath()
    {
        string userName = System.Environment.UserName;

        string primaryPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "Klei", "OxygenNotIncluded", "mods", "Local"
        );

        try
        {
            if (!Directory.Exists(primaryPath))
            {
                Directory.CreateDirectory(primaryPath);
                UnityEngine.Debug.Log("Created mod directory: " + primaryPath);
            }
            return primaryPath;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Cannot use primary mod path: " + e.Message);
        }

        string oneDrivePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "OneDrive", "Documents", "Klei", "OxygenNotIncluded", "mods", "Local"
        );

        if (Directory.Exists(oneDrivePath))
        {
            UnityEngine.Debug.Log("Using OneDrive mod path: " + oneDrivePath);
            return oneDrivePath;
        }

        try
        {
            Directory.CreateDirectory(primaryPath);
            UnityEngine.Debug.Log("Created primary mod directory: " + primaryPath);
            return primaryPath;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to create mod directories: " + e.Message);
            return "";
        }
    }

    string GetONISteamPath()
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

    string GetONIEpicPath()
    {
        string[] quickPaths = new string[]
       {
            @"C:\Program Files\Epic Games\OxygenNotIncluded",
            @"C:\Games\Epic\OxygenNotIncluded",
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
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("OxygenNotIncluded") || content.Contains("OxygenNotIncluded"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
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
}
