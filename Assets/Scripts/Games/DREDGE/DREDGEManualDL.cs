using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class DREDGEManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DREDGE FILES")]
    public FileDownloader.FileData dredgeApworld;
    public FileDownloader.FileData dredgeAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string dredgePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private DREDGEConfig remoteConfig;
    private bool configLoaded = false;
    private Process gameProcess;
    private bool isEpic = false;

    [System.Serializable]
    public class DREDGEConfig
    {
        public string dredgeApworld;
        public string dredgeAP;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        dredgePath = GetDREDGEPath();
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

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.isOn = false;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnDestroy()
    {
        CloseGame();
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
        dredgePath = GetDREDGEPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + dredgePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        dredgePath = GetDREDGEPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + dredgePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(dredgePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyDREDGEConfig()
    {
        if (remoteConfig == null)
            return;

        dredgeApworld.url = remoteConfig.dredgeApworld;
        dredgeAP.url = remoteConfig.dredgeAP;
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
        dredgePath = GetDREDGEPath();

        if (string.IsNullOrEmpty(dredgePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("DREDGE not found in " + platform + ". Please check installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
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
        dredgePath = GetDREDGEPath();

        if (string.IsNullOrEmpty(dredgePath))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanModsToggle != null && fullCleanModsToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            ShowInfo("Removing AP mods...");
            CleanupProcesses();
            RemoveAPMods();
            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully clean all mods?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");
        RemoveAPMods();

        if (fullClean)
        {
            ShowInfo("Cleaning all mod files...");
            RemoveAllModFiles();
            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    void RemoveAPMods()
    {
        DeleteOldVersionFiles();
    }

    void RemoveAllModFiles()
    {
        // Remove all mod-related files and directories
        SafeDeleteDirectory(Path.Combine(dredgePath, "Mods"));
        SafeDeleteDirectory(Path.Combine(dredgePath, "Logs"));
        SafeDeleteFile(Path.Combine(dredgePath, "0Harmony.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "0Harmony.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "asset_update_date.txt"));
        SafeDeleteFile(Path.Combine(dredgePath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(dredgePath, "mod_list.json"));
        SafeDeleteFile(Path.Combine(dredgePath, "mod_meta.json"));
        SafeDeleteFile(Path.Combine(dredgePath, "Mono.Cecil.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "Mono.Cecil.Mdb.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "Mono.Cecil.Pdb.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "Mono.Cecil.Rocks.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "MonoMod.RuntimeDetour.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "MonoMod.RuntimeDetour.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "MonoMod.Utils.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "MonoMod.Utils.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "Newtonsoft.Json.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "System.ComponentModel.Annotations.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "System.ComponentModel.Annotations.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "Winch.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "Winch.pdb"));
        SafeDeleteFile(Path.Combine(dredgePath, "Winch.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchCommon.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchCommon.pdb"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchCommon.xml"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchConfig.json"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchConsole.exe"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchConsole.exe.config"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchConsole.pdb"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchLauncher.deps.json"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchLauncher.dll"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchLauncher.exe"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchLauncher.pdb"));
        SafeDeleteFile(Path.Combine(dredgePath, "WinchLauncher.runtimeconfig.json"));
        SafeDeleteFile(Path.Combine(dredgePath, "winhttp.dll"));
    }

    // Remplacer la méthode HasOtherMods() par ceci (version whitelist simple)
    bool HasOtherMods()
    {
        try
        {
            var allowedFiles = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "0Harmony.dll",
            "0Harmony.xml",
            "asset_update_date.txt",
            "doorstop_config.ini",
            "DREDGE Archipelago Version Unknown.txt", // éventuellement d'autres variantes gérées par StartsWith ci-dessous
            "DREDGE.exe",
            "EOSBootstrapper.exe",
            "EOSBootstrapper.ini",
            "mod_list.json",
            "mod_meta.json",
            "Mono.Cecil.dll",
            "Mono.Cecil.Mdb.dll",
            "Mono.Cecil.Pdb.dll",
            "Mono.Cecil.Rocks.dll",
            "MonoMod.RuntimeDetour.dll",
            "MonoMod.RuntimeDetour.xml",
            "MonoMod.Utils.dll",
            "MonoMod.Utils.xml",
            "Newtonsoft.Json.dll",
            "System.ComponentModel.Annotations.dll",
            "System.ComponentModel.Annotations.xml",
            "UnityCrashHandler32.exe",
            "UnityPlayer.dll",
            "Winch.dll",
            "Winch.pdb",
            "Winch.xml",
            "WinchCommon.dll",
            "WinchCommon.pdb",
            "WinchCommon.xml",
            "WinchConfig.json",
            "WinchConsole.exe",
            "WinchConsole.exe.config",
            "WinchConsole.pdb",
            "WinchLauncher.deps.json",
            "WinchLauncher.dll",
            "WinchLauncher.exe",
            "WinchLauncher.pdb",
            "WinchLauncher.runtimeconfig.json",
            "winhttp.dll"
        };

            var allowedDirs = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ".egstore",
            "DREDGE_Data",
            "Mods",
            "MonoBleedingEdge"
        };

            // Check root files - any unknown file => other mods
            foreach (string file in Directory.GetFiles(dredgePath))
            {
                string name = Path.GetFileName(file);
                if (allowedFiles.Contains(name)) continue;
                if (name.StartsWith("DREDGE Archipelago Version", System.StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                return true;
            }

            // If Mods folder exists and has content -> it's the AP mod (not other mods)
            string modsPath = Path.Combine(dredgePath, "Mods");
            if (Directory.Exists(modsPath) && (Directory.GetFiles(modsPath).Length > 0 || Directory.GetDirectories(modsPath).Length > 0))
                return false;

            // Check root directories - any unknown directory => other mods
            foreach (string dir in Directory.GetDirectories(dredgePath))
            {
                string name = Path.GetFileName(dir);
                if (allowedDirs.Contains(name)) continue;
                if (string.Equals(name, "Mods", System.StringComparison.OrdinalIgnoreCase)) continue;
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

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        CreateVersionFile(dredgeApworld.url, dredgeAP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DREDGE...");
            LaunchGame();
            yield return WaitForGameClose();
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator WaitForGameClose()
    {
        if (gameProcess == null)
            yield break;

        while (!gameProcess.HasExited)
        {
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("DREDGE has closed");
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + dredgeApworld.url);

        if (string.IsNullOrEmpty(dredgeApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = dredgeApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = dredgeApworld.url.Substring(dredgeApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + dredgeApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(dredgeApworld.url, localPath);

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
        ShowInfo("Extracting AP Mod...");

        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "DREDGEAPTemp");

        if (!Directory.Exists(extractPath))
            Directory.CreateDirectory(extractPath);

        yield return downloader.DownloadAndExtract(dredgeAP, Application.persistentDataPath, extractPath);

        try
        {
            // Find the Preloaded-DREDGE folder
            string[] preloadedDirs = Directory.GetDirectories(extractPath, "Preloaded-DREDGE", SearchOption.AllDirectories);

            if (preloadedDirs.Length > 0)
            {
                string preloadedSourcePath = preloadedDirs[0];

                // Copy all contents from Preloaded-DREDGE directly to the game directory
                CopyDirectory(preloadedSourcePath, dredgePath);
                UnityEngine.Debug.Log("Preloaded-DREDGE contents copied to: " + dredgePath);

                ShowInfo("AP Mod installed successfully!");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Preloaded-DREDGE folder not found in archive");
                ShowInfo("ERROR: Preloaded-DREDGE folder not found in the zip!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing AP Mod: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod!\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    IEnumerator APWorldOnlyFlow()
    {
        dredgePath = GetDREDGEPath();

        if (string.IsNullOrEmpty(dredgePath))
            yield break;

        yield return InstallAPWorld();
        ShowInfo("APWorld installed successfully!");
    }

    IEnumerator APModOnlyFlow()
    {
        dredgePath = GetDREDGEPath();

        if (string.IsNullOrEmpty(dredgePath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(dredgeApworld.url, dredgeAP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DREDGE...");
            LaunchGame();
            yield return WaitForGameClose();
            ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<DREDGEConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDREDGEConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchGame()
    {
        try
        {
            string exePath = Path.Combine(dredgePath, "DREDGE.exe");

            if (File.Exists(exePath))
            {
                gameProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("DREDGE launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("DREDGE.exe not found at: " + exePath);
                ShowInfo("ERROR: DREDGE.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch DREDGE: " + e.Message);
            ShowInfo("ERROR: Failed to launch DREDGE!");
        }
    }

    void CloseGame()
    {
        try
        {
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        // Close DREDGE if it's running
        try
        {
            Process[] processes = Process.GetProcessesByName("DREDGE");
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
                Directory.Delete(path, true);
        }
        catch { }
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
                UnityEngine.Debug.Log("Copied file: " + Path.GetFileName(file));
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

    void CreateVersionFile(string apworldUrl, string apUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);
            string apVersion = ExtractVersionFromUrl(apUrl);

            string versionFileName = "DREDGE Archipelago Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== DREDGE AP ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(dredgePath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DREDGE Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(dredgePath);
            foreach (string file in files)
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

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetDREDGEPath()
    {
        if (isEpic)
            return GetDREDGEEpicPath();
        else
            return GetDREDGESteamPath();
    }

    string GetDREDGESteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "DREDGE"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "DREDGE"),
            @"D:\Steam\steamapps\common\DREDGE",
            @"D:\SteamLibrary\steamapps\common\DREDGE",
            @"D:\steamapps\common\DREDGE",
            @"E:\Steam\steamapps\common\DREDGE",
            @"E:\SteamLibrary\steamapps\common\DREDGE",
            @"E:\steamapps\common\DREDGE",
            @"E:\Program Files (x86)\steamapps\common\DREDGE",
            @"E:\Program Files\steamapps\common\DREDGE",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + path);
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
                    string dredgePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "DREDGE");
                    if (Directory.Exists(dredgePath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + dredgePath);
                        return dredgePath;
                    }

                    dredgePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "DREDGE");
                    if (Directory.Exists(dredgePath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + dredgePath);
                        return dredgePath;
                    }

                    dredgePath = Path.Combine(drive.Name, "steamapps", "common", "DREDGE");
                    if (Directory.Exists(dredgePath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + dredgePath);
                        return dredgePath;
                    }

                    dredgePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "DREDGE");
                    if (Directory.Exists(dredgePath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + dredgePath);
                        return dredgePath;
                    }

                    dredgePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "DREDGE");
                    if (Directory.Exists(dredgePath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Steam) at: " + dredgePath);
                        return dredgePath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("DREDGE (Steam) not found.");
        return "";
    }

    string GetDREDGEEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\DREDGEmKMzx",
            @"D:\Epic Games\DREDGEmKMzx",
            @"E:\Epic Games\DREDGEmKMzx",
            @"C:\Games\Epic\DREDGEmKMzx",
            @"D:\Games\Epic\DREDGEmKMzx",
            @"E:\Games\Epic\DREDGEmKMzx",
            @"C:\Epic\DREDGEmKMzx",
            @"D:\Epic\DREDGEmKMzx",
            @"E:\Epic\DREDGEmKMzx",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found DREDGE (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Search in Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Search for DREDGE manifest
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("DREDGEmKMzx") || content.Contains("DREDGE"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found DREDGE (Epic) at: " + epicPath);
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
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "DREDGEmKMzx");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "DREDGEmKMzx");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found DREDGE (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("DREDGE (Epic) not found.");
        return "";
    }
}
