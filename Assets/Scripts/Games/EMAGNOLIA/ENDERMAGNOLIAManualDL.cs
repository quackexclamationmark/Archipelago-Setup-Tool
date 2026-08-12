using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class ENDERMAGNOLIAManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData endermagnoliaApworld;
    public FileDownloader.FileData endermagnoliaAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "ENDER MAGNOLIA";

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
        public string endermagnoliaApworld;
        public string endermagnoliaAP;
        public string[] steamSearchPaths;
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

        endermagnoliaApworld.url = remoteConfig.endermagnoliaApworld;
        endermagnoliaAP.url = remoteConfig.endermagnoliaAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to start setup?", "Setup");
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private string GetWin64Path()
    {
        return Path.Combine(gamePath, "EnderMagnolia", "Binaries", "Win64");
    }

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;
        bool needsGamePath = ap;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallApworld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Ender Magnolia...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting Ender Magnolia Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string win64Path = GetWin64Path();
            string randomizerPath = Path.Combine(win64Path, "EnderMagnolia.Randomizer");
            string versionDllPath = Path.Combine(win64Path, "version.dll");

            if (Directory.Exists(randomizerPath))
            {
                try
                {
                    Directory.Delete(randomizerPath, true);
                    UnityEngine.Debug.Log("Deleted EnderMagnolia.Randomizer folder: " + randomizerPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete EnderMagnolia.Randomizer folder: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("EnderMagnolia.Randomizer folder not found at: " + randomizerPath);
            }

            if (File.Exists(versionDllPath))
            {
                try
                {
                    File.Delete(versionDllPath);
                    UnityEngine.Debug.Log("Deleted version.dll: " + versionDllPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete version.dll: " + e.Message);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("version.dll not found at: " + versionDllPath);
            }

            DeleteOldVersionFiles();

            ShowInfo("Ender Magnolia Archipelago removed successfully!");
            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator APOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPFiles(endermagnoliaAP);

        CreateVersionFile(endermagnoliaAP.url, endermagnoliaApworld.url);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
            yield return InstallApworld();

        if (installAPToggle == null || installAPToggle.isOn)
        {
            yield return InstallAPFiles(endermagnoliaAP);
            CreateVersionFile(endermagnoliaAP.url, endermagnoliaApworld.url);
        }

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Ender Magnolia...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallAPFiles(FileDownloader.FileData apFile)
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Ender Magnolia AP URL: " + apFile.url);

        if (string.IsNullOrEmpty(apFile.url))
        {
            ShowInfo("ERROR: Ender Magnolia AP URL is empty!");
            UnityEngine.Debug.LogError("Ender Magnolia AP URL not set!");
            yield break;
        }

        string win64Path = GetWin64Path();
        string randomizerPath = Path.Combine(win64Path, "EnderMagnolia.Randomizer");
        bool isExistingInstallation = Directory.Exists(randomizerPath) &&
            Directory.GetFiles(randomizerPath, "*", SearchOption.AllDirectories).Length > 0;

        if (!isExistingInstallation)
        {
            if (!Directory.Exists(win64Path))
            {
                Directory.CreateDirectory(win64Path);
                UnityEngine.Debug.Log("Created Win64 folder: " + win64Path);
            }

            string fileName = apFile.fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = apFile.url.Substring(apFile.url.LastIndexOf('/') + 1);

                if (fileName.Contains("?"))
                    fileName = fileName.Substring(0, fileName.IndexOf("?"));

                UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
            }

            string localPath = Path.Combine(Application.persistentDataPath, fileName);
            string extractPath = Path.Combine(Application.persistentDataPath, "EnderMagnoliaAPTemp");

            UnityEngine.Debug.Log("Downloading Ender Magnolia AP from: " + apFile.url);
            UnityEngine.Debug.Log("Saving to: " + localPath);

            yield return DownloadFile(apFile.url, localPath);

            if (!File.Exists(localPath))
            {
                UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
                ShowInfo("ERROR: Ender Magnolia AP download failed!");
                yield break;
            }

            UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

            yield return downloader.DownloadAndExtract(apFile, Application.persistentDataPath, extractPath);

            if (Directory.Exists(extractPath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(extractPath.Length + 1);
                        string targetFile = Path.Combine(win64Path, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                        File.Copy(file, targetFile, true);

                        UnityEngine.Debug.Log("Copied: " + targetFile);
                    }

                    SafeDeleteDirectory(extractPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Error extracting files: " + e.Message);
                    ShowInfo("ERROR: Failed to extract Ender Magnolia AP files\n" + e.Message);
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

        ShowInfo("Ender Magnolia Archipelago installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Apworld URL: " + endermagnoliaApworld.url);

        if (string.IsNullOrEmpty(endermagnoliaApworld.url))
        {
            ShowInfo("ERROR: Apworld URL is empty!");
            UnityEngine.Debug.LogError("Apworld URL not set!");
            yield break;
        }

        string fileName = endermagnoliaApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = endermagnoliaApworld.url.Substring(endermagnoliaApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Apworld from: " + endermagnoliaApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(endermagnoliaApworld.url, localPath);

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
        string exePath = Path.Combine(gamePath, "EnderMagnolia.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("EnderMagnolia.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("EnderMagnolia.exe not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gamePath,
                UseShellExecute = true
            };
            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Ender Magnolia launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Ender Magnolia:\n" + e.Message);
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

    void CreateVersionFile(string apUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);

            string versionFileName = "Ender Magnolia Archipelago Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== ENDER MAGNOLIA ARCHIPELAGO ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string win64Path = GetWin64Path();
            string versionPath = Path.Combine(win64Path, versionFileName);
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
            string win64Path = GetWin64Path();

            if (!Directory.Exists(win64Path))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Ender Magnolia Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(win64Path);
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

    string ExtractVersionFromUrl(string url)
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