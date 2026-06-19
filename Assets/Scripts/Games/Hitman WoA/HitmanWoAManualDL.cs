using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class HitmanWoAManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData hitmanPeacock;    // Peacock-v8.8.1.zip
    public FileDownloader.FileData hitmanwoaApworld; // hitman_woa.apworld
    public FileDownloader.FileData hitmanwoaAP;     // archipelago.plugin.js file data

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installPeacockToggle;              // install Peacock folder
    public Toggle installAPWorldToggle;              // install apworld
    public Toggle installPluginToggle;               // install archipelago.plugin.js into Peacock\plugins
    public Toggle runPeacockServicesToggle;          // starts PeacockPatcher.exe then Start Server.cmd
    public Toggle launchGameAfterPeacockToggle;      // only enabled when runPeacockServicesToggle is on, launches game after 10s

    [Header("REVERT OPTIONS")]
    public Toggle removePluginOnlyToggle;            // remove only archipelago.plugin.js from Peacock\plugins
    public Toggle fullCleanToggle;                   // remove entire Peacock folder and related files

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private InstalledFilesManifest currentManifest;
    private bool isEpic = false;

    private HitmanConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
    }

    [System.Serializable]
    public class HitmanConfig
    {
        public string hitmanwoaAP;
        public string hitmanwoaPeacock;
        public string hitmanwoaApworld;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        gamePath = GetGamePath();

        // Start loading remote config
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (runPeacockServicesToggle != null)
        {
            runPeacockServicesToggle.isOn = false;
            runPeacockServicesToggle.onValueChanged.AddListener((v) => UpdateDependentToggles());
        }

        if (launchGameAfterPeacockToggle != null)
            launchGameAfterPeacockToggle.isOn = false;

        if (installPeacockToggle != null)
        {
            installPeacockToggle.isOn = true;
            installPeacockToggle.onValueChanged.AddListener((v) => UpdateDependentToggles());
        }

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installPluginToggle != null)
            installPluginToggle.isOn = true;

        // Revert toggles defaults
        if (removePluginOnlyToggle != null)
            removePluginOnlyToggle.isOn = true;

        if (fullCleanToggle != null)
        {
            fullCleanToggle.isOn = false;
            fullCleanToggle.onValueChanged.AddListener(OnFullCleanChanged);
        }

        UpdateDependentToggles();
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked()
    {
        SelectSteam();
    }

    void OnEpicButtonClicked()
    {
        SelectEpic();
    }

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
            platformStatus.text = $"Platform: {platform}\n{status}";
        }
    }

    // =========================================================

    void UpdateDependentToggles()
    {
        // launchGameAfterPeacock depends on runPeacockServicesToggle
        if (launchGameAfterPeacockToggle != null && runPeacockServicesToggle != null)
        {
            bool allowed = runPeacockServicesToggle.isOn;
            launchGameAfterPeacockToggle.interactable = allowed;
            if (!allowed) launchGameAfterPeacockToggle.isOn = false;
        }

        // plugin installation depends on Peacock being installed/selected
        if (installPluginToggle != null && installPeacockToggle != null)
        {
            bool allowed = installPeacockToggle.isOn;
            installPluginToggle.interactable = allowed;
            if (!allowed) installPluginToggle.isOn = false;
        }

        // removePluginOnly depends on Peacock presence too (UI convenience)
        if (removePluginOnlyToggle != null)
        {
            bool allowed = true;
            if (installPeacockToggle != null)
                allowed = installPeacockToggle.isOn || Directory.Exists(Path.Combine(gamePath ?? "", "Peacock-v8.8.1"));
            removePluginOnlyToggle.interactable = allowed && !(fullCleanToggle != null && fullCleanToggle.isOn);
            if (!removePluginOnlyToggle.interactable)
                removePluginOnlyToggle.isOn = false;
        }

        // fullClean disables removePluginOnly
        if (fullCleanToggle != null && removePluginOnlyToggle != null)
        {
            if (fullCleanToggle.isOn)
            {
                removePluginOnlyToggle.isOn = false;
                removePluginOnlyToggle.interactable = false;
            }
            else
            {
                // allow based on peacock presence
                if (installPeacockToggle != null)
                    removePluginOnlyToggle.interactable = installPeacockToggle.isOn;
            }
        }
    }

    void OnFullCleanChanged(bool value)
    {
        UpdateDependentToggles();
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

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to start setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove installed files?", "Revert");
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            ShowInfo("Game path not found. Please ensure HITMAN 3 is installed on " + platform + ".");
            return;
        }

        bool doPeacock = installPeacockToggle == null || installPeacockToggle.isOn;
        bool doAPWorld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool doPlugin = installPluginToggle == null || installPluginToggle.isOn;

        int count = (doPeacock ? 1 : 0) + (doAPWorld ? 1 : 0) + (doPlugin ? 1 : 0);

        // Individual flows when only one selected
        if (doPlugin && !doPeacock && !doAPWorld && count == 1)
        {
            StartCoroutine(PluginOnlyFlow());
            return;
        }

        if (doPeacock && !doPlugin && !doAPWorld && count == 1)
        {
            StartCoroutine(PeacockOnlyFlow());
            return;
        }

        if (doAPWorld && !doPeacock && !doPlugin && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Cannot revert.");
            return;
        }

        bool removePluginOnly = removePluginOnlyToggle != null && removePluginOnlyToggle.isOn;
        bool fullClean = fullCleanToggle != null && fullCleanToggle.isOn;

        if (!removePluginOnly && !fullClean)
        {
            ShowInfo("Please select a revert option (Remove plugin only or Full clean).");
            return;
        }

        if (removePluginOnly)
        {
            StartCoroutine(RemovePluginOnlyAsync());
            return;
        }

        if (fullClean)
        {
            StartCoroutine(FullCleanAsync());
            return;
        }
    }

    IEnumerator RemovePluginOnlyAsync()
    {
        string pluginFileName = (hitmanwoaAP != null && !string.IsNullOrEmpty(hitmanwoaAP.fileName)) ? hitmanwoaAP.fileName : "archipelago.plugin.js";
        string pluginPath = Path.Combine(gamePath, "Peacock-v8.8.1", "plugins", pluginFileName);

        ShowInfo("Removing Archipelago plugin...");
        yield return new WaitForSeconds(0.3f);

        if (File.Exists(pluginPath))
        {
            try
            {
                File.Delete(pluginPath);
                UnityEngine.Debug.Log("Deleted plugin: " + pluginPath);
                ShowInfo("Plugin removed successfully.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to delete plugin: " + e.Message);
                ShowInfo("Failed to remove plugin:\n" + e.Message);
            }
        }
        else
        {
            ShowInfo("Plugin not found at:\n" + pluginPath);
        }

        // Update manifest if present (remove plugin entries)
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_HitmanWoA.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                InstalledFilesManifest manifest = JsonUtility.FromJson<InstalledFilesManifest>(json);
                if (manifest != null && manifest.installedFiles != null)
                {
                    manifest.installedFiles = manifest.installedFiles.Where(p => !string.Equals(p, pluginPath, System.StringComparison.OrdinalIgnoreCase)).ToList();
                    File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                    UnityEngine.Debug.Log("Manifest updated to remove plugin entry.");
                }
            }
            catch { }
        }

        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator FullCleanAsync()
    {
        ShowInfo("Performing full clean of Peacock...");
        yield return new WaitForSeconds(0.3f);

        string peacockFolder = Path.Combine(gamePath, "Peacock-v8.8.1");

        // Stop any started processes
        CleanupProcesses();

        // Delete Peacock folder
        if (Directory.Exists(peacockFolder))
        {
            try
            {
                Directory.Delete(peacockFolder, true);
                UnityEngine.Debug.Log("Deleted Peacock folder: " + peacockFolder);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to delete Peacock folder: " + e.Message);
                ShowInfo("Failed to fully clean Peacock:\n" + e.Message);
                yield break;
            }
        }
        else
        {
            UnityEngine.Debug.Log("Peacock folder not present: " + peacockFolder);
        }

        // Remove manifest if it only contained Peacock/plugin entries (safe to remove manifest for full clean)
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_HitmanWoA.json");
        try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { }

        ShowInfo("Full clean completed.");
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator SetupWithTracking()
    {
        ShowInfo("Initializing installation...");
        yield return new WaitForSeconds(0.4f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Installing selected components...");

        if (installPeacockToggle == null || installPeacockToggle.isOn)
            yield return InstallPeacock();

        if (installPluginToggle == null || installPluginToggle.isOn)
            yield return InstallPlugin();

        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        SaveInstalledFilesManifest(currentManifest);

        // If user wants Peacock services start them
        if (runPeacockServicesToggle != null && runPeacockServicesToggle.isOn)
        {
            yield return StartPeacockServicesCoroutine();
        }

        // If user wants to launch the game after Peacock, wait 10s then launch
        if (launchGameAfterPeacockToggle != null && launchGameAfterPeacockToggle.isOn)
        {
            ShowInfo("Waiting 10 seconds before launching the game...");
            yield return new WaitForSeconds(10f);
            LaunchGame();
        }

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator PeacockOnlyFlow()
    {
        gamePath = GetGamePath();
        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallPeacock();

        // only install plugin if requested
        if (installPluginToggle != null && installPluginToggle.isOn)
            yield return InstallPlugin();

        SaveInstalledFilesManifest(currentManifest);

        if (runPeacockServicesToggle == null || runPeacockServicesToggle.isOn)
            yield return StartPeacockServicesCoroutine();

        if (launchGameAfterPeacockToggle != null && launchGameAfterPeacockToggle.isOn)
        {
            yield return new WaitForSeconds(10f);
            LaunchGame();
        }
    }

    IEnumerator PluginOnlyFlow()
    {
        gamePath = GetGamePath();
        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = currentManifest ?? new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallPlugin();

        SaveInstalledFilesManifest(currentManifest);
    }

    IEnumerator APWorldOnlyFlow()
    {
        gamePath = GetGamePath();
        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = currentManifest ?? new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallAPWorld();

        SaveInstalledFilesManifest(currentManifest);
    }

    IEnumerator InstallPeacock()
    {
        // wait for config to be loaded so hitmanPeacock.url is available
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "PeacockTemp");

        yield return downloader.DownloadAndExtract(hitmanPeacock, Application.persistentDataPath, extractPath);

        string sourceFolder = Path.Combine(extractPath, "Peacock-v8.8.1");

        if (!Directory.Exists(sourceFolder))
        {
            ShowInfo("ERROR: Peacock folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string targetFolder = Path.Combine(gamePath, "Peacock-v8.8.1");

        // Move the Peacock folder to game root (copy/move files)
        MoveDirectoryAndTrack(sourceFolder, targetFolder);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Peacock installed successfully!");
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator InstallPlugin()
    {
        while (!configLoaded) yield return null;

        if (hitmanwoaAP == null || string.IsNullOrEmpty(hitmanwoaAP.url))
        {
            ShowInfo("Plugin data not configured.");
            UnityEngine.Debug.LogError("InstallPlugin: hitmanwoaAP or URL is null/empty.");
            yield break;
        }

        string peacockFolder = Path.Combine(gamePath, "Peacock-v8.8.1");
        string pluginsFolder = Path.Combine(peacockFolder, "plugins");

        // Ensure parent and plugins folder exist
        try
        {
            if (!Directory.Exists(peacockFolder))
            {
                Directory.CreateDirectory(peacockFolder);
                UnityEngine.Debug.Log("InstallPlugin: created peacock folder: " + peacockFolder);
            }
            if (!Directory.Exists(pluginsFolder))
            {
                Directory.CreateDirectory(pluginsFolder);
                UnityEngine.Debug.Log("InstallPlugin: created plugins folder: " + pluginsFolder);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("InstallPlugin: could not create folders: " + e.Message);
            ShowInfo("ERROR: impossible de créer les dossiers Peacock/plugins:\n" + e.Message);
            yield break;
        }

        // Download directly as a single file
        string fileName = "archipelago.plugin.js"; // Force le nom du fichier

        string tempFile = Path.Combine(Application.persistentDataPath, "plugin_tmp_file");
        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

        ShowInfo("Downloading plugin...");
        yield return DownloadFile(hitmanwoaAP.url, tempFile);

        if (File.Exists(tempFile))
        {
            try
            {
                string dest = Path.Combine(pluginsFolder, fileName);
                File.Copy(tempFile, dest, true);
                File.SetAttributes(dest, FileAttributes.Normal);
                if (currentManifest != null) currentManifest.installedFiles.Add(dest);
                UnityEngine.Debug.Log("InstallPlugin: plugin copied -> " + dest);
                ShowInfo("Plugin installé !");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("InstallPlugin: failed to copy downloaded plugin: " + e.Message);
                ShowInfo("ERROR: impossible de copier le plugin:\n" + e.Message);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        else
        {
            UnityEngine.Debug.LogError("InstallPlugin: no file found after download.");
            ShowInfo("ERROR: plugin introuvable après téléchargement.");
        }

        yield return null;
    }

    IEnumerator InstallAPWorld()
    {
        // wait for config to be loaded so hitmanwoaApworld.url is available
        while (!configLoaded)
            yield return null;

        if (hitmanwoaApworld == null || string.IsNullOrEmpty(hitmanwoaApworld.url))
        {
            ShowInfo("APWorld data not configured.");
            yield break;
        }

        // Extract filename
        string fileName = hitmanwoaApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = hitmanwoaApworld.url.Substring(hitmanwoaApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        yield return DownloadFile(hitmanwoaApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("APWorld download failed: file not found at " + localPath);
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
        foreach (string p in targetPaths)
        {
            try
            {
                string dir = Path.GetDirectoryName(p);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                target = p;
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(p) + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            yield break;
        }

        try
        {
            if (File.Exists(target))
                File.Delete(target);

            File.Copy(localPath, target, true);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }

        yield return null;
    }

    IEnumerator StartPeacockServicesCoroutine()
    {
        ShowInfo("Starting Peacock services...");
        yield return new WaitForSeconds(0.3f);

        string peacockFolder = Path.Combine(gamePath, "Peacock-v8.8.1");
        if (!Directory.Exists(peacockFolder))
        {
            ShowInfo("Peacock folder not found. Cannot start services.");
            yield break;
        }

        string patcherExe = Path.Combine(peacockFolder, "PeacockPatcher.exe");
        string serverCmd = Path.Combine(peacockFolder, "Start Server.cmd");

        // Start PeacockPatcher.exe if present
        if (File.Exists(patcherExe))
        {
            try
            {
                UnityEngine.Debug.Log("Starting PeacockPatcher: " + patcherExe);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = patcherExe;
                psi.WorkingDirectory = peacockFolder;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to start PeacockPatcher: " + e);
                ShowInfo("Error starting PeacockPatcher:\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("PeacockPatcher.exe not found: " + patcherExe);
        }

        // Then start Start Server.cmd with admin privileges
        if (File.Exists(serverCmd))
        {
            try
            {
                UnityEngine.Debug.Log("Starting Start Server.cmd in: " + peacockFolder);
                ProcessStartInfo psi2 = new ProcessStartInfo();
                psi2.FileName = serverCmd;
                psi2.WorkingDirectory = peacockFolder;
                psi2.UseShellExecute = true;
                psi2.Verb = "runas";
                Process.Start(psi2);
                UnityEngine.Debug.Log("Start Server.cmd launched!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to start Start Server.cmd: " + e);
                ShowInfo("Error starting server cmd:\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Start Server.cmd not found: " + serverCmd);
        }

        yield return new WaitForSeconds(0.5f);
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

    void CleanupProcesses()
    {
        CloseGame();
    }

    void LaunchGame()
    {
        string currentGamePath = GetGamePath();
        UnityEngine.Debug.Log("LaunchGame called. GamePath: " + currentGamePath);

        if (string.IsNullOrEmpty(currentGamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string[] possiblePaths = new string[]
        {
            Path.Combine(currentGamePath, "HITMAN 3", "Launcher.exe"),
            Path.Combine(currentGamePath, "Launcher.exe"),
            Path.Combine(currentGamePath, "Binaries", "Win64", "HITMAN3.exe"),
            Path.Combine(currentGamePath, "HITMAN3", "Launcher.exe"),
        };

        string exePath = "";
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                exePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            ShowInfo("Game executable not found. Checked common locations.");
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        try
        {
            UnityEngine.Debug.Log("Starting game process: " + exePath);
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            psi.WorkingDirectory = Path.GetDirectoryName(exePath);
            psi.UseShellExecute = true;
            psi.Verb = "runas"; // Admin
            gameProcess = Process.Start(psi);
            UnityEngine.Debug.Log("Game launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseGame()
    {
        try
        {
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
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

    void MoveDirectoryAndTrack(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);
        }

        try { if (Directory.Exists(source)) Directory.Delete(source, true); } catch { }
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_HitmanWoA.json");
        string json = JsonUtility.ToJson(manifest, true);

        try
        {
            File.WriteAllText(manifestPath, json);
            UnityEngine.Debug.Log("Installation manifest saved: " + manifestPath);
            UnityEngine.Debug.Log("Tracked " + manifest.installedFiles.Count + " files for future revert");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save manifest: " + e.Message);
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
            remoteConfig = JsonUtility.FromJson<HitmanConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyHitmanConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void ApplyHitmanConfig()
    {
        if (remoteConfig == null)
            return;

        if (hitmanPeacock != null)
        {
            hitmanPeacock.url = remoteConfig.hitmanwoaPeacock;
            if (string.IsNullOrEmpty(hitmanPeacock.fileName))
                hitmanPeacock.fileName = "Peacock-v8.8.1.zip";
        }

        if (hitmanwoaApworld != null)
        {
            hitmanwoaApworld.url = remoteConfig.hitmanwoaApworld;
            if (string.IsNullOrEmpty(hitmanwoaApworld.fileName))
                hitmanwoaApworld.fileName = "hitman_woa.apworld";
        }

        if (hitmanwoaAP != null)
        {
            hitmanwoaAP.url = remoteConfig.hitmanwoaAP;
            if (string.IsNullOrEmpty(hitmanwoaAP.fileName))
                hitmanwoaAP.fileName = "archipelago.plugin.js";
        }
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetGamePath()
    {
        if (isEpic)
            return GetGamePathEpic();
        else
            return GetGamePathSteam();
    }

    string GetGamePathSteam()
    {
        // Try likely locations first
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "HITMAN 3"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "HITMAN 3"),
            @"D:\Steam\steamapps\common\HITMAN 3",
            @"D:\SteamLibrary\steamapps\common\HITMAN 3",
            @"D:\steamapps\common\HITMAN 3",
            @"E:\Steam\steamapps\common\HITMAN 3",
            @"E:\SteamLibrary\steamapps\common\HITMAN 3",
            @"E:\steamapps\common\HITMAN 3",
            @"E:\Program Files (x86)\steamapps\common\HITMAN 3",
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
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "HITMAN 3");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "HITMAN 3");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    gamePath = Path.Combine(drive.Name, "steamapps", "common", "HITMAN 3");
                    if (Directory.Exists(gamePath))
                        return gamePath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    string GetGamePathEpic()
    {
        string[] quickPaths = new string[]
        {
        @"C:\Program Files\Epic Games\HITMAN3",
        @"D:\Epic Games\HITMAN3",
        @"E:\Epic Games\HITMAN3",
        @"C:\Games\Epic\HITMAN3",
        @"D:\Games\Epic\HITMAN3",
        @"E:\Games\Epic\HITMAN3",
        @"C:\Epic\HITMAN3",
        @"D:\Epic\HITMAN3",
        @"E:\Epic\HITMAN3",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found HITMAN 3 (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Scan all drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "HITMAN3");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found HITMAN 3 (Epic) at: " + epicPath);
                        return epicPath;
                    }

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "HITMAN3");
                    if (Directory.Exists(epicPath))
                    {
                        UnityEngine.Debug.Log("Found HITMAN 3 (Epic) at: " + epicPath);
                        return epicPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("HITMAN 3 (Epic) not found.");
        return "";
    }
}
