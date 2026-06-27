using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class SlimeRancher2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SLIME RANCHER 2 FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData slimerancher2AP;
    public FileDownloader.FileData slimerancher2Interop;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installBepInExToggle;
    public Toggle installSlimeRancher2APToggle;

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

    private Process slimeRancher2Process;
    private string slimeRancher2Path;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private SlimeRancher2Config remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class SlimeRancher2Config
    {
        public string slimerancher2Apworld;
        public string slimerancher2BepInEx;
        public string slimerancher2AP;
        public string slimerancher2Interop;
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

        slimeRancher2Path = GetSlimeRancher2Path();
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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
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
        slimeRancher2Path = GetSlimeRancher2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + slimeRancher2Path);
    }

    void SelectEpic()
    {
        isEpic = true;
        slimeRancher2Path = GetSlimeRancher2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + slimeRancher2Path);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(slimeRancher2Path) ? "Not Found" : "Found";
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

    void ApplySlimeRancher2Config()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.slimerancher2Apworld;
        bepInEx.url = remoteConfig.slimerancher2BepInEx;
        slimerancher2AP.url = remoteConfig.slimerancher2AP;
        slimerancher2Interop.url = remoteConfig.slimerancher2Interop;
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
        slimeRancher2Path = GetSlimeRancher2Path();

        if (string.IsNullOrEmpty(slimeRancher2Path))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Slime Rancher 2 not found in " + platform + ". Please check installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        slimeRancher2Path = GetSlimeRancher2Path();

        if (string.IsNullOrEmpty(slimeRancher2Path))
            return;

        string pluginsPath = Path.Combine(slimeRancher2Path, "BepInEx", "plugins");

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

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            // Supprimer le dossier SlimeRancher2-AP
            SafeDeleteDirectory(Path.Combine(pluginsPath, "SlimeRancher2-AP"));

            // Supprimer les fichiers DLL du mod
            SafeDeleteFile(Path.Combine(pluginsPath, "SlimeRancher2-AP.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "SlimeRancher2-AP.deps.json"));
            SafeDeleteFile(Path.Combine(pluginsPath, "SlimeRancher2-AP.pdb"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Newtonsoft.Json.dll"));

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

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SlimeRancher2-AP"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(slimeRancher2Path, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(slimeRancher2Path, "dotnet"));
            SafeDeleteFile(Path.Combine(slimeRancher2Path, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(slimeRancher2Path, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(slimeRancher2Path, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(slimeRancher2Path, "changelog.txt"));

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

                // Whitelist les fichiers du mod SlimeRancher2-AP
                if (name.StartsWith("Slime Rancher 2 Archipelago Version") && name.EndsWith(".txt"))
                    continue;
                if (name == "SlimeRancher2-AP.dll")
                    continue;
                if (name == "SlimeRancher2-AP.deps.json")
                    continue;
                if (name == "SlimeRancher2-AP.pdb")
                    continue;
                if (name == "Archipelago.MultiClient.Net.dll")
                    continue;
                if (name == "Newtonsoft.Json.dll")
                    continue;

                return true;
            }

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName != "SlimeRancher2-AP")
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

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return StartCoroutine(InstallBepInEx());

            // Installer Interop directement après BepInEx
            ShowInfo("Installing Interop...");
            yield return StartCoroutine(InstallInterop());
        }

        if (installSlimeRancher2APToggle == null || installSlimeRancher2APToggle.isOn)
        {
            ShowInfo("Installing Slime Rancher 2 Archipelago...");
            yield return StartCoroutine(InstallSlimeRancher2AP());
        }

        CreateVersionFile(apworld.url, bepInEx.url, slimerancher2AP.url, slimerancher2Interop.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching Slime Rancher 2...");
            yield return StartCoroutine(LaunchSlimeRancher2());
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

        // Move all BepInEx content to the Slime Rancher 2 directory root
        MoveDirectory(extractPath, slimeRancher2Path);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallInterop()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "InteropTemp");
        yield return StartCoroutine(downloader.DownloadAndExtract(slimerancher2Interop, Application.persistentDataPath, extractPath));

        string bepInExPath = Path.Combine(slimeRancher2Path, "BepInEx");
        Directory.CreateDirectory(bepInExPath);

        // Move the interop folder from extracted contents to BepInEx
        string sourceFolder = Path.Combine(extractPath, "interop");
        string targetFolder = Path.Combine(bepInExPath, "interop");

        if (Directory.Exists(sourceFolder))
        {
            // Remove existing folder if it exists
            if (Directory.Exists(targetFolder))
            {
                SafeDeleteDirectory(targetFolder);
            }

            // Copy the entire folder
            CopyDirectory(sourceFolder, targetFolder);
            UnityEngine.Debug.Log("Copied interop folder to BepInEx");
        }
        else
        {
            UnityEngine.Debug.LogWarning("interop source folder not found: " + sourceFolder);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallSlimeRancher2AP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SlimeRancher2APTemp");
        yield return StartCoroutine(downloader.DownloadAndExtract(slimerancher2AP, Application.persistentDataPath, extractPath));

        string pluginsPath = Path.Combine(slimeRancher2Path, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Copie TOUT le contenu du dossier extrait directement dans plugins
        try
        {
            // Copie tous les fichiers du root
            foreach (string file in Directory.GetFiles(extractPath))
            {
                string destFile = Path.Combine(pluginsPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                UnityEngine.Debug.Log("Copied file: " + Path.GetFileName(file));
            }

            // Copie tous les dossiers du root
            foreach (string dir in Directory.GetDirectories(extractPath))
            {
                string destDir = Path.Combine(pluginsPath, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
                UnityEngine.Debug.Log("Copied directory: " + Path.GetFileName(dir));
            }

            UnityEngine.Debug.Log("SlimeRancher2-AP content copied to plugins");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying SlimeRancher2-AP: " + e.Message);
        }

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
            remoteConfig = JsonUtility.FromJson<SlimeRancher2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySlimeRancher2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    IEnumerator LaunchSlimeRancher2()
    {
        if (string.IsNullOrEmpty(slimeRancher2Path))
        {
            ShowInfo("ERROR: Slime Rancher 2 path not found!");
            yield break;
        }

        string exePath = Path.Combine(slimeRancher2Path, "SlimeRancher2.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("ERROR: SlimeRancher2.exe not found at " + exePath);
            UnityEngine.Debug.LogError("SlimeRancher2.exe not found at: " + exePath);
            yield break;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = exePath,
            WorkingDirectory = slimeRancher2Path,
            UseShellExecute = true
        };

        try
        {
            slimeRancher2Process = Process.Start(startInfo);
            UnityEngine.Debug.Log("Launched Slime Rancher 2 from: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Slime Rancher 2: " + e.Message);
            ShowInfo("ERROR: Failed to launch Slime Rancher 2\n" + e.Message);
            yield break;
        }

        // Wait for the first process to fully initialize
        yield return new WaitForSeconds(3f);

        // Get all SlimeRancher2 processes
        Process[] processes = Process.GetProcessesByName("SlimeRancher2");

        if (processes.Length > 1)
        {
            UnityEngine.Debug.Log("Found " + processes.Length + " Slime Rancher 2 processes. Closing the duplicate...");

            // Keep the first one, close any duplicates
            for (int i = 1; i < processes.Length; i++)
            {
                try
                {
                    processes[i].Kill();
                    UnityEngine.Debug.Log("Closed duplicate Slime Rancher 2 process");
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
        // Close Slime Rancher 2 if it's running
        try
        {
            Process[] processes = Process.GetProcessesByName("SlimeRancher2");
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        try
        {
            CopyDirectory(source, target);
            SafeDeleteDirectory(source);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error moving directory: " + e.Message);
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

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetSlimeRancher2Path()
    {
        if (isEpic)
            return GetSlimeRancher2EpicPath();
        else
            return GetSlimeRancher2SteamPath();
    }

    string GetSlimeRancher2SteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Slime Rancher 2"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Slime Rancher 2"),
            @"D:\Steam\steamapps\common\Slime Rancher 2",
            @"D:\SteamLibrary\steamapps\common\Slime Rancher 2",
            @"D:\steamapps\common\Slime Rancher 2",
            @"E:\Steam\steamapps\common\Slime Rancher 2",
            @"E:\SteamLibrary\steamapps\common\Slime Rancher 2",
            @"E:\steamapps\common\Slime Rancher 2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + path);
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
                    string sr2Path = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Slime Rancher 2");
                    if (Directory.Exists(sr2Path))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + sr2Path);
                        return sr2Path;
                    }

                    sr2Path = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Slime Rancher 2");
                    if (Directory.Exists(sr2Path))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + sr2Path);
                        return sr2Path;
                    }

                    sr2Path = Path.Combine(drive.Name, "steamapps", "common", "Slime Rancher 2");
                    if (Directory.Exists(sr2Path))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + sr2Path);
                        return sr2Path;
                    }

                    sr2Path = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Slime Rancher 2");
                    if (Directory.Exists(sr2Path))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + sr2Path);
                        return sr2Path;
                    }

                    sr2Path = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Slime Rancher 2");
                    if (Directory.Exists(sr2Path))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Steam) at: " + sr2Path);
                        return sr2Path;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Slime Rancher 2 (Steam) not found.");
        return "";
    }

    string GetSlimeRancher2EpicPath()
    {
        string[] quickPaths = new string[]
        {
        @"C:\Program Files\Epic Games\SlimeRancher2",
        @"D:\Epic Games\SlimeRancher2",
        @"E:\Epic Games\SlimeRancher2",
        @"C:\Games\Epic\SlimeRancher2",
        @"D:\Games\Epic\SlimeRancher2",
        @"E:\Games\Epic\SlimeRancher2",
        @"C:\Epic\SlimeRancher2",
        @"D:\Epic\SlimeRancher2",
        @"E:\Epic\SlimeRancher2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Slime Rancher 2 (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Cherche dans Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Cherche le manifest pour Slime Rancher 2
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Slime Rancher 2") || content.Contains("SlimeRancher2"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Slime Rancher 2 (Epic) at: " + epicPath);
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

        // Scan all drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Slime Rancher 2");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Slime Rancher 2");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Slime Rancher 2 (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Slime Rancher 2 (Epic) not found.");
        return "";
    }

    void CreateVersionFile(string apworldUrl, string bepinexUrl, string slimerancher2APUrl, string interopUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string slimerancher2APVersion = ExtractVersionFromUrl(slimerancher2APUrl, @"/releases/download/([^/]+)/");
            string interopVersion = ExtractVersionFromUrl(interopUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "Slime Rancher 2 Archipelago Version " + slimerancher2APVersion + ".txt";
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
            content += "=== INTEROP ===\n";
            content += "Downloaded from: " + interopUrl + "\n";
            content += "Version: " + interopVersion + "\n";
            content += "\n";
            content += "=== SLIME RANCHER 2 ARCHIPELAGO ===\n";
            content += "Downloaded from: " + slimerancher2APUrl + "\n";
            content += "Version: " + slimerancher2APVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(slimeRancher2Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(slimeRancher2Path, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Slime Rancher 2 Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(slimeRancher2Path);
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

            string pluginsPath = Path.Combine(slimeRancher2Path, "BepInEx", "plugins");
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
