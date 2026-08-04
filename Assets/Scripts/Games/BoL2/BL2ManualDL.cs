using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class BL2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData borderlands2Apworld;
    public FileDownloader.FileData borderlands2ModManager;
    public FileDownloader.FileData borderlands2AP;
    public FileDownloader.FileData borderlands2Coroutines;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Borderlands 2";

    [Header("FEATURE TOGGLES")]
    public Toggle installModManagerToggle;
    public Toggle installAPToggle;
    public Toggle installCoroutinesToggle;
    public Toggle installApworldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearToggle;
    public Toggle clearApModsToggle;

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
        public string borderlands2Apworld;
        public string borderlands2ModManager;
        public string borderlands2AP;
        public string borderlands2Coroutines;
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

        if (installModManagerToggle != null)
            installModManagerToggle.isOn = true;

        if (installAPToggle != null)
            installAPToggle.isOn = true;

        if (installCoroutinesToggle != null)
            installCoroutinesToggle.isOn = true;

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (fullClearToggle != null)
        {
            fullClearToggle.isOn = false;
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);
        }

        if (clearApModsToggle != null)
        {
            clearApModsToggle.isOn = true;
        }
    }

    void OnFullClearChanged(bool value)
    {
        if (clearApModsToggle != null)
        {
            if (value)
            {
                clearApModsToggle.isOn = false;
                clearApModsToggle.interactable = false;
            }
            else
            {
                clearApModsToggle.interactable = true;
            }
        }
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        borderlands2Apworld.url = remoteConfig.borderlands2Apworld;
        borderlands2ModManager.url = remoteConfig.borderlands2ModManager;
        borderlands2AP.url = remoteConfig.borderlands2AP;
        borderlands2Coroutines.url = remoteConfig.borderlands2Coroutines;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
    }

    public void ClearAPMods()
    {
        ShowConfirmation("Are you sure you want to remove AP Mods", "ClearAP");
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

        if (string.IsNullOrEmpty(pendingAction))
            return;

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;
            case "FullClear":
                ExecuteFullClear();
                break;
            case "ClearAP":
                ExecuteClearAP();
                break;
            case "Revert":
                ExecuteRevert();
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool doModManager = installModManagerToggle == null || installModManagerToggle.isOn;
        bool doAP = installAPToggle == null || installAPToggle.isOn;
        bool doCoroutines = installCoroutinesToggle == null || installCoroutinesToggle.isOn;
        bool doApworld = installApworldToggle == null || installApworldToggle.isOn;

        bool needsGamePath = doModManager || doAP || doCoroutines;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        int count = (doModManager ? 1 : 0) + (doAP ? 1 : 0) + (doCoroutines ? 1 : 0) + (doApworld ? 1 : 0);
        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(doModManager, doAP, doCoroutines, doApworld));
    }

    IEnumerator InstallFlow(bool installModManager, bool installAP, bool installCoroutines, bool installApworld)
    {
        while (!configLoaded)
        {
            yield return new WaitForSeconds(0.2f);
        }

        if (installModManager)
        {
            yield return InstallModManager();
            yield return new WaitForSeconds(0.5f);
        }

        string sdkModsPath = Path.Combine(gamePath, "sdk_mods");
        if ((installAP || installCoroutines) && !Directory.Exists(sdkModsPath))
        {
            try
            {
                Directory.CreateDirectory(sdkModsPath);
            }
            catch (System.Exception e)
            {
                ShowInfo("ERROR: Could not create sdk_mods folder:\n" + e.Message);
                yield break;
            }
        }

        if (installAP)
        {
            yield return InstallSdkMod(borderlands2AP);
            yield return new WaitForSeconds(0.2f);
        }

        if (installCoroutines)
        {
            yield return InstallSdkMod(borderlands2Coroutines);
            yield return new WaitForSeconds(0.2f);
        }

        if (installApworld)
        {
            yield return InstallApworld();
            yield return new WaitForSeconds(0.2f);
        }

        try
        {
            CreateVersionFile(borderlands2AP.url, borderlands2Apworld.url);
        }
        catch { }

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching Borderlands 2...");
            yield return new WaitForSeconds(0.5f);
            LaunchGame();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallModManager()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.2f);

        if (string.IsNullOrEmpty(borderlands2ModManager.url))
        {
            ShowInfo("ERROR: Mod Manager URL is empty!");
            UnityEngine.Debug.LogError("Mod Manager URL not set!");
            yield break;
        }

        string fileName = borderlands2ModManager.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = borderlands2ModManager.url.Substring(borderlands2ModManager.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "BL2ModManagerTemp");

        UnityEngine.Debug.Log("Downloading Mod Manager from: " + borderlands2ModManager.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(borderlands2ModManager.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Mod Manager download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("Extracting Mod Manager zip...");
        yield return downloader.DownloadAndExtract(borderlands2ModManager, Application.persistentDataPath, extractPath);

        string sourcePath = extractPath;
        if (Directory.Exists(sourcePath))
        {
            try
            {
                foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(sourcePath.Length + 1);
                    string target = Path.Combine(gamePath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                    UnityEngine.Debug.Log("Copied ModManager file: " + target);
                }

                SafeDeleteDirectory(extractPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error installing Mod Manager: " + e.Message);
                ShowInfo("ERROR: Failed to install Mod Manager\n" + e.Message);
                yield break;
            }
        }

        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        ShowInfo("Mod Manager installed successfully!");
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator InstallSdkMod(FileDownloader.FileData modFile)
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.2f);

        if (modFile == null || string.IsNullOrEmpty(modFile.url))
        {
            ShowInfo("ERROR: SDK mod URL is empty!");
            UnityEngine.Debug.LogError("SDK mod URL not set!");
            yield break;
        }

        // Determine filename
        string fileName = modFile.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = modFile.url.Substring(modFile.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string sdkModsPath = Path.Combine(gamePath, "sdk_mods");
        string targetPath = Path.Combine(sdkModsPath, fileName);

        UnityEngine.Debug.Log("Downloading SDK mod from: " + modFile.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(modFile.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: SDK mod download failed!");
            yield break;
        }

        try
        {
            if (!Directory.Exists(sdkModsPath))
                Directory.CreateDirectory(sdkModsPath);

            // overwrite if exists
            File.Copy(localPath, targetPath, true);
            UnityEngine.Debug.Log("Copied SDK mod to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy SDK mod: " + e.Message);
            ShowInfo("ERROR: Failed to install SDK mod\n" + e.Message);
            yield break;
        }

        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        ShowInfo(Path.GetFileName(targetPath) + " installed successfully!");
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.2f);

        UnityEngine.Debug.Log("Config loaded. Apworld URL: " + borderlands2Apworld.url);

        if (string.IsNullOrEmpty(borderlands2Apworld.url))
        {
            ShowInfo("ERROR: Apworld URL is empty!");
            UnityEngine.Debug.LogError("Apworld URL not set!");
            yield break;
        }

        string fileName = borderlands2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = borderlands2Apworld.url.Substring(borderlands2Apworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Apworld from: " + borderlands2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(borderlands2Apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Apworld download failed!");
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

    // Entry point used by RevertAll: decide which revert flow based on toggles
    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        bool doFullClear = fullClearToggle != null && fullClearToggle.isOn;
        bool doClearAP = clearApModsToggle != null && clearApModsToggle.isOn;

        if (!doFullClear && !doClearAP)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        CleanupProcesses();

        if (doClearAP)
        {
            StartCoroutine(ClearAPAsync());
            return;
        }

        if (doFullClear)
        {
            StartCoroutine(FullClearAsync());
            return;
        }
    }

    private void ExecuteFullClear()
    {
        // kept for direct button compatibility
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(FullClearAsync());
    }

    private void ExecuteClearAP()
    {
        // kept for direct button compatibility
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(ClearAPAsync());
    }

    IEnumerator FullClearAsync()
    {
        ShowInfo("Fully reverting...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string sdkModsPath = Path.Combine(gamePath, "sdk_mods");
            if (Directory.Exists(sdkModsPath))
            {
                try
                {
                    Directory.Delete(sdkModsPath, true);
                    UnityEngine.Debug.Log("Deleted sdk_mods folder: " + sdkModsPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete sdk_mods folder: " + e.Message);
                }
            }

            string win32Path = Path.Combine(gamePath, "Binaries", "Win32");
            string ddrawPath = Path.Combine(win32Path, "ddraw.dll");
            if (File.Exists(ddrawPath))
            {
                try
                {
                    File.Delete(ddrawPath);
                    UnityEngine.Debug.Log("Deleted ddraw.dll: " + ddrawPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete ddraw.dll: " + e.Message);
                }
            }

            string pluginsPath = Path.Combine(win32Path, "Plugins");
            if (Directory.Exists(pluginsPath))
            {
                try
                {
                    Directory.Delete(pluginsPath, true);
                    UnityEngine.Debug.Log("Deleted Plugins folder: " + pluginsPath);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to delete Plugins folder: " + e.Message);
                }
            }

            // delete version files (as implemented for BL2)
            DeleteOldVersionFiles();

            ShowInfo("Full clear complete!");
            UnityEngine.Debug.Log("Full clear complete");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during full clear:\n" + e.Message);
            UnityEngine.Debug.LogError("Full clear error: " + e);
        }
    }

    IEnumerator ClearAPAsync()
    {
        ShowInfo("Removing AP-related mods...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string sdkModsPath = Path.Combine(gamePath, "sdk_mods");

            if (Directory.Exists(sdkModsPath))
            {
                // BouncyLootGod.sdkmod
                string apFile = Path.Combine(sdkModsPath, Path.GetFileName(borderlands2AP.url ?? "BouncyLootGod.sdkmod"));
                if (File.Exists(apFile))
                {
                    try { File.Delete(apFile); UnityEngine.Debug.Log("Deleted: " + apFile); } catch (System.Exception e) { UnityEngine.Debug.LogWarning("Could not delete " + apFile + " - " + e.Message); }
                }

                // coroutines.sdkmod
                string coroFile = Path.Combine(sdkModsPath, Path.GetFileName(borderlands2Coroutines.url ?? "coroutines.sdkmod"));
                if (File.Exists(coroFile))
                {
                    try { File.Delete(coroFile); UnityEngine.Debug.Log("Deleted: " + coroFile); } catch (System.Exception e) { UnityEngine.Debug.LogWarning("Could not delete " + coroFile + " - " + e.Message); }
                }
            }

            // delete version files
            DeleteOldVersionFiles();

            ShowInfo("AP-related mods removed!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during clear AP mods:\n" + e.Message);
            UnityEngine.Debug.LogError("Clear AP error: " + e);
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

    IEnumerator LoadRemoteConfig()
    {
        // Update this URL to point to your remote config for BL2 (the user asked to use remote config similarly)
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
        string exePath = Path.Combine(gamePath, "Binaries", "Win32", "Borderlands2.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Borderlands2.exe not found at:\n" + exePath);
            UnityEngine.Debug.LogError("Borderlands2.exe not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.Combine(gamePath, "Binaries", "Win32"),
                UseShellExecute = true
            };
            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Borderlands 2 launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Borderlands 2:\n" + e.Message);
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
            string apVersion = ExtractVersionFromUrl(apUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "BL2 Archipelago Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BORDERLANDS 2 ARCHIPELAGO ===\n";
            content += "AP file: " + apUrl + "\n";
            content += "AP Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "apworld: " + apworldUrl + "\n";
            content += "apworld Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string modsPath = Path.Combine(gamePath, "sdk_mods");
            if (!Directory.Exists(modsPath))
                Directory.CreateDirectory(modsPath);

            string versionPath = Path.Combine(modsPath, versionFileName);
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
            string sdkModsPath = Path.Combine(gamePath, "sdk_mods");

            if (!Directory.Exists(sdkModsPath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"BL2 Archipelago Version .+\.txt");

            string[] files = Directory.GetFiles(sdkModsPath);
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

        // fallback to filename or Unknown
        string fileName = url.Substring(url.LastIndexOf('/') + 1);
        if (fileName.Contains("?"))
            fileName = fileName.Substring(0, fileName.IndexOf("?"));
        return fileName.Length > 0 ? fileName : "Unknown";
    }
}