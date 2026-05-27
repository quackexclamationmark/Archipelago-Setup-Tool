using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class STS2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("STS2 FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apmod;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
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

    private Process sts2Process;
    private string sts2Path;
    private string pendingAction;
    private STS2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class STS2Config
    {
        public string sts2Apworld;
        public string sts2AP;
    }

    void Start()
    {
        sts2Path = GetSTS2Path();
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
    }

    void CleanupProcesses()
    {
        CloseSTS2();
    }

    void ApplySTS2Config()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.sts2Apworld;
        apmod.url = remoteConfig.sts2AP;
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(sts2Path))
        {
            ShowInfo("Slay the Spire 2 path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        sts2Path = GetSTS2Path();

        if (string.IsNullOrEmpty(sts2Path))
            return;

        string modsPath = Path.Combine(sts2Path, "mods");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;

        if (!removeAP)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        CleanupProcesses();

        if (!Directory.Exists(modsPath))
        {
            ShowInfo("AP mods already removed!");
            return;
        }

        ShowInfo("Removing AP mods...");

        SafeDeleteDirectory(Path.Combine(modsPath, "Archipelago"));

        DeleteOldVersionFiles();

        ShowInfo("AP mods removed successfully!");
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Slay the Spire 2...");
            LaunchSTS2();
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
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // ✅ Cibles possibles
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

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string modsPath = Path.Combine(sts2Path, "mods");
        Directory.CreateDirectory(modsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "STS2APModTemp");

        yield return downloader.DownloadAndExtract(apmod, Application.persistentDataPath, extractPath);

        // Cherche le dossier "Archipelago" dans le zip
        string[] dirs = Directory.GetDirectories(extractPath);
        string archipelagoDir = null;

        foreach (string dir in dirs)
        {
            if (Path.GetFileName(dir) == "Archipelago")
            {
                archipelagoDir = dir;
                break;
            }
        }

        if (archipelagoDir != null)
        {
            string targetArchipelagoPath = Path.Combine(modsPath, "Archipelago");

            // Supprimer l'ancien dossier s'il existe
            if (Directory.Exists(targetArchipelagoPath))
                SafeDeleteDirectory(targetArchipelagoPath);

            // Copier le dossier Archipelago
            CopyDirectory(archipelagoDir, targetArchipelagoPath);

            UnityEngine.Debug.Log("Archipelago mod installed to: " + targetArchipelagoPath);

            // Créer les fichiers version APRÈS l'installation
            CreateVersionFile(apmod.url, apworld.url);

            ShowInfo("AP Mod installed successfully!");
        }
        else
        {
            UnityEngine.Debug.LogError("Archipelago folder not found in extracted files!");
            ShowInfo("ERROR: Archipelago folder not found in download!");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APWorldOnlyFlow()
    {
        sts2Path = GetSTS2Path();

        if (string.IsNullOrEmpty(sts2Path))
            yield break;

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchSTS2();
    }

    IEnumerator APModOnlyFlow()
    {
        sts2Path = GetSTS2Path();

        if (string.IsNullOrEmpty(sts2Path))
            yield break;

        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchSTS2();

        ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<STS2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySTS2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchSTS2()
    {
        string exePath = Path.Combine(sts2Path, "Slay the Spire 2.exe");

        if (File.Exists(exePath))
            sts2Process = Process.Start(exePath);
        else
        {
            ShowInfo("ERROR: Could not find Slay the Spire 2 executable!");
        }
    }

    void CloseSTS2()
    {
        try
        {
            if (sts2Process != null && !sts2Process.HasExited)
            {
                sts2Process.Kill();
                sts2Process.Dispose();
                sts2Process = null;
            }
        }
        catch { }
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

    void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string subDir in Directory.GetDirectories(source))
        {
            string destDir = Path.Combine(destination, Path.GetFileName(subDir));
            CopyDirectory(subDir, destDir);
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
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);
            string apworldVersion = ExtractApworldVersion(apworldUrl);

            string versionFileName = "STS2 APMod Version " + apmodVersion + ".txt";

            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            // Créer dans le root avec l'exe
            string rootVersionPath = Path.Combine(sts2Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            // Créer dans le dossier Archipelago
            string archipelagoPath = Path.Combine(sts2Path, "mods", "Archipelago");
            if (Directory.Exists(archipelagoPath))
            {
                string archipelagoVersionPath = Path.Combine(archipelagoPath, versionFileName);
                File.WriteAllText(archipelagoVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Archipelago folder: " + archipelagoVersionPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Archipelago folder not found for version file creation");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"STS2 APMod Version .+\.txt");

            // Supprimer dans le root
            string[] rootFiles = Directory.GetFiles(sts2Path);
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

            // Supprimer dans le dossier Archipelago
            string archipelagoPath = Path.Combine(sts2Path, "mods", "Archipelago");
            if (Directory.Exists(archipelagoPath))
            {
                string[] archipelagoFiles = Directory.GetFiles(archipelagoPath);
                foreach (string file in archipelagoFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in Archipelago folder: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Archipelago folder: " + e.Message);
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
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"/download/([^/]+)/sts2-client\.zip");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string ExtractApworldVersion(string url)
    {
        // Cherche le pattern GitHub releases: /releases/download/VERSION/filename
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        // Fallback: extrait le nom du fichier depuis l'URL
        string fileName = url.Substring(url.LastIndexOf('/') + 1);

        if (fileName.Contains("?"))
            fileName = fileName.Substring(0, fileName.IndexOf("?"));

        // Enlève l'extension .apworld
        if (fileName.EndsWith(".apworld"))
            fileName = fileName.Substring(0, fileName.Length - 8);

        return fileName;
    }

    string GetSTS2Path()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Slay the Spire 2"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Slay the Spire 2"),
            @"D:\Steam\steamapps\common\Slay the Spire 2",
            @"D:\SteamLibrary\steamapps\common\Slay the Spire 2",
            @"E:\Steam\steamapps\common\Slay the Spire 2",
            @"E:\SteamLibrary\steamapps\common\Slay the Spire 2",
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
                    string sts2Path = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Slay the Spire 2");
                    if (Directory.Exists(sts2Path))
                        return sts2Path;

                    sts2Path = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Slay the Spire 2");
                    if (Directory.Exists(sts2Path))
                        return sts2Path;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
