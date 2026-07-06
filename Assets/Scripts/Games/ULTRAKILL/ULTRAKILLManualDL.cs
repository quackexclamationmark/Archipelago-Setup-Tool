using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

public class ULTRAKILLManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ULTRAKILL FILES")]
    public FileDownloader.FileData ultrakillBepInEx;
    public FileDownloader.FileData ultrakillAP;
    public FileDownloader.FileData ultrakillAPWorld;
    public FileDownloader.FileData ultrakillPluginConfigurator;

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;
    public Toggle installAPWorldToggle;
    public Toggle installPluginConfiguratorToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanBepInExToggle;
    public Toggle removeAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process ultrakillProcess;
    private string ultrakillPath;
    private string pendingAction;
    private ULTRAKILLConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class ULTRAKILLConfig
    {
        public string ultrakillAP;
        public string ultrakillApworld;
        public string ultrakillBepInEx;
        public string ultrakillPluginConfigurator;
    }

    void Start()
    {
        ultrakillPath = GetULTRAKILLPath();
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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    void ApplyULTRAKILLConfig()
    {
        if (remoteConfig == null)
            return;

        ultrakillBepInEx.url = remoteConfig.ultrakillBepInEx;
        ultrakillBepInEx.fileName = "BepInEx.zip";

        ultrakillAP.url = remoteConfig.ultrakillAP;
        ultrakillAP.fileName = "ArchipelagoULTRAKILL.zip";

        ultrakillAPWorld.url = remoteConfig.ultrakillApworld;
        ultrakillAPWorld.fileName = "ultrakill.apworld";

        ultrakillPluginConfigurator.url = remoteConfig.ultrakillPluginConfigurator;
        ultrakillPluginConfigurator.fileName = "PluginConfigurator.zip";
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(ultrakillPath))
        {
            ShowInfo("ULTRAKILL path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        ultrakillPath = GetULTRAKILLPath();

        if (string.IsNullOrEmpty(ultrakillPath))
            return;

        string pluginsPath = Path.Combine(ultrakillPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(pluginsPath))
                return;

            ShowInfo("Removing AP mods...");

            // Remove all AP-related folders
            string[] pluginDirs = Directory.GetDirectories(pluginsPath);
            foreach (string dir in pluginDirs)
            {
                string dirName = Path.GetFileName(dir);
                // Only remove AP-related folders
                if (dirName.ToLower().Contains("archipelago") || dirName.ToLower().Contains("ap") || dirName.ToLower().Contains("trpg"))
                {
                    SafeDeleteDirectory(dir);
                }
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // Full clean
        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(ultrakillPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(ultrakillPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(ultrakillPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(ultrakillPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(ultrakillPath, ".doorstop_version"));

        DeleteOldVersionFiles();

        ShowInfo("Full clean completed!");
    }

    IEnumerator InstallFlow()
    {
        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing ULTRAKILL Archipelago Mod...");
            yield return InstallULTRAKILLAP();
        }

        if (installPluginConfiguratorToggle != null && installPluginConfiguratorToggle.isOn)
        {
            ShowInfo("Installing Plugin Configurator...");
            yield return InstallPluginConfigurator();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(ultrakillBepInEx.url, ultrakillAP.url, ultrakillAPWorld.url);

        ShowInfo("Launching ULTRAKILL...");
        LaunchULTRAKILL();

        yield return new WaitForSeconds(2f);

        CloseULTRAKILL();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching ULTRAKILL...");
            LaunchULTRAKILL();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(ultrakillBepInEx, Application.persistentDataPath, extractPath);

        // Extract all content from the .zip into the ULTRAKILL root directory
        string bepInExSourcePath = Path.Combine(extractPath, "BepInEx");
        string bepInExTargetPath = Path.Combine(ultrakillPath, "BepInEx");

        if (Directory.Exists(bepInExSourcePath))
        {
            if (Directory.Exists(bepInExTargetPath))
                SafeDeleteDirectory(bepInExTargetPath);

            MoveDirectory(bepInExSourcePath, bepInExTargetPath);
        }

        // Copy .dll and .ini files
        string[] dllFiles = Directory.GetFiles(extractPath, "*.dll");
        foreach (string dll in dllFiles)
        {
            string targetDll = Path.Combine(ultrakillPath, Path.GetFileName(dll));
            File.Copy(dll, targetDll, true);
        }

        string[] iniFiles = Directory.GetFiles(extractPath, "*.ini");
        foreach (string ini in iniFiles)
        {
            string targetIni = Path.Combine(ultrakillPath, Path.GetFileName(ini));
            File.Copy(ini, targetIni, true);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallULTRAKILLAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ULTRAKILLAPTemp");
        yield return downloader.DownloadAndExtract(ultrakillAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(ultrakillPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Look for TRPG-Archipelago folder in the archive
        string apSourcePath = Path.Combine(extractPath, "TRPG-Archipelago");

        if (Directory.Exists(apSourcePath))
        {
            string targetPluginPath = Path.Combine(pluginsPath, "TRPG-Archipelago");

            if (Directory.Exists(targetPluginPath))
                SafeDeleteDirectory(targetPluginPath);

            CopyDirectory(apSourcePath, targetPluginPath);
            UnityEngine.Debug.Log("Copied ULTRAKILL Archipelago to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("TRPG-Archipelago folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallPluginConfigurator()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "PluginConfiguratorTemp");
        yield return downloader.DownloadAndExtract(ultrakillPluginConfigurator, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(ultrakillPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Look for PluginConfigurator folder in plugins
        string pcSourcePath = Path.Combine(extractPath, "plugins", "PluginConfigurator");

        if (Directory.Exists(pcSourcePath))
        {
            string targetPluginPath = Path.Combine(pluginsPath, "PluginConfigurator");

            if (Directory.Exists(targetPluginPath))
                SafeDeleteDirectory(targetPluginPath);

            CopyDirectory(pcSourcePath, targetPluginPath);
            UnityEngine.Debug.Log("Copied PluginConfigurator to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("PluginConfigurator folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        string fileName = "ultrakill.apworld";

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "APWorldTemp");
        Directory.CreateDirectory(tempDownloadPath);

        yield return downloader.DownloadToFolder(ultrakillAPWorld, tempDownloadPath);

        string[] apWorldFiles = Directory.GetFiles(tempDownloadPath, "*.apworld");

        if (apWorldFiles.Length == 0)
        {
            UnityEngine.Debug.LogWarning("ultrakill.apworld not found in download");
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        string sourceFile = apWorldFiles[0];

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
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        try
        {
            File.Copy(sourceFile, target, true);
            UnityEngine.Debug.Log("APWorld file copied to: " + target);
            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }

        SafeDeleteDirectory(tempDownloadPath);
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
            remoteConfig = JsonUtility.FromJson<ULTRAKILLConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyULTRAKILLConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchULTRAKILL()
    {
        string exePath = Path.Combine(ultrakillPath, "ULTRAKILL.exe");

        if (File.Exists(exePath))
        {
            try
            {
                ultrakillProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("ULTRAKILL launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching ULTRAKILL:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseULTRAKILL()
    {
        try
        {
            if (ultrakillProcess != null && !ultrakillProcess.HasExited)
            {
                ultrakillProcess.Kill();
                ultrakillProcess.Dispose();
                ultrakillProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseULTRAKILL();
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
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        try
        {
            CopyDirectory(source, target);
            SafeDeleteDirectory(source);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error moving directory: " + e.Message);
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

    string GetULTRAKILLPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "ULTRAKILL"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "ULTRAKILL"),
            @"D:\Steam\steamapps\common\ULTRAKILL",
            @"D:\SteamLibrary\steamapps\common\ULTRAKILL",
            @"E:\Steam\steamapps\common\ULTRAKILL",
            @"E:\SteamLibrary\steamapps\common\ULTRAKILL",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found ULTRAKILL at: " + path);
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
                    string ukPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "ULTRAKILL");
                    if (Directory.Exists(ukPath))
                        return ukPath;

                    ukPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "ULTRAKILL");
                    if (Directory.Exists(ukPath))
                        return ukPath;

                    ukPath = Path.Combine(drive.Name, "steamapps", "common", "ULTRAKILL");
                    if (Directory.Exists(ukPath))
                        return ukPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("ULTRAKILL not found.");
        return "";
    }

    void CreateVersionFile(string bepinexUrl, string ultrakillApUrl, string apWorldUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string ultrakillApVersion = ExtractVersionFromUrl(ultrakillApUrl);
            string apWorldVersion = ExtractVersionFromUrl(apWorldUrl);

            string versionFileName = "ULTRAKILL Archipelago Version " + ultrakillApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== ULTRAKILL ARCHIPELAGO ===\n";
            content += "Downloaded from: " + ultrakillApUrl + "\n";
            content += "Version: " + ultrakillApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apWorldUrl + "\n";
            content += "Version: " + apWorldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(ultrakillPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(ultrakillPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ULTRAKILL Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(ultrakillPath);
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

            string pluginsPath = Path.Combine(ultrakillPath, "BepInEx", "plugins");
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
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }
}
