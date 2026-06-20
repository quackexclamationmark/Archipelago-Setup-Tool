using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class ADOFAIManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ADOFAI FILES")]
    public FileDownloader.FileData adofaiApworld;
    public FileDownloader.FileData adofaiBepInEx;
    public FileDownloader.FileData adofaiAP;

    [Header("FEATURE TOGGLES")]
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

    private Process adofaiProcess;
    private string adofaiPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private ADOFAIConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class ADOFAIConfig
    {
        public string adofaiApworld;
        public string adofaiBepInEx;
        public string adofaiAP;
    }

    void Start()
    {
        adofaiPath = GetADOFAIPath();
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
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplyADOFAIConfig()
    {
        if (remoteConfig == null)
            return;

        adofaiApworld.url = remoteConfig.adofaiApworld;
        adofaiBepInEx.url = remoteConfig.adofaiBepInEx;
        adofaiAP.url = remoteConfig.adofaiAP;
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
        adofaiPath = GetADOFAIPath();

        if (string.IsNullOrEmpty(adofaiPath))
        {
            ShowInfo("ADOFAI not found in Steam. Please check installation.");
            return;
        }

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;

        int count =
            (bep ? 1 : 0) +
            (ap ? 1 : 0);

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        adofaiPath = GetADOFAIPath();

        if (string.IsNullOrEmpty(adofaiPath))
            return;

        string pluginsPath = Path.Combine(adofaiPath, "BepInEx", "plugins");

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

            SafeDeleteFile(Path.Combine(pluginsPath, "ADOFAI_AP.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
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

            SafeDeleteDirectory(Path.Combine(adofaiPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(adofaiPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(adofaiPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(adofaiPath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(adofaiPath, "winhttp.dll"));
            DeleteOldVersionFiles();

            ShowInfo("Full clear completed!");
            return;
        }

        ShowInfo("Revert completed!");
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

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "ADOFAI_AP")
                return true;
        }

        string[] files = Directory.GetFiles(pluginsPath);

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            // Whitelist version files
            if (fileName.StartsWith("ADOFAI AP Version") && fileName.EndsWith(".txt"))
                continue;

            if (fileName != "ADOFAI_AP.dll" && fileName != "Archipelago.MultiClient.Net.dll")
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
        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAP();
        }

        CreateVersionFile(adofaiAP.url, adofaiBepInEx.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching ADOFAI...");
            LaunchADOFAI();

            yield return WaitForConfigFiles();

            CloseADOFAI();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchADOFAI();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ADOFAIAPTemp");
        yield return downloader.DownloadAndExtract(adofaiAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(adofaiPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Copy the two DLL files from BepInEx\plugins in the zip to the game's BepInEx\plugins
        string zipPluginsPath = Path.Combine(extractPath, "BepInEx", "plugins");

        if (Directory.Exists(zipPluginsPath))
        {
            string adofaiAPDll = Path.Combine(zipPluginsPath, "ADOFAI_AP.dll");
            string archipelagoDll = Path.Combine(zipPluginsPath, "Archipelago.MultiClient.Net.dll");

            if (File.Exists(adofaiAPDll))
            {
                string targetDll = Path.Combine(pluginsPath, "ADOFAI_AP.dll");
                File.Copy(adofaiAPDll, targetDll, true);
            }

            if (File.Exists(archipelagoDll))
            {
                string targetDll = Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll");
                File.Copy(archipelagoDll, targetDll, true);
            }

            UnityEngine.Debug.Log("AP Mod installed successfully!");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(adofaiBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, adofaiPath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APOnlyFlow()
    {
        ShowInfo("Installing AP Mod...");
        yield return InstallAP();

        CreateVersionFile(adofaiAP.url, adofaiBepInEx.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching ADOFAI...");
            LaunchADOFAI();
            yield return WaitForConfigFiles();
            CloseADOFAI();

            ShowInfo("Second launch...");
            LaunchADOFAI();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(adofaiPath, "BepInEx", "config", "BepInEx.cfg");

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
        string url = "https://raw.githubusercontent.com/yourusername/ADOFAI-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

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
            remoteConfig = JsonUtility.FromJson<ADOFAIConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyADOFAIConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchADOFAI()
    {
        string exePath = Path.Combine(adofaiPath, "A Dance of Fire and Ice.exe");

        if (File.Exists(exePath))
            adofaiProcess = Process.Start(exePath);
    }

    void CloseADOFAI()
    {
        try
        {
            if (adofaiProcess != null && !adofaiProcess.HasExited)
            {
                adofaiProcess.Kill();
                adofaiProcess.Dispose();
                adofaiProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseADOFAI();
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

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apUrl, string bepinexUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string bepinexVersion = ExtractBepInExVersion(bepinexUrl);

            string versionFileName = "ADOFAI AP Version " + apVersion + ".txt";
            string content = "ADOFAI AP Setup Tool\n";
            content += "https://github.com/yourusername/ADOFAI-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(adofaiPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(adofaiPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ADOFAI AP Version .+\.txt");

            // Delete files in root
            string[] rootFiles = Directory.GetFiles(adofaiPath);
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

            // Delete files in plugins
            string pluginsPath = Path.Combine(adofaiPath, "BepInEx", "plugins");
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

    string ExtractBepInExVersion(string url)
    {
        // Looks for BepInEx: BepInEx_UnityIL2CPP_x64_VERSION.zip
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"BepInEx_UnityIL2CPP_x64_([^/]+)");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetADOFAIPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "A Dance of Fire and Ice"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "A Dance of Fire and Ice"),
            @"D:\Steam\steamapps\common\A Dance of Fire and Ice",
            @"D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice",
            @"D:\steamapps\common\A Dance of Fire and Ice",
            @"E:\Steam\steamapps\common\A Dance of Fire and Ice",
            @"E:\SteamLibrary\steamapps\common\A Dance of Fire and Ice",
            @"E:\steamapps\common\A Dance of Fire and Ice",
            @"E:\Program Files (x86)\steamapps\common\A Dance of Fire and Ice",
            @"E:\Program Files\steamapps\common\A Dance of Fire and Ice",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + path);
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
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "A Dance of Fire and Ice");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "A Dance of Fire and Ice");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for steamapps at the root of the drive
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "A Dance of Fire and Ice");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "A Dance of Fire and Ice");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "A Dance of Fire and Ice");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found ADOFAI (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("ADOFAI (Steam) not found.");
        return "";
    }
}
