using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

public class COTLManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("COTL FILES")]
    public FileDownloader.FileData cotlApworld;
    public FileDownloader.FileData cotlBepInEx;
    public FileDownloader.FileData cotlAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Cult of the Lamb";

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

    private const string BepInExSubFolder = "BepInExPack_CultOfTheLamb";
    private const string APModFolderName = "CotL Archipelago";
    private const string GameExeName = "Cult Of The Lamb.exe";
    private const string VersionFilePrefix = "COTL AP Version ";

    private Process cotlProcess;
    private string cotlPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private COTLConfig remoteConfig;
    private bool lastApWorldInstallSuccess = false;
    private bool configLoaded = false;

    [System.Serializable]
    public class COTLConfig
    {
        public string cotlApworld;
        public string cotlBepInEx;
        public string cotlAP;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        cotlPath = GetCOTLPath();
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
        {
            fullClearBepInExToggle.isOn = false;
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);
        }

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;
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

    void ApplyCOTLConfig()
    {
        if (remoteConfig == null)
            return;

        cotlApworld.url = remoteConfig.cotlApworld;
        cotlBepInEx.url = remoteConfig.cotlBepInEx;
        cotlAP.url = remoteConfig.cotlAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
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
        cotlPath = GetCOTLPath();

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        bool needsGamePath = bep || ap;

        if (needsGamePath && (string.IsNullOrEmpty(cotlPath) || !Directory.Exists(cotlPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (bep ? 1 : 0) +
            (ap ? 1 : 0) +
            (apworld ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one install option.");
            return;
        }

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

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            LaunchCOTL();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        cotlPath = GetCOTLPath();

        if (string.IsNullOrEmpty(cotlPath))
            return;

        string pluginsPath = Path.Combine(cotlPath, "BepInEx", "plugins");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, APModFolderName));
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

            // Version files first (the plugins copy lives inside the BepInEx folder)
            DeleteOldVersionFiles();

            SafeDeleteDirectory(Path.Combine(cotlPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(cotlPath, "doorstop_libs"));
            SafeDeleteFile(Path.Combine(cotlPath, "run_bepinex.sh"));
            SafeDeleteFile(Path.Combine(cotlPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(cotlPath, "winhttp.dll"));

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

        foreach (string dir in Directory.GetDirectories(pluginsPath))
        {
            if (Path.GetFileName(dir) != APModFolderName)
                return true;
        }

        foreach (string file in Directory.GetFiles(pluginsPath))
        {
            string fileName = Path.GetFileName(file);

            // Whitelist version files
            if (fileName.StartsWith(VersionFilePrefix) && fileName.EndsWith(".txt"))
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

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(cotlAP.url, cotlBepInEx.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Cult of the Lamb...");
            LaunchCOTL();

            yield return WaitForConfigFiles();

            CloseCOTL();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchCOTL();
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

        string extractPath = Path.Combine(Application.persistentDataPath, "COTLAPTemp");
        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(cotlAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(cotlPath, "BepInEx", "plugins");
        string apModPath = Path.Combine(pluginsPath, APModFolderName);

        Directory.CreateDirectory(pluginsPath);

        // Clean previous install, then put the whole zip content in plugins\CotL Archipelago
        SafeDeleteDirectory(apModPath);
        MoveDirectory(extractPath, apModPath);

        UnityEngine.Debug.Log("AP Mod installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");
        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(cotlBepInEx, Application.persistentDataPath, extractPath);

        // Only the content of the "BepInExPack_CultOfTheLamb" sub-folder goes into the game folder
        string packPath = Path.Combine(extractPath, BepInExSubFolder);

        if (!Directory.Exists(packPath))
        {
            string[] found = Directory.GetDirectories(extractPath, BepInExSubFolder, SearchOption.AllDirectories);
            if (found.Length > 0)
                packPath = found[0];
            else
                packPath = null;
        }

        if (packPath == null)
        {
            UnityEngine.Debug.LogError("Sub-folder '" + BepInExSubFolder + "' not found in BepInEx zip.");
            ShowInfo("ERROR: '" + BepInExSubFolder + "' folder not found in the BepInEx zip!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        MoveDirectory(packPath, cotlPath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + cotlApworld.url);

        if (string.IsNullOrEmpty(cotlApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = cotlApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = cotlApworld.url.Substring(cotlApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        // Expected name: cult_of_the_lamb.apworld
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".apworld"))
            fileName = "cult_of_the_lamb.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + cotlApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(cotlApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            UnityEngine.Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);
        UnityEngine.Debug.Log("Using target path: " + target);

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
            lastApWorldInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    void DeleteTempFile(string localPath)
    {
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

    IEnumerator APOnlyFlow()
    {
        ShowInfo("Installing AP Mod...");
        yield return InstallAP();

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(cotlAP.url, cotlBepInEx.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Cult of the Lamb...");
            LaunchCOTL();
            yield return WaitForConfigFiles();
            CloseCOTL();

            yield return new WaitForSeconds(1f);

            ShowInfo("Second launch...");
            LaunchCOTL();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(cotlPath, "BepInEx", "config", "BepInEx.cfg");

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
            remoteConfig = JsonUtility.FromJson<COTLConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyCOTLConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        cotlPath = GetCOTLPath();
    }

    void LaunchCOTL()
    {
        string exePath = Path.Combine(cotlPath, GameExeName);

        if (File.Exists(exePath))
            cotlProcess = Process.Start(exePath);
    }

    void CloseCOTL()
    {
        try
        {
            if (cotlProcess != null && !cotlProcess.HasExited)
            {
                cotlProcess.Kill();
                cotlProcess.Dispose();
                cotlProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseCOTL();
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

            string versionFileName = VersionFilePrefix + apVersion + ".txt";
            string content = "COTL AP Setup Tool\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
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

            string rootVersionPath = Path.Combine(cotlPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(cotlPath, "BepInEx", "plugins");
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
            Regex pattern = new Regex(@"^COTL AP Version .+\.txt$");

            DeleteMatchingFiles(cotlPath, pattern, "root");
            DeleteMatchingFiles(Path.Combine(cotlPath, "BepInEx", "plugins"), pattern, "plugins");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    void DeleteMatchingFiles(string folder, Regex pattern, string label)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return;

        foreach (string file in Directory.GetFiles(folder))
        {
            string fileName = Path.GetFileName(file);

            if (!pattern.IsMatch(fileName))
                continue;

            try
            {
                File.Delete(file);
                UnityEngine.Debug.Log("Deleted old version file in " + label + ": " + fileName);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete old version file in " + label + ": " + e.Message);
            }
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        Match match = new Regex(pattern).Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string ExtractBepInExVersion(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        Match match = new Regex(BepInExSubFolder + @"[/_-]v?([0-9][^/]*?)(\.zip)?(/|$)").Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        match = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?)").Match(url);
        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetCOTLPath()
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

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.apSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, "custom_worlds");
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Archipelago custom_worlds (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Archipelago custom_worlds directory not found.");
        return "";
    }
}