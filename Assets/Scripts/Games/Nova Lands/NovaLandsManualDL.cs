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

public class NovaLandsManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("NOVALANDS FILES")]
    public FileDownloader.FileData novalandsApworld;
    public FileDownloader.FileData novalandsMelonLoader;
    public FileDownloader.FileData novalandsAP;
    public FileDownloader.FileData novalandsMultiClient;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Nova Lands";
    public string epicGameFolderName = "NovaLandsNzKp8";

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

    private const string APModDllName = "NovaLandsArchipelago.dll";
    private const string MultiClientDllName = "Archipelago.MultiClient.Net.dll";

    private string gamePath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool isEpic = false;

    [System.Serializable]
    public class NovaLandsConfig
    {
        public string novalandsAP;
        public string novalandsMultiClient;
        public string novalandsMelonLoader;
        public string novalandsApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
        public string[] apSearchPaths;
    }

    private NovaLandsConfig remoteConfig;

    void Start()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        SelectSteam();

        gamePath = GetGamePath();

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
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + gamePath);
    }

    void SelectEpic()
    {
        isEpic = true;
        gamePath = GetGamePath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + gamePath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(gamePath) ? "Not Found" : "Found";
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
        gamePath = GetGamePath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool melonloader = installMelonLoaderToggle != null && installMelonLoaderToggle.isOn;
        bool apmod = installAPToggle != null && installAPToggle.isOn;
        bool needsGamePath = apmod || melonloader || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(gamePath))
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

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
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

        string modsPath = Path.Combine(gamePath, "Mods");

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

            SafeDeleteFile(Path.Combine(modsPath, APModDllName));
            SafeDeleteFile(Path.Combine(modsPath, MultiClientDllName));

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

        SafeDeleteFile(Path.Combine(modsPath, APModDllName));
        SafeDeleteFile(Path.Combine(modsPath, MultiClientDllName));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(gamePath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(gamePath, "UserData"));
            SafeDeleteDirectory(Path.Combine(gamePath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(gamePath, "Mods"));
            SafeDeleteDirectory(Path.Combine(gamePath, "Plugins"));
            SafeDeleteFile(Path.Combine(gamePath, "version.dll"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning MelonLoader...");

            SafeDeleteDirectory(Path.Combine(gamePath, "MelonLoader"));
            SafeDeleteDirectory(Path.Combine(gamePath, "UserData"));
            SafeDeleteDirectory(Path.Combine(gamePath, "UserLibs"));
            SafeDeleteDirectory(Path.Combine(gamePath, "Mods"));
            SafeDeleteDirectory(Path.Combine(gamePath, "Plugins"));
            SafeDeleteFile(Path.Combine(gamePath, "version.dll"));

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

            if (name.StartsWith("NovaLands APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name == APModDllName || name == MultiClientDllName)
                continue;

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

        CreateVersionFile(novalandsAP.url, novalandsMultiClient.url, novalandsMelonLoader.url, novalandsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Nova Lands...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + novalandsApworld.url);

        if (string.IsNullOrEmpty(novalandsApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = novalandsApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = novalandsApworld.url.Substring(novalandsApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + novalandsApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(novalandsApworld.url, localPath);

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

    IEnumerator InstallAPMod()
    {
        UnityEngine.Debug.Log("START InstallAPMod");

        if (string.IsNullOrEmpty(novalandsAP.url))
        {
            ShowInfo("ERROR: AP Mod URL is empty!");
            yield break;
        }

        if (string.IsNullOrEmpty(novalandsMultiClient.url))
        {
            ShowInfo("ERROR: MultiClient URL is empty!");
            yield break;
        }

        string modsPath = Path.Combine(gamePath, "Mods");
        Directory.CreateDirectory(modsPath);

        string apLocalPath = Path.Combine(Application.persistentDataPath, APModDllName);
        string multiClientLocalPath = Path.Combine(Application.persistentDataPath, MultiClientDllName);

        UnityEngine.Debug.Log("Downloading AP Mod dll from: " + novalandsAP.url);
        yield return DownloadFile(novalandsAP.url, apLocalPath);

        if (!File.Exists(apLocalPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + apLocalPath);
            ShowInfo("ERROR: AP Mod download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("Downloading MultiClient dll from: " + novalandsMultiClient.url);
        yield return DownloadFile(novalandsMultiClient.url, multiClientLocalPath);

        if (!File.Exists(multiClientLocalPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + multiClientLocalPath);
            ShowInfo("ERROR: MultiClient download failed!");
            DeleteTempFile(apLocalPath);
            yield break;
        }

        try
        {
            string apDest = Path.Combine(modsPath, APModDllName);
            string multiClientDest = Path.Combine(modsPath, MultiClientDllName);

            if (File.Exists(apDest))
                File.Delete(apDest);
            File.Copy(apLocalPath, apDest);

            if (File.Exists(multiClientDest))
                File.Delete(multiClientDest);
            File.Copy(multiClientLocalPath, multiClientDest);

            UnityEngine.Debug.Log("AP mod dlls copied to: " + modsPath);
            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed copying AP mod dlls: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
        }

        DeleteTempFile(apLocalPath);
        DeleteTempFile(multiClientLocalPath);

        UnityEngine.Debug.Log("END InstallAPMod");
        yield return null;
    }

    IEnumerator InstallMelonLoader()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("START InstallMelonLoader");

        if (string.IsNullOrEmpty(novalandsMelonLoader.url))
        {
            ShowInfo("ERROR: MelonLoader URL is empty!");
            yield break;
        }

        ShowInfo("Extracting MelonLoader...");

        string extractPath = Path.Combine(Application.persistentDataPath, "NovaLands_MelonTemp");

        yield return downloader.DownloadAndExtract(novalandsMelonLoader, Application.persistentDataPath, extractPath);

        UnityEngine.Debug.Log("Extraction complete");

        CopyMelonLoaderFiles(extractPath, gamePath);

        Directory.CreateDirectory(Path.Combine(gamePath, "Mods"));

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
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(novalandsAP.url, novalandsMultiClient.url, novalandsMelonLoader.url, novalandsApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Nova Lands...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    void LaunchGame()
    {
        try
        {
            string exePath = Path.Combine(gamePath, "Nova Lands.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Nova Lands launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Nova Lands.exe not found at: " + exePath);
                ShowInfo("ERROR: Nova Lands.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Nova Lands: " + e.Message);
            ShowInfo("ERROR: Failed to launch Nova Lands!");
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

    void CreateVersionFile(string apUrl, string multiClientUrl, string melonloaderUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");
            string multiClientVersion = ExtractVersionFromUrl(multiClientUrl, @"/releases/download/([^/]+)/");
            string melonVersion = ExtractVersionFromUrl(melonloaderUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "NovaLands APMod Version " + apVersion + ".txt";
            string content = "Nova Lands Archipelago Setup Tool\n";
            content += "https://github.com/\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== MULTICLIENT ===\n";
            content += "Downloaded from: " + multiClientUrl + "\n";
            content += "Version: " + multiClientVersion + "\n";
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

            string rootVersionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(gamePath, "Mods");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"NovaLands APMod Version .+\.txt");

            if (!string.IsNullOrEmpty(gamePath))
            {
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

                string modsPath = Path.Combine(gamePath, "Mods");
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

    string GetGamePath()
    {
        if (isEpic)
            return GetGameEpicPath();
        else
            return GetGameSteamPath();
    }

    string GetGameSteamPath()
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

    string GetGameEpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\NovaLandsNzKp8",
            @"C:\Games\Epic\NovaLandsNzKp8",
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
                        if (content.Contains("NovaLandsNzKp8"))
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
                remoteConfig = JsonUtility.FromJson<NovaLandsConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyNovaLandsConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;

            gamePath = GetGamePath();
            UpdatePlatformStatus();
        }
    }

    void ApplyNovaLandsConfig()
    {
        if (remoteConfig == null)
            return;

        if (novalandsAP != null && !string.IsNullOrEmpty(remoteConfig.novalandsAP))
            novalandsAP.url = remoteConfig.novalandsAP;

        if (novalandsMultiClient != null && !string.IsNullOrEmpty(remoteConfig.novalandsMultiClient))
            novalandsMultiClient.url = remoteConfig.novalandsMultiClient;

        if (novalandsMelonLoader != null && !string.IsNullOrEmpty(remoteConfig.novalandsMelonLoader))
            novalandsMelonLoader.url = remoteConfig.novalandsMelonLoader;

        if (novalandsApworld != null && !string.IsNullOrEmpty(remoteConfig.novalandsApworld))
            novalandsApworld.url = remoteConfig.novalandsApworld;
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