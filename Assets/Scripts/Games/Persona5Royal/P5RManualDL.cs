using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class P5RManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData p5rApworld;
    public FileDownloader.FileData p5rAP;
    public FileDownloader.FileData p5rReloadedII;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "P5R";

    // Every mod folder installed by the AP mod zip MUST start with this prefix.
    private const string APModFolderPrefix = "P5R_Archipelago";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installReloadedIIToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearReloadedToggle;
    public Toggle clearAPModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string gamePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string p5rAP;
        public string p5rApworld;
        public string p5rReloadedII;
        public string[] steamSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
        public List<string> installedDirectories = new List<string>();
    }

    void Start()
    {
        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (installReloadedIIToggle != null)
            installReloadedIIToggle.isOn = true;

        if (fullClearReloadedToggle != null)
        {
            fullClearReloadedToggle.isOn = false;
            fullClearReloadedToggle.onValueChanged.AddListener(OnFullClearChanged);
        }

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    // TOGGLE RULE: If fullClearReloaded is true, clearAPMods must be disabled
    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = !value ? clearAPModsToggle.isOn : false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        p5rAP.url = remoteConfig.p5rAP;
        p5rApworld.url = remoteConfig.p5rApworld;
        p5rReloadedII.url = remoteConfig.p5rReloadedII;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void RevertAll()
    {
        bool fullClear = fullClearReloadedToggle != null && fullClearReloadedToggle.isOn;
        bool clearAPMods = clearAPModsToggle != null && clearAPModsToggle.isOn;

        if (!fullClear && !clearAPMods)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

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
        gamePath = GetGamePath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;
        bool reloadedII = installReloadedIIToggle == null || installReloadedIIToggle.isOn;

        bool needsGamePath = reloadedII || apmod;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (apmod ? 1 : 0) + (reloadedII ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupWithTracking(apworld, apmod, reloadedII));
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching P5R...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupWithTracking(bool installAPWorld, bool installAPMod, bool installReloadedII)
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow(installAPWorld, installAPMod, installReloadedII);

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            return;

        DeleteOldVersionFiles();

        string reloadedPath = Path.Combine(gamePath, "Reloaded-II");
        string modsPath = Path.Combine(reloadedPath, "Mods");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClean = fullClearReloadedToggle != null && fullClearReloadedToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing Archipelago mods...");

            DeleteAPModFolders(modsPath);

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation(
                "Other mods were detected in Reloaded-II\\Mods.\nDo you REALLY want to fully delete Reloaded-II and related files?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Removing AP mods...");

        DeleteAPModFolders(modsPath);

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning Reloaded-II...");

            SafeDeleteDirectory(reloadedPath);

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning Reloaded-II (no other mods detected)...");

            SafeDeleteDirectory(reloadedPath);

            ShowInfo("Revert completed!");
        }
    }

    // Deletes every mod folder inside Mods whose name starts with the AP prefix.
    void DeleteAPModFolders(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return;

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);

            if (dirName.StartsWith(APModFolderPrefix))
                SafeDeleteDirectory(dir);
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);

            if (dirName.StartsWith(APModFolderPrefix))
                continue;

            return true;
        }

        return false;
    }

    IEnumerator InstallFlow(bool installAPWorld, bool installAPMod, bool installReloadedII)
    {
        if (installReloadedII)
        {
            ShowInfo("Installing Reloaded-II...");
            yield return InstallReloadedII();
        }

        if (installAPMod)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (installAPWorld)
        {
            ShowInfo("Installing P5R APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(p5rAP.url, p5rApworld.url, p5rReloadedII.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Mod Manager...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallReloadedII()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "P5RReloadedIITemp");
        string targetPath = Path.Combine(gamePath, "Reloaded-II");

        yield return downloader.DownloadAndExtract(p5rReloadedII, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Reloaded-II extraction failed!");
            yield break;
        }

        try
        {
            CopyAllFromExtract(extractPath, targetPath);
            ShowInfo("Reloaded-II installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install Reloaded-II\n" + e.Message);
            UnityEngine.Debug.LogError("Reloaded-II installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "P5RAPModTemp");

        yield return downloader.DownloadAndExtract(p5rAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: AP Mod extraction failed!");
            yield break;
        }

        string reloadedPath = Path.Combine(gamePath, "Reloaded-II");
        string modsPath = Path.Combine(reloadedPath, "Mods");

        try
        {
            if (!Directory.Exists(modsPath))
                Directory.CreateDirectory(modsPath);

            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(modsPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                if (File.Exists(dest))
                    File.Delete(dest);

                File.Move(file, dest);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(dest);
            }

            if (currentManifest != null && !currentManifest.installedDirectories.Contains(modsPath))
                currentManifest.installedDirectories.Add(modsPath);

            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
            UnityEngine.Debug.LogError("AP Mod installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + p5rApworld.url);

        if (string.IsNullOrEmpty(p5rApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = p5rApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = p5rApworld.url.Substring(p5rApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + p5rApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(p5rApworld.url, localPath);

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

        UnityEngine.Debug.Log("Target path: " + target);

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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

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
            remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGameConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        gamePath = GetGamePath();
    }

    void LaunchGame()
    {
        string currentGamePath = GetGamePath();
        UnityEngine.Debug.Log("LaunchGame called. GamePath: " + currentGamePath);

        if (string.IsNullOrEmpty(currentGamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string exePath = Path.Combine(currentGamePath, "Reloaded-II", "Reloaded-II.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Reloaded-II.exe not found. Checked:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            Process.Start(psi);
            UnityEngine.Debug.Log("Game launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseGame()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("Reloaded-II");
            foreach (Process p in processes)
            {
                if (!p.HasExited)
                {
                    p.Kill();
                    p.Dispose();
                }
            }
        }
        catch { }
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

    void CopyAllFromExtract(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyAllFromExtract");

        if (!Directory.Exists(extractPath))
            return;

        try
        {
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to ensure target root directory: " + targetPath + " - " + e.Message);
        }

        foreach (string dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destDir = Path.Combine(targetPath, rel);
            try
            {
                if (Directory.Exists(destDir))
                    SafeDeleteDirectory(destDir);
                Directory.CreateDirectory(destDir);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to create dest dir: " + destDir + " - " + e.Message);
            }
        }

        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(targetPath, rel);
            try
            {
                string destDirName = Path.GetDirectoryName(dest);

                if (!string.IsNullOrWhiteSpace(destDirName))
                {
                    Directory.CreateDirectory(destDirName);
                }
                else
                {
                    if (!Directory.Exists(targetPath))
                        Directory.CreateDirectory(targetPath);
                }

                if (File.Exists(dest))
                    File.Delete(dest);

                File.Copy(file, dest, true);
                UnityEngine.Debug.Log("Copied file: " + dest);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(dest);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to copy file: " + file + " - " + e.Message);
            }
        }

        try
        {
            if (currentManifest != null && !string.IsNullOrEmpty(targetPath))
            {
                if (!currentManifest.installedDirectories.Contains(targetPath))
                    currentManifest.installedDirectories.Add(targetPath);
            }
        }
        catch { }

        UnityEngine.Debug.Log("END CopyAllFromExtract");
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "P5RInstalledFilesManifest.json");
        string json = JsonUtility.ToJson(manifest, true);

        try
        {
            File.WriteAllText(manifestPath, json);
            UnityEngine.Debug.Log("Installation manifest saved: " + manifestPath);
            UnityEngine.Debug.Log("Tracked " + manifest.installedFiles.Count + " files for future revert");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save manifest: " + e.Message);
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

    string GetGamePath()
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

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apModUrl, string apworldUrl, string reloadedIIUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                UnityEngine.Debug.LogWarning("CreateVersionFile: gamePath is empty or doesn't exist. Skipping version file creation.");
                return;
            }

            string apModVersion = ExtractVersionFromUrl(apModUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string reloadedIIVersion = ExtractVersionFromUrl(reloadedIIUrl, "");

            string versionFileName = "P5R AP Version " + apModVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apModUrl + "\n";
            content += "Version: " + apModVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== RELOADED-II ===\n";
            content += "Downloaded from: " + reloadedIIUrl + "\n";
            content += "Version: " + reloadedIIVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(versionPath);
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
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                UnityEngine.Debug.LogWarning("DeleteOldVersionFiles: gamePath is empty or doesn't exist. Skipping cleanup of old version files.");
                return;
            }

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"P5R AP Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
            foreach (string file in rootFiles)
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

    string ExtractVersionFromUrl(string url, string pattern)
    {
        // Thunderstore pattern: https://thunderstore.io/package/download/Author/Package/VERSION/
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