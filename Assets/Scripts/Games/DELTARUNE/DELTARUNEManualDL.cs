using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DELTARUNEManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DELTARUNE FILES")]
    public FileDownloader.FileData deltaruneApworld;
    public FileDownloader.FileData deltaruneAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installPatchToggle;

    [Header("BUTTONS")]
    public Button setupButton;
    public Button revertButton;
    public Button launchButton;

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

    private const string PATCH_FOLDER_NAME = "DELTARUNE_PATCH";
    private const string GAME_FOLDER_NAME = "DELTARUNE";
    private const string GAME_EXE_NAME = "DELTARUNE.exe";

    private DELTARUNEConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastPatchInstallSuccess = false;

    [System.Serializable]
    public class DELTARUNEConfig
    {
        public string deltaruneApworld;
        public string deltaruneAP;
        public string[] apSearchPaths;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (setupButton != null)
            setupButton.onClick.AddListener(RunSetup);

        if (revertButton != null)
            revertButton.onClick.AddListener(RevertAll);

        if (launchButton != null)
            launchButton.onClick.AddListener(LaunchGame);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installPatchToggle != null)
            installPatchToggle.isOn = true;
    }

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

    void ApplyDELTARUNEConfig()
    {
        if (remoteConfig == null)
            return;

        deltaruneApworld.url = remoteConfig.deltaruneApworld;
        deltaruneAP.url = remoteConfig.deltaruneAP;
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
            remoteConfig = JsonUtility.FromJson<DELTARUNEConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDELTARUNEConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    // =========================================================

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
                StartCoroutine(ExecuteSetup());
                break;
            case "Revert":
                StartCoroutine(ExecuteRevert());
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // =========================================================
    // SETUP
    // =========================================================

    private IEnumerator ExecuteSetup()
    {
        bool doApworld = installApworldToggle == null || installApworldToggle.isOn;
        bool doPatch = installPatchToggle == null || installPatchToggle.isOn;

        int count =
            (doApworld ? 1 : 0) +
            (doPatch ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            yield break;
        }

        if (doApworld && count == 1)
        {
            yield return APWorldOnlyFlow();
            yield break;
        }

        if (doPatch && count == 1)
        {
            yield return PatchOnlyFlow();
            yield break;
        }

        yield return InstallFlow();
    }

    IEnumerator InstallFlow()
    {
        if (installPatchToggle == null || installPatchToggle.isOn)
        {
            ShowInfo("Installing DELTARUNE patch...");
            yield return InstallPatch();
        }

        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        UnityEngine.Debug.Log("END: DELTARUNE Setup");
        ShowInfo("Installation complete!");
    }

    IEnumerator PatchOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing DELTARUNE patch...");
        yield return InstallPatch();

        if (!lastPatchInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
    }

    // =========================================================
    // APWORLD INSTALL
    // =========================================================

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + deltaruneApworld.url);

        if (string.IsNullOrEmpty(deltaruneApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = deltaruneApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = deltaruneApworld.url.Substring(deltaruneApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + deltaruneApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(deltaruneApworld.url, localPath);

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

    // =========================================================
    // PATCH INSTALL (zip -> DELTARUNE_PATCH)
    // =========================================================

    IEnumerator InstallPatch()
    {
        lastPatchInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Patch URL: " + deltaruneAP.url);

        if (string.IsNullOrEmpty(deltaruneAP.url))
        {
            ShowInfo("ERROR: Patch URL is empty!");
            UnityEngine.Debug.LogError("Patch URL not set!");
            yield break;
        }

        string fileName = deltaruneAP.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = deltaruneAP.url.Substring(deltaruneAP.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localZipPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading patch from: " + deltaruneAP.url);
        UnityEngine.Debug.Log("Saving to: " + localZipPath);

        yield return DownloadFile(deltaruneAP.url, localZipPath);

        if (!File.Exists(localZipPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localZipPath);
            ShowInfo("ERROR: Patch download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("Patch archive downloaded successfully: " + localZipPath);

        string archipelagoRoot = GetOrCreateArchipelagoRootPath();

        if (string.IsNullOrEmpty(archipelagoRoot))
        {
            ShowInfo("ERROR: Cannot create/find Archipelago directory.");
            UnityEngine.Debug.LogError("No valid Archipelago root directory found for patch install.");
            DeleteTempFile(localZipPath);
            yield break;
        }

        string patchDir = Path.Combine(archipelagoRoot, PATCH_FOLDER_NAME);

        bool extractSuccess = false;
        string extractError = "";

        try
        {
            if (Directory.Exists(patchDir))
            {
                DeleteDirectoryWithRetry(patchDir, maxRetries: 10);
            }

            Directory.CreateDirectory(patchDir);

            ZipFile.ExtractToDirectory(localZipPath, patchDir);

            extractSuccess = true;
            UnityEngine.Debug.Log("Patch archive extracted to: " + patchDir);
        }
        catch (System.Exception e)
        {
            extractError = e.Message;
            UnityEngine.Debug.LogError("Failed to extract patch archive: " + e.Message);
        }

        if (!extractSuccess)
        {
            ShowInfo("ERROR: Failed to extract patch archive\n" + extractError);
            DeleteTempFile(localZipPath);
            yield break;
        }

        ShowInfo("DELTARUNE patch installed successfully!");
        lastPatchInstallSuccess = true;

        DeleteTempFile(localZipPath);
    }

    void WriteVersionFile(string versionFileName, string sourceUrl)
    {
        try
        {
            string archipelagoRoot = GetOrCreateArchipelagoRootPath();
            if (string.IsNullOrEmpty(archipelagoRoot))
                return;

            string versionPath = Path.Combine(archipelagoRoot, versionFileName);
            string content = "url=" + sourceUrl + "\ninstalled=" + System.DateTime.UtcNow.ToString("o");
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Wrote version file: " + versionPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not write version file " + versionFileName + ": " + e.Message);
        }
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    // =========================================================
    // LAUNCH
    // =========================================================

    public void LaunchGame()
    {
        string archipelagoRoot = GetOrCreateArchipelagoRootPath();

        if (string.IsNullOrEmpty(archipelagoRoot))
        {
            ShowInfo("Archipelago directory not found. Please run setup first.");
            return;
        }

        string exePath = Path.Combine(Path.Combine(archipelagoRoot, GAME_FOLDER_NAME), GAME_EXE_NAME);

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogWarning("DELTARUNE executable not found at: " + exePath);
            ShowInfo("DELTARUNE.exe not found in Archipelago\\DELTARUNE. Please verify installation.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            });
            UnityEngine.Debug.Log("Launched: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch DELTARUNE: " + e.Message);
            ShowInfo("ERROR: Failed to launch DELTARUNE\n" + e.Message);
        }
    }

    // =========================================================
    // REVERT
    // =========================================================

    private IEnumerator ExecuteRevert()
    {
        ShowInfo("Reverting...");

        string[] archipelagoRoots = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        bool foundAny = false;

        foreach (string root in archipelagoRoots)
        {
            if (!Directory.Exists(root))
                continue;

            string patchDir = Path.Combine(root, PATCH_FOLDER_NAME);
            string gameDir = Path.Combine(root, GAME_FOLDER_NAME);

            if (Directory.Exists(patchDir))
            {
                foundAny = true;
                yield return SafeDeleteDirectoryAsync(patchDir);
            }

            if (Directory.Exists(gameDir))
            {
                foundAny = true;
                yield return SafeDeleteDirectoryAsync(gameDir);
            }
        }

        if (!foundAny)
        {
            UnityEngine.Debug.LogWarning("No DELTARUNE_PATCH, DELTARUNE folder or version files found to delete.");
            ShowInfo("No AP DELTARUNE installation found.");
        }
        else
        {
            ShowInfo("Revert complete!");
        }

        UnityEngine.Debug.Log("END: DELTARUNE Revert");
        yield return null;
    }

    // =========================================================
    // DOWNLOAD / DELETE HELPERS
    // =========================================================

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

    IEnumerator SafeDeleteDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            UnityEngine.Debug.Log("Directory doesn't exist, skipping: " + path);
            yield break;
        }

        UnityEngine.Debug.Log("Starting deletion of: " + path);

        yield return new WaitForSeconds(0.3f);

        bool success = false;
        string errorMessage = "";

        try
        {
            DeleteDirectoryWithRetry(path, maxRetries: 10);
            success = true;
        }
        catch (System.Exception e)
        {
            errorMessage = e.Message;
        }

        if (success)
        {
            UnityEngine.Debug.Log("Directory deleted successfully: " + path);
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to delete directory: " + path + " - " + errorMessage);
            ShowInfo("ERROR: Could not delete " + path);
        }
    }

    void DeleteDirectoryWithRetry(string path, int maxRetries = 10)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Directory already deleted: " + path);
                    return;
                }

                string[] files = Directory.GetFiles(path);
                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted file: " + file);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete file: " + file + " - " + e.Message);
                    }
                }

                string[] directories = Directory.GetDirectories(path);
                foreach (string dir in directories)
                {
                    try
                    {
                        DeleteDirectoryWithRetry(dir, maxRetries);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete subdirectory: " + dir + " - " + e.Message);
                    }
                }

                Directory.Delete(path, false);
                UnityEngine.Debug.Log("Successfully deleted directory: " + path);
                return;
            }
            catch (System.Exception e)
            {
                if (attempt < maxRetries - 1)
                {
                    UnityEngine.Debug.LogWarning($"Delete attempt {attempt + 1}/{maxRetries} failed for {path}: {e.Message}");
                    System.Threading.Thread.Sleep(200);
                }
                else
                {
                    UnityEngine.Debug.LogError($"All {maxRetries} attempts failed for {path}: {e.Message}");
                    throw;
                }
            }
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
    // PATH DETECTION
    // =========================================================

    string GetOrCreateArchipelagoRootPath()
    {
        string[] archipelagoRoots = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        // Prefer an Archipelago folder that already exists.
        foreach (string root in archipelagoRoots)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    UnityEngine.Debug.Log("Using existing Archipelago root: " + root);
                    return root;
                }
            }
            catch { }
        }

        // Otherwise try to create one.
        foreach (string root in archipelagoRoots)
        {
            try
            {
                Directory.CreateDirectory(root);
                UnityEngine.Debug.Log("Created Archipelago root: " + root);
                return root;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not create Archipelago root: " + root + " - " + e.Message);
            }
        }

        UnityEngine.Debug.LogError("No valid Archipelago root directory found or creatable.");
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