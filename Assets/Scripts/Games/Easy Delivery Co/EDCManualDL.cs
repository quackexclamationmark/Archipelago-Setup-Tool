using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class EDCManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("EASY DELIVERY CO FILES")]
    public FileDownloader.FileData easydeliverycoBepInEx;
    public FileDownloader.FileData easydeliverycoApworld; // easy_delivery_co.apworld
    public FileDownloader.FileData easydeliverycoAP;      // EasyDeliveryAP-0.1.2.zip -> folder EasyDeliveryAP
    public FileDownloader.FileData easydeliverycoAPI;     // EasyDeliveryAPI.zip.zip -> contains BepInEx/plugins/*.dll

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installApworldToggle;
    public Toggle installArchipelagoToggle; // maps to easydeliverycoAP
    public Toggle installAPI_Toggle;        // maps to easydeliverycoAPI

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

    private Process edcProcess;
    private string edcPath;
    private string pendingAction;
    private EDCConfig remoteConfig;
    private bool configLoaded = false;

    // Optional manifest tracking (kept minimal, used if you later want to save installed files)
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class EDCConfig
    {
        public string easydeliverycoBepInEx;
        public string easydeliverycoApworld;
        public string easydeliverycoAP;
        public string easydeliverycoAPI;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
    }

    void Start()
    {
        edcPath = GetEDCPath();
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyEDCConfig()
    {
        if (remoteConfig == null)
            return;

        easydeliverycoBepInEx.url = remoteConfig.easydeliverycoBepInEx;
        easydeliverycoBepInEx.fileName = "BepInEx.zip";

        easydeliverycoApworld.url = remoteConfig.easydeliverycoApworld;
        easydeliverycoApworld.fileName = "easy_delivery_co.apworld";

        easydeliverycoAP.url = remoteConfig.easydeliverycoAP;
        easydeliverycoAP.fileName = "EasyDeliveryAP-0.1.2.zip";

        easydeliverycoAPI.url = remoteConfig.easydeliverycoAPI;
        easydeliverycoAPI.fileName = "EasyDeliveryAPI.zip.zip";
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files for Easy Delivery Co?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert Easy Delivery Co changes?", "Revert");
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
        if (string.IsNullOrEmpty(edcPath))
        {
            ShowInfo("Easy Delivery Co path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        edcPath = GetEDCPath();

        if (string.IsNullOrEmpty(edcPath))
            return;

        string pluginsPath = Path.Combine(edcPath, "BepInEx", "plugins");

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

            ShowInfo("Removing AP / EDC mods...");

            SafeDeleteDirectory(Path.Combine(pluginsPath, "EasyDeliveryAP"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "EasyDeliveryAPI"));

            DeleteOldVersionFiles();

            ShowInfo("AP / EDC mods removed successfully!");
            return;
        }

        // Full clean
        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(edcPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(edcPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(edcPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(edcPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(edcPath, ".doorstop_version"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    IEnumerator InstallFlow()
    {
        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installApworldToggle != null && installApworldToggle.isOn)
        {
            ShowInfo("Installing .apworld file...");
            yield return InstallApworld();
        }

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing EasyDeliveryAP...");
            yield return InstallEasyDeliveryAP();
        }

        if (installAPI_Toggle != null && installAPI_Toggle.isOn)
        {
            ShowInfo("Installing EasyDeliveryAPI DLLs...");
            yield return InstallEasyDeliveryAPI();
        }

        CreateVersionFile(easydeliverycoBepInEx.url, easydeliverycoAP.url, easydeliverycoAPI.url);

        ShowInfo("Launching Easy Delivery Co...");
        LaunchEDC();

        yield return new WaitForSeconds(2f);

        CloseEDC();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchEDC();
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

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp_EDC");

        yield return downloader.DownloadAndExtract(easydeliverycoBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, edcPath);

        SafeDeleteDirectory(extractPath);
    }

    // Remplacé par une logique inspirée de COE33ManualDL:
    // - Téléchargement direct via UnityWebRequest -> fichier local
    // - Recherche du nom de fichier si absent
    // - Tentative de plusieurs chemins cibles pour Archipelago/custom_worlds
    IEnumerator InstallApworld()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + easydeliverycoApworld.url);

        if (string.IsNullOrEmpty(easydeliverycoApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = easydeliverycoApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            // Extraire le nom de fichier de l'URL
            fileName = easydeliverycoApworld.url.Substring(easydeliverycoApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + easydeliverycoApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        // Télécharge directement avec UnityWebRequest et DownloadHandlerFile
        yield return DownloadFile(easydeliverycoApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // Emplacements cibles possibles (mêmes choix que COE33)
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

        UnityEngine.Debug.Log("Target path: " + target);

        // Supprime l'ancien fichier s'il existe
        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                UnityEngine.Debug.Log("Deleted old apworld file");
            }
            catch { }
        }

        // Copie le fichier téléchargé vers le dossier cible
        try
        {
            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("APWorld file copied to: " + target);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }
    }

    IEnumerator InstallEasyDeliveryAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "EasyDeliveryAPTemp");
        yield return downloader.DownloadAndExtract(easydeliverycoAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(edcPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Chercher le dossier "EasyDeliveryAP" à l'intérieur de l'archive
        string[] apDirs = Directory.GetDirectories(extractPath, "EasyDeliveryAP", SearchOption.AllDirectories);

        if (apDirs.Length > 0)
        {
            string sourcePath = apDirs[0];
            string targetPath = Path.Combine(pluginsPath, "EasyDeliveryAP");

            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            CopyDirectory(sourcePath, targetPath);
            UnityEngine.Debug.Log("Copied EasyDeliveryAP to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("EasyDeliveryAP folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallEasyDeliveryAPI()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "EasyDeliveryAPITemp");
        yield return downloader.DownloadAndExtract(easydeliverycoAPI, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(edcPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Cherche les DLLs EasyDeliveryAPI.dll et Newtonsoft.Json.dll dans l'extraction
        string[] dllFiles = Directory.GetFiles(extractPath, "*.dll", SearchOption.AllDirectories);
        List<string> found = new List<string>();
        foreach (string f in dllFiles)
        {
            string name = Path.GetFileName(f);
            if (name.Equals("EasyDeliveryAPI.dll") || name.Equals("Newtonsoft.Json.dll"))
                found.Add(f);
        }

        if (found.Count > 0)
        {
            string apiTargetDir = Path.Combine(pluginsPath, "EasyDeliveryAPI");
            if (!Directory.Exists(apiTargetDir))
                Directory.CreateDirectory(apiTargetDir);

            foreach (string src in found)
            {
                string dest = Path.Combine(apiTargetDir, Path.GetFileName(src));
                try
                {
                    File.Copy(src, dest, true);
                    UnityEngine.Debug.Log("Copied " + Path.GetFileName(src) + " to " + apiTargetDir);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Failed to copy DLL: " + e.Message);
                }
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("EasyDeliveryAPI.dll or Newtonsoft.Json.dll not found in archive");
        }

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<EDCConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyEDCConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchEDC()
    {
        string exePath = Path.Combine(edcPath, "EasyDeliveryCo.exe");

        if (File.Exists(exePath))
        {
            try
            {
                edcProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Easy Delivery Co launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Easy Delivery Co:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseEDC()
    {
        try
        {
            if (edcProcess != null && !edcProcess.HasExited)
            {
                edcProcess.Kill();
                edcProcess.Dispose();
                edcProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseEDC();
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

    string GetEDCPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Easy Delivery Co"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Easy Delivery Co"),
            @"D:\Steam\steamapps\common\Easy Delivery Co",
            @"D:\SteamLibrary\steamapps\common\Easy Delivery Co",
            @"E:\Steam\steamapps\common\Easy Delivery Co",
            @"E:\SteamLibrary\steamapps\common\Easy Delivery Co",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Easy Delivery Co at: " + path);
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
                    string p = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Easy Delivery Co");
                    if (Directory.Exists(p))
                        return p;

                    p = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Easy Delivery Co");
                    if (Directory.Exists(p))
                        return p;

                    p = Path.Combine(drive.Name, "steamapps", "common", "Easy Delivery Co");
                    if (Directory.Exists(p))
                        return p;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Easy Delivery Co not found.");
        return "";
    }

    void CreateVersionFile(string bepinexUrl, string apUrl, string apiUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string apVersion = ExtractVersionFromUrl(apUrl);
            string apiVersion = ExtractVersionFromUrl(apiUrl);

            string versionFileName = "Easy Delivery Co Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== EASY DELIVERY API ===\n";
            content += "Downloaded from: " + apiUrl + "\n";
            content += "Version: " + apiVersion + "\n";
            content += "\n";
            content += "=== EASY DELIVERY AP ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(edcPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(edcPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Easy Delivery Co Version .+\.txt");

            if (!string.IsNullOrEmpty(edcPath))
            {
                string[] rootFiles = Directory.GetFiles(edcPath);
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

                string pluginsPath = Path.Combine(edcPath, "BepInEx", "plugins");
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
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

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

    // Helper to download a file to disk (uses DownloadHandlerFile like COE33ManualDL)
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
}