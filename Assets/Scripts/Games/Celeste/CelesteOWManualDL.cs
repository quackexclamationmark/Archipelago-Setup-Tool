using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public class CelesteOWManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CELESTE OW FILES")]
    public FileDownloader.FileData celesteowAP;
    public FileDownloader.FileData celesteowEverest;
    public FileDownloader.FileData celesteowOlympus;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPToggle;
    public Toggle installEverestToggle;
    public Toggle installOlympusToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchEverestToggle;
    public Toggle launchOlympusToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process celesteProcess;
    private string celestePath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private CelesteOWConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    private static readonly string[] AP_MOD_WHITELIST = new string[]
    {
        "Archipelago_Open_World.zip",
        "Mods",
        "Cache",
        "blacklist.txt",
        "favorites.txt",
        "modoptionsorder.txt",
        "updaterblacklist.txt"
    };

    private static readonly string[] FULL_CLEAR_DIRECTORIES = new string[]
{
    "everest-lib",
    "EverestSplash",
    "lib64-win-x64",
    "Mods",
    "piton-apphosts"
};

    private static readonly string[] FULL_CLEAR_FILES = new string[]
    {
    "BuildIsFNA.txt",
    "Celeste.deps.json",
    "Celeste.dll",
    "Celeste.Mod.mm.deps.json",
    "Celeste.Mod.mm.dll",
    "Celeste.Mod.mm.pdb",
    "Celeste.Mod.mm.xml",
    "Celeste.pdb",
    "Celeste.runtimeconfig.json",
    "Celeste.xml",
    "DiscordGameSDK.dll",
    "DiscordGameSDK.pdb",
    "DotNetZip.dll",
    "everest-launch.txt",
    "Everest.Installer.exe",
    "FNA.dll",
    "FNA.pdb",
    "FNA.xml",
    "gamecontrollerdb.txt",
    "installer-log.txt",
    "Jdenticon.dll",
    "KeraLua.dll",
    "log.txt",
    "MAB.DotIgnore.dll",
    "Microsoft.NET.HostModel.dll",
    "Microsoft.Win32.SystemEvents.dll",
    "MiniInstaller-linux",
    "miniinstaller-log.txt",
    "MiniInstaller-osx",
    "MiniInstaller-win.exe",
    "MiniInstaller-win64.exe",
    "MiniInstaller.deps.json",
    "MiniInstaller.dll",
    "MiniInstaller.pdb",
    "MiniInstaller.runtimeconfig.json",
    "MMHOOK_Celeste.dll",
    "MMHOOK_Celeste.pdb",
    "Mono.Cecil.dll",
    "Mono.Cecil.Mdb.dll",
    "Mono.Cecil.Pdb.dll",
    "Mono.Cecil.Rocks.dll",
    "MonoMod.Backports.dll",
    "MonoMod.Core.dll",
    "MonoMod.Core.pdb",
    "MonoMod.Core.xml",
    "MonoMod.Iced.dll",
    "MonoMod.Iced.pdb",
    "MonoMod.Iced.xml",
    "MonoMod.ILHelpers.dll",
    "MonoMod.Patcher",
    "MonoMod.Patcher.dll",
    "MonoMod.Patcher.pdb",
    "MonoMod.Patcher.runtimeconfig.json",
    "MonoMod.Patcher.xml",
    "MonoMod.RuntimeDetour.dll",
    "MonoMod.RuntimeDetour.HookGen",
    "MonoMod.RuntimeDetour.HookGen.dll",
    "MonoMod.RuntimeDetour.HookGen.pdb",
    "MonoMod.RuntimeDetour.HookGen.runtimeconfig.json",
    "MonoMod.RuntimeDetour.HookGen.xml",
    "MonoMod.RuntimeDetour.pdb",
    "MonoMod.RuntimeDetour.xml",
    "MonoMod.Utils.dll",
    "MonoMod.Utils.pdb",
    "MonoMod.Utils.xml",
    "NETCoreifier.deps.json",
    "NETCoreifier.dll",
    "NETCoreifier.pdb",
    "Newtonsoft.Json.dll",
    "NLua.dll",
    "NLua.pdb",
    "piton-runtime.yaml",
    "System.Drawing.Common.dll",
    "System.Security.Permissions.dll",
    "System.Windows.Extensions.dll",
    "YamlDotNet.dll"
    };

    [System.Serializable]
    public class CelesteOWConfig
    {
        public string celesteowAP;
        public string celesteowEverest;
        public string celesteowOlympus;
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

        celestePath = GetCelestePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchEverestToggle != null)
            launchEverestToggle.isOn = true;

        if (launchOlympusToggle != null)
        {
            launchOlympusToggle.isOn = false;
            launchOlympusToggle.interactable = false;
        }

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (fullClearToggle != null)
            fullClearToggle.isOn = false;

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearToggle != null)
            fullClearToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installEverestToggle != null)
            installEverestToggle.isOn = true;

        if (installOlympusToggle != null)
        {
            installOlympusToggle.isOn = false;
            installOlympusToggle.onValueChanged.AddListener(OnInstallOlympusChanged);
        }

        if (infoPanel != null)
            infoPanel.SetActive(false);
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
        celestePath = GetCelestePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + celestePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        celestePath = GetCelestePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + celestePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(celestePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void OnInstallOlympusChanged(bool value)
    {
        if (launchOlympusToggle != null)
        {
            launchOlympusToggle.interactable = value;
            if (!value)
            {
                launchOlympusToggle.isOn = false;
            }
        }
    }

    // =========================================================

    void ApplyCelesteOWConfig()
    {
        if (remoteConfig == null)
            return;

        celesteowAP.url = remoteConfig.celesteowAP;
        celesteowEverest.url = remoteConfig.celesteowEverest;
        celesteowOlympus.url = remoteConfig.celesteowOlympus;
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

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;

            case "Revert":
                ExecuteRevert();
                break;

            case "ForceFullClear":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        celestePath = GetCelestePath();

        if (string.IsNullOrEmpty(celestePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Celeste not found in " + platform + ". Please check installation.");
            return;
        }

        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool everest = installEverestToggle != null && installEverestToggle.isOn;
        bool olympus = installOlympusToggle != null && installOlympusToggle.isOn;

        int count = (ap ? 1 : 0) + (everest ? 1 : 0) + (olympus ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        celestePath = GetCelestePath();

        if (string.IsNullOrEmpty(celestePath))
            return;

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearToggle != null && fullClearToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            // Remove Archipelago_Open_World.zip from Mods folder
            string modsPath = Path.Combine(celestePath, "Mods");
            if (Directory.Exists(modsPath))
            {
                SafeDeleteFile(Path.Combine(modsPath, "Archipelago_Open_World.zip"));
            }

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (hasOtherMods && !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other files were detected in Mods folder.\nDo you want to continue?",
                "ForceFullClear"
            );

            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        ShowInfo("Clearing specified directories and files...");

        // 1) Supprimer uniquement les répertoires listés
        foreach (string dir in FULL_CLEAR_DIRECTORIES)
        {
            string dirPath = Path.Combine(celestePath, dir);
            try
            {
                SafeDeleteDirectory(dirPath);
                UnityEngine.Debug.Log("Attempted delete directory: " + dirPath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Error deleting directory " + dirPath + " : " + e.Message);
            }
        }

        // 2) Supprimer uniquement les fichiers (ou dossiers) listés dans FULL_CLEAR_FILES
        foreach (string name in FULL_CLEAR_FILES)
        {
            if (string.IsNullOrEmpty(name))
                continue;

            string path = Path.Combine(celestePath, name);

            try
            {
                if (Directory.Exists(path))
                {
                    SafeDeleteDirectory(path);
                    UnityEngine.Debug.Log("Removed directory (from files list): " + path);
                    continue;
                }

                if (File.Exists(path))
                {
                    SafeDeleteFile(path);
                    UnityEngine.Debug.Log("Removed file: " + path);
                    continue;
                }

                // Tentative de correspondance insensible à la casse dans la racine (au cas où)
                try
                {
                    var rootFiles = Directory.GetFiles(celestePath);
                    foreach (string f in rootFiles)
                    {
                        if (string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase))
                        {
                            SafeDeleteFile(f);
                            UnityEngine.Debug.Log("Removed file (case-insensitive match): " + f);
                            goto NextName;
                        }
                    }

                    var rootDirs = Directory.GetDirectories(celestePath);
                    foreach (string d in rootDirs)
                    {
                        if (string.Equals(new DirectoryInfo(d).Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            SafeDeleteDirectory(d);
                            UnityEngine.Debug.Log("Removed directory (case-insensitive match): " + d);
                            break;
                        }
                    }
                }
                catch { }

            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Error deleting " + path + " : " + e.Message);
            }

        NextName:
            continue;
        }

        // 3) Restaurer Celeste.exe et Steamworks.NET.dll depuis orig (si présents)
        try
        {
            RestoreOriginalFiles();
            UnityEngine.Debug.Log("Restoration from orig attempted.");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Error restoring originals: " + e.Message);
        }

        // 4) Supprimer ensuite le dossier orig
        try
        {
            string origPath = Path.Combine(celestePath, "orig");
            SafeDeleteDirectory(origPath);
            UnityEngine.Debug.Log("Attempted delete orig directory: " + origPath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Error deleting orig folder: " + e.Message);
        }

        ShowInfo("Revert completed (specified items).");
    }

    // =========================================================
    // INFO
    // =========================================================

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
    // OTHER LOGIC
    // =========================================================

    bool IsWhitelisted(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        DirectoryInfo parent = null;
        try
        {
            parent = new DirectoryInfo(Path.GetDirectoryName(filePath));
        }
        catch { }
        string folderName = parent != null ? parent.Name : "";

        // Check if filename or folder is in whitelist
        foreach (string whitelisted in AP_MOD_WHITELIST)
        {
            if (fileName.Equals(whitelisted, StringComparison.OrdinalIgnoreCase))
                return true;

            if (folderName.Equals(whitelisted, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    bool HasOtherMods()
    {
        string modsPath = Path.Combine(celestePath, "Mods");

        if (Directory.Exists(modsPath))
        {
            string[] files = Directory.GetFiles(modsPath, "*", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                bool hasNonWhitelisted = false;
                foreach (string file in files)
                {
                    if (!IsWhitelisted(file))
                    {
                        if (!hasNonWhitelisted)
                        {
                            UnityEngine.Debug.LogWarning("Found non-whitelisted file(s) in Mods:");
                            hasNonWhitelisted = true;
                        }
                        UnityEngine.Debug.LogWarning("  - " + file);
                    }
                }

                return hasNonWhitelisted;
            }
        }

        return false;
    }

    void RestoreOriginalFiles()
    {
        string origPath = Path.Combine(celestePath, "orig");

        if (!Directory.Exists(origPath))
            return;

        try
        {
            // Copy Celeste.exe from orig to root
            string origCelesteExe = Path.Combine(origPath, "Celeste.exe");
            if (File.Exists(origCelesteExe))
            {
                string targetCelesteExe = Path.Combine(celestePath, "Celeste.exe");
                File.Copy(origCelesteExe, targetCelesteExe, true);
                UnityEngine.Debug.Log("Restored Celeste.exe from orig");
            }

            // Copy Steamworks.NET.dll from orig to root (if present)
            string origSteamworks = Path.Combine(origPath, "Steamworks.NET.dll");
            if (File.Exists(origSteamworks))
            {
                string targetSteamworks = Path.Combine(celestePath, "Steamworks.NET.dll");
                File.Copy(origSteamworks, targetSteamworks, true);
                UnityEngine.Debug.Log("Restored Steamworks.NET.dll from orig");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Error restoring original files: " + e.Message);
        }
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing AP mod...");
            yield return InstallAPMod();
        }

        if (installEverestToggle != null && installEverestToggle.isOn)
        {
            ShowInfo("Installing Everest...");
            yield return InstallEverest();
        }

        if (installOlympusToggle != null && installOlympusToggle.isOn)
        {
            ShowInfo("Installing Olympus...");
            yield return InstallOlympus();
        }

        if (launchEverestToggle != null && launchEverestToggle.isOn)
        {
            ShowInfo("Launching Everest...");
            LaunchEverest();
            yield return new WaitForSeconds(2f);
        }

        if (launchOlympusToggle != null && launchOlympusToggle.isOn)
        {
            ShowInfo("Launching Olympus...");
            LaunchOlympus();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string modsPath = Path.Combine(celestePath, "Mods");

        // Create Mods folder if it doesn't exist
        Directory.CreateDirectory(modsPath);

        string targetZip = Path.Combine(modsPath, "Archipelago_Open_World.zip");

        // Download the file directly without extraction
        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(celesteowAP.url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Failed to download AP mod: " + request.error);
            SafeDeleteFile(targetZip);
            yield break;
        }

        // Write the downloaded file directly to Mods folder
        try
        {
            File.WriteAllBytes(targetZip, request.downloadHandler.data);
            UnityEngine.Debug.Log("AP mod installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error saving AP mod: " + e.Message);
            SafeDeleteFile(targetZip);
        }
    }

    IEnumerator InstallEverest()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string installerPath = Path.Combine(celestePath, "Everest.Installer.exe");

        // Download the file directly
        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(celesteowEverest.url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Failed to download Everest: " + request.error);
            yield break;
        }

        // Write the downloaded file directly
        try
        {
            File.WriteAllBytes(installerPath, request.downloadHandler.data);
            UnityEngine.Debug.Log("Everest installer downloaded successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error saving Everest installer: " + e.Message);
        }
    }

    IEnumerator InstallOlympus()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        // Save the Olympus installer under the fixed name "Olympus.exe" in the Celeste folder
        string installerPath = Path.Combine(celestePath, "Olympus.exe");

        // Download the file directly
        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(celesteowOlympus.url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Failed to download Olympus: " + request.error);
            yield break;
        }

        // Write the downloaded file directly
        try
        {
            File.WriteAllBytes(installerPath, request.downloadHandler.data);
            UnityEngine.Debug.Log("Olympus installer downloaded successfully to: " + installerPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error saving Olympus installer: " + e.Message);
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
            remoteConfig = JsonUtility.FromJson<CelesteOWConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyCelesteOWConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchEverest()
    {
        string exePath = Path.Combine(celestePath, "Everest.Installer.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Everest installer executable not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        ExecuteInstaller(exePath);
    }

    void LaunchOlympus()
    {
        // Try the fixed filename first
        string fixedInstaller = Path.Combine(celestePath, "Olympus.exe");
        if (File.Exists(fixedInstaller))
        {
            ExecuteInstaller(fixedInstaller);
            return;
        }

        // Fallback: search for olympus*.exe (compatibility with older behavior)
        try
        {
            string[] exeFiles = Directory.GetFiles(celestePath, "olympus*.exe");

            if (exeFiles.Length == 0)
            {
                ShowInfo("Olympus executable not found!");
                UnityEngine.Debug.LogError("Olympus executable not found in: " + celestePath);
                return;
            }

            ExecuteInstaller(exeFiles[0]);
        }
        catch (Exception e)
        {
            ShowInfo("Olympus executable not found!");
            UnityEngine.Debug.LogError("Olympus launch error: " + e);
        }
    }

    void ExecuteInstaller(string exePath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = exePath;
        startInfo.WorkingDirectory = Path.GetDirectoryName(exePath);

        try
        {
            celesteProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Installer launched successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching installer:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseCeleste()
    {
        try
        {
            if (celesteProcess != null && !celesteProcess.HasExited)
            {
                celesteProcess.Kill();
                celesteProcess.Dispose();
                celesteProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseCeleste();
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

    void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
        }
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetCelestePath()
    {
        if (isEpic)
            return GetCelesteEpicPath();
        else
            return GetCelesteSteamPath();
    }

    string GetCelesteSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Celeste"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Celeste"),
            @"D:\Steam\steamapps\common\Celeste",
            @"D:\SteamLibrary\steamapps\common\Celeste",
            @"D:\steamapps\common\Celeste",
            @"E:\Steam\steamapps\common\Celeste",
            @"E:\SteamLibrary\steamapps\common\Celeste",
            @"E:\steamapps\common\Celeste",
            @"E:\Program Files (x86)\steamapps\common\Celeste",
            @"E:\Program Files\steamapps\common\Celeste",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Celeste (Steam) at: " + path);
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
                    // Search Steam\steamapps
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Celeste");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Celeste");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search steamapps at drive root
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "Celeste");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Celeste");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Search Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Celeste");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Celeste (Steam) not found.");
        return "";
    }

    string GetCelesteEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Celeste",
            @"D:\Epic Games\Celeste",
            @"E:\Epic Games\Celeste",
            @"C:\Games\Epic\Celeste",
            @"D:\Games\Epic\Celeste",
            @"E:\Games\Epic\Celeste",
            @"C:\Epic\Celeste",
            @"D:\Epic\Celeste",
            @"E:\Epic\Celeste",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Celeste (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Search in Epic Games Launcher directory
        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                // Search for Celeste manifest
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Celeste"))
                        {
                            // Extract install location from manifest
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Celeste (Epic) at: " + epicPath);
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
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Celeste");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Celeste");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found Celeste (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Celeste (Epic) not found.");
        return "";
    }
}