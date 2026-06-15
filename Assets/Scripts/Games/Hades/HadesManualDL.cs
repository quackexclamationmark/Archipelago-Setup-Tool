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

    private string backupPath;
    private const string BACKUP_FOLDER = "HadesSetupToolBackup";

    [System.Serializable]
    public class HadesConfig
    {
        public string hadesApworld;
        public string hadesImporter;
    }

    void Start()
    {
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

    void ApplyHadesConfig()
    {
        if (remoteConfig == null)
            return;

        hadesApworld.url = remoteConfig.hadesApworld;
        hadesImporter.url = remoteConfig.hadesImporter;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup Hades integration?", "Setup");
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
            ShowInfo("Hades path not found. Please check Steam installation.");
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

        // Backup important files first
        yield return BackupImportantFiles();

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

    private IEnumerator BackupImportantFiles()
    {
        UnityEngine.Debug.Log("START: Backup Important Files");

        string contentPath = Path.Combine(hadesPath, "Content");
        Directory.CreateDirectory(backupPath);

        string[] filesToBackup = new string[]
        {
            "ConditionalItemData.lua",
            "Main.lua",
            "RoomManager.lua",
            "TextLineSets.lua"
        };

        foreach (string fileName in filesToBackup)
        {
            string sourceFile = Path.Combine(contentPath, fileName);
            string backupFile = Path.Combine(backupPath, fileName);

            if (File.Exists(sourceFile))
            {
                try
                {
                    File.Copy(sourceFile, backupFile, true);
                    UnityEngine.Debug.Log("Backed up: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not backup " + fileName + ": " + e.Message);
                }
            }
        }

        UnityEngine.Debug.Log("END: Backup Important Files");
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

        // Delete files from Hades root
        string[] rootFilesToDelete = new string[]
        {
            ".gitignore",
            "README.md",
            "StyxScribe.py",
            "SubsumeHades.py",
            "SubsumePyre.py"
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

        // Delete directories from Hades\Content
        string contentPath = Path.Combine(hadesPath, "Content");
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

        // Restore backed-up files
        ShowInfo("Restoring backed-up files...");
        yield return RestoreBackupedFiles();

        ShowInfo("Hades revert complete!");
        UnityEngine.Debug.Log("END: Hades Revert");

        yield return null;
    }

    private IEnumerator RestoreBackupedFiles()
    {
        UnityEngine.Debug.Log("START: Restore Backuped Files");

        if (!Directory.Exists(backupPath))
        {
            UnityEngine.Debug.LogWarning("Backup folder not found: " + backupPath);
            yield break;
        }

        string contentPath = Path.Combine(hadesPath, "Content");
        Directory.CreateDirectory(contentPath);

        string[] filesToRestore = new string[]
        {
            "ConditionalItemData.lua",
            "Main.lua",
            "RoomManager.lua",
            "TextLineSets.lua"
        };

        foreach (string fileName in filesToRestore)
        {
            string backupFile = Path.Combine(backupPath, fileName);
            string targetFile = Path.Combine(contentPath, fileName);

            if (File.Exists(backupFile))
            {
                try
                {
                    File.Copy(backupFile, targetFile, true);
                    UnityEngine.Debug.Log("Restored: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Could not restore " + fileName + ": " + e.Message);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Backup file not found: " + fileName);
            }
        }

        UnityEngine.Debug.Log("END: Restore Backuped Files");
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

    string GetHadesPath()
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
                    return path;
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
                        return hadesPath;

                    hadesPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                        return hadesPath;

                    hadesPath = Path.Combine(drive.Name, "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                        return hadesPath;

                    hadesPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                        return hadesPath;

                    hadesPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Hades");
                    if (Directory.Exists(hadesPath))
                        return hadesPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
