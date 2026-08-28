using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class DTManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DT FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData melonLoader;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Demon Tides";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installMelonLoaderToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanMelonLoaderToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string dtPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private DTConfig remoteConfig;
    private bool configLoaded = false;

    private static readonly string[] APModFiles = new string[]
    {
        "Archipelago.MultiClient.Net.dll",
        "demon_tides_ap",
        "DemonTidesAP.dll",
    };

    [System.Serializable]
    public class DTConfig
    {
        public string dtAP;
        public string dtMelonLoader;
        public string dtApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        dtPath = GetDTPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (removeAPModsOnlyToggle != null) removeAPModsOnlyToggle.isOn = true;
        if (fullCleanMelonLoaderToggle != null) fullCleanMelonLoaderToggle.isOn = false;
        if (fullCleanMelonLoaderToggle != null) fullCleanMelonLoaderToggle.onValueChanged.AddListener(OnFullCleanChanged);
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

    void ApplyDTConfig()
    {
        if (remoteConfig == null) return;

        apMod.url = remoteConfig.dtAP;
        melonLoader.url = remoteConfig.dtMelonLoader;
        apworld.url = remoteConfig.dtApworld;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
    }

    void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        confirmationMessage.text = message;
        confirmationPanel.SetActive(true);
    }

    void OnConfirm()
    {
        confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
            case "ForceFullClean": ExecuteRevert(); break;
        }
    }

    void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    void ExecuteSetup()
    {
        dtPath = GetDTPath();

        bool apworldOn = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloaderOn = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmodOn = installAPModToggle == null || installAPModToggle.isOn;

        bool needsGamePath = melonloaderOn || apmodOn;

        if (needsGamePath && (string.IsNullOrEmpty(dtPath) || !Directory.Exists(dtPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworldOn ? 1 : 0) + (melonloaderOn ? 1 : 0) + (apmodOn ? 1 : 0);

        if (apworldOn && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (melonloaderOn && count == 1) { StartCoroutine(MelonLoaderOnlyFlow()); return; }
        if (apmodOn && count == 1) { StartCoroutine(APModOnlyFlow()); return; }

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
            LaunchDT();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    void ExecuteRevert()
    {
        dtPath = GetDTPath();
        if (string.IsNullOrEmpty(dtPath)) return;

        string modsPath = Path.Combine(dtPath, "Mods");
        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanMelonLoaderToggle != null && fullCleanMelonLoaderToggle.isOn;

        if (!removeAP && !fullClean) { ShowInfo("Please select at least one revert option."); return; }

        if (removeAP)
        {
            if (!Directory.Exists(modsPath)) return;

            ShowInfo("Removing AP mods...");

            // remove only the specific AP mod files from Mods
            foreach (string apFile in APModFiles)
            {
                SafeDeleteFile(Path.Combine(modsPath, apFile));
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation("Other mods were detected.\nDo you REALLY want to fully delete MelonLoader?", "ForceFullClean");
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Cleaning MelonLoader...");

        SafeDeleteDirectory(Path.Combine(dtPath, "MelonLoader"));
        SafeDeleteDirectory(Path.Combine(dtPath, "UserData"));
        SafeDeleteDirectory(Path.Combine(dtPath, "UserLibs"));
        SafeDeleteDirectory(Path.Combine(dtPath, "Mods"));
        SafeDeleteDirectory(Path.Combine(dtPath, "Plugins"));
        SafeDeleteFile(Path.Combine(dtPath, "version.dll"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        // ignore DT version files and the specific AP mod files we install
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("DT APMod Version") && name.EndsWith(".txt"))
                continue;

            bool isAPModFile = false;
            foreach (string apFile in APModFiles)
            {
                if (name == apFile) { isAPModFile = true; break; }
            }
            if (isAPModFile)
                continue;

            // any other file counts as other mods
            return true;
        }

        // any directory in Mods counts as other mods (we no longer create one)
        if (dirs.Length > 0)
            return true;

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn) { ShowInfo("Installing APWorld..."); yield return InstallAPWorld(); }
        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn) { ShowInfo("Installing MelonLoader..."); yield return InstallMelonLoader(); }
        if (installAPModToggle == null || installAPModToggle.isOn) { ShowInfo("Installing DemonTides Archipelago content..."); yield return InstallAPContent(); }

        CreateVersionFile(apMod.url, melonLoader.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Demon Tides...");
            yield return new WaitForSeconds(1f);
            LaunchDT();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded) { UnityEngine.Debug.Log("Waiting for config to load..."); yield return new WaitForSeconds(0.5f); }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + apworld.url);
        if (string.IsNullOrEmpty(apworld.url)) { ShowInfo("ERROR: APWorld URL is empty!"); UnityEngine.Debug.LogError("APWorld URL not set!"); yield break; }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName)) fileName = "demontides.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        UnityEngine.Debug.Log("Downloading APWorld from: " + apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);
        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath)) { UnityEngine.Debug.LogError("Download failed: file not found at " + localPath); ShowInfo("ERROR: APWorld download failed!"); yield break; }

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
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                target = path;
                UnityEngine.Debug.Log("Using target path: " + target);
                break;
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message); }
        }

        if (string.IsNullOrEmpty(target)) { ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!"); UnityEngine.Debug.LogError("No valid target directory found!"); yield break; }

        if (File.Exists(target)) { try { File.Delete(target); UnityEngine.Debug.Log("Deleted old apworld file"); } catch { } }

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

    IEnumerator InstallAPContent()
    {
        UnityEngine.Debug.Log("START InstallAPContent (DemonTides Archipelago)");
        ShowInfo("Downloading DemonTides Archipelago content...");
        while (!configLoaded) yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "DTAPTemp");
        Directory.CreateDirectory(extractPath);

        UnityEngine.Debug.Log("Downloading and extracting from: " + apMod.url + " to " + extractPath);
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(dtPath, "Mods");
        if (!Directory.Exists(modsPath))
        {
            Directory.CreateDirectory(modsPath);
            UnityEngine.Debug.Log("Created Mods directory: " + modsPath);
        }

        try
        {
            UnityEngine.Debug.Log("Copying extracted zip content from: " + extractPath + " to: " + modsPath);
            MoveDirectory(extractPath, modsPath);
            ShowInfo("DemonTides Archipelago content installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy DemonTides Archipelago content: " + e.Message);
            ShowInfo("ERROR: Failed to install DemonTides Archipelago content\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        UnityEngine.Debug.Log("END InstallAPContent");
        yield return null;
    }

    IEnumerator InstallMelonLoader()
    {
        UnityEngine.Debug.Log("START InstallMelonLoader");
        while (!configLoaded) yield return null;

        ShowInfo("Extracting MelonLoader...");
        string extractPath = Path.Combine(Application.persistentDataPath, "MelonLoaderTemp");
        UnityEngine.Debug.Log("Download path: " + extractPath);

        yield return downloader.DownloadAndExtract(melonLoader, Application.persistentDataPath, extractPath);
        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, dtPath);
        Directory.CreateDirectory(Path.Combine(dtPath, "Mods"));
        SafeDeleteDirectory(extractPath);
        UnityEngine.Debug.Log("END InstallMelonLoader");
        yield return null;
    }

    void CopyMelonLoaderFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyMelonLoaderFiles");
        string versionDllSource = FindFile(extractPath, "version.dll");
        if (!string.IsNullOrEmpty(versionDllSource))
        {
            File.Copy(versionDllSource, Path.Combine(targetPath, "version.dll"), true);
            UnityEngine.Debug.Log("version.dll copied");
        }
        else UnityEngine.Debug.LogWarning("version.dll not found!");

        string melonLoaderFolder = FindDirectory(extractPath, "MelonLoader");
        if (!string.IsNullOrEmpty(melonLoaderFolder))
        {
            string destFolder = Path.Combine(targetPath, "MelonLoader");
            if (Directory.Exists(destFolder)) SafeDeleteDirectory(destFolder);
            MoveDirectory(melonLoaderFolder, destFolder);
            UnityEngine.Debug.Log("MelonLoader folder copied");
        }
        else UnityEngine.Debug.LogWarning("MelonLoader folder not found!");

        UnityEngine.Debug.Log("END CopyMelonLoaderFiles");
    }

    IEnumerator MelonLoaderOnlyFlow()
    {
        ShowInfo("Installing MelonLoader...");
        yield return InstallMelonLoader();
        ShowInfo("MelonLoader installed successfully!");
        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        dtPath = GetDTPath();
        if (string.IsNullOrEmpty(dtPath)) yield break;
        ShowInfo("Installing DemonTides Archipelago content...");
        yield return InstallAPContent();
        CreateVersionFile(apMod.url, melonLoader.url, apworld.url);
        if (secondLaunchToggle == null || secondLaunchToggle.isOn) { ShowInfo("Launching Demon Tides..."); yield return new WaitForSeconds(1f); LaunchDT(); }
        else ShowInfo("Installation complete!");
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/dt_config.json";
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
            remoteConfig = JsonUtility.FromJson<DTConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDTConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        dtPath = GetDTPath();
    }

    void LaunchDT()
    {
        try
        {
            string exePath = Path.Combine(dtPath, "Demon Tides.exe");
            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Demon Tides launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Demon Tides.exe not found at: " + exePath);
                ShowInfo("ERROR: Demon Tides.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Demon Tides: " + e.Message);
            ShowInfo("ERROR: Failed to launch Demon Tides!");
        }
    }

    void SafeDeleteFile(string path) { StartCoroutine(DeleteFileForce(path)); }

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
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }

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

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName) return file;
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("Error finding file: " + e.Message); }
        return "";
    }

    string FindDirectory(string root, string dirName)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir) == dirName) return dir;
            }
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("Error finding directory: " + e.Message); }
        return "";
    }

    void CreateVersionFile(string apmodUrl, string melonloaderUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/releases/download/([^/]+)/");
            string melonloaderVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "DT APMod Version " + apmodVersion + ".txt";
            string content = "Demon Tides Archipelago Setup by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n\n";
            content += "=== AP CONTENT ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n\n";
            content += "=== MELONLOADER ===\n";
            content += "Downloaded from: " + melonloaderUrl + "\n";
            content += "Version: " + melonloaderVersion + "\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(dtPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(dtPath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods: " + modsVersionPath);
            }
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("Error creating version file: " + e.Message); }
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DT APMod Version .+\.txt");

            if (Directory.Exists(dtPath))
            {
                string[] rootFiles = Directory.GetFiles(dtPath);
                foreach (string file in rootFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try { File.Delete(file); UnityEngine.Debug.Log("Deleted old version file in root: " + fileName); }
                        catch (System.Exception e) { UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message); }
                    }
                }
            }

            string modsPath = Path.Combine(dtPath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string[] modsFiles = Directory.GetFiles(modsPath);
                foreach (string file in modsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try { File.Delete(file); UnityEngine.Debug.Log("Deleted old version file in Mods: " + fileName); }
                        catch (System.Exception e) { UnityEngine.Debug.LogWarning("Could not delete old version file in Mods: " + e.Message); }
                    }
                }
            }
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message); }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);
        if (match.Success) return match.Groups[1].Value;
        return "Unknown";
    }

    string GetDTPath()
    {
        return GetDTSteamPath();
    }

    string GetDTSteamPath()
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
}