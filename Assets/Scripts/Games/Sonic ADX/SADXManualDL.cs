using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class SADXManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SADX FILES")]
    public FileDownloader.FileData sadxApworld;
    public FileDownloader.FileData sadxModManager;
    public FileDownloader.FileData sadxAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Sonic Adventure DX";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installSAModManagerToggle;
    public Toggle installSADXArchipelagoToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanToggle;
    public Toggle removeAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process sadxProcess;
    private string sadxPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private SADXConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class SADXConfig
    {
        public string sadxApworld;
        public string sadxModManager;
        public string sadxAP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        sadxPath = GetSADXPath();
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

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanToggle != null)
            fullCleanToggle.isOn = false;

        if (fullCleanToggle != null)
            fullCleanToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplySADXConfig()
    {
        if (remoteConfig == null)
            return;

        sadxApworld.url = remoteConfig.sadxApworld;
        sadxModManager.url = remoteConfig.sadxModManager;
        sadxAP.url = remoteConfig.sadxAP;
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

    // =========================================================
    // SETUP (inspiré de REPOManualDL : détection du nombre de
    // composants sélectionnés + flows dédiés "Only" quand un
    // seul composant est coché, sinon flow complet)
    // =========================================================

    private void ExecuteSetup()
    {
        sadxPath = GetSADXPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool modManager = installSAModManagerToggle != null && installSAModManagerToggle.isOn;
        bool archipelago = installSADXArchipelagoToggle == null || installSADXArchipelagoToggle.isOn;
        bool needsGamePath = modManager || archipelago;

        if (needsGamePath && (string.IsNullOrEmpty(sadxPath) || !Directory.Exists(sadxPath)))
        {
            ShowInfo("SADX path not found. Please check your installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (modManager ? 1 : 0) +
            (archipelago ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (modManager && count == 1) { StartCoroutine(SAModManagerOnlyFlow()); return; }
        if (archipelago && count == 1) { StartCoroutine(SADXArchipelagoOnlyFlow()); return; }

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

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Mod Manager...");
            LaunchSAModManager();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SAModManagerOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing SAModManager...");
        yield return InstallSAModManager();

        CreateVersionFile(sadxApworld.url, sadxModManager.url, sadxAP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            if (string.IsNullOrEmpty(sadxPath))
            {
                ShowInfo("SADX path not found. Cannot launch SAModManager.");
                yield break;
            }

            ShowInfo("Launching SAModManager...");
            LaunchSAModManager();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SADXArchipelagoOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing SADX Archipelago...");
        yield return InstallSADXArchipelago();

        CreateVersionFile(sadxApworld.url, sadxModManager.url, sadxAP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            if (string.IsNullOrEmpty(sadxPath))
            {
                ShowInfo("SADX path not found. Cannot launch SAModManager.");
                yield break;
            }

            ShowInfo("Launching SAModManager...");
            LaunchSAModManager();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        sadxPath = GetSADXPath();

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanToggle != null && fullCleanToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (string.IsNullOrEmpty(sadxPath))
            {
                ShowInfo("SADX path not found. Cannot remove AP mods.");
                return;
            }

            ShowInfo("Removing AP mods...");

            string modsPath = Path.Combine(sadxPath, "mods");
            if (Directory.Exists(modsPath))
            {
                SafeDeleteDirectory(Path.Combine(modsPath, "SADX_Archipelago"));
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (string.IsNullOrEmpty(sadxPath))
        {
            ShowInfo("SADX path not found. Cannot perform full clean.");
            return;
        }

        bool hasOtherMods = HasOtherMods(Path.Combine(sadxPath, "mods"));

        if (hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete mods and SAModManager?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        string modsPathFull = Path.Combine(sadxPath, "mods");
        SafeDeleteDirectory(modsPathFull);
        SafeDeleteFile(Path.Combine(sadxPath, "SAModManager.exe"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning SADX...");

            string systemPath = Path.Combine(sadxPath, "system");
            if (Directory.Exists(systemPath))
            {
                string chrmodelsDll = Path.Combine(systemPath, "CHRMODELS.dll");
                string chrmodelsOrigDll = Path.Combine(systemPath, "CHRMODELS_orig.dll");

                try
                {
                    if (File.Exists(chrmodelsDll))
                        File.Delete(chrmodelsDll);

                    if (File.Exists(chrmodelsOrigDll))
                        File.Move(chrmodelsOrigDll, chrmodelsDll);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not restore CHRMODELS.dll: " + e.Message);
                }
            }

            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        // Whitelist of folders that should not be considered as "other mods"
        string[] whitelistedDirs = new string[] { ".modloader", "SADX_Archipelago" };

        try
        {
            string[] dirs = Directory.GetDirectories(modsPath);

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);

                // Check if directory is in whitelist
                bool isWhitelisted = false;
                foreach (string whitelisted in whitelistedDirs)
                {
                    if (dirName == whitelisted)
                    {
                        isWhitelisted = true;
                        break;
                    }
                }

                if (!isWhitelisted)
                {
                    UnityEngine.Debug.Log("Other mod detected: " + dirName);
                    return true;
                }
            }

            string[] files = Directory.GetFiles(modsPath);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                UnityEngine.Debug.Log("Other file detected in mods: " + fileName);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installSAModManagerToggle != null && installSAModManagerToggle.isOn)
        {
            ShowInfo("Installing SAModManager...");
            yield return InstallSAModManager();
        }

        if (installSADXArchipelagoToggle == null || installSADXArchipelagoToggle.isOn)
        {
            ShowInfo("Installing SADX Archipelago...");
            yield return InstallSADXArchipelago();
        }

        CreateVersionFile(sadxApworld.url, sadxModManager.url, sadxAP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            if (string.IsNullOrEmpty(sadxPath))
            {
                ShowInfo("SADX path not found. Cannot launch SAModManager.");
                yield break;
            }

            ShowInfo("Launching SAModManager...");
            LaunchSAModManager();

            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + sadxApworld.url);

        if (string.IsNullOrEmpty(sadxApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = sadxApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = sadxApworld.url.Substring(sadxApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + sadxApworld.url);

        yield return DownloadFile(sadxApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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
            yield break;
        }

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete!");
            }
        }
    }

    IEnumerator InstallSAModManager()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SAModManagerTemp");
        yield return downloader.DownloadAndExtract(sadxModManager, Application.persistentDataPath, extractPath);

        // Look for "Preloaded-SAModManager" folder in extracted contents
        string sourceFolder = Path.Combine(extractPath, "Preloaded-SAModManager");

        if (Directory.Exists(sourceFolder))
        {
            if (string.IsNullOrEmpty(sadxPath))
            {
                ShowInfo("SADX path not found. Cannot install SAModManager.");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            // Copy contents of Preloaded-SAModManager to SADX directory
            CopyDirectory(sourceFolder, sadxPath);
            UnityEngine.Debug.Log("Copied SAModManager contents to SADX directory");
        }
        else
        {
            UnityEngine.Debug.LogWarning("Preloaded-SAModManager folder not found in extracted content");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallSADXArchipelago()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(sadxPath))
        {
            ShowInfo("SADX path not found. Cannot install SADX Archipelago.");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "SADXArchipelagoTemp");
        yield return downloader.DownloadAndExtract(sadxAP, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(sadxPath, "mods");
        Directory.CreateDirectory(modsPath);

        // Look for SADX_Archipelago folder and copy it to mods directory
        string sourceFolder = Path.Combine(extractPath, "SADX_Archipelago");

        if (Directory.Exists(sourceFolder))
        {
            string targetFolder = Path.Combine(modsPath, "SADX_Archipelago");

            // Remove existing folder if it exists
            if (Directory.Exists(targetFolder))
            {
                SafeDeleteDirectory(targetFolder);
            }

            // Copy the entire folder
            CopyDirectory(sourceFolder, targetFolder);
            UnityEngine.Debug.Log("Copied SADX_Archipelago folder to mods");
        }
        else
        {
            UnityEngine.Debug.LogWarning("SADX_Archipelago folder not found in extracted content");
        }

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<SADXConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySADXConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        sadxPath = GetSADXPath();
    }

    void LaunchSAModManager()
    {
        if (string.IsNullOrEmpty(sadxPath))
        {
            ShowInfo("Error: SADX path not found.");
            return;
        }

        string exePath = Path.Combine(sadxPath, "SAModManager.exe");

        if (File.Exists(exePath))
        {
            try
            {
                sadxProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("SAModManager launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching SAModManager:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
        else
        {
            ShowInfo("Error: SAModManager.exe not found at:\n" + exePath);
        }
    }

    void CloseSAModManager()
    {
        try
        {
            if (sadxProcess != null && !sadxProcess.HasExited)
            {
                sadxProcess.Kill();
                sadxProcess.Dispose();
                sadxProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseSAModManager();
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
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        try
        {
            CopyDirectory(source, target);
            SafeDeleteDirectory(source);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error moving directory: " + e.Message);
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

    string GetSADXPath()
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
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apworldUrl, string modManagerUrl, string sadxArchipelagoUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);
            string modManagerVersion = ExtractVersionFromUrl(modManagerUrl);
            string sadxArchipelagoVersion = ExtractVersionFromUrl(sadxArchipelagoUrl);

            string versionFileName = "SADX Archipelago Version " + sadxArchipelagoVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== SA MOD MANAGER ===\n";
            content += "Downloaded from: " + modManagerUrl + "\n";
            content += "Version: " + modManagerVersion + "\n";
            content += "\n";
            content += "=== SADX ARCHIPELAGO ===\n";
            content += "Downloaded from: " + sadxArchipelagoUrl + "\n";
            content += "Version: " + sadxArchipelagoVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            if (!string.IsNullOrEmpty(sadxPath))
            {
                string rootVersionPath = Path.Combine(sadxPath, versionFileName);
                File.WriteAllText(rootVersionPath, content);
                UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles()
    {
        if (string.IsNullOrEmpty(sadxPath))
            return;

        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"SADX Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(sadxPath);
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
}