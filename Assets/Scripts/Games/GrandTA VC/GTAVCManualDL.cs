using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

public class GTAVCManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData gtavcApworld;
    public FileDownloader.FileData gtavcASI;
    public FileDownloader.FileData gtavcWidescreen;
    public FileDownloader.FileData gtavcCleo;
    public FileDownloader.FileData gtavcExe;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button rockstarButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Grand Theft Auto Vice City";
    public string[] rockstarGameFolderNames = new string[]
    {
        "Grand Theft Auto Vice City",
        "GTA Vice City"
    };

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installASIToggle;
    public Toggle installWidescreenToggle;
    public Toggle installCleoToggle;
    public Toggle installExeToggle;

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
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private bool isRockstar = false;
    private InstalledFilesManifest currentManifest;
    private bool lastApWorldInstallSuccess = false;

    private const string ExeBackupFolderName = ".exeBackup";

    private static readonly string[] RevertFolders = new string[]
    {
        "CLEO",
        "scripts"
    };

    private static readonly string[] RevertFiles = new string[]
    {
        "cleo.log",
        "d3d8.dll",
        "dinput8.dll",
        "gtavc_ap_asi.log",
        "GtaVcAp.VC.asi",
        "VC.CLEO.asi"
    };

    [System.Serializable]
    public class GameConfig
    {
        public string gtavcApworld;
        public string gtavcASI;
        public string gtavcWidescreen;
        public string gtavcCleo;
        public string gtavcExe;
        public string[] steamSearchPaths;
        public string[] rockstarSearchPaths;
        public string[] apSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
        public List<string> backedUpFiles = new List<string>();
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (rockstarButton != null)
            rockstarButton.onClick.AddListener(OnRockstarButtonClicked);

        SelectSteam();

        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installASIToggle != null)
            installASIToggle.isOn = true;

        if (installWidescreenToggle != null)
            installWidescreenToggle.isOn = true;

        if (installCleoToggle != null)
            installCleoToggle.isOn = true;

        if (installExeToggle != null)
            installExeToggle.isOn = true;

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

    void OnRockstarButtonClicked()
    {
        SelectRockstar();
    }

    void SelectSteam()
    {
        isRockstar = false;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + gamePath);
    }

    void SelectRockstar()
    {
        isRockstar = true;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Rockstar - Path: " + gamePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isRockstar ? "Rockstar" : "Steam";
            string status = string.IsNullOrEmpty(gamePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        gtavcApworld.url = remoteConfig.gtavcApworld;
        gtavcASI.url = remoteConfig.gtavcASI;
        gtavcWidescreen.url = remoteConfig.gtavcWidescreen;
        gtavcCleo.url = remoteConfig.gtavcCleo;
        gtavcExe.url = remoteConfig.gtavcExe;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove all mods?", "Revert");
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

        bool Apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ASI = installASIToggle == null || installASIToggle.isOn;
        bool Widescreen = installWidescreenToggle == null || installWidescreenToggle.isOn;
        bool Cleo = installCleoToggle == null || installCleoToggle.isOn;
        bool Exe = installExeToggle == null || installExeToggle.isOn;

        bool needsGamePath = Widescreen || Cleo || ASI || Exe || !Apworld;

        if (needsGamePath && string.IsNullOrEmpty(gamePath))
        {
            string platform = isRockstar ? "Rockstar" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count = (Apworld ? 1 : 0) + (ASI ? 1 : 0) + (Widescreen ? 1 : 0) + (Cleo ? 1 : 0) + (Exe ? 1 : 0);

        if (Apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupWithTracking(Apworld, ASI, Widescreen, Cleo, Exe));
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupWithTracking(bool wantApworld, bool wantASI, bool wantWidescreen, bool wantCleo, bool wantExe)
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return new WaitUntil(() => configLoaded);

        if (wantApworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (wantASI)
        {
            ShowInfo("Installing ASI loader...");
            yield return InstallZipToGameDir(gtavcASI, "ASI");
        }

        if (wantWidescreen)
        {
            ShowInfo("Installing widescreen fix...");
            yield return InstallZipToGameDir(gtavcWidescreen, "Widescreen");
        }

        if (wantCleo)
        {
            ShowInfo("Installing CLEO...");
            yield return InstallZipToGameDir(gtavcCleo, "CLEO");
        }

        if (wantExe)
        {
            ShowInfo("Installing patched executable...");
            yield return InstallExeWithBackup(gtavcExe, "Exe");
        }

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found. Cannot revert.\nPlease reinstall the game if needed.");
            return;
        }

        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");

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
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        successCount++;
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Failed to delete: " + filePath + " - " + e.Message);
                        errorCount++;
                    }
                }
            }

            // Explicitly remove the known folders/files dropped by the ASI/Widescreen/CLEO
            // installs, regardless of whether the manifest tracked them individually.
            foreach (string folderName in RevertFolders)
            {
                try
                {
                    string folderPath = Path.Combine(manifest.gameInstallPath, folderName);
                    if (Directory.Exists(folderPath))
                    {
                        Directory.Delete(folderPath, true);
                        successCount++;
                        UnityEngine.Debug.Log("Deleted folder: " + folderPath);
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete folder: " + folderName + " - " + e.Message);
                    errorCount++;
                }
            }

            foreach (string revertFileName in RevertFiles)
            {
                try
                {
                    string filePath = Path.Combine(manifest.gameInstallPath, revertFileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        successCount++;
                        UnityEngine.Debug.Log("Deleted file: " + filePath);
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete file: " + revertFileName + " - " + e.Message);
                    errorCount++;
                }
            }

            // Restore the original exe from .exeBackup, then clean up the backup folder.
            string backupDir = Path.Combine(manifest.gameInstallPath, ExeBackupFolderName);
            if (manifest.backedUpFiles != null)
            {
                foreach (string backedUpFileName in manifest.backedUpFiles)
                {
                    try
                    {
                        string backupPath = Path.Combine(backupDir, backedUpFileName);
                        string restorePath = Path.Combine(manifest.gameInstallPath, backedUpFileName);

                        if (File.Exists(backupPath))
                        {
                            if (File.Exists(restorePath))
                                File.Delete(restorePath);

                            File.Move(backupPath, restorePath);
                            UnityEngine.Debug.Log("Restored original exe from backup: " + restorePath);
                        }
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Failed to restore backed up exe: " + e.Message);
                        errorCount++;
                    }
                }
            }

            SafeDeleteDirectory(backupDir);

            RemoveEmptyDirectories(manifest.gameInstallPath);

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

    void RemoveEmptyDirectories(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }
        catch { }
    }

    // Downloads a zip via the remote config URL, extracts it, and moves ALL of its
    // contents directly into the game install directory, tracking every moved file
    // in the current manifest so it can be reverted later.
    IEnumerator InstallZipToGameDir(FileDownloader.FileData fileData, string componentName)
    {
        if (fileData == null || string.IsNullOrEmpty(fileData.url))
        {
            UnityEngine.Debug.LogWarning(componentName + " URL is empty, skipping.");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, componentName + "Temp");

        yield return downloader.DownloadAndExtract(fileData, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: " + componentName + " extraction failed!");
            yield break;
        }

        MoveDirectoryAndTrack(extractPath, gamePath);

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log(componentName + " installed successfully!");
    }

    // Downloads a single .exe file. Before placing it in the game directory, any
    // existing file with the same name in the game directory is moved into a
    // ".exeBackup" subfolder so it can be restored later on revert.
    IEnumerator InstallExeWithBackup(FileDownloader.FileData fileData, string componentName)
    {
        if (fileData == null || string.IsNullOrEmpty(fileData.url))
        {
            UnityEngine.Debug.LogWarning(componentName + " URL is empty, skipping.");
            yield break;
        }

        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowInfo("ERROR: Game directory not found, cannot install " + componentName + "!");
            yield break;
        }

        string fileName = fileData.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = fileData.url.Substring(fileData.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string tempPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading " + componentName + " from: " + fileData.url);

        yield return DownloadFile(fileData.url, tempPath);

        if (!File.Exists(tempPath))
        {
            UnityEngine.Debug.LogError(componentName + " download failed: file not found at " + tempPath);
            ShowInfo("ERROR: " + componentName + " download failed!");
            yield break;
        }

        string targetPath = Path.Combine(gamePath, fileName);

        // Back up whatever exe is currently sitting there, if any.
        if (File.Exists(targetPath))
        {
            try
            {
                string backupDir = Path.Combine(gamePath, ExeBackupFolderName);
                Directory.CreateDirectory(backupDir);

                string backupPath = Path.Combine(backupDir, fileName);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(targetPath, backupPath);

                UnityEngine.Debug.Log("Backed up existing exe to: " + backupPath);

                if (currentManifest != null)
                    currentManifest.backedUpFiles.Add(fileName);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to back up existing exe: " + e.Message);
                ShowInfo("ERROR: Failed to back up existing " + componentName + "\n" + e.Message);
                yield break;
            }
        }

        try
        {
            File.Copy(tempPath, targetPath, true);

            UnityEngine.Debug.Log(componentName + " copied to: " + targetPath);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(targetPath);

            ShowInfo(componentName + " installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy " + componentName + ": " + e.Message);
            ShowInfo("ERROR: Failed to install " + componentName + "\n" + e.Message);
            yield break;
        }

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary " + componentName + " file: " + e.Message);
        }
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + gtavcApworld.url);

        if (string.IsNullOrEmpty(gtavcApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = gtavcApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = gtavcApworld.url.Substring(gtavcApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + gtavcApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(gtavcApworld.url, localPath);

        if (!File.Exists(localPath))
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

        gamePath = GetGamePath();
        UpdatePlatformStatus();
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

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

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
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");
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

    // =========================================================
    // GAME PATH DETECTION
    // =========================================================

    string GetGamePath()
    {
        return isRockstar ? GetRockstarPath() : GetSteamPath();
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

    string GetRockstarPath()
    {
        List<string> quickBasePaths = new List<string>
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Rockstar Games"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Rockstar Games"),
        };

        foreach (string basePath in quickBasePaths)
        {
            foreach (string folderName in rockstarGameFolderNames)
            {
                try
                {
                    string path = Path.Combine(basePath, folderName);
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Found Game (Rockstar) at: " + path);
                        return path;
                    }
                }
                catch { }
            }
        }

        try
        {
            string documentsRockstarPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "Rockstar Games");

            foreach (string folderName in rockstarGameFolderNames)
            {
                try
                {
                    string path = Path.Combine(documentsRockstarPath, folderName);
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Found Game (Rockstar, via Documents) at: " + path);
                        return path;
                    }
                }
                catch { }
            }
        }
        catch { }

        if (remoteConfig != null && remoteConfig.rockstarSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.rockstarSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        foreach (string folderName in rockstarGameFolderNames)
                        {
                            try
                            {
                                string path = Path.Combine(drive.Name, relativePath, folderName);
                                if (Directory.Exists(path))
                                {
                                    UnityEngine.Debug.Log("Found Game (Rockstar, via remote config) at: " + path);
                                    return path;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Rockstar) not found.");
        return "";
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