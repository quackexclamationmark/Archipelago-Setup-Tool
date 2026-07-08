using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class RefunctManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData refunctApworld;
    public FileDownloader.FileData refunctAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;

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
        public string refunctApworld;
        public string refunctAP;
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

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installAPToggle != null)
            installAPToggle.isOn = true;

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

        refunctApworld.url = remoteConfig.refunctApworld;
        refunctAP.url = remoteConfig.refunctAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install Refunct Archipelago?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove Refunct Archipelago?", "Revert");
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
            ShowInfo("Game path not found. Please check your Refunct installation.");
            return;
        }

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(ApworldOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting Refunct Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string practiceWindowsPath = Path.Combine(gamePath, "practice-windows");

            // Delete practice-windows folder
            if (Directory.Exists(practiceWindowsPath))
            {
                try
                {
                    Directory.Delete(practiceWindowsPath, true);
                    UnityEngine.Debug.Log("Deleted practice-windows folder: " + practiceWindowsPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete practice-windows folder: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("practice-windows folder not found at: " + practiceWindowsPath);
            }

            DeleteOldVersionFiles();

            ShowInfo("Refunct Archipelago removed successfully!");
            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator ApworldOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallApworld();

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator APOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPFiles();

        CreateVersionFile(refunctAP.url, refunctApworld.url);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
            yield return InstallApworld();

        if (installAPToggle == null || installAPToggle.isOn)
        {
            yield return InstallAPFiles();
            CreateVersionFile(refunctAP.url, refunctApworld.url);
        }

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Refunct...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallAPFiles()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Refunct AP URL: " + refunctAP.url);

        if (string.IsNullOrEmpty(refunctAP.url))
        {
            ShowInfo("ERROR: Refunct AP URL is empty!");
            UnityEngine.Debug.LogError("Refunct AP URL not set!");
            yield break;
        }

        string practiceWindowsPath = Path.Combine(gamePath, "practice-windows");
        bool isExistingInstallation = false;

        if (Directory.Exists(practiceWindowsPath))
        {
            UnityEngine.Debug.Log("practice-windows folder already exists. Checking for existing installation...");
            isExistingInstallation = Directory.GetFiles(practiceWindowsPath, "*", SearchOption.AllDirectories).Length > 0;
        }

        if (!isExistingInstallation)
        {
            if (!Directory.Exists(practiceWindowsPath))
            {
                Directory.CreateDirectory(practiceWindowsPath);
                UnityEngine.Debug.Log("Created practice-windows folder: " + practiceWindowsPath);
            }

            string fileName = refunctAP.fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = refunctAP.url.Substring(refunctAP.url.LastIndexOf('/') + 1);

                if (fileName.Contains("?"))
                    fileName = fileName.Substring(0, fileName.IndexOf("?"));

                UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
            }

            string localPath = Path.Combine(Application.persistentDataPath, fileName);
            string extractPath = Path.Combine(Application.persistentDataPath, "RefunctAPTemp");

            UnityEngine.Debug.Log("Downloading Refunct AP from: " + refunctAP.url);
            UnityEngine.Debug.Log("Saving to: " + localPath);

            yield return DownloadFile(refunctAP.url, localPath);

            if (!File.Exists(localPath))
            {
                UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
                ShowInfo("ERROR: Refunct AP download failed!");
                yield break;
            }

            UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

            yield return downloader.DownloadAndExtract(refunctAP, Application.persistentDataPath, extractPath);

            // Look for practice-windows folder inside the extraction
            string nestedPracticeWindowsPath = Path.Combine(extractPath, "practice-windows");
            string sourcePath = extractPath;

            if (Directory.Exists(nestedPracticeWindowsPath))
            {
                UnityEngine.Debug.Log("Found nested practice-windows folder, using it as source");
                sourcePath = nestedPracticeWindowsPath;
            }

            if (Directory.Exists(sourcePath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(sourcePath.Length + 1);
                        string targetFile = Path.Combine(practiceWindowsPath, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                        File.Copy(file, targetFile, true);

                        UnityEngine.Debug.Log("Copied: " + targetFile);
                    }

                    SafeDeleteDirectory(extractPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Error extracting files: " + e.Message);
                    ShowInfo("ERROR: Failed to extract Refunct AP files\n" + e.Message);
                    yield break;
                }
            }

            try
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
            catch { }
        }

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Apworld URL: " + refunctApworld.url);

        if (string.IsNullOrEmpty(refunctApworld.url))
        {
            ShowInfo("ERROR: Apworld URL is empty!");
            UnityEngine.Debug.LogError("Apworld URL not set!");
            yield break;
        }

        string fileName = refunctApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = refunctApworld.url.Substring(refunctApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Apworld from: " + refunctApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(refunctApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Apworld download failed!");
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

            UnityEngine.Debug.Log("Apworld file copied to: " + target);

            ShowInfo("Apworld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Apworld: " + e.Message);
            ShowInfo("ERROR: Failed to install Apworld\n" + e.Message);
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
        string refunctTasPath = Path.Combine(gamePath, "practice-windows", "refunct-tas.exe");

        // Try to find the main game executable
        string gameExePath = Path.Combine(gamePath, "Refunct", "Binaries", "Win32", "Refunct-Win32-Shipping.exe");

        if (!File.Exists(gameExePath))
        {
            gameExePath = Path.Combine(gamePath, "Binaries", "Win32", "Refunct-Win32-Shipping.exe");
        }

        if (!File.Exists(gameExePath))
        {
            ShowInfo("Refunct executable not found at:\n" + gameExePath);
            UnityEngine.Debug.LogError("Refunct executable not found!");
            return;
        }

        if (!File.Exists(refunctTasPath))
        {
            ShowInfo("refunct-tas.exe not found at:\n" + refunctTasPath);
            UnityEngine.Debug.LogError("refunct-tas.exe not found!");
            return;
        }

        StartCoroutine(LaunchGameSequence(gameExePath, refunctTasPath));
    }

    IEnumerator LaunchGameSequence(string gameExePath, string refunctTasPath)
    {
        ProcessStartInfo gameStartInfo = null;
        ProcessStartInfo tasStartInfo = null;
        Process tasProcess = null;

        try
        {
            // Launch the main game
            UnityEngine.Debug.Log("Launching game from: " + gameExePath);
            gameStartInfo = new ProcessStartInfo
            {
                FileName = gameExePath,
                WorkingDirectory = Path.GetDirectoryName(gameExePath),
                UseShellExecute = true
            };
            gameProcess = Process.Start(gameStartInfo);
            UnityEngine.Debug.Log("Game launched successfully");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Refunct:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
            yield break;
        }

        // Wait 2 seconds
        yield return new WaitForSeconds(2f);

        try
        {
            // Launch refunct-tas.exe
            UnityEngine.Debug.Log("Launching refunct-tas.exe from: " + refunctTasPath);
            tasStartInfo = new ProcessStartInfo
            {
                FileName = refunctTasPath,
                WorkingDirectory = Path.GetDirectoryName(refunctTasPath),
                UseShellExecute = true
            };
            tasProcess = Process.Start(tasStartInfo);
            UnityEngine.Debug.Log("refunct-tas.exe launched successfully");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching refunct-tas.exe:\n" + e.Message);
            UnityEngine.Debug.LogError("TAS launch error: " + e);
            yield break;
        }

        // Wait 15 seconds for the game to close
        float timer = 0f;
        while (timer < 15f)
        {
            if (gameProcess.HasExited)
            {
                UnityEngine.Debug.Log("Game closed, relaunching...");
                yield return new WaitForSeconds(1f);

                try
                {
                    // Relaunch the game
                    gameProcess = Process.Start(gameStartInfo);
                    UnityEngine.Debug.Log("Game relaunched successfully");
                }
                catch (System.Exception e)
                {
                    ShowInfo("Error relaunching Refunct:\n" + e.Message);
                    UnityEngine.Debug.LogError("Relaunch error: " + e);
                }
                yield break;
            }

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Game did not close within 15 seconds, installation complete");
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
        ShowConfirmation("Are you sure you want to install Refunct Archipelago?", "Setup");
    }

    string GetGamePath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Refunct"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Refunct"),
            @"D:\Steam\steamapps\common\Refunct",
            @"D:\SteamLibrary\steamapps\common\Refunct",
            @"D:\steamapps\common\Refunct",
            @"E:\Steam\steamapps\common\Refunct",
            @"E:\SteamLibrary\steamapps\common\Refunct",
            @"E:\steamapps\common\Refunct",
            @"E:\Program Files (x86)\steamapps\common\Refunct",
            @"E:\Program Files\steamapps\common\Refunct",
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
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Refunct");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Refunct");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "steamapps", "common", "Refunct");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Refunct");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Refunct");
                    if (Directory.Exists(gamePath))
                        return gamePath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    void CreateVersionFile(string refunctApUrl, string apworldUrl)
    {
        try
        {
            string refunctApVersion = ExtractVersionFromUrl(refunctApUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "Refunct Archipelago Version " + refunctApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== REFUNCT ARCHIPELAGO ===\n";
            content += "Downloaded from: " + refunctApUrl + "\n";
            content += "Version: " + refunctApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
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
            if (!Directory.Exists(gamePath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Refunct Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(gamePath);
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