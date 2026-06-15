using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

public class UGGManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("UNTITLED GOOSE GAME FILES")]
    public FileDownloader.FileData untitledApworld;
    public FileDownloader.FileData untitledBepInEx;
    public FileDownloader.FileData untitledAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanBepInExToggle;
    public Toggle removeAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process uggProcess;
    private string uggPath;
    private string pendingAction;
    private UGGConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class UGGConfig
    {
        public string untitledApworld;
        public string untitledBepInEx;
        public string untitledAP;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        uggPath = GetUGGPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked()
    {
        SelectSteam();
    }

    void OnEpicButtonClicked()
    {
        SelectEpic();
    }

    void SelectSteam()
    {
        isEpic = false;
        uggPath = GetUGGPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + uggPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        uggPath = GetUGGPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + uggPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(uggPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyUGGConfig()
    {
        if (remoteConfig == null)
            return;

        untitledApworld.url = remoteConfig.untitledApworld;
        untitledApworld.fileName = "untitled_goose_game.apworld";

        untitledBepInEx.url = remoteConfig.untitledBepInEx;
        untitledBepInEx.fileName = "BepInEx_x64_5.4.9.0.zip";

        untitledAP.url = remoteConfig.untitledAP;
        untitledAP.fileName = "UGG-Archipelago.zip";
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(uggPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Untitled Goose Game not found in " + platform + ". Please check installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        uggPath = GetUGGPath();

        if (string.IsNullOrEmpty(uggPath))
            return;

        string pluginsPath = Path.Combine(uggPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(pluginsPath, "UGG-Archipelago"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // Full clean
        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(uggPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(uggPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(uggPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(uggPath, ".doorstop_version"));
        SafeDeleteFile(Path.Combine(uggPath, "changelog.txt"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle != null && installApworldToggle.isOn)
        {
            ShowInfo("Installing .apworld...");
            yield return InstallApworld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing Untitled Goose Game Archipelago Mod...");
            yield return InstallUGGAP();
        }

        CreateVersionFile(untitledApworld.url, untitledBepInEx.url, untitledAP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Untitled Goose Game...");
            LaunchUGG();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
            yield return null;

        string apworldPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "worlds");
        Directory.CreateDirectory(apworldPath);

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "ApworldTemp");
        yield return downloader.DownloadToFolder(untitledApworld, tempDownloadPath);

        string[] apworldFiles = Directory.GetFiles(tempDownloadPath, "*.apworld");

        if (apworldFiles.Length > 0)
        {
            string targetPath = Path.Combine(apworldPath, Path.GetFileName(apworldFiles[0]));
            File.Copy(apworldFiles[0], targetPath, true);
            UnityEngine.Debug.Log("Copied .apworld to Archipelago worlds: " + targetPath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("untitled_goose_game.apworld not found in download");
        }

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(untitledBepInEx, Application.persistentDataPath, extractPath);

        // Copy all contents from extractPath to uggPath
        CopyDirectory(extractPath, uggPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallUGGAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "UGGAPTemp");
        yield return downloader.DownloadAndExtract(untitledAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(uggPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Le nom du dossier à créer dans plugins sera le même que le nom du fichier zip (sans extension)
        // Par défaut "UGG-Archipelago"
        string modFolderName = Path.GetFileNameWithoutExtension(untitledAP.fileName);
        string modTargetPath = Path.Combine(pluginsPath, modFolderName);

        // Supprimer l'ancien dossier s'il existe
        if (Directory.Exists(modTargetPath))
            SafeDeleteDirectory(modTargetPath);

        // Créer le nouveau dossier
        Directory.CreateDirectory(modTargetPath);

        // Copier TOUT le contenu de l'extraction vers le nouveau dossier
        CopyDirectory(extractPath, modTargetPath);

        UnityEngine.Debug.Log("Copied all contents to " + modTargetPath);

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<UGGConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyUGGConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchUGG()
    {
        string exePath = Path.Combine(uggPath, "Untitled.exe");

        if (File.Exists(exePath))
        {
            try
            {
                uggProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Untitled Goose Game launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Untitled Goose Game:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseUGG()
    {
        try
        {
            if (uggProcess != null && !uggProcess.HasExited)
            {
                uggProcess.Kill();
                uggProcess.Dispose();
                uggProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseUGG();
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
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        try
        {
            CopyDirectory(source, target);
            SafeDeleteDirectory(source);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error moving directory: " + e.Message);
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

    string GetUGGPath()
    {
        if (isEpic)
            return GetUGGEpicPath();
        else
            return GetUGGSteamPath();
    }

    string GetUGGSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Untitled Goose Game"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Untitled Goose Game"),
            @"D:\Steam\steamapps\common\Untitled Goose Game",
            @"D:\SteamLibrary\steamapps\common\Untitled Goose Game",
            @"E:\Steam\steamapps\common\Untitled Goose Game",
            @"E:\SteamLibrary\steamapps\common\Untitled Goose Game",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Untitled Goose Game (Steam) at: " + path);
                    return path;
                }
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
                    string uggPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Untitled Goose Game");
                    if (Directory.Exists(uggPath))
                    {
                        UnityEngine.Debug.Log("Found Untitled Goose Game (Steam) at: " + uggPath);
                        return uggPath;
                    }

                    uggPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Untitled Goose Game");
                    if (Directory.Exists(uggPath))
                    {
                        UnityEngine.Debug.Log("Found Untitled Goose Game (Steam) at: " + uggPath);
                        return uggPath;
                    }

                    uggPath = Path.Combine(drive.Name, "steamapps", "common", "Untitled Goose Game");
                    if (Directory.Exists(uggPath))
                    {
                        UnityEngine.Debug.Log("Found Untitled Goose Game (Steam) at: " + uggPath);
                        return uggPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Untitled Goose Game (Steam) not found.");
        return "";
    }

    string GetUGGEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Untitled Goose Game",
            @"D:\Epic Games\Untitled Goose Game",
            @"E:\Epic Games\Untitled Goose Game",
            @"C:\Games\Epic\Untitled Goose Game",
            @"D:\Games\Epic\Untitled Goose Game",
            @"E:\Games\Epic\Untitled Goose Game",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Untitled Goose Game (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Cherche dans Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Cherche le manifest pour Untitled Goose Game
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Untitled Goose Game"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Untitled Goose Game (Epic) at: " + epicPath);
                                    return epicPath;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // Scan all drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Untitled Goose Game");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Untitled Goose Game (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Untitled Goose Game");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Untitled Goose Game (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Untitled Goose Game (Epic) not found.");
        return "";
    }

    void CreateVersionFile(string apworldUrl, string bepinexUrl, string uggapUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string uggapVersion = ExtractVersionFromUrl(uggapUrl);

            string versionFileName = "UGG Archipelago Version " + uggapVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== UNTITLED GOOSE GAME ARCHIPELAGO ===\n";
            content += "Downloaded from: " + uggapUrl + "\n";
            content += "Version: " + uggapVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(uggPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(uggPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"UGG Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(uggPath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (pattern.IsMatch(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted old version file in root: " + fileName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message);
                    }
                }
            }

            string pluginsPath = Path.Combine(uggPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string[] pluginsFiles = Directory.GetFiles(pluginsPath);
                foreach (string file in pluginsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in plugins: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in plugins: " + e.Message);
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