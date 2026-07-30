using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class HiFiManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData apmodFiles;
    public FileDownloader.FileData apworld;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Hi-Fi RUSH";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installUE4SSToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanUE4SSToggle;
    public Toggle removeAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL - SETUP")]
    public GameObject setupConfirmationPanel;
    public TextMeshProUGUI setupConfirmationMessage;
    public Button setupConfirmButton;
    public Button setupCancelButton;

    [Header("INFO PANEL - SETUP")]
    public GameObject setupInfoPanel;
    public TextMeshProUGUI setupInfoText;
    public Button setupInfoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;

    private const string GameInternalFolderName = "Hibiki";

    private static readonly string[] UE4SS_FILES = new string[]
    {
        "hibiki_bootstrap.dll",
        "imgui.ini",
        "lua-apclientpp.dll",
        "UE4SS.dll",
        "UE4SS-settings.ini",
        "XAPOFX1_5.dll"
    };

    private static readonly string[] UE4SS_FOLDERS = new string[]
    {
        "Mods",
        "UE4SS_Signatures"
    };

    [System.Serializable]
    public class GameConfig
    {
        public string hifirushAP;
        public string hifirushApworld;
        public string[] steamSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
    }

    void Start()
    {
        gamePath = GetHiFiRushPath();
        StartCoroutine(LoadRemoteConfig());

        if (setupInfoPanel != null)
            setupInfoPanel.SetActive(false);

        if (setupInfoOkButton != null)
            setupInfoOkButton.onClick.AddListener(CloseSetupInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installUE4SSToggle != null)
            installUE4SSToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (setupConfirmationPanel != null)
            setupConfirmationPanel.SetActive(false);

        if (setupConfirmButton != null)
            setupConfirmButton.onClick.AddListener(OnSetupConfirm);

        if (setupCancelButton != null)
            setupCancelButton.onClick.AddListener(OnSetupCancel);

        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.onValueChanged.AddListener(OnFullCleanChanged);

        UnityEngine.Debug.Log("HiFi RUSH path: " + gamePath);
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            if (value)
            {
                removeAPModsOnlyToggle.isOn = false;
                removeAPModsOnlyToggle.interactable = false;
            }
            else
            {
                removeAPModsOnlyToggle.interactable = true;
            }
        }
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        apmodFiles.url = remoteConfig.hifirushAP;
        apworld.url = remoteConfig.hifirushApworld;
    }

    public void RunSetup()
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Hi-Fi RUSH not found. Please check your Steam installation.");
            return;
        }

        ShowSetupConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    public void RevertAll()
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Hi-Fi RUSH not found. Please check your Steam installation.");
            return;
        }

        ShowSetupConfirmation("Are you sure you want to revert?", "Revert");
    }

    private void ShowSetupConfirmation(string message, string action)
    {
        pendingAction = action;
        setupConfirmationMessage.text = message;
        setupConfirmationPanel.SetActive(true);
    }

    private void OnSetupConfirm()
    {
        setupConfirmationPanel.SetActive(false);

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

    private void OnSetupCancel()
    {
        setupConfirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        gamePath = GetHiFiRushPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ue4ss = installUE4SSToggle == null || installUE4SSToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;
        bool needsGamePath = ue4ss || apmod;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Hi-Fi RUSH path not found!");
            return;
        }

        int count = (apworld ? 1 : 0) + (ue4ss ? 1 : 0) + (apmod ? 1 : 0);

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

        if (ue4ss && count == 1)
        {
            StartCoroutine(UE4SSOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching HiFi RUSH...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
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

        ResetInstallationToggles();
    }

    private void ExecuteRevert()
    {
        gamePath = GetHiFiRushPath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Hi-Fi RUSH path not found!");
            return;
        }

        string win64Path = GetWin64Path();

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanUE4SSToggle != null && fullCleanUE4SSToggle.isOn;

        UnityEngine.Debug.Log("ExecuteRevert - removeAP: " + removeAP + ", fullClean: " + fullClean);

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        bool hasOtherMods = HasOtherMods(win64Path);

        if (removeAP && !fullClean)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            string modsPath = Path.Combine(win64Path, "Mods");
            string apmodTargetPath = Path.Combine(modsPath, "HbkArchipelago");

            UnityEngine.Debug.Log("Attempting to remove: " + apmodTargetPath);

            SafeDeleteDirectory(apmodTargetPath);
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowSetupConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete UE4SS?",
                "ForceFullClean"
            );
            return;
        }

        if (fullClean)
        {
            pendingFullCleanConfirmation = false;

            CleanupProcesses();

            ShowInfo("Cleaning UE4SS...");

            // Supprimer les fichiers UE4SS
            foreach (string file in UE4SS_FILES)
            {
                string filePath = Path.Combine(win64Path, file);
                UnityEngine.Debug.Log("Removing: " + filePath);
                SafeDeleteFile(filePath);
            }

            // Supprimer les dossiers UE4SS
            foreach (string folder in UE4SS_FOLDERS)
            {
                string folderPath = Path.Combine(win64Path, folder);
                UnityEngine.Debug.Log("Removing: " + folderPath);
                SafeDeleteDirectory(folderPath);
            }

            // Supprimer UE4SS.log
            string logPath = Path.Combine(win64Path, "UE4SS.log");
            UnityEngine.Debug.Log("Removing: " + logPath);
            SafeDeleteFile(logPath);

            DeleteOldVersionFiles();

            ShowInfo("Full clean completed!");
            return;
        }

        UnityEngine.Debug.LogWarning("ExecuteRevert: No revert action was performed!");
    }

    bool HasOtherMods(string win64Path)
    {
        string modsPath = Path.Combine(win64Path, "Mods");

        if (!Directory.Exists(modsPath))
            return false;

        // Whitelist des mods autorisés
        string[] whitelistedMods = new string[]
        {
        "ActorDumperMod",
        "BPML_GenericFunctions",
        "BPModLoaderMod",
        "CheatManagerEnablerMod",
        "ConsoleCommandsMod",
        "ConsoleEnablerMod",
        "EventViewerMod",
        "HbkArchipelago",
        "jsbLuaProfilerMod",
        "Keybinds",
        "KismetDebuggerMod",
        "LineTraceMod",
        "shared",
        "SplitScreenMod",
        "TestMod",
        "TestWidgetMod"
        };

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            // Vérifier si le dossier est dans la whitelist
            bool isWhitelisted = false;
            foreach (string whitelisted in whitelistedMods)
            {
                if (name == whitelisted)
                {
                    isWhitelisted = true;
                    break;
                }
            }

            // S'il n'est pas dans la whitelist, c'est un autre mod
            if (!isWhitelisted)
                return true;
        }

        return false;
    }

    IEnumerator UE4SSOnlyFlow()
    {
        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallUE4SS();

        SaveInstalledFilesManifest(currentManifest);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();

        ResetInstallationToggles();
    }

    IEnumerator APModOnlyFlow()
    {
        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallAPMod();

        SaveInstalledFilesManifest(currentManifest);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();

        ResetInstallationToggles();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installUE4SSToggle == null || installUE4SSToggle.isOn)
            yield return InstallUE4SS();

        if (installAPModToggle == null || installAPModToggle.isOn)
            yield return InstallAPMod();

        CreateVersionFile(apmodFiles.url, apworld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallUE4SS()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "HiFiUE4SSTemp");

        yield return downloader.DownloadAndExtract(apmodFiles, Application.persistentDataPath, extractPath);

        // Target path: [gamePath]\Hibiki\Binaries\Win64
        string targetBasePath = GetWin64Path();
        Directory.CreateDirectory(targetBasePath);

        // Copier les fichiers UE4SS
        foreach (string file in UE4SS_FILES)
        {
            string sourceFile = FindFileInDirectory(extractPath, file);
            if (!string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile))
            {
                string targetFile = Path.Combine(targetBasePath, file);
                if (File.Exists(targetFile))
                    File.Delete(targetFile);

                File.Copy(sourceFile, targetFile, true);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(targetFile);

                UnityEngine.Debug.Log(file + " installed to: " + targetFile);
            }
            else
            {
                UnityEngine.Debug.LogWarning("WARNING: " + file + " not found in extraction!");
            }
        }

        // Copier les dossiers UE4SS (Mods, UE4SS_Signatures)
        foreach (string folder in UE4SS_FOLDERS)
        {
            string sourceFolder = FindFolderInDirectory(extractPath, folder);
            if (!string.IsNullOrEmpty(sourceFolder) && Directory.Exists(sourceFolder))
            {
                string targetFolder = Path.Combine(targetBasePath, folder);
                CopyDirectoryAndTrack(sourceFolder, targetFolder);

                UnityEngine.Debug.Log(folder + " installed to: " + targetFolder);
            }
            else
            {
                UnityEngine.Debug.LogWarning("WARNING: " + folder + " not found in extraction!");
            }
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("UE4SS installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "HiFiAPModTemp");

        yield return downloader.DownloadAndExtract(apmodFiles, Application.persistentDataPath, extractPath);

        // Target path: [gamePath]\Hibiki\Binaries\Win64
        string targetBasePath = GetWin64Path();
        Directory.CreateDirectory(targetBasePath);

        // Copier les fichiers UE4SS
        foreach (string file in UE4SS_FILES)
        {
            string sourceFile = FindFileInDirectory(extractPath, file);
            if (!string.IsNullOrEmpty(sourceFile) && File.Exists(sourceFile))
            {
                string targetFile = Path.Combine(targetBasePath, file);
                if (File.Exists(targetFile))
                    File.Delete(targetFile);

                File.Copy(sourceFile, targetFile, true);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(targetFile);

                UnityEngine.Debug.Log(file + " installed to: " + targetFile);
            }
            else
            {
                UnityEngine.Debug.LogWarning("WARNING: " + file + " not found in extraction!");
            }
        }

        // Copier dossier Mods avec HbkArchipelago
        string modsSourceFolder = FindFolderInDirectory(extractPath, "Mods");
        if (!string.IsNullOrEmpty(modsSourceFolder) && Directory.Exists(modsSourceFolder))
        {
            string modsTargetFolder = Path.Combine(targetBasePath, "Mods");
            string hbkSourceFolder = Path.Combine(modsSourceFolder, "HbkArchipelago");

            if (Directory.Exists(hbkSourceFolder))
            {
                string hbkTargetFolder = Path.Combine(modsTargetFolder, "HbkArchipelago");
                CopyDirectoryAndTrack(hbkSourceFolder, hbkTargetFolder);
                UnityEngine.Debug.Log("HbkArchipelago mod installed to: " + hbkTargetFolder);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("WARNING: Mods folder not found in extraction!");
        }

        // Copier dossier UE4SS_Signatures
        string signaturesSourceFolder = FindFolderInDirectory(extractPath, "UE4SS_Signatures");
        if (!string.IsNullOrEmpty(signaturesSourceFolder) && Directory.Exists(signaturesSourceFolder))
        {
            string signaturesTargetFolder = Path.Combine(targetBasePath, "UE4SS_Signatures");
            CopyDirectoryAndTrack(signaturesSourceFolder, signaturesTargetFolder);
            UnityEngine.Debug.Log("UE4SS_Signatures installed to: " + signaturesTargetFolder);
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("APMod installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + apworld.url);

        if (string.IsNullOrEmpty(apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apworld.url, localPath);

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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

            ShowInfo("APWorld installed successfully!");
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

        gamePath = GetHiFiRushPath();
    }

    void LaunchGame()
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string exePath = Path.Combine(GetWin64Path(), "Hi-Fi-RUSH.exe");

        UnityEngine.Debug.Log("Looking for exe at: " + exePath);

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found at:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            gameProcess = Process.Start(exePath);
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

    void CleanupProcesses()
    {
        CloseGame();
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                UnityEngine.Debug.Log("Deleting directory: " + path);
                Directory.Delete(path, true);
                UnityEngine.Debug.Log("Directory deleted successfully: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error deleting directory: " + path + " - " + e.Message);
        }
    }

    void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                UnityEngine.Debug.Log("Deleting file: " + path);
                File.Delete(path);
                UnityEngine.Debug.Log("File deleted successfully: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error deleting file: " + path + " - " + e.Message);
        }
    }

    void CopyDirectoryAndTrack(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Copy(file, dest, true);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);
        }
    }

    string FindFileInDirectory(string searchPath, string fileName)
    {
        try
        {
            string[] files = Directory.GetFiles(searchPath, fileName, SearchOption.AllDirectories);
            if (files.Length > 0)
                return files[0];
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error searching for file: " + e.Message);
        }

        return "";
    }

    string FindFolderInDirectory(string searchPath, string folderName)
    {
        try
        {
            string[] folders = Directory.GetDirectories(searchPath, folderName, SearchOption.AllDirectories);
            if (folders.Length > 0)
                return folders[0];
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error searching for folder: " + e.Message);
        }

        return "";
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "HiFiInstalledFilesManifest.json");
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

    void CreateVersionFile(string apmodUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);

            string versionFileName = "HiFi RUSH APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== APMOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"HiFi RUSH APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
            foreach (string file in rootFiles)
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

    void ShowInfo(string message)
    {
        if (setupInfoPanel == null || setupInfoText == null)
            return;

        setupInfoText.text = message;
        setupInfoPanel.SetActive(true);
    }

    void CloseSetupInfoPanel()
    {
        if (setupInfoPanel != null)
            setupInfoPanel.SetActive(false);
    }

    void ResetInstallationToggles()
    {
        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installUE4SSToggle != null)
            installUE4SSToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        UnityEngine.Debug.Log("Installation toggles reset to default state");
    }

    public void ResetState()
    {
        ResetInstallationToggles();
        pendingAction = "";
        pendingFullCleanConfirmation = false;

        if (setupConfirmationPanel != null)
            setupConfirmationPanel.SetActive(false);

        if (setupInfoPanel != null)
            setupInfoPanel.SetActive(false);

        UnityEngine.Debug.Log("HiFiManualDL state reset");
    }

    // =========================================================
    // PATH HELPERS
    // =========================================================

    string GetWin64Path()
    {
        return Path.Combine(gamePath, GameInternalFolderName, "Binaries", "Win64");
    }

    string GetHiFiRushPath()
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