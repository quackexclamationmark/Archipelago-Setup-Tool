using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class Spelunky2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SPELUNKY 2 FILES")]
    public FileDownloader.FileData spelunky2Apworld;
    public FileDownloader.FileData spelunky2Playlunky;
    public FileDownloader.FileData spelunky2AP;

    [Header("FEATURE TOGGLES")]
    public Toggle installSpelunky2ApworldToggle;
    public Toggle installSpelunky2PlaylunkyToggle;
    public Toggle installSpelunky2APToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsOnlyToggle;
    public Toggle fullClearPlaylunkyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process spelunky2Process;
    private string spelunky2Path;
    private string modsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private Spelunky2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class Spelunky2Config
    {
        public string spelunky2Apworld;
        public string spelunky2Playlunky;
        public string spelunky2AP;
    }

    void Start()
    {
        spelunky2Path = GetSpelunky2Path();
        modsPath = Path.Combine(spelunky2Path, "Mods");
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

        if (clearAPModsOnlyToggle != null)
            clearAPModsOnlyToggle.isOn = true;

        if (fullClearPlaylunkyToggle != null)
            fullClearPlaylunkyToggle.isOn = false;

        if (fullClearPlaylunkyToggle != null)
            fullClearPlaylunkyToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseSpelunky2();
    }

    void ApplySpelunky2Config()
    {
        if (remoteConfig == null)
            return;

        spelunky2Apworld.url = remoteConfig.spelunky2Apworld;
        spelunky2Playlunky.url = remoteConfig.spelunky2Playlunky;
        spelunky2AP.url = remoteConfig.spelunky2AP;
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
        if (string.IsNullOrEmpty(spelunky2Path))
        {
            ShowInfo("Spelunky 2 path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installSpelunky2ApworldToggle == null || installSpelunky2ApworldToggle.isOn;
        bool playlunky = installSpelunky2PlaylunkyToggle != null && installSpelunky2PlaylunkyToggle.isOn;
        bool archipelago = installSpelunky2APToggle != null && installSpelunky2APToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (playlunky ? 1 : 0) +
            (archipelago ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(ApWorldOnlyFlow());
            return;
        }

        if (playlunky && count == 1)
        {
            StartCoroutine(PlaylunkyOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        spelunky2Path = GetSpelunky2Path();
        modsPath = Path.Combine(spelunky2Path, "Mods");

        if (string.IsNullOrEmpty(spelunky2Path))
            return;

        bool clearAP = clearAPModsOnlyToggle != null && clearAPModsOnlyToggle.isOn;
        bool fullClean = fullClearPlaylunkyToggle != null && fullClearPlaylunkyToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            string spelunky2APModsPath = Path.Combine(modsPath, "Packs", "Spelunky2-Archipelago");
            SafeDeleteDirectory(spelunky2APModsPath);

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully clear Playlunky?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Clearing Playlunky...");

        // Delete Playlunky files
        SafeDeleteFile(Path.Combine(spelunky2Path, "playlunky_launcher.exe"));
        SafeDeleteFile(Path.Combine(spelunky2Path, "playlunky64.dll"));
        SafeDeleteFile(Path.Combine(spelunky2Path, "readme.txt"));
        SafeDeleteFile(Path.Combine(spelunky2Path, "spel2.dll"));

        // Delete Mods folder
        SafeDeleteDirectory(modsPath);

        ShowInfo("Full clear completed!");
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string packsPath = Path.Combine(modsPath, "Packs");
        if (!Directory.Exists(packsPath))
            return false;

        string[] dirs = Directory.GetDirectories(packsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "Spelunky2-Archipelago" && name != ".db")
            {
                UnityEngine.Debug.LogWarning("Other mod detected in Packs: " + name);
                return true;
            }
        }

        // Vérifie aussi les fichiers, mais ignore les fichiers de version et autres fichiers système
        string[] files = Directory.GetFiles(modsPath);
        System.Text.RegularExpressions.Regex versionPattern = new System.Text.RegularExpressions.Regex(@"Spelunky2 AP Setup Version .+\.txt");

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            // Ignore les fichiers de version
            if (!versionPattern.IsMatch(fileName))
            {
                UnityEngine.Debug.LogWarning("Other file detected in Mods: " + fileName);
                return true;
            }
        }

        // Vérifie les fichiers dans Packs, mais ignore les fichiers système
        string[] packsFiles = Directory.GetFiles(packsPath);
        foreach (string file in packsFiles)
        {
            string fileName = Path.GetFileName(file);

            // Ignore load_order.txt et .db
            if (fileName != "load_order.txt" && fileName != ".db")
            {
                UnityEngine.Debug.LogWarning("Other file detected in Packs: " + fileName);
                return true;
            }
        }

        // Ignore le dossier Extracted et tout son contenu
        string extractedPath = Path.Combine(modsPath, "Extracted");
        if (Directory.Exists(extractedPath))
        {
            UnityEngine.Debug.Log("Extracted folder found and ignored");
        }

        UnityEngine.Debug.Log("No other mods detected");
        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installSpelunky2ApworldToggle == null || installSpelunky2ApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallSpelunky2APWorld();
        }

        if (installSpelunky2PlaylunkyToggle != null && installSpelunky2PlaylunkyToggle.isOn)
        {
            ShowInfo("Installing Playlunky...");
            yield return InstallSpelunky2Playlunky();
        }

        if (installSpelunky2APToggle != null && installSpelunky2APToggle.isOn)
        {
            ShowInfo("Installing Spelunky2-Archipelago...");
            yield return InstallSpelunky2AP();
        }

        CreateVersionFile(spelunky2Apworld.url, spelunky2Playlunky.url, spelunky2AP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Playlunky...");
            LaunchSpelunky2();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator InstallSpelunky2APWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Spelunky 2 APWorld URL: " + spelunky2Apworld.url);

        if (string.IsNullOrEmpty(spelunky2Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = spelunky2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = spelunky2Apworld.url.Substring(spelunky2Apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + spelunky2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(spelunky2Apworld.url, localPath);

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

    IEnumerator InstallSpelunky2Playlunky()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "Spelunky2PlaylunkyTemp");

        yield return downloader.DownloadAndExtract(spelunky2Playlunky, Application.persistentDataPath, extractPath);

        try
        {
            // Copy all content from ZIP directly to Spelunky 2 folder
            foreach (string file in Directory.GetFiles(extractPath))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(spelunky2Path, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (string dir in Directory.GetDirectories(extractPath))
            {
                string dirName = Path.GetFileName(dir);
                string targetDir = Path.Combine(spelunky2Path, dirName);
                PerformDirectoryCopy(dir, targetDir);
            }

            // Create Mods/Packs folder if it doesn't exist
            string packsPath = Path.Combine(modsPath, "Packs");
            Directory.CreateDirectory(packsPath);

            UnityEngine.Debug.Log("Playlunky installed to: " + spelunky2Path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Playlunky: " + e.Message);
            ShowInfo("ERROR: Failed to install Playlunky\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    void PerformDirectoryCopy(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            PerformDirectoryCopy(dir, targetSubDir);
        }
    }

    IEnumerator InstallSpelunky2AP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "Spelunky2APTemp");

        yield return downloader.DownloadAndExtract(spelunky2AP, Application.persistentDataPath, extractPath);

        string packsPath = Path.Combine(modsPath, "Packs");
        Directory.CreateDirectory(packsPath);

        string targetPath = Path.Combine(packsPath, "Spelunky2-Archipelago");

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        try
        {
            Directory.CreateDirectory(targetPath);

            // Copy all content from ZIP to the target Spelunky2-Archipelago folder
            foreach (string file in Directory.GetFiles(extractPath))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetPath, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (string dir in Directory.GetDirectories(extractPath))
            {
                string dirName = Path.GetFileName(dir);
                string targetSubDir = Path.Combine(targetPath, dirName);
                PerformDirectoryCopy(dir, targetSubDir);
            }

            UnityEngine.Debug.Log("Spelunky2-Archipelago installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Spelunky2-Archipelago: " + e.Message);
            ShowInfo("ERROR: Failed to install Spelunky2-Archipelago\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator ApWorldOnlyFlow()
    {
        spelunky2Path = GetSpelunky2Path();
        modsPath = Path.Combine(spelunky2Path, "Mods");

        if (string.IsNullOrEmpty(spelunky2Path))
            yield break;

        yield return InstallSpelunky2APWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchSpelunky2();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator PlaylunkyOnlyFlow()
    {
        ShowInfo("Installing Playlunky...");
        yield return InstallSpelunky2Playlunky();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Playlunky...");
            LaunchSpelunky2();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            ShowInfo("Installation complete!");
        }

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
            remoteConfig = JsonUtility.FromJson<Spelunky2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySpelunky2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchSpelunky2()
    {
        string playlunkyLauncherPath = Path.Combine(spelunky2Path, "playlunky_launcher.exe");

        if (File.Exists(playlunkyLauncherPath))
        {
            UnityEngine.Debug.Log("Launching playlunky_launcher.exe with admin privileges");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = playlunkyLauncherPath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = spelunky2Path;

                spelunky2Process = Process.Start(psi);
                UnityEngine.Debug.Log("Process started successfully");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // L'utilisateur a annulé l'UAC ou pas d'admin
                UnityEngine.Debug.LogWarning("User cancelled UAC or admin privileges denied: " + ex.Message);
                ShowInfo("Admin privileges required to launch Playlunky!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to launch: " + e.Message);
                ShowInfo("ERROR: Failed to launch Playlunky\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogError("playlunky_launcher.exe not found at: " + playlunkyLauncherPath);
            ShowInfo("ERROR: playlunky_launcher.exe not found!");
        }
    }

    void CloseSpelunky2()
    {
        try
        {
            if (spelunky2Process != null && !spelunky2Process.HasExited)
            {
                spelunky2Process.Kill();
                spelunky2Process.Dispose();
                spelunky2Process = null;
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
        if (clearAPModsOnlyToggle != null)
        {
            clearAPModsOnlyToggle.isOn = false;
            clearAPModsOnlyToggle.interactable = !value;
        }
    }

    void CreateVersionFile(string apworldUrl, string playlunkyUrl, string apUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string playlunkyVersion = ExtractVersionFromUrl(playlunkyUrl, @"/([^/]+)\.zip");
            string apVersion = ExtractVersionFromUrl(apUrl, @"/([^/]+)\.zip");

            string versionFileName = "Spelunky2 AP Setup Version " + apVersion + ".txt";
            string content = "Spelunky 2 Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== SPELUNKY 2 APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== PLAYLUNKY ===\n";
            content += "Downloaded from: " + playlunkyUrl + "\n";
            content += "Version: " + playlunkyVersion + "\n";
            content += "\n";
            content += "=== SPELUNKY 2 ARCHIPELAGO ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            if (Directory.Exists(modsPath))
            {
                string versionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(versionPath, content);
                UnityEngine.Debug.Log("Version file created: " + versionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Spelunky2 AP Setup Version .+\.txt");

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
                            UnityEngine.Debug.Log("Deleted old version file: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file: " + e.Message);
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

    string GetSpelunky2Path()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Spelunky 2"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Spelunky 2"),
            @"D:\Steam\steamapps\common\Spelunky 2",
            @"D:\SteamLibrary\steamapps\common\Spelunky 2",
            @"D:\steamapps\common\Spelunky 2",
            @"E:\Steam\steamapps\common\Spelunky 2",
            @"E:\SteamLibrary\steamapps\common\Spelunky 2",
            @"E:\steamapps\common\Spelunky 2",
            @"E:\Program Files (x86)\steamapps\common\Spelunky 2",
            @"E:\Program Files\steamapps\common\Spelunky 2",
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
                    string spelunky2Path = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Spelunky 2");
                    if (Directory.Exists(spelunky2Path))
                        return spelunky2Path;

                    spelunky2Path = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Spelunky 2");
                    if (Directory.Exists(spelunky2Path))
                        return spelunky2Path;

                    spelunky2Path = Path.Combine(drive.Name, "steamapps", "common", "Spelunky 2");
                    if (Directory.Exists(spelunky2Path))
                        return spelunky2Path;

                    spelunky2Path = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Spelunky 2");
                    if (Directory.Exists(spelunky2Path))
                        return spelunky2Path;

                    spelunky2Path = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Spelunky 2");
                    if (Directory.Exists(spelunky2Path))
                        return spelunky2Path;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}