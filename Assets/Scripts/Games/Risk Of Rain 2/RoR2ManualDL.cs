using Microsoft.Win32;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoR2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ROR2 FILES")]
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData bepInExGUI;
    public FileDownloader.FileData ror2BepInExPack;
    public FileDownloader.FileData ror2AP;
    public FileDownloader.FileData hookGenPatcher;
    public FileDownloader.FileData ror2MiscFixes;
    public FileDownloader.FileData ror2SeekersPatcher;
    public FileDownloader.FileData ror2FixSerialization;

    [Header("R2API DEPENDENCIES")]
    public FileDownloader.FileData ror2API;
    public FileDownloader.FileData ror2APICore;
    public FileDownloader.FileData ror2APIAddressables;
    public FileDownloader.FileData ror2APIContentManagement;
    public FileDownloader.FileData ror2APINetworking;
    public FileDownloader.FileData ror2APIRecalculateStats;
    public FileDownloader.FileData ror2APIDamageType;
    public FileDownloader.FileData ror2APIDifficulty;
    public FileDownloader.FileData ror2APIOrb;
    public FileDownloader.FileData ror2APILobbyConfig;
    public FileDownloader.FileData ror2APILanguage;
    public FileDownloader.FileData ror2APIDot;
    public FileDownloader.FileData ror2APIDeployable;
    public FileDownloader.FileData ror2APICommandHelper;
    public FileDownloader.FileData ror2APIArtifactCode;
    public FileDownloader.FileData ror2APIDirector;
    public FileDownloader.FileData ror2APIUnlockable;
    public FileDownloader.FileData ror2APITempVisualEffect;
    public FileDownloader.FileData ror2APISound;
    public FileDownloader.FileData ror2APIPrefab;
    public FileDownloader.FileData ror2APILoadout;
    public FileDownloader.FileData ror2APIItems;
    public FileDownloader.FileData ror2APIElites;
    public FileDownloader.FileData ror2APISceneAsset;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;
    public Toggle installR2APIToggle;
    public Toggle installMiscFixesToggle;
    public Toggle installSeekersPatcjerToggle;
    public Toggle installFixSerializationToggle;
    public Toggle installHookGenPatcherToggle;

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

    private Process ror2Process;
    private string ror2Path;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private RoR2Config remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    // Track which mods were actually installed
    private List<(string name, string url, string version)> installedMods = new List<(string, string, string)>();

    [System.Serializable]
    public class RoR2Config
    {
        public string ror2BepInEx;
        public string ror2BepInExGUI;
        public string ror2BepInExPack;
        public string ror2AP;
        public string ror2HookGenPatcher;
        public string ror2MiscFixes;
        public string ror2SeekersPatcher;
        public string ror2FixSerialization;
        public string ror2API;
        public string ror2APICore;
        public string ror2APIAddressables;
        public string ror2APIContentManagement;
        public string ror2APINetworking;
        public string ror2APIRecalculateStats;
        public string ror2APIDamageType;
        public string ror2APIDifficulty;
        public string ror2APIOrb;
        public string ror2APILobbyConfig;
        public string ror2APILanguage;
        public string ror2APIDot;
        public string ror2APIDeployable;
        public string ror2APICommandHelper;
        public string ror2APIArtifactCode;
        public string ror2APIDirector;
        public string ror2APIUnlockable;
        public string ror2APITempVisualEffect;
        public string ror2APISound;
        public string ror2APIPrefab;
        public string ror2APILoadout;
        public string ror2APIItems;
        public string ror2APIElites;
        public string ror2APISceneAsset;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        ror2Path = GetRoR2Path();
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
        ror2Path = GetRoR2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + ror2Path);
    }

    void SelectEpic()
    {
        isEpic = true;
        ror2Path = GetRoR2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + ror2Path);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(ror2Path) ? "Not Found" : "Found";
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

    void ApplyRoR2Config()
    {
        if (remoteConfig == null)
            return;

        bepInEx.url = remoteConfig.ror2BepInEx;
        bepInExGUI.url = remoteConfig.ror2BepInExGUI;
        ror2BepInExPack.url = remoteConfig.ror2BepInExPack;
        ror2AP.url = remoteConfig.ror2AP;
        hookGenPatcher.url = remoteConfig.ror2HookGenPatcher;
        ror2MiscFixes.url = remoteConfig.ror2MiscFixes;
        ror2SeekersPatcher.url = remoteConfig.ror2SeekersPatcher;
        ror2FixSerialization.url = remoteConfig.ror2FixSerialization;
        ror2API.url = remoteConfig.ror2API;
        ror2APICore.url = remoteConfig.ror2APICore;
        ror2APIAddressables.url = remoteConfig.ror2APIAddressables;
        ror2APIContentManagement.url = remoteConfig.ror2APIContentManagement;
        ror2APINetworking.url = remoteConfig.ror2APINetworking;
        ror2APIRecalculateStats.url = remoteConfig.ror2APIRecalculateStats;
        ror2APIDamageType.url = remoteConfig.ror2APIDamageType;
        ror2APIDifficulty.url = remoteConfig.ror2APIDifficulty;
        ror2APIOrb.url = remoteConfig.ror2APIOrb;
        ror2APILobbyConfig.url = remoteConfig.ror2APILobbyConfig;
        ror2APILanguage.url = remoteConfig.ror2APILanguage;
        ror2APIDot.url = remoteConfig.ror2APIDot;
        ror2APIDeployable.url = remoteConfig.ror2APIDeployable;
        ror2APICommandHelper.url = remoteConfig.ror2APICommandHelper;
        ror2APIArtifactCode.url = remoteConfig.ror2APIArtifactCode;
        ror2APIDirector.url = remoteConfig.ror2APIDirector;
        ror2APIUnlockable.url = remoteConfig.ror2APIUnlockable;
        ror2APITempVisualEffect.url = remoteConfig.ror2APITempVisualEffect;
        ror2APISound.url = remoteConfig.ror2APISound;
        ror2APIPrefab.url = remoteConfig.ror2APIPrefab;
        ror2APILoadout.url = remoteConfig.ror2APILoadout;
        ror2APIItems.url = remoteConfig.ror2APIItems;
        ror2APIElites.url = remoteConfig.ror2APIElites;
        ror2APISceneAsset.url = remoteConfig.ror2APISceneAsset;
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
        ror2Path = GetRoR2Path();

        if (string.IsNullOrEmpty(ror2Path))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Risk of Rain 2 not found in " + platform + ". Please check installation.");
            return;
        }

        installedMods.Clear();
        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        ror2Path = GetRoR2Path();

        if (string.IsNullOrEmpty(ror2Path))
            return;

        string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
        string patchersPath = Path.Combine(ror2Path, "BepInEx", "patchers");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, "Sneaki-Archipelago"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

            if (Directory.Exists(patchersPath))
            {
                SafeDeleteDirectory(Path.Combine(patchersPath, "BepInEx.MonoMod.HookGenPatcher"));
            }

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

        SafeDeleteDirectory(Path.Combine(pluginsPath, "Sneaki-Archipelago"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(ror2Path, "BepInEx"));
            SafeDeleteFile(Path.Combine(ror2Path, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(ror2Path, "changelog.txt"));
            SafeDeleteFile(Path.Combine(ror2Path, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(ror2Path, ".doorstop_version"));

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

            string[] allowedFiles = new string[]
            {
            "connectbundle",
            "newtonsoft.json.dll",
            "ror2bepinexpack.dll",
            "miscfixes.dll",
            "archipelago.multiclient.net.dll",
            "archipelago.riskofrain2.dll",
            "archipelago.riskofrain2.deps.json",
            "archipelago.riskofrain2.pdb",
            "icon.png",
            "manifest.json",
            "changelog.md",
            "readme.md"
            };

            string[] allowedDirs = new string[]
            {
            "MMHOOK",
            "R2API.Addressables",
            "R2API.ArtifactCode",
            "R2API.CommandHelper",
            "R2API.ContentManagement",
            "R2API.Core",
            "R2API.DamageType",
            "R2API.Deployable",
            "R2API.Difficulty",
            "R2API.Director",
            "R2API.Dot",
            "R2API.Elites",
            "R2API.Items",
            "R2API.Language",
            "R2API.Legacy",
            "R2API.Loadout",
            "R2API.LobbyConfig",
            "R2API.Networking",
            "R2API.Orb",
            "R2API.Prefab",
            "R2API.RecalculateStats",
            "R2API.SceneAsset",
            "R2API.Sound",
            "R2API.TempVisualEffect",
            "R2API.Unlockable",
            "Sneaki-Archipelago",
            "Archipelago"
            };

            foreach (string file in files)
            {
                string name = Path.GetFileName(file).ToLower();

                // Skip version files
                if (name.StartsWith("risk of rain 2 archipelago version") && name.EndsWith(".txt"))
                    continue;

                bool isAllowed = false;
                foreach (string allowed in allowedFiles)
                {
                    if (string.Equals(name, allowed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    UnityEngine.Debug.LogWarning("Found other mod file: " + name);
                    return true;
                }
            }

            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);

                bool isAllowed = false;
                foreach (string allowed in allowedDirs)
                {
                    if (string.Equals(dirName, allowed, System.StringComparison.OrdinalIgnoreCase))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    UnityEngine.Debug.LogWarning("Found other mod directory: " + dirName);
                    return true;
                }
            }

            return false;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error checking for other mods: " + e.Message);
            return false;
        }
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
            installedMods.Add(("BepInEx", bepInEx.url, ExtractVersionFromUrl(bepInEx.url)));

            ShowInfo("Installing BepInEx GUI...");
            yield return InstallToPath(bepInExGUI, "BepInEx");
            installedMods.Add(("BepInEx GUI", bepInExGUI.url, ExtractVersionFromUrl(bepInExGUI.url)));

            ShowInfo("Installing BepInExPack...");
            yield return InstallBepInExPack();
            installedMods.Add(("BepInExPack", ror2BepInExPack.url, ExtractVersionFromUrl(ror2BepInExPack.url)));
        }

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing Archipelago Mod...");
            yield return InstallRoR2AP();
            installedMods.Add(("Archipelago", ror2AP.url, ExtractVersionFromUrl(ror2AP.url)));
        }

        if (installHookGenPatcherToggle != null && installHookGenPatcherToggle.isOn)
        {
            ShowInfo("Installing HookGenPatcher...");
            yield return InstallHookGenPatcher();
            installedMods.Add(("HookGenPatcher", hookGenPatcher.url, ExtractVersionFromUrl(hookGenPatcher.url)));
        }

        if (installFixSerializationToggle != null && installFixSerializationToggle.isOn)
        {
            ShowInfo("Installing FixPluginTypesSerialization...");
            yield return InstallToPath(ror2FixSerialization, "BepInEx");
            installedMods.Add(("FixPluginTypesSerialization", ror2FixSerialization.url, ExtractVersionFromUrl(ror2FixSerialization.url)));
        }

        if (installSeekersPatcjerToggle != null && installSeekersPatcjerToggle.isOn)
        {
            ShowInfo("Installing SeekersPatcher...");
            yield return InstallToPath(ror2SeekersPatcher, "BepInEx");
            installedMods.Add(("SeekersPatcher", ror2SeekersPatcher.url, ExtractVersionFromUrl(ror2SeekersPatcher.url)));
        }

        if (installMiscFixesToggle != null && installMiscFixesToggle.isOn)
        {
            ShowInfo("Installing MiscFixes...");
            string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsPath);
            yield return InstallMiscFixes(ror2MiscFixes, pluginsPath);
            installedMods.Add(("MiscFixes", ror2MiscFixes.url, ExtractVersionFromUrl(ror2MiscFixes.url)));
        }

        if (installR2APIToggle != null && installR2APIToggle.isOn)
        {
            ShowInfo("Installing R2API...");
            yield return InstallR2API();
        }

        CreateVersionFile();

        ShowInfo("Launching Risk of Rain 2...");
        LaunchRoR2();

        yield return WaitForConfigFiles();

        CloseRoR2();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchRoR2();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        string bepinexPackPath = Path.Combine(extractPath, "BepInExPack");

        if (!Directory.Exists(bepinexPackPath))
        {
            UnityEngine.Debug.LogWarning("BepInExPack folder not found, trying direct content");
            bepinexPackPath = extractPath;
        }

        MoveDirectory(bepinexPackPath, ror2Path);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallHookGenPatcher()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "HookGenPatcherTemp");
        yield return downloader.DownloadAndExtract(hookGenPatcher, Application.persistentDataPath, extractPath);

        string patchersPath = Path.Combine(ror2Path, "BepInEx", "patchers");
        Directory.CreateDirectory(patchersPath);

        string hookGenSourcePath = Path.Combine(extractPath, "BepInEx", "patchers", "BepInEx.MonoMod.HookGenPatcher");

        if (Directory.Exists(hookGenSourcePath))
        {
            CopyFileIfExists(hookGenSourcePath, "BepInEx.MonoMod.HookGenPatcher.dll", patchersPath);
            CopyFileIfExists(hookGenSourcePath, "MonoMod.dll", patchersPath);
            CopyFileIfExists(hookGenSourcePath, "MonoMod.RuntimeDetour.HookGen.dll", patchersPath);
            UnityEngine.Debug.Log("Copied HookGenPatcher files");
        }

        string mmhookSourcePath = Path.Combine(extractPath, "BepInEx", "plugins", "MMHOOK");
        string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);
        string mmhookTargetPath = Path.Combine(pluginsPath, "MMHOOK");

        if (Directory.Exists(mmhookSourcePath))
        {
            if (Directory.Exists(mmhookTargetPath))
                SafeDeleteDirectory(mmhookTargetPath);

            CopyDirectory(mmhookSourcePath, mmhookTargetPath);
            UnityEngine.Debug.Log("Copied MMHOOK folder");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallToPath(FileDownloader.FileData mod, string bepinexFolder)
    {
        while (!configLoaded)
            yield return null;

        string patchersPath = Path.Combine(ror2Path, "BepInEx", "patchers");
        string configPath = Path.Combine(ror2Path, "BepInEx", "config");
        Directory.CreateDirectory(patchersPath);
        Directory.CreateDirectory(configPath);

        string tempExtractPath = Path.Combine(Application.persistentDataPath, "Temp_" + System.Guid.NewGuid().ToString().Substring(0, 8));

        yield return downloader.DownloadAndExtract(mod, Application.persistentDataPath, tempExtractPath);

        try
        {
            string bepinexPath = Path.Combine(tempExtractPath, "BepInEx");

            if (Directory.Exists(bepinexPath))
            {
                string sourcePatchersPath = Path.Combine(bepinexPath, "patchers");
                if (Directory.Exists(sourcePatchersPath))
                {
                    foreach (string dir in Directory.GetDirectories(sourcePatchersPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        string targetPath = Path.Combine(patchersPath, dirName);
                        if (Directory.Exists(targetPath))
                            SafeDeleteDirectory(targetPath);
                        CopyDirectory(dir, targetPath);
                        UnityEngine.Debug.Log("Copied patcher: " + dirName);
                    }
                }

                string sourceConfigPath = Path.Combine(bepinexPath, "config");
                if (Directory.Exists(sourceConfigPath))
                {
                    foreach (string file in Directory.GetFiles(sourceConfigPath))
                    {
                        File.Copy(file, Path.Combine(configPath, Path.GetFileName(file)), true);
                        UnityEngine.Debug.Log("Copied config: " + Path.GetFileName(file));
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing component: " + e.Message);
        }

        SafeDeleteDirectory(tempExtractPath);
    }

    IEnumerator InstallMiscFixes(FileDownloader.FileData mod, string pluginsPath)
    {
        while (!configLoaded)
            yield return null;

        string tempExtractPath = Path.Combine(Application.persistentDataPath, "MiscFixes_Temp_" + System.Guid.NewGuid().ToString().Substring(0, 8));

        yield return downloader.DownloadAndExtract(mod, Application.persistentDataPath, tempExtractPath);

        try
        {
            string[] dllFiles = Directory.GetFiles(tempExtractPath, "MiscFixes.dll", SearchOption.AllDirectories);
            if (dllFiles.Length > 0)
            {
                File.Copy(dllFiles[0], Path.Combine(pluginsPath, "MiscFixes.dll"), true);
                UnityEngine.Debug.Log("Copied MiscFixes.dll");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing MiscFixes: " + e.Message);
        }

        SafeDeleteDirectory(tempExtractPath);
    }

    IEnumerator InstallBepInExPack()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(ror2BepInExPack.url))
            yield break;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExPack_Temp");
        yield return downloader.DownloadAndExtract(ror2BepInExPack, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        try
        {
            string[] bepinexPackDirs = Directory.GetDirectories(extractPath, "RoR2BepInExPack", SearchOption.AllDirectories);

            if (bepinexPackDirs.Length > 0)
            {
                string packDir = bepinexPackDirs[0];
                string[] dllFiles = Directory.GetFiles(packDir, "*.dll");

                foreach (string dll in dllFiles)
                {
                    File.Copy(dll, Path.Combine(pluginsPath, Path.GetFileName(dll)), true);
                    UnityEngine.Debug.Log("Copied " + Path.GetFileName(dll));
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing BepInExPack: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallR2API()
    {
        while (!configLoaded)
            yield return null;

        List<FileDownloader.FileData> r2apiMods = new List<FileDownloader.FileData>
        {
            ror2API,
            ror2APICore,
            ror2APIAddressables,
            ror2APIContentManagement,
            ror2APINetworking,
            ror2APIRecalculateStats,
            ror2APIDamageType,
            ror2APIDifficulty,
            ror2APIOrb,
            ror2APILobbyConfig,
            ror2APILanguage,
            ror2APIDot,
            ror2APIDeployable,
            ror2APICommandHelper,
            ror2APIArtifactCode,
            ror2APIDirector,
            ror2APIUnlockable,
            ror2APITempVisualEffect,
            ror2APISound,
            ror2APIPrefab,
            ror2APILoadout,
            ror2APIItems,
            ror2APIElites,
            ror2APISceneAsset
        };

        foreach (FileDownloader.FileData apiMod in r2apiMods)
        {
            if (!string.IsNullOrEmpty(apiMod.url))
            {
                yield return InstallSingleMod(apiMod);
            }
        }
    }

    IEnumerator InstallSingleMod(FileDownloader.FileData mod)
    {
        string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string tempExtractPath = Path.Combine(Application.persistentDataPath, "R2API_Temp_" + System.Guid.NewGuid().ToString().Substring(0, 8));

        yield return downloader.DownloadAndExtract(mod, Application.persistentDataPath, tempExtractPath);

        try
        {
            string thunderstorePluginsPath = Path.Combine(tempExtractPath, "BepInEx", "plugins");

            if (!Directory.Exists(thunderstorePluginsPath))
            {
                thunderstorePluginsPath = Path.Combine(tempExtractPath, "plugins");
            }

            if (Directory.Exists(thunderstorePluginsPath))
            {
                UnityEngine.Debug.Log("Found plugins path at: " + thunderstorePluginsPath);

                foreach (string dir in Directory.GetDirectories(thunderstorePluginsPath))
                {
                    string dirName = Path.GetFileName(dir);
                    UnityEngine.Debug.Log("Found directory: " + dirName);

                    string targetPath = Path.Combine(pluginsPath, dirName);

                    if (Directory.Exists(targetPath))
                        SafeDeleteDirectory(targetPath);

                    CopyDirectory(dir, targetPath);
                    UnityEngine.Debug.Log("Copied " + dirName + " to plugins");

                    // Add R2API mod to tracked list
                    installedMods.Add((dirName, mod.url, ExtractVersionFromUrl(mod.url)));
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("No plugins path found in archive");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error processing mod: " + e.Message);
        }

        SafeDeleteDirectory(tempExtractPath);
    }

    IEnumerator InstallRoR2AP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "RoR2APTemp");
        yield return downloader.DownloadAndExtract(ror2AP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string[] dirs = Directory.GetDirectories(extractPath);
        string[] files = Directory.GetFiles(extractPath);

        foreach (string file in files)
        {
            string destFile = Path.Combine(pluginsPath, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);
            string targetDir = Path.Combine(pluginsPath, dirName);

            if (Directory.Exists(targetDir))
                SafeDeleteDirectory(targetDir);

            CopyDirectory(dir, targetDir);
            UnityEngine.Debug.Log("Copied " + dirName + " to plugins");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(ror2Path, "BepInEx", "config", "BepInEx.cfg");

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
            remoteConfig = JsonUtility.FromJson<RoR2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyRoR2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchRoR2()
    {
        string exePath = Path.Combine(ror2Path, "Risk of Rain 2.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Risk of Rain 2 executable not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        try
        {
            ror2Process = Process.Start(exePath);
            UnityEngine.Debug.Log("Risk of Rain 2 launched successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Risk of Rain 2:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseRoR2()
    {
        try
        {
            if (ror2Process != null && !ror2Process.HasExited)
            {
                ror2Process.Kill();
                ror2Process.Dispose();
                ror2Process = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseRoR2();
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

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetRoR2Path()
    {
        if (isEpic)
            return GetRoR2EpicPath();
        else
            return GetRoR2SteamPath();
    }

    string GetRoR2SteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Risk of Rain 2"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Risk of Rain 2"),
            @"D:\Steam\steamapps\common\Risk of Rain 2",
            @"D:\SteamLibrary\steamapps\common\Risk of Rain 2",
            @"D:\steamapps\common\Risk of Rain 2",
            @"E:\Steam\steamapps\common\Risk of Rain 2",
            @"E:\SteamLibrary\steamapps\common\Risk of Rain 2",
            @"E:\steamapps\common\Risk of Rain 2",
            @"E:\Program Files (x86)\steamapps\common\Risk of Rain 2",
            @"E:\Program Files\steamapps\common\Risk of Rain 2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + path);
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
                    string ror2Path = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Risk of Rain 2");
                    if (Directory.Exists(ror2Path))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + ror2Path);
                        return ror2Path;
                    }

                    ror2Path = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Risk of Rain 2");
                    if (Directory.Exists(ror2Path))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + ror2Path);
                        return ror2Path;
                    }

                    ror2Path = Path.Combine(drive.Name, "steamapps", "common", "Risk of Rain 2");
                    if (Directory.Exists(ror2Path))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + ror2Path);
                        return ror2Path;
                    }

                    ror2Path = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Risk of Rain 2");
                    if (Directory.Exists(ror2Path))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + ror2Path);
                        return ror2Path;
                    }

                    ror2Path = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Risk of Rain 2");
                    if (Directory.Exists(ror2Path))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Steam) at: " + ror2Path);
                        return ror2Path;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Risk of Rain 2 (Steam) not found.");
        return "";
    }

    string GetRoR2EpicPath()
    {
        string[] quickPaths = new string[]
        {
        @"C:\Program Files\Epic Games\RiskofRain2",
        @"D:\Epic Games\RiskofRain2",
        @"E:\Epic Games\RiskofRain2",
        @"C:\Games\Epic\RiskofRain2",
        @"D:\Games\Epic\RiskofRain2",
        @"E:\Games\Epic\RiskofRain2",
        @"C:\Epic\RiskofRain2",
        @"D:\Epic\RiskofRain2",
        @"E:\Epic\RiskofRain2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Risk of Rain 2 (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Cherche dans Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Cherche le manifest pour Risk of Rain 2
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Risk of Rain 2") || content.Contains("RiskOfRain2"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Risk of Rain 2 (Epic) at: " + epicPath);
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

        // Scan all drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Risk of Rain 2");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Risk of Rain 2");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Risk of Rain 2 (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Risk of Rain 2 (Epic) not found.");
        return "";
    }

    void CreateVersionFile()
    {
        try
        {
            string versionFileName = "Risk of Rain 2 Archipelago Version " + ExtractVersionFromUrl(ror2AP.url) + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== INSTALLED MODS ===\n";

            // Add all installed mods
            foreach (var mod in installedMods)
            {
                content += "\n" + mod.name + ":\n";
                content += "  Downloaded from: " + mod.url + "\n";
                content += "  Version: " + mod.version + "\n";
            }

            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(ror2Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Risk of Rain 2 Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(ror2Path);
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

            string pluginsPath = Path.Combine(ror2Path, "BepInEx", "plugins");
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

    string ExtractVersionFromUrl(string url)
    {
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
