using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class VotVManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData ue4ssFiles;
    public FileDownloader.FileData apmodFiles;
    public FileDownloader.FileData apworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installUE4SSToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("DIRECTORY SELECTION")]
    public Button directoryButton;
    public TMP_InputField directoryInputField;
    public TextMeshProUGUI directoryPlaceholder;
    public UISkin darkSkin;

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

    [System.Serializable]
    public class GameConfig
    {
        public string votvUE4SS;
        public string votvAP;
        public string votvApworld;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
    }

    void Start()
    {
        gamePath = "";
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

        if (directoryButton != null)
            directoryButton.onClick.AddListener(SelectDirectory);

        if (directoryInputField != null)
        {
            directoryInputField.onEndEdit.AddListener(OnDirectoryInputChanged);
            if (directoryPlaceholder != null)
                directoryPlaceholder.gameObject.SetActive(true);
        }

        // Revert toggles setup
        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.onValueChanged.AddListener(OnFullCleanChanged);
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

    void SelectDirectory()
    {
        StartCoroutine(ShowFileBrowser());
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
        {
            FileBrowser.Skin = darkSkin;
            UnityEngine.Debug.Log("DarkSkin assigned!");
        }

        string startPath = gamePath;
        if (directoryInputField != null && !string.IsNullOrEmpty(directoryInputField.text))
        {
            string inputPath = directoryInputField.text;
            if (Directory.Exists(inputPath))
                startPath = inputPath;
        }

        if (string.IsNullOrEmpty(startPath) || !Directory.Exists(startPath))
        {
            startPath = "C:\\";
        }

        FileBrowser.SetFilters(true, new FileBrowser.Filter("Executable", ".exe"));

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.FilesAndFolders,
            false,
            startPath,
            "Select votv.exe or VotV Game Directory"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];

            // If a file is selected (votv.exe), get its directory
            if (File.Exists(selectedPath) && selectedPath.EndsWith("votv.exe", System.StringComparison.OrdinalIgnoreCase))
            {
                selectedPath = Path.GetDirectoryName(selectedPath);
            }

            SetDirectory(selectedPath);
        }
    }

    void SetDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ShowSetupInfo("Directory path is empty!");
            return;
        }

        // Verify votv.exe exists in the selected directory
        string votvExePath = Path.Combine(path, "votv.exe");
        if (!File.Exists(votvExePath))
        {
            ShowSetupInfo("Invalid directory! votv.exe not found.\nPlease select the directory containing votv.exe");
            return;
        }

        if (!Directory.Exists(path))
        {
            ShowSetupInfo("Invalid directory path!");
            return;
        }

        gamePath = path;

        if (directoryPlaceholder != null)
            directoryPlaceholder.gameObject.SetActive(false);

        if (directoryInputField != null)
        {
            directoryInputField.gameObject.SetActive(true);
            directoryInputField.text = path;
        }

        UnityEngine.Debug.Log("Directory selected: " + path);
    }

    void OnDirectoryInputChanged(string newDirectory)
    {
        if (string.IsNullOrEmpty(newDirectory))
        {
            gamePath = "";
            UnityEngine.Debug.Log("Directory input cleared");
            return;
        }

        if (!Directory.Exists(newDirectory))
        {
            UnityEngine.Debug.Log("Directory does not exist: " + newDirectory);
            return;
        }

        string votvExePath = Path.Combine(newDirectory, "votv.exe");
        if (!File.Exists(votvExePath))
        {
            ShowSetupInfo("votv.exe not found in this directory!");
            return;
        }

        gamePath = newDirectory;
        UnityEngine.Debug.Log("Game path updated from input field: " + gamePath);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        ue4ssFiles.url = remoteConfig.votvUE4SS;
        apmodFiles.url = remoteConfig.votvAP;
        apworld.url = remoteConfig.votvApworld;
    }

    public void RunSetup()
    {
        ShowSetupConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    public void RevertAll()
    {
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

    // =========================================================
    // SETUP
    // =========================================================
    private void ExecuteSetup()
    {
        if (!configLoaded)
        {
            ShowSetupInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        if (directoryInputField == null || string.IsNullOrEmpty(directoryInputField.text))
        {
            ShowSetupInfo("Please select a directory first!");
            return;
        }

        string testPath = directoryInputField.text;
        if (!Directory.Exists(testPath))
        {
            ShowSetupInfo("Selected directory does not exist:\n" + testPath);
            return;
        }

        string votvExePath = Path.Combine(testPath, "votv.exe");
        if (!File.Exists(votvExePath))
        {
            ShowSetupInfo("votv.exe not found in selected directory!");
            return;
        }

        gamePath = testPath;

        bool apworldInstall = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ue4ss = installUE4SSToggle == null || installUE4SSToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;

        int count = (apworldInstall ? 1 : 0) + (ue4ss ? 1 : 0) + (apmod ? 1 : 0);

        if (apworldInstall && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (ue4ss && count == 1) { StartCoroutine(UE4SSOnlyFlow()); return; }
        if (apmod && count == 1) { StartCoroutine(APModOnlyFlow()); return; }

        if (count == 0)
        {
            ShowSetupInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator SetupWithTracking()
    {
        ShowSetupInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowSetupInfo("Downloading and installing files...");

        yield return InstallFlow();

        SaveInstalledFilesManifest(currentManifest);

        ShowSetupInfo("Installation complete!");
        yield return new WaitForSeconds(1f);

        // Reset les toggles après l'installation
        ResetInstallationToggles();
    }

    private void ExecuteRevert()
    {
        // VERIF STRICTE: Vérifier que le chemin est valide AVANT de faire le revert
        if (directoryInputField == null || string.IsNullOrEmpty(directoryInputField.text))
        {
            ShowSetupInfo("Please select a directory first!");
            return;
        }

        string testPath = directoryInputField.text;
        if (!Directory.Exists(testPath))
        {
            ShowSetupInfo("Selected directory does not exist!");
            return;
        }

        string votvExePath = Path.Combine(testPath, "votv.exe");
        if (!File.Exists(votvExePath))
        {
            ShowSetupInfo("votv.exe not found in selected directory!");
            return;
        }

        gamePath = testPath;

        string win64Path = Path.Combine(gamePath, "VotV", "Binaries", "Win64");
        string ue4ssPath = Path.Combine(win64Path, "ue4ss");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanUE4SSToggle != null && fullCleanUE4SSToggle.isOn;

        UnityEngine.Debug.Log("ExecuteRevert - removeAP: " + removeAP + ", fullClean: " + fullClean);

        if (!removeAP && !fullClean)
        {
            ShowSetupInfo("Please select at least one revert option.");
            return;
        }

        bool hasOtherMods = HasOtherMods(ue4ssPath);

        if (removeAP && !fullClean)
        {
            CleanupProcesses();

            ShowSetupInfo("Removing AP mods...");

            string modsPath = Path.Combine(ue4ssPath, "Mods");
            string apmodTargetPath = Path.Combine(modsPath, "votv_ap-main");

            UnityEngine.Debug.Log("Attempting to remove: " + apmodTargetPath);

            SafeDeleteDirectory(apmodTargetPath);
            DeleteOldVersionFiles();

            ShowSetupInfo("AP mods removed successfully!");
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

            ShowSetupInfo("Cleaning UE4SS...");

            UnityEngine.Debug.Log("Removing UE4SS at: " + ue4ssPath);
            UnityEngine.Debug.Log("Removing dwmapi.dll at: " + Path.Combine(win64Path, "dwmapi.dll"));

            SafeDeleteDirectory(ue4ssPath);
            SafeDeleteFile(Path.Combine(win64Path, "dwmapi.dll"));
            DeleteOldVersionFiles();

            ShowSetupInfo("Full clean completed!");
            return;
        }

        UnityEngine.Debug.LogWarning("ExecuteRevert: No revert action was performed!");
    }

    bool HasOtherMods(string ue4ssPath)
    {
        string modsPath = Path.Combine(ue4ssPath, "Mods");

        if (!Directory.Exists(modsPath))
            return false;

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "votv_ap-main")
                return true;
        }

        return false;
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowSetupInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowSetupInfo("Launching VotV...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowSetupInfo("Installation complete!");
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

        // Reset les toggles après l'installation
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

        // Reset les toggles après l'installation
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

        CreateVersionFile(ue4ssFiles.url, apmodFiles.url, apworld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowSetupInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallUE4SS()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "UE4SSTemp");

        yield return downloader.DownloadAndExtract(ue4ssFiles, Application.persistentDataPath, extractPath);

        string ue4ssSourcePath = Path.Combine(extractPath, "ue4ss");

        if (!Directory.Exists(ue4ssSourcePath))
        {
            ShowSetupInfo("ERROR: ue4ss folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Target path: [gamePath]\VotV\Binaries\Win64
        string targetBasePath = Path.Combine(gamePath, "VotV", "Binaries", "Win64");
        Directory.CreateDirectory(targetBasePath);

        string ue4ssTargetPath = Path.Combine(targetBasePath, "ue4ss");
        MoveDirectoryAndTrack(ue4ssSourcePath, ue4ssTargetPath);

        string dwmapiSource = Path.Combine(extractPath, "dwmapi.dll");
        if (File.Exists(dwmapiSource))
        {
            string dwmapiTarget = Path.Combine(targetBasePath, "dwmapi.dll");

            if (File.Exists(dwmapiTarget))
                File.Delete(dwmapiTarget);

            File.Copy(dwmapiSource, dwmapiTarget, true);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dwmapiTarget);

            UnityEngine.Debug.Log("dwmapi.dll installed to: " + dwmapiTarget);
        }
        else
        {
            UnityEngine.Debug.LogWarning("WARNING: dwmapi.dll not found in extraction!");
        }

        SafeDeleteDirectory(extractPath);

        ShowSetupInfo("UE4SS installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "APModTemp");

        yield return downloader.DownloadAndExtract(apmodFiles, Application.persistentDataPath, extractPath);

        string apmodSourcePath = Path.Combine(extractPath, "votv_ap-main");

        if (!Directory.Exists(apmodSourcePath))
        {
            ShowSetupInfo("ERROR: votv_ap-main folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string targetModsPath = Path.Combine(gamePath, "VotV", "Binaries", "Win64", "ue4ss", "Mods");
        Directory.CreateDirectory(targetModsPath);

        string apmodTargetPath = Path.Combine(targetModsPath, "votv_ap-main");
        MoveDirectoryAndTrack(apmodSourcePath, apmodTargetPath);

        SafeDeleteDirectory(extractPath);

        ShowSetupInfo("APMod installation verified successfully!");
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
            ShowSetupInfo("ERROR: APWorld URL is empty!");
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
            ShowSetupInfo("ERROR: APWorld download failed!");
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
            ShowSetupInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
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

            ShowSetupInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowSetupInfo("ERROR: Failed to install APWorld\n" + e.Message);
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
        UnityEngine.Debug.Log("Config marked as loaded");
    }

    void LaunchGame()
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowSetupInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string[] possibleExePaths = new string[]
        {
            Path.Combine(gamePath, "votv.exe"),
        };

        string exePath = "";
        foreach (string path in possibleExePaths)
        {
            if (File.Exists(path))
            {
                exePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            ShowSetupInfo("Game executable not found at:\n" + gamePath + "\\votv.exe");
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
            ShowSetupInfo("Error launching game:\n" + e.Message);
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

    void MoveDirectoryAndTrack(string source, string target)
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

            File.Move(file, dest);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);
        }
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "VotVInstalledFilesManifest.json");
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

    void CreateVersionFile(string ue4ssUrl, string apmodUrl, string apworldUrl)
    {
        try
        {
            string ue4ssVersion = ExtractVersionFromUrl(ue4ssUrl);
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);

            string versionFileName = "VotV APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== UE4SS ===\n";
            content += "Downloaded from: " + ue4ssUrl + "\n";
            content += "Version: " + ue4ssVersion + "\n";
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"VotV APMod Version .+\.txt");

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

    void ShowSetupInfo(string message)
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
        gamePath = "";

        if (directoryInputField != null)
        {
            directoryInputField.text = "";
            directoryInputField.gameObject.SetActive(false);
        }

        if (directoryPlaceholder != null)
            directoryPlaceholder.gameObject.SetActive(true);

        if (setupConfirmationPanel != null)
            setupConfirmationPanel.SetActive(false);

        if (setupInfoPanel != null)
            setupInfoPanel.SetActive(false);

        UnityEngine.Debug.Log("VotVManualDL state reset");
    }

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseSetupInfoPanel();
        ShowSetupConfirmation("Are you sure you want to install all the files?", "Setup");
    }
}