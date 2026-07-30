using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class DRDRManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DRDR FILES")]
    public FileDownloader.FileData drdrApworld;         // drdr.apworld
    public FileDownloader.FileData drdrREFramework;      // dinput8.dll (single file)
    public FileDownloader.FileData drdrAP;               // DRAP.zip -> extracted into /reframework
    public FileDownloader.FileData drdrLuaAPClient;      // lua-apclientpp.dll (single file)

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "DEAD RISING DELUXE REMASTER";

    [Header("FEATURE TOGGLES")]
    public Toggle installDrdrApworldToggle;
    public Toggle installDrdrFrameworkToggle;
    public Toggle installDrdrAPToggle; // also drives lua-apclientpp.dll install

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanFrameworkToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process drdrProcess;
    private string drdrPath;
    private string pendingAction;
    private DRDRConfig remoteConfig;
    private bool configLoaded = false;

    // --------- FULL CLEAN: specific single files ---------
    private string[] filesToDeleteOnFullClean = new string[]
    {
        "lua-apclientpp.dll",
        "dinput8.dll",
        "re2_framework_log.txt",
        "ref_ui.ini",
        "re2_fw_config.txt"
    };

    // Directories removed entirely on full clean
    private string[] directoriesToDeleteOnFullClean = new string[]
    {
        "reframework",
        "_storage_"
    };

    // --------- CLEAR AP MODS ONLY ---------
    // Folders/files under reframework\autorun
    private string[] autorunFoldersToDeleteOnClearAPMods = new string[]
    {
        "AP_REF",
        "DRAP"
    };

    private string[] autorunFilesToDeleteOnClearAPMods = new string[]
    {
        "AP_DRDR_main.lua"
    };

    // Folders under reframework\data
    private string[] dataFoldersToDeleteOnClearAPMods = new string[]
    {
        "AP_DRDR_Items"
    };

    // Files under reframework\data
    private string[] dataFilesToDeleteOnClearAPMods = new string[]
    {
        "AP_REF.json",
        "drdr_items.json",
        "drdr_shared.json",
        "Mall.png",
        "PPstickers.json",
        "README.md",
        "survivors.json"
    };

    [System.Serializable]
    public class DRDRConfig
    {
        public string drdrApworld;
        public string drdrFramework;
        public string drdrAP;
        public string drdrLuaAPClient;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        drdrPath = GetDRDRPath();
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

        if (fullCleanFrameworkToggle != null)
            fullCleanFrameworkToggle.isOn = false;

        if (fullCleanFrameworkToggle != null)
            fullCleanFrameworkToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnDestroy()
    {
        // Intentionally NOT killing drdrProcess here: if the user launched the
        // game via "Launch after setup" and then closes the installer, the
        // game must keep running. Killing on install teardown is only needed
        // before a revert (to release file locks), handled in CleanupProcesses().
    }

    void CleanupProcesses()
    {
        // Kill any running instance of the game so its files aren't locked
        // during a revert/uninstall. This intentionally looks up the process
        // by name rather than only relying on drdrProcess, since the game may
        // have been launched outside this tool or the reference may be stale.
        try
        {
            Process[] running = Process.GetProcessesByName("DRDR");
            foreach (Process p in running)
            {
                try
                {
                    p.Kill();
                    p.Dispose();
                }
                catch { }
            }
        }
        catch { }

        drdrProcess = null;
    }

    void ApplyDRDRConfig()
    {
        if (remoteConfig == null)
            return;

        drdrApworld.url = remoteConfig.drdrApworld;
        drdrREFramework.url = remoteConfig.drdrFramework;
        drdrAP.url = remoteConfig.drdrAP;
        drdrLuaAPClient.url = remoteConfig.drdrLuaAPClient;
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

            case "ForceFullClean":
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
        drdrPath = GetDRDRPath();

        bool apworld = installDrdrApworldToggle == null || installDrdrApworldToggle.isOn;
        bool framework = installDrdrFrameworkToggle != null && installDrdrFrameworkToggle.isOn;
        bool apMod = installDrdrAPToggle != null && installDrdrAPToggle.isOn; // installs DRAP.zip + lua-apclientpp.dll
        bool needsGamePath = framework || apMod;

        if (needsGamePath && (string.IsNullOrEmpty(drdrPath) || !Directory.Exists(drdrPath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (framework ? 1 : 0) +
            (apMod ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        drdrPath = GetDRDRPath();

        if (string.IsNullOrEmpty(drdrPath))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanFrameworkToggle != null && fullCleanFrameworkToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();
            ShowInfo("Removing AP mods...");
            ClearAPMods();
            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClean)
        {
            CleanupProcesses();
            ShowInfo("Removing all Framework files...");
            FullClean();
            ShowInfo("Full clean completed!");
            return;
        }

        ShowInfo("Revert completed!");
    }

    private void ClearAPMods()
    {
        try
        {
            string autorunPath = Path.Combine(drdrPath, "reframework", "autorun");
            string dataPath = Path.Combine(drdrPath, "reframework", "data");

            // autorun folders
            foreach (string folder in autorunFoldersToDeleteOnClearAPMods)
                SafeDeleteDirectory(Path.Combine(autorunPath, folder));

            // autorun files
            foreach (string file in autorunFilesToDeleteOnClearAPMods)
                SafeDeleteFileImmediate(Path.Combine(autorunPath, file));

            // data folders
            foreach (string folder in dataFoldersToDeleteOnClearAPMods)
                SafeDeleteDirectory(Path.Combine(dataPath, folder));

            // data files
            foreach (string file in dataFilesToDeleteOnClearAPMods)
                SafeDeleteFileImmediate(Path.Combine(dataPath, file));

            UnityEngine.Debug.Log("AP mods cleared");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error clearing AP mods: " + e.Message);
            ShowInfo("ERROR: Clear AP mods failed\n" + e.Message);
        }
    }

    private void FullClean()
    {
        try
        {
            DirectoryInfo drdrRoot = new DirectoryInfo(drdrPath);

            foreach (FileInfo file in drdrRoot.GetFiles())
            {
                foreach (string fileToDelete in filesToDeleteOnFullClean)
                {
                    if (file.Name.Equals(fileToDelete, System.StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            file.Delete();
                            UnityEngine.Debug.Log("Deleted: " + file.Name);
                        }
                        catch { }
                        break;
                    }
                }
            }

            foreach (string dir in directoriesToDeleteOnFullClean)
                SafeDeleteDirectory(Path.Combine(drdrPath, dir));
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error during full clean: " + e.Message);
            ShowInfo("ERROR: Full clean failed\n" + e.Message);
        }
    }

    IEnumerator InstallFlow()
    {
        if (installDrdrApworldToggle == null || installDrdrApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallDRDRApworld();
        }

        if (installDrdrFrameworkToggle != null && installDrdrFrameworkToggle.isOn)
        {
            ShowInfo("Installing Framework (dinput8.dll)...");
            yield return InstallDRDRFramework();
        }

        if (installDrdrAPToggle != null && installDrdrAPToggle.isOn)
        {
            ShowInfo("Installing Lua AP Client...");
            yield return InstallLuaAPClient();

            ShowInfo("Installing AP Client (REFramework mod)...");
            yield return InstallDRDRAP();
        }

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DRDR...");
            LaunchDRDR();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    // ---------------- APWORLD (goes into Archipelago custom_worlds) ----------------
    IEnumerator InstallDRDRApworld()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(drdrApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = drdrApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = drdrApworld.url.Substring(drdrApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        yield return DownloadFile(drdrApworld.url, localPath);

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
                File.Delete(localPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
        }
    }

    // ---------------- FRAMEWORK: dinput8.dll, single file into game root ----------------
    IEnumerator InstallDRDRFramework()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(drdrREFramework.url))
        {
            ShowInfo("ERROR: Framework URL is empty!");
            yield break;
        }

        string fileName = "dinput8.dll";
        string target = Path.Combine(drdrPath, fileName);

        yield return DownloadFile(drdrREFramework.url, target);

        if (!File.Exists(target))
        {
            ShowInfo("ERROR: dinput8.dll download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("dinput8.dll installed to: " + target);
    }

    // ---------------- LUA AP CLIENT: lua-apclientpp.dll, single file into game root ----------------
    IEnumerator InstallLuaAPClient()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(drdrLuaAPClient.url))
        {
            ShowInfo("ERROR: Lua AP Client URL is empty!");
            yield break;
        }

        string fileName = "lua-apclientpp.dll";
        string target = Path.Combine(drdrPath, fileName);

        yield return DownloadFile(drdrLuaAPClient.url, target);

        if (!File.Exists(target))
        {
            ShowInfo("ERROR: lua-apclientpp.dll download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("lua-apclientpp.dll installed to: " + target);
    }

    // ---------------- AP MOD: DRAP.zip, contents go into /reframework (create if missing) ----------------
    IEnumerator InstallDRDRAP()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(drdrAP.url))
        {
            ShowInfo("ERROR: AP package URL is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "DRDRAPTemp");

        yield return downloader.DownloadAndExtract(drdrAP, Application.persistentDataPath, extractPath);

        string reframeworkPath = Path.Combine(drdrPath, "reframework");

        if (!Directory.Exists(reframeworkPath))
            Directory.CreateDirectory(reframeworkPath);

        try
        {
            MoveDirectory(extractPath, reframeworkPath);
            UnityEngine.Debug.Log("DRAP.zip contents installed to: " + reframeworkPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install AP package: " + e.Message);
            ShowInfo("ERROR: Failed to install AP package\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
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
            UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<DRDRConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDRDRConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        drdrPath = GetDRDRPath();
    }

    void LaunchDRDR()
    {
        string exePath = Path.Combine(drdrPath, "DRDR.exe");

        if (File.Exists(exePath))
            drdrProcess = Process.Start(exePath);
    }

    void SafeDeleteFileImmediate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                UnityEngine.Debug.Log("Deleted: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete file: " + path + " - " + e.Message);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                UnityEngine.Debug.Log("Deleted directory: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete directory: " + path + " - " + e.Message);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(source.Length + 1);
            string dest = Path.Combine(target, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    string GetDRDRPath()
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