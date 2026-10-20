using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;

public class SilksongManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SILKSONG FILES")]
    public FileDownloader.FileData hksilksongApworld;
    public FileDownloader.FileData hksilksongAP;
    public FileDownloader.FileData hksilksongBepInEx;
    public FileDownloader.FileData hksilksongBench;

    [Header("BENCHWARP DEPENDENCIES (installed with the Bench toggle)")]
    public FileDownloader.FileData hksilksongAssetHelper;
    public FileDownloader.FileData hksilksongMonoDetourBep5;
    public FileDownloader.FileData hksilksongMonoDetour;
    public FileDownloader.FileData hksilksongI18N;
    public FileDownloader.FileData hksilksongDataManager;
    public FileDownloader.FileData hksilksongFsmUtil;
    public FileDownloader.FileData hksilksongModMenu;
    public FileDownloader.FileData hksilksongUnityHelper;
    public FileDownloader.FileData hksilksongWavLib;
    public FileDownloader.FileData hksilksongPrepatcher;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Hollow Knight Silksong";


    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installAPToggle;
    public Toggle installBepInExToggle;
    public Toggle installBenchToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchAfterSetupToggle;

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

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private SilksongConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    private const string AP_FOLDER_PREFIX = "SilksongRandomizer";
    private const string BENCH_FOLDER_NAME = "Benchwarp";
    private const string AP2_PUT_IN_FOLDER_NAME = "PUT IN SILKSONG FOLDER";

    private static readonly string[] BENCH_EXTRA_PLUGIN_FOLDERS = new string[]
    {
        "AssetHelper",
        "I18N",
        "DataManager",
        "FsmUtil",
        "ModMenu",
        "UnityHelper",
        "WavLib"
    };

    [System.Serializable]
    public class SilksongConfig
    {
        public string hksilksongApworld;
        public string hksilksongBepInEx;
        public string hksilksongAP;
        public string hksilksongBench;
        public string hksilksongBenchDep;
        public string hksilksongAssetHelper;
        public string hksilksongMonoDetourBep5;
        public string hksilksongMonoDetour;
        public string hksilksongI18N;
        public string hksilksongDataManager;
        public string hksilksongFsmUtil;
        public string hksilksongModMenu;
        public string hksilksongUnityHelper;
        public string hksilksongWavLib;
        public string hksilksongPrepatcher;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;

    }

    void Start()
    {
        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchAfterSetupToggle != null)
            launchAfterSetupToggle.isOn = false;

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

        if (installBenchToggle != null)
            installBenchToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyConfig()
    {
        if (remoteConfig == null)
            return;

        hksilksongAP.url = remoteConfig.hksilksongAP;
        hksilksongBepInEx.url = remoteConfig.hksilksongBepInEx;
        hksilksongApworld.url = remoteConfig.hksilksongApworld;
        hksilksongBench.url = remoteConfig.hksilksongBench;

        hksilksongAssetHelper.url = remoteConfig.hksilksongAssetHelper;
        hksilksongMonoDetourBep5.url = remoteConfig.hksilksongMonoDetourBep5;
        hksilksongMonoDetour.url = remoteConfig.hksilksongMonoDetour;
        hksilksongI18N.url = remoteConfig.hksilksongI18N;
        hksilksongDataManager.url = remoteConfig.hksilksongDataManager;
        hksilksongFsmUtil.url = remoteConfig.hksilksongFsmUtil;
        hksilksongModMenu.url = remoteConfig.hksilksongModMenu;
        hksilksongUnityHelper.url = remoteConfig.hksilksongUnityHelper;
        hksilksongWavLib.url = remoteConfig.hksilksongWavLib;
        hksilksongPrepatcher.url = remoteConfig.hksilksongPrepatcher;
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
        gamePath = GetGamePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;
        bool bench = installBenchToggle != null && installBenchToggle.isOn;

        bool needsGamePath = bep || ap || bench;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (ap ? 1 : 0) +
            (bench ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (bep && count == 1) { StartCoroutine(BepInExOnlyFlow()); return; }
        if (ap && count == 1) { StartCoroutine(APOnlyFlow()); return; }
        if (bench && count == 1) { StartCoroutine(BenchOnlyFlow()); return; }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
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

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
        {
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            return;

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP && !fullClean)
        {
            CleanupProcesses();

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            RemoveAPModsFolder(pluginsPath);
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

        if (removeAP)
        {
            ShowInfo("Removing AP mods...");

            RemoveAPModsFolder(pluginsPath);
            DeleteOldVersionFiles();
        }

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx"));
        SafeDeleteFile(Path.Combine(gamePath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(gamePath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(gamePath, ".doorstop_version"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    bool IsAPManagedFolder(string name)
    {
        return name.StartsWith(AP_FOLDER_PREFIX)
            || name == BENCH_FOLDER_NAME
            || BENCH_EXTRA_PLUGIN_FOLDERS.Contains(name);
    }

    void RemoveAPModsFolder(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return;

        foreach (string dir in Directory.GetDirectories(pluginsPath))
        {
            string name = Path.GetFileName(dir);

            if (IsAPManagedFolder(name))
                SafeDeleteDirectory(dir);
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

            if (name.StartsWith(AP_FOLDER_PREFIX) && name.EndsWith(".txt"))
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (IsAPManagedFolder(name))
                continue;

            return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPToggle == null || installAPToggle.isOn)
        {
            ShowInfo("Installing Archipelago Mod...");
            yield return InstallAP();
        }

        if (installBenchToggle != null && installBenchToggle.isOn)
        {
            ShowInfo("Installing Benchwarp...");
            yield return InstallBench();
        }

        CreateVersionFile(hksilksongAP.url, hksilksongBepInEx.url, hksilksongApworld.url, hksilksongBench.url);

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Hollow Knight Silksong...");
            LaunchGame();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + hksilksongApworld.url);

        if (string.IsNullOrEmpty(hksilksongApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = hksilksongApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = hksilksongApworld.url.Substring(hksilksongApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + hksilksongApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(hksilksongApworld.url, localPath);

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

    IEnumerator InstallAP()
    {
        while (!configLoaded)
            yield return null;

        if (hksilksongAP == null || string.IsNullOrEmpty(hksilksongAP.url))
        {
            ShowInfo("ERROR: AP mod URL is empty!");
            UnityEngine.Debug.LogError("hksilksongAP2 URL not set!");
            yield break;
        }

        string unrarExe = Path.Combine(Application.streamingAssetsPath, "RAR", "UnRAR.exe");

        if (!File.Exists(unrarExe))
        {
            ShowInfo("ERROR: UnRAR.exe not found!");
            UnityEngine.Debug.LogError("UnRAR.exe not found at: " + unrarExe);
            yield break;
        }

        string rarPath = Path.Combine(Application.persistentDataPath, "SilksongAP2.rar");
        string extractPath = Path.Combine(Application.persistentDataPath, "SilksongAP2Temp");

        // Clean leftovers from a previous run
        SafeDeleteDirectory(extractPath);
        DeleteTempFile(rarPath);

        // 1) Download the .rar
        yield return DownloadFile(hksilksongAP.url, rarPath);

        if (!File.Exists(rarPath))
        {
            ShowInfo("ERROR: AP mod download failed!");
            UnityEngine.Debug.LogError("AP2 download failed: file not found at " + rarPath);
            yield break;
        }

        Task<bool> extractTask = ExtractRarAsync(unrarExe, rarPath, extractPath);
        yield return new WaitUntil(() => extractTask.IsCompleted);

        if (!extractTask.Result)
        {
            ShowInfo("ERROR: Could not extract the AP mod archive!");
            DeleteTempFile(rarPath);
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string putInFolder = FindDirInDir(extractPath, AP2_PUT_IN_FOLDER_NAME);

        if (string.IsNullOrEmpty(putInFolder))
        {
            UnityEngine.Debug.LogError("'" + AP2_PUT_IN_FOLDER_NAME + "' folder not found inside the AP mod archive.");
            ShowInfo("ERROR: '" + AP2_PUT_IN_FOLDER_NAME + "' folder not found in the archive!");
            DeleteTempFile(rarPath);
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        try
        {
            Directory.CreateDirectory(gamePath);
            MoveDirectory(putInFolder, gamePath);
            UnityEngine.Debug.Log("AP mod installed into: " + gamePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install AP mod: " + e.Message);
            ShowInfo("ERROR: Failed to install AP mod\n" + e.Message);
        }

        DeleteTempFile(rarPath);
        SafeDeleteDirectory(extractPath);
    }

    Task<bool> ExtractRarAsync(string unrarExe, string rarPath, string outputDir)
    {
        return Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(outputDir);

                var psi = new ProcessStartInfo
                {
                    FileName = unrarExe,
                    Arguments = "x -y -o+ -p- \"" + rarPath + "\"",
                    WorkingDirectory = outputDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process p = Process.Start(psi))
                {
                    Task<string> outTask = p.StandardOutput.ReadToEndAsync();
                    Task<string> errTask = p.StandardError.ReadToEndAsync();

                    p.WaitForExit();

                    string output = outTask.Result;
                    string error = errTask.Result;

                    if (p.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError("UnRAR failed (code " + p.ExitCode + "): " + error + "\n" + output);
                        return false;
                    }

                    return true;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("UnRAR exception: " + e.Message);
                return false;
            }
        });
    }

    IEnumerator InstallBench()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(hksilksongBench.url))
        {
            ShowInfo("ERROR: Benchwarp URL is empty!");
            UnityEngine.Debug.LogError("Benchwarp URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "SilksongBenchTemp");

        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(hksilksongBench, Application.persistentDataPath, extractPath);

        string sourcePluginsDir = null;

        if (Directory.Exists(extractPath))
        {
            sourcePluginsDir = Directory
                .GetDirectories(extractPath, "plugins", SearchOption.AllDirectories)
                .OrderBy(d => d.Length)
                .FirstOrDefault();
        }

        if (string.IsNullOrEmpty(sourcePluginsDir))
        {
            UnityEngine.Debug.LogError("No 'plugins' folder found inside the Benchwarp zip.");
            ShowInfo("ERROR: 'plugins' folder not found in the Benchwarp archive!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string benchTarget = Path.Combine(gamePath, "BepInEx", "plugins", BENCH_FOLDER_NAME);

        // Remove any previous install so old files don't linger
        SafeDeleteDirectory(benchTarget);
        Directory.CreateDirectory(benchTarget);

        MoveDirectory(sourcePluginsDir, benchTarget);

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("Benchwarp installed to: " + benchTarget);

        // ---- Additional Benchwarp dependencies ----
        yield return InstallBenchExtras();
    }

    // =====================================================================
    //  BENCHWARP EXTRA DEPENDENCIES
    // =====================================================================

    IEnumerator InstallBenchExtras()
    {
        string bepInExPath = Path.Combine(gamePath, "BepInEx");
        string pluginsPath = Path.Combine(bepInExPath, "plugins");
        Directory.CreateDirectory(pluginsPath);

        // AssetHelper: everything inside the zip's subfolder -> BepInEx\plugins\AssetHelper
        yield return DownloadAndPlace(hksilksongAssetHelper, "SilksongAssetHelperTemp", "AssetHelper", extract =>
        {
            string sub = Directory.GetDirectories(extract).OrderBy(d => d.Length).FirstOrDefault();
            string source = string.IsNullOrEmpty(sub) ? extract : sub;

            string target = Path.Combine(pluginsPath, "AssetHelper");
            SafeDeleteDirectory(target);
            Directory.CreateDirectory(target);
            MoveDirectory(source, target);
        });

        // MonoDetour BepInEx 5: "core" + "patchers" -> BepInEx
        yield return DownloadAndPlace(hksilksongMonoDetourBep5, "SilksongMonoDetourBep5Temp", "MonoDetour (BepInEx 5)", extract =>
        {
            MoveNamedDirectory(extract, "core", Path.Combine(bepInExPath, "core"));
            MoveNamedDirectory(extract, "patchers", Path.Combine(bepInExPath, "patchers"));
        });

        // MonoDetour: "core" -> BepInEx
        yield return DownloadAndPlace(hksilksongMonoDetour, "SilksongMonoDetourTemp", "MonoDetour", extract =>
        {
            MoveNamedDirectory(extract, "core", Path.Combine(bepInExPath, "core"));
        });

        // I18N: dll/pdb/xml -> BepInEx\plugins\I18N
        yield return DownloadAndPlace(hksilksongI18N, "SilksongI18NTemp", "I18N", extract =>
        {
            CopyNamedFiles(extract,
                new[] { "Silksong.I18N.dll", "Silksong.I18N.pdb", "Silksong.I18N.xml" },
                Path.Combine(pluginsPath, "I18N"));
        });

        // DataManager: dll/pdb/xml + "languages" folder -> BepInEx\plugins\DataManager
        yield return DownloadAndPlace(hksilksongDataManager, "SilksongDataManagerTemp", "DataManager", extract =>
        {
            string target = Path.Combine(pluginsPath, "DataManager");

            CopyNamedFiles(extract,
                new[] { "Silksong.DataManager.dll", "Silksong.DataManager.pdb", "Silksong.DataManager.xml" },
                target);

            MoveNamedDirectory(extract, "languages", Path.Combine(target, "languages"));
        });

        // FsmUtil: dll/pdb/xml -> BepInEx\plugins\FsmUtil
        yield return DownloadAndPlace(hksilksongFsmUtil, "SilksongFsmUtilTemp", "FsmUtil", extract =>
        {
            CopyNamedFiles(extract,
                new[] { "Silksong.FsmUtil.dll", "Silksong.FsmUtil.pdb", "Silksong.FsmUtil.xml" },
                Path.Combine(pluginsPath, "FsmUtil"));
        });

        // ModMenu: dll/pdb/xml -> BepInEx\plugins\ModMenu
        yield return DownloadAndPlace(hksilksongModMenu, "SilksongModMenuTemp", "ModMenu", extract =>
        {
            CopyNamedFiles(extract,
                new[] { "Silksong.ModMenu.dll", "Silksong.ModMenu.pdb", "Silksong.ModMenu.xml" },
                Path.Combine(pluginsPath, "ModMenu"));
        });

        // UnityHelper: dll/pdb/xml -> BepInEx\plugins\UnityHelper
        yield return DownloadAndPlace(hksilksongUnityHelper, "SilksongUnityHelperTemp", "UnityHelper", extract =>
        {
            CopyNamedFiles(extract,
                new[] { "UnityHelper.dll", "UnityHelper.pdb", "UnityHelper.xml" },
                Path.Combine(pluginsPath, "UnityHelper"));
        });

        // WavLib: dll/pdb/xml -> BepInEx\plugins\WavLib
        yield return DownloadAndPlace(hksilksongWavLib, "SilksongWavLibTemp", "WavLib", extract =>
        {
            CopyNamedFiles(extract,
                new[] { "WavLib.dll", "WavLib.pdb", "WavLib.xml" },
                Path.Combine(pluginsPath, "WavLib"));
        });

        // Prepatcher: "patchers" + "plugins" -> BepInEx
        yield return DownloadAndPlace(hksilksongPrepatcher, "SilksongPrepatcherTemp", "Prepatcher", extract =>
        {
            MoveNamedDirectory(extract, "patchers", Path.Combine(bepInExPath, "patchers"));
            MoveNamedDirectory(extract, "plugins", Path.Combine(bepInExPath, "plugins"));
        });
    }

    // Downloads + extracts a zip into a temp folder, runs the placement callback, then cleans up.
    IEnumerator DownloadAndPlace(FileDownloader.FileData data, string tempFolderName, string label, System.Action<string> place)
    {
        if (data == null || string.IsNullOrEmpty(data.url))
        {
            UnityEngine.Debug.LogWarning(label + " URL is empty, skipping.");
            yield break;
        }

        ShowInfo("Installing " + label + "...");

        string extractPath = Path.Combine(Application.persistentDataPath, tempFolderName);

        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(data, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError(label + " extraction failed.");
            ShowInfo("ERROR: " + label + " could not be extracted!");
            yield break;
        }

        try
        {
            place(extractPath);
            UnityEngine.Debug.Log(label + " installed.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install " + label + ": " + e.Message);
            ShowInfo("ERROR: Failed to install " + label + "\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    string FindFileInDir(string root, string fileName)
    {
        return Directory
            .GetFiles(root, fileName, SearchOption.AllDirectories)
            .OrderBy(f => f.Length)
            .FirstOrDefault();
    }

    string FindDirInDir(string root, string dirName)
    {
        return Directory
            .GetDirectories(root, dirName, SearchOption.AllDirectories)
            .OrderBy(d => d.Length)
            .FirstOrDefault();
    }

    void CopyNamedFiles(string root, string[] fileNames, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string fileName in fileNames)
        {
            string found = FindFileInDir(root, fileName);

            if (string.IsNullOrEmpty(found))
            {
                UnityEngine.Debug.LogWarning("File not found in archive: " + fileName);
                continue;
            }

            File.Copy(found, Path.Combine(targetDir, fileName), true);
        }
    }

    void MoveNamedDirectory(string root, string dirName, string targetDir)
    {
        string found = FindDirInDir(root, dirName);

        if (string.IsNullOrEmpty(found))
        {
            UnityEngine.Debug.LogWarning("Folder not found in archive: " + dirName);
            return;
        }

        Directory.CreateDirectory(targetDir);
        MoveDirectory(found, targetDir);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(hksilksongBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, gamePath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Hollow Knight Silksong...");
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator APOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAP();

        CreateVersionFile(hksilksongAP.url, hksilksongBepInEx.url, hksilksongApworld.url, hksilksongBench.url);

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
            LaunchGame();

        ShowInfo("Installation complete!");
    }

    IEnumerator BenchOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        ShowInfo("Installing Benchwarp...");
        yield return InstallBench();

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Hollow Knight Silksong...");
            LaunchGame();
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
            remoteConfig = JsonUtility.FromJson<SilksongConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        gamePath = GetGamePath();
    }

    void LaunchGame()
    {
        string exePath = Path.Combine(gamePath, "Hollow Knight Silksong.exe");

        if (File.Exists(exePath))
            gameProcess = Process.Start(exePath);
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void CreateVersionFile(string apUrl, string bepinexUrl, string apworldUrl, string benchUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string benchVersion = ExtractVersionFromUrl(benchUrl, @"/releases/download/([^/]+)/");

            string versionFileName = AP_FOLDER_PREFIX + " Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== SILKSONG RANDOMIZER ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Origin: https://github.com/Batatvideogames/silksong-archipelago-randomizer/releases/tag/v0.4.2\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== BENCHWARP ===\n";
            content += "Downloaded from: " + benchUrl + "\n";
            content += "Version: " + benchVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(AP_FOLDER_PREFIX + @" Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
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

            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
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
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
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