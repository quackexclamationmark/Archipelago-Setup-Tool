using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class HollowKnightManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("HOLLOW KNIGHT FILES")]
    public FileDownloader.FileData apMod;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPModToggle;
    public Toggle installMapModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsOnlyToggle;
    public Toggle fullCleanToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process hollowKnightProcess;
    private string hollowKnightPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private HollowKnightConfig remoteConfig;
    private bool configLoaded = false;

    // AP Mod dependencies (only Archipelago + these)
    private readonly string[] AP_MOD_ONLY = { "Archipelago", "ItemChanger", "MenuChanger", "Benchwarp", "QoL", "Vasi" };

    // Map Mod and its dependencies (everything else)
    private readonly string[] MAP_MOD_DEPENDENCIES = { "Archipelago Map Mod", "ConnectionMetadataInjector", "MagicUI", "MapChanger", "RandoMapCore", "RandomizerCore", "RandomizerCore.Json", "RCPathfinder", "Satchel" };

    // DLLs à restaurer (ne pas supprimer)
    private readonly string[] BACKUP_DLLS = { "Assembly-CSharp.dll", "mscorlib.dll", "Newtonsoft.Json.dll" };

    // DLLs et fichiers à supprimer lors du full clean
    private readonly string[] FILES_TO_DELETE = {
        "Assembly-CSharp.xml",
        "MMHOOK_Assembly-CSharp.dll",
        "MMHOOK_PlayMaker.dll",
        "Mono.Cecil.dll",
        "MonoMod.RuntimeDetour.dll",
        "MonoMod.Utils.dll",
        "mscorlib.xml",
        "unityscenerepacker.dll",
        "README.md"
    };

    [System.Serializable]
    public class HollowKnightConfig
    {
        public string hollowknightAP;
    }

    void Start()
    {
        hollowKnightPath = GetHollowKnightPath();
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

        if (clearAPModsOnlyToggle != null)
            clearAPModsOnlyToggle.isOn = true;

        if (fullCleanToggle != null)
            fullCleanToggle.isOn = false;

        if (fullCleanToggle != null)
            fullCleanToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (clearAPModsOnlyToggle != null)
        {
            clearAPModsOnlyToggle.isOn = false;
            clearAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyHollowKnightConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.hollowknightAP;
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

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(hollowKnightPath))
        {
            ShowInfo("Hollow Knight path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        hollowKnightPath = GetHollowKnightPath();

        if (string.IsNullOrEmpty(hollowKnightPath))
            return;

        string modsPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed", "Mods");

        bool clearAP = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanToggle != null && fullCleanToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing AP mods...");

            // Supprimer Archipelago et ses dependencies
            foreach (string mod in AP_MOD_ONLY)
            {
                SafeDeleteDirectory(Path.Combine(modsPath, mod));
            }

            // Supprimer Archipelago Map Mod et ses dependencies
            foreach (string mod in MAP_MOD_DEPENDENCIES)
            {
                SafeDeleteDirectory(Path.Combine(modsPath, mod));
            }

            // Supprimer les fichiers de version
            DeleteVersionFiles(modsPath);

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete all mods?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        // Supprimer Archipelago et ses dependencies
        foreach (string mod in AP_MOD_ONLY)
        {
            SafeDeleteDirectory(Path.Combine(modsPath, mod));
        }

        // Supprimer Archipelago Map Mod et ses dependencies
        foreach (string mod in MAP_MOD_DEPENDENCIES)
        {
            SafeDeleteDirectory(Path.Combine(modsPath, mod));
        }

        // Supprimer les fichiers de version
        DeleteVersionFiles(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning all mods and files...");

            string managedPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed");

            // Supprimer le dossier Mods
            SafeDeleteDirectory(modsPath);

            // Supprimer les fichiers spécifiés dans Managed
            DeleteSpecificFiles(managedPath);

            // Supprimer steam_appid.txt
            SafeDeleteFile(Path.Combine(hollowKnightPath, "steam_appid.txt"));

            // Restaurer les DLLs originaux depuis la sauvegarde
            RestoreOriginalDLLs(managedPath);

            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    void DeleteSpecificFiles(string managedPath)
    {
        if (!Directory.Exists(managedPath))
            return;

        foreach (string fileName in FILES_TO_DELETE)
        {
            string filePath = Path.Combine(managedPath, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    SafeDeleteFile(filePath);
                    UnityEngine.Debug.Log("Deleted: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete " + fileName + ": " + e.Message);
                }
            }
        }
    }

    void RestoreOriginalDLLs(string managedPath)
    {
        string backupPath = Path.Combine(Application.persistentDataPath, "HollowKnightDLLBackup");

        if (!Directory.Exists(backupPath))
        {
            UnityEngine.Debug.LogWarning("No DLL backup found. Skipping DLL restoration.");
            return;
        }

        try
        {
            foreach (string dll in BACKUP_DLLS)
            {
                string backupFile = Path.Combine(backupPath, dll);
                string targetFile = Path.Combine(managedPath, dll);

                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, targetFile, true);
                    UnityEngine.Debug.Log("Restored original DLL: " + dll);
                }
            }

            SafeDeleteDirectory(backupPath);
            UnityEngine.Debug.Log("DLL backup folder cleaned up");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error restoring DLLs: " + e.Message);
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        try
        {
            string[] dirs = Directory.GetDirectories(modsPath);

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);

                // Ignorer le dossier Disabled
                if (dirName == "Disabled")
                    continue;

                // Vérifier si c'est un mod AP (AP mod ou une de ses dependencies)
                if (System.Array.Exists(AP_MOD_ONLY, element => element == dirName))
                    continue;

                // Vérifier si c'est un mod Map (Map mod ou une de ses dependencies)
                if (System.Array.Exists(MAP_MOD_DEPENDENCIES, element => element == dirName))
                    continue;

                // C'est un autre mod
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
        ShowInfo("Installing AP Mod...");
        yield return StartCoroutine(InstallAPMod());

        CreateVersionFile(apMod.url);

        // Créer le fichier steam_appid.txt
        CreateSteamAppIDFile();

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching Hollow Knight...");
            yield return StartCoroutine(LaunchHollowKnight());
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    void CreateSteamAppIDFile()
    {
        try
        {
            string steamAppIDPath = Path.Combine(hollowKnightPath, "steam_appid.txt");
            File.WriteAllText(steamAppIDPath, "367520");
            UnityEngine.Debug.Log("Created steam_appid.txt at: " + steamAppIDPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating steam_appid.txt: " + e.Message);
        }
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. AP Mod URL: " + apMod.url);

        if (string.IsNullOrEmpty(apMod.url))
        {
            ShowInfo("ERROR: AP Mod URL is empty!");
            UnityEngine.Debug.LogError("AP Mod URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "HKAPModTemp");
        string managedPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed");
        string modsPath = Path.Combine(managedPath, "Mods");

        yield return StartCoroutine(downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath));

        // Le ZIP contient Preloaded-Lumafly-HKAP
        string preloadedPath = Path.Combine(extractPath, "Preloaded-Lumafly-HKAP");

        if (!Directory.Exists(preloadedPath))
        {
            UnityEngine.Debug.LogError("Preloaded-Lumafly-HKAP folder not found in extracted files");
            ShowInfo("ERROR: Invalid AP Mod structure!");
            yield break;
        }

        Directory.CreateDirectory(managedPath);

        // Sauvegarder les DLLs originaux avant d'installer
        BackupOriginalDLLs(managedPath);

        // Copier tout le contenu de Preloaded-Lumafly-HKAP vers Managed
        try
        {
            CopyDirectory(preloadedPath, managedPath);
            UnityEngine.Debug.Log("Copied Preloaded-Lumafly-HKAP content to Managed");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying AP Mod: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
            yield break;
        }

        // Nettoyer les mods optionnels
        yield return StartCoroutine(CleanupOptionalMods());

        SafeDeleteDirectory(extractPath);

        ShowInfo("AP Mod installed successfully!");
    }

    void BackupOriginalDLLs(string managedPath)
    {
        string backupPath = Path.Combine(Application.persistentDataPath, "HollowKnightDLLBackup");

        // Supprimer l'ancienne sauvegarde si elle existe
        if (Directory.Exists(backupPath))
        {
            SafeDeleteDirectory(backupPath);
        }

        Directory.CreateDirectory(backupPath);

        try
        {
            foreach (string dll in BACKUP_DLLS)
            {
                string sourceDll = Path.Combine(managedPath, dll);
                string backupDll = Path.Combine(backupPath, dll);

                if (File.Exists(sourceDll))
                {
                    File.Copy(sourceDll, backupDll, true);
                    UnityEngine.Debug.Log("Backed up original DLL: " + dll);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Original DLL not found for backup: " + dll);
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error backing up DLLs: " + e.Message);
        }
    }

    IEnumerator CleanupOptionalMods()
    {
        string modsPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed", "Mods");

        if (!Directory.Exists(modsPath))
            yield break;

        // Supprimer AP Mod et ses dependencies si le toggle est désactivé
        if (installAPModToggle == null || !installAPModToggle.isOn)
        {
            foreach (string mod in AP_MOD_ONLY)
            {
                SafeDeleteDirectory(Path.Combine(modsPath, mod));
            }
            UnityEngine.Debug.Log("Archipelago AP Mod removed (toggle disabled)");
        }

        // Supprimer Map Mod et ses dependencies si le toggle est désactivé
        if (installMapModToggle == null || !installMapModToggle.isOn)
        {
            foreach (string mod in MAP_MOD_DEPENDENCIES)
            {
                SafeDeleteDirectory(Path.Combine(modsPath, mod));
            }
            UnityEngine.Debug.Log("Archipelago Map Mod removed (toggle disabled)");
        }

        yield return null;
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
            remoteConfig = JsonUtility.FromJson<HollowKnightConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyHollowKnightConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    IEnumerator LaunchHollowKnight()
    {
        if (string.IsNullOrEmpty(hollowKnightPath))
        {
            ShowInfo("ERROR: Hollow Knight path not found!");
            yield break;
        }

        string exePath = Path.Combine(hollowKnightPath, "hollow_knight.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("ERROR: hollow_knight.exe not found at " + exePath);
            UnityEngine.Debug.LogError("hollow_knight.exe not found at: " + exePath);
            yield break;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = exePath,
            WorkingDirectory = hollowKnightPath,
            UseShellExecute = true
        };

        try
        {
            hollowKnightProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Launched Hollow Knight from: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Hollow Knight: " + e.Message);
            ShowInfo("ERROR: Failed to launch Hollow Knight\n" + e.Message);
            yield break;
        }

        yield return new WaitForSeconds(3f);

        // Get all Hollow Knight processes
        Process[] processes = Process.GetProcessesByName("hollow_knight");

        if (processes.Length > 1)
        {
            UnityEngine.Debug.Log("Found " + processes.Length + " Hollow Knight processes. Closing the duplicate...");

            for (int i = 1; i < processes.Length; i++)
            {
                try
                {
                    processes[i].Kill();
                    UnityEngine.Debug.Log("Closed duplicate Hollow Knight process");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Failed to close duplicate process: " + e.Message);
                }
            }
        }
    }

    void CleanupProcesses()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("hollow_knight");
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
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete file: " + path + " - " + e.Message);
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

    string GetHollowKnightPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Hollow Knight"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Hollow Knight"),
            @"D:\Steam\steamapps\common\Hollow Knight",
            @"D:\SteamLibrary\steamapps\common\Hollow Knight",
            @"D:\steamapps\common\Hollow Knight",
            @"E:\Steam\steamapps\common\Hollow Knight",
            @"E:\SteamLibrary\steamapps\common\Hollow Knight",
            @"E:\steamapps\common\Hollow Knight",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Hollow Knight at: " + path);
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
                    string hkPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Hollow Knight");
                    if (Directory.Exists(hkPath))
                        return hkPath;

                    hkPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Hollow Knight");
                    if (Directory.Exists(hkPath))
                        return hkPath;

                    hkPath = Path.Combine(drive.Name, "steamapps", "common", "Hollow Knight");
                    if (Directory.Exists(hkPath))
                        return hkPath;

                    hkPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Hollow Knight");
                    if (Directory.Exists(hkPath))
                        return hkPath;

                    hkPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Hollow Knight");
                    if (Directory.Exists(hkPath))
                        return hkPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Hollow Knight not found.");
        return "";
    }

    void CreateVersionFile(string apModUrl)
    {
        try
        {
            string apModVersion = ExtractVersionFromUrl(apModUrl, "");

            string versionFileName = "Hollow Knight Archipelago Version " + apModVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apModUrl + "\n";
            content += "Version: " + apModVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string modsPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed", "Mods");
            if (Directory.Exists(modsPath))
            {
                string versionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(versionPath, content);
                UnityEngine.Debug.Log("Version file created: " + versionPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteVersionFiles(string modsPath)
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Hollow Knight Archipelago Version .+\.txt");

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
                            UnityEngine.Debug.Log("Deleted version file: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete version file: " + e.Message);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up version files: " + e.Message);
        }
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Hollow Knight Archipelago Version .+\.txt");

            string modsPath = Path.Combine(hollowKnightPath, "hollow_knight_Data", "Managed", "Mods");
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
                            UnityEngine.Debug.Log("Deleted old version file: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file: " + e.Message);
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
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }
}
