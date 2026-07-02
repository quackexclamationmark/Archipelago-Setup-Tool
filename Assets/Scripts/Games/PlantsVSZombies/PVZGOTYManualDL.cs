using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class PVZGOTYManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData pvzgotyAPWorld;
    public FileDownloader.FileData pvzgotyAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
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
        public string pvzgotyApworld;
        public string pvzgotyAP;
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

        pvzgotyAPWorld.url = remoteConfig.pvzgotyApworld;
        pvzgotyAP.url = remoteConfig.pvzgotyAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install PVZ GOTY Archipelago?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove PVZ GOTY Archipelago?", "Revert");
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
        bool ap = installAPToggle == null || installAPToggle.isOn;

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0);

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

    private void ExecuteRevert()
    {
        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting PVZ GOTY Archipelago...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string archipelagoPath = Path.Combine(gamePath, "Archipelago");

            if (Directory.Exists(archipelagoPath))
            {
                try
                {
                    Directory.Delete(archipelagoPath, true);
                    UnityEngine.Debug.Log("Deleted Archipelago folder: " + archipelagoPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete Archipelago folder: " + e.Message);
                    ShowInfo("Error during revert:\n" + e.Message);
                    yield break;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Archipelago folder not found at: " + archipelagoPath);
                ShowInfo("Archipelago folder not found.");
                yield break;
            }

            DeleteOldVersionFiles();

            ShowInfo("PVZ GOTY Archipelago removed successfully!");
            UnityEngine.Debug.Log("Revert complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
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

    IEnumerator APOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPFiles();

        CreateVersionFile(pvzgotyAP.url, pvzgotyAPWorld.url);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installAPToggle == null || installAPToggle.isOn)
            yield return InstallAPFiles();

        CreateVersionFile(pvzgotyAP.url, pvzgotyAPWorld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching PVZ GOTY...");
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

        UnityEngine.Debug.Log("Config loaded. PVZGOTY AP URL: " + pvzgotyAP.url);

        if (string.IsNullOrEmpty(pvzgotyAP.url))
        {
            ShowInfo("ERROR: PVZGOTY AP URL is empty!");
            UnityEngine.Debug.LogError("PVZGOTY AP URL not set!");
            yield break;
        }

        string archipelagoPath = Path.Combine(gamePath, "Archipelago");
        bool isExistingInstallation = false;

        if (Directory.Exists(archipelagoPath))
        {
            string exePath = Path.Combine(archipelagoPath, "PlantsVsZombies.exe");
            if (File.Exists(exePath))
            {
                UnityEngine.Debug.Log("Archipelago folder already exists with launcher. Skipping extraction.");
                isExistingInstallation = true;
            }
        }

        if (!isExistingInstallation)
        {
            if (!Directory.Exists(archipelagoPath))
            {
                Directory.CreateDirectory(archipelagoPath);
                UnityEngine.Debug.Log("Created Archipelago folder: " + archipelagoPath);
            }

            string fileName = pvzgotyAP.fileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = pvzgotyAP.url.Substring(pvzgotyAP.url.LastIndexOf('/') + 1);

                if (fileName.Contains("?"))
                    fileName = fileName.Substring(0, fileName.IndexOf("?"));

                UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
            }

            string localPath = Path.Combine(Application.persistentDataPath, fileName);
            string extractPath = Path.Combine(Application.persistentDataPath, "PVZGOTYAPTemp");

            UnityEngine.Debug.Log("Downloading PVZGOTY AP from: " + pvzgotyAP.url);
            UnityEngine.Debug.Log("Saving to: " + localPath);

            yield return DownloadFile(pvzgotyAP.url, localPath);

            if (!File.Exists(localPath))
            {
                UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
                ShowInfo("ERROR: PVZGOTY AP download failed!");
                yield break;
            }

            UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

            yield return downloader.DownloadAndExtract(pvzgotyAP, Application.persistentDataPath, extractPath);

            string artifactPath = Path.Combine(extractPath, "artifact-output");
            string sourcePath = artifactPath;

            if (!Directory.Exists(artifactPath))
            {
                UnityEngine.Debug.LogWarning("artifact-output folder not found, using root extraction path");
                sourcePath = extractPath;
            }

            if (Directory.Exists(sourcePath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(sourcePath.Length + 1);
                        string targetFile = Path.Combine(archipelagoPath, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                        File.Copy(file, targetFile, true);

                        UnityEngine.Debug.Log("Copied: " + targetFile);
                    }

                    SafeDeleteDirectory(extractPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("Error extracting files: " + e.Message);
                    ShowInfo("ERROR: Failed to extract PVZGOTY AP files\n" + e.Message);
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

        ShowInfo("PVZGOTY Archipelago installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + pvzgotyAPWorld.url);

        if (string.IsNullOrEmpty(pvzgotyAPWorld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pvzgotyAPWorld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = pvzgotyAPWorld.url.Substring(pvzgotyAPWorld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pvzgotyAPWorld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pvzgotyAPWorld.url, localPath);

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
        string archipelagoPath = Path.Combine(gamePath, "Archipelago");
        string exePath = Path.Combine(archipelagoPath, "PlantsVsZombies.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("PlantsVsZombies.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("PlantsVsZombies.exe not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = archipelagoPath,
                UseShellExecute = true
            };
            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("PVZ GOTY launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching PVZ GOTY:\n" + e.Message);
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to install PVZ GOTY Archipelago?", "Setup");
    }

    string GetGamePath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Plants Vs Zombies"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Plants Vs Zombies"),
            @"D:\Steam\steamapps\common\Plants Vs Zombies",
            @"D:\SteamLibrary\steamapps\common\Plants Vs Zombies",
            @"D:\steamapps\common\Plants Vs Zombies",
            @"E:\Steam\steamapps\common\Plants Vs Zombies",
            @"E:\SteamLibrary\steamapps\common\Plants Vs Zombies",
            @"E:\steamapps\common\Plants Vs Zombies",
            @"E:\Program Files (x86)\steamapps\common\Plants Vs Zombies",
            @"E:\Program Files\steamapps\common\Plants Vs Zombies",
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
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Plants Vs Zombies");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Plants Vs Zombies");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "steamapps", "common", "Plants Vs Zombies");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Plants Vs Zombies");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Plants Vs Zombies");
                    if (Directory.Exists(gamePath))
                        return gamePath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    void CreateVersionFile(string pvzgotyApUrl, string apworldUrl)
    {
        try
        {
            string pvzgotyApVersion = ExtractVersionFromUrl(pvzgotyApUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "PVZ GOTY Archipelago Version " + pvzgotyApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== PVZ GOTY ARCHIPELAGO ===\n";
            content += "Downloaded from: " + pvzgotyApUrl + "\n";
            content += "Version: " + pvzgotyApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string archipelagoPath = Path.Combine(gamePath, "Archipelago");
            string versionPath = Path.Combine(archipelagoPath, versionFileName);
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
            string archipelagoPath = Path.Combine(gamePath, "Archipelago");

            if (!Directory.Exists(archipelagoPath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"PVZ GOTY Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(archipelagoPath);
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
