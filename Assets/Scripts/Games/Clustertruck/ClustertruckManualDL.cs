using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class ClustertruckManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CLUSTERTRUCK FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installBepInExToggle;
    public Toggle installAPModToggle;
    public Toggle installConfigurationManagerToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsOnlyToggle;
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process clustertruckProcess;
    private string clustertruckPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private ClustertruckConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class ClustertruckConfig
    {
        public string clustertruckApworld;
        public string clustertruckBepInEx;
    }

    void Start()
    {
        clustertruckPath = GetClustertruckPath();
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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (clearAPModsOnlyToggle != null)
        {
            clearAPModsOnlyToggle.isOn = false;
            clearAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyClustertruckConfig()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.clustertruckApworld;
        bepInEx.url = remoteConfig.clustertruckBepInEx;
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
        if (string.IsNullOrEmpty(clustertruckPath))
        {
            ShowInfo("Clustertruck path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        clustertruckPath = GetClustertruckPath();

        if (string.IsNullOrEmpty(clustertruckPath))
            return;

        string pluginsPath = Path.Combine(clustertruckPath, "BepInEx", "plugins");

        bool clearAP = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            // Supprimer le dossier ArchipelagoClusterTruck
            SafeDeleteDirectory(Path.Combine(pluginsPath, "ArchipelagoClusterTruck"));

            // Supprimer le dossier ConfigurationManager
            SafeDeleteDirectory(Path.Combine(pluginsPath, "ConfigurationManager"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        // Supprimer le dossier ArchipelagoClusterTruck
        SafeDeleteDirectory(Path.Combine(pluginsPath, "ArchipelagoClusterTruck"));

        // Supprimer le dossier ConfigurationManager
        SafeDeleteDirectory(Path.Combine(pluginsPath, "ConfigurationManager"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(clustertruckPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(clustertruckPath, "unstripped"));
            SafeDeleteFile(Path.Combine(clustertruckPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(clustertruckPath, "winhttp.dll"));

            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        try
        {
            string[] files = Directory.GetFiles(pluginsPath);
            string[] dirs = Directory.GetDirectories(pluginsPath);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);

                // Whitelist le version file
                if (name.StartsWith("Clustertruck Archipelago Version") && name.EndsWith(".txt"))
                    continue;

                return true;
            }

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName != "ArchipelagoClusterTruck" && dirName != "ConfigurationManager")
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
            yield return StartCoroutine(InstallAPWorld());
        }

        if (installBepInExToggle == null || installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return StartCoroutine(InstallBepInEx());

            // Supprimer les mods optionnels après l'installation de BepInEx
            yield return StartCoroutine(CleanupOptionalMods());
        }

        CreateVersionFile(apworld.url, bepInEx.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching Clustertruck...");
            yield return StartCoroutine(LaunchClustertruck());
        }
        else
        {
            ShowInfo("Installation complete!");
        }
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

        yield return StartCoroutine(DownloadFile(apworld.url, localPath));

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

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return StartCoroutine(downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath));

        // Copie TOUT le contenu du dossier extrait directement dans le répertoire du jeu
        try
        {
            // Copie tous les fichiers du root
            foreach (string file in Directory.GetFiles(extractPath))
            {
                string destFile = Path.Combine(clustertruckPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                UnityEngine.Debug.Log("Copied file: " + Path.GetFileName(file));
            }

            // Copie tous les dossiers du root
            foreach (string dir in Directory.GetDirectories(extractPath))
            {
                string destDir = Path.Combine(clustertruckPath, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
                UnityEngine.Debug.Log("Copied directory: " + Path.GetFileName(dir));
            }

            UnityEngine.Debug.Log("BepInEx content copied to game directory");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying BepInEx: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator CleanupOptionalMods()
    {
        string pluginsPath = Path.Combine(clustertruckPath, "BepInEx", "plugins");

        if (!Directory.Exists(pluginsPath))
            yield break;

        // Supprimer ArchipelagoClusterTruck si le toggle est désactivé
        if (installAPModToggle == null || !installAPModToggle.isOn)
        {
            SafeDeleteDirectory(Path.Combine(pluginsPath, "ArchipelagoClusterTruck"));
            UnityEngine.Debug.Log("ArchipelagoClusterTruck removed (toggle disabled)");
        }

        // Supprimer ConfigurationManager si le toggle est désactivé
        if (installConfigurationManagerToggle == null || !installConfigurationManagerToggle.isOn)
        {
            SafeDeleteDirectory(Path.Combine(pluginsPath, "ConfigurationManager"));
            UnityEngine.Debug.Log("ConfigurationManager removed (toggle disabled)");
        }

        yield return null;
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/clustertruck_config.json";

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
            remoteConfig = JsonUtility.FromJson<ClustertruckConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyClustertruckConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    IEnumerator LaunchClustertruck()
    {
        if (string.IsNullOrEmpty(clustertruckPath))
        {
            ShowInfo("ERROR: Clustertruck path not found!");
            yield break;
        }

        string exePath = Path.Combine(clustertruckPath, "Clustertruck.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("ERROR: Clustertruck.exe not found at " + exePath);
            UnityEngine.Debug.LogError("Clustertruck.exe not found at: " + exePath);
            yield break;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = exePath,
            WorkingDirectory = clustertruckPath,
            UseShellExecute = true
        };

        try
        {
            clustertruckProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Launched Clustertruck from: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Clustertruck: " + e.Message);
            ShowInfo("ERROR: Failed to launch Clustertruck\n" + e.Message);
            yield break;
        }

        // Wait for the first process to fully initialize
        yield return new WaitForSeconds(3f);

        // Get all Clustertruck processes
        Process[] processes = Process.GetProcessesByName("Clustertruck");

        if (processes.Length > 1)
        {
            UnityEngine.Debug.Log("Found " + processes.Length + " Clustertruck processes. Closing the duplicate...");

            // Keep the first one, close any duplicates
            for (int i = 1; i < processes.Length; i++)
            {
                try
                {
                    processes[i].Kill();
                    UnityEngine.Debug.Log("Closed duplicate Clustertruck process");
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
        // Close Clustertruck if it's running
        try
        {
            Process[] processes = Process.GetProcessesByName("Clustertruck");
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

    string GetClustertruckPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "ClusterTruck"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "ClusterTruck"),
            @"D:\Steam\steamapps\common\ClusterTruck",
            @"D:\SteamLibrary\steamapps\common\ClusterTruck",
            @"D:\steamapps\common\ClusterTruck",
            @"E:\Steam\steamapps\common\ClusterTruck",
            @"E:\SteamLibrary\steamapps\common\ClusterTruck",
            @"E:\steamapps\common\ClusterTruck",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Clustertruck at: " + path);
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
                    string ctPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "ClusterTruck");
                    if (Directory.Exists(ctPath))
                        return ctPath;

                    ctPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "ClusterTruck");
                    if (Directory.Exists(ctPath))
                        return ctPath;

                    ctPath = Path.Combine(drive.Name, "steamapps", "common", "ClusterTruck");
                    if (Directory.Exists(ctPath))
                        return ctPath;

                    ctPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "ClusterTruck");
                    if (Directory.Exists(ctPath))
                        return ctPath;

                    ctPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "ClusterTruck");
                    if (Directory.Exists(ctPath))
                        return ctPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Clustertruck not found.");
        return "";
    }

    void CreateVersionFile(string apworldUrl, string bepinexUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "Clustertruck Archipelago Version " + apworldVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string pluginsPath = Path.Combine(clustertruckPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Clustertruck Archipelago Version .+\.txt");

            string pluginsPath = Path.Combine(clustertruckPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string[] pluginsFiles = Directory.GetFiles(pluginsPath);
                foreach (string file in pluginsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in plugins: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in plugins: " + e.Message);
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
