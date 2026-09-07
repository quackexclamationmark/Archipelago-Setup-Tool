using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class PlagueIncManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("PLAGUEINK FILES")]
    public FileDownloader.FileData plagueincapworld;
    public FileDownloader.FileData plagueincMelonLoader;
    public FileDownloader.FileData plagueincAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "PlagueInc";

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

    private string plagueIncPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private PlagueIncConfig remoteConfig;
    private bool configLoaded = false;

    private static readonly string[] APModFiles = new string[]
    {
        "SW_CreeperKing.Plaguepelago",
        "Plaguepelago.dll",
    };

    private static readonly string[] APModFilesInUserLibs = new string[]
    {
        "BundledApClient.dll",
        "MultiClient LICENSE.md",
        "Newtonsoft LICENSE.md",
    };

    [System.Serializable]
    public class PlagueIncConfig
    {
        public string plagueincAP;
        public string plagueincMelonLoader;
        public string plagueincApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        plagueIncPath = GetPlagueIncPath();
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

    void ApplyPlagueIncConfig()
    {
        if (remoteConfig == null) return;

        plagueincAP.url = remoteConfig.plagueincAP;
        plagueincMelonLoader.url = remoteConfig.plagueincMelonLoader;
        plagueincapworld.url = remoteConfig.plagueincApworld;
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
        plagueIncPath = GetPlagueIncPath();

        bool apworldOn = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloaderOn = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmodOn = installAPModToggle == null || installAPModToggle.isOn;

        bool needsGamePath = melonloaderOn || apmodOn;

        if (needsGamePath && (string.IsNullOrEmpty(plagueIncPath) || !Directory.Exists(plagueIncPath)))
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
            LaunchPlagueInc();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    void ExecuteRevert()
    {
        plagueIncPath = GetPlagueIncPath();
        if (string.IsNullOrEmpty(plagueIncPath)) return;

        string modsPath = Path.Combine(plagueIncPath, "Mods");
        string userLibsPath = Path.Combine(plagueIncPath, "UserLibs");
        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanMelonLoaderToggle != null && fullCleanMelonLoaderToggle.isOn;

        if (!removeAP && !fullClean) { ShowInfo("Please select at least one revert option."); return; }

        if (removeAP)
        {
            ShowInfo("Removing AP mods...");

            // Remove AP mod files from Mods
            if (Directory.Exists(modsPath))
            {
                foreach (string apFile in APModFiles)
                {
                    string filePath = Path.Combine(modsPath, apFile);
                    if (File.Exists(filePath))
                    {
                        SafeDeleteFile(filePath);
                    }
                    else if (Directory.Exists(filePath))
                    {
                        SafeDeleteDirectory(filePath);
                    }
                }
            }

            // Remove AP mod files from UserLibs
            if (Directory.Exists(userLibsPath))
            {
                foreach (string apFile in APModFilesInUserLibs)
                {
                    string filePath = Path.Combine(userLibsPath, apFile);
                    SafeDeleteFile(filePath);
                }
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath, userLibsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation("Other mods were detected.\nDo you REALLY want to fully delete MelonLoader?", "ForceFullClean");
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Cleaning MelonLoader...");

        SafeDeleteFile(Path.Combine(plagueIncPath, "version.dll"));
        SafeDeleteDirectory(Path.Combine(plagueIncPath, "MelonLoader"));
        SafeDeleteDirectory(Path.Combine(plagueIncPath, "UserData"));
        SafeDeleteDirectory(Path.Combine(plagueIncPath, "UserLibs"));
        SafeDeleteDirectory(Path.Combine(plagueIncPath, "Mods"));
        SafeDeleteDirectory(Path.Combine(plagueIncPath, "Plugins"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    bool HasOtherMods(string modsPath, string userLibsPath)
    {
        // Check Mods directory
        if (Directory.Exists(modsPath))
        {
            string[] files = Directory.GetFiles(modsPath);
            string[] dirs = Directory.GetDirectories(modsPath);

            // ignore PlaguePelago version files and the specific AP mod files we install
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("PlaguePelago Version") && name.EndsWith(".txt"))
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

            // Check for other directories
            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                bool isAPModDir = false;
                foreach (string apFile in APModFiles)
                {
                    if (dirName == apFile) { isAPModDir = true; break; }
                }
                if (!isAPModDir)
                    return true;
            }
        }

        // Check UserLibs directory for other files (excluding AP mod files)
        if (Directory.Exists(userLibsPath))
        {
            string[] files = Directory.GetFiles(userLibsPath);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                bool isAPModFile = false;
                foreach (string apFile in APModFilesInUserLibs)
                {
                    if (name == apFile) { isAPModFile = true; break; }
                }
                if (!isAPModFile)
                    return true;
            }
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn) { ShowInfo("Installing APWorld..."); yield return InstallAPWorld(); }
        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn) { ShowInfo("Installing MelonLoader..."); yield return InstallMelonLoader(); }
        if (installAPModToggle == null || installAPModToggle.isOn) { ShowInfo("Installing PlaguePelago content..."); yield return InstallAP(); }

        CreateVersionFile(plagueincAP.url, plagueincMelonLoader.url, plagueincapworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Plague Inc...");
            yield return new WaitForSeconds(1f);
            LaunchPlagueInc();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + plagueincapworld.url);
        if (string.IsNullOrEmpty(plagueincapworld.url)) { ShowInfo("ERROR: APWorld URL is empty!"); UnityEngine.Debug.LogError("APWorld URL not set!"); yield break; }

        string fileName = plagueincapworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = plagueincapworld.url.Substring(plagueincapworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName)) fileName = "plague_inc.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        UnityEngine.Debug.Log("Downloading APWorld from: " + plagueincapworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);
        yield return DownloadFile(plagueincapworld.url, localPath);

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

    IEnumerator InstallAP()
    {
        UnityEngine.Debug.Log("START InstallAPContent (PlaguePelago)");
        ShowInfo("Downloading Archipelago Mod...");
        while (!configLoaded) yield return null;

        plagueIncPath = GetPlagueIncPath();
        if (string.IsNullOrEmpty(plagueIncPath))
        {
            UnityEngine.Debug.LogError("Game path not found for installing PlaguePelago!");
            ShowInfo("ERROR: Game path not found!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "PlaguePelagoTemp");
        SafeDeleteDirectory(extractPath);
        Directory.CreateDirectory(extractPath);

        UnityEngine.Debug.Log("Downloading and extracting from: " + plagueincAP.url + " to " + extractPath);
        yield return downloader.DownloadAndExtract(plagueincAP, Application.persistentDataPath, extractPath);

        string plaguepelagyFolder = FindDirectory(extractPath, "Plaguepelago");
        if (string.IsNullOrEmpty(plaguepelagyFolder))
        {
            UnityEngine.Debug.LogError("Plaguepelago folder not found in extracted content!");
            ShowInfo("ERROR: Plaguepelago folder not found in downloaded content!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        try
        {
            UnityEngine.Debug.Log("Copying Plaguepelago content from: " + plaguepelagyFolder + " to: " + plagueIncPath);
            MoveDirectory(plaguepelagyFolder, plagueIncPath);
            ShowInfo("PlaguePelago content installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy PlaguePelago content: " + e.Message);
            ShowInfo("ERROR: Failed to install PlaguePelago content\n" + e.Message);
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

        yield return downloader.DownloadAndExtract(plagueincMelonLoader, Application.persistentDataPath, extractPath);
        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, plagueIncPath);
        Directory.CreateDirectory(Path.Combine(plagueIncPath, "Mods"));
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
        plagueIncPath = GetPlagueIncPath();
        if (string.IsNullOrEmpty(plagueIncPath)) yield break;
        ShowInfo("Installing PlaguePelago content...");
        yield return InstallAP();
        CreateVersionFile(plagueincAP.url, plagueincMelonLoader.url, plagueincapworld.url);
        if (secondLaunchToggle == null || secondLaunchToggle.isOn) { ShowInfo("Launching Plague Inc..."); yield return new WaitForSeconds(1f); LaunchPlagueInc(); }
        else ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<PlagueIncConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyPlagueIncConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        plagueIncPath = GetPlagueIncPath();
    }

    void LaunchPlagueInc()
    {
        try
        {
            string exePath = Path.Combine(plagueIncPath, "PlagueIncEvolved.exe");
            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Plague Inc Evolved launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("PlagueIncEvolved.exe not found at: " + exePath);
                ShowInfo("ERROR: PlagueIncEvolved.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Plague Inc Evolved: " + e.Message);
            ShowInfo("ERROR: Failed to launch Plague Inc Evolved!");
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

            string versionFileName = "PlaguePelago Version " + apmodVersion + ".txt";
            string content = "Plague Inc Archipelago Setup by quack!\n";
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

            string rootVersionPath = Path.Combine(plagueIncPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(plagueIncPath, "Mods");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"PlaguePelago Version .+\.txt");

            if (Directory.Exists(plagueIncPath))
            {
                string[] rootFiles = Directory.GetFiles(plagueIncPath);
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

            string modsPath = Path.Combine(plagueIncPath, "Mods");
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

    string GetPlagueIncPath()
    {
        return GetPlagueIncSteamPath();
    }

    string GetPlagueIncSteamPath()
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
