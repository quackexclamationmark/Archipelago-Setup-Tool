using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
/*using UnityEngine.LightTransport;*/
using UnityEngine.UI;

public class FNAFWManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("FNAFW FILES")]
    public FileDownloader.FileData fnafworldApworld;
    public FileDownloader.FileData fnafworldExe;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle gameCopyToggle;

    [Header("BUTTONS")]
    public Button setupButton;
    public Button revertButton;

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
    private bool lastExeInstallSuccess = false;

    [System.Serializable]
    public class FNAFWConfig
    {
        public string fnafworldApworld;
        public string fnafworldExe;
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

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (gameCopyToggle != null)
            gameCopyToggle.isOn = true;
    }

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

    void ApplyFNAFWConfig()
    {
        if (remoteConfig == null)
            return;

        fnafworldApworld.url = remoteConfig.fnafworldApworld;
        fnafworldExe.url = remoteConfig.fnafworldExe;
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
        bool doGameCopy = gameCopyToggle == null || gameCopyToggle.isOn;

        int count =
            (doApworld ? 1 : 0) +
            (doGameCopy ? 1 : 0);

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

        if (doGameCopy && count == 1)
        {
            yield return ExeOnlyFlow();
            yield break;
        }

        yield return InstallFlow();
    }

    IEnumerator InstallFlow()
    {
        if (gameCopyToggle == null || gameCopyToggle.isOn)
        {
            ShowInfo("Downloading FNaF World...");
            yield return InstallExe();
        }

        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        UnityEngine.Debug.Log("END: FNAFW Setup");
        ShowInfo("Installation complete!");
    }

    IEnumerator ExeOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Downloading FNaF World...");
        yield return new WaitForSeconds(1f);

        yield return InstallExe();

        if (!lastExeInstallSuccess)
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

    IEnumerator InstallExe()
    {
        lastExeInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. FNaF World exe URL: " + fnafworldExe.url);

        if (string.IsNullOrEmpty(fnafworldExe.url))
        {
            ShowInfo("ERROR: FNaF World download URL is empty!");
            UnityEngine.Debug.LogError("FNaF World exe URL not set!");
            yield break;
        }

        string fileName = fnafworldExe.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = fnafworldExe.url.Substring(fnafworldExe.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName))
            fileName = "fnaf-world.exe";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading FNaF World from: " + fnafworldExe.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(fnafworldExe.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: FNaF World download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string[] archipelagoDirs = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", FNAFW_FOLDER_NAME),
        };

        string targetDir = "";
        foreach (string dir in archipelagoDirs)
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                targetDir = dir;
                UnityEngine.Debug.Log("Using Archipelago target dir: " + targetDir);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not create Archipelago dir: " + dir + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(targetDir))
        {
            ShowInfo("ERROR: Cannot create Archipelago FNaFW Game directory.");
            UnityEngine.Debug.LogError("No valid Archipelago target directory found for FNaFW exe.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(targetDir, fileName);

        try
        {
            if (File.Exists(target))
            {
                try
                {
                    File.SetAttributes(target, FileAttributes.Normal);
                    File.Delete(target);
                    UnityEngine.Debug.Log("Deleted existing target exe");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete existing target exe: " + e.Message);
                }
            }

            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("Copied FNaF World exe to: " + target);
            ShowInfo("FNaF World executable copied to Archipelago\\FNaFW Game successfully!");
            lastExeInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy exe: " + e.Message);
            ShowInfo("ERROR: Failed to copy FNaF World executable\n" + e.Message);
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

    private IEnumerator ExecuteRevert()
    {
        ShowInfo("Removing Archipelago\\FNaFW Game folder...");

        string[] archipelagoDirs = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", FNAFW_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", FNAFW_FOLDER_NAME),
        };

        bool foundAny = false;
        foreach (string dir in archipelagoDirs)
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