using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class NineSolsManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("NINE SOLS FILES")]
    public FileDownloader.FileData ninesolsapworld;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData archipelagoRandomizer;
    public FileDownloader.FileData teleportFromAnywhere;
    public FileDownloader.FileData configurationManager;
    public FileDownloader.FileData nineSolsAPI;

    [Header("FEATURE TOGGLES")]
    public Toggle installNineSolsapworldToggle;
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoRandomizerToggle;
    public Toggle installTeleportFromAnywhereToggle;
    public Toggle installConfigurationManagerToggle;
    public Toggle installNineSolsAPIToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process ninesolsProcess;
    private string ninesolsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private NineSolsConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class NineSolsConfig
    {
        public string ninesolsApworld;
        public string ninesolsBepInEx;
        public string ninesolsAP;
        public string ninesolsTeleport;
        public string ninesolsBepInExConfiguration;
        public string ninesolsAPI;
    }

    void Start()
    {
        ninesolsPath = GetNineSolsPath();
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

    void CleanupProcesses()
    {
        CloseNineSols();
    }

    void ApplyNineSolsConfig()
    {
        if (remoteConfig == null)
            return;

        ninesolsapworld.url = remoteConfig.ninesolsApworld;
        bepInEx.url = remoteConfig.ninesolsBepInEx;
        archipelagoRandomizer.url = remoteConfig.ninesolsAP;
        teleportFromAnywhere.url = remoteConfig.ninesolsTeleport;
        configurationManager.url = remoteConfig.ninesolsBepInExConfiguration;
        nineSolsAPI.url = remoteConfig.ninesolsAPI;
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
        if (string.IsNullOrEmpty(ninesolsPath))
        {
            ShowInfo("Nine Sols path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installNineSolsapworldToggle == null || installNineSolsapworldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool archipelago = installArchipelagoRandomizerToggle != null && installArchipelagoRandomizerToggle.isOn;
        bool teleport = installTeleportFromAnywhereToggle != null && installTeleportFromAnywhereToggle.isOn;
        bool configMgr = installConfigurationManagerToggle != null && installConfigurationManagerToggle.isOn;
        bool api = installNineSolsAPIToggle != null && installNineSolsAPIToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (archipelago ? 1 : 0) +
            (teleport ? 1 : 0) +
            (configMgr ? 1 : 0) +
            (api ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(ApWorldOnlyFlow());
            return;
        }

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        ninesolsPath = GetNineSolsPath();

        if (string.IsNullOrEmpty(ninesolsPath))
            return;

        string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");

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

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(pluginsPath, "ArchipelagoRandomizer.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "TeleportFromAnywhere.dll"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteFile(Path.Combine(pluginsPath, "ArchipelagoRandomizer.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "TeleportFromAnywhere.dll"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "ConfigurationManager"));
        SafeDeleteFile(Path.Combine(pluginsPath, "NineSolsAPI.dll"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(ninesolsPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(ninesolsPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(ninesolsPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(ninesolsPath, ".doorstop_version"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(ninesolsPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(ninesolsPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(ninesolsPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(ninesolsPath, ".doorstop_version"));

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("NineSols APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name != "ArchipelagoRandomizer.dll" &&
                name != "Archipelago.MultiClient.Net.dll" &&
                name != "TeleportFromAnywhere.dll" &&
                name != "NineSolsAPI.dll")
                return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "ConfigurationManager")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installNineSolsapworldToggle == null || installNineSolsapworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallNineSolsAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installNineSolsAPIToggle != null && installNineSolsAPIToggle.isOn)
        {
            ShowInfo("Installing Nine Sols API...");
            yield return InstallNineSolsAPI();
        }

        if (installConfigurationManagerToggle != null && installConfigurationManagerToggle.isOn)
        {
            ShowInfo("Installing Configuration Manager...");
            yield return InstallConfigurationManager();
        }

        if (installArchipelagoRandomizerToggle != null && installArchipelagoRandomizerToggle.isOn)
        {
            ShowInfo("Installing Archipelago Randomizer...");
            yield return InstallArchipelagoRandomizer();
        }

        if (installTeleportFromAnywhereToggle != null && installTeleportFromAnywhereToggle.isOn)
        {
            ShowInfo("Installing Teleport From Anywhere...");
            yield return InstallTeleportFromAnywhere();
        }

        CreateVersionFile(ninesolsapworld.url, bepInEx.url, archipelagoRandomizer.url, teleportFromAnywhere.url, configurationManager.url, nineSolsAPI.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Nine Sols...");
            LaunchNineSols();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }


    IEnumerator InstallNineSolsAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Nine Sols APWorld URL: " + ninesolsapworld.url);

        if (string.IsNullOrEmpty(ninesolsapworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = ninesolsapworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ninesolsapworld.url.Substring(ninesolsapworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + ninesolsapworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(ninesolsapworld.url, localPath);

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

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, ninesolsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallNineSolsAPI()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "NineSolsAPITemp");

        yield return downloader.DownloadAndExtract(nineSolsAPI, Application.persistentDataPath, extractPath);

        string dllPath = FindFile(extractPath, "NineSolsAPI.dll");

        string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        if (!string.IsNullOrEmpty(dllPath))
            File.Copy(dllPath, Path.Combine(pluginsPath, "NineSolsAPI.dll"), true);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallConfigurationManager()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ConfigurationManagerTemp");

        yield return downloader.DownloadAndExtract(configurationManager, Application.persistentDataPath, extractPath);

        string configManagerFolder = FindConfigurationManagerFolder(extractPath);

        if (string.IsNullOrEmpty(configManagerFolder))
        {
            UnityEngine.Debug.LogError("ConfigurationManager folder not found!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string targetPath = Path.Combine(pluginsPath, "ConfigurationManager");

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        MoveDirectory(configManagerFolder, targetPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallArchipelagoRandomizer()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ArchipelagoRandomizerTemp");

        yield return downloader.DownloadAndExtract(archipelagoRandomizer, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string archipelagoRandomizerPath = FindFile(extractPath, "ArchipelagoRandomizer.dll");
        string multiClientNetPath = FindFile(extractPath, "Archipelago.MultiClient.Net.dll");

        if (!string.IsNullOrEmpty(archipelagoRandomizerPath))
            File.Copy(archipelagoRandomizerPath, Path.Combine(pluginsPath, "ArchipelagoRandomizer.dll"), true);

        if (!string.IsNullOrEmpty(multiClientNetPath))
            File.Copy(multiClientNetPath, Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"), true);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallTeleportFromAnywhere()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "TeleportFromAnywhereTemp");

        yield return downloader.DownloadAndExtract(teleportFromAnywhere, Application.persistentDataPath, extractPath);

        string dllPath = FindFile(extractPath, "TeleportFromAnywhere.dll");

        string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        if (!string.IsNullOrEmpty(dllPath))
            File.Copy(dllPath, Path.Combine(pluginsPath, "TeleportFromAnywhere.dll"), true);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator ApWorldOnlyFlow()
    {
        ninesolsPath = GetNineSolsPath();

        if (string.IsNullOrEmpty(ninesolsPath))
            yield break;

        yield return InstallNineSolsAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchNineSols();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Nine Sols...");
            LaunchNineSols();
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
            remoteConfig = JsonUtility.FromJson<NineSolsConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyNineSolsConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchNineSols()
    {
        string exePath = Path.Combine(ninesolsPath, "NineSols.exe");

        if (File.Exists(exePath))
            ninesolsProcess = Process.Start(exePath);
    }

    void CloseNineSols()
    {
        try
        {
            if (ninesolsProcess != null && !ninesolsProcess.HasExited)
            {
                ninesolsProcess.Kill();
                ninesolsProcess.Dispose();
                ninesolsProcess = null;
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(source.Length + 1);
            string dest = Path.Combine(target, relativePath);

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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    string FindConfigurationManagerFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "ConfigurationManager")
                return dir;
        }

        return "";
    }

    void CreateVersionFile(string apworldUrl, string bepinexUrl, string archipelagoUrl, string teleportUrl, string configManagerUrl, string apiUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string archipelagoVersion = ExtractVersionFromUrl(archipelagoUrl, @"/releases/download/([^/]+)/");
            string teleportVersion = ExtractVersionFromUrl(teleportUrl, @"/download/([^/]+)/");
            string configManagerVersion = ExtractVersionFromUrl(configManagerUrl, @"/download/([^/]+)/");
            string apiVersion = ExtractVersionFromUrl(apiUrl, @"/download/([^/]+)/");

            string versionFileName = "NineSols APMod Version " + archipelagoVersion + ".txt";
            string content = "Nine Sols Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== NINE SOLS APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== NINE SOLS API ===\n";
            content += "Downloaded from: " + apiUrl + "\n";
            content += "Version: " + apiVersion + "\n";
            content += "\n";
            content += "=== CONFIGURATION MANAGER ===\n";
            content += "Downloaded from: " + configManagerUrl + "\n";
            content += "Version: " + configManagerVersion + "\n";
            content += "\n";
            content += "=== ARCHIPELAGO RANDOMIZER ===\n";
            content += "Downloaded from: " + archipelagoUrl + "\n";
            content += "Version: " + archipelagoVersion + "\n";
            content += "\n";
            content += "=== TELEPORT FROM ANYWHERE ===\n";
            content += "Downloaded from: " + teleportUrl + "\n";
            content += "Version: " + teleportVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"NineSols APMod Version .+\.txt");

            string pluginsPath = Path.Combine(ninesolsPath, "BepInEx", "plugins");
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

    string GetNineSolsPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Nine Sols"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Nine Sols"),
            @"D:\Steam\steamapps\common\Nine Sols",
            @"D:\SteamLibrary\steamapps\common\Nine Sols",
            @"D:\steamapps\common\Nine Sols",
            @"E:\Steam\steamapps\common\Nine Sols",
            @"E:\SteamLibrary\steamapps\common\Nine Sols",
            @"E:\steamapps\common\Nine Sols",
            @"E:\Program Files (x86)\steamapps\common\Nine Sols",
            @"E:\Program Files\steamapps\common\Nine Sols",
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
                    // Search Steam\steamapps
                    string ninesolsPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Nine Sols");
                    if (Directory.Exists(ninesolsPath))
                        return ninesolsPath;

                    // Search SteamLibrary\steamapps
                    ninesolsPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Nine Sols");
                    if (Directory.Exists(ninesolsPath))
                        return ninesolsPath;

                    // Search steamapps at root
                    ninesolsPath = Path.Combine(drive.Name, "steamapps", "common", "Nine Sols");
                    if (Directory.Exists(ninesolsPath))
                        return ninesolsPath;

                    // Search Program Files (x86)\steamapps
                    ninesolsPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Nine Sols");
                    if (Directory.Exists(ninesolsPath))
                        return ninesolsPath;

                    // Search Program Files\steamapps
                    ninesolsPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Nine Sols");
                    if (Directory.Exists(ninesolsPath))
                        return ninesolsPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
