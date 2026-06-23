using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class LethalCompanyManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("LETHAL COMPANY FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData lethalAPI;
    public FileDownloader.FileData apScrap;
    public FileDownloader.FileData levelLoader;
    public FileDownloader.FileData lethalLib;
    public FileDownloader.FileData fixSerialize;
    public FileDownloader.FileData modData;
    public FileDownloader.FileData hook;
    public FileDownloader.FileData monoDetourBep5;
    public FileDownloader.FileData monoDetour;
    public FileDownloader.FileData mmhook;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;
    public Toggle installLethalAPIToggle;
    public Toggle installAPScrapToggle;
    public Toggle installLevelLoaderToggle;
    public Toggle installLethalLibToggle;
    public Toggle installFixSerializeToggle;
    public Toggle installModDataToggle;
    public Toggle installHookToggle;
    public Toggle installMonoDetourBep5Toggle;
    public Toggle installMonoDetourToggle;

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

    private Process lethalCompanyProcess;
    private string lethalCompanyPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private LethalCompanyConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class LethalCompanyConfig
    {
        public string lethalcompanyAP;
        public string lethalcompanyBepInEx;
        public string lethalcompanyApworld;
        public string lethalcompanyLethalAPI;
        public string lethalcompanyAPScrap;
        public string lethalcompanyLevelLoader;
        public string lethalcompanyLethalLib;
        public string lethalcompanyFixSerialize;
        public string lethalcompanyModData;
        public string lethalcompanyHook;
        public string lethalcompanyMonoDetourBep5;
        public string lethalcompanyMonoDetour;
        public string lethalcompanyMMHook;
    }

    void Start()
    {
        lethalCompanyPath = GetLethalCompanyPath();
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyLethalCompanyConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.lethalcompanyAP;
        bepInEx.url = remoteConfig.lethalcompanyBepInEx;
        apworld.url = remoteConfig.lethalcompanyApworld;
        lethalAPI.url = remoteConfig.lethalcompanyLethalAPI;
        apScrap.url = remoteConfig.lethalcompanyAPScrap;
        levelLoader.url = remoteConfig.lethalcompanyLevelLoader;
        lethalLib.url = remoteConfig.lethalcompanyLethalLib;
        fixSerialize.url = remoteConfig.lethalcompanyFixSerialize;
        modData.url = remoteConfig.lethalcompanyModData;
        hook.url = remoteConfig.lethalcompanyHook;
        monoDetourBep5.url = remoteConfig.lethalcompanyMonoDetourBep5;
        monoDetour.url = remoteConfig.lethalcompanyMonoDetour;
        mmhook.url = remoteConfig.lethalcompanyMMHook;
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
        if (string.IsNullOrEmpty(lethalCompanyPath))
        {
            ShowInfo("Lethal Company path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        lethalCompanyPath = GetLethalCompanyPath();

        if (string.IsNullOrEmpty(lethalCompanyPath))
            return;

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");

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

            SafeDeleteFile(Path.Combine(pluginsPath, "APLC.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago_Scrap.dll"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (hasOtherMods && !pendingFullCleanConfirmation)
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

        SafeDeleteFile(Path.Combine(pluginsPath, "APLC.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago_Scrap.dll"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(lethalCompanyPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(lethalCompanyPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(lethalCompanyPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(lethalCompanyPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(lethalCompanyPath, ".doorstop_version"));

            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        try
        {
            string[] files = Directory.GetFiles(pluginsPath);
            string[] dirs = Directory.GetDirectories(pluginsPath);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);

                if (name.StartsWith("LethalCompany APMod Version") && name.EndsWith(".txt"))
                    continue;

                if (name != "APLC.dll" &&
                    name != "Archipelago.MultiClient.Net.dll" &&
                    name != "Archipelago_Scrap.dll" &&
                    name != "LethalAPI.Terminal.dll" &&
                    name != "LethalLevelLoader.dll" &&
                    name != "LethalLib.dll" &&
                    name != "MaxWasUnavailable.LethalModDataLib.dll")
                    return true;
            }

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName != "MMHOOK")
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
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

        if (installLethalAPIToggle == null || installLethalAPIToggle.isOn)
        {
            ShowInfo("Installing LethalAPI...");
            yield return InstallLethalAPI();
        }

        if (installAPScrapToggle == null || installAPScrapToggle.isOn)
        {
            ShowInfo("Installing AP Scrap...");
            yield return InstallAPScrap();
        }

        if (installLevelLoaderToggle == null || installLevelLoaderToggle.isOn)
        {
            ShowInfo("Installing Level Loader...");
            yield return InstallLevelLoader();
        }

        if (installLethalLibToggle == null || installLethalLibToggle.isOn)
        {
            ShowInfo("Installing LethalLib...");
            yield return InstallLethalLib();
        }

        if (installFixSerializeToggle == null || installFixSerializeToggle.isOn)
        {
            ShowInfo("Installing Fix Serialize...");
            yield return InstallFixSerialize();
        }

        if (installModDataToggle == null || installModDataToggle.isOn)
        {
            ShowInfo("Installing Mod Data...");
            yield return InstallModData();
        }

        if (installHookToggle == null || installHookToggle.isOn)
        {
            ShowInfo("Installing Hook...");
            yield return InstallHook();
        }

        if (installMonoDetourBep5Toggle == null || installMonoDetourBep5Toggle.isOn)
        {
            ShowInfo("Installing MonoDetour BepInEx 5...");
            yield return InstallMonoDetourBep5();
        }

        if (installMonoDetourToggle == null || installMonoDetourToggle.isOn)
        {
            ShowInfo("Installing MonoDetour...");
            yield return InstallMonoDetour();
        }

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url, lethalAPI.url, apScrap.url, levelLoader.url, lethalLib.url, fixSerialize.url, modData.url, hook.url, monoDetourBep5.url, monoDetour.url);

        ShowInfo("Launching Lethal Company...");
        LaunchLethalCompany();

        yield return WaitForConfigFiles();

        CloseLethalCompany();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchLethalCompany();
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

        yield return DownloadFile(apworld.url, localPath);

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
            try
            {
                File.Delete(target);
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete!");
            }
        }
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "APModTemp");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(extractPath, "APLC.dll", pluginsPath);
        CopyFileIfExists(extractPath, "Archipelago.MultiClient.Net.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallLethalAPI()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "LethalAPITemp");
        yield return downloader.DownloadAndExtract(lethalAPI, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(Path.Combine(extractPath, "BepInEx", "plugins"), "LethalAPI.Terminal.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPScrap()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "APScrapTemp");
        yield return downloader.DownloadAndExtract(apScrap, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(extractPath, "Archipelago_Scrap.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallLevelLoader()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "LevelLoaderTemp");
        yield return downloader.DownloadAndExtract(levelLoader, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(Path.Combine(extractPath, "BepInEx", "plugins"), "LethalLevelLoader.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallLethalLib()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "LethalLibTemp");
        yield return downloader.DownloadAndExtract(lethalLib, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(Path.Combine(extractPath, "plugins", "LethalLib"), "LethalLib.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallFixSerialize()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "FixSerializeTemp");
        yield return downloader.DownloadAndExtract(fixSerialize, Application.persistentDataPath, extractPath);

        string patchersPath = Path.Combine(lethalCompanyPath, "BepInEx", "patchers");
        Directory.CreateDirectory(patchersPath);

        string fixSerializeSourcePath = Path.Combine(extractPath, "BepInEx", "patchers", "FixPluginTypesSerialization");

        if (Directory.Exists(fixSerializeSourcePath))
        {
            CopyFileIfExists(fixSerializeSourcePath, "FixPluginTypesSerialization.dll", patchersPath);
            CopyFileIfExists(fixSerializeSourcePath, "Microsoft.Deployment.Compression.Cab.dll", patchersPath);
            CopyFileIfExists(fixSerializeSourcePath, "Microsoft.Deployment.Compression.dll", patchersPath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("FixSerialize source folder not found: " + fixSerializeSourcePath);
        }

        string configPath = Path.Combine(lethalCompanyPath, "BepInEx", "config");
        Directory.CreateDirectory(configPath);
        CopyFileIfExists(Path.Combine(extractPath, "BepInEx", "config"), "FixPluginTypesSerialization.cfg", configPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallModData()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ModDataTemp");
        yield return downloader.DownloadAndExtract(modData, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        CopyFileIfExists(Path.Combine(extractPath, "BepInEx", "plugins", "LethalModDataLib"), "MaxWasUnavailable.LethalModDataLib.dll", pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallHook()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "HookTemp");
        yield return downloader.DownloadAndExtract(hook, Application.persistentDataPath, extractPath);

        string patchersPath = Path.Combine(lethalCompanyPath, "BepInEx", "patchers");
        Directory.CreateDirectory(patchersPath);

        string hookGenSourcePath = Path.Combine(extractPath, "patchers", "BepInEx.MonoMod.HookGenPatcher");

        if (Directory.Exists(hookGenSourcePath))
        {
            CopyFileIfExists(hookGenSourcePath, "BepInEx.MonoMod.HookGenPatcher.dll", patchersPath);
            CopyFileIfExists(hookGenSourcePath, "MonoMod.dll", patchersPath);
            CopyFileIfExists(hookGenSourcePath, "MonoMod.RuntimeDetour.HookGen.dll", patchersPath);
        }
        else
        {
            UnityEngine.Debug.LogWarning("HookGenPatcher source folder not found: " + hookGenSourcePath);
        }

        string configPath = Path.Combine(lethalCompanyPath, "BepInEx", "config");
        Directory.CreateDirectory(configPath);
        CopyFileIfExists(Path.Combine(extractPath, "config"), "HookGenPatcher.cfg", configPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMonoDetourBep5()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "MonoDetourBep5Temp");
        yield return downloader.DownloadAndExtract(monoDetourBep5, Application.persistentDataPath, extractPath);

        string patchersPath = Path.Combine(lethalCompanyPath, "BepInEx", "patchers");
        Directory.CreateDirectory(patchersPath);

        string corePath = Path.Combine(lethalCompanyPath, "BepInEx", "core");
        Directory.CreateDirectory(corePath);

        CopyFileIfExists(Path.Combine(extractPath, "core"), "com.github.MonoDetour.Interop.HarmonyX.dll", corePath);
        CopyFileIfExists(Path.Combine(extractPath, "patchers"), "0.com.github.MonoDetour.BepInEx.5.dll", patchersPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMonoDetour()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "MonoDetourTemp");
        yield return downloader.DownloadAndExtract(monoDetour, Application.persistentDataPath, extractPath);

        string corePath = Path.Combine(lethalCompanyPath, "BepInEx", "core");
        Directory.CreateDirectory(corePath);

        CopyFileIfExists(Path.Combine(extractPath, "core"), "com.github.MonoDetour.Bindings.Reorg.dll", corePath);
        CopyFileIfExists(Path.Combine(extractPath, "core"), "com.github.MonoDetour.dll", corePath);
        CopyFileIfExists(Path.Combine(extractPath, "core"), "com.github.MonoDetour.Reflection.dll", corePath);

        SafeDeleteDirectory(extractPath);
    }


    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, lethalCompanyPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(lethalCompanyPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (!File.Exists(cfg) && timer < timeout)
        {
            timer += 1f;
            yield return new WaitForSeconds(1f);
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
            remoteConfig = JsonUtility.FromJson<LethalCompanyConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyLethalCompanyConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchLethalCompany()
    {
        string exePath = Path.Combine(lethalCompanyPath, "Lethal Company.exe");

        if (File.Exists(exePath))
        {
            try
            {
                lethalCompanyProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Lethal Company launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Lethal Company:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseLethalCompany()
    {
        try
        {
            if (lethalCompanyProcess != null && !lethalCompanyProcess.HasExited)
            {
                lethalCompanyProcess.Kill();
                lethalCompanyProcess.Dispose();
                lethalCompanyProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseLethalCompany();
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

    void CopyFileIfExists(string sourceDir, string fileName, string targetDir)
    {
        string sourcePath = Path.Combine(sourceDir, fileName);

        if (File.Exists(sourcePath))
        {
            string targetPath = Path.Combine(targetDir, fileName);
            File.Copy(sourcePath, targetPath, true);
            UnityEngine.Debug.Log("Copied: " + fileName);
        }
        else
        {
            UnityEngine.Debug.LogWarning("File not found: " + sourcePath);
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

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    string GetLethalCompanyPath()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Lethal Company"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Lethal Company"),
        @"D:\Steam\steamapps\common\Lethal Company",
        @"D:\SteamLibrary\steamapps\common\Lethal Company",
        @"D:\steamapps\common\Lethal Company",
        @"E:\Steam\steamapps\common\Lethal Company",
        @"E:\SteamLibrary\steamapps\common\Lethal Company",
        @"E:\steamapps\common\Lethal Company",
        @"E:\Program Files (x86)\steamapps\common\Lethal Company",
        @"E:\Program Files\steamapps\common\Lethal Company",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Lethal Company at: " + path);
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
                    // Cherche Steam\steamapps
                    string lcPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Lethal Company");
                    if (Directory.Exists(lcPath))
                        return lcPath;

                    // Cherche SteamLibrary\steamapps
                    lcPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Lethal Company");
                    if (Directory.Exists(lcPath))
                        return lcPath;

                    // Cherche directement steamapps à la racine du disque
                    lcPath = Path.Combine(drive.Name, "steamapps", "common", "Lethal Company");
                    if (Directory.Exists(lcPath))
                        return lcPath;

                    // Cherche dans Program Files (x86)\steamapps
                    lcPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Lethal Company");
                    if (Directory.Exists(lcPath))
                        return lcPath;

                    // Cherche dans Program Files\steamapps
                    lcPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Lethal Company");
                    if (Directory.Exists(lcPath))
                        return lcPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Lethal Company not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string bepinexUrl, string apworldUrl, string lethalApiUrl, string apScrapUrl, string levelLoaderUrl, string lethalLibUrl, string fixSerializeUrl, string modDataUrl, string hookUrl, string monoDetourBep5Url, string monoDetourUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"/download/[^/]+/[^/]+/([^/]+)/?$");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");
            string lethalApiVersion = ExtractVersionFromUrl(lethalApiUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string apScrapVersion = ExtractVersionFromUrl(apScrapUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string levelLoaderVersion = ExtractVersionFromUrl(levelLoaderUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string lethalLibVersion = ExtractVersionFromUrl(lethalLibUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string fixSerializeVersion = ExtractVersionFromUrl(fixSerializeUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string modDataVersion = ExtractVersionFromUrl(modDataUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string hookVersion = ExtractVersionFromUrl(hookUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string monoDetourBep5Version = ExtractVersionFromUrl(monoDetourBep5Url, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string monoDetourVersion = ExtractVersionFromUrl(monoDetourUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");

            string versionFileName = "LethalCompany APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== LETHAL API ===\n";
            content += "Downloaded from: " + lethalApiUrl + "\n";
            content += "Version: " + lethalApiVersion + "\n";
            content += "\n";
            content += "=== AP SCRAP ===\n";
            content += "Downloaded from: " + apScrapUrl + "\n";
            content += "Version: " + apScrapVersion + "\n";
            content += "\n";
            content += "=== LEVEL LOADER ===\n";
            content += "Downloaded from: " + levelLoaderUrl + "\n";
            content += "Version: " + levelLoaderVersion + "\n";
            content += "\n";
            content += "=== LETHAL LIB ===\n";
            content += "Downloaded from: " + lethalLibUrl + "\n";
            content += "Version: " + lethalLibVersion + "\n";
            content += "\n";
            content += "=== FIX SERIALIZE ===\n";
            content += "Downloaded from: " + fixSerializeUrl + "\n";
            content += "Version: " + fixSerializeVersion + "\n";
            content += "\n";
            content += "=== MOD DATA ===\n";
            content += "Downloaded from: " + modDataUrl + "\n";
            content += "Version: " + modDataVersion + "\n";
            content += "\n";
            content += "=== HOOK ===\n";
            content += "Downloaded from: " + hookUrl + "\n";
            content += "Version: " + hookVersion + "\n";
            content += "\n";
            content += "=== MONO DETOUR BEP5 ===\n";
            content += "Downloaded from: " + monoDetourBep5Url + "\n";
            content += "Version: " + monoDetourBep5Version + "\n";
            content += "\n";
            content += "=== MONO DETOUR ===\n";
            content += "Downloaded from: " + monoDetourUrl + "\n";
            content += "Version: " + monoDetourVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(lethalCompanyPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"LethalCompany APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(lethalCompanyPath);
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

            string pluginsPath = Path.Combine(lethalCompanyPath, "BepInEx", "plugins");
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
        // Pattern pour Thunderstore: https://thunderstore.io/package/download/Author/Package/VERSION/
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
