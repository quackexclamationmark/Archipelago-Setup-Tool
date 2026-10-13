using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FTLManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("FTL FILES")]
    public FileDownloader.FileData ftlApworld;
    public FileDownloader.FileData ftlPreloaded;
    public FileDownloader.FileData ftlAP;
    public FileDownloader.FileData ftlHyperspace;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "FTL Faster Than Light";
    public string epicGameFolderName = "FasterThanLight";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installPreloadedToggle;
    public Toggle installAPModToggle;
    public Toggle installHyperspaceToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private const string GameExeName = "FTLGame.exe";
    private const string GameExeBackupName = "FTLGame_orig.exe";
    private const string GameDatName = "ftl.dat";
    private const string GameDatBackupName = "ftl_orig.dat";
    private const string FtlManDirName = "ftlman";
    private const string ApModFileName = "ArchipelagoFTL.ftl";
    private const string SteamAppId = "212680";

    private Process ftlProcess;
    private string ftlPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private FTLConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastDownloadSuccess = false;
    private bool lastSingleFileSuccess = false;

    [System.Serializable]
    public class FTLConfig
    {
        public string ftlApworld;
        public string ftlPreloaded;
        public string ftlAP;
        public string ftlHyperspace;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        StartCoroutine(LoadRemoteConfig());

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installPreloadedToggle != null)
            installPreloadedToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (installHyperspaceToggle != null)
            installHyperspaceToggle.isOn = true;
    }

    // =====================================================================
    // PLATFORM SELECTION
    // =====================================================================

    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        ftlPath = GetFTLPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + ftlPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        ftlPath = GetFTLPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + ftlPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(ftlPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void ApplyFTLConfig()
    {
        if (remoteConfig == null)
            return;

        ftlApworld.url = remoteConfig.ftlApworld;
        ftlPreloaded.url = remoteConfig.ftlPreloaded;
        ftlAP.url = remoteConfig.ftlAP;
        ftlHyperspace.url = remoteConfig.ftlHyperspace;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
    }

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

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
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    // =====================================================================
    // SETUP
    // =====================================================================

    private void ExecuteSetup()
    {
        ftlPath = GetFTLPath();

        bool preloaded = installPreloadedToggle != null && installPreloadedToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool hyperspace = installHyperspaceToggle != null && installHyperspaceToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        bool needsGamePath = preloaded || apmod || hyperspace;

        if (!needsGamePath && !apworld)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (needsGamePath && (string.IsNullOrEmpty(ftlPath) || !Directory.Exists(ftlPath)))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check your installation.");
            return;
        }

        if (apworld && !needsGamePath)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            LaunchFTL();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        bool preloaded = installPreloadedToggle != null && installPreloadedToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool hyperspace = installHyperspaceToggle != null && installHyperspaceToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        CleanupProcesses();

        // 1) Before downloading anything: back up the original game files
        if (preloaded)
        {
            ShowInfo("Backing up original game files...");

            try
            {
                BackupFile(Path.Combine(ftlPath, GameDatName), Path.Combine(ftlPath, GameDatBackupName));
                BackupFile(Path.Combine(ftlPath, GameExeName), Path.Combine(ftlPath, GameExeBackupName));
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to back up original files: " + e.Message);
                ShowInfo("ERROR: Failed to rename original game files.\n" + e.Message);
                yield break;
            }
        }

        // 2) Preloaded (zip -> content of the inner sub-folder goes into the game folder)
        if (preloaded)
        {
            ShowInfo("Installing Preloaded files...");

            string extractPath = Path.Combine(Application.persistentDataPath, "FTLPreloadedTemp");
            SafeDeleteDirectory(extractPath);

            yield return downloader.DownloadAndExtract(ftlPreloaded, Application.persistentDataPath, extractPath);

            if (!Directory.Exists(extractPath))
            {
                ShowInfo("ERROR: Preloaded download failed!");
                yield break;
            }

            string[] subDirs = Directory.GetDirectories(extractPath);
            string sourceDir = subDirs.Length == 1 ? subDirs[0] : extractPath;

            UnityEngine.Debug.Log("Preloaded source folder: " + sourceDir);

            MoveDirectory(sourceDir, ftlPath);
            SafeDeleteDirectory(extractPath);

            UnityEngine.Debug.Log("Preloaded installed successfully!");
        }

        // 3) AP mod (single file -> ftlman\mods)
        if (apmod)
        {
            ShowInfo("Installing AP mod...");

            string modsDir = Path.Combine(ftlPath, FtlManDirName, "mods");
            yield return InstallSingleFile(ftlAP, "ftl.dat", modsDir);

            if (!lastSingleFileSuccess)
            {
                ShowInfo("ERROR: AP mod download failed!");
                yield break;
            }

            CreateVersionFile(ftlAP.url);
        }

        // 4) Hyperspace (single file -> game folder)
        if (hyperspace)
        {
            ShowInfo("Installing Hyperspace...");

            yield return InstallSingleFile(ftlHyperspace, "Hyperspace.dll", ftlPath);

            if (!lastSingleFileSuccess)
            {
                ShowInfo("ERROR: Hyperspace download failed!");
                yield break;
            }
        }

        // 5) APWorld
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        // 6) Config files (after everything is downloaded)
        try
        {
            WriteSettingsJson();

            if (apmod)
                WriteModOrderJson();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to write FTL Manager config: " + e.Message);
            ShowInfo("ERROR: Failed to write config files.\n" + e.Message);
            yield break;
        }

        // 7) Launch
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching FTL...");
            LaunchFTL();

            yield return new WaitForSeconds(2f);

            CloseFTL();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchFTL();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    void BackupFile(string source, string backup)
    {
        // If a backup already exists, the current file is a previously installed modded one: never overwrite the real original.
        if (File.Exists(backup))
        {
            UnityEngine.Debug.Log("Backup already exists, skipping: " + backup);
            return;
        }

        if (!File.Exists(source))
        {
            UnityEngine.Debug.LogWarning("File to back up not found: " + source);
            return;
        }

        File.Move(source, backup);
        UnityEngine.Debug.Log("Renamed " + source + " -> " + backup);
    }

    IEnumerator InstallSingleFile(FileDownloader.FileData data, string defaultName, string targetDir)
    {
        lastSingleFileSuccess = false;

        if (string.IsNullOrEmpty(data.url))
        {
            UnityEngine.Debug.LogError("URL not set for " + defaultName);
            yield break;
        }

        string fileName = data.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = GetFileNameFromUrl(data.url, defaultName);

        string tempDir = Path.Combine(Application.persistentDataPath, "FTLSingleTemp");
        SafeDeleteDirectory(tempDir);
        Directory.CreateDirectory(tempDir);

        string tempFile = Path.Combine(tempDir, fileName);

        yield return DownloadFile(data.url, tempFile);

        if (!lastDownloadSuccess || !File.Exists(tempFile))
        {
            SafeDeleteDirectory(tempDir);
            yield break;
        }

        try
        {
            Directory.CreateDirectory(targetDir);
            string target = Path.Combine(targetDir, fileName);

            File.Copy(tempFile, target, true);
            UnityEngine.Debug.Log("Installed " + fileName + " to " + target);
            lastSingleFileSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy " + fileName + ": " + e.Message);
        }

        SafeDeleteDirectory(tempDir);
    }

    string GetFileNameFromUrl(string url, string fallback)
    {
        try
        {
            string name = url.Substring(url.LastIndexOf('/') + 1);

            if (name.Contains("?"))
                name = name.Substring(0, name.IndexOf("?"));

            if (!string.IsNullOrEmpty(name))
                return name;
        }
        catch { }

        return fallback;
    }

    // =====================================================================
    // FTL MANAGER CONFIG FILES
    // =====================================================================

    string EscapeJsonString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    string BuildSettingsJson(string gameDir)
    {
        return "{\"mod_directory\":\"mods\",\"ftl_directory\":\"" + EscapeJsonString(gameDir) +
               "\",\"dirs_are_mods\":true,\"zips_are_mods\":true,\"ftl_is_zip\":true,\"repack_ftl_data\":true," +
               "\"disable_hs_installer\":false,\"autoupdate\":true,\"warn_about_missing_hyperspace\":true," +
               "\"theme\":{\"colors\":\"Dark\",\"opacity\":1.0}}";
    }

    void WriteSettingsJson()
    {
        string ftlManDir = Path.Combine(ftlPath, FtlManDirName);
        Directory.CreateDirectory(ftlManDir);

        string settingsPath = Path.Combine(ftlManDir, "settings.json");
        string escapedPath = EscapeJsonString(ftlPath);
        string newField = "\"ftl_directory\":\"" + escapedPath + "\"";

        if (File.Exists(settingsPath))
        {
            string text = File.ReadAllText(settingsPath);

            Regex fieldRegex = new Regex("\"ftl_directory\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"");

            if (fieldRegex.IsMatch(text))
            {
                text = fieldRegex.Replace(text, m => newField, 1);
                File.WriteAllText(settingsPath, text);
                UnityEngine.Debug.Log("Updated ftl_directory in existing settings.json");
                return;
            }

            // Field missing: insert it right after the first '{'
            int braceIndex = text.IndexOf('{');
            if (braceIndex >= 0)
            {
                bool emptyObject = Regex.IsMatch(text.Substring(braceIndex + 1), @"^\s*\}");
                text = text.Insert(braceIndex + 1, newField + (emptyObject ? "" : ","));
                File.WriteAllText(settingsPath, text);
                UnityEngine.Debug.Log("Added ftl_directory to existing settings.json");
                return;
            }
        }

        File.WriteAllText(settingsPath, BuildSettingsJson(ftlPath));
        UnityEngine.Debug.Log("settings.json created: " + settingsPath);
    }

    void WriteModOrderJson()
    {
        string modsDir = Path.Combine(ftlPath, FtlManDirName, "mods");
        Directory.CreateDirectory(modsDir);

        string modOrderPath = Path.Combine(modsDir, "modorder.json");
        string defaultJson = "{\"hyperspace\":null,\"order\":[{\"filename\":\"" + ApModFileName + "\",\"enabled\":true}]}";
        string entry = "{\"filename\":\"" + ApModFileName + "\",\"enabled\":true}";

        if (File.Exists(modOrderPath))
        {
            string text = File.ReadAllText(modOrderPath);

            if (text.Contains("\"" + ApModFileName + "\""))
            {
                UnityEngine.Debug.Log("modorder.json already contains " + ApModFileName);
                return;
            }

            Match m = new Regex("\"order\"\\s*:\\s*\\[").Match(text);

            if (m.Success)
            {
                int insertAt = m.Index + m.Length;
                bool emptyArray = Regex.IsMatch(text.Substring(insertAt), @"^\s*\]");

                text = text.Insert(insertAt, entry + (emptyArray ? "" : ","));
                File.WriteAllText(modOrderPath, text);
                UnityEngine.Debug.Log("Added " + ApModFileName + " first in existing modorder.json");
                return;
            }
        }

        File.WriteAllText(modOrderPath, defaultJson);
        UnityEngine.Debug.Log("modorder.json created: " + modOrderPath);
    }

    // =====================================================================
    // REVERT
    // =====================================================================

    private void ExecuteRevert()
    {
        ftlPath = GetFTLPath();

        if (string.IsNullOrEmpty(ftlPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check your installation.");
            return;
        }

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteFileSync(Path.Combine(ftlPath, FtlManDirName, "mods", ApModFileName));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(Path.Combine(ftlPath, FtlManDirName, "mods"));

        if (fullClear &&
            hasOtherMods &&
            !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully clear FTL?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        if (fullClear)
            StartCoroutine(FullClearRoutine());
    }

    IEnumerator FullClearRoutine()
    {
        ShowInfo("Clearing FTL...");

        string[] filesToDelete = new string[]
        {
            "FTL_HS.log",
            "zhl.log",
            GameDatName,
            "settings.ini",
            "FTL_HS.log.bak",
            "zhl.log.bak",
            GameExeName,
            "xinput1_4.dll"
        };

        foreach (string file in filesToDelete)
            yield return DeleteFileForce(Path.Combine(ftlPath, file));

        SafeDeleteDirectory(Path.Combine(ftlPath, FtlManDirName));
        DeleteOldVersionFiles();

        // Restore the original files
        try
        {
            RestoreFile(Path.Combine(ftlPath, GameExeBackupName), Path.Combine(ftlPath, GameExeName));
            RestoreFile(Path.Combine(ftlPath, GameDatBackupName), Path.Combine(ftlPath, GameDatName));
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to restore original files: " + e.Message);
        }

        if (!isEpic)
        {
            // Ask Steam to verify the game files
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("steam://validate/" + SteamAppId);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to start Steam validation: " + e.Message);
            }

            ShowInfo("Full clear completed!");
        }
        else
        {
            // Epic has no URI to trigger file verification
            ShowInfo("Full clear completed!\nPlease verify the game files in the Epic Games Launcher.");
        }
    }

    void RestoreFile(string backup, string original)
    {
        if (!File.Exists(backup))
        {
            UnityEngine.Debug.LogWarning("Backup not found, cannot restore: " + backup);
            return;
        }

        if (File.Exists(original))
        {
            File.SetAttributes(original, FileAttributes.Normal);
            File.Delete(original);
        }

        File.Move(backup, original);
        UnityEngine.Debug.Log("Restored " + backup + " -> " + original);
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        if (Directory.GetDirectories(modsPath).Length > 0)
            return true;

        foreach (string file in Directory.GetFiles(modsPath))
        {
            string fileName = Path.GetFileName(file);

            if (fileName == ApModFileName || fileName == "modorder.json")
                continue;

            return true;
        }

        return false;
    }

    // =====================================================================
    // UI
    // =====================================================================

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

    public void QuitLauncher()
    {
        Application.Quit();
    }

    // =====================================================================
    // APWORLD
    // =====================================================================

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + ftlApworld.url);

        if (string.IsNullOrEmpty(ftlApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = ftlApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = GetFileNameFromUrl(ftlApworld.url, "ftl.apworld");

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + ftlApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(ftlApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            UnityEngine.Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);
        UnityEngine.Debug.Log("Using target path: " + target);

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
            lastApWorldInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        lastDownloadSuccess = false;

        UnityEngine.Debug.Log("Starting download from: " + url);

        try
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(savePath))
                File.Delete(savePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not prepare download path: " + e.Message);
        }

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
                lastDownloadSuccess = true;
                UnityEngine.Debug.Log("Download complete! File size: " + new System.IO.FileInfo(savePath).Length + " bytes");
            }
        }
    }

    // =====================================================================
    // PROCESSES
    // =====================================================================

    void LaunchFTL()
    {
        string exePath = Path.Combine(ftlPath, GameExeName);

        try
        {
            if (File.Exists(exePath))
                ftlProcess = Process.Start(exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch FTL: " + e.Message);
        }
    }

    void CloseFTL()
    {
        try
        {
            if (ftlProcess != null && !ftlProcess.HasExited)
            {
                ftlProcess.Kill();
                ftlProcess.Dispose();
                ftlProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseFTL();

        try
        {
            foreach (Process p in Process.GetProcessesByName("FTLGame"))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
    }

    // =====================================================================
    // FILE HELPERS
    // =====================================================================

    void SafeDeleteFileSync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch { }
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        try
        {
            Directory.Delete(source, true);
        }
        catch { }
    }

    // =====================================================================
    // VERSION FILES (game root only)
    // =====================================================================

    void CreateVersionFile(string apUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "FTL AP Version " + apVersion + ".txt";
            string content = "FTL AP Setup Tool\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(ftlPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);
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
            Regex pattern = new Regex(@"FTL AP Version .+\.txt");

            if (string.IsNullOrEmpty(ftlPath) || !Directory.Exists(ftlPath))
                return;

            foreach (string file in Directory.GetFiles(ftlPath))
            {
                string fileName = Path.GetFileName(file);

                if (!pattern.IsMatch(fileName))
                    continue;

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
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        try
        {
            Regex regex = new Regex(pattern);
            Match match = regex.Match(url);

            if (match.Success)
                return match.Groups[1].Value;
        }
        catch { }

        if (!string.IsNullOrEmpty(url))
        {
            int idx = url.LastIndexOf('/');
            if (idx >= 0 && idx + 1 < url.Length)
                return url.Substring(idx + 1);
        }

        return "Unknown";
    }

    // =====================================================================
    // PATHS / REMOTE CONFIG
    // =====================================================================

    string GetFTLPath()
    {
        return isEpic ? GetFTLEpicPath() : GetFTLSteamPath();
    }

    string GetFTLSteamPath()
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

    string GetFTLEpicPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(@"C:\Program Files\Epic Games", epicGameFolderName),
            Path.Combine(@"C:\Games\Epic", epicGameFolderName),
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Epic Games Launcher manifests
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                foreach (string manifest in Directory.GetFiles(epicBaseDir, "*.item"))
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);

                        if (!content.Contains(epicGameFolderName))
                            continue;

                        Match match = Regex.Match(content, "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");

                        if (match.Success)
                        {
                            string epicPath = match.Groups[1].Value.Replace("\\\\", "\\");

                            if (Directory.Exists(epicPath))
                            {
                                UnityEngine.Debug.Log("Found Game (Epic) at: " + epicPath);
                                return epicPath;
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        if (remoteConfig != null && remoteConfig.epicSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.epicSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string epicPath = Path.Combine(drive.Name, relativePath, epicGameFolderName);
                            if (Directory.Exists(epicPath))
                            {
                                UnityEngine.Debug.Log("Found Game (Epic, via remote config) at: " + epicPath);
                                return epicPath;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
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
            remoteConfig = JsonUtility.FromJson<FTLConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyFTLConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        ftlPath = GetFTLPath();
        UpdatePlatformStatus();
    }

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.apSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, "custom_worlds");
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Archipelago custom_worlds (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Archipelago custom_worlds directory not found.");
        return "";
    }
}