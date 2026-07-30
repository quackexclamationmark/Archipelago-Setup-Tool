using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class HuniePop2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("HUNIEPOP2 FILES")]
    public FileDownloader.FileData huniepop2Apworld;
    public FileDownloader.FileData huniepop2AP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "HuniePop 2 - Double Date";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installBepInExToggle;
    public Toggle installAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process hunie2Process;
    private string hunie2Path;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private Hunie2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class Hunie2Config
    {
        public string huniepop2Apworld;
        public string huniepop2AP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        hunie2Path = GetHuniePop2Path();
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

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.isOn = false;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;
    }

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void ApplyHunieConfig()
    {
        if (remoteConfig == null)
            return;

        huniepop2Apworld.url = remoteConfig.huniepop2Apworld;
        huniepop2AP.url = remoteConfig.huniepop2AP;
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
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
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
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        hunie2Path = GetHuniePop2Path();

        if (string.IsNullOrEmpty(hunie2Path))
        {
            ShowInfo("HuniePop 2 not found in Steam. Please check installation.");
            return;
        }

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        int count = (bep ? 1 : 0) + (ap ? 1 : 0) + (apworld ? 1 : 0);

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (ap && count == 1 && !bep)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        hunie2Path = GetHuniePop2Path();

        if (string.IsNullOrEmpty(hunie2Path))
            return;

        string pluginsPath = Path.Combine(hunie2Path, "BepInEx", "plugins");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            // Remove plugin folder
            string pluginDir = Path.Combine(pluginsPath, "HunniePop2ArchipelagoClient");
            SafeDeleteDirectory(pluginDir);

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClear &&
            hasOtherMods &&
            !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        if (fullClear)
        {
            ShowInfo("Clearing BepInEx...");

            SafeDeleteDirectory(Path.Combine(hunie2Path, "BepInEx"));
            SafeDeleteFile(Path.Combine(hunie2Path, "changelog.txt"));
            SafeDeleteFile(Path.Combine(hunie2Path, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(hunie2Path, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(hunie2Path, "winhttp.dll"));
            DeleteOldVersionFiles();

            ShowInfo("Full clear completed!");
            return;
        }

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

        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "HunniePop2ArchipelagoClient")
                return true;
        }

        string[] files = Directory.GetFiles(pluginsPath);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            if (fileName.StartsWith("HuniePop2 AP Version") && fileName.EndsWith(".txt"))
                continue;
        }

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "HuniePop2APTemp");

        ShowInfo("Downloading package...");
        yield return downloader.DownloadAndExtract(huniepop2AP, Application.persistentDataPath, extractPath);

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        string extractedPluginDir = Path.Combine(extractPath, "HuniePop 2 Double Date", "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
        string altExtractedPluginDir = Path.Combine(extractPath, "BepInEx", "plugins", "HunniePop2ArchipelagoClient");

        string pluginDirUsed = Directory.Exists(extractedPluginDir) ? extractedPluginDir : (Directory.Exists(altExtractedPluginDir) ? altExtractedPluginDir : null);

        if (bep)
        {
            ShowInfo("Installing files to HuniePop 2...");

            if (!ap)
            {
                try
                {
                    if (pluginDirUsed != null && Directory.Exists(pluginDirUsed))
                        Directory.Delete(pluginDirUsed, true);
                }
                catch { }
            }

            string extractedRootWithGameFolder = Path.Combine(extractPath, "HuniePop 2 Double Date");
            if (Directory.Exists(extractedRootWithGameFolder))
                MoveDirectory(extractedRootWithGameFolder, hunie2Path);
            else
                MoveDirectory(extractPath, hunie2Path);

            UnityEngine.Debug.Log("HuniePop2 content installed successfully!");
        }

        if (ap)
        {
            ShowInfo("Installing AP Mod...");

            string extractedPlugin = null;
            if (Directory.Exists(Path.Combine(extractPath, "HuniePop 2 Double Date", "BepInEx", "plugins", "HunniePop2ArchipelagoClient")))
                extractedPlugin = Path.Combine(extractPath, "HuniePop 2 Double Date", "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
            else if (Directory.Exists(Path.Combine(extractPath, "BepInEx", "plugins", "HunniePop2ArchipelagoClient")))
                extractedPlugin = Path.Combine(extractPath, "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
            else if (pluginDirUsed != null)
                extractedPlugin = pluginDirUsed;

            string targetPluginsPath = Path.Combine(hunie2Path, "BepInEx", "plugins");

            if (!string.IsNullOrEmpty(extractedPlugin) && Directory.Exists(extractedPlugin))    
            {
                if (!bep)
                {
                    // Only install plugin if BepInEx exists in game dir
                    if (Directory.Exists(Path.Combine(hunie2Path, "BepInEx")))
                    {
                        Directory.CreateDirectory(targetPluginsPath);
                        string targetPluginDest = Path.Combine(targetPluginsPath, "HunniePop2ArchipelagoClient");
                        SafeDeleteDirectory(targetPluginDest);
                        CopyDirectory(extractedPlugin, targetPluginDest);
                        UnityEngine.Debug.Log("AP plugin copied into existing BepInEx plugins.");
                    }
                    else
                    {
                        ShowInfo("BepInEx not found in HuniePop 2. AP plugin will not be installed.");
                        UnityEngine.Debug.LogWarning("BepInEx not present; skipping plugin install.");
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("AP plugin included with BepInEx install.");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("AP plugin directory not found inside archive.");
            }
        }

        SafeDeleteDirectory(extractPath);

        if (ap || bep)
            CreateVersionFile(huniepop2AP.url);

        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching HuniePop 2...");
            LaunchHuniePop2();

            yield return new WaitForSeconds(2f);

            CloseHuniePop2();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchHuniePop2();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + huniepop2Apworld.url);

        if (string.IsNullOrEmpty(huniepop2Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = huniepop2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = huniepop2Apworld.url.Substring(huniepop2Apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + huniepop2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(huniepop2Apworld.url, localPath);

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

    IEnumerator APOnlyFlow()
    {
        while (!configLoaded)
            yield return null;

        ShowInfo("Installing AP Mod...");

        string extractPath = Path.Combine(Application.persistentDataPath, "HuniePop2APTemp");   
        yield return downloader.DownloadAndExtract(huniepop2AP, Application.persistentDataPath, extractPath);

        string extractedPluginDir = Path.Combine(extractPath, "HuniePop 2 Double Date", "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
        string altExtractedPluginDir = Path.Combine(extractPath, "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
        string pluginDir = Directory.Exists(extractedPluginDir) ? extractedPluginDir : (Directory.Exists(altExtractedPluginDir) ? altExtractedPluginDir : null);

        string targetPluginsPath = Path.Combine(hunie2Path, "BepInEx", "plugins");

        if (!string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir))
        {
            if (Directory.Exists(Path.Combine(hunie2Path, "BepInEx")))
            {
                Directory.CreateDirectory(targetPluginsPath);
                string targetPluginDest = Path.Combine(targetPluginsPath, "HunniePop2ArchipelagoClient");
                SafeDeleteDirectory(targetPluginDest);
                CopyDirectory(pluginDir, targetPluginDest);
                UnityEngine.Debug.Log("AP plugin copied into existing BepInEx plugins.");
                CreateVersionFile(huniepop2AP.url);
                ShowInfo("Installation complete!");
            }
            else
            {
                ShowInfo("BepInEx not found in HuniePop 2. AP plugin was not installed.");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("AP plugin folder not found inside archive.");
            ShowInfo("AP plugin not found in archive.");
        }

        SafeDeleteDirectory(extractPath);
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        while (!configLoaded)
            yield return null;

        ShowInfo("Installing BepInEx and files...");

        string extractPath = Path.Combine(Application.persistentDataPath, "HuniePop2APTemp");
        yield return downloader.DownloadAndExtract(huniepop2AP, Application.persistentDataPath, extractPath);

        // Try to remove plugin folder inside extracted tree to avoid installing the plugin when user only wanted BepInEx
        string extractedPlugin1 = Path.Combine(extractPath, "HuniePop 2 Double Date", "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
        string extractedPlugin2 = Path.Combine(extractPath, "BepInEx", "plugins", "HunniePop2ArchipelagoClient");
        try
        {
            if (Directory.Exists(extractedPlugin1))
                Directory.Delete(extractedPlugin1, true);
            if (Directory.Exists(extractedPlugin2))
                Directory.Delete(extractedPlugin2, true);
        }
        catch { }

        string extractedRootWithGameFolder = Path.Combine(extractPath, "HuniePop 2 Double Date");
        if (Directory.Exists(extractedRootWithGameFolder))
            MoveDirectory(extractedRootWithGameFolder, hunie2Path);
        else
            MoveDirectory(extractPath, hunie2Path);

        CreateVersionFile(huniepop2AP.url);

        SafeDeleteDirectory(extractPath);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching HuniePop 2...");
            LaunchHuniePop2();
            yield return new WaitForSeconds(2f);
            CloseHuniePop2();

            ShowInfo("Second launch...");
            LaunchHuniePop2();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    void LaunchHuniePop2()
    {
        string exePath = Path.Combine(hunie2Path, "HuniePop 2 - Double Date.exe");

        try
        {
            if (File.Exists(exePath))
                hunie2Process = Process.Start(exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch HuniePop 2: " + e.Message);
        }
    }

    void CloseHuniePop2()
    {
        try
        {
            if (hunie2Process != null && !hunie2Process.HasExited)
            {
                hunie2Process.Kill();
                hunie2Process.Dispose();
                hunie2Process = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseHuniePop2();
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

        try
        {
            Directory.Delete(source, true);
        }
        catch { }
    }

    void CreateVersionFile(string apUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "HuniePop2 AP Version " + apVersion + ".txt";
            string content = "HuniePop2 AP Setup Tool\n";
            content += "https://github.com/yourusername/HuniePop2-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(hunie2Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(hunie2Path, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"HuniePop2 AP Version .+\.txt");

            if (Directory.Exists(hunie2Path))
            {
                string[] rootFiles = Directory.GetFiles(hunie2Path);
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
            }

            string pluginsPath = Path.Combine(hunie2Path, "BepInEx", "plugins");
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
        try
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
            System.Text.RegularExpressions.Match match = regex.Match(url);

            if (match.Success)
                return match.Groups[1].Value;
        }
        catch { }

        if (!string.IsNullOrEmpty(url))
        {
            int idx = url.LastIndexOf('/');
            if (idx >= 0 && idx + 1 < url.Length)
            {
                string candidate = url.Substring(idx + 1);
                return candidate;
            }
        }

        return "Unknown";
    }

    string GetHuniePop2Path()
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
            remoteConfig = JsonUtility.FromJson<Hunie2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyHunieConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        hunie2Path = GetHuniePop2Path();
    }
}