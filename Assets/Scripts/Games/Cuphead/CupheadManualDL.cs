using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class CupheadManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CUPHEAD FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData cupheadArchipelago;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installBepInExToggle;
    public Toggle installCupheadArchipelagoToggle;

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

    private Process cupheadProcess;
    private string cupheadPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private CupheadConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class CupheadConfig
    {
        public string cupheadApworld;
        public string cupheadBepInEx;
        public string cupheadAP;
    }

    void Start()
    {
        cupheadPath = GetCupheadPath();
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyCupheadConfig()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.cupheadApworld;
        bepInEx.url = remoteConfig.cupheadBepInEx;
        cupheadArchipelago.url = remoteConfig.cupheadAP;
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
        if (string.IsNullOrEmpty(cupheadPath))
        {
            ShowInfo("Cuphead path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        cupheadPath = GetCupheadPath();

        if (string.IsNullOrEmpty(cupheadPath))
            return;

        string pluginsPath = Path.Combine(cupheadPath, "BepInEx", "plugins");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, "CupheadArchipelago"));

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

        SafeDeleteDirectory(Path.Combine(pluginsPath, "CupheadArchipelago"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(cupheadPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(cupheadPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(cupheadPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(cupheadPath, ".doorstop_version"));

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

                if (name.StartsWith("Cuphead Archipelago Version") && name.EndsWith(".txt"))
                    continue;

                if (name != "Cuphead Archipelago Version.txt")
                    return true;
            }

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName != "CupheadArchipelago")
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

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installCupheadArchipelagoToggle == null || installCupheadArchipelagoToggle.isOn)
        {
            ShowInfo("Installing Cuphead Archipelago...");
            yield return InstallCupheadArchipelago();
        }

        CreateVersionFile(apworld.url, bepInEx.url, cupheadArchipelago.url);

        ShowInfo("Launching Cuphead...");
        LaunchCuphead();

        yield return WaitForConfigFiles();

        CloseCuphead();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchCuphead();
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

        yield return DownloadFile(apworld.url, localPath);

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

    IEnumerator InstallCupheadArchipelago()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "CupheadArchipelagoTemp");
        yield return downloader.DownloadAndExtract(cupheadArchipelago, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(cupheadPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Move the entire CupheadArchipelago folder from extracted contents to plugins
        string sourceFolder = Path.Combine(extractPath, "CupheadArchipelago");
        string targetFolder = Path.Combine(pluginsPath, "CupheadArchipelago");

        if (Directory.Exists(sourceFolder))
        {
            // Remove existing folder if it exists
            if (Directory.Exists(targetFolder))
            {
                SafeDeleteDirectory(targetFolder);
            }

            // Copy the entire folder
            CopyDirectory(sourceFolder, targetFolder);
            UnityEngine.Debug.Log("Copied CupheadArchipelago folder to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("CupheadArchipelago source folder not found: " + sourceFolder);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        // Move all BepInEx content to the Cuphead directory root
        MoveDirectory(extractPath, cupheadPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(cupheadPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (!File.Exists(cfg) && timer < timeout)
        {
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/cuphead_config.json";

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
            remoteConfig = JsonUtility.FromJson<CupheadConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyCupheadConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchCuphead()
    {
        string exePath = Path.Combine(cupheadPath, "Cuphead.exe");

        if (File.Exists(exePath))
        {
            try
            {
                cupheadProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Cuphead launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Cuphead:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseCuphead()
    {
        try
        {
            if (cupheadProcess != null && !cupheadProcess.HasExited)
            {
                cupheadProcess.Kill();
                cupheadProcess.Dispose();
                cupheadProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseCuphead();
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

    string GetCupheadPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Cuphead"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Cuphead"),
            @"D:\Steam\steamapps\common\Cuphead",
            @"D:\SteamLibrary\steamapps\common\Cuphead",
            @"D:\steamapps\common\Cuphead",
            @"E:\Steam\steamapps\common\Cuphead",
            @"E:\SteamLibrary\steamapps\common\Cuphead",
            @"E:\steamapps\common\Cuphead",
            @"E:\Program Files (x86)\steamapps\common\Cuphead",
            @"E:\Program Files\steamapps\common\Cuphead",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Cuphead at: " + path);
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
                    string cupheadPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Cuphead");
                    if (Directory.Exists(cupheadPath))
                        return cupheadPath;

                    cupheadPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Cuphead");
                    if (Directory.Exists(cupheadPath))
                        return cupheadPath;

                    cupheadPath = Path.Combine(drive.Name, "steamapps", "common", "Cuphead");
                    if (Directory.Exists(cupheadPath))
                        return cupheadPath;

                    cupheadPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Cuphead");
                    if (Directory.Exists(cupheadPath))
                        return cupheadPath;

                    cupheadPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Cuphead");
                    if (Directory.Exists(cupheadPath))
                        return cupheadPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Cuphead not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apworldUrl, string bepinexUrl, string cupheadArchipelagoUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string cupheadArchipelagoVersion = ExtractVersionFromUrl(cupheadArchipelagoUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");

            string versionFileName = "Cuphead Archipelago Version " + cupheadArchipelagoVersion + ".txt";
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
            content += "=== CUPHEAD ARCHIPELAGO ===\n";
            content += "Downloaded from: " + cupheadArchipelagoUrl + "\n";
            content += "Version: " + cupheadArchipelagoVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(cupheadPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(cupheadPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Cuphead Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(cupheadPath);
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

            string pluginsPath = Path.Combine(cupheadPath, "BepInEx", "plugins");
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