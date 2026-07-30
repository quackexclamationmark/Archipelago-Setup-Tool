using Microsoft.Win32;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PVZRManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("PVZR FILES")]
    public FileDownloader.FileData pvzreplantedApworld;
    public FileDownloader.FileData pvzreplantedMelonLoader;
    public FileDownloader.FileData pvzreplantedAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "PVZ Replanted";
    public string epicGameFolderName = "PlantsvsZombiesReplaiXEyJ";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPToggle;
    public Toggle installMelonLoaderToggle;

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

    private string pvzPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class PVZRConfig
    {
        public string pvzreplantedAP;
        public string pvzreplantedMelonLoader;
        public string pvzreplantedApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
    }

    private PVZRConfig remoteConfig;

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        pvzPath = GetPVZPath();

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

        StartCoroutine(LoadRemoteConfig());
    }

    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        pvzPath = GetPVZPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + pvzPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        pvzPath = GetPVZPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + pvzPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(pvzPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
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
        pvzPath = GetPVZPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmod = installAPToggle != null && installAPToggle.isOn;
        bool needsGamePath = apmod || melonloader || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(pvzPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (melonloader ? 1 : 0) + (apmod ? 1 : 0);

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

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching PVZ Replanted...");
            LaunchPVZR();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        pvzPath = GetPVZPath();

        if (string.IsNullOrEmpty(pvzPath))
            return;

        string modsPath = Path.Combine(pvzPath, "Mods");

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

            SafeDeleteFile(Path.Combine(modsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(modsPath, "ReplantedArchipelago.dll"));

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

        SafeDeleteFile(Path.Combine(modsPath, "Archipelago.MultiClient.Net.dll"));
        SafeDeleteFile(Path.Combine(modsPath, "ReplantedArchipelago.dll"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(pvzPath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "UserData"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "Mods"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "Plugins"));
            SafeDeleteFile(Path.Combine(pvzPath, "version.dll"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(pvzPath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "UserData"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "Mods"));
            SafeDeleteDirectory(Path.Combine(pvzPath, "Plugins"));
            SafeDeleteFile(Path.Combine(pvzPath, "version.dll"));

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

            if (name.StartsWith("PVZR APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name != "Archipelago.MultiClient.Net.dll" && name != "ReplantedArchipelago.dll")
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

        if (installAPToggle == null || installAPToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        CreateVersionFile(pvzreplantedAP.url, pvzreplantedMelonLoader.url, pvzreplantedApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching PVZ Replanted...");
            yield return new WaitForSeconds(1f);
            LaunchPVZR();
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

        string url = pvzreplantedApworld.url;

        if (string.IsNullOrEmpty(url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pvzreplantedApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = url.Substring(url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(url, localPath);

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
        Path.Combine(pvzPath, "custom_worlds", fileName)
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

    IEnumerator InstallAPMod()
    {
        UnityEngine.Debug.Log("START InstallAPMod");

        if (string.IsNullOrEmpty(pvzreplantedAP.url))
        {
            ShowInfo("ERROR: AP Mod URL is empty!");
            yield break;
        }

        string modsPath = Path.Combine(pvzPath, "Mods");
        Directory.CreateDirectory(modsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "PVZR_AP_Extract");

        yield return downloader.DownloadAndExtract(pvzreplantedAP, Application.persistentDataPath, extractPath);

        string foundModsDir = FindDirectory(extractPath, "Mods");
        if (string.IsNullOrEmpty(foundModsDir))
        {
            UnityEngine.Debug.LogError("Mods folder not found inside AP archive!");
            ShowInfo("ERROR: AP archive does not contain a Mods folder!");
        }
        else
        {
            try
            {
                foreach (string file in Directory.GetFiles(foundModsDir, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(foundModsDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string dest = Path.Combine(modsPath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    if (File.Exists(dest))
                        File.Delete(dest);
                    File.Copy(file, dest);
                }

                UnityEngine.Debug.Log("AP mods copied to Mods folder");
                ShowInfo("AP Mod installed successfully!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed copying AP mods: " + e.Message);
                ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
            }
        }

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    IEnumerator InstallMelonLoader()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("START InstallMelonLoader");

        if (string.IsNullOrEmpty(pvzreplantedMelonLoader.url))
        {
            ShowInfo("ERROR: MelonLoader URL is empty!");
            yield break;
        }

        ShowInfo("Extracting MelonLoader...");

        string extractPath = Path.Combine(Application.persistentDataPath, "PVZR_MelonTemp");

        yield return downloader.DownloadAndExtract(pvzreplantedMelonLoader, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, pvzPath);

        Directory.CreateDirectory(Path.Combine(pvzPath, "Mods"));

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallMelonLoader");
        yield return null;
    }

    void CopyMelonLoaderFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyMelonLoaderFiles");

        try
        {
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("All MelonLoader files copied to root");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying MelonLoader files: " + e.Message);
        }

        UnityEngine.Debug.Log("END CopyMelonLoaderFiles");
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
        pvzPath = GetPVZPath();

        if (string.IsNullOrEmpty(pvzPath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(pvzreplantedAP.url, pvzreplantedMelonLoader.url, pvzreplantedApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching PVZ Replanted...");
            yield return new WaitForSeconds(1f);
            LaunchPVZR();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    void LaunchPVZR()
    {
        try
        {
            string exePath = Path.Combine(pvzPath, "Replanted.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("PVZ Replanted launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Replanted.exe not found at: " + exePath);
                ShowInfo("ERROR: Replanted.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch PVZ Replanted: " + e.Message);
            ShowInfo("ERROR: Failed to launch PVZ Replanted!");
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
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

    void CreateVersionFile(string apUrl, string melonloaderUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string melonVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "PVZR APMod Version " + apVersion + ".txt";
            string content = "PVZ Replanted Archipelago Setup Tool\n";
            content += "https://github.com/\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== MELONLOADER ===\n";
            content += "Downloaded from: " + melonloaderUrl + "\n";
            content += "Version: " + melonVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(pvzPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(pvzPath, "Mods");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"PVZR APMod Version .+\.txt");

            if (!string.IsNullOrEmpty(pvzPath))
            {
                string[] rootFiles = Directory.GetFiles(pvzPath);
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

                string modsPath = Path.Combine(pvzPath, "Mods");
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

    string GetPVZPath()
    {
        if (isEpic)
            return GetPVZEpicPath();
        else
            return GetPVZSteamPath();
    }

    string GetPVZSteamPath()
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

    string GetPVZEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\PlantsvsZombiesReplaiXEyJ",
            @"C:\Games\Epic\PlantsvsZombiesReplaiXEyJ",
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
                        if (content.Contains("PlantsvsZombiesReplaiXEyJ") || content.Contains("PlantsvsZombiesReplaiXEyJ"))
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

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<PVZRConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyPVZRConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;

            pvzPath = GetPVZPath();
            UpdatePlatformStatus();
        }
    }

    void ApplyPVZRConfig()
    {
        if (remoteConfig == null)
            return;

        if (pvzreplantedAP != null && !string.IsNullOrEmpty(remoteConfig.pvzreplantedAP))
            pvzreplantedAP.url = remoteConfig.pvzreplantedAP;

        if (pvzreplantedMelonLoader != null && !string.IsNullOrEmpty(remoteConfig.pvzreplantedMelonLoader))
            pvzreplantedMelonLoader.url = remoteConfig.pvzreplantedMelonLoader;

        if (pvzreplantedApworld != null && !string.IsNullOrEmpty(remoteConfig.pvzreplantedApworld))
            pvzreplantedApworld.url = remoteConfig.pvzreplantedApworld;
    }
}