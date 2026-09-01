using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class DCManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DEAD CELLS FILES")]
    public FileDownloader.FileData deadcellsApworld;
    public FileDownloader.FileData deadcellsCoreModding;
    public FileDownloader.FileData deadcellsAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Dead Cells";
    public string[] epicGameFolderNames = new string[]
    {
    "DeadCells",
    "DeadCellseb1d7",
    "DeadCellsedafe"
    };

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installCoreModdingToggle;
    public Toggle installAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsOnlyToggle;
    public Toggle fullClearCoreToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string dcPath;
    private string pendingAction;
    private DCConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class DCConfig
    {
        public string deadcellsAP;
        public string deadcellsCoreModding;
        public string deadcellsApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        dcPath = GetDCPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (clearAPModsOnlyToggle != null)
            clearAPModsOnlyToggle.isOn = true;

        if (fullClearCoreToggle != null)
            fullClearCoreToggle.isOn = false;

        if (fullClearCoreToggle != null)
            fullClearCoreToggle.onValueChanged.AddListener(OnFullClearChanged);
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
        dcPath = GetDCPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + dcPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        dcPath = GetDCPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + dcPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(dcPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
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

    void ApplyDCConfig()
    {
        if (remoteConfig == null)
            return;

        deadcellsAP.url = remoteConfig.deadcellsAP;
        deadcellsCoreModding.url = remoteConfig.deadcellsCoreModding;
        deadcellsApworld.url = remoteConfig.deadcellsApworld;
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        dcPath = GetDCPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool coreModding = installCoreModdingToggle != null && installCoreModdingToggle.isOn;
        bool apMod = installAPToggle != null && installAPToggle.isOn;

        bool needsGamePath = coreModding || apMod;

        if (needsGamePath && string.IsNullOrEmpty(dcPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (coreModding ? 1 : 0) +
            (apMod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (coreModding && count == 1)
        {
            StartCoroutine(CoreModdingOnlyFlow());
            return;
        }

        if (apMod && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchDC();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        dcPath = GetDCPath();

        if (string.IsNullOrEmpty(dcPath))
            return;

        string coremodPath = Path.Combine(dcPath, "coremod");
        string apModsPath = Path.Combine(coremodPath, "mods", "DeadCellsArchipelago");

        bool clearAPOnly = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClear = fullClearCoreToggle != null && fullClearCoreToggle.isOn;

        if (!clearAPOnly && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAPOnly)
        {
            if (!Directory.Exists(apModsPath))
            {
                ShowInfo("No AP mods folder found.");
                return;
            }

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(apModsPath);
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // Full clear core
        ShowInfo("Removing coremod...");

        SafeDeleteDirectory(coremodPath);
        DeleteOldVersionFiles();

        ShowInfo("Full clear completed!");
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installCoreModdingToggle != null && installCoreModdingToggle.isOn)
        {
            ShowInfo("Installing Core Modding...");
            yield return InstallZipToCoremod(deadcellsCoreModding, "CoreModding");
        }

        if (installAPToggle == null || installAPToggle.isOn)
        {
            ShowInfo("Installing AP...");
            yield return InstallZipToCoremod(deadcellsAP, "AP");
        }

        CreateVersionFile(deadcellsAP.url, deadcellsCoreModding.url, deadcellsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Dead Cells...");
            yield return new WaitForSeconds(1f);
            LaunchDC();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + deadcellsApworld.url);

        if (string.IsNullOrEmpty(deadcellsApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = deadcellsApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = deadcellsApworld.url.Substring(deadcellsApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName))
            fileName = "dead_cells.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + deadcellsApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(deadcellsApworld.url, localPath);

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
            yield break;
        }

        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary APWorld file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
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

    IEnumerator InstallZipToCoremod(FileDownloader.FileData data, string label)
    {
        UnityEngine.Debug.Log("START InstallZipToCoremod: " + label);

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(data.url))
        {
            ShowInfo("ERROR: " + label + " URL is empty!");
            UnityEngine.Debug.LogError(label + " URL not set!");
            yield break;
        }

        ShowInfo("Downloading " + label + "...");

        string extractPath = Path.Combine(Application.persistentDataPath, label + "Temp");

        UnityEngine.Debug.Log("Extract path: " + extractPath);

        yield return downloader.DownloadAndExtract(data, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete for " + label);

        string coremodPath = Path.Combine(dcPath, "coremod");
        if (!Directory.Exists(coremodPath))
            Directory.CreateDirectory(coremodPath);

        MoveDirectory(extractPath, coremodPath);

        SafeDeleteDirectory(extractPath);

        if (label == "AP")
        {
            string apDataPath = Path.Combine(coremodPath, "mods", "DeadCellsArchipelago", "data");
            if (!Directory.Exists(apDataPath))
            {
                Directory.CreateDirectory(apDataPath);
                UnityEngine.Debug.Log("Created missing AP data folder: " + apDataPath);
            }
        }

        ShowInfo(label + " installed successfully!");

        UnityEngine.Debug.Log("END InstallZipToCoremod: " + label);
    }

    IEnumerator CoreModdingOnlyFlow()
    {
        dcPath = GetDCPath();

        if (string.IsNullOrEmpty(dcPath))
            yield break;

        ShowInfo("Installing Core Modding...");
        yield return InstallZipToCoremod(deadcellsCoreModding, "CoreModding");

        CreateVersionFile(deadcellsAP.url, deadcellsCoreModding.url, deadcellsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Dead Cells...");
            yield return new WaitForSeconds(1f);
            LaunchDC();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator APOnlyFlow()
    {
        dcPath = GetDCPath();

        if (string.IsNullOrEmpty(dcPath))
            yield break;

        ShowInfo("Installing AP...");
        yield return InstallZipToCoremod(deadcellsAP, "AP");

        CreateVersionFile(deadcellsAP.url, deadcellsCoreModding.url, deadcellsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Dead Cells...");
            yield return new WaitForSeconds(1f);
            LaunchDC();
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
            remoteConfig = JsonUtility.FromJson<DCConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDCConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        dcPath = GetDCPath();
        UpdatePlatformStatus();
    }

    void LaunchDC()
    {
        try
        {
            string exePath = Path.Combine(dcPath, "coremod", "core", "host", "startup", "DeadCellsModding.exe");

            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                Process.Start(startInfo);
                UnityEngine.Debug.Log("Dead Cells launched (admin): " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("DeadCellsModding.exe not found at: " + exePath);
                ShowInfo("ERROR: DeadCellsModding.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Dead Cells: " + e.Message);
            ShowInfo("ERROR: Failed to launch Dead Cells!");
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
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

    void CreateVersionFile(string apUrl, string coreModdingUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string coreModdingVersion = ExtractVersionFromUrl(coreModdingUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "DC APMod Version " + apVersion + ".txt";
            string content = "Dead Cells Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== CORE MODDING ===\n";
            content += "Downloaded from: " + coreModdingUrl + "\n";
            content += "Version: " + coreModdingVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(dcPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string coremodPath = Path.Combine(dcPath, "coremod");
            if (Directory.Exists(coremodPath))
            {
                string coremodVersionPath = Path.Combine(coremodPath, versionFileName);
                File.WriteAllText(coremodVersionPath, content);
                UnityEngine.Debug.Log("Version file created in coremod: " + coremodVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DC APMod Version .+\.txt");

            if (Directory.Exists(dcPath))
            {
                string[] rootFiles = Directory.GetFiles(dcPath);
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

            string coremodPath = Path.Combine(dcPath, "coremod");
            if (Directory.Exists(coremodPath))
            {
                string[] coremodFiles = Directory.GetFiles(coremodPath);
                foreach (string file in coremodFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in coremod: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in coremod: " + e.Message);
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
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string GetDCPath()
    {
        if (isEpic)
            return GetDCEpicPath();
        else
            return GetDCSteamPath();
    }

    string GetDCSteamPath()
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

    string GetDCEpicPath()
    {
        string[] quickBases = new string[]
        {
            @"C:\Program Files\Epic Games",
            @"C:\Games\Epic",
        };

        foreach (string basePath in quickBases)
        {
            foreach (string folderName in epicGameFolderNames)
            {
                try
                {
                    string path = Path.Combine(basePath, folderName);
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                        return path;
                    }
                }
                catch { }
            }
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

                        bool matchesDC = false;
                        foreach (string folderName in epicGameFolderNames)
                        {
                            if (content.Contains(folderName))
                            {
                                matchesDC = true;
                                break;
                            }
                        }

                        if (matchesDC)
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value.Replace(@"\\", @"\");
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Game (Epic) at: " + epicPath);
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

        if (remoteConfig != null && remoteConfig.epicSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.epicSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        foreach (string folderName in epicGameFolderNames)
                        {
                            try
                            {
                                string epicPath = Path.Combine(drive.Name, relativePath, folderName);
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Game (Epic, via remote config) at: " + epicPath);
                                    return epicPath;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
    }
}