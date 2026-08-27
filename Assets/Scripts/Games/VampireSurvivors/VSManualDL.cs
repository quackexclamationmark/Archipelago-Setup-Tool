using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class VSManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("VS FILES")]
    public FileDownloader.FileData vsApworld;
    public FileDownloader.FileData vsAP;
    public FileDownloader.FileData vsMelonLoader;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Vampire Survivors";

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

    private string vsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private VSConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class VSConfig
    {
        public string vsAP;
        public string vsMelonLoader;
        public string vsApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        vsPath = GetVSPath();
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

    void ApplyVSConfig()
    {
        if (remoteConfig == null) return;

        vsAP.url = remoteConfig.vsAP;
        vsMelonLoader.url = remoteConfig.vsMelonLoader;
        vsApworld.url = remoteConfig.vsApworld;
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
        vsPath = GetVSPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        bool needsGamePath = melonloader || apmod;

        if (needsGamePath && (string.IsNullOrEmpty(vsPath) || !Directory.Exists(vsPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (melonloader ? 1 : 0) + (apmod ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (melonloader && count == 1) { StartCoroutine(MelonLoaderOnlyFlow()); return; }
        if (apmod && count == 1) { StartCoroutine(APModOnlyFlow()); return; }

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
            LaunchVS();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    void ExecuteRevert()
    {
        vsPath = GetVSPath();
        if (string.IsNullOrEmpty(vsPath)) return;

        string modsPath = Path.Combine(vsPath, "Mods");
        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanMelonLoaderToggle != null && fullCleanMelonLoaderToggle.isOn;

        if (!removeAP && !fullClean) { ShowInfo("Please select at least one revert option."); return; }

        if (removeAP)
        {
            if (!Directory.Exists(modsPath) && !Directory.Exists(Path.Combine(vsPath, "UserLibs"))) return;

            ShowInfo("Removing AP mods...");

            // delete specific UserLibs files
            string userLibs = Path.Combine(vsPath, "UserLibs");
            SafeDeleteFile(Path.Combine(userLibs, "BundledApClient.dll"));

            // delete specific Mods entries
            SafeDeleteFile(Path.Combine(modsPath, "ArchipelagoSurvivors.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "AutoUnexcludeEquippedWeapons.dll"));
            SafeDeleteDirectory(Path.Combine(modsPath, "SW_CreeperKing.ArchipelagoSurvivors"));
            SafeDeleteDirectory(Path.Combine(modsPath, "ArchipelagoSurvivors"));

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

        ShowInfo("Removing mods...");

        // delete the AP-specific files when doing general removal as well
        string userLibs2 = Path.Combine(vsPath, "UserLibs");
        SafeDeleteFile(Path.Combine(userLibs2, "BundledApClient.dll"));

        SafeDeleteFile(Path.Combine(modsPath, "ArchipelagoSurvivors.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "AutoUnexcludeEquippedWeapons.dll"));
        SafeDeleteDirectory(Path.Combine(modsPath, "SW_CreeperKing.ArchipelagoSurvivors"));
        SafeDeleteDirectory(Path.Combine(modsPath, "ArchipelagoSurvivors"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning MelonLoader...");
            SafeDeleteDirectory(Path.Combine(vsPath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(vsPath, "UserData"));
            SafeDeleteDirectory(Path.Combine(vsPath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(vsPath, "Mods"));
            SafeDeleteDirectory(Path.Combine(vsPath, "Plugins"));
            SafeDeleteFile(Path.Combine(vsPath, "version.dll"));
            DeleteOldVersionFiles();
            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning MelonLoader...");
            SafeDeleteDirectory(Path.Combine(vsPath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(vsPath, "UserData"));
            SafeDeleteDirectory(Path.Combine(vsPath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(vsPath, "Mods"));
            SafeDeleteDirectory(Path.Combine(vsPath, "Plugins"));
            SafeDeleteFile(Path.Combine(vsPath, "version.dll"));
            DeleteOldVersionFiles();
            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        // ignore VS version files and known AP files
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("VS APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name == "ArchipelagoSurvivors.dll" || name == "AutoUnexcludeEquippedWeapons.dll")
                continue;

            // any other file counts as other mods
            return true;
        }

        // if directories exist, consider them other mods except the ones we install ourselves
        if (dirs.Length > 0)
        {
            bool allAreOurs = true;
            foreach (string dir in dirs)
            {
                string dname = Path.GetFileName(dir);
                if (dname != "SW_CreeperKing.ArchipelagoSurvivors" && dname != "ArchipelagoSurvivors")
                {
                    allAreOurs = false;
                    break;
                }
            }

            if (!allAreOurs)
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn) { ShowInfo("Installing APWorld..."); yield return InstallAPWorld(); }
        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn) { ShowInfo("Installing MelonLoader..."); yield return InstallMelonLoader(); }
        if (installAPModToggle == null || installAPModToggle.isOn) { ShowInfo("Installing Archipelago Survivors content..."); yield return InstallAPContent(); }

        CreateVersionFile(vsAP.url, vsMelonLoader.url, vsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Vampire Survivors...");
            yield return new WaitForSeconds(1f);
            LaunchVS();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + vsApworld.url);
        if (string.IsNullOrEmpty(vsApworld.url)) { ShowInfo("ERROR: APWorld URL is empty!"); UnityEngine.Debug.LogError("APWorld URL not set!"); yield break; }

        string fileName = vsApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = vsApworld.url.Substring(vsApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName)) fileName = "vampire_survivors.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        UnityEngine.Debug.Log("Downloading APWorld from: " + vsApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);
        yield return DownloadFile(vsApworld.url, localPath);

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
        UnityEngine.Debug.Log("START InstallAPContent (Archipelago Survivors)");
        ShowInfo("Downloading Archipelago Survivors content...");
        while (!configLoaded) yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ArchipelagoSurvivorsTemp");
        Directory.CreateDirectory(extractPath);

        UnityEngine.Debug.Log("Downloading and extracting from: " + vsAP.url + " to " + extractPath);
        yield return downloader.DownloadAndExtract(vsAP, Application.persistentDataPath, extractPath);

        // the zip contains a single subfolder; we need its CONTENTS copied into the game directory
        string contentFolder = FindSingleSubdirectory(extractPath);
        if (string.IsNullOrEmpty(contentFolder))
        {
            UnityEngine.Debug.LogWarning("No subfolder found inside archive; attempting to use root of extraction.");
            contentFolder = extractPath;
        }

        try
        {
            UnityEngine.Debug.Log("Copying Archipelago Survivors content from: " + contentFolder + " to game directory: " + vsPath);
            MoveDirectory(contentFolder, vsPath);
            ShowInfo("Archipelago Survivors content installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Archipelago Survivors content: " + e.Message);
            ShowInfo("ERROR: Failed to install Archipelago Survivors content\n" + e.Message);
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

        yield return downloader.DownloadAndExtract(vsMelonLoader, Application.persistentDataPath, extractPath);
        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, vsPath);
        Directory.CreateDirectory(Path.Combine(vsPath, "Mods"));
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
        vsPath = GetVSPath();
        if (string.IsNullOrEmpty(vsPath)) yield break;
        ShowInfo("Installing Archipelago Survivors content...");
        yield return InstallAPContent();
        CreateVersionFile(vsAP.url, vsMelonLoader.url, vsApworld.url);
        if (secondLaunchToggle == null || secondLaunchToggle.isOn) { ShowInfo("Launching Vampire Survivors..."); yield return new WaitForSeconds(1f); LaunchVS(); }
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
            remoteConfig = JsonUtility.FromJson<VSConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyVSConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        vsPath = GetVSPath();
    }

    void LaunchVS()
    {
        try
        {
            string exePath = Path.Combine(vsPath, "VampireSurvivors.exe");
            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = vsPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                UnityEngine.Debug.Log("Vampire Survivors launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("VampireSurvivors.exe not found at: " + exePath);
                ShowInfo("ERROR: VampireSurvivors.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Vampire Survivors: " + e.Message);
            ShowInfo("ERROR: Failed to launch Vampire Survivors!");
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

    // Returns the single top-level subdirectory inside root (the folder the zip
    // wraps its contents in), regardless of its name. If there isn't exactly
    // one top-level directory, returns empty string.
    string FindSingleSubdirectory(string root)
    {
        try
        {
            string[] dirs = Directory.GetDirectories(root);
            if (dirs.Length == 1) return dirs[0];
            UnityEngine.Debug.LogWarning("Expected exactly one subfolder inside archive, found: " + dirs.Length);
        }
        catch (System.Exception e) { UnityEngine.Debug.LogError("Error finding subdirectory: " + e.Message); }
        return "";
    }

    void CreateVersionFile(string apUrl, string melonloaderUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string melonloaderVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "VS APMod Version " + apVersion + ".txt";
            string content = "Vampire Survivors Archipelago Setup by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n\n";
            content += "=== AP CONTENT ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n\n";
            content += "=== MELONLOADER ===\n";
            content += "Downloaded from: " + melonloaderUrl + "\n";
            content += "Version: " + melonloaderVersion + "\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(vsPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(vsPath, "Mods");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"VS APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(vsPath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (pattern.IsMatch(fileName))
                {
                    try { File.Delete(file); UnityEngine.Debug.Log("Deleted old version file in root: " + fileName); }
                    catch (System.Exception e) { UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message); }
                }
            }

            string modsPath = Path.Combine(vsPath, "Mods");
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

    string GetVSPath()
    {
        return GetVSSteamPath();
    }

    string GetVSSteamPath()
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