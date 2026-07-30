using Microsoft.Win32;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CloverPitManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CLOVERPIT FILES")]
    public FileDownloader.FileData cloverPitBepInEx;
    public FileDownloader.FileData cloverPitAP;
    public FileDownloader.FileData cloverPitAPWorld;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "CloverPit";

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;
    public Toggle installAPWorldToggle;

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

    private Process cloverPitProcess;
    private string cloverPitPath;
    private string pendingAction;
    private RoR2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class RoR2Config
    {
        public string cloverpitBepInEx;
        public string cloverpitAP;
        public string cloverpitApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        cloverPitPath = GetCloverPitPath();
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

    void ApplyCloverPitConfig()
    {
        if (remoteConfig == null)
            return;

        cloverPitBepInEx.url = remoteConfig.cloverpitBepInEx;
        cloverPitBepInEx.fileName = "BepInEx.zip";

        cloverPitAP.url = remoteConfig.cloverpitAP;
        cloverPitAP.fileName = "CloverPit-AP.zip";

        cloverPitAPWorld.url = remoteConfig.cloverpitApworld;
        cloverPitAPWorld.fileName = "cloverpit.apworld";
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
        cloverPitPath = GetCloverPitPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ap = installArchipelagoToggle == null || installArchipelagoToggle.isOn;
        bool bep = installBepInExToggle == null || installBepInExToggle.isOn;
        bool needsGamePath = ap || bep;

        if (needsGamePath && (string.IsNullOrEmpty(cloverPitPath) || !Directory.Exists(cloverPitPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (bep ? 1 : 0) + (ap ? 1 : 0);

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

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching CloverPit...");
            LaunchCloverPit();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        cloverPitPath = GetCloverPitPath();

        if (string.IsNullOrEmpty(cloverPitPath))
            return;

        string pluginsPath = Path.Combine(cloverPitPath, "BepInEx", "plugins");

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

            string[] pluginDirs = Directory.GetDirectories(pluginsPath);
            foreach (string dir in pluginDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.ToLower().Contains("archipelago") || dirName.ToLower().Contains("ap"))
                {
                    SafeDeleteDirectory(dir);
                }
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(cloverPitPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(cloverPitPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(cloverPitPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(cloverPitPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(cloverPitPath, ".doorstop_version"));

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
            ShowInfo("Installing CloverPit Archipelago Mod...");
            yield return InstallCloverPitAP();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing cloverpit.apworld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(cloverPitBepInEx.url, cloverPitAP.url, cloverPitAPWorld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching CloverPit...");
            LaunchCloverPit();
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

        yield return downloader.DownloadAndExtract(cloverPitBepInEx, Application.persistentDataPath, extractPath);

        // Extraire tout le contenu du .zip dans le dossier racine de CloverPit
        string bepInExSourcePath = Path.Combine(extractPath, "BepInEx");
        string bepInExTargetPath = Path.Combine(cloverPitPath, "BepInEx");

        if (Directory.Exists(bepInExSourcePath))
        {
            if (Directory.Exists(bepInExTargetPath))
                SafeDeleteDirectory(bepInExTargetPath);

            MoveDirectory(bepInExSourcePath, bepInExTargetPath);
        }

        // Copier les fichiers .dll et .ini
        string[] dllFiles = Directory.GetFiles(extractPath, "*.dll");
        foreach (string dll in dllFiles)
        {
            string targetDll = Path.Combine(cloverPitPath, Path.GetFileName(dll));
            File.Copy(dll, targetDll, true);
        }

        string[] iniFiles = Directory.GetFiles(extractPath, "*.ini");
        foreach (string ini in iniFiles)
        {
            string targetIni = Path.Combine(cloverPitPath, Path.GetFileName(ini));
            File.Copy(ini, targetIni, true);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallCloverPitAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "CloverPitAPTemp");
        yield return downloader.DownloadAndExtract(cloverPitAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(cloverPitPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string apPluginsSourcePath = Path.Combine(extractPath, "BepInEx", "Plugins");

        if (Directory.Exists(apPluginsSourcePath))
        {
            string[] pluginDirs = Directory.GetDirectories(apPluginsSourcePath);
            foreach (string pluginDir in pluginDirs)
            {
                string pluginName = Path.GetFileName(pluginDir);
                string targetPluginPath = Path.Combine(pluginsPath, pluginName);

                if (Directory.Exists(targetPluginPath))
                    SafeDeleteDirectory(targetPluginPath);

                CopyDirectory(pluginDir, targetPluginPath);
            }

            // Copier aussi les fichiers DLL directs s'il y en a
            string[] pluginDlls = Directory.GetFiles(apPluginsSourcePath, "*.dll");
            foreach (string dll in pluginDlls)
            {
                string targetDll = Path.Combine(pluginsPath, Path.GetFileName(dll));
                File.Copy(dll, targetDll, true);
            }

            UnityEngine.Debug.Log("Copied AP mods to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("BepInEx\\Plugins folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        string fileName = "cloverpit.apworld";

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "APWorldTemp");
        Directory.CreateDirectory(tempDownloadPath);

        yield return downloader.DownloadToFolder(cloverPitAPWorld, tempDownloadPath);

        string[] apWorldFiles = Directory.GetFiles(tempDownloadPath, "*.apworld");

        if (apWorldFiles.Length == 0)
        {
            UnityEngine.Debug.LogWarning("cloverpit.apworld not found in download");
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
            remoteConfig = JsonUtility.FromJson<RoR2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyCloverPitConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        cloverPitPath = GetCloverPitPath();
    }

    void LaunchCloverPit()
    {
        string exePath = Path.Combine(cloverPitPath, "CloverPit.exe");

        if (File.Exists(exePath))
        {
            try
            {
                cloverPitProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("CloverPit launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching CloverPit:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseCloverPit()
    {
        try
        {
            if (cloverPitProcess != null && !cloverPitProcess.HasExited)
            {
                cloverPitProcess.Kill();
                cloverPitProcess.Dispose();
                cloverPitProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseCloverPit();
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

    string GetCloverPitPath()
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

    void CreateVersionFile(string bepinexUrl, string cloverPitApUrl, string apWorldUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string cloverPitApVersion = ExtractVersionFromUrl(cloverPitApUrl);
            string apWorldVersion = ExtractVersionFromUrl(apWorldUrl);

            string versionFileName = "CloverPit Archipelago Version " + cloverPitApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== CLOVERPIT ARCHIPELAGO ===\n";
            content += "Downloaded from: " + cloverPitApUrl + "\n";
            content += "Version: " + cloverPitApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apWorldUrl + "\n";
            content += "Version: " + apWorldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(cloverPitPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(cloverPitPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"CloverPit Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(cloverPitPath);
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

            string pluginsPath = Path.Combine(cloverPitPath, "BepInEx", "plugins");
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
