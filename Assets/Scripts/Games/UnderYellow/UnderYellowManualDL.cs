using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UnderYellowManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("UNDERTALE YELLOW FILES")]
    public FileDownloader.FileData undertaleyellowApworld;
    public FileDownloader.FileData undertaleyellowDL;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installUndertaleYellowToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("LAUNCH")]
    public Button launchButton;

    private const string InstalledFoldersKey = "UndertaleYellow_InstalledFolders";

    private string archipelagoPath;
    private string pendingAction;

    private UndertaleYellowConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class UndertaleYellowConfig
    {
        public string undertaleyellowApworld;
        public string undertaleyellowDL;
        public string[] apSearchPaths;
    }


    void Start()
    {
        archipelagoPath = GetArchipelagoPath();

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

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchButtonClicked);

        StartCoroutine(LoadRemoteConfig());
    }

    // ---------------------------------------------------------------
    // BUTTON ENTRY POINTS
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

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // LAUNCH BUTTON HANDLER
    // ---------------------------------------------------------------

    public void OnLaunchButtonClicked()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath) || !Directory.Exists(archipelagoPath))
        {
            ShowInfo("Archipelago folder not found.");
            return;
        }

        ShowInfo("Launching Undertale Yellow...");
        LaunchUndertaleYellow();
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath))
        {
            ShowInfo("ERROR: Could not find or create the Archipelago folder!");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool undertaleYellow = installUndertaleYellowToggle == null || installUndertaleYellowToggle.isOn;

        if (!apworld && !undertaleYellow)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && !undertaleYellow)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow(apworld, undertaleYellow));
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

    IEnumerator InstallFlow(bool apworld, bool undertaleYellow)
    {
        if (apworld)
        {
            ShowInfo("Installing Undertale Yellow APWorld...");
            yield return InstallAPWorld();
        }

        if (undertaleYellow)
        {
            ShowInfo("Downloading Undertale Yellow...");
            yield return InstallUndertaleYellowDL();
        }
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + undertaleyellowApworld.url);

        if (string.IsNullOrEmpty(undertaleyellowApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = undertaleyellowApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = undertaleyellowApworld.url.Substring(undertaleyellowApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + undertaleyellowApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(undertaleyellowApworld.url, localPath);

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

    IEnumerator InstallUndertaleYellowDL()
    {
        UnityEngine.Debug.Log("START InstallUndertaleYellowDL");

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(undertaleyellowDL.url))
        {
            ShowInfo("ERROR: Download URL is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "UndertaleYellow_Extract");

        yield return downloader.DownloadAndExtract(undertaleyellowDL, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extraction failed: " + extractPath + " does not exist");
            ShowInfo("ERROR: Download/extraction failed!");
            yield break;
        }

        // Remember which top-level folder(s) came out of the zip, so Revert/Launch
        // can find them again even if the version (and therefore the folder name) changes.
        string[] topLevelFolders = GetTopLevelDirNames(extractPath);

        // Copy the extracted content directly into the Archipelago root folder
        // (one level above custom_worlds), NOT into custom_worlds.
        CopyExtractedFiles(extractPath, archipelagoPath);

        if (topLevelFolders.Length > 0)
        {
            PlayerPrefs.SetString(InstalledFoldersKey, string.Join(",", topLevelFolders));
            PlayerPrefs.Save();
        }

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallUndertaleYellowDL");
        ShowInfo("Undertale Yellow installed successfully!");
    }

    void CopyExtractedFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyExtractedFiles: " + extractPath + " -> " + targetPath);

        try
        {
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("All extracted files copied to Archipelago directory");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying extracted files: " + e.Message);
        }

        UnityEngine.Debug.Log("END CopyExtractedFiles");
    }

    string[] GetTopLevelDirNames(string path)
    {
        try
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            string[] names = new string[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
                names[i] = Path.GetFileName(dirs[i]);
            return names;
        }
        catch
        {
            return new string[0];
        }
    }

    // ---------------------------------------------------------------
    // REVERT (only removes the folder placed into Archipelago by the DL zip)
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath) || !Directory.Exists(archipelagoPath))
        {
            ShowInfo("Archipelago folder not found.");
            return;
        }

        ShowInfo("Removing Undertale Yellow files...");

        bool removedSomething = false;

        string stored = PlayerPrefs.GetString(InstalledFoldersKey, "");
        if (!string.IsNullOrEmpty(stored))
        {
            string[] folders = stored.Split(',');
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string fullPath = Path.Combine(archipelagoPath, folder);
                if (Directory.Exists(fullPath))
                {
                    SafeDeleteDirectory(fullPath);
                    removedSomething = true;
                }
            }

            PlayerPrefs.DeleteKey(InstalledFoldersKey);
            PlayerPrefs.Save();
        }

        if (!removedSomething)
        {
            // Fallback if we have no record (e.g. fresh app run): look for a folder
            // whose name still looks like an Undertale Yellow install.
            try
            {
                string[] dirs = Directory.GetDirectories(archipelagoPath);
                foreach (string dir in dirs)
                {
                    string name = Path.GetFileName(dir).ToLower();
                    if (name.Contains("undertale") && name.Contains("yellow"))
                    {
                        SafeDeleteDirectory(dir);
                        removedSomething = true;
                    }
                }
            }
            catch { }
        }

        if (removedSomething)
            ShowInfo("Undertale Yellow files removed successfully!");
        else
            ShowInfo("No Undertale Yellow installation found to remove.");
    }

    // ---------------------------------------------------------------
    // LAUNCH
    // ---------------------------------------------------------------

    void LaunchUndertaleYellow()
    {
        try
        {
            string exePath = FindUndertaleYellowExecutable();

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Undertale Yellow launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Undertale Yellow.exe not found under: " + archipelagoPath);
                ShowInfo("Undertale Yellow.exe not found! Please make sure you setup first!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Undertale Yellow: " + e.Message);
            ShowInfo("ERROR: Failed to launch Undertale Yellow!");
        }
    }

    // Locates the exe dynamically since the install folder / exe name changes with game version
    // (e.g. "Archipelago\Undertale Yellow v-1-3-1\Undertale Yellow.exe" today, something else tomorrow).
    string FindUndertaleYellowExecutable()
    {
        if (string.IsNullOrEmpty(archipelagoPath) || !Directory.Exists(archipelagoPath))
            return "";

        string stored = PlayerPrefs.GetString(InstalledFoldersKey, "");
        if (!string.IsNullOrEmpty(stored))
        {
            string[] folders = stored.Split(',');
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string exe = FindExeInFolder(Path.Combine(archipelagoPath, folder));
                if (!string.IsNullOrEmpty(exe))
                    return exe;
            }
        }

        // Fallback: scan the Archipelago root for a folder that looks like Undertale Yellow
        try
        {
            string[] dirs = Directory.GetDirectories(archipelagoPath);
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir).ToLower();
                if (name.Contains("undertale") && name.Contains("yellow"))
                {
                    string exe = FindExeInFolder(dir);
                    if (!string.IsNullOrEmpty(exe))
                        return exe;
                }
            }
        }
        catch { }

        return "";
    }

    string FindExeInFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return "";

            string[] exes = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);

            // Prefer an exe whose name actually says "Undertale Yellow"
            foreach (string exe in exes)
            {
                string name = Path.GetFileNameWithoutExtension(exe).ToLower();
                if (name.Contains("undertale") && name.Contains("yellow"))
                    return exe;
            }

            if (exes.Length > 0)
                return exes[0];
        }
        catch { }

        return "";
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
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

    // Finds (or creates) the Archipelago root folder, e.g. C:\ProgramData\Archipelago
    string GetArchipelagoPath()
    {
        string[] candidates = new string[]
        {
            @"C:\ProgramData\Archipelago",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        // Prefer an Archipelago folder that already exists
        foreach (string candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    UnityEngine.Debug.Log("Found Archipelago folder at: " + candidate);
                    return candidate;
                }
            }
            catch { }
        }

        // Otherwise create the first one we're able to
        foreach (string candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                UnityEngine.Debug.Log("Created Archipelago folder at: " + candidate);
                return candidate;
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Could not find or create an Archipelago folder.");
        return "";
    }

    // ---------------------------------------------------------------
    // Remote config loader (optional)
    // ---------------------------------------------------------------
    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("Undertale Yellow config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<UndertaleYellowConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Undertale Yellow remote config loaded successfully");
                ApplyUndertaleYellowConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Undertale Yellow config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyUndertaleYellowConfig()
    {
        if (remoteConfig == null)
            return;

        if (undertaleyellowApworld != null && !string.IsNullOrEmpty(remoteConfig.undertaleyellowApworld))
            undertaleyellowApworld.url = remoteConfig.undertaleyellowApworld;

        if (undertaleyellowDL != null && !string.IsNullOrEmpty(remoteConfig.undertaleyellowDL))
            undertaleyellowDL.url = remoteConfig.undertaleyellowDL;
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