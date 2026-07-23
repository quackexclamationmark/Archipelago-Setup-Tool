using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class ScritchyManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SCRITCHY FILES")]
    public FileDownloader.FileData scritchyApworld;
    public FileDownloader.FileData scritchyAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installAPWorldToggle;
    public Toggle installAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process scritchyProcess;
    private string scritchyPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private ScritchyConfig remoteConfig;
    private bool configLoaded = false;

    // AP mod DLL files to manage
    private readonly string[] AP_MOD_DLLS = new string[]
    {
        "Archipelago.MultiClient.Net.dll",
        "Il2Cpp.TlsAdapter.dll",
        "Newtonsoft.Json.dll",
        "ScritchyScratchAP.dll"
    };

    // BepInEx root-level files
    private readonly string[] BEPINEX_ROOT_FILES = new string[]
    {
        "winhttp.dll",
        ".doorstop_version",
        "doorstop_config.ini"
    };

    // BepInEx directories
    private readonly string[] BEPINEX_DIRECTORIES = new string[]
    {
        "BepInEx",
        "dotnet"
    };

    [System.Serializable]
    public class ScritchyConfig
    {
        public string scritchyApworld;
        public string scritchyAP;
    }

    void Start()
    {
        scritchyPath = GetScritchyPath();
        StartCoroutine(LoadRemoteConfig());

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.isOn = false;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installBepInExToggle != null)
            installBepInExToggle.isOn = true;

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installAPToggle != null)
            installAPToggle.isOn = true;
    }

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void ApplyScritchyConfig()
    {
        if (remoteConfig == null)
            return;

        scritchyApworld.url = remoteConfig.scritchyApworld;
        scritchyAP.url = remoteConfig.scritchyAP;
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
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;
            case "Revert":
                ExecuteRevert();
                break;
            case "ForceFullClear":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        if (!apworld && !bep && !ap)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        if ((bep || ap) && string.IsNullOrEmpty(scritchyPath))
            if (string.IsNullOrEmpty(scritchyPath))
        {
            ShowInfo("Scritchy Scratchy not found. Please check installation.");
            return;
        }

        int count = (bep ? 1 : 0) + (ap ? 1 : 0) + (apworld ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (ap && count == 1 && !bep)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchScritchy();
    }

    private void ExecuteRevert()
    {
        scritchyPath = GetScritchyPath();

        if (string.IsNullOrEmpty(scritchyPath))
            return;

        DeleteOldVersionFiles();

        string pluginsPath = Path.Combine(scritchyPath, "BepInEx", "plugins");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            // Remove AP mod DLLs from plugins
            RemoveAPModDLLs(pluginsPath);

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClear &&
            hasOtherMods &&
            !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        if (fullClear)
        {
            ShowInfo("Clearing BepInEx...");

            // Remove specific files
            SafeDeleteFile(Path.Combine(scritchyPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(scritchyPath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(scritchyPath, "doorstop_config.ini"));

            // Remove directories
            SafeDeleteDirectory(Path.Combine(scritchyPath, "BepInEx"));
            SafeDeleteDirectory(Path.Combine(scritchyPath, "dotnet"));

            ShowInfo("Full clear completed!");
            return;
        }

        ShowInfo("Revert completed!");
    }

    void RemoveAPModDLLs(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return;

        foreach (string dllName in AP_MOD_DLLS)
        {
            string dllPath = Path.Combine(pluginsPath, dllName);
            if (File.Exists(dllPath))
            {
                try
                {
                    File.Delete(dllPath);
                    UnityEngine.Debug.Log("Deleted AP mod DLL: " + dllName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not delete " + dllName + ": " + e.Message);
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

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
        {
            UnityEngine.Debug.Log("Plugins path does not exist: " + pluginsPath);
            return false;
        }

        // Check directories
        string[] dirs = Directory.GetDirectories(pluginsPath);
        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);
            UnityEngine.Debug.LogWarning("[OtherMods] Found directory: " + name);
            return true;
        }

        // Check files
        string[] files = Directory.GetFiles(pluginsPath);
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            // Skip AP mod DLLs
            bool isAPDLL = false;
            foreach (string dllName in AP_MOD_DLLS)
            {
                if (fileName == dllName)
                {
                    isAPDLL = true;
                    UnityEngine.Debug.Log("[OtherMods] Skipping AP mod DLL: " + fileName);
                    break;
                }
            }

            if (isAPDLL)
                continue;

            if (fileName.StartsWith("Scritchy Scratchy AP Version") && fileName.EndsWith(".txt"))
            {
                UnityEngine.Debug.Log("[OtherMods] Skipping version file: " + fileName);
                continue;
            }

            if (fileName == "ScritchyScratchyAP.dll")
            {
                UnityEngine.Debug.Log("[OtherMods] Skipping ScritchyScratchyAP.dll: " + fileName);
                continue;
            }

            // Found other mod
            UnityEngine.Debug.LogWarning("[OtherMods] Found other mod file: " + fileName);
            return true;
        }

        UnityEngine.Debug.Log("No other mods detected in plugins directory.");
        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ScritchyAPTemp");

        ShowInfo("Installing AP Mod...");
        yield return downloader.DownloadAndExtract(scritchyAP, Application.persistentDataPath, extractPath);

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        if (bep)
        {
            ShowInfo("Installing BepInEx...");

            // Copy BepInEx root files
            foreach (string fileName in BEPINEX_ROOT_FILES)
            {
                string sourcePath = Path.Combine(extractPath, fileName);
                if (File.Exists(sourcePath))
                {
                    string targetPath = Path.Combine(scritchyPath, fileName);
                    try
                    {
                        File.Copy(sourcePath, targetPath, true);
                        UnityEngine.Debug.Log("Copied BepInEx file: " + fileName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not copy " + fileName + ": " + e.Message);
                    }
                }
            }

            foreach (string dirName in BEPINEX_DIRECTORIES)
            {
                string sourceDir = Path.Combine(extractPath, dirName);
                if (Directory.Exists(sourceDir))
                {
                    string targetDir = Path.Combine(scritchyPath, dirName);

                    if (dirName == "BepInEx" && Directory.Exists(targetDir))
                    {
                        MergeDirectory(sourceDir, targetDir);
                        UnityEngine.Debug.Log("BepInEx merged into existing installation.");
                    }
                    else
                    {
                        if (Directory.Exists(targetDir))
                            SafeDeleteDirectory(targetDir);

                        CopyDirectory(sourceDir, targetDir);
                        UnityEngine.Debug.Log("Copied directory: " + dirName);
                    }
                }
            }
        }

        if (ap)
        {
            ShowInfo("Installing AP Mod files...");

            string extractedPluginsDir = Path.Combine(extractPath, "BepInEx", "plugins");
            string targetBepInExDir = Path.Combine(scritchyPath, "BepInEx");
            string targetPluginsPath = Path.Combine(targetBepInExDir, "plugins");

            if (!Directory.Exists(targetBepInExDir))
            {
                Directory.CreateDirectory(targetBepInExDir);
            }

            if (Directory.Exists(extractedPluginsDir))
            {
                Directory.CreateDirectory(targetPluginsPath);

                foreach (string dllName in AP_MOD_DLLS)
                {
                    string sourceDll = Path.Combine(extractedPluginsDir, dllName);
                    if (File.Exists(sourceDll))
                    {
                        string targetDll = Path.Combine(targetPluginsPath, dllName);
                        try
                        {
                            File.Copy(sourceDll, targetDll, true);
                            UnityEngine.Debug.Log("Copied AP mod DLL: " + dllName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not copy " + dllName + ": " + e.Message);
                        }
                    }
                }
            }

            CreateVersionFile(scritchyAP.url);
        }

        SafeDeleteDirectory(extractPath);

        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Scritchy Scratchy...");
            LaunchScritchy();

            yield return new WaitForSeconds(2f);

            CloseScritchy();

            yield return new WaitForSeconds(1f);

            ShowInfo("Launching game...");
            LaunchScritchy();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + scritchyApworld.url);

        if (string.IsNullOrEmpty(scritchyApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = scritchyApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = scritchyApworld.url.Substring(scritchyApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + scritchyApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(scritchyApworld.url, localPath);

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

        UnityEngine.Debug.Log("Target path: " + target);

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

    IEnumerator APOnlyFlow()
    {
        while (!configLoaded)
            yield return null;

        ShowInfo("Installing AP Mod...");

        string extractPath = Path.Combine(Application.persistentDataPath, "ScritchyAPTemp");
        yield return downloader.DownloadAndExtract(scritchyAP, Application.persistentDataPath, extractPath);

        string extractedPluginsDir = Path.Combine(extractPath, "BepInEx", "plugins");
        string targetBepInExDir = Path.Combine(scritchyPath, "BepInEx");
        string targetPluginsPath = Path.Combine(targetBepInExDir, "plugins");

        // Ensure BepInEx structure exists
        if (!Directory.Exists(targetBepInExDir))
        {
            Directory.CreateDirectory(targetBepInExDir);
        }

        // Copy AP mod DLLs from plugins
        if (Directory.Exists(extractedPluginsDir))
        {
            Directory.CreateDirectory(targetPluginsPath);

            foreach (string dllName in AP_MOD_DLLS)
            {
                string sourceDll = Path.Combine(extractedPluginsDir, dllName);
                if (File.Exists(sourceDll))
                {
                    string targetDll = Path.Combine(targetPluginsPath, dllName);
                    try
                    {
                        File.Copy(sourceDll, targetDll, true);
                        UnityEngine.Debug.Log("Copied AP mod DLL: " + dllName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not copy " + dllName + ": " + e.Message);
                    }
                }
            }
        }

        CreateVersionFile(scritchyAP.url);
        ShowInfo("Installation complete!");

        SafeDeleteDirectory(extractPath);
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        while (!configLoaded)
            yield return null;

        ShowInfo("Installing BepInEx...");

        string extractPath = Path.Combine(Application.persistentDataPath, "ScritchyAPTemp");
        yield return downloader.DownloadAndExtract(scritchyAP, Application.persistentDataPath, extractPath);

        // Copy BepInEx root files
        foreach (string fileName in BEPINEX_ROOT_FILES)
        {
            string sourcePath = Path.Combine(extractPath, fileName);
            if (File.Exists(sourcePath))
            {
                string targetPath = Path.Combine(scritchyPath, fileName);
                try
                {
                    File.Copy(sourcePath, targetPath, true);
                    UnityEngine.Debug.Log("Copied BepInEx file: " + fileName);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Could not copy " + fileName + ": " + e.Message);
                }
            }
        }

        // Copy BepInEx directories
        foreach (string dirName in BEPINEX_DIRECTORIES)
        {
            string sourceDir = Path.Combine(extractPath, dirName);
            if (Directory.Exists(sourceDir))
            {
                string targetDir = Path.Combine(scritchyPath, dirName);

                if (Directory.Exists(targetDir))
                    SafeDeleteDirectory(targetDir);

                CopyDirectory(sourceDir, targetDir);
                UnityEngine.Debug.Log("Copied directory: " + dirName);
            }
        }

        SafeDeleteDirectory(extractPath);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Scritchy Scratchy...");
            LaunchScritchy();
            yield return new WaitForSeconds(2f);
            CloseScritchy();

            ShowInfo("Second launch...");
            LaunchScritchy();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    void LaunchScritchy()
    {
        string exePath = Path.Combine(scritchyPath, "ScritchyScratchy.exe");

        try
        {
            if (File.Exists(exePath))
                scritchyProcess = Process.Start(exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch Scritchy Scratchy: " + e.Message);
        }
    }

    void CloseScritchy()
    {
        try
        {
            if (scritchyProcess != null && !scritchyProcess.HasExited)
            {
                scritchyProcess.Kill();
                scritchyProcess.Dispose();
                scritchyProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseScritchy();
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

    void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
        }
    }

    void MergeDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string targetDir = dir.Replace(source, target);
            Directory.CreateDirectory(targetDir);
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            try
            {
                File.Copy(file, dest, true);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not copy file " + Path.GetFileName(file) + ": " + e.Message);
            }
        }
    }

    void CreateVersionFile(string apUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl);

            string versionFileName = "Scritchy Scratchy AP Version " + apVersion + ".txt";
            string content = "Scritchy Scratchy AP Setup Tool\n";
            content += "https://github.com/yourusername/Scritchy-Scratchy-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(scritchyPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(scritchyPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Scritchy Scratchy AP Version .+\.txt");

            if (Directory.Exists(scritchyPath))
            {
                string[] rootFiles = Directory.GetFiles(scritchyPath);
                foreach (string file in rootFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in root: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message);
                        }
                    }
                }
            }

            string pluginsPath = Path.Combine(scritchyPath, "BepInEx", "plugins");
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

    string ExtractVersionFromUrl(string url)
    {
        try
        {
            // Try to extract version from filename pattern like "ScritchyScratchyAP-v0.1.2-hotfix.2.zip"
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"ScritchyScratchyAP[^/]*");
            System.Text.RegularExpressions.Match match = regex.Match(url);

            if (match.Success)
            {
                string matched = match.Groups[0].Value;
                // Remove .zip extension if present
                if (matched.EndsWith(".zip"))
                    matched = matched.Substring(0, matched.Length - 4);
                return matched;
            }
        }
        catch { }

        if (!string.IsNullOrEmpty(url))
        {
            int idx = url.LastIndexOf('/');
            if (idx >= 0 && idx + 1 < url.Length)
            {
                string candidate = url.Substring(idx + 1);
                if (candidate.EndsWith(".zip"))
                    candidate = candidate.Substring(0, candidate.Length - 4);
                return candidate;
            }
        }

        return "Unknown";
    }

    string GetScritchyPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Scritchy Scratchy"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Scritchy Scratchy"),
            @"D:\Steam\steamapps\common\Scritchy Scratchy",
            @"D:\SteamLibrary\steamapps\common\Scritchy Scratchy",
            @"D:\steamapps\common\Scritchy Scratchy",
            @"E:\Steam\steamapps\common\Scritchy Scratchy",
            @"E:\SteamLibrary\steamapps\common\Scritchy Scratchy",
            @"E:\steamapps\common\Scritchy Scratchy",
            @"E:\Program Files (x86)\steamapps\common\Scritchy Scratchy",
            @"E:\Program Files\steamapps\common\Scritchy Scratchy",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + path);
                    return path;
                }
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
                    // Look for Steam\steamapps
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Scritchy Scratchy");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for SteamLibrary\steamapps
                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Scritchy Scratchy");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look for steamapps at the root of the drive
                    subPath = Path.Combine(drive.Name, "steamapps", "common", "Scritchy Scratchy");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files (x86)\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Scritchy Scratchy");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + subPath);
                        return subPath;
                    }

                    // Look in Program Files\steamapps
                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Scritchy Scratchy");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Scritchy Scratchy (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Scritchy Scratchy (Steam) not found.");
        return "";
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
            remoteConfig = JsonUtility.FromJson<ScritchyConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyScritchyConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }
}
