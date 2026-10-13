using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class RaftManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("RAFT FILES")]
    public FileDownloader.FileData raftRML;
    public FileDownloader.FileData raftAP;
    public FileDownloader.FileData raftModUtils;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Raft";

    [Header("FEATURE TOGGLES")]
    public Toggle installRaftRMLToggle;      // optional (treated as ON if not assigned)
    public Toggle installRaftAPToggle;
    public Toggle installRaftModUtilsToggle; // optional (treated as ON if not assigned)

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearToggle;
    public Toggle clearAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string raftPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private RaftConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastDownloadOk = false;

    [System.Serializable]
    public class RaftConfig
    {
        public string raftRML;
        public string raftAP;
        public string raftModUtils;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        raftPath = GetRaftPath();
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

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);
    }

    void ApplyRaftConfig()
    {
        if (remoteConfig == null)
            return;

        raftRML.url = remoteConfig.raftRML;
        raftAP.url = remoteConfig.raftAP;
        raftModUtils.url = remoteConfig.raftModUtils;
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

    private bool IsOn(Toggle toggle)
    {
        // A toggle that is not assigned in the inspector counts as enabled
        return toggle == null || toggle.isOn;
    }

    private void ExecuteSetup()
    {
        raftPath = GetRaftPath();

        if (string.IsNullOrEmpty(raftPath))
        {
            ShowInfo("Raft path not found. Please check your installation.");
            return;
        }

        bool rml = IsOn(installRaftRMLToggle);
        bool raftap = installRaftAPToggle != null && installRaftAPToggle.isOn;
        bool modutils = IsOn(installRaftModUtilsToggle);

        if (!rml && !raftap && !modutils)
        {
            ShowInfo("Please select an option to install.");
            return;
        }

        StartCoroutine(InstallFlow(rml, raftap, modutils));
    }

    private void ExecuteRevert()
    {
        raftPath = GetRaftPath();

        if (string.IsNullOrEmpty(raftPath))
            return;

        bool clearAPMods = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;

        if (!clearAPMods && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAPMods)
        {
            string modsPath = Path.Combine(raftPath, "mods");

            if (!Directory.Exists(modsPath))
            {
                ShowInfo("AP mods not found!");
                return;
            }

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(modsPath, "Raftipelago.rmod"));
            SafeDeleteFile(Path.Combine(modsPath, "ModUtils.rmod"));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(Path.Combine(raftPath, "mods"));

        if (fullClear && hasOtherMods && !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully clear Raft?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        ShowInfo("Removing files...");

        string modsPath2 = Path.Combine(raftPath, "mods");
        SafeDeleteFile(Path.Combine(modsPath2, "Raftipelago.rmod"));
        SafeDeleteFile(Path.Combine(modsPath2, "ModUtils.rmod"));
        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath2);

        if (fullClear)
        {
            ShowInfo("Clearing Raft installation...");

            SafeDeleteFile(Path.Combine(raftPath, "RMLLauncher.exe"));
            SafeDeleteFile(Path.Combine(raftPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(raftPath, "RaftModLoader.dll"));
            SafeDeleteFile(Path.Combine(raftPath, "doorstop_config.ini"));
            SafeDeleteDirectory(modsPath2);

            ShowInfo("Full clear completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Clearing Raft mods...");

            SafeDeleteDirectory(modsPath2);

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            // Whitelist version files
            if (name.StartsWith("Raft Archipelago Version") && name.EndsWith(".txt"))
                continue;

            if (name != "Raftipelago.rmod" && name != "ModUtils.rmod")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow(bool installRML, bool installAP, bool installModUtils)
    {
        // Wait for the remote config (download URLs)
        while (!configLoaded)
            yield return null;

        // Always make sure the "mods" folder exists in the game directory
        string modsPath = Path.Combine(raftPath, "mods");

        try
        {
            Directory.CreateDirectory(modsPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[RaftInstall] Could not create mods folder: " + e.Message);
            ShowInfo("ERROR: Could not create the mods folder!\n" + e.Message);
            yield break;
        }

        // 1) RML Launcher (.exe -> game directory)
        if (installRML)
        {
            ShowInfo("Installing Raft Mod Loader...");
            yield return InstallSingleFile(
                raftRML.url,
                Path.Combine(raftPath, "RMLLauncher.exe"),
                "Raft Mod Loader");

            if (!lastDownloadOk)
                yield break;
        }

        // 2) Raftipelago.rmod (-> mods)
        if (installAP)
        {
            ShowInfo("Installing Raftipelago...");
            yield return InstallSingleFile(
                raftAP.url,
                Path.Combine(modsPath, "Raftipelago.rmod"),
                "Raftipelago");

            if (!lastDownloadOk)
                yield break;
        }

        // 3) ModUtils.rmod (-> mods)
        if (installModUtils)
        {
            ShowInfo("Installing ModUtils...");
            yield return InstallSingleFile(
                raftModUtils.url,
                Path.Combine(modsPath, "ModUtils.rmod"),
                "ModUtils");

            if (!lastDownloadOk)
                yield break;
        }

        CreateVersionFile(installRML, installAP, installModUtils);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Mod Loader...");
            LaunchModLoader();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    // Downloads a single file and copies it to destPath. Sets lastDownloadOk.
    IEnumerator InstallSingleFile(string url, string destPath, string label)
    {
        lastDownloadOk = false;

        if (string.IsNullOrEmpty(url))
        {
            UnityEngine.Debug.LogError("[RaftInstall] ERROR: URL for " + label + " is empty!");
            ShowInfo("ERROR: " + label + " URL is not set!");
            yield break;
        }

        string tempPath = Path.Combine(Application.persistentDataPath, Path.GetFileName(destPath) + ".tmp");

        SafeDeleteFile(tempPath);

        UnityEngine.Debug.Log("[RaftInstall] Downloading " + label + " from: " + url);

        yield return DownloadFile(url, tempPath);

        if (!File.Exists(tempPath))
        {
            UnityEngine.Debug.LogError("[RaftInstall] Download failed for " + label);
            ShowInfo("ERROR: " + label + " download failed!");
            yield break;
        }

        try
        {
            string destDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(tempPath, destPath, true);
            File.Delete(tempPath);

            UnityEngine.Debug.Log("[RaftInstall] " + label + " installed to: " + destPath);
            lastDownloadOk = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[RaftInstall] Error installing " + label + ": " + e.Message);
            ShowInfo("ERROR: Failed to install " + label + "\n" + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath)
            {
                removeFileOnAbort = true
            };

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);

                // Do not keep a partial / error-page file
                try
                {
                    if (File.Exists(savePath))
                        File.Delete(savePath);
                }
                catch { }
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

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
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

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsOnlyToggle != null)
        {
            clearAPModsOnlyToggle.isOn = false;
            clearAPModsOnlyToggle.interactable = !value;
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
            remoteConfig = JsonUtility.FromJson<RaftConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyRaftConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        raftPath = GetRaftPath();
    }

    void LaunchModLoader()
    {
        string exePath = Path.Combine(raftPath, "RMLLauncher.exe");

        if (File.Exists(exePath))
        {
            Process.Start(exePath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Mod Loader executable not found at: " + exePath);
            ShowInfo("ERROR: Mod Loader executable not found!");
        }
    }

    void CreateVersionFile(bool installRML, bool installAP, bool installModUtils)
    {
        try
        {
            // Version is read from the Raftipelago URL (…/releases/download/<version>/…)
            string raftapVersion = ExtractVersionFromUrl(raftAP.url, @"/releases/download/([^/]+)/");

            string versionFileName = "Raft Archipelago Version " + raftapVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";

            if (installRML)
            {
                content += "=== RAFT MOD LOADER ===\n";
                content += "Downloaded from: " + raftRML.url + "\n";
                content += "\n";
            }

            if (installAP)
            {
                content += "=== RAFTIPELAGO ===\n";
                content += "Downloaded from: " + raftAP.url + "\n";
                content += "Version: " + raftapVersion + "\n";
                content += "\n";
            }

            if (installModUtils)
            {
                content += "=== MODUTILS ===\n";
                content += "Downloaded from: " + raftModUtils.url + "\n";
                content += "\n";
            }

            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string modsPath = Path.Combine(raftPath, "mods");

            Directory.CreateDirectory(modsPath);

            string versionPath = Path.Combine(modsPath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created in mods: " + versionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Raft Archipelago Version .+\.txt");

            string modsPath = Path.Combine(raftPath, "mods");

            if (Directory.Exists(modsPath))
            {
                string[] modsFiles = Directory.GetFiles(modsPath);
                foreach (string file in modsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in mods: " + e.Message);
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

    string GetRaftPath()
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