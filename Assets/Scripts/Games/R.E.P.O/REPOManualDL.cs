using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class REPOManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("REPO FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;

    [Header("MODS")]
    public FileDownloader.FileData menuLib;
    public FileDownloader.FileData repoLib;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installMenuLibToggle;
    public Toggle installRepoLibToggle;
    public Toggle installBepInExToggle;

    [Header("CONFIG TOGGLES")]
    public Toggle patchBepInExConfigToggle;
    public Toggle patchRepoLibConfigToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle patchConfigsToggle;
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

    private Process repoProcess;
    private string repoPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private RepoConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class RepoConfig
    {
        public string repoAP;
        public string repoMenuLib;
        public string repoRepoLib;
        public string repoBepInEx;
        public string repoApworld;
    }

    void Start()
    {
        repoPath = GetRepoPath();
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

        if (patchConfigsToggle != null)
            patchConfigsToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseREPO();
    }

    void ApplyRepoConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.repoAP;
        menuLib.url = remoteConfig.repoMenuLib;
        repoLib.url = remoteConfig.repoRepoLib;
        bepInEx.url = remoteConfig.repoBepInEx;
        apworld.url = remoteConfig.repoApworld;
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
        if (string.IsNullOrEmpty(repoPath))
        {
            ShowInfo("REPO path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool menulib = installMenuLibToggle != null && installMenuLibToggle.isOn;
        bool repolib = installRepoLibToggle != null && installRepoLibToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (apmod ? 1 : 0) +
            (menulib ? 1 : 0) +
            (repolib ? 1 : 0);

        if (menulib && count == 1)
        {
            StartCoroutine(MenuLibOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (repolib && count == 1)
        {
            StartCoroutine(RepoLibOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        repoPath = GetRepoPath();

        if (string.IsNullOrEmpty(repoPath))
            return;

        string pluginsPath = Path.Combine(repoPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;
        bool patchConfigs = patchConfigsToggle != null && patchConfigsToggle.isOn;

        if (!removeAP && !fullClean && !patchConfigs)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (patchConfigs && !removeAP && !fullClean)
        {
            SetDefaultBepInExConfig();
            SetDefaultRepoLibConfig();
            ShowInfo("Configs patched successfully!");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.repobundle"));
            SafeDeleteFile(Path.Combine(pluginsPath, "MenuLib.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "RepoAP.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "RepoLib.dll"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.repobundle"));
        SafeDeleteFile(Path.Combine(pluginsPath, "MenuLib.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "RepoAP.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "RepoLib.dll"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(repoPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(repoPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(repoPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(repoPath, ".doorstop_version"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(repoPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(repoPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(repoPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(repoPath, ".doorstop_version"));

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("REPO APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name != "Archipelago.repobundle" &&
                name != "MenuLib.dll" &&
                name != "RepoAP.dll" &&
                name != "RepoLib.dll")
                return true;
        }

        if (dirs.Length > 0)
            return true;

        return false;
    }

    void SetDefaultBepInExConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        if (!File.Exists(cfgPath))
            return;

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("HideManagerGameObject"))
                lines[i] = "HideManagerGameObject = false";

        File.WriteAllLines(cfgPath, lines);
    }

    void SetDefaultRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        if (!File.Exists(cfgPath))
            return;

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("VanillaDeveloperMode"))
                lines[i] = "VanillaDeveloperMode = false";

            if (lines[i].Contains("DeveloperMode"))
                lines[i] = "DeveloperMode = false";
        }

        File.WriteAllLines(cfgPath, lines);
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (installMenuLibToggle == null || installMenuLibToggle.isOn)
        {
            ShowInfo("Installing MenuLib...");
            yield return InstallMod(menuLib, "MenuLib.dll");
        }

        if (installRepoLibToggle == null || installRepoLibToggle.isOn)
        {
            ShowInfo("Installing RepoLib...");
            yield return InstallRepoLib();
        }

        CreateVersionFile(apMod.url, menuLib.url, repoLib.url, bepInEx.url, apworld.url);

        ShowInfo("Launching REPO...");
        LaunchREPO();

        yield return WaitForConfigFiles();
        yield return WaitForBepInExConfigComplete();

        CloseREPO();

        yield return new WaitForSeconds(1f);

        if (patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn)
        {
            ShowInfo("Patching BepInEx config...");
            yield return SetBepInExConfig();
        }

        if (patchRepoLibConfigToggle != null && patchRepoLibConfigToggle.isOn)
        {
            ShowInfo("Patching RepoLib config...");
            yield return SetRepoLibConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchREPO();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
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
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "APModTemp");

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string plugins = Path.Combine(repoPath, "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);

        CopyIfExists(extractPath, "RepoAP.dll", plugins);
        CopyIfExists(extractPath, "Archipelago.repobundle", plugins);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallRepoLib()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "RepoLibTemp");

        yield return downloader.DownloadAndExtract(repoLib, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(repoPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyIfExists(Path.Combine(extractPath, "plugins"), "RepoLib.dll",
            Path.Combine(repoPath, "BepInEx", "plugins"));

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMod(FileDownloader.FileData mod, string dllName)
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, dllName + "_temp");

        yield return downloader.DownloadAndExtract(mod, Application.persistentDataPath, extractPath);

        string dllPath = FindFile(extractPath, dllName);

        string plugins = Path.Combine(repoPath, "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);

        if (!string.IsNullOrEmpty(dllPath))
            File.Copy(dllPath, Path.Combine(plugins, dllName), true);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, repoPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APWorldOnlyFlow()
    {
        repoPath = GetRepoPath();

        if (string.IsNullOrEmpty(repoPath))
            yield break;

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchREPO();
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        bool shouldPatchConfig = patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn;

        if (shouldPatchConfig)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
            yield return WaitForBepInExConfigComplete();
            CloseREPO();
            yield return new WaitForSeconds(1f);
            ShowInfo("Patching BepInEx config...");
            yield return SetBepInExConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator WaitForBepInExConfigComplete()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfgPath))
            {
                string[] lines = File.ReadAllLines(cfgPath);

                foreach (string line in lines)
                {
                    if (line.Contains("HideManagerGameObject"))
                        yield break;
                }
            }

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator RepoLibOnlyFlow()
    {
        ShowInfo("Installing RepoLib...");
        yield return InstallRepoLib();

        bool shouldPatchConfig = patchRepoLibConfigToggle != null && patchRepoLibConfigToggle.isOn;

        if (shouldPatchConfig)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
            yield return WaitForRepoLibConfig();
            CloseREPO();
            yield return new WaitForSeconds(1f);
            ShowInfo("Patching RepoLib config...");
            yield return SetRepoLibConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        repoPath = GetRepoPath();

        if (string.IsNullOrEmpty(repoPath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, menuLib.url, repoLib.url, bepInEx.url, apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchREPO();

        ShowInfo("Installation complete!");
    }

    IEnumerator MenuLibOnlyFlow()
    {
        repoPath = GetRepoPath();

        if (string.IsNullOrEmpty(repoPath))
            yield break;

        ShowInfo("Installing MenuLib...");
        yield return InstallMod(menuLib, "MenuLib.dll");

        CreateVersionFile(apMod.url, menuLib.url, repoLib.url, bepInEx.url, apworld.url);

        bool shouldPatchConfig = patchConfigsToggle != null && patchConfigsToggle.isOn;

        if (shouldPatchConfig)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
            yield return WaitForBepInExConfigComplete();
            CloseREPO();
            yield return new WaitForSeconds(1f);

            ShowInfo("Patching BepInEx config...");
            yield return SetBepInExConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching REPO...");
            LaunchREPO();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator WaitForRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfgPath))
            {
                string[] lines = File.ReadAllLines(cfgPath);

                foreach (string line in lines)
                {
                    if (line.Contains("DeveloperMode"))
                        yield break;
                }
            }

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator WaitForConfigFiles()
    {
        string bepConfig = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");
        string repoConfig = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(bepConfig) && File.Exists(repoConfig))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator SetBepInExConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        yield return new WaitUntil(() => File.Exists(cfgPath));

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("HideManagerGameObject"))
                lines[i] = "HideManagerGameObject = true";

        File.WriteAllLines(cfgPath, lines);
    }

    IEnumerator SetRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        yield return new WaitUntil(() => File.Exists(cfgPath));

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("VanillaDeveloperMode"))
                lines[i] = "VanillaDeveloperMode = true";
            else if (lines[i].StartsWith("DeveloperMode"))
                lines[i] = "DeveloperMode = true";
        }

        File.WriteAllLines(cfgPath, lines);
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
            remoteConfig = JsonUtility.FromJson<RepoConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyRepoConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchREPO()
    {
        string exePath = Path.Combine(repoPath, "REPO.exe");

        if (File.Exists(exePath))
            repoProcess = Process.Start(exePath);
    }

    void CloseREPO()
    {
        try
        {
            if (repoProcess != null && !repoProcess.HasExited)
            {
                repoProcess.Kill();
                repoProcess.Dispose();
                repoProcess = null;
            }
        }
        catch { }
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
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

    void CopyIfExists(string root, string file, string target)
    {
        string path = Path.Combine(root, file);

        if (File.Exists(path))
            File.Copy(path, Path.Combine(target, file), true);
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

    void OnFullCleanChanged(bool value)
    {
        if (patchConfigsToggle != null)
        {
            patchConfigsToggle.isOn = false;
            patchConfigsToggle.interactable = !value;
        }

        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    void CreateVersionFile(string apmodUrl, string menulibUrl, string repolibUrl, string bepinexUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/releases/download/([^/]+)/");
            string menulibVersion = ExtractVersionFromUrl(menulibUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string repolibVersion = ExtractVersionFromUrl(repolibUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "REPO APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
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
            content += "=== MENULIB ===\n";
            content += "Downloaded from: " + menulibUrl + "\n";
            content += "Version: " + menulibVersion + "\n";
            content += "\n";
            content += "=== REPOLIB ===\n";
            content += "Downloaded from: " + repolibUrl + "\n";
            content += "Version: " + repolibVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(repoPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(repoPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"REPO APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(repoPath);
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

            string pluginsPath = Path.Combine(repoPath, "BepInEx", "plugins");
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

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string GetRepoPath()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "REPO"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "REPO"),
        @"D:\Steam\steamapps\common\REPO",
        @"D:\SteamLibrary\steamapps\common\REPO",
        @"D:\steamapps\common\REPO",
        @"E:\Steam\steamapps\common\REPO",
        @"E:\SteamLibrary\steamapps\common\REPO",
        @"E:\steamapps\common\REPO",
        @"E:\Program Files (x86)\steamapps\common\REPO",
        @"E:\Program Files\steamapps\common\REPO",
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
                    // Cherche Steam\steamapps
                    string repoPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "REPO");
                    if (Directory.Exists(repoPath))
                        return repoPath;

                    // Cherche SteamLibrary\steamapps
                    repoPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "REPO");
                    if (Directory.Exists(repoPath))
                        return repoPath;

                    // Cherche directement steamapps à la racine du disque
                    repoPath = Path.Combine(drive.Name, "steamapps", "common", "REPO");
                    if (Directory.Exists(repoPath))
                        return repoPath;

                    // Cherche dans Program Files (x86)\steamapps
                    repoPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "REPO");
                    if (Directory.Exists(repoPath))
                        return repoPath;

                    // Cherche dans Program Files\steamapps
                    repoPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "REPO");
                    if (Directory.Exists(repoPath))
                        return repoPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}