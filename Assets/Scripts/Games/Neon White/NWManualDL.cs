using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NWManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("NEON WHITE FILES")]
    public FileDownloader.FileData neonwhiteApworld;
    public FileDownloader.FileData neonwhiteMelonLoader;
    public FileDownloader.FileData neonwhiteAP;
    public FileDownloader.FileData neonwhiteAPMCN;
    public FileDownloader.FileData neonwhitePrefManager;
    public FileDownloader.FileData neonwhiteUniverseLib;
    public FileDownloader.FileData neonwhiteNeonLite;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Neon White";
    public string epicGameFolderName = "NeonWhite";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installMelonLoaderToggle;
    public Toggle installAPsToggle;
    public Toggle installPrefManagerToggle;
    public Toggle installUniverseLibToggle;
    public Toggle installNeonLiteToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearMelonLoaderToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string neonwhitePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private NWConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class NWConfig
    {
        public string neonwhiteApworld;
        public string neonwhiteMelonLoader;
        public string neonwhiteAP;
        public string neonwhiteAPMCN;
        public string neonwhitePrefManager;
        public string neonwhiteUniverseLib;
        public string neonwhiteNeonLite;
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

        neonwhitePath = GetNeonWhitePath();
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

        if (fullClearMelonLoaderToggle != null)
            fullClearMelonLoaderToggle.onValueChanged.AddListener(OnFullCleanChanged);
        if (fullClearMelonLoaderToggle != null)
            fullClearMelonLoaderToggle.isOn = false;
        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;
    }

    // PLATFORM
    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        neonwhitePath = GetNeonWhitePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + neonwhitePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        neonwhitePath = GetNeonWhitePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + neonwhitePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(neonwhitePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // TOGGLE RULE
    void OnFullCleanChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = !value ? clearAPModsToggle.isOn : false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void ApplyNWConfig()
    {
        if (remoteConfig == null)
            return;

        neonwhiteApworld.url = remoteConfig.neonwhiteApworld;
        neonwhiteMelonLoader.url = remoteConfig.neonwhiteMelonLoader;
        neonwhiteAP.url = remoteConfig.neonwhiteAP;
        neonwhiteAPMCN.url = remoteConfig.neonwhiteAPMCN;
        neonwhitePrefManager.url = remoteConfig.neonwhitePrefManager;
        neonwhiteUniverseLib.url = remoteConfig.neonwhiteUniverseLib;
        neonwhiteNeonLite.url = remoteConfig.neonwhiteNeonLite;
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
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        neonwhitePath = GetNeonWhitePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool aps = installAPsToggle != null && installAPsToggle.isOn;
        bool pref = installPrefManagerToggle != null && installPrefManagerToggle.isOn;
        bool uni = installUniverseLibToggle != null && installUniverseLibToggle.isOn;
        bool neonlite = installNeonLiteToggle != null && installNeonLiteToggle.isOn;
        bool needsGamePath = aps || melonloader || pref || uni || neonlite;

        if (needsGamePath && string.IsNullOrEmpty(neonwhitePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (melonloader ? 1 : 0) +
            (aps ? 1 : 0) +
            (pref ? 1 : 0) +
            (uni ? 1 : 0) +
            (neonlite ? 1 : 0);

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
        if (aps && count == 1)
        {
            StartCoroutine(APsOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallApworld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Neon White...");
            LaunchNeonWhite();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        neonwhitePath = GetNeonWhitePath();

        if (string.IsNullOrEmpty(neonwhitePath))
            return;

        string modsPath = Path.Combine(neonwhitePath, "Mods");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClean = fullClearMelonLoaderToggle != null && fullClearMelonLoaderToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing Archipelago mods...");

            SafeDeleteFile(Path.Combine(modsPath, "NWArchipelago.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "MelonPrefManager.Mono.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "NeonLite.dll"));

            try
            {
                foreach (string file in Directory.GetFiles(modsPath))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("UniverseLib"))
                        SafeDeleteFile(file);
                }
            }
            catch { }

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // At this point fullClean == true
        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;
            ShowConfirmation(
                "Other mods were detected in Mods.\nDo you REALLY want to fully delete MelonLoader and related files?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        ShowInfo("Removing AP mods...");

        SafeDeleteFile(Path.Combine(modsPath, "NWArchipelago.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "Archipelago.MultiClient.Net.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "MelonPrefManager.Mono.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "NeonLite.dll"));

        // UniverseLib contents
        try
        {
            foreach (string file in Directory.GetFiles(modsPath))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("UniverseLib"))
                    SafeDeleteFile(file);
            }
        }
        catch { }

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(neonwhitePath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "UserData"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "Plugins"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "dobby.dll"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "NOTICE.txt"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "version.dll"));

            // Optionally remove Mods folder if empty (be conservative)
            try
            {
                if (Directory.Exists(modsPath) && Directory.GetFiles(modsPath).Length == 0 && Directory.GetDirectories(modsPath).Length == 0)
                    Directory.Delete(modsPath);
            }
            catch { }

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning MelonLoader (no other mods detected)...");

            SafeDeleteDirectory(Path.Combine(neonwhitePath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "UserData"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(neonwhitePath, "Plugins"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "dobby.dll"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "NOTICE.txt"));
            SafeDeleteFile(Path.Combine(neonwhitePath, "version.dll"));

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

            if (name == "NWArchipelago.dll" || name == "Archipelago.MultiClient.Net.dll" || name == "MelonPrefManager.Mono.dll" || name == "NeonLite.dll")
                continue;

            if (name.StartsWith("UniverseLib"))
                continue;

            // If any other file found => other mod present
            return true;
        }

        if (dirs.Length > 0)
            return true;

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing NeonWhite APWorld...");
            yield return InstallApworld();
        }

        if (installMelonLoaderToggle != null && installMelonLoaderToggle.isOn)
        {
            ShowInfo("Installing MelonLoader...");
            yield return InstallMelonLoader();
        }

        // Ensure Mods folder exists
        Directory.CreateDirectory(Path.Combine(neonwhitePath, "Mods"));

        if (installAPsToggle != null && installAPsToggle.isOn)
        {
            ShowInfo("Installing NWArchipelago.dll and Archipelago.MultiClient.Net.dll...");
            yield return InstallSimpleDll(neonwhiteAP, "NWArchipelago.dll");
            yield return InstallSimpleDll(neonwhiteAPMCN, "Archipelago.MultiClient.Net.dll");
        }

        if (installPrefManagerToggle != null && installPrefManagerToggle.isOn)
        {
            ShowInfo("Installing MelonPrefManager.Mono.dll...");
            yield return InstallSimpleDll(neonwhitePrefManager, "MelonPrefManager.Mono.dll");
        }

        if (installUniverseLibToggle != null && installUniverseLibToggle.isOn)
        {
            ShowInfo("Installing UniverseLib...");
            yield return InstallUniverseLib();
        }

        if (installNeonLiteToggle != null && installNeonLiteToggle.isOn)
        {
            ShowInfo("Installing NeonLite.dll...");
            yield return InstallSimpleDll(neonwhiteNeonLite, "NeonLite.dll");
        }

        // Optionally launch
        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Neon White...");
            yield return new WaitForSeconds(1f);
            LaunchNeonWhite();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallApworld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(neonwhiteApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = neonwhiteApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = neonwhiteApworld.url.Substring(neonwhiteApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        yield return DownloadFile(neonwhiteApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Apworld download failed!");
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
            catch { }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            yield break;
        }

        try
        {
            if (File.Exists(target))
                File.Delete(target);
            File.Copy(localPath, target, true);
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

    IEnumerator InstallSimpleDll(FileDownloader.FileData fileData, string targetFileName)
    {
        string modsPath = Path.Combine(neonwhitePath, "Mods");
        Directory.CreateDirectory(modsPath);

        string dllPath = Path.Combine(modsPath, targetFileName);

        UnityEngine.Debug.Log("Downloading from: " + fileData.url);
        UnityEngine.Debug.Log("Saving to: " + dllPath);

        yield return DownloadFile(fileData.url, dllPath);

        if (File.Exists(dllPath))
        {
            UnityEngine.Debug.Log(targetFileName + " downloaded successfully");
            ShowInfo(targetFileName + " installed successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError(targetFileName + " download failed!");
            ShowInfo("ERROR: " + targetFileName + " download failed!");
        }

        yield return null;
    }

    IEnumerator InstallUniverseLib()
    {
        // Expect a zip; extract and move contents of UniverseLib.Mono subfolder into Mods
        string extractPath = Path.Combine(Application.persistentDataPath, "UniverseLibTemp");
        Directory.CreateDirectory(extractPath);

        yield return downloader.DownloadAndExtract(neonwhiteUniverseLib, Application.persistentDataPath, extractPath);

        // Find UniverseLib.Mono dir inside extractPath
        string uniDir = FindDirectory(extractPath, "UniverseLib.Mono");
        if (string.IsNullOrEmpty(uniDir))
        {
            // Maybe the zip directly contains files starting with UniverseLib
            // Try to move all UniverseLib* files to Mods
            string modsPath = Path.Combine(neonwhitePath, "Mods");
            Directory.CreateDirectory(modsPath);

            try
            {
                foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("UniverseLib"))
                    {
                        string dest = Path.Combine(modsPath, name);
                        File.Copy(file, dest, true);
                    }
                }
                ShowInfo("UniverseLib installed successfully!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to install UniverseLib: " + e.Message);
                ShowInfo("ERROR: Failed to install UniverseLib\n" + e.Message);
            }

            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Move contents of uniDir into Mods
        string mods = Path.Combine(neonwhitePath, "Mods");
        Directory.CreateDirectory(mods);

        foreach (string file in Directory.GetFiles(uniDir, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(uniDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(mods, relative);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest, true);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to copy UniverseLib file: " + e.Message);
            }
        }

        ShowInfo("UniverseLib installed successfully!");
        SafeDeleteDirectory(extractPath);
        yield return null;
    }

    IEnumerator InstallMelonLoader()
    {
        while (!configLoaded)
            yield return null;

        ShowInfo("Extracting MelonLoader...");

        string extractPath = Path.Combine(Application.persistentDataPath, "MelonLoaderTemp");

        yield return downloader.DownloadAndExtract(neonwhiteMelonLoader, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        // Copy everything from extractPath root into neonwhitePath (game root)
        CopyAllFromExtract(extractPath, neonwhitePath);

        Directory.CreateDirectory(Path.Combine(neonwhitePath, "Mods"));

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallMelonLoader");
        yield return null;
    }

    void CopyAllFromExtract(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyAllFromExtract");

        if (!Directory.Exists(extractPath))
            return;

        foreach (string dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destDir = Path.Combine(targetPath, rel);
            try
            {
                if (Directory.Exists(destDir))
                    SafeDeleteDirectory(destDir);
                Directory.CreateDirectory(destDir);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to create dest dir: " + destDir + " - " + e.Message);
            }
        }

        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(targetPath, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest, true);
                UnityEngine.Debug.Log("Copied file: " + dest);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to copy file: " + file + " - " + e.Message);
            }
        }

        UnityEngine.Debug.Log("END CopyAllFromExtract");
    }

    IEnumerator APsOnlyFlow()
    {
        neonwhitePath = GetNeonWhitePath();

        if (string.IsNullOrEmpty(neonwhitePath))
            yield break;

        Directory.CreateDirectory(Path.Combine(neonwhitePath, "Mods"));

        ShowInfo("Installing NWArchipelago.dll and Archipelago.MultiClient.Net.dll...");
        yield return InstallSimpleDll(neonwhiteAP, "NWArchipelago.dll");
        yield return InstallSimpleDll(neonwhiteAPMCN, "Archipelago.MultiClient.Net.dll");

        ShowInfo("Installation complete!");
    }

    IEnumerator MelonLoaderOnlyFlow()
    {
        ShowInfo("Installing MelonLoader...");
        yield return InstallMelonLoader();

        ShowInfo("MelonLoader installed successfully!");

        yield break;
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
            remoteConfig = JsonUtility.FromJson<NWConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyNWConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        neonwhitePath = GetNeonWhitePath();
        UpdatePlatformStatus();
    }

    void LaunchNeonWhite()
    {
        try
        {
            string exeName = isEpic ? "NeonWhite.exe" : "Neon White.exe";
            string exePath = Path.Combine(neonwhitePath, exeName);

            if (File.Exists(exePath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = true
                };
                Process.Start(psi);
                UnityEngine.Debug.Log("Neon White launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Neon White exe not found at: " + exePath);
                ShowInfo("ERROR: Neon White exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Neon White: " + e.Message);
            ShowInfo("ERROR: Failed to launch Neon White!");
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

    string GetNeonWhitePath()
    {
        if (isEpic)
            return GetNeonWhiteEpicPath();
        else
            return GetNeonWhiteSteamPath();
    }

    string GetNeonWhiteSteamPath()
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

    string GetNeonWhiteEpicPath()
    {
        string[] quickPaths = new string[]
       {
            @"C:\Program Files\Epic Games\NeonWhite",
            @"C:\Games\Epic\NeonWhite",
       };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
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
                        if (content.Contains("NeonWhite") || content.Contains("NeonWhite"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
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

                        try
                        {
                            string epicPath = Path.Combine(drive.Name, relativePath, epicGameFolderName);
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
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
    }
}