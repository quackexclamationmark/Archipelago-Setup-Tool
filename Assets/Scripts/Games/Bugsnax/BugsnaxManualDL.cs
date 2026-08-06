using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BugsnaxManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("BUGSNAX FILES")]
    public FileDownloader.FileData bugsnaxApworld; // bugsnax.apworld
    public FileDownloader.FileData bugsnaxAP;       // BugsnaxMod.zip (copied as-is, no extraction)

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Bugsnax";
    public string epicGameFolderName = "Bugsnax";

    [Header("FEATURE TOGGLES")]
    public Toggle installBugsnaxApworldToggle;
    public Toggle installBugsnaxAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private const string SaveFileName = "Bugsnax2.save";
    private const string ModZipFileName = "BugsnaxMod.zip";
    private const string SaveCopyFolderName = "SaveCopy";
    private const string APSaveFolderName = "APSave";

    private Process bugsnaxProcess;
    private bool bugsnaxLaunchedAsHelper = false;
    private string bugsnaxPath;
    private string pendingAction;
    private BugsnaxConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class BugsnaxConfig
    {
        public string bugsnaxAP;
        public string bugsnaxApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
    }

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        bugsnaxPath = GetBugsnaxPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked() => SelectSteam();
    void OnEpicButtonClicked() => SelectEpic();

    void SelectSteam()
    {
        isEpic = false;
        bugsnaxPath = GetBugsnaxPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + bugsnaxPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        bugsnaxPath = GetBugsnaxPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + bugsnaxPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(bugsnaxPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyBugsnaxConfig()
    {
        if (remoteConfig == null) return;
        bugsnaxAP.url = remoteConfig.bugsnaxAP;
        bugsnaxApworld.url = remoteConfig.bugsnaxApworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    public void RevertAll() => ShowConfirmation("Are you sure you want to revert?", "Revert");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        bugsnaxPath = GetBugsnaxPath();

        bool apworld = installBugsnaxApworldToggle == null || installBugsnaxApworldToggle.isOn;
        bool apmod = installBugsnaxAPToggle != null && installBugsnaxAPToggle.isOn;

        bool needsGamePath = apmod;

        if (needsGamePath && string.IsNullOrEmpty(bugsnaxPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (apmod ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (apmod && count == 1) { StartCoroutine(APModOnlyFlow()); return; }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallAPWorld();

        bool savedGameFound = false;
        yield return InstallAPMod((found) => savedGameFound = found);

        FinishInstall(savedGameFound);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(2f);
            LaunchBugsnax(false);
        }
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing Bugsnax APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            LaunchBugsnax(false);
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator APModOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing Bugsnax AP Mod...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        bool savedGameFound = false;
        yield return InstallAPMod((found) => savedGameFound = found);

        FinishInstall(savedGameFound);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(1f);
            LaunchBugsnax(false);
        }
    }

    void FinishInstall(bool savedGameFound)
    {
        if (savedGameFound)
        {
            ShowInfo("Installation complete! A save slot 2 save was found, it was saved inside \"Bugsnax\\SaveCopy\".");
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(bugsnaxApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = bugsnaxApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = bugsnaxApworld.url.Substring(bugsnaxApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + bugsnaxApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(bugsnaxApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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

        if (File.Exists(target))
        {
            try { File.Delete(target); } catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("APWorld file copied to: " + target);
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

    // Downloads BugsnaxMod.zip and places it directly (no extraction) inside the game directory.
    // Also backs up an existing "Bugsnax2.save" (slot 2 save) into "<GameDir>\SaveCopy" if one is found.
    IEnumerator InstallAPMod(System.Action<bool> onSavedGameResult)
    {
        while (!configLoaded)
            yield return null;

        bool savedGameFound = false;

        // --- Save slot 2 backup check ---
        try
        {
            string savedGamesDir = GetSavedGamesBugsnaxPath();
            string existingSavePath = Path.Combine(savedGamesDir, SaveFileName);

            if (File.Exists(existingSavePath))
            {
                string saveCopyDir = Path.Combine(bugsnaxPath, SaveCopyFolderName);
                Directory.CreateDirectory(saveCopyDir);
                string backupPath = Path.Combine(saveCopyDir, SaveFileName);
                File.Copy(existingSavePath, backupPath, true);

                File.SetAttributes(existingSavePath, FileAttributes.Normal);
                File.Delete(existingSavePath);

                savedGameFound = true;
                UnityEngine.Debug.Log("Backed up existing save slot 2 to: " + backupPath + " and removed original from Saved Games.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Save backup check failed: " + e.Message);
        }

        // --- Download and place BugsnaxMod.zip ---
        if (string.IsNullOrEmpty(bugsnaxAP.url))
        {
            ShowInfo("ERROR: Bugsnax AP Mod URL is empty!");
            UnityEngine.Debug.LogError("Bugsnax AP Mod URL not set!");
            onSavedGameResult?.Invoke(savedGameFound);
            yield break;
        }

        string fileName = bugsnaxAP.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = bugsnaxAP.url.Substring(bugsnaxAP.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }
        if (string.IsNullOrEmpty(fileName)) fileName = ModZipFileName;

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Bugsnax AP Mod from: " + bugsnaxAP.url);
        yield return DownloadFile(bugsnaxAP.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Bugsnax AP Mod download failed!");
            onSavedGameResult?.Invoke(savedGameFound);
            yield break;
        }

        try
        {
            Directory.CreateDirectory(bugsnaxPath);
            string target = Path.Combine(bugsnaxPath, ModZipFileName);
            if (File.Exists(target))
            {
                try { File.Delete(target); } catch { }
            }
            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("Bugsnax AP Mod zip copied to: " + target);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Bugsnax AP Mod zip: " + e.Message);
            ShowInfo("ERROR: Failed to install Bugsnax AP Mod\n" + e.Message);
            onSavedGameResult?.Invoke(savedGameFound);
            yield break;
        }

        try
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary Bugsnax AP Mod file: " + e.Message);
        }

        onSavedGameResult?.Invoke(savedGameFound);
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! " + savePath);
            }
        }
    }

    // =========================================================
    // REVERT
    // =========================================================
    // 1) Deletes BugsnaxMod.zip from the game directory.
    // 2) If a slot 2 save was backed up during setup (SaveCopy\Bugsnax2.save exists):
    //    - If a NEW Bugsnax2.save currently exists in Saved Games (the AP playthrough save),
    //      it is moved into SaveCopy (overwriting the backed-up original there), and the
    //      SaveCopy folder is renamed to "APSave".
    //    - The original backed-up save is then restored to Saved Games\Bugsnax.

    private void ExecuteRevert()
    {
        bugsnaxPath = GetBugsnaxPath();
        if (string.IsNullOrEmpty(bugsnaxPath))
        {
            ShowInfo("Game not found, cannot revert.");
            return;
        }

        CleanupProcesses();

        ShowInfo("Removing Bugsnax AP mod...");

        // 1) Delete the mod zip
        try
        {
            string zipPath = Path.Combine(bugsnaxPath, ModZipFileName);
            if (File.Exists(zipPath))
            {
                File.SetAttributes(zipPath, FileAttributes.Normal);
                File.Delete(zipPath);
                UnityEngine.Debug.Log("Deleted: " + zipPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete Bugsnax AP Mod zip: " + e.Message);
        }

        // 2) Restore the backed-up save, if one exists
        try
        {
            string saveCopyDir = Path.Combine(bugsnaxPath, SaveCopyFolderName);
            string originalSavePath = Path.Combine(saveCopyDir, SaveFileName);

            if (File.Exists(originalSavePath))
            {
                string savedGamesDir = GetSavedGamesBugsnaxPath();
                Directory.CreateDirectory(savedGamesDir);
                string currentSavePath = Path.Combine(savedGamesDir, SaveFileName);

                // Keep the original bytes safe before we potentially overwrite SaveCopy
                byte[] originalBytes = File.ReadAllBytes(originalSavePath);

                if (File.Exists(currentSavePath))
                {
                    // A new (AP) save exists in Saved Games - preserve it into SaveCopy, then rename to APSave
                    File.Copy(currentSavePath, originalSavePath, true);

                    string apSaveDir = Path.Combine(bugsnaxPath, APSaveFolderName);
                    if (Directory.Exists(apSaveDir))
                        SafeDeleteDirectory(apSaveDir);
                    Directory.Move(saveCopyDir, apSaveDir);

                    UnityEngine.Debug.Log("New save found - preserved to: " + apSaveDir);
                }

                // Restore the original save back into Saved Games\Bugsnax
                File.WriteAllBytes(currentSavePath, originalBytes);
                UnityEngine.Debug.Log("Original save restored to: " + currentSavePath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Save restore failed: " + e.Message);
        }

        ShowInfo("Revert completed!");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    void LaunchBugsnax(bool asHelper = false)
    {
        string exePath = Path.Combine(bugsnaxPath, "Bugsnax.exe");
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    WorkingDirectory = bugsnaxPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                bugsnaxProcess = Process.Start(psi);
                bugsnaxLaunchedAsHelper = asHelper;
                UnityEngine.Debug.Log($"Bugsnax launched for {(asHelper ? "config generation (helper)" : "user/interactive launch")}.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Bugsnax executable not found: " + exePath);
        }
    }

    void CloseBugsnax(bool force = false)
    {
        try
        {
            if (bugsnaxProcess != null && !bugsnaxProcess.HasExited)
            {
                if (force || bugsnaxLaunchedAsHelper)
                {
                    bugsnaxProcess.Kill();
                    bugsnaxProcess.Dispose();
                    bugsnaxProcess = null;
                    bugsnaxLaunchedAsHelper = false;
                    UnityEngine.Debug.Log("Bugsnax process closed.");
                }
                else
                {
                    UnityEngine.Debug.Log("CloseBugsnax: process started interactively, not killing it.");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("CloseBugsnax error: " + ex.Message);
        }
    }

    void CleanupProcesses() => CloseBugsnax(true);

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

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
            remoteConfig = JsonUtility.FromJson<BugsnaxConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyBugsnaxConfig();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        bugsnaxPath = GetBugsnaxPath();
        UpdatePlatformStatus();
    }

    string GetBugsnaxPath()
    {
        if (isEpic)
            return GetBugsnaxEpicPath();
        else
            return GetBugsnaxSteamPath();
    }

    string GetBugsnaxSteamPath()
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

    string GetBugsnaxEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Bugsnax",
            @"C:\Games\Epic\Bugsnax",
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

        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Bugsnax"))
                        {
                            Regex regex = new Regex(@"""InstallLocation"":""([^""]+)""");
                            Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Game (Epic) at: " + epicPath);
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


    string GetSavedGamesBugsnaxPath()
    {
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Saved Games", "Bugsnax"
        );
    }
}