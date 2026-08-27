using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class SMBManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData smbApworld;
    public FileDownloader.FileData smbAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Super Meat Boy";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;
    public Toggle launchClientToggle;

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
    private Process clientProcess;
    private string gamePath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    private const string SaveFileName = "savegame.dat";
    private const string UserDataFolderName = "UserData";
    private const string ClientExeName = "SMB_Archipelago_Client.exe";
    private const string GameExeName = "SuperMeatBoy.exe";

    [System.Serializable]
    public class GameConfig
    {
        public string smbApworld;
        public string smbAP;
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

        if (launchClientToggle != null)
            launchClientToggle.isOn = false;

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

        smbApworld.url = remoteConfig.smbApworld;
        smbAP.url = remoteConfig.smbAP;
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

        if (launchGameToggle != null && launchGameToggle.isOn)
        {
            ShowInfo("Launching Super Meat Boy...");
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
        ShowInfo("Reverting Super Meat Boy Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                ShowInfo("Game path not found. Please check Steam installation.");
                yield break;
            }

            string backupPath = Path.Combine(gamePath, "SaveBackup");
            string userDataPath = Path.Combine(gamePath, UserDataFolderName);
            string clientExePath = Path.Combine(gamePath, ClientExeName);
            string savegamePath = Path.Combine(userDataPath, SaveFileName);
            string backupSavegamePath = Path.Combine(backupPath, SaveFileName);

            // Remove the installed AP client exe
            try
            {
                if (File.Exists(clientExePath))
                {
                    File.Delete(clientExePath);
                    UnityEngine.Debug.Log("Deleted installed file: " + clientExePath);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to delete file: " + clientExePath + " - " + e.Message);
            }

            // Restore the backed up vanilla save, if a backup exists.
            // The current savegame.dat in UserData (if present) is always deleted
            // first, right before restoring, to guarantee it never lingers.
            if (Directory.Exists(backupPath))
            {
                try
                {
                    if (File.Exists(savegamePath))
                    {
                        File.Delete(savegamePath);
                        UnityEngine.Debug.Log("Deleted current save file: " + savegamePath);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("No savegame.dat found in UserData. Skipping deletion, continuing revert.");
                    }

                    if (File.Exists(backupSavegamePath))
                    {
                        if (!Directory.Exists(userDataPath))
                            Directory.CreateDirectory(userDataPath);

                        File.Copy(backupSavegamePath, savegamePath, true);
                        UnityEngine.Debug.Log("Restored save file from backup: " + savegamePath);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("No backed up savegame.dat found in SaveBackup. Nothing to restore.");
                    }

                    SafeDeleteDirectory(backupPath);
                    UnityEngine.Debug.Log("Removed SaveBackup folder: " + backupPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete/restore save file: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                // No backup to restore, but still remove the current save if present.
                try
                {
                    if (File.Exists(savegamePath))
                    {
                        File.Delete(savegamePath);
                        UnityEngine.Debug.Log("Deleted current save file: " + savegamePath);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("No savegame.dat found in UserData. Skipping deletion, continuing revert.");
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete save file: " + savegamePath + " - " + e.Message);
                }

                UnityEngine.Debug.LogWarning("SaveBackup folder not found at: " + backupPath + ". Nothing to restore.");
            }

            ShowInfo("Super Meat Boy Archipelago removed successfully!");
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

        yield return InstallAP();

        if (launchGameToggle != null && launchGameToggle.isOn)
            LaunchGame();

        if (launchClientToggle != null && launchClientToggle.isOn)
            LaunchClient();
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
            yield return InstallApworld();

        if (installAPToggle == null || installAPToggle.isOn)
            yield return InstallAP();

        yield return new WaitForSeconds(2f);

        if (launchGameToggle != null && launchGameToggle.isOn)
        {
            ShowInfo("Launching Super Meat Boy...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }

        if (launchClientToggle != null && launchClientToggle.isOn)
        {
            ShowInfo("Launching SMB Archipelago Client...");
            yield return new WaitForSeconds(1f);
            LaunchClient();
        }
    }

    IEnumerator InstallAP()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(smbAP.url))
        {
            ShowInfo("ERROR: Super Meat Boy AP Client URL is empty!");
            UnityEngine.Debug.LogError("AP Client URL not set!");
            yield break;
        }

        string fileName = smbAP.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = smbAP.url.Substring(smbAP.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading AP Client from: " + smbAP.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(smbAP.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Super Meat Boy AP Client failed to download!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // Back up the vanilla save before doing anything else
        string userDataPath = Path.Combine(gamePath, UserDataFolderName);
        string savegamePath = Path.Combine(userDataPath, SaveFileName);
        string backupPath = Path.Combine(gamePath, "SaveBackup");
        string backupSavegamePath = Path.Combine(backupPath, SaveFileName);

        try
        {
            if (File.Exists(savegamePath))
            {
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                    UnityEngine.Debug.Log("Created SaveBackup folder: " + backupPath);
                }

                File.Copy(savegamePath, backupSavegamePath, true);
                UnityEngine.Debug.Log("Backed up existing save file: " + savegamePath + " -> " + backupSavegamePath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("No savegame.dat found in UserData to back up.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error backing up save file: " + e.Message);
            ShowInfo("ERROR: Failed to back up save file\n" + e.Message);
            yield break;
        }

        // Copy the newly downloaded client exe into the game folder
        string targetExePath = Path.Combine(gamePath, ClientExeName);

        try
        {
            if (File.Exists(targetExePath))
            {
                File.Delete(targetExePath);
                UnityEngine.Debug.Log("Deleted old AP Client exe");
            }

            File.Copy(localPath, targetExePath, true);
            UnityEngine.Debug.Log("Copied AP Client exe: " + targetExePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing AP Client: " + e.Message);
            ShowInfo("ERROR: Failed to install Super Meat Boy AP Client\n" + e.Message);
            yield break;
        }

        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary AP Client file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary AP Client file: " + e.Message);
        }

        ShowInfo("Super Meat Boy Archipelago installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Apworld URL: " + smbApworld.url);

        if (string.IsNullOrEmpty(smbApworld.url))
        {
            ShowInfo("ERROR: Apworld URL is empty!");
            UnityEngine.Debug.LogError("Apworld URL not set!");
            yield break;
        }

        string fileName = smbApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = smbApworld.url.Substring(smbApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Apworld from: " + smbApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(smbApworld.url, localPath);

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
        string exePath = Path.Combine(gamePath, GameExeName);

        if (!File.Exists(exePath))
        {
            ShowInfo("SuperMeatBoy.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("SuperMeatBoy.exe not found!");
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
            UnityEngine.Debug.Log("Super Meat Boy launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Super Meat Boy:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void LaunchClient()
    {
        string exePath = Path.Combine(gamePath, ClientExeName);

        if (!File.Exists(exePath))
        {
            ShowInfo("SMB_Archipelago_Client.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("SMB_Archipelago_Client.exe not found!");
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
            clientProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("SMB Archipelago Client launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching SMB Archipelago Client:\n" + e.Message);
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

        try
        {
            if (clientProcess != null && !clientProcess.HasExited)
            {
                clientProcess.Kill();
                clientProcess.Dispose();
                clientProcess = null;
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