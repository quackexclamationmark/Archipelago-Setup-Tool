using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class DOOMEternalManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DOOM ETERNAL FILES")]
    public FileDownloader.FileData doometernalAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "DOOMEternal";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPToggle;
    public Toggle installAPWorldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchDoomLauncherAfterSetupToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process doometernalProcess;
    private string doometernalPath;
    private string doometernalBasePath;
    private string modsPath;
    private string documentsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private DOOMEternalConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class DOOMEternalConfig
    {
        public string doometernalAP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        doometernalPath = GetDOOMEternalPath();
        doometernalBasePath = Path.Combine(doometernalPath, "base");
        modsPath = Path.Combine(doometernalPath, "Mods");
        documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchDoomLauncherAfterSetupToggle != null)
            launchDoomLauncherAfterSetupToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.isOn = false;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseDOOMEternal();
    }

    void ApplyDOOMEternalConfig()
    {
        if (remoteConfig == null)
            return;

        doometernalAP.url = remoteConfig.doometernalAP;
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
        doometernalPath = GetDOOMEternalPath();

        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        if ((ap || apworld) && (string.IsNullOrEmpty(doometernalPath) || !Directory.Exists(doometernalPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (ap ? 1 : 0) +
            (apworld ? 1 : 0);

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

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

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchDoomLauncherAfterSetupToggle != null && launchDoomLauncherAfterSetupToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal Archipelago Launcher...");
            LaunchDoomEternalArchipelagoLauncher();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        doometernalPath = GetDOOMEternalPath();

        doometernalBasePath = Path.Combine(doometernalPath, "base");
        modsPath = Path.Combine(doometernalPath, "Mods");

        if (string.IsNullOrEmpty(doometernalPath))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanModsToggle != null && fullCleanModsToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing DOOM Eternal AP mods...");

            RemoveOldAPModZip();
            SafeDeleteFile(Path.Combine(modsPath, "apmod_zip_name.txt"));

            DeleteOldVersionFiles();

            ShowInfo("DOOM Eternal AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete all mods?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing all mod files...");

        // Remove files from base directory
        SafeDeleteFile(Path.Combine(doometernalBasePath, "BlangParser.dll"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "DEternal_loadMods.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "DEternal_patchManifest.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.def"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.exe.config"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "idRehash.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "Newtonsoft.Json.dll"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "opusdec.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "opusenc.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "rs_data"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "zlib64.dll"));

        // Remove Mods directory completely
        SafeDeleteDirectory(modsPath);

        // Remove DOOM Eternal Archipelago directory from Documents
        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago");
        SafeDeleteDirectory(apInstallPath);

        // Remove version files
        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    bool HasOtherMods()
    {
        if (!Directory.Exists(modsPath))
            return false;

        string knownApZipName = "";
        string markerPath = Path.Combine(modsPath, "apmod_zip_name.txt");
        if (File.Exists(markerPath))
        {
            try { knownApZipName = File.ReadAllText(markerPath).Trim(); } catch { }
        }

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("DOOM Eternal APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name == "apmod_zip_name.txt")
                continue;

            if (!string.IsNullOrEmpty(knownApZipName) && name == knownApZipName)
                continue;

            // Compat rétro : ancien nom fixe
            if (name == "DoomEternalArchipelagoPreAlpha.zip")
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "DOOM Eternal Archipelago")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing DOOM Eternal Archipelago...");
            yield return InstallDOOMEternalAP();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(doometernalAP.url);

        if (launchDoomLauncherAfterSetupToggle != null && launchDoomLauncherAfterSetupToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal Archipelago Launcher...");
            LaunchDoomEternalArchipelagoLauncher();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator InstallDOOMEternalAP()
    {
        while (!configLoaded)
            yield return null;

        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago");
        string downloadedFile = Path.Combine(Application.persistentDataPath, "DoomEternalArchipelago.zip");

        UnityEngine.Debug.Log("Downloading DOOM Eternal AP from: " + doometernalAP.url);

        yield return DownloadFile(doometernalAP.url, downloadedFile);

        if (!File.Exists(downloadedFile))
        {
            UnityEngine.Debug.LogError("Download failed!");
            ShowInfo("ERROR: DOOM Eternal AP download failed!");
            yield break;
        }

        try
        {
            if (Directory.Exists(apInstallPath))
            {
                UnityEngine.Debug.Log("Deleting old installation: " + apInstallPath);
                Directory.Delete(apInstallPath, true);
            }

            Directory.CreateDirectory(apInstallPath);
            System.IO.Compression.ZipFile.ExtractToDirectory(downloadedFile, apInstallPath, true);

            UnityEngine.Debug.Log("DOOM Eternal AP installed to: " + apInstallPath);
            ShowInfo("DOOM Eternal AP installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install DOOM Eternal AP: " + e.Message);
            ShowInfo("ERROR: Failed to install DOOM Eternal AP\n" + e.Message);
            yield break;
        }

        try
        {
            if (File.Exists(downloadedFile))
                File.Delete(downloadedFile);
        }
        catch { }

        yield break;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago");

        UnityEngine.Debug.Log("Looking for APWorld file in: " + apInstallPath);

        // Find the doometernal.apworld file in subdirectories
        string apworldFilePath = FindApworldFile(apInstallPath);

        if (string.IsNullOrEmpty(apworldFilePath))
        {
            UnityEngine.Debug.LogError("doometernal.apworld file not found in AP installation!");
            ShowInfo("ERROR: doometernal.apworld file not found in the AP installation!");
            yield break;
        }

        UnityEngine.Debug.Log("Found doometernal.apworld at: " + apworldFilePath);

        // Target paths
        string[] targetPaths = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", "doometernal.apworld"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", "doometernal.apworld"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", "doometernal.apworld"),
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

        UnityEngine.Debug.Log("Target path: " + target);

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
            File.Copy(apworldFilePath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);

            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            yield break;
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

    IEnumerator APOnlyFlow()
    {
        ShowInfo("Installing DOOM Eternal Archipelago...");
        yield return InstallDOOMEternalAP();

        if (launchDoomLauncherAfterSetupToggle != null && launchDoomLauncherAfterSetupToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal Archipelago Launcher...");
            LaunchDoomEternalArchipelagoLauncher();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");

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
            remoteConfig = JsonUtility.FromJson<DOOMEternalConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDOOMEternalConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        doometernalPath = GetDOOMEternalPath();
    }

    void LaunchDoomEternalArchipelagoLauncher()
    {
        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago", "DoomEternalArchipelago");
        string launcherPath = Path.Combine(apInstallPath, "DoomEternalArchipelagoLauncher.exe");

        if (!File.Exists(launcherPath))
        {
            UnityEngine.Debug.LogWarning("DoomEternalArchipelagoLauncher.exe not found at: " + launcherPath);
            ShowInfo("ERROR: DoomEternalArchipelagoLauncher.exe not found!");
            return;
        }

        try
        {
            Process.Start(launcherPath);
            UnityEngine.Debug.Log("Launched DoomEternalArchipelagoLauncher: " + launcherPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch DoomEternalArchipelagoLauncher: " + e.Message);
            ShowInfo("ERROR: Failed to launch DoomEternalArchipelagoLauncher\n" + e.Message);
        }
    }

    void CloseDOOMEternal()
    {
        try
        {
            if (doometernalProcess != null && !doometernalProcess.HasExited)
            {
                doometernalProcess.Kill();
                doometernalProcess.Dispose();
                doometernalProcess = null;
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
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // Cherche le fichier doometernal.apworld dans les sous-dossiers
    string FindApworldFile(string root)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*.apworld", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).ToLower() == "doometernal.apworld")
                    return file;
            }
        }
        catch { }

        return "";
    }

    // Supprime l'ancien zip AP installé dans Mods, en se basant sur le marqueur
    void RemoveOldAPModZip()
    {
        if (!Directory.Exists(modsPath))
            return;

        string markerPath = Path.Combine(modsPath, "apmod_zip_name.txt");

        if (File.Exists(markerPath))
        {
            try
            {
                string oldName = File.ReadAllText(markerPath).Trim();
                if (!string.IsNullOrEmpty(oldName))
                    SafeDeleteFile(Path.Combine(modsPath, oldName));
            }
            catch { }
        }

        // Compat rétro : au cas où l'ancien nom fixe traîne encore d'une version précédente
        SafeDeleteFile(Path.Combine(modsPath, "DoomEternalArchipelagoPreAlpha.zip"));
    }

    void CreateVersionFile(string apUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/([^/]+)\.zip");

            string versionFileName = "DOOM Eternal APMod Version " + apVersion + ".txt";
            string content = "DOOM Eternal Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/DOOM-Eternal-Setup-Tool\n";
            content += "\n";
            content += "=== DOOM ETERNAL ARCHIPELAGO ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            // Create in mods directory
            Directory.CreateDirectory(modsPath);
            string modsVersionPath = Path.Combine(modsPath, versionFileName);
            File.WriteAllText(modsVersionPath, content);
            UnityEngine.Debug.Log("Version file created in mods: " + modsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DOOM Eternal APMod Version .+\.txt");

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

    string GetDOOMEternalPath()
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
}
