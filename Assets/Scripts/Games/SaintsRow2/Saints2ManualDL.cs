using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class Saints2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData saints2Apworld;
    public FileDownloader.FileData saints2AP;
    public FileDownloader.FileData saints2Juiced;
    public FileDownloader.FileData saints2ASI;
    public FileDownloader.FileData saints2Exe;

    [Header("GAME FOLDER NAME")]
    public string steamGameFolderName = "Saints Row 2";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;
    public Toggle installJuicedToggle;
    public Toggle installASIToggle;
    public Toggle installExeToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearToggle;
    public Toggle clearApModsToggle;

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
    private bool lastApWorldInstallSuccess = false;
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string saints2Apworld;
        public string saints2AP;
        public string saints2Juiced;
        public string saints2ASI;
        public string saints2Exe;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
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
        if (installJuicedToggle != null)
            installJuicedToggle.isOn = true;
        if (installASIToggle != null)
            installASIToggle.isOn = true;
        if (installExeToggle != null)
            installExeToggle.isOn = true;

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (clearApModsToggle != null)
            clearApModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        saints2AP.url = remoteConfig.saints2AP;
        saints2Apworld.url = remoteConfig.saints2Apworld;
        saints2Juiced.url = remoteConfig.saints2Juiced;
        saints2ASI.url = remoteConfig.saints2ASI;
        saints2Exe.url = remoteConfig.saints2Exe;
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

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;
        bool juiced = installJuicedToggle == null || installJuicedToggle.isOn;
        bool asi = installASIToggle == null || installASIToggle.isOn;
        bool exe = installExeToggle == null || installExeToggle.isOn;

        bool needsGamePath = ap || juiced || asi || exe;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0) + (juiced ? 1 : 0) + (asi ? 1 : 0) + (exe ? 1 : 0);

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

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
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

        yield return InstallFlow();

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallFlow()
    {
        if (installJuicedToggle == null || installJuicedToggle.isOn)
        {
            ShowInfo("Installing Juiced...");
            yield return InstallJuiced();
        }

        if (installExeToggle == null || installExeToggle.isOn)
        {
            ShowInfo("Installing patched executable...");
            yield return InstallExe();
        }

        if (installAPToggle == null || installAPToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAP();
        }

        if (installASIToggle == null || installASIToggle.isOn)
        {
            ShowInfo("Installing ASI loader...");
            yield return InstallASI();
        }

        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        UpdateLooseTxt();

        yield return new WaitForSeconds(1f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    // =========================================================
    // INDIVIDUAL INSTALLERS
    // =========================================================

    IEnumerator InstallJuiced()
    {
        BackupFile("DFEngine.dll");
        yield return InstallZipContentsToGamePath(saints2Juiced, "Saints2JuicedTemp", "Saints2 Juiced");
    }

    IEnumerator InstallExe()
    {
        BackupFile("SR2_pc.exe");

        string localPath = Path.Combine(Application.persistentDataPath, "sr2_patched_download.exe");

        yield return DownloadFile(saints2Exe.url, localPath);

        if (!File.Exists(localPath))
        {
            ShowInfo("ERROR: Executable download failed!");
            yield break;
        }

        string target = Path.Combine(gamePath, "SR2_pc.exe");

        try
        {
            if (File.Exists(target))
                File.Delete(target);

            File.Copy(localPath, target, true);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

            ShowInfo("Patched executable installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy executable: " + e.Message);
            ShowInfo("ERROR: Failed to install executable\n" + e.Message);
            yield break;
        }

        try
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch { }
    }

    IEnumerator InstallAP()
    {
        yield return InstallZipContentsToGamePath(saints2AP, "Saints2APTemp", "Saints2 AP");
    }

    IEnumerator InstallASI()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "Saints2ASITemp");

        yield return downloader.DownloadAndExtract(saints2ASI, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: ASI package extraction failed!");
            yield break;
        }

        string dllSource = FindFile(extractPath, "dinput8.dll");

        if (string.IsNullOrEmpty(dllSource))
        {
            ShowInfo("ERROR: dinput8.dll not found in the ASI package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string dest = Path.Combine(gamePath, "dinput8.dll");

        try
        {
            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(dllSource, dest);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);

            ShowInfo("ASI loader installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install dinput8.dll: " + e.Message);
            ShowInfo("ERROR: Failed to install ASI loader\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallZipContentsToGamePath(FileDownloader.FileData file, string tempFolderName, string logLabel)
    {
        string extractPath = Path.Combine(Application.persistentDataPath, tempFolderName);

        yield return downloader.DownloadAndExtract(file, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: " + logLabel + " extraction failed!");
            yield break;
        }

        MoveDirectoryAndTrack(extractPath, gamePath);

        SafeDeleteDirectory(extractPath);

        ShowInfo(logLabel + " installed successfully!");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + saints2Apworld.url);

        if (string.IsNullOrEmpty(saints2Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = saints2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = saints2Apworld.url.Substring(saints2Apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + saints2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(saints2Apworld.url, localPath);

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

    // =========================================================
    // BACKUP MANAGEMENT
    // =========================================================

    void BackupFile(string fileName)
    {
        try
        { 
            string source = Path.Combine(gamePath, fileName);

            if (!File.Exists(source))
                return;

            string backupDir = Path.Combine(gamePath, "Backup");
            Directory.CreateDirectory(backupDir);

            string backupTarget = Path.Combine(backupDir, fileName);

            if (File.Exists(backupTarget))
            {
                File.Delete(source);
            }
            else
            {
                File.Move(source, backupTarget);
                UnityEngine.Debug.Log("Backed up original file: " + fileName);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Backup failed for " + fileName + ": " + e.Message);
        }
    }

    void RestoreBackup(string installPath, string fileName)
    {
        try
        {
            string backupPath = Path.Combine(installPath, "Backup", fileName);
            string originalPath = Path.Combine(installPath, fileName);

            if (!File.Exists(backupPath))
                return;

            if (File.Exists(originalPath))
                File.Delete(originalPath);

            File.Move(backupPath, originalPath);
            UnityEngine.Debug.Log("Restored original file: " + fileName);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to restore backup for " + fileName + ": " + e.Message);
        }
    }

    void OnFullCleanChanged(bool value)
    {
        if (clearApModsToggle != null)
        {
            clearApModsToggle.isOn = false;
            clearApModsToggle.interactable = !value;
        }
    }

    // =========================================================
    // LOOSE.TXT
    // =========================================================

    void UpdateLooseTxt()
    {
        try
        {
            string loosePath = Path.Combine(gamePath, "loose.txt");
            string lineToAdd = "mods/sr2ap_core_files\nmods/sr2ap_seed_files";

            if (!File.Exists(loosePath))
            {
                File.WriteAllText(loosePath, lineToAdd + "\n");
            }
            else
            {
                string content = File.ReadAllText(loosePath);

                if (!content.Contains(lineToAdd))
                {
                    if (content.Length > 0 && !content.EndsWith("\n"))
                        content += "\n";

                    content += lineToAdd + "\n";
                    File.WriteAllText(loosePath, content);
                }
            }

            UnityEngine.Debug.Log("loose.txt updated with AP mod entries");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to update loose.txt: " + e.Message);
        }
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;
        bool clearApMods = clearApModsToggle != null && clearApModsToggle.isOn;

        if (!fullClear && !clearApMods)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        CleanupProcesses();

        if (fullClear)
        {
            StartCoroutine(ExecuteFullClear());
        }
        else if (clearApMods)
        {
            ExecuteClearApMods();
        }
    }

    IEnumerator ExecuteFullClear()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found. Cannot revert.\nPlease reinstall the game if needed.");
            yield break;
        }

        ShowInfo("Performing full clear...");
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

            RestoreBackup(manifest.gameInstallPath, "DFEngine.dll");
            RestoreBackup(manifest.gameInstallPath, "SR2_pc.exe");

            string[] foldersToForceDelete = { "Mods", "Scripts", "Juiced" };
            foreach (string folderName in foldersToForceDelete)
            {
                SafeDeleteDirectory(Path.Combine(manifest.gameInstallPath, folderName));
            }

            RemoveEmptyDirectories(manifest.gameInstallPath);

            SafeDeleteDirectoryIfEmpty(Path.Combine(manifest.gameInstallPath, "Backup"));

            ShowInfo($"Full clear complete!\n({successCount} files deleted)");
            UnityEngine.Debug.Log($"Full clear: {successCount} files deleted, {errorCount} errors");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during full clear:\n" + e.Message);
            UnityEngine.Debug.LogError("Full clear error: " + e);
        }
        finally
        {
            try { File.Delete(manifestPath); }
            catch { }
        }
    }

    void ExecuteClearApMods()
    {
        string modsPath = Path.Combine(gamePath, "Mods");
        string scriptsPath = Path.Combine(gamePath, "Scripts");

        ShowInfo("Clearing AP mods...");

        SafeDeleteDirectory(Path.Combine(modsPath, "sr2ap_core_files"));
        SafeDeleteDirectory(Path.Combine(modsPath, "sr2ap_seed_files"));

        SafeDeleteFile(Path.Combine(scriptsPath, "SR2Archipelago.asi"));
        SafeDeleteFile(Path.Combine(scriptsPath, "SR2Archipelago.toml"));
        SafeDeleteFile(Path.Combine(scriptsPath, "SR2Archipelago.log"));

        ShowInfo("AP mods cleared successfully!");
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

            if (Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
                Directory.Delete(path);
        }
        catch { }
    }

    void SafeDeleteDirectoryIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0)
                Directory.Delete(path);
        }
        catch { }
    }

    // =========================================================
    // DOWNLOAD / FILE HELPERS
    // =========================================================

    IEnumerator DownloadFile(string url, string savePath)
    {
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

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName)
                    return file;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error finding file: " + e.Message);
        }

        return "";
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
    // GAME PATH (STEAM ONLY)
    // =========================================================

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

    // =========================================================
    // LAUNCH / CLOSE
    // =========================================================

    void LaunchGame()
    {
        string currentGamePath = GetGamePath();

        if (string.IsNullOrEmpty(currentGamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string exePath = Path.Combine(currentGamePath, "SR2_pc.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        try
        {
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

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

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

        gamePath = GetGamePath();
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