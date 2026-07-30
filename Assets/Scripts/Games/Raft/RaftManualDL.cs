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
    public FileDownloader.FileData raftAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Raft";

    [Header("FEATURE TOGGLES")]
    public Toggle installRaftAPToggle;

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

    [System.Serializable]
    public class RaftConfig
    {
        public string raftAP;
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

        raftAP.url = remoteConfig.raftAP;
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

    private void ExecuteSetup()
    {
        raftPath = GetRaftPath();

        if (string.IsNullOrEmpty(raftPath))
        {
            ShowInfo("Raft path not found. Please check your installation.");
            return;
        }

        bool raftap = installRaftAPToggle != null && installRaftAPToggle.isOn;

        if (!raftap)
        {
            ShowInfo("Please select an option to install.");
            return;
        }

        StartCoroutine(InstallFlow());
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

    IEnumerator InstallFlow()
    {
        ShowInfo("Installing Raftipelago...");
        yield return InstallRaftAP();

        CreateVersionFile(raftAP.url);

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

    IEnumerator InstallRaftAP()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("[InstallRaftAP] Waiting for config to load...");
            yield return null;
        }

        UnityEngine.Debug.Log("[InstallRaftAP] Config loaded!");
        UnityEngine.Debug.Log("[InstallRaftAP] raftAP URL: " + raftAP.url);

        if (string.IsNullOrEmpty(raftAP.url))
        {
            UnityEngine.Debug.LogError("[InstallRaftAP] ERROR: raftAP.url is empty!");
            ShowInfo("ERROR: Raftipelago URL is not set!");
            yield break;
        }

        string tempExtractPath = Path.Combine(Application.persistentDataPath, "RaftAPTemp");
        string zipFileName = "Preloaded-RMLL-Raft.zip";
        string zipPath = Path.Combine(Application.persistentDataPath, zipFileName);

        UnityEngine.Debug.Log("[InstallRaftAP] Downloading ZIP from: " + raftAP.url);
        UnityEngine.Debug.Log("[InstallRaftAP] Saving to: " + zipPath);

        yield return DownloadFile(raftAP.url, zipPath);

        if (!File.Exists(zipPath))
        {
            UnityEngine.Debug.LogError("[InstallRaftAP] Download failed: ZIP not found at " + zipPath);
            ShowInfo("ERROR: Raftipelago download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("[InstallRaftAP] ZIP downloaded successfully");

        // Extract ZIP
        UnityEngine.Debug.Log("[InstallRaftAP] Extracting ZIP...");
        yield return ExtractZip(zipPath, tempExtractPath);

        // Look for Preloaded-Raft subdirectory
        string preloadedPath = Path.Combine(tempExtractPath, "Preloaded-Raft");

        if (!Directory.Exists(preloadedPath))
        {
            UnityEngine.Debug.LogError("[InstallRaftAP] Preloaded-Raft subdirectory not found!");
            ShowInfo("ERROR: Invalid Raftipelago ZIP structure!");
            SafeDeleteDirectory(tempExtractPath);
            SafeDeleteFile(zipPath);
            yield break;
        }

        UnityEngine.Debug.Log("[InstallRaftAP] Found Preloaded-Raft directory");

        // Copy all contents from Preloaded-Raft directly to Raft root directory
        UnityEngine.Debug.Log("[InstallRaftAP] Copying all files from Preloaded-Raft to Raft directory");
        CopyDirectoryContents(preloadedPath, raftPath);

        // Cleanup
        SafeDeleteDirectory(tempExtractPath);
        SafeDeleteFile(zipPath);

        UnityEngine.Debug.Log("[InstallRaftAP] Installation complete!");
        ShowInfo("Raftipelago installed successfully!");
    }

    void CopyDirectoryContents(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        // Copy all files from source to target
        foreach (string file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, destFile, true);
            UnityEngine.Debug.Log("[InstallRaftAP] Copied file: " + Path.GetFileName(file));
        }

        // Recursively copy all directories
        foreach (string dir in Directory.GetDirectories(source))
        {
            string dirName = Path.GetFileName(dir);
            string targetDir = Path.Combine(target, dirName);
            CopyDirectoryContents(dir, targetDir);
        }
    }

    IEnumerator ExtractZip(string zipPath, string extractPath)
    {
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            UnityEngine.Debug.Log("[InstallRaftAP] ZIP extracted to: " + extractPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[InstallRaftAP] Error extracting ZIP: " + e.Message);
            ShowInfo("ERROR: Failed to extract Raftipelago ZIP\n" + e.Message);
        }

        yield return null;
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

    void CreateVersionFile(string raftapUrl)
    {
        try
        {
            string raftapVersion = ExtractVersionFromUrl(raftapUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "Raft Archipelago Version " + raftapVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== RAFTIPELAGO ===\n";
            content += "Downloaded from: " + raftapUrl + "\n";
            content += "Version: " + raftapVersion + "\n";
            content += "\n";
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
