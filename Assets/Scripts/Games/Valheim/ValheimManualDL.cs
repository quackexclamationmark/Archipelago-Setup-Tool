using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

public class ValheimManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("VALHEIM FILES")]
    public FileDownloader.FileData valheimBepInEx;
    public FileDownloader.FileData valheimAP;
    public FileDownloader.FileData valheimApworld;
    public FileDownloader.FileData valheimJotunn;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Valheim";

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;
    public Toggle installAPWorldToggle;
    public Toggle installJotunnToggle;

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

    private Process valheimProcess;
    private string valheimPath;
    private string pendingAction;
    private ValheimConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class ValheimConfig
    {
        public string valheimAP;
        public string valheimApworld;
        public string valheimBepInEx;
        public string valheimJotunn;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        valheimPath = GetValheimPath();
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

    void ApplyValheimConfig()
    {
        if (remoteConfig == null)
            return;

        valheimBepInEx.url = remoteConfig.valheimBepInEx;
        valheimBepInEx.fileName = "BepInEx.zip";

        valheimAP.url = remoteConfig.valheimAP;
        valheimAP.fileName = "ValheimRandomizer.zip";

        valheimApworld.url = remoteConfig.valheimApworld;
        valheimApworld.fileName = "valheim.apworld";

        valheimJotunn.url = remoteConfig.valheimJotunn;
        valheimJotunn.fileName = "Jotunn.zip";
    }

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
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        valheimPath = GetValheimPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installArchipelagoToggle != null && installArchipelagoToggle.isOn;
        bool jotunn = installJotunnToggle != null && installJotunnToggle.isOn;
        bool needsGamePath = bep || apmod || jotunn;

        if (needsGamePath && (string.IsNullOrEmpty(valheimPath) || !Directory.Exists(valheimPath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (jotunn ? 1 : 0) +
            (apmod ? 1 : 0);

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

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchValheim();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        valheimPath = GetValheimPath();

        if (string.IsNullOrEmpty(valheimPath))
            return;

        string pluginsPath = Path.Combine(valheimPath, "BepInEx", "plugins");

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

            ShowInfo("Removing AP mods...");

            if (Directory.Exists(pluginsPath))
            {
                SafeDeleteDirectory(Path.Combine(pluginsPath, "ValheimRandomizer"));
                SafeDeleteDirectory(Path.Combine(pluginsPath, "Jotunn"));
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(valheimPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(valheimPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(valheimPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(valheimPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(valheimPath, ".doorstop_version"));

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
            ShowInfo("Installing Valheim Randomizer Mod...");
            yield return InstallValheimAP();
        }

        if (installJotunnToggle != null && installJotunnToggle.isOn)
        {
            ShowInfo("Installing Jotunn...");
            yield return InstallJotunn();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(valheimBepInEx.url, valheimAP.url, valheimApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching Valheim...");
            LaunchValheim();
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

        yield return downloader.DownloadAndExtract(valheimBepInEx, Application.persistentDataPath, extractPath);

        string bepInExSourcePath = Path.Combine(extractPath, "BepInEx");
        string bepInExTargetPath = Path.Combine(valheimPath, "BepInEx");

        if (Directory.Exists(bepInExSourcePath))
        {
            if (Directory.Exists(bepInExTargetPath))
                SafeDeleteDirectory(bepInExTargetPath);

            MoveDirectory(bepInExSourcePath, bepInExTargetPath);
        }

        string[] dllFiles = Directory.GetFiles(extractPath, "*.dll");
        foreach (string dll in dllFiles)
        {
            string targetDll = Path.Combine(valheimPath, Path.GetFileName(dll));
            File.Copy(dll, targetDll, true);
        }

        string[] iniFiles = Directory.GetFiles(extractPath, "*.ini");
        foreach (string ini in iniFiles)
        {
            string targetIni = Path.Combine(valheimPath, Path.GetFileName(ini));
            File.Copy(ini, targetIni, true);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallValheimAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ValheimAPTemp");
        yield return downloader.DownloadAndExtract(valheimAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(valheimPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string apSourcePath = Path.Combine(extractPath, "BepInEx", "plugins", "ValheimRandomizer");

        if (Directory.Exists(apSourcePath))
        {
            string targetPluginPath = Path.Combine(pluginsPath, "ValheimRandomizer");

            if (Directory.Exists(targetPluginPath))
                SafeDeleteDirectory(targetPluginPath);

            CopyDirectory(apSourcePath, targetPluginPath);
            UnityEngine.Debug.Log("Copied ValheimRandomizer to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("BepInEx/plugins/ValheimRandomizer folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallJotunn()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "JotunnTemp");
        yield return downloader.DownloadAndExtract(valheimJotunn, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(valheimPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string jotunnSourcePath = Path.Combine(extractPath, "plugins");

        if (Directory.Exists(jotunnSourcePath))
        {
            string targetPluginPath = Path.Combine(pluginsPath, "Jotunn");

            if (Directory.Exists(targetPluginPath))
                SafeDeleteDirectory(targetPluginPath);

            CopyDirectory(jotunnSourcePath, targetPluginPath);
            UnityEngine.Debug.Log("Copied Jotunn contents to plugins/Jotunn");
        }
        else
        {
            UnityEngine.Debug.LogWarning("plugins folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + valheimApworld.url);

        if (string.IsNullOrEmpty(valheimApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = valheimApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = valheimApworld.url.Substring(valheimApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + valheimApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(valheimApworld.url, localPath);

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
            remoteConfig = JsonUtility.FromJson<ValheimConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyValheimConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        valheimPath = GetValheimPath();
    }

    void LaunchValheim()
    {
        string exePath = Path.Combine(valheimPath, "valheim.exe");

        if (File.Exists(exePath))
        {
            try
            {
                valheimProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Valheim launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Valheim:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseValheim()
    {
        try
        {
            if (valheimProcess != null && !valheimProcess.HasExited)
            {
                valheimProcess.Kill();
                valheimProcess.Dispose();
                valheimProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseValheim();
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

    string GetValheimPath()
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

    void CreateVersionFile(string bepinexUrl, string valheimApUrl, string apWorldUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string valheimApVersion = ExtractVersionFromUrl(valheimApUrl);
            string apWorldVersion = ExtractVersionFromUrl(apWorldUrl);

            string versionFileName = "Valheim Archipelago Version " + valheimApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== VALHEIM ARCHIPELAGO ===\n";
            content += "Downloaded from: " + valheimApUrl + "\n";
            content += "Version: " + valheimApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apWorldUrl + "\n";
            content += "Version: " + apWorldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(valheimPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(valheimPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Valheim Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(valheimPath);
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

            string pluginsPath = Path.Combine(valheimPath, "BepInEx", "plugins");
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