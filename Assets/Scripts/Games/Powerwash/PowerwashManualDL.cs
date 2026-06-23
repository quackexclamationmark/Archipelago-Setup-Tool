using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class PowerwashManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("POWERWASH FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanBepInExToggle;
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

    private Process powerwashProcess;
    private string powerwashPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private PowerwashConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class PowerwashConfig
    {
        public string powerwashAP;
        public string powerwashBepInEx;
        public string powerwashApworld;
    }

    void Start()
    {
        powerwashPath = GetPowerwashPath();
        StartCoroutine(LoadRemoteConfig());

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            if (value)
            {
                removeAPModsOnlyToggle.isOn = false;
                removeAPModsOnlyToggle.interactable = false;
            }
            else
            {
                removeAPModsOnlyToggle.interactable = true;
            }
        }
    }

    void ApplyPowerwashConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.powerwashAP;
        bepInEx.url = remoteConfig.powerwashBepInEx;
        apworld.url = remoteConfig.powerwashApworld;
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
        if (string.IsNullOrEmpty(powerwashPath))
        {
            ShowInfo("PowerWash Simulator path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
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
        powerwashPath = GetPowerwashPath();

        if (string.IsNullOrEmpty(powerwashPath))
            return;

        string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (removeAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(pluginsPath, "SW_CreeperKing.ArchipelagoMod"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClean &&
            hasOtherMods &&
            !pendingFullCleanConfirmation)
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

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(powerwashPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(powerwashPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(powerwashPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(powerwashPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(powerwashPath, ".doorstop_version"));
            DeleteOldVersionFiles();

            ShowInfo("Full clean completed!");
            return;
        }

        ShowInfo("Removing mods...");

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SW_CreeperKing.ArchipelagoMod"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));
        DeleteOldVersionFiles();

        ShowInfo("Revert completed!");
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

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "SW_CreeperKing.ArchipelagoMod")
                return true;
        }

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            // Ignorer les fichiers de version PowerWash APMod
            if (fileName.StartsWith("PowerWash APMod Version") && fileName.EndsWith(".txt"))
                continue;

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
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url);

        ShowInfo("Launching PowerWash Simulator...");
        LaunchPowerwash();

        yield return WaitForConfigFiles();

        ClosePowerwash();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchPowerwash();
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
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string targetFolder = null;

        string[] possiblePaths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Archipelago", "custom_worlds"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds"),
        };

        foreach (string path in possiblePaths)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                targetFolder = path;
                UnityEngine.Debug.Log("Using target folder: " + targetFolder);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + path + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    string path = Path.Combine(drive.RootDirectory.FullName, "Archipelago", "custom_worlds");

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    targetFolder = path;
                    UnityEngine.Debug.Log("Using target folder on drive: " + targetFolder);
                    break;
                }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            ShowInfo("Archipelago Launcher is not installed.\nPlease install it before using APWorld.");
            UnityEngine.Debug.LogError("No valid Archipelago folder found!");
            yield break;
        }

        string target = Path.Combine(targetFolder, fileName);

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
            UnityEngine.Debug.LogError("APWorld install failed: " + e.Message);
            ShowInfo("Failed to install APWorld.");
            yield break;
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
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "PowerwashAPModTemp");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string modFolder = Path.Combine(extractPath, "SW_CreeperKing.ArchipelagoMod");
        string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");

        Directory.CreateDirectory(pluginsPath);

        if (Directory.Exists(modFolder))
        {
            string targetMod = Path.Combine(pluginsPath, "SW_CreeperKing.ArchipelagoMod");

            if (Directory.Exists(targetMod))
                Directory.Delete(targetMod, true);

            CopyDirectory(modFolder, targetMod);
            UnityEngine.Debug.Log("AP Mod installed successfully!");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, powerwashPath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();
        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        ShowInfo("Launching PowerWash Simulator...");
        LaunchPowerwash();
        yield return WaitForConfigFiles();
        ClosePowerwash();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchPowerwash();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(powerwashPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfg))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
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
            remoteConfig = JsonUtility.FromJson<PowerwashConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyPowerwashConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchPowerwash()
    {
        string exePath = Path.Combine(powerwashPath, "PowerWashSimulator.exe");

        if (File.Exists(exePath))
            powerwashProcess = Process.Start(exePath);
    }

    void ClosePowerwash()
    {
        try
        {
            if (powerwashProcess != null && !powerwashProcess.HasExited)
            {
                powerwashProcess.Kill();
                powerwashProcess.Dispose();
                powerwashProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        ClosePowerwash();
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        Directory.Delete(source, true);
    }

    string GetPowerwashPath()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "PowerWash Simulator"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "PowerWash Simulator"),
        @"D:\Steam\steamapps\common\PowerWash Simulator",
        @"D:\SteamLibrary\steamapps\common\PowerWash Simulator",
        @"D:\steamapps\common\PowerWash Simulator",
        @"E:\Steam\steamapps\common\PowerWash Simulator",
        @"E:\SteamLibrary\steamapps\common\PowerWash Simulator",
        @"E:\steamapps\common\PowerWash Simulator",
        @"E:\Program Files (x86)\steamapps\common\PowerWash Simulator",
        @"E:\Program Files\steamapps\common\PowerWash Simulator",
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
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                try
                {
                    // Cherche Steam\steamapps
                    string path = Path.Combine(drive.RootDirectory.FullName, "Steam", "steamapps", "common", "PowerWash Simulator");
                    if (Directory.Exists(path))
                        return path;

                    // Cherche SteamLibrary\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "PowerWash Simulator");
                    if (Directory.Exists(path))
                        return path;

                    // Cherche directement steamapps à la racine du disque
                    path = Path.Combine(drive.RootDirectory.FullName, "steamapps", "common", "PowerWash Simulator");
                    if (Directory.Exists(path))
                        return path;

                    // Cherche dans Program Files (x86)\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "steamapps", "common", "PowerWash Simulator");
                    if (Directory.Exists(path))
                        return path;

                    // Cherche dans Program Files\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "Program Files", "steamapps", "common", "PowerWash Simulator");
                    if (Directory.Exists(path))
                        return path;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string bepinexUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/releases/download/([^/]+)/");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = "N/A";

            string versionFileName = "PowerWash APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(powerwashPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"PowerWash APMod Version .+\.txt");

            // Supprimer les fichiers à la racine
            string[] rootFiles = Directory.GetFiles(powerwashPath);
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

            // Supprimer les fichiers dans plugins
            string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");
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
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }
}
