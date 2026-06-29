using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class FuniManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData funiraccoonAP;
    public FileDownloader.FileData funiraccoonApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installModsToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class GameConfig
    {
        public string funiraccoonAP;
        public string funiraccoonApworld;
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

        if (installModsToggle != null)
            installModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        if (!string.IsNullOrEmpty(remoteConfig.funiraccoonAP))
            funiraccoonAP.url = remoteConfig.funiraccoonAP;

        if (!string.IsNullOrEmpty(remoteConfig.funiraccoonApworld))
            funiraccoonApworld.url = remoteConfig.funiraccoonApworld;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove all mods?", "Revert");
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
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool mods = installModsToggle == null || installModsToggle.isOn;

        int count = (apworld ? 1 : 0) + (mods ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (mods && count == 1)
        {
            StartCoroutine(ModsOnlyFlow());
            return;
        }

        StartCoroutine(SetupFlow());
    }

    IEnumerator SetupFlow()
    {
        ShowInfo("Downloading and installing files...");
        yield return InstallFlow();
        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        // refresh game path in case it's changed
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Cannot revert.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting modifications...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string[] specificFiles = new string[] { "mod_boot.gd", "mods.pck", "override.cfg" };

            foreach (string f in specificFiles)
            {
                string p = Path.Combine(gamePath, f);
                if (File.Exists(p))
                {
                    try
                    {
                        File.SetAttributes(p, FileAttributes.Normal);
                        File.Delete(p);
                        UnityEngine.Debug.Log("Deleted: " + p);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Failed to delete specific file: " + p + " - " + e.Message);
                    }
                }
            }

            // Optionally remove leftover empty directories in the game path
            RemoveEmptyDirectories(gamePath);

            ShowInfo("Revert complete!");
            UnityEngine.Debug.Log("Revert completed");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    void RemoveEmptyDirectories(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                try
                {
                    if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    IEnumerator APWorldOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator ModsOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallFuniMods();

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installModsToggle == null || installModsToggle.isOn)
            yield return InstallFuniMods();

        CreateVersionFile(funiraccoonAP.url, funiraccoonApworld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallFuniMods()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "FuniModFilesTemp");

        yield return downloader.DownloadAndExtract(funiraccoonAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Extraction failed!");
            yield break;
        }

        // Determine actual source root inside the extracted folder:
        // If the zip has a single root folder, use it; otherwise use extractPath itself.
        string sourceRoot = extractPath;
        string[] directories = Directory.GetDirectories(extractPath);
        if (directories.Length == 1 && Directory.GetFiles(extractPath).Length == 0)
        {
            sourceRoot = directories[0];
            UnityEngine.Debug.Log("Single-folder zip detected, using root: " + sourceRoot);
        }

        // Move all files/folders from sourceRoot into the game root
        MoveDirectory(sourceRoot, gamePath);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + funiraccoonApworld.url);

        if (string.IsNullOrEmpty(funiraccoonApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = funiraccoonApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = funiraccoonApworld.url.Substring(funiraccoonApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + funiraccoonApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(funiraccoonApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // Targets to try for Archipelago custom_worlds
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
            UnityEngine.Debug.LogError("Config load failed: " + request.error);
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
            UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
        }

        configLoaded = true;
        UnityEngine.Debug.Log("Config marked as loaded");
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

        string exeName = "Funi_Raccoon_Game.exe";
        string exePath = Path.Combine(currentGamePath, exeName);

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found. Checked:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        // Build override.cfg full path
        string overrideCfgPath = Path.Combine(currentGamePath, "override.cfg");

        // Steam-style argument: --override-cfg "%STEAMAPPS%\common\Funi Raccoon Game\override.cfg"
        // We'll pass the concrete path
        string args = $"--override-cfg \"{overrideCfgPath}\"";

        UnityEngine.Debug.Log("Starting process: " + exePath + " " + args);

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = currentGamePath,
                UseShellExecute = false,
            };

            gameProcess = Process.Start(psi);
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
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        // Move all files preserving relative structure
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(target, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        // Also handle directories that may be empty after moving
        try
        {
            // Remove any leftover empty directories in source
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories).Reverse())
            {
                try
                {
                    if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch { }
            }
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    string GetGamePath()
    {
        string gameFolderName = "Funi Raccoon Game";

        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", gameFolderName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", gameFolderName),
            @"D:\Steam\steamapps\common\" + gameFolderName,
            @"D:\SteamLibrary\steamapps\common\" + gameFolderName,
            @"D:\steamapps\common\" + gameFolderName,
            @"E:\Steam\steamapps\common\" + gameFolderName,
            @"E:\SteamLibrary\steamapps\common\" + gameFolderName,
            @"E:\steamapps\common\" + gameFolderName,
            @"E:\Program Files (x86)\steamapps\common\" + gameFolderName,
            @"E:\Program Files\steamapps\common\" + gameFolderName,
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
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", gameFolderName);
                    if (Directory.Exists(gamePath))
                        return gamePath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string modFilesUrl, string apworldUrl)
    {
        try
        {
            string modFilesVersion = ExtractVersionFromUrl(modFilesUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "FuniRaccoon Mod Version " + modFilesVersion + ".txt";
            string content = "Archipelago Setup Tool adapted for Funi Raccoon\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== MOD FILES ===\n";
            content += "Downloaded from: " + (modFilesUrl ?? "Unknown") + "\n";
            content += "Version: " + modFilesVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + (apworldUrl ?? "Unknown") + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            if (!string.IsNullOrEmpty(gamePath))
            {
                string coreVersionPath = Path.Combine(gamePath, versionFileName);
                File.WriteAllText(coreVersionPath, content);
                UnityEngine.Debug.Log("Version file created: " + coreVersionPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("CreateVersionFile: gamePath is empty, skipping version file creation.");
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
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                UnityEngine.Debug.LogWarning("DeleteOldVersionFiles: gamePath invalid or does not exist, skipping.");
                return;
            }

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"FuniRaccoon Mod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (fileName == null)
                    continue;

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
        if (string.IsNullOrEmpty(url))
            return "Unknown";

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