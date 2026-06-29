using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class HadesManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("HADES FILES")]
    public FileDownloader.FileData hadesApworld;
    public FileDownloader.FileData hadesImporter;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installImporterToggle;

    [Header("BUTTONS")]
    public Button setupButton;
    public Button revertButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string hadesPath;
    private string pendingAction;
    private HadesConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    private string backupPath;
    private const string BACKUP_FOLDER = "HadesSetupToolBackup";
    private const string BACKUP_SCRIPTS_FOLDER = "HadesScriptsBackup";

    [System.Serializable]
    public class HadesConfig
    {
        public string hadesApworld;
        public string hadesImporter;
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

        hadesPath = GetHadesPath();
        backupPath = Path.Combine(Application.persistentDataPath, BACKUP_FOLDER);
        StartCoroutine(LoadRemoteConfig());

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

        if (setupButton != null)
            setupButton.onClick.AddListener(RunSetup);

        if (revertButton != null)
            revertButton.onClick.AddListener(RevertAll);

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installImporterToggle != null)
            installImporterToggle.isOn = true;
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
        hadesPath = GetHadesPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + hadesPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        hadesPath = GetHadesPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + hadesPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(hadesPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyHadesConfig()
    {
        if (remoteConfig == null)
            return;

        hadesApworld.url = remoteConfig.hadesApworld;
        hadesImporter.url = remoteConfig.hadesImporter;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup Hades AP Mod", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert all changes?\nThis will restore backups and remove mod files.", "Revert");
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
                StartCoroutine(ExecuteRevertCoroutine());
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        hadesPath = GetHadesPath();

        if (string.IsNullOrEmpty(hadesPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Hades not found in " + platform + ". Please check installation.");
            return;
        }

        bool installApworld = installApworldToggle == null || installApworldToggle.isOn;
        bool installImporter = installImporterToggle == null || installImporterToggle.isOn;

        if (!installApworld && !installImporter)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        StartCoroutine(InstallFlow(installApworld, installImporter));
    }

    private IEnumerator InstallFlow(bool installApworld, bool installImporter)
    {
        ShowInfo("Installing Hades integration...");
        UnityEngine.Debug.Log("START: Hades Setup");

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        // Backup Scripts directory first (full backup)
        yield return BackupScriptsDirectory();

        // Install APWorld if toggled
        if (installApworld)
        {
            ShowInfo("Installing Hades APWorld...");
            yield return InstallHadesApworld();
        }

        // Install Importer if toggled
        if (installImporter)
        {
            ShowInfo("Installing Hades Importer...");
            yield return InstallAndRunImporter();
            // Ne pas afficher "setup complete" ici, l'utilisateur fermera le panneau lui-même
            yield break;
        }

        ShowInfo("Hades setup complete!");
        UnityEngine.Debug.Log("END: Hades Setup");

        yield return null;
    }

    private IEnumerator BackupScriptsDirectory()
    {
        UnityEngine.Debug.Log("START: Backup Scripts Directory");

        string scriptsPath = Path.Combine(hadesPath, "Content", "Scripts");
        string backupScriptsPath = Path.Combine(Application.persistentDataPath, BACKUP_SCRIPTS_FOLDER);

        // Delete old backup if it exists
        if (Directory.Exists(backupScriptsPath))
        {
            try
            {
                Directory.Delete(backupScriptsPath, true);
                UnityEngine.Debug.Log("Deleted old scripts backup");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete old backup: " + e.Message);
            }
        }

        // Create backup of entire Scripts folder
        if (Directory.Exists(scriptsPath))
        {
            try
            {
                CopyDirectory(scriptsPath, backupScriptsPath);
                UnityEngine.Debug.Log("Backed up entire Scripts directory to: " + backupScriptsPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Could not backup Scripts directory: " + e.Message);
                ShowInfo("ERROR: Could not backup Scripts directory\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Scripts directory not found at: " + scriptsPath);
        }

        UnityEngine.Debug.Log("END: Backup Scripts Directory");
        yield return null;
    }

    private IEnumerator InstallHadesApworld()
    {
        if (string.IsNullOrEmpty(hadesApworld.url))
        {
            ShowInfo("ERROR: Hades APWorld URL is empty!");
            UnityEngine.Debug.LogError("Hades APWorld URL not set!");
            yield break;
        }

        string fileName = hadesApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = hadesApworld.url.Substring(hadesApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Hades APWorld from: " + hadesApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(hadesApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Hades APWorld download failed!");
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
            UnityEngine.Debug.Log("Hades APWorld copied to: " + target);
            ShowInfo("Hades APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Hades APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install Hades APWorld\n" + e.Message);
        }
    }

    private IEnumerator InstallAndRunImporter()
    {
        if (string.IsNullOrEmpty(hadesImporter.url))
        {
            ShowInfo("ERROR: Hades Importer URL is empty!");
            UnityEngine.Debug.LogError("Hades Importer URL not set!");
            yield break;
        }

        string fileName = "Installer.exe";
        string tempPath = Path.Combine(Application.persistentDataPath, fileName);
        string installerPath = Path.Combine(hadesPath, fileName);

        UnityEngine.Debug.Log("Downloading Hades Importer from: " + hadesImporter.url);
        UnityEngine.Debug.Log("Saving to: " + tempPath);

        yield return DownloadFile(hadesImporter.url, tempPath);

        if (!File.Exists(tempPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + tempPath);
            ShowInfo("ERROR: Hades Importer download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("Installer downloaded successfully: " + tempPath);

        // Move to Hades root directory
        if (!MoveInstallerFile(tempPath, installerPath))
            yield break;

        // Launch the installer
        yield return LaunchInstallerAsync(installerPath);
    }

    private bool MoveInstallerFile(string sourcePath, string destPath)
    {
        try
        {
            if (File.Exists(destPath))
                File.Delete(destPath);

            File.Move(sourcePath, destPath);
            UnityEngine.Debug.Log("Installer moved to: " + destPath);
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to move installer: " + e.Message);
            ShowInfo("ERROR: Failed to prepare Installer\n" + e.Message);
            return false;
        }
    }

    private IEnumerator LaunchInstallerAsync(string installerPath)
    {
        try
        {
            UnityEngine.Debug.Log("Executing Installer: " + installerPath);

            ProcessStartInfo psi = new ProcessStartInfo(installerPath);
            psi.UseShellExecute = true;
            psi.Verb = "runas"; // Lance en administrateur
            psi.WorkingDirectory = Path.GetDirectoryName(installerPath);
            psi.CreateNoWindow = false;

            Process process = Process.Start(psi);

            ShowInfo("Installer launched as administrator! Please follow the installation steps.");
            UnityEngine.Debug.Log("Process ID: " + process.Id);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to execute Installer: " + e.Message);
            UnityEngine.Debug.LogError("Stack: " + e.StackTrace);
            ShowInfo("ERROR: Failed to launch Installer\n" + e.Message);
        }

        yield return null;
    }

    private IEnumerator ExecuteRevertCoroutine()
    {
        UnityEngine.Debug.Log("START: Hades Revert");

        hadesPath = GetHadesPath();

        if (string.IsNullOrEmpty(hadesPath))
        {
            ShowInfo("Hades path not found.");
            yield break;
        }

        ShowInfo("Reverting Hades changes...");

        string contentPath = Path.Combine(hadesPath, "Content");
        string scriptsPath = Path.Combine(contentPath, "Scripts");
        string backupScriptsPath = Path.Combine(Application.persistentDataPath, BACKUP_SCRIPTS_FOLDER);

        // STEP 1: Delete files from Hades root
        UnityEngine.Debug.Log("STEP 1: Deleting root files...");
        string[] rootFilesToDelete = new string[]
        {
            ".gitignore",
            "README.md",
            "StyxScribe.py",
            "SubsumeHades.py",
            "SubsumePyre.py",
            "Installer.exe"
        };

        foreach (string fileName in rootFilesToDelete)
        {
            string filePath = Path.Combine(hadesPath, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                    UnityEngine.Debug.Log("Deleted: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete " + fileName + ": " + e.Message);
                }
            }
        }

        // STEP 2: Delete files from Hades\Content
        UnityEngine.Debug.Log("STEP 2: Deleting Content files...");
        string[] contentFilesToDelete = new string[]
        {
            "modimporter.exe",
            "modimporter.log.txt"
        };

        foreach (string fileName in contentFilesToDelete)
        {
            string filePath = Path.Combine(contentPath, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                    UnityEngine.Debug.Log("Deleted from Content: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete " + fileName + ": " + e.Message);
                }
            }
        }

        // STEP 3: Delete modified directories
        UnityEngine.Debug.Log("STEP 3: Deleting modified directories...");
        string[] dirsToDelete = new string[]
        {
            "Mods",
            "StyxScribeScripts",
            "Backup"
        };

        foreach (string dirName in dirsToDelete)
        {
            string dirPath = Path.Combine(contentPath, dirName);
            if (Directory.Exists(dirPath))
            {
                yield return SafeDeleteDirectoryAsync(dirPath);
            }
        }

        // STEP 4: Delete modified Scripts directory and restore backup
        UnityEngine.Debug.Log("STEP 4: Restoring Scripts directory...");
        ShowInfo("Restoring Scripts directory...");

        if (Directory.Exists(scriptsPath))
        {
            yield return SafeDeleteDirectoryAsync(scriptsPath);
        }

        if (Directory.Exists(backupScriptsPath))
        {
            try
            {
                CopyDirectory(backupScriptsPath, scriptsPath);
                UnityEngine.Debug.Log("Restored Scripts directory from backup");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Could not restore Scripts directory: " + e.Message);
                ShowInfo("ERROR: Could not restore Scripts directory\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Scripts backup not found at: " + backupScriptsPath);
        }

        ShowInfo("Revert complete!");
        UnityEngine.Debug.Log("END: Hades Revert");

        yield return null;
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

    IEnumerator SafeDeleteDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            UnityEngine.Debug.Log("Directory doesn't exist, skipping: " + path);
            yield break;
        }

        UnityEngine.Debug.Log("Starting deletion of: " + path);

        yield return new WaitForSeconds(0.3f);

        bool success = false;
        string errorMessage = "";

        try
        {
            DeleteDirectoryWithRetry(path, maxRetries: 10);
            success = true;
        }
        catch (System.Exception e)
        {
            errorMessage = e.Message;
        }

        if (success)
        {
            UnityEngine.Debug.Log("Directory deleted successfully: " + path);
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to delete directory: " + path + " - " + errorMessage);
            ShowInfo("ERROR: Could not delete " + path);
        }
    }

    void DeleteDirectoryWithRetry(string path, int maxRetries = 10)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Directory already deleted: " + path);
                    return;
                }

                string[] files = Directory.GetFiles(path);
                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted file: " + file);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete file: " + file + " - " + e.Message);
                    }
                }

                string[] directories = Directory.GetDirectories(path);
                foreach (string dir in directories)
                {
                    try
                    {
                        DeleteDirectoryWithRetry(dir, maxRetries);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete subdirectory: " + dir + " - " + e.Message);
                    }
                }

                Directory.Delete(path, false);
                UnityEngine.Debug.Log("Successfully deleted directory: " + path);
                return;
            }
            catch (System.Exception e)
            {
                if (attempt < maxRetries - 1)
                {
                    UnityEngine.Debug.LogWarning($"Delete attempt {attempt + 1}/{maxRetries} failed for {path}: {e.Message}");
                    System.Threading.Thread.Sleep(200);
                }
                else
                {
                    UnityEngine.Debug.LogError($"All {maxRetries} attempts failed for {path}: {e.Message}");
                    throw;
                }
            }
        }
    }

    void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            UnityEngine.Debug.LogWarning("Source directory does not exist: " + source);
            return;
        }

        Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
            throw;
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
            remoteConfig = JsonUtility.FromJson<HadesConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyHadesConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetHadesPath()
    {
        if (isEpic)
            return GetHadesEpicPath();
        else
            return GetHadesSteamPath();
    }

    string GetHadesSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Hades"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Hades"),
            @"D:\Steam\steamapps\common\Hades",
            @"D:\SteamLibrary\steamapps\common\Hades",
            @"D:\steamapps\common\Hades",
            @"E:\Steam\steamapps\common\Hades",
            @"E:\SteamLibrary\steamapps\common\Hades",
            @"E:\steamapps\common\Hades",
            @"E:\Program Files (x86)\steamapps\common\Hades",
            @"E:\Program Files\steamapps\common\Hades",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Hades (Steam) at: " + path);
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
                    string hadesPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Steam) at: " + hadesPath);
                        return hadesPath;
                    }

                    hadesPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Steam) at: " + hadesPath);
                        return hadesPath;
                    }

                    hadesPath = Path.Combine(drive.Name, "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Steam) at: " + hadesPath);
                        return hadesPath;
                    }

                    hadesPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Steam) at: " + hadesPath);
                        return hadesPath;
                    }

                    hadesPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Steam) at: " + hadesPath);
                        return hadesPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Hades (Steam) not found.");
        return "";
    }

    string GetHadesEpicPath()
    {
        string[] quickPaths = new string[]
        {
        @"C:\Program Files\Epic Games\Hades",
        @"D:\Epic Games\Hades",      
        @"E:\Epic Games\Hades",
        @"C:\Games\Epic\Hades",
        @"D:\Games\Epic\Hades",
        @"E:\Games\Epic\Hades",
        @"C:\Epic\Hades",
        @"D:\Epic\Hades",
        @"E:\Epic\Hades",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Hades (Epic) at: " + path);
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
                // Cherche le manifest pour Hades
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Hades") || content.Contains("hades"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Hades (Epic) at: " + epicPath);
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
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Hades");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Hades");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Hades (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Hades (Epic) not found.");
        return "";
    }
}
