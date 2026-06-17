using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class OriBFManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ORIBF FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("CONFIG TOGGLES")]
    public Toggle patchBepInExConfigToggle;

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

    private string oriBFPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private OriBFConfig remoteConfig;
    private bool configLoaded = false;

    private const string BEPINEX_CONFIG_URL = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/ConfigFilesPresets/OriBF-BepInEx.cfg";

    [System.Serializable]
    public class OriBFConfig
    {
        public string oriblindAP;
        public string oriblindApworld;
        public string oriblindBepInEx;
    }

    void Start()
    {
        oriBFPath = GetOriBFPath();
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

    void ApplyOriBFConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.oriblindAP;
        apworld.url = remoteConfig.oriblindApworld;
        bepInEx.url = remoteConfig.oriblindBepInEx;
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
        if (string.IsNullOrEmpty(oriBFPath))
        {
            ShowInfo("Ori and the Blind Forest path not found. Please check Steam installation.");
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
    oriBFPath = GetOriBFPath();

    if (string.IsNullOrEmpty(oriBFPath))
        return;

    string bepinexPath = Path.Combine(oriBFPath, "BepInEx");

    bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
    bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

    if (!removeAP && !fullClean)
    {
        ShowInfo("Please select at least one revert option.");
        return;
    }

    if (removeAP)
    {
        string pluginsPath = Path.Combine(bepinexPath, "plugins");

        if (!Directory.Exists(pluginsPath))
        {
            ShowInfo("AP mods not found!");
            return;
        }

        ShowInfo("Removing AP mods...");

        SafeDeleteDirectory(Path.Combine(pluginsPath, "OriBFArchipelago"));
        DeleteOldVersionFiles();

        ShowInfo("AP mods removed successfully!");
        return;
    }

    bool hasOtherMods = HasOtherMods(Path.Combine(bepinexPath, "plugins"));

    if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
    {
        pendingFullCleanConfirmation = true;

        ShowConfirmation(
            "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
            "ForceFullClean"
        );
        return;
    }

    pendingFullCleanConfirmation = false;

    ShowInfo("Removing mods...");

    string pluginsPath2 = Path.Combine(bepinexPath, "plugins");
    SafeDeleteDirectory(Path.Combine(pluginsPath2, "OriBFArchipelago"));
    DeleteOldVersionFiles();

    hasOtherMods = HasOtherMods(pluginsPath2);

    if (fullClean)
    {
        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(bepinexPath);
        SafeDeleteFile(Path.Combine(oriBFPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(oriBFPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(oriBFPath, ".doorstop_version"));
        SafeDeleteFile(Path.Combine(oriBFPath, "changelog.txt"));
        SafeDeleteDirectory(Path.Combine(oriBFPath, "ArchipelagoData"));
        SafeDeleteDirectory(Path.Combine(oriBFPath, "Logs"));

        ShowInfo("Full clean completed!");
        return;
    }

    if (!hasOtherMods)
    {
        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(bepinexPath);
        SafeDeleteFile(Path.Combine(oriBFPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(oriBFPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(oriBFPath, ".doorstop_version"));
        SafeDeleteFile(Path.Combine(oriBFPath, "changelog.txt"));
        SafeDeleteDirectory(Path.Combine(oriBFPath, "ArchipelagoData"));
        SafeDeleteDirectory(Path.Combine(oriBFPath, "Logs"));

        ShowInfo("Revert completed!");
    }
}

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            // Whitelist version files
            if (name.StartsWith("OriBF Archipelago Version") && name.EndsWith(".txt"))
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);

            // Whitelist our plugin directory
            if (dirName == "OriBFArchipelago")
                continue;

            return true;
        }

        return false;
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

        if (patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn)
        {
            ShowInfo("Creating BepInEx config...");
            yield return CreateBepInExConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching game...");
            LaunchGame();
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

        string fileName = "oribf.apworld";
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
            request.timeout = 300; // 5 minutes

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
        {
            UnityEngine.Debug.Log("[InstallAPMod] Waiting for config to load...");
            yield return null;
        }

        UnityEngine.Debug.Log("[InstallAPMod] Config loaded!");
        UnityEngine.Debug.Log("[InstallAPMod] apMod URL: " + apMod.url);

        string pluginsPath = Path.Combine(oriBFPath, "BepInEx", "plugins");
        string extractPath = Path.Combine(Application.persistentDataPath, "OriBFModTemp");

        UnityEngine.Debug.Log("[InstallAPMod] Plugins path: " + pluginsPath);
        UnityEngine.Debug.Log("[InstallAPMod] Extract path: " + extractPath);

        if (string.IsNullOrEmpty(apMod.url))
        {
            UnityEngine.Debug.LogError("[InstallAPMod] ERROR: apMod.url is empty!");
            ShowInfo("ERROR: AP Mod URL is not set!");
            yield break;
        }

        Directory.CreateDirectory(pluginsPath);
        UnityEngine.Debug.Log("[InstallAPMod] Plugins directory created/verified");

        UnityEngine.Debug.Log("[InstallAPMod] Starting AP Mod download and extract...");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        // Copy OriBFArchipelago folder to plugins
        string sourcePath = Path.Combine(extractPath, "OriBFArchipelago");
        string destPath = Path.Combine(pluginsPath, "OriBFArchipelago");

        if (Directory.Exists(sourcePath))
        {
            UnityEngine.Debug.Log("[InstallAPMod] Copying OriBFArchipelago folder...");
            CopyDirectory(sourcePath, destPath);
            UnityEngine.Debug.Log("[InstallAPMod] OriBFArchipelago folder copied successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError("[InstallAPMod] ERROR: OriBFArchipelago folder not found in extracted files!");
            ShowInfo("ERROR: OriBFArchipelago folder not found in download!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        ShowInfo("AP Mod installed successfully!");
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, oriBFPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APWorldOnlyFlow()
    {
        oriBFPath = GetOriBFPath();

        if (string.IsNullOrEmpty(oriBFPath))
            yield break;

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchGame();
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn)
        {
            ShowInfo("Creating BepInEx config...");
            yield return CreateBepInExConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching game...");
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        oriBFPath = GetOriBFPath();

        if (string.IsNullOrEmpty(oriBFPath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchGame();

        ShowInfo("Installation complete!");
    }

    IEnumerator CreateBepInExConfig()
    {
        string configPath = Path.Combine(oriBFPath, "BepInEx", "config", "BepInEx.cfg");

        UnityEngine.Debug.Log("Downloading BepInEx config from: " + BEPINEX_CONFIG_URL);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(BEPINEX_CONFIG_URL))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Failed to download BepInEx config: " + request.error);
                ShowInfo("ERROR: Failed to download BepInEx config");
                yield break;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, request.downloadHandler.text);
                UnityEngine.Debug.Log("BepInEx config created at: " + configPath);
                ShowInfo("BepInEx config created successfully!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error creating BepInEx config: " + e.Message);
                ShowInfo("ERROR: Failed to create BepInEx config\n" + e.Message);
            }
        }
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

    void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string dest = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (string dir in Directory.GetDirectories(source))
        {
            string dest = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
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
            remoteConfig = JsonUtility.FromJson<OriBFConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyOriBFConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchGame()
    {
        string exePath = Path.Combine(oriBFPath, "oriDE.exe");

        if (File.Exists(exePath))
        {
            Process.Start(exePath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Game executable not found at: " + exePath);
        }
    }

    void CreateVersionFile(string apmodUrl, string bepinexUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/releases/download/([^/]+)/");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "OriBF Archipelago Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(oriBFPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(oriBFPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"OriBF Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(oriBFPath);
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

            string pluginsPath = Path.Combine(oriBFPath, "BepInEx", "plugins");
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

    string GetOriBFPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Ori DE"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Ori DE"),
            @"D:\Steam\steamapps\common\Ori DE",
            @"D:\SteamLibrary\steamapps\common\Ori DE",
            @"D:\steamapps\common\Ori DE",
            @"E:\Steam\steamapps\common\Ori DE",
            @"E:\SteamLibrary\steamapps\common\Ori DE",
            @"E:\steamapps\common\Ori DE",
            @"E:\Program Files (x86)\steamapps\common\Ori DE",
            @"E:\Program Files\steamapps\common\Ori DE",
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
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string oriBFPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Ori DE");
                    if (Directory.Exists(oriBFPath))
                        return oriBFPath;

                    oriBFPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Ori DE");
                    if (Directory.Exists(oriBFPath))
                        return oriBFPath;

                    oriBFPath = Path.Combine(drive.Name, "steamapps", "common", "Ori DE");
                    if (Directory.Exists(oriBFPath))
                        return oriBFPath;

                    oriBFPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Ori DE");
                    if (Directory.Exists(oriBFPath))
                        return oriBFPath;

                    oriBFPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Ori DE");
                    if (Directory.Exists(oriBFPath))
                        return oriBFPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}