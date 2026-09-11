using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
/*using UnityEngine.LightTransport;*/
using UnityEngine.UI;

public class HitmanWoAManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData hitmanPeacock;
    public FileDownloader.FileData hitmanwoaApworld;
    public FileDownloader.FileData hitmanwoaAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "HITMAN 3";
    public string epicGameFolderName = "HITMAN3";

    [Header("FEATURE TOGGLES")]
    public Toggle installPeacockToggle;
    public Toggle installAPWorldToggle;
    public Toggle installPluginToggle;
    public Toggle runPeacockServicesToggle;
    public Toggle launchGameAfterPeacockToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removePluginOnlyToggle;
    public Toggle fullCleanToggle;

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
    private bool lastApWorldInstallSuccess = false;

    private string currentPeacockFolderName = "";

    private HitmanConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public string peacockFolderName = "";
        public List<string> installedFiles = new List<string>();
    }

    [System.Serializable]
    public class HitmanConfig
    {
        public string hitmanwoaAP;
        public string hitmanwoaPeacock;
        public string hitmanwoaApworld;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
        public string[] apSearchPaths;
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
                allowed = installPeacockToggle.isOn || !string.IsNullOrEmpty(GetPeacockFolderPath());
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
        gamePath = GetGamePath();

        bool peacock = installPeacockToggle == null || installPeacockToggle.isOn;
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool plugin = installPluginToggle == null || installPluginToggle.isOn;
        bool needsGamePath = plugin || peacock || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(gamePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Game not found on " + platform + ". Please check installation.");
            return;
        }

        int count = (peacock ? 1 : 0) + (apworld ? 1 : 0) + (plugin ? 1 : 0);

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

        if (peacock && count == 1)
        {
            StartCoroutine(PeacockOnlyFlow());
            return;
        }

        if (plugin && count == 1)
        {
            StartCoroutine(PluginOnlyFlow());
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (launchGameAfterPeacockToggle == null || launchGameAfterPeacockToggle.isOn)
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

        string peacockFolder = GetPeacockFolderPath();
        if (string.IsNullOrEmpty(peacockFolder))
        {
            ShowInfo("Peacock not found. Cannot remove the plugin.");
            yield break;
        }

        string pluginPath = Path.Combine(peacockFolder, "plugins", pluginFileName);

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

        string peacockFolder = GetPeacockFolderPath();

        CleanupProcesses();

        if (!string.IsNullOrEmpty(peacockFolder) && Directory.Exists(peacockFolder))
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
            UnityEngine.Debug.Log("Peacock folder not present or could not be located.");
        }

        currentPeacockFolderName = "";

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

    IEnumerator InstallPeacock()
    {
        // wait for config to be loaded so hitmanPeacock.url is available
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "PeacockTemp");

        yield return downloader.DownloadAndExtract(hitmanPeacock, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Peacock extraction folder not found!");
            yield break;
        }

        // On ne se base plus sur un nom de fichier/dossier fixe. On prend le contenu
        // réel de l'archive : si elle contient un unique sous-dossier englobant
        // (et aucun fichier à la racine), ce sous-dossier est la vraie racine du
        // contenu Peacock, quel que soit son nom. Sinon on prend directement
        // le contenu extrait tel quel.
        string sourceFolder = extractPath;

        string[] topLevelFiles = Directory.GetFiles(extractPath);
        string[] topLevelDirs = Directory.GetDirectories(extractPath);

        if (topLevelFiles.Length == 0 && topLevelDirs.Length == 1)
        {
            sourceFolder = topLevelDirs[0];
        }

        // Nom final utilisé pour le dossier Peacock côté jeu : celui du dossier
        // source détecté si disponible, sinon un nom générique par défaut.
        string folderName = (sourceFolder != extractPath) ? Path.GetFileName(sourceFolder) : "Peacock";
        if (string.IsNullOrEmpty(folderName))
            folderName = "Peacock";

        currentPeacockFolderName = folderName;

        string targetFolder = Path.Combine(gamePath, folderName);

        // Move the Peacock folder content to game root (copy/move files)
        MoveDirectoryAndTrack(sourceFolder, targetFolder);

        SafeDeleteDirectory(extractPath);

        if (currentManifest != null)
            currentManifest.peacockFolderName = folderName;

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

        string peacockFolder = GetPeacockFolderPath();

        // Si aucun dossier Peacock n'a été trouvé/installé, on en crée un avec un nom générique.
        if (string.IsNullOrEmpty(peacockFolder))
        {
            currentPeacockFolderName = "Peacock";
            peacockFolder = Path.Combine(gamePath, currentPeacockFolderName);
        }

        string pluginsFolder = Path.Combine(peacockFolder, "plugins");

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
            ShowInfo("ERROR: could not create Peacock/plugins folders:\n" + e.Message);
            yield break;
        }

        if (currentManifest != null)
            currentManifest.peacockFolderName = currentPeacockFolderName;

        string fileName = "archipelago.plugin.js";

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
                ShowInfo("Plugin installed successfully!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("InstallPlugin: failed to copy downloaded plugin: " + e.Message);
                ShowInfo("ERROR: could not copy the plugin:\n" + e.Message);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        else
        {
            UnityEngine.Debug.LogError("InstallPlugin: no file found after download.");
            ShowInfo("ERROR: plugin not found after download.");
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + hitmanwoaApworld.url);

        if (string.IsNullOrEmpty(hitmanwoaApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = hitmanwoaApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = hitmanwoaApworld.url.Substring(hitmanwoaApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + hitmanwoaApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(hitmanwoaApworld.url, localPath);

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
                UnityEngine.Debug.Log("Cleaned up temporary APWorld file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
        }
    }

    IEnumerator StartPeacockServicesCoroutine()
    {
        ShowInfo("Starting Peacock services...");
        yield return new WaitForSeconds(0.3f);

        string peacockFolder = GetPeacockFolderPath();
        if (string.IsNullOrEmpty(peacockFolder))
        {
            ShowInfo("Peacock folder not found. Cannot start services.");
            yield break;
        }

        string patcherExe = Path.Combine(peacockFolder, "PeacockPatcher.exe");
        string serverCmd = Path.Combine(peacockFolder, "Start Server.cmd");

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
        // S'assure que le nom du dossier Peacock résolu dynamiquement est bien
        // persisté dans le manifest, même si ExecuteRevert/InstallPlugin/InstallPeacock
        // ont mis à jour currentPeacockFolderName sans repasser par ce point.
        if (manifest != null && string.IsNullOrEmpty(manifest.peacockFolderName) && !string.IsNullOrEmpty(currentPeacockFolderName))
            manifest.peacockFolderName = currentPeacockFolderName;

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
            UnityEngine.Debug.Log("Hitman Remote config loaded successfully");
            ApplyHitmanConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        gamePath = GetGamePath();
        UpdatePlatformStatus();
    }

    void ApplyHitmanConfig()
    {
        if (remoteConfig == null)
            return;

        if (hitmanPeacock != null)
        {
            hitmanPeacock.url = remoteConfig.hitmanwoaPeacock;
            if (string.IsNullOrEmpty(hitmanPeacock.fileName))
                hitmanPeacock.fileName = "Peacock";
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
    // PEACOCK FOLDER RESOLUTION (dynamique, aucun nom hardcodé)
    // =========================================================

    private string GetPeacockFolderPath()
    {
        if (string.IsNullOrEmpty(gamePath))
            return "";

        // 1. Nom déjà connu en mémoire
        if (!string.IsNullOrEmpty(currentPeacockFolderName))
        {
            string p = Path.Combine(gamePath, currentPeacockFolderName);
            if (Directory.Exists(p))
                return p;
        }

        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest_HitmanWoA.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                InstalledFilesManifest manifest = JsonUtility.FromJson<InstalledFilesManifest>(json);
                if (manifest != null && !string.IsNullOrEmpty(manifest.peacockFolderName))
                {
                    string p = Path.Combine(gamePath, manifest.peacockFolderName);
                    if (Directory.Exists(p))
                    {
                        currentPeacockFolderName = manifest.peacockFolderName;
                        return p;
                    }
                }
            }
            catch { }
        }

        // 3. Fallback : scan du dossier du jeu à la recherche d'un dossier "Peacock-like"
        try
        {
            foreach (string dir in Directory.GetDirectories(gamePath))
            {
                if (File.Exists(Path.Combine(dir, "PeacockPatcher.exe")) ||
                    File.Exists(Path.Combine(dir, "Start Server.cmd")))
                {
                    currentPeacockFolderName = Path.GetFileName(dir);
                    return dir;
                }
            }
        }
        catch { }

        return "";
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

    string GetGamePathEpic()
    {
        string[] quickPaths = new string[]
       {
            @"C:\Program Files\Epic Games\HITMAN3",
            @"C:\Games\Epic\HITMAN3",
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
                        if (content.Contains("HITMAN3") || content.Contains("HITMAN3"))
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