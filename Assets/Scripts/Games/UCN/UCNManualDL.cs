using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
/*using UnityEngine.LightTransport;*/
using UnityEngine.UI;

public class UCNManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("UCN FILES")]
    public FileDownloader.FileData ucnApworld;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Ultimate Custom Night";

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

    private string ucnPath;
    private string pendingAction;
    private const string UCN_FOLDER_NAME = "UCN";

    private UCNConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class UCNConfig
    {
        public string ucnApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        ucnPath = GetUCNSteamPath();

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

    void ApplyUCNConfig()
    {
        if (remoteConfig == null)
            return;

        ucnApworld.url = remoteConfig.ucnApworld;
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
            remoteConfig = JsonUtility.FromJson<UCNConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyUCNConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        ucnPath = GetUCNSteamPath();
    }

    // =========================================================

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to start setup?", "Setup");
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
        ucnPath = GetUCNSteamPath();

        bool doApworld = installApworldToggle == null || installApworldToggle.isOn;
        bool doGameCopy = gameCopyToggle == null || gameCopyToggle.isOn;

        bool needsGamePath = doGameCopy;

        if (needsGamePath && (string.IsNullOrEmpty(ucnPath) || !Directory.Exists(ucnPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            yield break;
        }

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
            yield return GameCopyOnlyFlow();
            yield break;
        }

        yield return InstallFlow();
    }

    IEnumerator InstallFlow()
    {
        if (gameCopyToggle == null || gameCopyToggle.isOn)
        {
            yield return CopyGameExeToArchipelago();
        }

        if (installApworldToggle == null || installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        UnityEngine.Debug.Log("END: UCN Setup");
        ShowInfo("Installation complete!");
    }

    IEnumerator GameCopyOnlyFlow()
    {
        yield return CopyGameExeToArchipelago();
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        ShowInfo("Installation complete!");
    }

    private IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. UCN APWorld URL: " + ucnApworld.url);

        if (ucnApworld == null || string.IsNullOrEmpty(ucnApworld.url))
        {
            ShowInfo("ERROR: UCN APWorld URL is empty!");
            UnityEngine.Debug.LogError("UCN APWorld URL not set!");
            yield break;
        }

        string fileName = ucnApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ucnApworld.url.Substring(ucnApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.temporaryCachePath, fileName);
        UnityEngine.Debug.Log("Downloading UCN APWorld from: " + ucnApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(ucnApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: UCN APWorld download failed!");
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
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete existing target apworld: " + e.Message);
            }
        }

        try
        {
            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("UCN APWorld copied to: " + target);
            ShowInfo("UCN APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy UCN APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install UCN APWorld\n" + e.Message);
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

    private IEnumerator CopyGameExeToArchipelago()
    {
        ShowInfo("Locating Ultimate Custom Night installation...");
        UnityEngine.Debug.Log("START: Copy Ultimate Custom Night.exe");

        string ucnPath = GetUCNSteamPath();

        if (string.IsNullOrEmpty(ucnPath))
        {
            UnityEngine.Debug.LogWarning("Ultimate Custom Night (Steam) not found.");
            ShowInfo("Ultimate Custom Night not found on this machine (Steam). Please verify installation.");
            yield break;
        }

        UnityEngine.Debug.Log("Found UCN at: " + ucnPath);

        string exeName = "Ultimate Custom Night.exe";
        string sourceExe = Path.Combine(ucnPath, exeName);

        if (!File.Exists(sourceExe))
        {
            string found = null;
            try
            {
                string[] files = Directory.GetFiles(ucnPath, "*.exe", SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    string fn = Path.GetFileName(f).ToLower();
                    if (fn.Contains("ultimate") && fn.Contains("custom") && fn.Contains("night"))
                    {
                        found = f;
                        break;
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Error searching for exe: " + e.Message);
            }

            if (string.IsNullOrEmpty(found))
            {
                UnityEngine.Debug.LogWarning("Ultimate Custom Night exe not found in: " + ucnPath);
                ShowInfo("Ultimate Custom Night executable not found in detected folder.");
                yield break;
            }
            else
            {
                sourceExe = found;
                UnityEngine.Debug.Log("Found exe at: " + sourceExe);
            }
        }

        string[] archipelagoDirs = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago", UCN_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", UCN_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", UCN_FOLDER_NAME),
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
            ShowInfo("ERROR: Cannot create Archipelago UCN directory.");
            UnityEngine.Debug.LogError("No valid Archipelago target directory found for UCN exe.");
            yield break;
        }

        string targetExe = Path.Combine(targetDir, exeName);

        try
        {
            if (File.Exists(targetExe))
            {
                try
                {
                    File.SetAttributes(targetExe, FileAttributes.Normal);
                    File.Delete(targetExe);
                    UnityEngine.Debug.Log("Deleted existing target exe");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete existing target exe: " + e.Message);
                }
            }

            File.Copy(sourceExe, targetExe, true);
            UnityEngine.Debug.Log("Copied UCN exe to: " + targetExe);
            ShowInfo("Ultimate Custom Night executable copied to Archipelago\\UCN successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy exe: " + e.Message);
            ShowInfo("ERROR: Failed to copy Ultimate Custom Night executable\n" + e.Message);
        }

        yield return null;
    }

    private IEnumerator ExecuteRevert()
    {
        string ucnPath = GetUCNSteamPath();

        ShowInfo("Removing Archipelago\\UCN folder...");

        string[] archipelagoDirs = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago", UCN_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", UCN_FOLDER_NAME),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", UCN_FOLDER_NAME),
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
            UnityEngine.Debug.LogWarning("No Archipelago\\UCN folder found to delete.");
            ShowInfo("No Archipelago\\UCN folder found.");
        }
        else
        {
            ShowInfo("Revert complete: Archipelago\\UCN removed where present.");
        }

        UnityEngine.Debug.Log("END: UCN Revert");
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

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetUCNSteamPath()
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