using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class BATIMManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("BATIM FILES")]
    public FileDownloader.FileData batimApworld;
    public FileDownloader.FileData batimBepInEx;
    public FileDownloader.FileData batimAP;

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

    private Process batimProcess;
    private string batimPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private BATIMConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class BATIMConfig
    {
        public string batimApworld;
        public string batimBepInEx;
        public string batimAP;
    }

    void Start()
    {
        batimPath = GetBATIMPath();
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

    void ApplyBATIMConfig()
    {
        if (remoteConfig == null)
            return;

        batimApworld.url = remoteConfig.batimApworld;
        batimBepInEx.url = remoteConfig.batimBepInEx;
        batimAP.url = remoteConfig.batimAP;
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
        bool bepinex = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        if (!apworld && !bepinex && !ap)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        if ((bepinex || ap) && string.IsNullOrEmpty(batimPath))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (bepinex ? 1 : 0) + (ap ? 1 : 0) + (apworld ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }
       

        if (bepinex && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (ap && count == 1 && !bepinex)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchBATIM();
    }

    private void ExecuteRevert()
    {
        batimPath = GetBATIMPath();

        if (string.IsNullOrEmpty(batimPath))
            return;

        string pluginsPath = Path.Combine(batimPath, "BepInEx", "plugins");

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
            string pluginDir = Path.Combine(pluginsPath, "BendyAndTheArchipelagoMachine");
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

            SafeDeleteDirectory(Path.Combine(batimPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(batimPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(batimPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(batimPath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(batimPath, "winhttp.dll"));
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

            if (name != "BendyAndTheArchipelagoMachine")
                return true;
        }

        string[] files = Directory.GetFiles(pluginsPath);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            if (fileName.StartsWith("BATIM AP Version") && fileName.EndsWith(".txt"))
                continue;

            if (fileName != "BendyAndTheArchipelagoMachine.dll")
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
        while (!configLoaded)
            yield return null;

        bool bepinex = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        // First, install BepInEx if needed
        if (bepinex || ap)
        {
            ShowInfo("Installing BepInEx...");
            string bepinexExtractPath = Path.Combine(Application.persistentDataPath, "BATIMBepInExTemp");
            yield return downloader.DownloadAndExtract(batimBepInEx, Application.persistentDataPath, bepinexExtractPath);

            // Move BepInEx content to BATIM directory
            MoveDirectory(bepinexExtractPath, batimPath);
        }

        // Then, install AP mod if needed
        if (ap)
        {
            ShowInfo("Installing AP Mod...");
            string apExtractPath = Path.Combine(Application.persistentDataPath, "BATIMAPTemp");
            yield return downloader.DownloadAndExtract(batimAP, Application.persistentDataPath, apExtractPath);

            // Extract the AP mod's BepInEx folder
            string apBepInExPath = Path.Combine(apExtractPath, "BepInEx");
            string targetBepInExPath = Path.Combine(batimPath, "BepInEx");

            if (Directory.Exists(apBepInExPath))
            {
                // Replace the BepInEx folder with the one from AP
                SafeDeleteDirectory(targetBepInExPath);
                CopyDirectory(apBepInExPath, targetBepInExPath);
                UnityEngine.Debug.Log("BepInEx replaced with AP version");
            }

            SafeDeleteDirectory(apExtractPath);
            CreateVersionFile(batimAP.url);
        }

        // Install APWorld if needed
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        // Handle second launch if needed
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Bendy and the Ink Machine...");
            LaunchBATIM();

            yield return new WaitForSeconds(2f);

            CloseBATIM();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchBATIM();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + batimApworld.url);

        if (string.IsNullOrEmpty(batimApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = batimApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = batimApworld.url.Substring(batimApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + batimApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(batimApworld.url, localPath);

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

        string extractPath = Path.Combine(Application.persistentDataPath, "BATIMAPTemp");
        yield return downloader.DownloadAndExtract(batimAP, Application.persistentDataPath, extractPath);

        string extractedPluginDir = Path.Combine(extractPath, "BepInEx", "plugins", "BendyAndTheArchipelagoMachine");
        string targetPluginsPath = Path.Combine(batimPath, "BepInEx", "plugins");

        if (Directory.Exists(extractedPluginDir))
        {
            if (Directory.Exists(Path.Combine(batimPath, "BepInEx")))
            {
                Directory.CreateDirectory(targetPluginsPath);
                string targetPluginDest = Path.Combine(targetPluginsPath, "BendyAndTheArchipelagoMachine");
                SafeDeleteDirectory(targetPluginDest);
                CopyDirectory(extractedPluginDir, targetPluginDest);
                UnityEngine.Debug.Log("AP plugin copied into existing BepInEx plugins.");
                CreateVersionFile(batimAP.url);
                ShowInfo("Installation complete!");
            }
            else
            {
                ShowInfo("BepInEx not found in BATIM. Setup BepInEx too.");
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

        string extractPath = Path.Combine(Application.persistentDataPath, "BATIMBepInExTemp");
        yield return downloader.DownloadAndExtract(batimBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, batimPath);
        CreateVersionFile(batimBepInEx.url);

        SafeDeleteDirectory(extractPath);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Bendy and the Ink Machine...");
            LaunchBATIM();
            yield return new WaitForSeconds(2f);
            CloseBATIM();

            ShowInfo("Second launch...");
            LaunchBATIM();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    void LaunchBATIM()
    {
        string exePath = Path.Combine(batimPath, "Bendy and the Ink Machine.exe");

        try
        {
            if (File.Exists(exePath))
                batimProcess = Process.Start(exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch Bendy and the Ink Machine: " + e.Message);
        }
    }

    void CloseBATIM()
    {
        try
        {
            if (batimProcess != null && !batimProcess.HasExited)
            {
                batimProcess.Kill();
                batimProcess.Dispose();
                batimProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseBATIM();
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

            string versionFileName = "BATIM AP Version " + apVersion + ".txt";
            string content = "Bendy and the Ink Machine AP Setup Tool\n";
            content += "https://github.com/yourusername/Bendy-And-The-Ink-Machine-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(batimPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(batimPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"BATIM AP Version .+\.txt");

            if (Directory.Exists(batimPath))
            {
                string[] rootFiles = Directory.GetFiles(batimPath);
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

            string pluginsPath = Path.Combine(batimPath, "BepInEx", "plugins");
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

    string GetBATIMPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Bendy and the Ink Machine"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Bendy and the Ink Machine"),
            @"D:\Steam\steamapps\common\Bendy and the Ink Machine",
            @"D:\SteamLibrary\steamapps\common\Bendy and the Ink Machine",
            @"D:\steamapps\common\Bendy and the Ink Machine",
            @"E:\Steam\steamapps\common\Bendy and the Ink Machine",
            @"E:\SteamLibrary\steamapps\common\Bendy and the Ink Machine",
            @"E:\steamapps\common\Bendy and the Ink Machine",
            @"E:\Program Files (x86)\steamapps\common\Bendy and the Ink Machine",
            @"E:\Program Files\steamapps\common\Bendy and the Ink Machine",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + path);
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
                    // Look for Steam\steamapps
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Bendy and the Ink Machine");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Bendy and the Ink Machine");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for steamapps at the root of the drive
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "Bendy and the Ink Machine");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Bendy and the Ink Machine");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Bendy and the Ink Machine");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Bendy and the Ink Machine (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Bendy and the Ink Machine (Steam) not found.");
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
            remoteConfig = JsonUtility.FromJson<BATIMConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyBATIMConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }
}
