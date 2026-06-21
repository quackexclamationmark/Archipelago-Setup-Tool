using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class DOOMManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData doomAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installDOOMAPToggle;

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
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string doomAP;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public string doomArchipelagoPath = "";
        public List<string> installedFiles = new List<string>();
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

        if (installDOOMAPToggle != null)
            installDOOMAPToggle.isOn = true;

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

        doomAP.url = remoteConfig.doomAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install DOOM Archipelago?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove DOOM Archipelago?", "Revert");
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

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator SetupWithTracking()
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow();

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting DOOM Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string doomArchipelagoPath = Path.Combine(gamePath, "DOOM Archipelago");

            // Remove the DOOM Archipelago folder entirely
            if (Directory.Exists(doomArchipelagoPath))
            {
                try
                {
                    Directory.Delete(doomArchipelagoPath, true);
                    UnityEngine.Debug.Log("Deleted DOOM Archipelago folder: " + doomArchipelagoPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete DOOM Archipelago folder: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("DOOM Archipelago folder not found at: " + doomArchipelagoPath);
                ShowInfo("DOOM Archipelago folder not found.");
                yield break;
            }

            ShowInfo("DOOM Archipelago removed successfully!");
            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator InstallFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("ERROR: Game path not found!");
            yield break;
        }

        yield return InstallDOOMAP();

        CreateVersionFile(doomAP.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching DOOM Archipelago...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallDOOMAP()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. DOOMAP URL: " + doomAP.url);

        if (string.IsNullOrEmpty(doomAP.url))
        {
            ShowInfo("ERROR: DOOMAP URL is empty!");
            UnityEngine.Debug.LogError("DOOMAP URL not set!");
            yield break;
        }

        // Create DOOM Archipelago folder
        string doomArchipelagoPath = Path.Combine(gamePath, "DOOM Archipelago");
        bool isExistingInstallation = false;

        if (Directory.Exists(doomArchipelagoPath))
        {
            // Check if APDoomLauncher.exe exists
            string launcherPath = Path.Combine(doomArchipelagoPath, "APDoomLauncher.exe");
            if (File.Exists(launcherPath))
            {
                UnityEngine.Debug.Log("DOOM Archipelago folder already exists with launcher. Skipping extraction, only copying doom.wad");
                isExistingInstallation = true;
            }
        }

        if (!isExistingInstallation)
        {
            if (!Directory.Exists(doomArchipelagoPath))
            {
                Directory.CreateDirectory(doomArchipelagoPath);
                UnityEngine.Debug.Log("Created DOOM Archipelago folder: " + doomArchipelagoPath);
            }

            currentManifest.doomArchipelagoPath = doomArchipelagoPath;

            string fileName = doomAP.fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = doomAP.url.Substring(doomAP.url.LastIndexOf('/') + 1);

                if (fileName.Contains("?"))
                    fileName = fileName.Substring(0, fileName.IndexOf("?"));

                UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
            }

            string localPath = Path.Combine(Application.persistentDataPath, fileName);
            string extractPath = Path.Combine(Application.persistentDataPath, "DOOMAPTemp");

            UnityEngine.Debug.Log("Downloading DOOMAP from: " + doomAP.url);
            UnityEngine.Debug.Log("Saving to: " + localPath);

            yield return DownloadFile(doomAP.url, localPath);

            // Verify file exists
            if (!File.Exists(localPath))
            {
                UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
                ShowInfo("ERROR: DOOMAP download failed!");
                yield break;
            }

            UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

            // Extract zip
            yield return downloader.DownloadAndExtract(doomAP, Application.persistentDataPath, extractPath);

            // Copy all contents from extraction to DOOM Archipelago folder
            if (Directory.Exists(extractPath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(extractPath.Length + 1);
                        string targetFile = Path.Combine(doomArchipelagoPath, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                        File.Copy(file, targetFile, true);

                        if (currentManifest != null)
                            currentManifest.installedFiles.Add(targetFile);

                        UnityEngine.Debug.Log("Copied: " + targetFile);
                    }

                    SafeDeleteDirectory(extractPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Error extracting files: " + e.Message);
                    ShowInfo("ERROR: Failed to extract DOOMAP files\n" + e.Message);
                    yield break;
                }
            }

            // Clean up downloaded file
            try
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
            catch { }
        }

        // Copy doom.wad to DOOM Archipelago folder (always do this)
        string doomWadPath = Path.Combine(gamePath, "doom.wad");
        if (File.Exists(doomWadPath))
        {
            try
            {
                string targetWadPath = Path.Combine(doomArchipelagoPath, "doom.wad");
                File.Copy(doomWadPath, targetWadPath, true);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(targetWadPath);

                UnityEngine.Debug.Log("Copied doom.wad to: " + targetWadPath);
                ShowInfo("doom.wad copied successfully!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to copy doom.wad: " + e.Message);
                ShowInfo("WARNING: Failed to copy doom.wad\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("doom.wad not found at: " + doomWadPath);
            ShowInfo("WARNING: doom.wad not found!\nPlease verify your game installation.");
        }

        ShowInfo("DOOM Archipelago installed successfully!");
        yield return new WaitForSeconds(1f);
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
        string doomArchipelagoPath = Path.Combine(gamePath, "DOOM Archipelago");

        UnityEngine.Debug.Log("LaunchGame called. DOOM Archipelago Path: " + doomArchipelagoPath);

        string exePath = Path.Combine(doomArchipelagoPath, "APDoomLauncher.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("APDoomLauncher.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("APDoomLauncher.exe not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            gameProcess = Process.Start(exePath);
            UnityEngine.Debug.Log("DOOM Archipelago launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching DOOM Archipelago:\n" + e.Message);
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

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "DOOMInstalledFilesManifest.json");
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to install DOOM Archipelago?", "Setup");
    }

    string GetGamePath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Ultimate Doom"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Ultimate Doom"),
            @"D:\Steam\steamapps\common\Ultimate Doom",
            @"D:\SteamLibrary\steamapps\common\Ultimate Doom",
            @"D:\steamapps\common\Ultimate Doom",
            @"E:\Steam\steamapps\common\Ultimate Doom",
            @"E:\SteamLibrary\steamapps\common\Ultimate Doom",
            @"E:\steamapps\common\Ultimate Doom",
            @"E:\Program Files (x86)\steamapps\common\Ultimate Doom",
            @"E:\Program Files\steamapps\common\Ultimate Doom",
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
                    // Search for Steam\steamapps
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Ultimate Doom");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Search for SteamLibrary\steamapps
                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Ultimate Doom");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Search for steamapps at drive root
                    gamePath = Path.Combine(drive.Name, "steamapps", "common", "Ultimate Doom");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Search in Program Files (x86)\steamapps
                    gamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Ultimate Doom");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Search in Program Files\steamapps
                    gamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Ultimate Doom");
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

    void CreateVersionFile(string doomAPUrl)
    {
        try
        {
            string doomAPVersion = ExtractVersionFromUrl(doomAPUrl, "");

            string versionFileName = "DOOM Archipelago Version " + doomAPVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== DOOM ARCHIPELAGO ===\n";
            content += "Downloaded from: " + doomAPUrl + "\n";
            content += "Version: " + doomAPVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string doomArchipelagoPath = Path.Combine(gamePath, "DOOM Archipelago");
            string versionPath = Path.Combine(doomArchipelagoPath, versionFileName);
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
            string doomArchipelagoPath = Path.Combine(gamePath, "DOOM Archipelago");

            if (!Directory.Exists(doomArchipelagoPath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DOOM Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(doomArchipelagoPath);
            foreach (string file in files)
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
        // Pattern for Thunderstore: https://thunderstore.io/package/download/Author/Package/VERSION/
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        // Pattern for GitHub releases: /releases/download/VERSION/
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }
}
