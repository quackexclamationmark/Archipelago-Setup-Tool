using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class BloonsTD6ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("BTD6 FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData melonLoader;
    public FileDownloader.FileData modHelper;
    public FileDownloader.FileData updater;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "BloonsTD6";
    public string[] epicGameFolderNames = new string[]
    {
    "BloonsTD6",
    "BloonsTD64782b",
    "BloonsTD6e960b"
    };

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installMelonLoaderToggle;
    public Toggle installModHelperToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanMelonLoaderToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string btd6Path;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private BTD6Config remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class BTD6Config
    {
        public string btd6AP;
        public string btd6MelonLoader;
        public string btd6Apworld;
        public string btd6ModHelper;
        public string btd6Updater;
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

        btd6Path = GetBTD6Path();
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

        if (fullCleanMelonLoaderToggle != null)
            fullCleanMelonLoaderToggle.isOn = false;

        if (fullCleanMelonLoaderToggle != null)
            fullCleanMelonLoaderToggle.onValueChanged.AddListener(OnFullCleanChanged);
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
        btd6Path = GetBTD6Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + btd6Path);
    }

    void SelectEpic()
    {
        isEpic = true;
        btd6Path = GetBTD6Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + btd6Path);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(btd6Path) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplyBTD6Config()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.btd6AP;
        melonLoader.url = remoteConfig.btd6MelonLoader;
        apworld.url = remoteConfig.btd6Apworld;
        modHelper.url = remoteConfig.btd6ModHelper;
        updater.url = remoteConfig.btd6Updater;
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
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        btd6Path = GetBTD6Path();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool modhelper = installModHelperToggle != null && installModHelperToggle.isOn;

        bool needsGamePath = apmod || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(btd6Path))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (melonloader ? 1 : 0) +
            (apmod ? 1 : 0) +
            (modhelper ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (melonloader && count == 1)
        {
            StartCoroutine(MelonLoaderOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        if (modhelper && count == 1)
        {
            StartCoroutine(ModHelperOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchBTD6();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        btd6Path = GetBTD6Path();

        if (string.IsNullOrEmpty(btd6Path))
            return;

        string modsPath = Path.Combine(btd6Path, "Mods");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanMelonLoaderToggle != null && fullCleanMelonLoaderToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(modsPath, "BloonsArchipelago.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "Btd6ModHelper.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "UpdaterPlugin.dll"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete MelonLoader?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Removing mods...");

        SafeDeleteFile(Path.Combine(modsPath, "BloonsArchipelago.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "Btd6ModHelper.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "UpdaterPlugin.dll"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(btd6Path, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "UserData"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "Mods"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "Plugins"));
            SafeDeleteFile(Path.Combine(btd6Path, "version.dll"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(btd6Path, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "UserData"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "Mods"));
            SafeDeleteDirectory(Path.Combine(btd6Path, "Plugins"));
            SafeDeleteFile(Path.Combine(btd6Path, "version.dll"));

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("BTD6 APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name != "BloonsArchipelago.dll" && name != "Btd6ModHelper.dll" && name != "UpdaterPlugin.dll")
                return true;
        }

        if (dirs.Length > 0)
            return true;

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn)
        {
            ShowInfo("Installing MelonLoader...");
            yield return InstallMelonLoader();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (installModHelperToggle == null || installModHelperToggle.isOn)
        {
            ShowInfo("Installing Mod Helper...");
            yield return InstallModHelper();
            yield return InstallUpdater();
        }

        CreateVersionFile(apMod.url, melonLoader.url, modHelper.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching BTD6...");
            yield return new WaitForSeconds(1f);
            LaunchBTD6();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
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

    IEnumerator InstallAPMod()
    {
        UnityEngine.Debug.Log("START InstallAPMod");
        ShowInfo("Downloading AP Mod...");

        string modsPath = Path.Combine(btd6Path, "Mods");
        Directory.CreateDirectory(modsPath);

        string dllPath = Path.Combine(modsPath, "BloonsArchipelago.dll");

        UnityEngine.Debug.Log("Downloading from: " + apMod.url);
        UnityEngine.Debug.Log("Saving to: " + dllPath);

        yield return DownloadFile(apMod.url, dllPath);

        if (File.Exists(dllPath))
        {
            UnityEngine.Debug.Log("BloonsArchipelago.dll downloaded successfully");
            ShowInfo("AP Mod installed successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError("BloonsArchipelago.dll download failed!");
            ShowInfo("ERROR: AP Mod download failed!");
        }

        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    IEnumerator InstallMelonLoader()
    {
        UnityEngine.Debug.Log("START InstallMelonLoader");

        while (!configLoaded)
            yield return null;

        ShowInfo("Extracting MelonLoader...");

        string extractPath = Path.Combine(Application.persistentDataPath, "MelonLoaderTemp");

        UnityEngine.Debug.Log("Download path: " + extractPath);

        yield return downloader.DownloadAndExtract(melonLoader, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, btd6Path);

        Directory.CreateDirectory(Path.Combine(btd6Path, "Mods"));

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallMelonLoader");
        yield return null;
    }

    void CopyMelonLoaderFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyMelonLoaderFiles");

        string versionDllSource = FindFile(extractPath, "version.dll");
        if (!string.IsNullOrEmpty(versionDllSource))
        {
            File.Copy(versionDllSource, Path.Combine(targetPath, "version.dll"), true);
            UnityEngine.Debug.Log("version.dll copied");
        }
        else
        {
            UnityEngine.Debug.LogWarning("version.dll not found!");
        }

        string melonLoaderFolder = FindDirectory(extractPath, "MelonLoader");
        if (!string.IsNullOrEmpty(melonLoaderFolder))
        {
            string destFolder = Path.Combine(targetPath, "MelonLoader");
            if (Directory.Exists(destFolder))
                SafeDeleteDirectory(destFolder);

            MoveDirectory(melonLoaderFolder, destFolder);
            UnityEngine.Debug.Log("MelonLoader folder copied");
        }
        else
        {
            UnityEngine.Debug.LogWarning("MelonLoader folder not found!");
        }

        UnityEngine.Debug.Log("END CopyMelonLoaderFiles");
    }

    IEnumerator InstallModHelper()
    {
        UnityEngine.Debug.Log("START InstallModHelper");
        ShowInfo("Downloading Mod Helper...");

        string modsPath = Path.Combine(btd6Path, "Mods");
        Directory.CreateDirectory(modsPath);

        string dllPath = Path.Combine(modsPath, "Btd6ModHelper.dll");

        UnityEngine.Debug.Log("Downloading from: " + modHelper.url);
        UnityEngine.Debug.Log("Saving to: " + dllPath);

        yield return DownloadFile(modHelper.url, dllPath);

        if (File.Exists(dllPath))
        {
            UnityEngine.Debug.Log("Btd6ModHelper.dll downloaded successfully");
            ShowInfo("Mod Helper installed successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError("Btd6ModHelper.dll download failed!");
            ShowInfo("ERROR: Mod Helper download failed!");
        }

        UnityEngine.Debug.Log("END InstallModHelper");
        yield return null;
    }

    IEnumerator InstallUpdater()
    {
        UnityEngine.Debug.Log("START InstallUpdater");
        ShowInfo("Downloading Updater Plugin...");

        string modsPath = Path.Combine(btd6Path, "Mods");
        Directory.CreateDirectory(modsPath);

        string dllPath = Path.Combine(modsPath, "UpdaterPlugin.dll");

        UnityEngine.Debug.Log("Downloading from: " + updater.url);
        UnityEngine.Debug.Log("Saving to: " + dllPath);

        yield return DownloadFile(updater.url, dllPath);

        if (File.Exists(dllPath))
        {
            UnityEngine.Debug.Log("UpdaterPlugin.dll downloaded successfully");
            ShowInfo("Updater Plugin installed successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError("UpdaterPlugin.dll download failed!");
            ShowInfo("ERROR: Updater Plugin download failed!");
        }

        UnityEngine.Debug.Log("END InstallUpdater");
        yield return null;
    }

    IEnumerator MelonLoaderOnlyFlow()
    {
        ShowInfo("Installing MelonLoader...");
        yield return InstallMelonLoader();

        ShowInfo("MelonLoader installed successfully!");

        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        btd6Path = GetBTD6Path();

        if (string.IsNullOrEmpty(btd6Path))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, melonLoader.url, modHelper.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching BTD6...");
            yield return new WaitForSeconds(1f);
            LaunchBTD6();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator ModHelperOnlyFlow()
    {
        btd6Path = GetBTD6Path();

        if (string.IsNullOrEmpty(btd6Path))
            yield break;

        ShowInfo("Installing Mod Helper...");
        yield return InstallModHelper();
        yield return InstallUpdater();

        CreateVersionFile(apMod.url, melonLoader.url, modHelper.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching BTD6...");
            yield return new WaitForSeconds(1f);
            LaunchBTD6();
        }
        else
        {
            ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<BTD6Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyBTD6Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        btd6Path = GetBTD6Path();
        UpdatePlatformStatus();
    }

    void LaunchBTD6()
    {
        try
        {
            string exePath = Path.Combine(btd6Path, "BloonsTD6.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("BTD6 launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("BloonsTD6.exe not found at: " + exePath);
                ShowInfo("ERROR: BloonsTD6.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch BTD6: " + e.Message);
            ShowInfo("ERROR: Failed to launch BTD6!");
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

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

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

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName)
                    return file;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error finding file: " + e.Message);
        }

        return "";
    }

    string FindDirectory(string root, string dirName)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir) == dirName)
                    return dir;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error finding directory: " + e.Message);
        }

        return "";
    }

    void CreateVersionFile(string apmodUrl, string melonloaderUrl, string modhelperUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/releases/download/([^/]+)/");
            string melonloaderVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string modhelperVersion = ExtractVersionFromUrl(modhelperUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "BTD6 APMod Version " + apmodVersion + ".txt";
            string content = "BloonsTD6 Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== MELONLOADER ===\n";
            content += "Downloaded from: " + melonloaderUrl + "\n";
            content += "Version: " + melonloaderVersion + "\n";
            content += "\n";
            content += "=== MOD HELPER ===\n";
            content += "Downloaded from: " + modhelperUrl + "\n";
            content += "Version: " + modhelperVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(btd6Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(btd6Path, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods: " + modsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"BTD6 APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(btd6Path);
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

            string modsPath = Path.Combine(btd6Path, "Mods");
            if (Directory.Exists(modsPath))
            {
                string[] modsFiles = Directory.GetFiles(modsPath);
                foreach (string file in modsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in Mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Mods: " + e.Message);
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

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string GetBTD6Path()
    {
        if (isEpic)
            return GetBTD6EpicPath();
        else
            return GetBTD6SteamPath();
    }

    string GetBTD6SteamPath()
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

    string GetBTD6EpicPath()
    {
        string[] quickBases = new string[]
        {
            @"C:\Program Files\Epic Games",
            @"C:\Games\Epic",
        };

        foreach (string basePath in quickBases)
        {
            foreach (string folderName in epicGameFolderNames)
            {
                try
                {
                    string path = Path.Combine(basePath, folderName);
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                        return path;
                    }
                }
                catch { }
            }
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

                        bool matchesBTD6 = false;
                        foreach (string folderName in epicGameFolderNames)
                        {
                            if (content.Contains(folderName))
                            {
                                matchesBTD6 = true;
                                break;
                            }
                        }

                        if (matchesBTD6)
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value.Replace(@"\\", @"\");
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

                        foreach (string folderName in epicGameFolderNames)
                        {
                            try
                            {
                                string epicPath = Path.Combine(drive.Name, relativePath, folderName);
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
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
    }
}