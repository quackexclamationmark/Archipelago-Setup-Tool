using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class THSCManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData thscApworld;
    public FileDownloader.FileData thscAP1;
    public FileDownloader.FileData thscAP2;
    public FileDownloader.FileData thscAP3;
    public FileDownloader.FileData thscAP4;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "HenryStickmin";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;

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

    private static readonly string[] BackupFileNames = new string[]
    {
        "Henry.swf",
        "HenryCtM2.swf",
        "HenryFtC.swf",
        "HenryCtM.swf"
    };

    [System.Serializable]
    public class GameConfig
    {
        public string thscApworld;
        public string thscAP1;
        public string thscAP2;
        public string thscAP3;
        public string thscAP4;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

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

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        thscApworld.url = remoteConfig.thscApworld;
        thscAP1.url = remoteConfig.thscAP1;
        thscAP2.url = remoteConfig.thscAP2;
        thscAP3.url = remoteConfig.thscAP3;
        thscAP4.url = remoteConfig.thscAP4;
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
        gamePath = GetGamePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;
        bool needsGamePath = ap;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallApworld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Henry Stickmin Collection...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting Henry Stickmin Collection Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                ShowInfo("Game path not found. Please check Steam installation.");
                yield break;
            }

            string backupPath = Path.Combine(gamePath, "SaveBackup");

            // Remove the newly installed files
            foreach (string fileName in BackupFileNames)
            {
                string targetFile = Path.Combine(gamePath, fileName);

                try
                {
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                        UnityEngine.Debug.Log("Deleted installed file: " + targetFile);
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete file: " + targetFile + " - " + e.Message);
                }
            }

            // Restore the backed up files, if a backup exists
            if (Directory.Exists(backupPath))
            {
                try
                {
                    foreach (string fileName in BackupFileNames)
                    {
                        string backupFile = Path.Combine(backupPath, fileName);
                        string targetFile = Path.Combine(gamePath, fileName);

                        if (File.Exists(backupFile))
                        {
                            File.Copy(backupFile, targetFile, true);
                            UnityEngine.Debug.Log("Restored file from backup: " + targetFile);
                        }
                    }

                    SafeDeleteDirectory(backupPath);
                    UnityEngine.Debug.Log("Removed SaveBackup folder: " + backupPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to restore backup files: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("SaveBackup folder not found at: " + backupPath + ". Nothing to restore.");
            }

            ShowInfo("Henry Stickmin Collection Archipelago removed successfully!");
            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator APOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPFiles();

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
            yield return InstallApworld();

        if (installAPToggle == null || installAPToggle.isOn)
            yield return InstallAPFiles();

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Henry Stickmin Collection...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallAPFiles()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        FileDownloader.FileData[] apFiles = new FileDownloader.FileData[] { thscAP1, thscAP2, thscAP3, thscAP4 };

        foreach (FileDownloader.FileData apFile in apFiles)
        {
            if (string.IsNullOrEmpty(apFile.url))
            {
                ShowInfo("ERROR: One of the Henry Stickmin Collection AP file URLs is empty!");
                UnityEngine.Debug.LogError("AP file URL not set!");
                yield break;
            }
        }

        string tempFolder = Path.Combine(Application.persistentDataPath, "THSCAPTemp");

        if (!Directory.Exists(tempFolder))
            Directory.CreateDirectory(tempFolder);

        List<string> downloadedFiles = new List<string>();

        // Download each file one after another
        foreach (FileDownloader.FileData apFile in apFiles)
        {
            string fileName = apFile.fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = apFile.url.Substring(apFile.url.LastIndexOf('/') + 1);

                if (fileName.Contains("?"))
                    fileName = fileName.Substring(0, fileName.IndexOf("?"));

                UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
            }

            string localPath = Path.Combine(tempFolder, fileName);

            UnityEngine.Debug.Log("Downloading AP file from: " + apFile.url);
            UnityEngine.Debug.Log("Saving to: " + localPath);

            yield return DownloadFile(apFile.url, localPath);

            if (!File.Exists(localPath))
            {
                UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
                ShowInfo("ERROR: An Henry Stickmin Collection AP file failed to download!");
                yield break;
            }

            UnityEngine.Debug.Log("File downloaded successfully: " + localPath);
            downloadedFiles.Add(localPath);
        }

        // Back up existing files before overwriting them
        string backupPath = Path.Combine(gamePath, "SaveBackup");

        try
        {
            bool anyExisting = BackupFileNames.Any(name => File.Exists(Path.Combine(gamePath, name)));

            if (anyExisting && !Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
                UnityEngine.Debug.Log("Created SaveBackup folder: " + backupPath);
            }

            foreach (string fileName in BackupFileNames)
            {
                string existingFile = Path.Combine(gamePath, fileName);

                if (File.Exists(existingFile))
                {
                    string backupFile = Path.Combine(backupPath, fileName);
                    File.Copy(existingFile, backupFile, true);
                    File.Delete(existingFile);
                    UnityEngine.Debug.Log("Backed up existing file: " + existingFile + " -> " + backupFile);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error backing up existing files: " + e.Message);
            ShowInfo("ERROR: Failed to back up existing files\n" + e.Message);
            yield break;
        }

        // Move the newly downloaded files into the game folder
        try
        {
            foreach (string downloadedFile in downloadedFiles)
            {
                string fileName = Path.GetFileName(downloadedFile);
                string targetFile = Path.Combine(gamePath, fileName);

                File.Copy(downloadedFile, targetFile, true);
                UnityEngine.Debug.Log("Copied: " + targetFile);
            }

            SafeDeleteDirectory(tempFolder);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing AP files: " + e.Message);
            ShowInfo("ERROR: Failed to install Henry Stickmin Collection AP files\n" + e.Message);
            yield break;
        }

        ShowInfo("Henry Stickmin Collection Archipelago installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Apworld URL: " + thscApworld.url);

        if (string.IsNullOrEmpty(thscApworld.url))
        {
            ShowInfo("ERROR: Apworld URL is empty!");
            UnityEngine.Debug.LogError("Apworld URL not set!");
            yield break;
        }

        string fileName = thscApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = thscApworld.url.Substring(thscApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Apworld from: " + thscApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(thscApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Apworld download failed!");
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

            UnityEngine.Debug.Log("Apworld file copied to: " + target);

            ShowInfo("Apworld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Apworld: " + e.Message);
            ShowInfo("ERROR: Failed to install Apworld\n" + e.Message);
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
        UnityEngine.Debug.Log("Config marked as loaded");
    }

    void LaunchGame()
    {
        string exePath = Path.Combine(gamePath, "HenryStickmin.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("HenryStickmin.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("HenryStickmin.exe not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gamePath,
                UseShellExecute = true
            };
            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Henry Stickmin Collection launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Henry Stickmin Collection:\n" + e.Message);
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

    string GetGamePath()
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
}