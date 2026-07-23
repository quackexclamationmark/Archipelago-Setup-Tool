using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public class TOEMManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("TOEM FILES")]
    public FileDownloader.FileData toemApworld;
    public FileDownloader.FileData toemBepInEx;
    public FileDownloader.FileData toemAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installTOEMApworldToggle;
    public Toggle installTOEMBepInExToggle;
    public Toggle installTOEMAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
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

    private Process toemProcess;
    private bool toemLaunchedAsHelper = false;
    private string toemPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private ToemConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class ToemConfig
    {
        public string toemAP;
        public string toemBepInEx;
        public string toemApworld;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        toemPath = GetTOEMPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (removeAPModsOnlyToggle != null) removeAPModsOnlyToggle.isOn = true;
        if (fullCleanBepInExToggle != null) fullCleanBepInExToggle.isOn = false;
        if (fullCleanBepInExToggle != null) fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked()
    {
        SelectSteam();
    }

    void OnEpicButtonClicked()
    {
        SelectEpic();
    }

    void SelectSteam()
    {
        isEpic = false;
        toemPath = GetTOEMPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + toemPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        toemPath = GetTOEMPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + toemPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(toemPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplyToemConfig()
    {
        if (remoteConfig == null) return;
        toemAP.url = remoteConfig.toemAP;
        toemBepInEx.url = remoteConfig.toemBepInEx;
        toemApworld.url = remoteConfig.toemApworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    public void RevertAll() => ShowConfirmation("Are you sure you want to revert?", "Revert");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
            case "ForceFullClean": ExecuteRevert(); break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        // wait for remote config if necessary
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        if (string.IsNullOrEmpty(toemPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("TOEM path not found. Please check " + platform + " installation.");
            return;
        }

        bool apworld = installTOEMApworldToggle == null || installTOEMApworldToggle.isOn;
        bool bepinex = installTOEMBepInExToggle != null && installTOEMBepInExToggle.isOn;
        bool apmod = installTOEMAPToggle != null && installTOEMAPToggle.isOn;

        int count = (apworld ? 1 : 0) + (bepinex ? 1 : 0) + (apmod ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (bepinex && count == 1) { StartCoroutine(BepInExOnlyFlow()); return; }
        if (apmod && count == 1) { StartCoroutine(APModOnlyFlow()); return; }

        StartCoroutine(InstallFlow());
    }

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    private void ExecuteRevert()
    {
        toemPath = GetTOEMPath();
        if (string.IsNullOrEmpty(toemPath)) return;

        string pluginsPath = Path.Combine(toemPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();
            if (!Directory.Exists(pluginsPath)) return;

            ShowInfo("Removing TOEM AP mods...");
            SafeDeleteDirectory(Path.Combine(pluginsPath, "TOEMArchipelago"));
            DeleteOldVersionFiles();
            ShowInfo("TOEM AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation("Other mods were detected.\nDo you REALLY want to fully delete BepInEx?", "ForceFullClean");
            return;
        }

        pendingFullCleanConfirmation = false;
        CleanupProcesses();

        ShowInfo("Removing mods...");
        SafeDeleteDirectory(Path.Combine(pluginsPath, "TOEMArchipelago"));
        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");
            SafeDeleteDirectory(Path.Combine(toemPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(toemPath, "dotnet"));
            SafeDeleteFile(Path.Combine(toemPath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(toemPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(toemPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(toemPath, "winhttp.dll"));
            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning BepInEx...");
            SafeDeleteDirectory(Path.Combine(toemPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(toemPath, "dotnet"));
            SafeDeleteFile(Path.Combine(toemPath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(toemPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(toemPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(toemPath, "winhttp.dll"));
            ShowInfo("Revert completed!");
        }
    }

    IEnumerator InstallFlow()
    {
        // wait for config loaded
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string pluginsPath = Path.Combine(toemPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        bool installApworld = installTOEMApworldToggle == null || installTOEMApworldToggle.isOn;
        bool installBepinex = installTOEMBepInExToggle != null && installTOEMBepInExToggle.isOn;
        bool installApmod = installTOEMAPToggle != null && installTOEMAPToggle.isOn;

        if (installBepinex)
        {
            ShowInfo("Installing BepInEx...");
            string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");
            yield return downloader.DownloadAndExtract(toemBepInEx, Application.persistentDataPath, extractPath);
            MoveDirectory(extractPath, toemPath);
            SafeDeleteDirectory(extractPath);
        }

        if (installApworld)
        {
            ShowInfo("Installing AP World...");
            yield return InstallAPWorld();
        }

        if (installApmod)
        {
            ShowInfo("Installing TOEM AP Client...");
            string extractPath = Path.Combine(Application.persistentDataPath, "TOEMAPTemp");
            yield return downloader.DownloadAndExtract(toemAP, Application.persistentDataPath, extractPath);

            // Handle archive that creates a single top folder
            string sourceRoot = extractPath;
            try
            {
                var topDirs = Directory.GetDirectories(extractPath);
                var topFiles = Directory.GetFiles(extractPath);
                if (topFiles.Length == 0 && topDirs.Length == 1)
                    sourceRoot = topDirs[0];
            }
            catch { }

            // Move content to plugins and rename to TOEMArchipelago
            string targetDir = Path.Combine(pluginsPath, "TOEMArchipelago");
            Directory.CreateDirectory(targetDir);
            MoveDirectory(sourceRoot, targetDir);
            SafeDeleteDirectory(extractPath);
        }

        CreateVersionFile(toemAP.url, toemBepInEx.url, toemApworld.url);

        ShowInfo("Installation complete!");
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(2f);
            LaunchTOEM(false);
        }
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallAPWorld();

        CreateVersionFile(toemAP.url, toemBepInEx.url, toemApworld.url);
        ShowInfo("AP World installed successfully!");
    }

    IEnumerator BepInExOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing BepInEx...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");
        yield return downloader.DownloadAndExtract(toemBepInEx, Application.persistentDataPath, extractPath);
        MoveDirectory(extractPath, toemPath);
        SafeDeleteDirectory(extractPath);

        CreateVersionFile(toemAP.url, toemBepInEx.url, toemApworld.url);
        ShowInfo("BepInEx installed successfully!");

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(1f);
            LaunchTOEM(false); // interactive second launch
        }
    }

    IEnumerator APModOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing TOEM AP Client...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string pluginsPath = Path.Combine(toemPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "TOEMAPTemp");
        yield return downloader.DownloadAndExtract(toemAP, Application.persistentDataPath, extractPath);

        string sourceRoot = extractPath;
        try
        {
            var topDirs = Directory.GetDirectories(extractPath);
            var topFiles = Directory.GetFiles(extractPath);
            if (topFiles.Length == 0 && topDirs.Length == 1)
                sourceRoot = topDirs[0];
        }
        catch { }

        string targetDir = Path.Combine(pluginsPath, "TOEMArchipelago");
        Directory.CreateDirectory(targetDir);
        MoveDirectory(sourceRoot, targetDir);
        SafeDeleteDirectory(extractPath);

        CreateVersionFile(toemAP.url, toemBepInEx.url, toemApworld.url);
        ShowInfo("TOEM AP Client installed successfully!");
    }

    // Installs the .apworld into Archipelago/custom_worlds (not into plugins)
    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(toemApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = toemApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = toemApworld.url.Substring(toemApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + toemApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(toemApworld.url, localPath);

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
            try { File.Delete(target); } catch { }
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! " + savePath);
            }
        }
    }

    // LaunchTOEM: accepts asHelper flag to mark the process as killable by CloseTOEM()
    void LaunchTOEM(bool asHelper = false)
    {
        string exePath = Path.Combine(toemPath, "TOEM.exe");
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                toemProcess = Process.Start(psi);
                toemLaunchedAsHelper = asHelper;
                UnityEngine.Debug.Log($"TOEM launched for {(asHelper ? "config generation (helper)" : "user/interactive launch")} (minimized).");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("TOEM executable not found: " + exePath);
        }
    }

    void CloseTOEM(bool force = false)
    {
        try
        {
            if (toemProcess != null && !toemProcess.HasExited)
            {
                if (force || toemLaunchedAsHelper)
                {
                    toemProcess.Kill();
                    toemProcess.Dispose();
                    toemProcess = null;
                    toemLaunchedAsHelper = false;
                    UnityEngine.Debug.Log("TOEM process closed.");
                }
                else
                {
                    UnityEngine.Debug.Log("CloseTOEM: process started interactively, not killing it.");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("CloseTOEM error: " + ex.Message);
        }
    }

    void CleanupProcesses() => CloseTOEM();

    void SafeDeleteFile(string path) => StartCoroutine(DeleteFileForce(path));

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;
        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                if (!File.Exists(path)) yield break;
            }
            catch { }
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(file, dest);
        }
    }

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName) return file;
        }
        catch { }
        return "";
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath)) return false;
        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("TOEM APMod Version") && name.EndsWith(".txt")) continue;
            if (name != "Archipelago.MultiClient.Net.dll") return true;
        }

        // Ignore TOEMArchipelago and Assets folders; any other directory counts as another mod
        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);
            if (!dirName.Equals("TOEMArchipelago", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(toemPath))
            {
                string fn = Path.GetFileName(file);
                if (fn.StartsWith("TOEM APMod Version") && fn.EndsWith(".txt")) SafeDeleteFile(file);
            }
            string pluginsPath = Path.Combine(toemPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                foreach (string file in Directory.GetFiles(pluginsPath))
                {
                    string fn = Path.GetFileName(file);
                    if (fn.StartsWith("TOEM APMod Version") && fn.EndsWith(".txt")) SafeDeleteFile(file);
                }
            }
        }
        catch { }
    }

    void CreateVersionFile(string toemapUrl, string bepinexUrl, string apworldUrl)
    {
        try
        {
            string toemapVersion = ExtractVersionFromUrl(toemapUrl, @"/([^/]+)\.zip");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/([^/]+)\.zip");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "TOEM APMod Version " + toemapVersion + ".txt";
            string content = "TOEM Archipelago Setup Tool by quack!\n\n";
            content += "=== TOEM AP CLIENT ===\nDownloaded from: " + toemapUrl + "\nVersion: " + toemapVersion + "\n\n";
            content += "=== APWORLD ===\nDownloaded from: " + apworldUrl + "\nName: " + apworldVersion + ".apworld\n\n";
            content += "=== BEPINEX ===\nDownloaded from: " + bepinexUrl + "\nVersion: " + bepinexVersion + "\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(toemPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(toemPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url)) return "Unknown";
        Match match = Regex.Match(url, pattern);
        return match.Success ? match.Groups[1].Value : "Unknown";
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
            remoteConfig = JsonUtility.FromJson<ToemConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyToemConfig();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    string GetTOEMPath()
    {
        if (isEpic)
            return GetTOEMEpicPath();
        else
            return GetTOEMSteamPath();
    }

    string GetTOEMSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "TOEM"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "TOEM"),
            @"D:\Steam\steamapps\common\TOEM",
            @"D:\SteamLibrary\steamapps\common\TOEM",
            @"C:\Program Files (x86)\steamapps\common\TOEM",
            @"C:\Program Files\steamapps\common\TOEM",
        };

        foreach (string p in quickPaths)
        {
            try { if (Directory.Exists(p)) return p; } catch { }
        }

        try
        {
            var drives = System.IO.DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed) continue;
                try
                {
                    string tryPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "TOEM");
                    if (Directory.Exists(tryPath)) return tryPath;
                    tryPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "TOEM");
                    if (Directory.Exists(tryPath)) return tryPath;
                    tryPath = Path.Combine(drive.Name, "steamapps", "common", "TOEM");
                    if (Directory.Exists(tryPath)) return tryPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("TOEM (Steam) not found.");
        return "";
    }

    string GetTOEMEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\TOEM",
            @"D:\Epic Games\TOEM",
            @"E:\Epic Games\TOEM",
            @"C:\Games\Epic\TOEM",
            @"D:\Games\Epic\TOEM",
            @"E:\Games\Epic\TOEM",
            @"C:\Epic\TOEM",
            @"D:\Epic\TOEM",
            @"E:\Epic\TOEM",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found TOEM (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("TOEM"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found TOEM (Epic) at: " + epicPath);
                                    return epicPath;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "TOEM");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found TOEM (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "TOEM");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found TOEM (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("TOEM (Epic) not found.");
        return "";
    }
}
