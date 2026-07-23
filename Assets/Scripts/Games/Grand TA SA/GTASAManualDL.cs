using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public class GTASAManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GTASA FILES")]
    public FileDownloader.FileData gtasaApworld;
    public FileDownloader.FileData gtasaASI;
    public FileDownloader.FileData gtasaWidescreen;
    public FileDownloader.FileData gtasaAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installGTASAApworldToggle;
    public Toggle installGTASAASIToggle;
    public Toggle installGTASAWidescreenToggle;
    public Toggle installGTASAAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT BUTTON")]
    public Button revertButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gtasaProcess;
    private bool gtasaLaunchedAsHelper = false;
    private string gtasaPath;
    private string backupPath;
    private string pendingAction;
    private GTASAConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class GTASAConfig
    {
        public string gtasaApworld;
        public string gtasaASI;
        public string gtasaWidescreen;
        public string gtasaAP;
    }

    void Start()
    {
        gtasaPath = GetGTASAPath();
        backupPath = Path.Combine(gtasaPath, "backup");
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (revertButton != null) revertButton.onClick.AddListener(OnRevertButtonClicked);
    }

    // =========================================================

    void ApplyGTASAConfig()
    {
        if (remoteConfig == null) return;
        gtasaApworld.url = remoteConfig.gtasaApworld;
        gtasaASI.url = remoteConfig.gtasaASI;
        gtasaWidescreen.url = remoteConfig.gtasaWidescreen;
        gtasaAP.url = remoteConfig.gtasaAP;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup all the files?", "Setup");

    void OnRevertButtonClicked() => ShowConfirmation("Are you sure you want to revert?", "Revert");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        // wait for remote config if necessary
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        if (string.IsNullOrEmpty(gtasaPath))
        {
            ShowInfo("GTA San Andreas path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installGTASAApworldToggle == null || installGTASAApworldToggle.isOn;
        bool asi = installGTASAASIToggle != null && installGTASAASIToggle.isOn;
        bool widescreen = installGTASAWidescreenToggle != null && installGTASAWidescreenToggle.isOn;
        bool apmod = installGTASAAPToggle != null && installGTASAAPToggle.isOn;

        int count = (apworld ? 1 : 0) + (asi ? 1 : 0) + (widescreen ? 1 : 0) + (apmod ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupFlow());
    }

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    IEnumerator SetupFlow()
    {
        // Create backup folder and backup vorbisFile.dll if it exists
        ShowInfo("Creating backup...");
        yield return new WaitForSeconds(0.5f);

        string vorbisSourcePath = Path.Combine(gtasaPath, "vorbisFile.dll");

        if (File.Exists(vorbisSourcePath))
        {
            try
            {
                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                string backupFilePath = Path.Combine(backupPath, "vorbisFile.dll");
                File.Copy(vorbisSourcePath, backupFilePath, true);
                UnityEngine.Debug.Log("Backup created: " + backupFilePath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to create backup: " + e.Message);
                ShowInfo("ERROR: Failed to create backup\n" + e.Message);
                yield break;
            }
        }

        yield return StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        gtasaPath = GetGTASAPath();
        if (string.IsNullOrEmpty(gtasaPath)) return;

        CleanupProcesses();
        ShowInfo("Reverting changes...");

        // Remove installed files
        SafeDeleteFile(Path.Combine(gtasaPath, "GTASAClient.exe"));
        SafeDeleteFile(Path.Combine(gtasaPath, "vorbisFile.dll"));

        // Remove scripts folder
        string scriptsPath = Path.Combine(gtasaPath, "scripts");
        if (Directory.Exists(scriptsPath))
        {
            SafeDeleteDirectory(scriptsPath);
        }

        // Restore vorbisFile.dll from backup if it exists
        string backupFilePath = Path.Combine(backupPath, "vorbisFile.dll");
        if (File.Exists(backupFilePath))
        {
            try
            {
                string targetPath = Path.Combine(gtasaPath, "vorbisFile.dll");
                File.Copy(backupFilePath, targetPath, true);
                UnityEngine.Debug.Log("vorbisFile.dll restored from backup: " + targetPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to restore vorbisFile.dll: " + e.Message);
                ShowInfo("ERROR: Failed to restore vorbisFile.dll\n" + e.Message);
            }
        }

        // Remove backup folder
        if (Directory.Exists(backupPath))
        {
            SafeDeleteDirectory(backupPath);
        }

        DeleteOldVersionFiles();
        ShowInfo("Revert completed!");
    }

    IEnumerator InstallFlow()
    {
        // wait for config loaded
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        bool installApworld = installGTASAApworldToggle == null || installGTASAApworldToggle.isOn;
        bool installASI = installGTASAASIToggle != null && installGTASAASIToggle.isOn;
        bool installWidescreen = installGTASAWidescreenToggle != null && installGTASAWidescreenToggle.isOn;
        bool installApmod = installGTASAAPToggle != null && installGTASAAPToggle.isOn;

        // Installation order: APWorld -> ASI -> Widescreen -> AP

        if (installApworld)
        {
            ShowInfo("Installing AP World...");
            yield return InstallAPWorld();
        }

        if (installASI)
        {
            ShowInfo("Installing ASI file...");
            yield return InstallASI();
        }

        if (installWidescreen)
        {
            ShowInfo("Installing Widescreen Fix...");
            yield return InstallWidescreen();
        }

        if (installApmod)
        {
            ShowInfo("Installing GTA SA Archipelago...");
            yield return InstallAPMod();
        }

        CreateVersionFile(gtasaApworld.url, gtasaASI.url, gtasaWidescreen.url, gtasaAP.url);

        ShowInfo("Installation complete!");
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(2f);
            LaunchGTASAClient(false);
        }
    }

    // Installs the .apworld into Archipelago/custom_worlds (not into plugins)
    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(gtasaApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = gtasaApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = gtasaApworld.url.Substring(gtasaApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + gtasaApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(gtasaApworld.url, localPath);

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
        }
    }

    // Downloads and installs vorbisFile.dll directly to GTA SA directory
    IEnumerator InstallASI()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(gtasaASI.url))
        {
            ShowInfo("ERROR: ASI URL is empty!");
            UnityEngine.Debug.LogError("ASI URL not set!");
            yield break;
        }

        string fileName = "vorbisFile.dll";
        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string targetPath = Path.Combine(gtasaPath, fileName);

        UnityEngine.Debug.Log("Downloading ASI from: " + gtasaASI.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(gtasaASI.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: ASI download failed!");
            yield break;
        }

        try
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Copy(localPath, targetPath, true);
            UnityEngine.Debug.Log("ASI file copied to: " + targetPath);
            ShowInfo("ASI installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy ASI: " + e.Message);
            ShowInfo("ERROR: Failed to install ASI\n" + e.Message);
        }
    }

    // Downloads, extracts and installs Widescreen Fix
    IEnumerator InstallWidescreen()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(gtasaWidescreen.url))
        {
            ShowInfo("ERROR: Widescreen URL is empty!");
            UnityEngine.Debug.LogError("Widescreen URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "WidescreenTemp");

        UnityEngine.Debug.Log("Downloading Widescreen from: " + gtasaWidescreen.url);

        yield return downloader.DownloadAndExtract(gtasaWidescreen, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extract failed for Widescreen");
            ShowInfo("ERROR: Widescreen extraction failed!");
            yield break;
        }

        try
        {
            MoveDirectory(extractPath, gtasaPath);
            SafeDeleteDirectory(extractPath);
            UnityEngine.Debug.Log("Widescreen files moved to: " + gtasaPath);
            ShowInfo("Widescreen installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Widescreen: " + e.Message);
            ShowInfo("ERROR: Failed to install Widescreen\n" + e.Message);
        }
    }

    // Downloads and installs AP Mod
    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(gtasaAP.url))
        {
            ShowInfo("ERROR: AP URL is empty!");
            UnityEngine.Debug.LogError("AP URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "GTASAAPTemp");

        UnityEngine.Debug.Log("Downloading AP Mod from: " + gtasaAP.url);

        yield return downloader.DownloadAndExtract(gtasaAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extract failed for AP Mod");
            ShowInfo("ERROR: AP Mod extraction failed!");
            yield break;
        }

        try
        {
            // Extract Archipelago.SA.asi to scripts folder
            string asiPath = FindFile(extractPath, "Archipelago.SA.asi");
            if (!string.IsNullOrEmpty(asiPath))
            {
                string scriptsPath = Path.Combine(gtasaPath, "scripts");
                Directory.CreateDirectory(scriptsPath);
                string targetASI = Path.Combine(scriptsPath, "Archipelago.SA.asi");
                if (File.Exists(targetASI)) File.Delete(targetASI);
                File.Copy(asiPath, targetASI, true);
                UnityEngine.Debug.Log("Archipelago.SA.asi copied to: " + targetASI);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Archipelago.SA.asi not found in extracted files");
            }

            // Extract GTASAClient.exe to GTA SA root
            string clientPath = FindFile(extractPath, "GTASAClient.exe");
            if (!string.IsNullOrEmpty(clientPath))
            {
                string targetClient = Path.Combine(gtasaPath, "GTASAClient.exe");
                if (File.Exists(targetClient)) File.Delete(targetClient);
                File.Copy(clientPath, targetClient, true);
                UnityEngine.Debug.Log("GTASAClient.exe copied to: " + targetClient);
            }
            else
            {
                UnityEngine.Debug.LogWarning("GTASAClient.exe not found in extracted files");
            }

            SafeDeleteDirectory(extractPath);
            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install AP Mod: " + e.Message);
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
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
                UnityEngine.Debug.Log("Download complete! " + savePath);
            }
        }
    }

    // LaunchGTASAClient: accepts asHelper flag to mark the process as killable by CloseGTASA()
    void LaunchGTASAClient(bool asHelper = false)
    {
        string clientPath = Path.Combine(gtasaPath, "GTASAClient.exe");
        if (File.Exists(clientPath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = clientPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                gtasaProcess = Process.Start(psi);
                gtasaLaunchedAsHelper = asHelper;
                UnityEngine.Debug.Log($"GTASAClient launched for {(asHelper ? "config generation (helper)" : "user/interactive launch")}.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("GTASAClient executable not found: " + clientPath);
        }
    }

    void CloseGTASA(bool force = false)
    {
        try
        {
            if (gtasaProcess != null && !gtasaProcess.HasExited)
            {
                if (force || gtasaLaunchedAsHelper)
                {
                    gtasaProcess.Kill();
                    gtasaProcess.Dispose();
                    gtasaProcess = null;
                    gtasaLaunchedAsHelper = false;
                    UnityEngine.Debug.Log("GTASAClient process closed.");
                }
                else
                {
                    UnityEngine.Debug.Log("CloseGTASA: process started interactively, not killing it.");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("CloseGTASA error: " + ex.Message);
        }
    }

    void CleanupProcesses() => CloseGTASA();

    void SafeDeleteFile(string path) => StartCoroutine(DeleteFileForce(path));

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;
        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                if (!File.Exists(path)) yield break;
            }
            catch { }
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(file, dest);
        }
    }

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName) return file;
        }
        catch { }
        return "";
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(gtasaPath))
            {
                string fn = Path.GetFileName(file);
                if (fn.StartsWith("GTA SA Archipelago Version") && fn.EndsWith(".txt")) SafeDeleteFile(file);
            }
        }
        catch { }
    }

    void CreateVersionFile(string apworldUrl, string asiUrl, string widescreenUrl, string apUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string asiVersion = "vorbisFile.dll";
            string widescreenVersion = ExtractVersionFromUrl(widescreenUrl, @"/([^/]+)\.zip");
            string apVersion = ExtractVersionFromUrl(apUrl, @"/([^/]+)\.zip");

            string versionFileName = "GTA SA Archipelago Version " + apVersion + ".txt";
            string content = "GTA San Andreas Archipelago Setup Tool\n\n";
            content += "=== GTASA ARCHIPELAGO ===\nDownloaded from: " + apUrl + "\nVersion: " + apVersion + "\n\n";
            content += "=== APWORLD ===\nDownloaded from: " + apworldUrl + "\nName: " + apworldVersion + ".apworld\n\n";
            content += "=== ASI ===\nDownloaded from: " + asiUrl + "\nFile: " + asiVersion + "\n\n";
            content += "=== WIDESCREEN FIX ===\nDownloaded from: " + widescreenUrl + "\nVersion: " + widescreenVersion + "\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(gtasaPath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url)) return "Unknown";
        Match match = Regex.Match(url, pattern);
        return match.Success ? match.Groups[1].Value : "Unknown";
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
            remoteConfig = JsonUtility.FromJson<GTASAConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGTASAConfig();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    string GetGTASAPath()
    {
        return GetGTASASteamPath();
    }

    string GetGTASASteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Grand Theft Auto San Andreas"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "GTA San Andreas - The Definitive Edition"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Grand Theft Auto San Andreas"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "GTA San Andreas - The Definitive Edition"),
            @"D:\Steam\steamapps\common\Grand Theft Auto San Andreas",
            @"D:\Steam\steamapps\common\GTA San Andreas - The Definitive Edition",
            @"D:\SteamLibrary\steamapps\common\Grand Theft Auto San Andreas",
            @"D:\SteamLibrary\steamapps\common\GTA San Andreas - The Definitive Edition",
            @"C:\Program Files (x86)\steamapps\common\Grand Theft Auto San Andreas",
            @"C:\Program Files (x86)\steamapps\common\GTA San Andreas - The Definitive Edition",
            @"C:\Program Files\steamapps\common\Grand Theft Auto San Andreas",
            @"C:\Program Files\steamapps\common\GTA San Andreas - The Definitive Edition",
        };

        foreach (string p in quickPaths)
        {
            try { if (Directory.Exists(p)) return p; } catch { }
        }

        try
        {
            var drives = System.IO.DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed) continue;
                try
                {
                    string[] pathPatterns = new string[]
                    {
                        Path.Combine(drive.Name, "Steam", "steamapps", "common", "Grand Theft Auto San Andreas"),
                        Path.Combine(drive.Name, "Steam", "steamapps", "common", "GTA San Andreas - The Definitive Edition"),
                        Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Grand Theft Auto San Andreas"),
                        Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "GTA San Andreas - The Definitive Edition"),
                        Path.Combine(drive.Name, "steamapps", "common", "Grand Theft Auto San Andreas"),
                        Path.Combine(drive.Name, "steamapps", "common", "GTA San Andreas - The Definitive Edition"),
                    };

                    foreach (string tryPath in pathPatterns)
                    {
                        if (Directory.Exists(tryPath)) return tryPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("GTA San Andreas not found.");
        return "";
    }
}
