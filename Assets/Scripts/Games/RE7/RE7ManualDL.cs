using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class RE7ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("RE7 FILES")]
    public FileDownloader.FileData re7Apworld;
    public FileDownloader.FileData re7Framework;
    public FileDownloader.FileData re7AP;

    [Header("FEATURE TOGGLES")]
    public Toggle installRe7ApworldToggle;
    public Toggle installRe7FrameworkToggle;
    public Toggle installRe7APToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanFrameworkToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process re7Process;
    private string re7Path;
    private string pendingAction;
    private RE7Config remoteConfig;
    private bool configLoaded = false;

    // Store framework backup for revert
    private string frameworkBackupPath;
    private string refprameworkBackupPath;

    // Files to delete in full clean
    private string[] filesToDeleteOnFullClean = new string[]
    {
        "dinput8.dll",
        "openvr_api.dll",
        "openvr_loader.dll",
        "reframework_revision.txt",
        "lua-apclientpp.dll",
        "lua-apclientpp-license.txt",
        ".gitattributes",
        ".gitignore",
        "LICENSE",
        "README.md",
        "DELETE_OPENVR_API_DLL_IF_YOU_WANT_TO_USE_OPENXR",
        "ref_ui.ini",
        "reframework_accessed_files.txt",
        "reframework_loose_files.txt",
        "re2_fw_config.txt",
        "re2_framework_log.txt"
    };

    // Files to delete when clearing AP mods
    private string[] filesToDeleteOnClearAPMods = new string[]
    {
        ".gitattributes",
        ".gitignore",
        "LICENSE",
        "lua-apclientpp.dll",
        "lua-apclientpp-license.txt",
        "README.md"
    };

    // DLLs to NEVER delete (game essentials)
    private string[] protectedDlls = new string[]
    {
        "AkConvolutionReverb.dll",
        "amd_ags_x64.dll",
        "dxilconv7.dll",
        "MSSpatial.dll",
        "steam_api64.dll",
        "RE7.exe"
    };

    [System.Serializable]
    public class RE7Config
    {
        public string re7Apworld;
        public string re7Framework;
        public string re7AP;
    }

    void Start()
    {
        re7Path = GetRE7Path();
        frameworkBackupPath = Path.Combine(Application.persistentDataPath, "RE7FrameworkBackup");
        refprameworkBackupPath = Path.Combine(Application.persistentDataPath, "RE7ReFrameworkBackup");
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

        if (fullCleanFrameworkToggle != null)
            fullCleanFrameworkToggle.isOn = false;

        if (fullCleanFrameworkToggle != null)
            fullCleanFrameworkToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnDestroy()
    {
        CleanupProcesses();
    }

    void CleanupProcesses()
    {
        CloseRE7();
    }

    void ApplyRE7Config()
    {
        if (remoteConfig == null)
            return;

        re7Apworld.url = remoteConfig.re7Apworld;
        re7Framework.url = remoteConfig.re7Framework;
        re7AP.url = remoteConfig.re7AP;
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
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(re7Path))
        {
            ShowInfo("RE7 path not found. Please check Steam installation.");
            return;
        }

        bool apworld = installRe7ApworldToggle == null || installRe7ApworldToggle.isOn;
        bool framework = installRe7FrameworkToggle != null && installRe7FrameworkToggle.isOn;
        bool re7mod = installRe7APToggle != null && installRe7APToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (framework ? 1 : 0) +
            (re7mod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(ApWorldOnlyFlow());
            return;
        }

        if (framework && count == 1)
        {
            StartCoroutine(FrameworkOnlyFlow());
            return;
        }

        if (re7mod && count == 1)
        {
            StartCoroutine(RE7APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        re7Path = GetRE7Path();

        if (string.IsNullOrEmpty(re7Path))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanFrameworkToggle != null && fullCleanFrameworkToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP Client...");

            // Remove AP Client files
            try
            {
                DirectoryInfo re7Root = new DirectoryInfo(re7Path);
                foreach (FileInfo file in re7Root.GetFiles())
                {
                    if (file.Name.StartsWith("RE7_AP_Client"))
                        file.Delete();
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Error removing AP Client files: " + e.Message);
            }

            // Remove AP Mod related files
            try
            {
                DirectoryInfo re7Root = new DirectoryInfo(re7Path);
                foreach (FileInfo file in re7Root.GetFiles())
                {
                    foreach (string fileToDelete in filesToDeleteOnClearAPMods)
                    {
                        if (file.Name.Equals(fileToDelete, System.StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                file.Delete();
                                UnityEngine.Debug.Log("Deleted AP mod file: " + file.Name);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Error removing AP mod files: " + e.Message);
            }

            // Remove natives directory
            SafeDeleteDirectory(Path.Combine(re7Path, "natives"));

            // Replace reframework with fresh version from remoteconfig
            try
            {
                StartCoroutine(ReplaceReFrameworkWithFresh());
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not replace reframework: " + e.Message);
            }

            DeleteOldVersionFiles();

            ShowInfo("AP Client removed successfully!");
            return;
        }

        // Full clean - remove all framework files, reframework, natives, and specific files
        if (fullClean)
        {
            CleanupProcesses();

            ShowInfo("Removing all Framework files...");

            try
            {
                // Remove specific files from the list
                DirectoryInfo re7Root = new DirectoryInfo(re7Path);
                foreach (FileInfo file in re7Root.GetFiles())
                {
                    // Check if file is in protected list
                    bool isProtected = false;
                    foreach (string protectedFile in protectedDlls)
                    {
                        if (file.Name.Equals(protectedFile, System.StringComparison.OrdinalIgnoreCase))
                        {
                            isProtected = true;
                            break;
                        }
                    }

                    if (isProtected)
                        continue;

                    // Remove AP Client files
                    if (file.Name.StartsWith("RE7_AP_Client"))
                    {
                        try
                        {
                            file.Delete();
                            UnityEngine.Debug.Log("Deleted: " + file.Name);
                        }
                        catch { }
                        continue;
                    }

                    // Remove specific files from the whitelist
                    bool isWhitelistedFile = false;
                    foreach (string fileToDelete in filesToDeleteOnFullClean)
                    {
                        if (file.Name.Equals(fileToDelete, System.StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                file.Delete();
                                UnityEngine.Debug.Log("Deleted: " + file.Name);
                            }
                            catch { }
                            isWhitelistedFile = true;
                            break;
                        }
                    }

                    if (isWhitelistedFile)
                        continue;

                    // Remove framework DLLs only
                    if (file.Name.EndsWith(".dll"))
                    {
                        try
                        {
                            file.Delete();
                            UnityEngine.Debug.Log("Deleted: " + file.Name);
                        }
                        catch { }
                    }
                }

                // Remove reframework directory
                SafeDeleteDirectory(Path.Combine(re7Path, "reframework"));

                // Remove natives directory
                SafeDeleteDirectory(Path.Combine(re7Path, "natives"));

                DeleteOldVersionFiles();
                SafeDeleteDirectory(frameworkBackupPath);
                SafeDeleteDirectory(refprameworkBackupPath);

                ShowInfo("Full clean completed!");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error during full clean: " + e.Message);
                ShowInfo("ERROR: Full clean failed\n" + e.Message);
            }

            return;
        }

        ShowInfo("Revert completed!");
    }

    IEnumerator ReplaceReFrameworkWithFresh()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        string extractPath = Path.Combine(Application.persistentDataPath, "RE7FrameworkReplace");

        yield return downloader.DownloadAndExtract(re7Framework, Application.persistentDataPath, extractPath);

        try
        {
            // Remove current reframework
            string reframeworkPath = Path.Combine(re7Path, "reframework");
            SafeDeleteDirectory(reframeworkPath);

            // Find reframework in extracted files
            string sourceReFramework = FindReFrameworkFolder(extractPath);
            if (!string.IsNullOrEmpty(sourceReFramework))
            {
                // Copy fresh reframework
                string targetReFramework = Path.Combine(re7Path, "reframework");
                MoveDirectory(sourceReFramework, targetReFramework);
                UnityEngine.Debug.Log("reframework replaced with fresh version from remote config");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Could not find reframework in downloaded framework");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error replacing reframework: " + e.Message);
        }
        finally
        {
            SafeDeleteDirectory(extractPath);
        }
    }

    IEnumerator InstallFlow()
    {
        if (installRe7ApworldToggle == null || installRe7ApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallRE7APWorld();
        }

        if (installRe7FrameworkToggle != null && installRe7FrameworkToggle.isOn)
        {
            ShowInfo("Installing Framework...");
            yield return InstallRE7Framework();
        }

        if (installRe7APToggle != null && installRe7APToggle.isOn)
        {
            ShowInfo("Installing AP Client...");
            yield return InstallRE7AP();
        }

        CreateVersionFile(re7Apworld.url, re7Framework.url, re7AP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching RE7...");
            LaunchRE7();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator InstallRE7APWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. RE7 APWorld URL: " + re7Apworld.url);

        if (string.IsNullOrEmpty(re7Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = re7Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = re7Apworld.url.Substring(re7Apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + re7Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(re7Apworld.url, localPath);

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

    IEnumerator InstallRE7Framework()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "RE7FrameworkTemp");

        // Create backups of current framework and reframework before installing new ones
        CreateFrameworkBackup();
        CreateReFrameworkBackup();

        yield return downloader.DownloadAndExtract(re7Framework, Application.persistentDataPath, extractPath);

        // Extract everything from the zip to RE7 root directory
        MoveDirectory(extractPath, re7Path);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallRE7AP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "RE7APTemp");

        yield return downloader.DownloadAndExtract(re7AP, Application.persistentDataPath, extractPath);

        // Find the RE7_AP_Client folder
        string re7APClientFolder = FindRE7APClientFolder(extractPath);

        if (string.IsNullOrEmpty(re7APClientFolder))
        {
            UnityEngine.Debug.LogError("RE7_AP_Client folder not found in package!");
            ShowInfo("ERROR: RE7_AP_Client folder not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Copy everything from RE7_AP_Client to RE7 root directory
        try
        {
            MoveDirectory(re7APClientFolder, re7Path);
            UnityEngine.Debug.Log("RE7 AP Client installed to: " + re7Path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install RE7 AP Client: " + e.Message);
            ShowInfo("ERROR: Failed to install RE7 AP Client\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator ApWorldOnlyFlow()
    {
        re7Path = GetRE7Path();

        if (string.IsNullOrEmpty(re7Path))
            yield break;

        yield return InstallRE7APWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchRE7();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator FrameworkOnlyFlow()
    {
        ShowInfo("Installing Framework...");
        CreateFrameworkBackup();
        CreateReFrameworkBackup();
        yield return InstallRE7Framework();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching RE7...");
            LaunchRE7();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator RE7APOnlyFlow()
    {
        re7Path = GetRE7Path();

        if (string.IsNullOrEmpty(re7Path))
            yield break;

        ShowInfo("Installing AP Client...");
        yield return InstallRE7AP();

        CreateVersionFile(re7Apworld.url, re7Framework.url, re7AP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchRE7();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<RE7Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyRE7Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchRE7()
    {
        string exePath = Path.Combine(re7Path, "RE7.exe");

        if (File.Exists(exePath))
            re7Process = Process.Start(exePath);
    }

    void CloseRE7()
    {
        try
        {
            if (re7Process != null && !re7Process.HasExited)
            {
                re7Process.Kill();
                re7Process.Dispose();
                re7Process = null;
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

    void CreateFrameworkBackup()
    {
        try
        {
            SafeDeleteDirectory(frameworkBackupPath);
            Directory.CreateDirectory(frameworkBackupPath);

            if (Directory.Exists(re7Path))
            {
                foreach (string file in Directory.GetFiles(re7Path))
                {
                    // Backup framework files (DLLs, EXEs except RE7.exe, etc)
                    string fileName = Path.GetFileName(file);
                    if ((file.EndsWith(".dll") || file.EndsWith(".exe")) && !fileName.Equals("RE7.exe"))
                    {
                        string backupFile = Path.Combine(frameworkBackupPath, fileName);
                        File.Copy(file, backupFile, true);
                    }
                }
            }

            UnityEngine.Debug.Log("Framework backup created");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not create framework backup: " + e.Message);
        }
    }

    void CreateReFrameworkBackup()
    {
        try
        {
            SafeDeleteDirectory(refprameworkBackupPath);
            Directory.CreateDirectory(refprameworkBackupPath);

            string reframeworkPath = Path.Combine(re7Path, "reframework");
            if (Directory.Exists(reframeworkPath))
            {
                // Copy entire reframework directory structure
                foreach (string file in Directory.GetFiles(reframeworkPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = file.Substring(reframeworkPath.Length + 1);
                    string backupFile = Path.Combine(refprameworkBackupPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
                    File.Copy(file, backupFile, true);
                }
            }

            UnityEngine.Debug.Log("reframework backup created");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not create reframework backup: " + e.Message);
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

    string FindRE7APClientFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name.StartsWith("RE7_AP_Client"))
                return dir;
        }

        return "";
    }

    string FindReFrameworkFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name.Equals("reframework", System.StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        return "";
    }

    void CreateVersionFile(string apworldUrl, string frameworkUrl, string re7ApUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"([^/]+)\.apworld");
            string frameworkVersion = ExtractVersionFromUrl(frameworkUrl, @"/releases/download/([^/]+)/");
            string re7ApVersion = ExtractVersionFromUrl(re7ApUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "RE7_AP_Version_" + re7ApVersion + ".txt";
            string content = "RE7 Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== RE7 APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== RE7 FRAMEWORK ===\n";
            content += "Downloaded from: " + frameworkUrl + "\n";
            content += "Version: " + frameworkVersion + "\n";
            content += "\n";
            content += "=== RE7 AP CLIENT ===\n";
            content += "Downloaded from: " + re7ApUrl + "\n";
            content += "Version: " + re7ApVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            if (Directory.Exists(re7Path))
            {
                string versionFilePath = Path.Combine(re7Path, versionFileName);
                File.WriteAllText(versionFilePath, content);
                UnityEngine.Debug.Log("Version file created: " + versionFilePath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"RE7_AP_Version_.+\.txt");

            if (Directory.Exists(re7Path))
            {
                string[] files = Directory.GetFiles(re7Path);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file: " + e.Message);
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

    string GetRE7Path()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "RESIDENT EVIL 7 biohazard"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "RESIDENT EVIL 7 biohazard"),
            @"D:\Steam\steamapps\common\RESIDENT EVIL 7 biohazard",
            @"D:\SteamLibrary\steamapps\common\RESIDENT EVIL 7 biohazard",
            @"D:\steamapps\common\RESIDENT EVIL 7 biohazard",
            @"E:\Steam\steamapps\common\RESIDENT EVIL 7 biohazard",
            @"E:\SteamLibrary\steamapps\common\RESIDENT EVIL 7 biohazard",
            @"E:\steamapps\common\RESIDENT EVIL 7 biohazard",
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
                    // Search Steam\steamapps
                    string re7Path = Path.Combine(drive.Name, "Steam", "steamapps", "common", "RESIDENT EVIL 7 biohazard");
                    if (Directory.Exists(re7Path))
                        return re7Path;

                    // Search SteamLibrary\steamapps
                    re7Path = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "RESIDENT EVIL 7 biohazard");
                    if (Directory.Exists(re7Path))
                        return re7Path;

                    // Search steamapps at root
                    re7Path = Path.Combine(drive.Name, "steamapps", "common", "RESIDENT EVIL 7 biohazard");
                    if (Directory.Exists(re7Path))
                        return re7Path;

                    // Search Program Files (x86)\steamapps
                    re7Path = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "RESIDENT EVIL 7 biohazard");
                    if (Directory.Exists(re7Path))
                        return re7Path;

                    // Search Program Files\steamapps
                    re7Path = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "RESIDENT EVIL 7 biohazard");
                    if (Directory.Exists(re7Path))
                        return re7Path;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
