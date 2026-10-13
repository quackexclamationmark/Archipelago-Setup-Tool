using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FNAFWManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("FNAFW FILES")]
    public FileDownloader.FileData fnafworldApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;

    [Header("BUTTONS")]
    public Button setupButton;
    public Button revertButton;

    [Header("GAME FOLDER (MANUAL DOWNLOAD)")]
    public TextMeshProUGUI gameFolderText;
    public Button copyFolderPathButton;
    public Button openFolderButton;

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
    private const string FNAFW_FOLDER_NAME = "FNaFW Game";

    private FNAFWConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private string gameFolderPath = "";

    [System.Serializable]
    public class FNAFWConfig
    {
        public string fnafworldApworld;
        public string[] apSearchPaths;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (setupButton != null)
            setupButton.onClick.AddListener(RunSetup);

        if (revertButton != null)
            revertButton.onClick.AddListener(RevertAll);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (copyFolderPathButton != null)
            copyFolderPathButton.onClick.AddListener(CopyGameFolderPath);

        if (openFolderButton != null)
            openFolderButton.onClick.AddListener(OpenGameFolder);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        SetupGameFolder();
    }

    // =========================================================
    // GAME FOLDER (MANUAL DOWNLOAD)
    // =========================================================

    string[] GetGameFolderCandidates()
    {
        return new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", FNAFW_FOLDER_NAME),
        };
    }

    // Creates the "FNaFW Game" folder (first location that works) and updates the text field.
    string EnsureGameFolder()
    {
        foreach (string dir in GetGameFolderCandidates())
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                gameFolderPath = dir;
                return dir;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not create FNaFW Game dir: " + dir + " - " + e.Message);
            }
        }

        gameFolderPath = "";
        return "";
    }

    void SetupGameFolder()
    {
        string path = EnsureGameFolder();

        if (gameFolderText != null)
        {
            if (string.IsNullOrEmpty(path))
                gameFolderText.text = "Could not create the FNaFW Game folder.";
            else
                gameFolderText.text = path;
        }

        UnityEngine.Debug.Log("FNaFW Game folder: " + path);
    }

    public void CopyGameFolderPath()
    {
        string path = EnsureGameFolder();

        if (string.IsNullOrEmpty(path))
        {
            ShowInfo("ERROR: Cannot create the FNaFW Game folder.");
            return;
        }

        GUIUtility.systemCopyBuffer = path;
        UnityEngine.Debug.Log("Copied to clipboard: " + path);
        ShowInfo("Folder path copied to clipboard:\n" + path);
    }

    public void OpenGameFolder()
    {
        string path = EnsureGameFolder();

        if (string.IsNullOrEmpty(path))
        {
            ShowInfo("ERROR: Cannot create the FNaFW Game folder.");
            return;
        }

        try
        {
            Process.Start("explorer.exe", "\"" + path + "\"");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to open folder: " + e.Message);
            ShowInfo("ERROR: Could not open the folder\n" + e.Message);
        }
    }

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

    void ApplyFNAFWConfig()
    {
        if (remoteConfig == null)
            return;

        fnafworldApworld.url = remoteConfig.fnafworldApworld;
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
            remoteConfig = JsonUtility.FromJson<FNAFWConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyFNAFWConfig();
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

        if (!doApworld)
        {
            ShowInfo("Please select at least one component to install.");
            yield break;
        }

        yield return APWorldOnlyFlow();
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

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + fnafworldApworld.url);

        if (string.IsNullOrEmpty(fnafworldApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = fnafworldApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = fnafworldApworld.url.Substring(fnafworldApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName))
            fileName = "fnafw.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + fnafworldApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(fnafworldApworld.url, localPath);

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
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    // =========================================================
    // REVERT
    // =========================================================

    private IEnumerator ExecuteRevert()
    {
        ShowInfo("Removing Archipelago\\FNaFW Game folder...");

        bool foundAny = false;
        foreach (string dir in GetGameFolderCandidates())
        {
            if (Directory.Exists(dir))
            {
                foundAny = true;
                yield return SafeDeleteDirectoryAsync(dir);
            }
        }

        if (!foundAny)
        {
            UnityEngine.Debug.LogWarning("No Archipelago\\FNaFW Game folder found to delete.");
            ShowInfo("No Archipelago\\FNaFW Game folder found.");
        }
        else
        {
            ShowInfo("Revert complete: Archipelago\\FNaFW Game removed where present.");
        }

        // Re-create the empty folder so the displayed path stays valid.
        SetupGameFolder();

        UnityEngine.Debug.Log("END: FNAFW Revert");
        yield return null;
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