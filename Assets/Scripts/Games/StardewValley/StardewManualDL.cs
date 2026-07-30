using System.Collections;
using System.Diagnostics;
using System.IO;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StardewManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("STARDEW VALLEY FILES")]
    public FileDownloader.FileData preloadedMods;
    public FileDownloader.FileData apMod;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Stardew Valley";

    [Header("FEATURE TOGGLES")]
    public Toggle installPreloadedToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsOnlyToggle;
    public Toggle fullClearToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process stardewProcess;
    private string stardewPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private StardewConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class StardewConfig
    {
        public string stardewvalleyAP;
        public string stardewvalleyPreloaded;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        stardewPath = GetStardewValleyPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (installPreloadedToggle != null)
            installPreloadedToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (clearAPModsOnlyToggle != null)
            clearAPModsOnlyToggle.isOn = true;

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsOnlyToggle != null)
        {
            clearAPModsOnlyToggle.isOn = false;
            clearAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplyStardewConfig()
    {
        if (remoteConfig == null)
            return;

        preloadedMods.url = remoteConfig.stardewvalleyPreloaded;
        apMod.url = remoteConfig.stardewvalleyAP;
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

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;

            case "Revert":
                ExecuteRevert();
                break;

            case "ForceFullClear":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        stardewPath = GetStardewValleyPath();

        if (string.IsNullOrEmpty(stardewPath))
        {
            ShowInfo("Stardew Valley not found. Please check installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        stardewPath = GetStardewValleyPath();

        if (string.IsNullOrEmpty(stardewPath))
            return;

        string modsPath = Path.Combine(stardewPath, "Mods");

        bool clearAPOnly = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;

        if (!clearAPOnly && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAPOnly)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(modsPath, "StardewArchipelago"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (hasOtherMods && !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected in Mods folder.\nDo you want to continue?",
                "ForceFullClear"
            );

            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        ShowInfo("Full clear in progress...");

        // Supprimer les dossiers
        SafeDeleteDirectory(Path.Combine(stardewPath, "Mods"));
        SafeDeleteDirectory(Path.Combine(stardewPath, "smapi-internal"));

        // Supprimer les fichiers SMAPI
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.deps.json"));
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.dll"));
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.exe"));
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.exe.config"));
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.runtimeconfig.json"));
        SafeDeleteFile(Path.Combine(stardewPath, "StardewModdingAPI.xml"));
        SafeDeleteFile(Path.Combine(stardewPath, "steam_appid.txt"));

        DeleteOldVersionFiles();

        ShowInfo("Full clear completed!");
    }

    // =========================================================
    // INFO
    // =========================================================

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
    // OTHER LOGIC
    // =========================================================

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        // Whitelist des dossiers à ignorer
        string[] whitelistedDirs = { "StardewArchipelago", "ConsoleCommands", "SaveBackup" };

        foreach (string dir in Directory.GetDirectories(modsPath))
        {
            string name = Path.GetFileName(dir);
            bool isWhitelisted = false;

            foreach (string whitelisted in whitelistedDirs)
            {
                if (name == whitelisted)
                {
                    isWhitelisted = true;
                    break;
                }
            }

            if (!isWhitelisted)
                return true;
        }

        // Whitelist des fichiers à ignorer
        System.Text.RegularExpressions.Regex versionFilePattern = new System.Text.RegularExpressions.Regex(@"Stardew Archipelago Version .+\.txt");

        foreach (string file in Directory.GetFiles(modsPath))
        {
            string fileName = Path.GetFileName(file);
            if (!versionFilePattern.IsMatch(fileName))
                return true;
        }

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installPreloadedToggle == null || installPreloadedToggle.isOn)
        {
            ShowInfo("Installing preloaded mods...");
            yield return InstallPreloadedMods();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        // Seulement lancer le jeu si second launch est activé
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching StardewModdingAPI...");
            LaunchStardewModdingAPI();

            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallPreloadedMods()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "StardewPreloadedTemp");

        yield return downloader.DownloadAndExtract(preloadedMods, Application.persistentDataPath, extractPath);

        // Cherche le dossier Preloaded-SV-Archipelago
        string preloadedFolder = Path.Combine(extractPath, "Preloaded-SV-Archipelago");

        if (!Directory.Exists(preloadedFolder))
        {
            UnityEngine.Debug.LogError("Preloaded-SV-Archipelago folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Copie tout le contenu du dossier Preloaded-SV-Archipelago directement dans le dossier Stardew Valley
        CopyDirectory(preloadedFolder, stardewPath);

        UnityEngine.Debug.Log("Preloaded mods installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "StardewAPTemp");

        CreateVersionFile(apMod.url, preloadedMods.url);

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(stardewPath, "Mods");
        Directory.CreateDirectory(modsPath);

        // Cherche le dossier StardewArchipelago dans l'extraction
        string archipelagoSourcePath = Path.Combine(extractPath, "StardewArchipelago");

        if (!Directory.Exists(archipelagoSourcePath))
        {
            // Peut-être que c'est directement dans le ZIP
            string[] directories = Directory.GetDirectories(extractPath);
            foreach (string dir in directories)
            {
                if (Path.GetFileName(dir).Contains("Archipelago") || Path.GetFileName(dir).Contains("StardewArchipelago"))
                {
                    archipelagoSourcePath = dir;
                    break;
                }
            }
        }

        if (!Directory.Exists(archipelagoSourcePath))
        {
            UnityEngine.Debug.LogError("StardewArchipelago folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string targetPath = Path.Combine(modsPath, "StardewArchipelago");

        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        CopyDirectory(archipelagoSourcePath, targetPath);

        UnityEngine.Debug.Log("AP Mod installed successfully!");

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
            remoteConfig = JsonUtility.FromJson<StardewConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyStardewConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        stardewPath = GetStardewValleyPath();
    }

    void LaunchStardewModdingAPI()
    {
        string exePath = Path.Combine(stardewPath, "StardewModdingAPI.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("StardewModdingAPI executable not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = exePath;
        startInfo.WorkingDirectory = stardewPath;

        try
        {
            stardewProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("StardewModdingAPI launched successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching StardewModdingAPI:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseStardewValley()
    {
        try
        {
            if (stardewProcess != null && !stardewProcess.HasExited)
            {
                stardewProcess.Kill();
                stardewProcess.Dispose();
                stardewProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseStardewValley();
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
            {
                Directory.Delete(path, true);
                UnityEngine.Debug.Log("Deleted directory: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete directory: " + path + " - " + e.Message);
        }
    }

    void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
        }
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string preloadedUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);

            string versionFileName = "Stardew Archipelago Version " + apmodVersion + ".txt";

            DeleteOldVersionFiles();

            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== PRELOADED MODS ===\n";
            content += "Downloaded from: " + preloadedUrl + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            // Créer dans le root
            string rootVersionPath = Path.Combine(stardewPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            // Créer dans le dossier Mods
            string modsPath = Path.Combine(stardewPath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods folder: " + modsVersionPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Mods folder not found for version file creation");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Stardew Archipelago Version .+\.txt");

            // Supprimer dans le root
            string[] rootFiles = Directory.GetFiles(stardewPath);
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

            // Supprimer dans le dossier Mods
            string modsPath = Path.Combine(stardewPath, "Mods");
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
                            UnityEngine.Debug.Log("Deleted old version file in Mods folder: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Mods folder: " + e.Message);
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

    string ExtractVersionFromUrl(string url)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"/download/(\d+\.\d+\.\d+)/");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetStardewValleyPath()
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