using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class OpenRCT2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData openrct2Apworld;
    public FileDownloader.FileData openrct2DL;
    public FileDownloader.FileData openrct2Randomizer;
    public FileDownloader.FileData openrct2APVanillaAsync;
    public FileDownloader.FileData openrct2APVanillaSync;
    public FileDownloader.FileData openrct2APExpAsync;
    public FileDownloader.FileData openrct2APExpSync;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installOpenRCT2Toggle;
    public Toggle installRandomizerToggle;
    public Toggle installVanillaAsyncToggle;
    public Toggle installVanillaSyncToggle;
    public Toggle installExpAsyncToggle;
    public Toggle installExpSyncToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastInstallSuccess = false;

    private static readonly string[] scenarioFilesToDelete = new string[]
    {
        "Archipelago.Madness.Vanilla.Asynchronus.park",
        "Archipelago.Madness.Vanilla.Synchronous.park",
        "Archipelago.Madness.Expansions.Asynchronus.park",
        "Archipelago.Madness.Expansions.Synchronus.park"
    };

    private const string randomizerFileToDelete = "rctrando.js";

    [System.Serializable]
    public class GameConfig
    {
        public string openrct2Apworld;
        public string openrct2DL;
        public string openrct2Randomizer;
        public string openrct2APVanillaAsync;
        public string openrct2APVanillaSync;
        public string openrct2APExpAsync;
        public string openrct2APExpSync;
        public string[] apSearchPaths;
    }

    void Start()
    {
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

        // Défauts des toggles
        SetToggle(installApworldToggle, true);
        SetToggle(installRandomizerToggle, true);
        SetToggle(installOpenRCT2Toggle, false);
        SetToggle(installVanillaAsyncToggle, false);
        SetToggle(installVanillaSyncToggle, false);
        SetToggle(installExpAsyncToggle, false);
        SetToggle(installExpSyncToggle, false);
    }

    void SetToggle(Toggle toggle, bool value)
    {
        if (toggle != null)
            toggle.isOn = value;
    }

    bool IsOn(Toggle toggle)
    {
        return toggle != null && toggle.isOn;
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        openrct2Apworld.url = remoteConfig.openrct2Apworld;
        openrct2DL.url = remoteConfig.openrct2DL;
        openrct2Randomizer.url = remoteConfig.openrct2Randomizer;
        openrct2APVanillaAsync.url = remoteConfig.openrct2APVanillaAsync;
        openrct2APVanillaSync.url = remoteConfig.openrct2APVanillaSync;
        openrct2APExpAsync.url = remoteConfig.openrct2APExpAsync;
        openrct2APExpSync.url = remoteConfig.openrct2APExpSync;
    }

    // ---------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------

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

        pendingAction = "";
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
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

    // ---------------------------------------------------------------
    // PATHS
    // ---------------------------------------------------------------

    string GetDownloadsPath()
    {
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    string GetOpenRCT2DocumentsPath()
    {
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "OpenRCT2");
    }

    string GetPluginPath()
    {
        return Path.Combine(GetOpenRCT2DocumentsPath(), "plugin");
    }

    string GetScenarioPath()
    {
        return Path.Combine(GetOpenRCT2DocumentsPath(), "scenario");
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
                                Debug.Log("Found Archipelago custom_worlds (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        Debug.LogWarning("Archipelago custom_worlds directory not found.");
        return "";
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        bool anySelected =
            IsOn(installApworldToggle) ||
            IsOn(installOpenRCT2Toggle) ||
            IsOn(installRandomizerToggle) ||
            IsOn(installVanillaAsyncToggle) ||
            IsOn(installVanillaSyncToggle) ||
            IsOn(installExpAsyncToggle) ||
            IsOn(installExpSyncToggle);

        if (!anySelected)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        // APWorld
        if (IsOn(installApworldToggle))
        {
            yield return InstallAPWorld();
            if (!lastInstallSuccess)
                yield break;
        }

        // OpenRCT2 (.exe) -> Downloads
        if (IsOn(installOpenRCT2Toggle))
        {
            yield return InstallSimpleFile(openrct2DL, "OpenRCT2", GetDownloadsPath());
            if (!lastInstallSuccess)
                yield break;
        }

        // Randomizer -> Documents\OpenRCT2\plugin
        if (IsOn(installRandomizerToggle))
        {
            yield return InstallSimpleFile(openrct2Randomizer, "OpenRCT2 Randomizer", GetPluginPath());
            if (!lastInstallSuccess)
                yield break;
        }

        // Scenarios -> Documents\OpenRCT2\scenario
        string scenarioPath = GetScenarioPath();

        if (IsOn(installVanillaAsyncToggle))
        {
            yield return InstallSimpleFile(openrct2APVanillaAsync, "Vanilla Async scenario", scenarioPath);
            if (!lastInstallSuccess)
                yield break;
        }

        if (IsOn(installVanillaSyncToggle))
        {
            yield return InstallSimpleFile(openrct2APVanillaSync, "Vanilla Sync scenario", scenarioPath);
            if (!lastInstallSuccess)
                yield break;
        }

        if (IsOn(installExpAsyncToggle))
        {
            yield return InstallSimpleFile(openrct2APExpAsync, "Expansions Async scenario", scenarioPath);
            if (!lastInstallSuccess)
                yield break;
        }

        if (IsOn(installExpSyncToggle))
        {
            yield return InstallSimpleFile(openrct2APExpSync, "Expansions Sync scenario", scenarioPath);
            if (!lastInstallSuccess)
                yield break;
        }

        ShowInfo("Installation complete!");
    }

    string ResolveFileName(FileDownloader.FileData file)
    {
        string fileName = file.fileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = file.url.Substring(file.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            Debug.Log("Extracted filename from URL: " + fileName);
        }

        return fileName;
    }

    IEnumerator InstallSimpleFile(FileDownloader.FileData file, string label, string destDir)
    {
        lastInstallSuccess = false;

        if (string.IsNullOrEmpty(file.url))
        {
            ShowInfo("ERROR: " + label + " URL is empty!");
            Debug.LogError(label + " URL not set!");
            yield break;
        }

        ShowInfo("Installing " + label + "...");

        string fileName = ResolveFileName(file);
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        Debug.Log("Downloading " + label + " from: " + file.url);
        yield return DownloadFile(file.url, localPath);

        if (!File.Exists(localPath))
        {
            Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: " + label + " download failed!");
            yield break;
        }

        try
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            string target = Path.Combine(destDir, fileName);
            File.Copy(localPath, target, true);
            Debug.Log(label + " copied to: " + target);

            lastInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to install " + label + ": " + e.Message);
            ShowInfo("ERROR: Failed to install " + label + "\n" + e.Message);
        }

        DeleteTempFile(localPath);
    }

    IEnumerator InstallAPWorld()
    {
        lastInstallSuccess = false;

        if (string.IsNullOrEmpty(openrct2Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            Debug.LogError("APWorld URL not set!");
            yield break;
        }

        ShowInfo("Installing APWorld...");

        string fileName = ResolveFileName(openrct2Apworld);
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        Debug.Log("Downloading APWorld from: " + openrct2Apworld.url);
        yield return DownloadFile(openrct2Apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                Debug.Log("Deleted old apworld file");
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
            Debug.Log("APWorld file copied to: " + target);
            lastInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }

        DeleteTempFile(localPath);
    }

    // ---------------------------------------------------------------
    // REVERT
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting OpenRCT2 Archipelago...");
        yield return new WaitForSeconds(0.5f);

        int deleted = 0;
        List<string> errors = new List<string>();

        // Scenarios
        string scenarioPath = GetScenarioPath();
        foreach (string name in scenarioFilesToDelete)
        {
            TryDeleteFile(Path.Combine(scenarioPath, name), errors, ref deleted);
        }

        // Plugin
        TryDeleteFile(Path.Combine(GetPluginPath(), randomizerFileToDelete), errors, ref deleted);

        if (errors.Count > 0)
        {
            ShowInfo("Revert finished with errors:\n" + string.Join("\n", errors));
        }
        else
        {
            ShowInfo("OpenRCT2 Archipelago removed successfully!");
        }

        Debug.Log("Revert complete. Files deleted: " + deleted);
    }

    void TryDeleteFile(string path, List<string> errors, ref int deleted)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                deleted++;
                Debug.Log("Deleted: " + path);
            }
            else
            {
                Debug.LogWarning("File not found, skipping: " + path);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to delete " + path + ": " + e.Message);
            errors.Add(Path.GetFileName(path) + ": " + e.Message);
        }
    }

    // ---------------------------------------------------------------
    // UTILS
    // ---------------------------------------------------------------

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Download error: " + request.error);
                Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Config load failed: " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
                Debug.Log("Remote config loaded successfully");
                ApplyGameConfig();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to parse config: " + e.Message);
            }
        }

        configLoaded = true;
        Debug.Log("Config marked as loaded");
    }
}