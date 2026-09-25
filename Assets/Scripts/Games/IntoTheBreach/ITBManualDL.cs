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

public class ITBManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ITB FILES")]
    public FileDownloader.FileData itbApworld;
    public FileDownloader.FileData itbAPLib;
    public FileDownloader.FileData itbModLoader;
    public FileDownloader.FileData itbAP;
    public FileDownloader.FileData itbMemedit;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Into the Breach";
    public string epicGameFolderName = "IntoTheBreach";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPLibToggle;
    public Toggle installModLoaderToggle;
    public Toggle installAPToggle;
    public Toggle installMemeditToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string itbPath;
    private string pendingAction;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class ITBConfig
    {
        public string itbApworld;
        public string itbAPLib;
        public string itbModLoader;
        public string itbAP;
        public string itbMemedit;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
        public string[] apSearchPaths;
    }

    private ITBConfig remoteConfig;

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        itbPath = GetITBPath();

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

        StartCoroutine(LoadRemoteConfig());
    }

    void OnSteamButtonClicked() { SelectSteam(); }
    void OnEpicButtonClicked() { SelectEpic(); }

    void SelectSteam()
    {
        isEpic = false;
        itbPath = GetITBPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + itbPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        itbPath = GetITBPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + itbPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(itbPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
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
                StartCoroutine(RevertFlow());
                break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        itbPath = GetITBPath();

        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool aplib = installAPLibToggle != null && installAPLibToggle.isOn;
        bool modloader = installModLoaderToggle != null && installModLoaderToggle.isOn;
        bool apmod = installAPToggle != null && installAPToggle.isOn;
        bool memedit = installMemeditToggle != null && installMemeditToggle.isOn;

        bool needsGamePath = modloader || apmod || memedit;

        if (needsGamePath && string.IsNullOrEmpty(itbPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (aplib ? 1 : 0) + (modloader ? 1 : 0) + (apmod ? 1 : 0) + (memedit ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld, aplib, modloader, apmod, memedit));
    }

    IEnumerator InstallFlow(bool apworld, bool aplib, bool modloader, bool apmod, bool memedit)
    {
        yield return new WaitUntil(() => configLoaded);

        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallApworld();
        }

        if (aplib)
        {
            ShowInfo("Installing AP Lib...");
            yield return InstallApLib();
        }

        if (modloader)
        {
            ShowInfo("Installing ITBModLoader...");
            yield return InstallModLoader();
        }

        if (apmod)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallApMod();
        }

        if (memedit)
        {
            ShowInfo("Installing memedit...");
            yield return InstallMemedit();
        }

        if (modloader || apmod || memedit)
            CreateVersionFile(itbAP.url, itbModLoader.url, itbMemedit.url, itbApworld.url, itbAPLib.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Into the Breach...");
            yield return new WaitForSeconds(1f);
            LaunchITB();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallApworld()
    {
        if (string.IsNullOrEmpty(itbApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = itbApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = itbApworld.url.Substring(itbApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + itbApworld.url);

        yield return DownloadFile(itbApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string customWorldsDir = GetApTargetPath("custom_worlds");

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago custom_worlds directory not found. Please report it on the Discord server.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);

        if (File.Exists(target))
        {
            try { File.Delete(target); } catch { }
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
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    IEnumerator InstallApLib()
    {
        if (string.IsNullOrEmpty(itbAPLib.url))
        {
            ShowInfo("ERROR: AP Lib URL is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "ITB_APLib_Extract");

        yield return downloader.DownloadAndExtract(itbAPLib, Application.persistentDataPath, extractPath);

        string libDir = GetApTargetPath("lib");

        if (string.IsNullOrEmpty(libDir))
        {
            ShowInfo("Archipelago lib directory not found. Please report it on the Discord server.");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        CopyDirectoryContentsFlat(extractPath, libDir);
        ShowInfo("AP Lib installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallModLoader()
    {
        if (string.IsNullOrEmpty(itbModLoader.url))
        {
            ShowInfo("ERROR: Mod Loader URL is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "ITB_ModLoader_Extract");

        yield return downloader.DownloadAndExtract(itbModLoader, Application.persistentDataPath, extractPath);

        CopyDirectoryContentsFlat(extractPath, itbPath);
        ShowInfo("Mod Loader installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallApMod()
    {
        if (string.IsNullOrEmpty(itbAP.url))
        {
            ShowInfo("ERROR: AP Mod URL is empty!");
            yield break;
        }

        string modsPath = Path.Combine(itbPath, "mods");
        string extractPath = Path.Combine(Application.persistentDataPath, "ITB_AP_Extract");

        yield return downloader.DownloadAndExtract(itbAP, Application.persistentDataPath, extractPath);

        CopyDirectoryContentsFlat(extractPath, modsPath);
        ShowInfo("AP Mod installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMemedit()
    {
        if (string.IsNullOrEmpty(itbMemedit.url))
        {
            ShowInfo("ERROR: Memedit URL is empty!");
            yield break;
        }

        string modsPath = Path.Combine(itbPath, "mods");
        string extractPath = Path.Combine(Application.persistentDataPath, "ITB_Memedit_Extract");

        yield return downloader.DownloadAndExtract(itbMemedit, Application.persistentDataPath, extractPath);

        CopyDirectoryContentsFlat(extractPath, modsPath);
        ShowInfo("Memedit installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    void CopyDirectoryContentsFlat(string sourceDir, string targetDir)
    {
        try
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("Files copied from " + sourceDir + " to " + targetDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying files: " + e.Message);
        }
    }

    IEnumerator RevertFlow()
    {
        itbPath = GetITBPath();

        if (string.IsNullOrEmpty(itbPath))
        {
            ShowInfo("Game installation not found.");
            yield break;
        }

        ShowInfo("Reverting ITB mods...");

        SafeDeleteDirectory(Path.Combine(itbPath, "mods"));

        yield return RevertDllSwap(Path.Combine(itbPath, "lua5.1.dll"), Path.Combine(itbPath, "lua5.1-original.dll"));
        yield return RevertDllSwap(Path.Combine(itbPath, "SDL2.dll"), Path.Combine(itbPath, "SDL2-original.dll"));

        SafeDeleteFile(Path.Combine(itbPath, "ftldat.dll"));
        SafeDeleteFile(Path.Combine(itbPath, "itb_io.dll"));
        SafeDeleteFile(Path.Combine(itbPath, "opengl32.dll"));
        SafeDeleteFile(Path.Combine(itbPath, "uninstall_modloader"));

        SafeDeleteDirectory(Path.Combine(itbPath, "resources", "mods"));

        SafeDeleteDirectory(Path.Combine(itbPath, "scripts", "mod_loader"));
        SafeDeleteFile(Path.Combine(itbPath, "scripts", "modloader.lua"));
        SafeDeleteFile(Path.Combine(itbPath, "scripts", "modloader.lua.bak"));

        DeleteOldVersionFiles();

        yield return new WaitForSeconds(1f);

        ShowInfo("Revert completed!");
    }

    // If an "-original" backup exists for this dll, delete the current dll and rename
    // the backup by stripping "-original" from its name. If no backup exists, the
    // current dll is left untouched.
    IEnumerator RevertDllSwap(string currentPath, string originalPath)
    {
        if (!File.Exists(originalPath))
        {
            UnityEngine.Debug.Log("No original backup found for " + currentPath + ", skipping.");
            yield break;
        }

        yield return DeleteFileForce(currentPath);

        if (File.Exists(currentPath))
        {
            UnityEngine.Debug.LogWarning("Could not delete " + currentPath + ", skipping rename of " + originalPath);
            yield break;
        }

        try
        {
            File.Move(originalPath, currentPath);
            UnityEngine.Debug.Log("Restored " + currentPath + " from " + originalPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to restore " + currentPath + ": " + e.Message);
        }
    }

    void LaunchITB()
    {
        try
        {
            string exePath = Path.Combine(itbPath, "Breach.exe");

            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
                startInfo.WorkingDirectory = itbPath;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";

                Process.Start(startInfo);
                UnityEngine.Debug.Log("Into the Breach launched (admin): " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Breach.exe not found at: " + exePath);
                ShowInfo("ERROR: Breach.exe not found!");
            }
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            UnityEngine.Debug.LogWarning("Launch cancelled or elevation denied: " + e.Message);
            ShowInfo("Launch cancelled (admin approval was declined).");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Into the Breach: " + e.Message);
            ShowInfo("ERROR: Failed to launch Into the Breach!");
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

    void CreateVersionFile(string apUrl, string modLoaderUrl, string memeditUrl, string apworldUrl, string apLibUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string modLoaderVersion = ExtractVersionFromUrl(modLoaderUrl, @"/releases/download/([^/]+)/");
            string memeditVersion = ExtractVersionFromUrl(memeditUrl, @"/releases/download/([^/]+)/");
            string apLibVersion = ExtractVersionFromUrl(apLibUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "ITB Mod Version " + apVersion + ".txt";
            string content = "Into the Breach Archipelago Setup Tool\n";
            content += "https://github.com/\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== MEMEDIT ===\n";
            content += "Downloaded from: " + memeditUrl + "\n";
            content += "Version: " + memeditVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== AP LIB ===\n";
            content += "Downloaded from: " + apLibUrl + "\n";
            content += "Version: " + apLibVersion + "\n";
            content += "\n";
            content += "=== MOD LOADER ===\n";
            content += "Downloaded from: " + modLoaderUrl + "\n";
            content += "Version: " + modLoaderVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(itbPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(itbPath, "mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in mods: " + modsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ITB Mod Version .+\.txt");

            if (!string.IsNullOrEmpty(itbPath))
            {
                if (Directory.Exists(itbPath))
                {
                    string[] rootFiles = Directory.GetFiles(itbPath);
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
                }

                string modsPath = Path.Combine(itbPath, "mods");
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
                                UnityEngine.Debug.Log("Deleted old version file in mods: " + fileName);
                            }
                            catch (System.Exception e)
                            {
                                UnityEngine.Debug.LogWarning("Could not delete old version file in mods: " + e.Message);
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

    string GetITBPath()
    {
        if (isEpic)
            return GetITBEpicPath();
        else
            return GetITBSteamPath();
    }

    string GetITBSteamPath()
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

    string GetITBEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\IntoTheBreach",
            @"C:\Games\Epic\IntoTheBreach",
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
                        if (content.Contains(epicGameFolderName))
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

    // Searches every fixed drive under each configured apSearchPaths entry for a
    // subfolder (e.g. "custom_worlds" or "lib") inside the Archipelago install.
    // Nothing here is hardcoded to a specific drive or path - it's entirely
    // driven by remoteConfig.apSearchPaths.
    string GetApTargetPath(string subfolder)
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
                            string path = Path.Combine(drive.Name, relativePath, subfolder);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Archipelago " + subfolder + " (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Archipelago " + subfolder + " directory not found.");
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
                remoteConfig = JsonUtility.FromJson<ITBConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyITBConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;

            itbPath = GetITBPath();
            UpdatePlatformStatus();
        }
    }

    void ApplyITBConfig()
    {
        if (remoteConfig == null)
            return;

        if (itbApworld != null && !string.IsNullOrEmpty(remoteConfig.itbApworld))
            itbApworld.url = remoteConfig.itbApworld;

        if (itbAPLib != null && !string.IsNullOrEmpty(remoteConfig.itbAPLib))
            itbAPLib.url = remoteConfig.itbAPLib;

        if (itbModLoader != null && !string.IsNullOrEmpty(remoteConfig.itbModLoader))
            itbModLoader.url = remoteConfig.itbModLoader;

        if (itbAP != null && !string.IsNullOrEmpty(remoteConfig.itbAP))
            itbAP.url = remoteConfig.itbAP;

        if (itbMemedit != null && !string.IsNullOrEmpty(remoteConfig.itbMemedit))
            itbMemedit.url = remoteConfig.itbMemedit;
    }
}