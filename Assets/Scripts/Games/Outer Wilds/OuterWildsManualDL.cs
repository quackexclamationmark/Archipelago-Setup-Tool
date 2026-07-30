using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class OuterWildsManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("OUTER WILDS FILES")]
    public FileDownloader.FileData outerWildsapworld;
    public FileDownloader.FileData owml;
    public FileDownloader.FileData archipelagoRandomizer;
    public FileDownloader.FileData customShipLogModes;
    public FileDownloader.FileData enableMeditation;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Outer Wilds";

    [Header("FEATURE TOGGLES")]
    public Toggle installOuterWildsapworldToggle;
    public Toggle installOWMLToggle;
    public Toggle installArchipelagoRandomizerToggle;
    public Toggle installCustomShipLogModesToggle;
    public Toggle installEnableMeditationToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanOWMLToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process outerWildsProcess;
    private string outerWildsPath;
    private string owmlPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private OuterWildsConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class OuterWildsConfig
    {
        public string outerwildsApworld;
        public string outerwildsOWML;
        public string outerwildsAP;
        public string outerwildsCustomShipLog;
        public string outerwildsEnableMeditation;
        public string poptrackerDL;
        public string outerwildsPopTracker;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        outerWildsPath = GetOuterWildsPath();
        owmlPath = Path.Combine(outerWildsPath, "OWML");
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

        if (fullCleanOWMLToggle != null)
            fullCleanOWMLToggle.isOn = false;

        if (fullCleanOWMLToggle != null)
            fullCleanOWMLToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseOuterWilds();
    }

    void ApplyOuterWildsConfig()
    {
        if (remoteConfig == null)
            return;

        outerWildsapworld.url = remoteConfig.outerwildsApworld;
        owml.url = remoteConfig.outerwildsOWML;
        archipelagoRandomizer.url = remoteConfig.outerwildsAP;
        customShipLogModes.url = remoteConfig.outerwildsCustomShipLog;
        enableMeditation.url = remoteConfig.outerwildsEnableMeditation;
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
        outerWildsPath = GetOuterWildsPath();

        bool apworld = installOuterWildsapworldToggle == null || installOuterWildsapworldToggle.isOn;
        bool owml = installOWMLToggle != null && installOWMLToggle.isOn;
        bool ap = installArchipelagoRandomizerToggle != null && installArchipelagoRandomizerToggle.isOn;
        bool customShipLog = installCustomShipLogModesToggle != null && installCustomShipLogModesToggle.isOn;
        bool meditate = installEnableMeditationToggle != null && installEnableMeditationToggle.isOn;
        bool needsGamePath = ap || owml || customShipLog || meditate;

        if (needsGamePath && (string.IsNullOrEmpty(outerWildsPath) || !Directory.Exists(outerWildsPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (owml ? 1 : 0) +
            (ap ? 1 : 0) +
            (customShipLog ? 1 : 0) +
            (meditate ? 1 : 0);

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

        if (owml && count == 1)
        {
            StartCoroutine(OWMLOnlyFlow());
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
            ShowInfo("Launching Outer Wilds...");
            LaunchOuterWilds();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        outerWildsPath = GetOuterWildsPath();

        owmlPath = Path.Combine(outerWildsPath, "OWML");

        if (string.IsNullOrEmpty(outerWildsPath))
            return;

        string modsPath = Path.Combine(owmlPath, "Mods");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanOWMLToggle != null && fullCleanOWMLToggle.isOn;

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

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(modsPath, "Ixrec.ArchipelagoRandomizer"));
            SafeDeleteDirectory(Path.Combine(modsPath, "dgarro.CustomShipLogModes"));
            SafeDeleteDirectory(Path.Combine(modsPath, "xen.EnableMeditation"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete OWML?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteDirectory(Path.Combine(modsPath, "Ixrec.ArchipelagoRandomizer"));
        SafeDeleteDirectory(Path.Combine(modsPath, "dgarro.CustomShipLogModes"));
        SafeDeleteDirectory(Path.Combine(modsPath, "xen.EnableMeditation"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(modsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning OWML...");

            SafeDeleteDirectory(owmlPath);

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning OWML...");

            SafeDeleteDirectory(owmlPath);

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "Ixrec.ArchipelagoRandomizer" &&
                name != "dgarro.CustomShipLogModes" &&
                name != "xen.EnableMeditation")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installOuterWildsapworldToggle == null || installOuterWildsapworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installOWMLToggle != null && installOWMLToggle.isOn)
        {
            ShowInfo("Installing OWML...");
            yield return InstallOWML();
        }

        if (installArchipelagoRandomizerToggle != null && installArchipelagoRandomizerToggle.isOn)
        {
            ShowInfo("Installing Archipelago Randomizer...");
            yield return InstallArchipelagoRandomizer();
        }

        if (installCustomShipLogModesToggle != null && installCustomShipLogModesToggle.isOn)
        {
            ShowInfo("Installing Custom Ship Log Modes...");
            yield return InstallCustomShipLogModes();
        }

        if (installEnableMeditationToggle != null && installEnableMeditationToggle.isOn)
        {
            ShowInfo("Installing Enable Meditation...");
            yield return InstallEnableMeditation();
        }

        CreateVersionFile(outerWildsapworld.url, owml.url, archipelagoRandomizer.url, customShipLogModes.url, enableMeditation.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Outer Wilds...");
            LaunchOuterWilds();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Outer Wilds APWorld URL: " + outerWildsapworld.url);

        if (string.IsNullOrEmpty(outerWildsapworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = outerWildsapworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = outerWildsapworld.url.Substring(outerWildsapworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + outerWildsapworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(outerWildsapworld.url, localPath);

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

    IEnumerator InstallOWML()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "OWMLTemp");

        yield return downloader.DownloadAndExtract(owml, Application.persistentDataPath, extractPath);

        if (Directory.Exists(owmlPath))
            SafeDeleteDirectory(owmlPath);

        Directory.CreateDirectory(owmlPath);

        try
        {
            // Copie tout le contenu du ZIP directement dans owmlPath
            foreach (string file in Directory.GetFiles(extractPath))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(owmlPath, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (string dir in Directory.GetDirectories(extractPath))
            {
                string dirName = Path.GetFileName(dir);
                string targetDir = Path.Combine(owmlPath, dirName);
                PerformDirectoryCopy(dir, targetDir);
            }

            UnityEngine.Debug.Log("OWML installed to: " + owmlPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install OWML: " + e.Message);
            ShowInfo("ERROR: Failed to install OWML\n" + e.Message);
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

    IEnumerator InstallArchipelagoRandomizer()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ArchipelagoRandomizerTemp");

        yield return downloader.DownloadAndExtract(archipelagoRandomizer, Application.persistentDataPath, extractPath);

        string modFolder = FindModFolder(extractPath, "Ixrec.ArchipelagoRandomizer");

        if (string.IsNullOrEmpty(modFolder))
        {
            UnityEngine.Debug.LogError("Ixrec.ArchipelagoRandomizer folder not found in package!");
            ShowInfo("ERROR: Ixrec.ArchipelagoRandomizer folder not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string modsPath = Path.Combine(owmlPath, "Mods");
        Directory.CreateDirectory(modsPath);

        string targetPath = Path.Combine(modsPath, "Ixrec.ArchipelagoRandomizer");

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        try
        {
            MoveDirectory(modFolder, targetPath);
            UnityEngine.Debug.Log("Ixrec.ArchipelagoRandomizer installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Ixrec.ArchipelagoRandomizer: " + e.Message);
            ShowInfo("ERROR: Failed to install Ixrec.ArchipelagoRandomizer\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallCustomShipLogModes()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "CustomShipLogModesTemp");

        yield return downloader.DownloadAndExtract(customShipLogModes, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(owmlPath, "Mods");
        Directory.CreateDirectory(modsPath);

        string contentPath = Path.Combine(extractPath, "dgarro.CustomShipLogModes");

        if (!Directory.Exists(contentPath))
        {
            contentPath = extractPath;
        }

        string targetPath = Path.Combine(modsPath, "dgarro.CustomShipLogModes");

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        try
        {
            Directory.CreateDirectory(targetPath);
            MoveDirectory(contentPath, targetPath);
            UnityEngine.Debug.Log("dgarro.CustomShipLogModes installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install dgarro.CustomShipLogModes: " + e.Message);
            ShowInfo("ERROR: Failed to install dgarro.CustomShipLogModes\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallEnableMeditation()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "EnableMeditationTemp");

        yield return downloader.DownloadAndExtract(enableMeditation, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(owmlPath, "Mods");
        Directory.CreateDirectory(modsPath);

        string contentPath = Path.Combine(extractPath, "xen.EnableMeditation");

        if (!Directory.Exists(contentPath))
        {
            contentPath = extractPath;
        }

        string targetPath = Path.Combine(modsPath, "xen.EnableMeditation");

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        try
        {
            Directory.CreateDirectory(targetPath);
            MoveDirectory(contentPath, targetPath);
            UnityEngine.Debug.Log("xen.EnableMeditation installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install xen.EnableMeditation: " + e.Message);
            ShowInfo("ERROR: Failed to install xen.EnableMeditation\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator OWMLOnlyFlow()
    {
        ShowInfo("Installing OWML...");
        yield return InstallOWML();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Outer Wilds...");
            LaunchOuterWilds();
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
            remoteConfig = JsonUtility.FromJson<OuterWildsConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyOuterWildsConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        outerWildsPath = GetOuterWildsPath();
    }

    void LaunchOuterWilds()
    {
        string owmlLauncherPath = Path.Combine(owmlPath, "OWML.Launcher.exe");
        string outerWildsExePath = Path.Combine(outerWildsPath, "OuterWilds.exe");

        if (File.Exists(owmlLauncherPath))
        {
            UnityEngine.Debug.Log("Launching OWML.Launcher.exe");
            Process.Start(owmlLauncherPath);
            StartCoroutine(CheckAndLaunchOuterWilds(outerWildsExePath));
        }
        else if (File.Exists(outerWildsExePath))
        {
            UnityEngine.Debug.Log("OWML.Launcher.exe not found, launching OuterWilds.exe directly");
            outerWildsProcess = Process.Start(outerWildsExePath);
        }
    }

    IEnumerator CheckAndLaunchOuterWilds(string exePath)
    {
        yield return new WaitForSeconds(5f);

        Process[] processes = Process.GetProcessesByName("OuterWilds");

        if (processes.Length == 0)
        {
            if (File.Exists(exePath))
            {
                UnityEngine.Debug.Log("OuterWilds.exe not running, launching it now");
                outerWildsProcess = Process.Start(exePath);
            }
        }
        else
        {
            UnityEngine.Debug.Log("OuterWilds.exe already running");
            outerWildsProcess = processes[0];
        }
    }

    void CloseOuterWilds()
    {
        try
        {
            if (outerWildsProcess != null && !outerWildsProcess.HasExited)
            {
                outerWildsProcess.Kill();
                outerWildsProcess.Dispose();
                outerWildsProcess = null;
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

    string FindOWMLFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "OWML")
                return dir;
        }

        return "";
    }

    string FindModFolder(string root, string modName)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == modName)
                return dir;
        }

        return "";
    }

    void CreateVersionFile(string apworldUrl, string owmlUrl, string archipelagoUrl, string customShipLogUrl, string enableMeditationUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string owmlVersion = ExtractVersionFromUrl(owmlUrl, @"/releases/download/([^/]+)/");
            string archipelagoVersion = ExtractVersionFromUrl(archipelagoUrl, @"/releases/tag/([^/]+)");
            string customShipLogVersion = ExtractVersionFromUrl(customShipLogUrl, @"/releases/tag/([^/]+)");
            string enableMeditationVersion = ExtractVersionFromUrl(enableMeditationUrl, @"/releases/tag/([^/]+)");

            string versionFileName = "OuterWilds APMod Version " + archipelagoVersion + ".txt";
            string content = "Outer Wilds Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== OUTER WILDS APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== OWML ===\n";
            content += "Downloaded from: " + owmlUrl + "\n";
            content += "Version: " + owmlVersion + "\n";
            content += "\n";
            content += "=== ARCHIPELAGO RANDOMIZER ===\n";
            content += "Downloaded from: " + archipelagoUrl + "\n";
            content += "Version: " + archipelagoVersion + "\n";
            content += "\n";
            content += "=== CUSTOM SHIP LOG MODES ===\n";
            content += "Downloaded from: " + customShipLogUrl + "\n";
            content += "Version: " + customShipLogVersion + "\n";
            content += "\n";
            content += "=== ENABLE MEDITATION ===\n";
            content += "Downloaded from: " + enableMeditationUrl + "\n";
            content += "Version: " + enableMeditationVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string modsPath = Path.Combine(owmlPath, "Mods");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"OuterWilds APMod Version .+\.txt");

            string modsPath = Path.Combine(owmlPath, "Mods");
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

    string GetOuterWildsPath()
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