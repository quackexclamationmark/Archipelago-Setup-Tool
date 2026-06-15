using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class ScheduleManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SCHEDULE I FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData melonLoader;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installMelonLoaderToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanMelonLoaderToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string schedulePath;
    private string pendingAction;
    private ScheduleConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class ScheduleConfig
    {
        public string scheduleAP;
        public string scheduleMelonLoader;
        public string scheduleApworld;
    }

    void Start()
    {
        schedulePath = GetSchedulePath();
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

        if (fullCleanMelonLoaderToggle != null)
            fullCleanMelonLoaderToggle.isOn = false;

        if (fullCleanMelonLoaderToggle != null)
            fullCleanMelonLoaderToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void ApplyScheduleConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.scheduleAP;
        melonLoader.url = remoteConfig.scheduleMelonLoader;
        apworld.url = remoteConfig.scheduleApworld;
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
                StartCoroutine(ExecuteFullCleanCoroutine());
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
        if (string.IsNullOrEmpty(schedulePath))
        {
            ShowInfo("Schedule I path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (melonloader ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (melonloader && count == 1)
        {
            StartCoroutine(MelonLoaderOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        schedulePath = GetSchedulePath();

        if (string.IsNullOrEmpty(schedulePath))
            return;

        string modsPath = Path.Combine(schedulePath, "Mods");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanMelonLoaderToggle != null && fullCleanMelonLoaderToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        // Lance la coroutine
        StartCoroutine(ExecuteRevertCoroutine(removeAP, fullClean, modsPath));
    }

    private IEnumerator ExecuteRevertCoroutine(bool removeAP, bool fullClean, string modsPath)
    {
        // REMOVE AP MODS ONLY
        if (removeAP && !fullClean)
        {
            if (!Directory.Exists(modsPath))
            {
                ShowInfo("Mods folder not found.");
                yield break;
            }

            ShowInfo("Removing AP mods...");
            UnityEngine.Debug.Log("START: Removing AP mods");

            // Delete everything in Mods folder
            yield return SafeDeleteDirectoryAsync(modsPath);

            // Recreate empty Mods folder
            Directory.CreateDirectory(modsPath);

            // Delete version files
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            UnityEngine.Debug.Log("END: AP mods removed");
            yield break;
        }

        // FULL CLEAN
        if (fullClean)
        {
            bool hasOtherMods = HasOtherMods(modsPath);

            if (hasOtherMods)
            {
                ShowConfirmation(
                    "Other mods were detected.\nDo you REALLY want to fully delete MelonLoader?",
                    "ForceFullClean"
                );
                yield break;
            }

            yield return ExecuteFullCleanCoroutine();
        }
    }

    private IEnumerator ExecuteFullCleanCoroutine()
    {
        ShowInfo("Removing mods...");
        UnityEngine.Debug.Log("START: Full clean");

        string modsPath = Path.Combine(schedulePath, "Mods");

        yield return SafeDeleteDirectoryAsync(Path.Combine(modsPath, "Narcopelago"));
        DeleteOldVersionFiles();

        ShowInfo("Cleaning MelonLoader...");

        yield return SafeDeleteDirectoryAsync(Path.Combine(schedulePath, "MelonLoader"));
        yield return SafeDeleteDirectoryAsync(Path.Combine(schedulePath, "UserData"));
        yield return SafeDeleteDirectoryAsync(Path.Combine(schedulePath, "UserLibs"));
        yield return SafeDeleteDirectoryAsync(Path.Combine(schedulePath, "Mods"));
        yield return SafeDeleteDirectoryAsync(Path.Combine(schedulePath, "Plugins"));
        yield return new WaitForSeconds(0.5f);
        SafeDeleteFile(Path.Combine(schedulePath, "version.dll"));

        ShowInfo("Full clean completed!");
        UnityEngine.Debug.Log("END: Full clean");
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        // FILES - Ignorer les fichiers de Narcopelago
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            // Fichiers autorisés (Narcopelago)
            if (name == "Archipelago.MultiClient.Net.dll" ||
                name == "icon.png" ||
                name == "manifest.json" ||
                name == "Narcopelago.dll" ||
                name == "README.md" ||
                name.StartsWith("Schedule APMod Version") && name.EndsWith(".txt"))
                continue;

            // Tous les autres fichiers = autres mods
            return true;
        }

        // DIRECTORIES - Ignorer ceux de Narcopelago
        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            // Dossiers autorisés (Narcopelago)
            if (name == "Data")
                continue;

            // Tous les autres dossiers = autres mods
            return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn)
        {
            ShowInfo("Installing MelonLoader...");
            yield return InstallMelonLoader();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        CreateVersionFile(apMod.url, melonLoader.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Schedule I...");
            yield return new WaitForSeconds(1f);
            LaunchSchedule();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
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

    IEnumerator InstallAPMod()
    {
        UnityEngine.Debug.Log("START InstallAPMod");

        while (!configLoaded)
            yield return null;

        ShowInfo("Extracting AP Mod...");

        string modsPath = Path.Combine(schedulePath, "Mods");
        Directory.CreateDirectory(modsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "ScheduleAPModTemp");

        UnityEngine.Debug.Log("Download path: " + extractPath);
        UnityEngine.Debug.Log("Target mods path: " + modsPath);

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        // LOG POUR DÉBOGUER
        UnityEngine.Debug.Log("=== EXTRACTED FILES ===");
        LogDirectoryStructure(extractPath, 3);

        CopyAPModFilesToMods(extractPath, modsPath);

        // LOG APRÈS COPY
        UnityEngine.Debug.Log("=== MODS FOLDER AFTER COPY ===");
        LogDirectoryStructure(modsPath, 2);

        yield return SafeDeleteDirectoryAsync(extractPath);

        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    void CopyAPModFilesToMods(string extractPath, string modsPath)
    {
        UnityEngine.Debug.Log("START CopyAPModFilesToMods");
        UnityEngine.Debug.Log("Extract path: " + extractPath);
        UnityEngine.Debug.Log("Mods path: " + modsPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extract path doesn't exist: " + extractPath);
            ShowInfo("ERROR: Extract path not found");
            return;
        }

        try
        {
            // Ensure Mods directory exists
            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
                UnityEngine.Debug.Log("Created Mods folder");
            }

            // Copy ALL files from extracted zip to Mods folder
            CopyDirectoryContents(extractPath, modsPath);

            UnityEngine.Debug.Log("All files copied to Mods folder");
            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy AP Mod files: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
        }

        UnityEngine.Debug.Log("END CopyAPModFilesToMods");
    }

    void CopyDirectoryContents(string sourceDir, string targetDir)
    {
        UnityEngine.Debug.Log("CopyDirectoryContents from: " + sourceDir + " to: " + targetDir);

        if (!Directory.Exists(sourceDir))
        {
            UnityEngine.Debug.LogError("Source directory doesn't exist: " + sourceDir);
            return;
        }

        try
        {
            // Ensure target directory exists
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            // Copy all files from source to target
            string[] files = Directory.GetFiles(sourceDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);

                File.Copy(file, targetFile, true);
                UnityEngine.Debug.Log("Copied file: " + fileName);
            }

            // Recursively copy all subdirectories
            string[] subdirs = Directory.GetDirectories(sourceDir);
            foreach (string subdir in subdirs)
            {
                string dirName = Path.GetFileName(subdir);
                string targetSubdir = Path.Combine(targetDir, dirName);

                UnityEngine.Debug.Log("Copying directory: " + dirName);
                CopyDirectoryContents(subdir, targetSubdir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory contents: " + e.Message);
            throw;
        }
    }

    IEnumerator InstallMelonLoader()
    {
        UnityEngine.Debug.Log("START InstallMelonLoader");

        while (!configLoaded)
            yield return null;

        ShowInfo("Extracting MelonLoader...");

        string extractPath = Path.Combine(Application.persistentDataPath, "MelonLoaderTemp");

        UnityEngine.Debug.Log("Download path: " + extractPath);

        yield return downloader.DownloadAndExtract(melonLoader, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, schedulePath);

        Directory.CreateDirectory(Path.Combine(schedulePath, "Mods"));

        yield return SafeDeleteDirectoryAsync(extractPath);

        UnityEngine.Debug.Log("END InstallMelonLoader");
        yield return null;
    }

    void CopyMelonLoaderFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyMelonLoaderFiles");

        string versionDllSource = FindFile(extractPath, "version.dll");
        if (!string.IsNullOrEmpty(versionDllSource))
        {
            File.Copy(versionDllSource, Path.Combine(targetPath, "version.dll"), true);
            UnityEngine.Debug.Log("version.dll copied");
        }
        else
        {
            UnityEngine.Debug.LogWarning("version.dll not found!");
        }

        string melonLoaderFolder = FindDirectory(extractPath, "MelonLoader");
        if (!string.IsNullOrEmpty(melonLoaderFolder))
        {
            string destFolder = Path.Combine(targetPath, "MelonLoader");
            if (Directory.Exists(destFolder))
            {
                try
                {
                    Directory.Delete(destFolder, true);
                }
                catch { }
            }

            CopyDirectoryContents(melonLoaderFolder, destFolder);
            UnityEngine.Debug.Log("MelonLoader folder copied");
        }
        else
        {
            UnityEngine.Debug.LogWarning("MelonLoader folder not found!");
        }

        UnityEngine.Debug.Log("END CopyMelonLoaderFiles");
    }

    IEnumerator APWorldOnlyFlow()
    {
        schedulePath = GetSchedulePath();

        if (string.IsNullOrEmpty(schedulePath))
            yield break;

        yield return InstallAPWorld();

        ShowInfo("APWorld installed successfully!");
    }

    IEnumerator MelonLoaderOnlyFlow()
    {
        ShowInfo("Installing MelonLoader...");
        yield return InstallMelonLoader();

        ShowInfo("MelonLoader installed successfully!");

        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        schedulePath = GetSchedulePath();

        if (string.IsNullOrEmpty(schedulePath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, melonLoader.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Schedule I...");
            yield return new WaitForSeconds(1f);
            LaunchSchedule();
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
            remoteConfig = JsonUtility.FromJson<ScheduleConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyScheduleConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchSchedule()
    {
        try
        {
            string exePath = Path.Combine(schedulePath, "Schedule I.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Schedule I launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Schedule.exe not found at: " + exePath);
                ShowInfo("ERROR: Schedule.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Schedule I: " + e.Message);
            ShowInfo("ERROR: Failed to launch Schedule I!");
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

    IEnumerator SafeDeleteDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            UnityEngine.Debug.Log("Directory doesn't exist, skipping: " + path);
            yield break;
        }

        UnityEngine.Debug.Log("Starting deletion of: " + path);

        // Attendre un peu avant de supprimer
        yield return new WaitForSeconds(0.3f);

        bool success = false;
        string errorMessage = "";

        // TRY-CATCH SANS YIELD
        try
        {
            DeleteDirectoryWithRetry(path, maxRetries: 10);
            success = true;
        }
        catch (System.Exception e)
        {
            errorMessage = e.Message;
        }

        // YIELD APRÈS LE TRY-CATCH
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

                // Récursivement supprimer les fichiers
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

                // Récursivement supprimer les sous-dossiers
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

                // Supprimer le dossier vide
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
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

    string FindDirectory(string root, string dirName)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir) == dirName)
                    return dir;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error finding directory: " + e.Message);
        }

        return "";
    }

    void CreateVersionFile(string apmodUrl, string melonloaderUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/([^/]+)\.zip");
            string melonloaderVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "Schedule APMod Version " + apmodVersion + ".txt";
            string content = "Schedule I Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== MELONLOADER ===\n";
            content += "Downloaded from: " + melonloaderUrl + "\n";
            content += "Version: " + melonloaderVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(schedulePath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(schedulePath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods: " + modsVersionPath);
            }
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Schedule APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(schedulePath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (pattern.IsMatch(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted old version file in root: " + fileName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message);
                    }
                }
            }

            string modsPath = Path.Combine(schedulePath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string[] modsFiles = Directory.GetFiles(modsPath);
                foreach (string file in modsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in Mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Mods: " + e.Message);
                        }
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
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    void LogDirectoryStructure(string path, int maxDepth, int currentDepth = 0)
    {
        if (!Directory.Exists(path) || currentDepth >= maxDepth)
            return;

        try
        {
            string indent = new string(' ', currentDepth * 2);

            string[] dirs = Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                UnityEngine.Debug.Log(indent + "📁 " + Path.GetFileName(dir));
                LogDirectoryStructure(dir, maxDepth, currentDepth + 1);
            }

            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                long fileSize = new FileInfo(file).Length;
                UnityEngine.Debug.Log(indent + "📄 " + Path.GetFileName(file) + " (" + fileSize + " bytes)");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error logging directory: " + e.Message);
        }
    }

    string GetSchedulePath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Schedule I"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Schedule I"),
            @"D:\Steam\steamapps\common\Schedule I",
            @"D:\SteamLibrary\steamapps\common\Schedule I",
            @"D:\steamapps\common\Schedule I",
            @"E:\Steam\steamapps\common\Schedule I",
            @"E:\SteamLibrary\steamapps\common\Schedule I",
            @"E:\steamapps\common\Schedule I",
            @"E:\Program Files (x86)\steamapps\common\Schedule I",
            @"E:\Program Files\steamapps\common\Schedule I",
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
                    // Search Steam\steamapps
                    string schedPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Schedule I");
                    if (Directory.Exists(schedPath))
                        return schedPath;

                    // Search SteamLibrary\steamapps
                    schedPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Schedule I");
                    if (Directory.Exists(schedPath))
                        return schedPath;

                    // Search steamapps at root
                    schedPath = Path.Combine(drive.Name, "steamapps", "common", "Schedule I");
                    if (Directory.Exists(schedPath))
                        return schedPath;

                    // Search Program Files (x86)\steamapps
                    schedPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Schedule I");
                    if (Directory.Exists(schedPath))
                        return schedPath;

                    // Search Program Files\steamapps
                    schedPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Schedule I");
                    if (Directory.Exists(schedPath))
                        return schedPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
