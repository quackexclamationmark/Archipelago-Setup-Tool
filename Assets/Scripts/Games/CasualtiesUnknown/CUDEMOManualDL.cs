using Microsoft.Win32;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CUDEMOManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CUDEMO FILES")]
    public FileDownloader.FileData cudemoBepInEx;
    public FileDownloader.FileData cudemoAP;
    public FileDownloader.FileData cudemoApworld;

    // URL fixe pour l'apworld (pas géré via le remote config)
    private const string CUDEMO_APWORLD_URL = "https://github.com/Nichologeam/CUAP/releases/download/v0.11.0/casualtiesunknown.apworld";

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Casualties Unknown Demo";

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

    private Process cudemoProcess;
    private string cudemoPath;
    private string pendingAction;
    private RoR2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class RoR2Config
    {
        public string cudemoBepInEx;
        public string cudemoAP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        cudemoPath = GetCUDEMOPath();
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

    void ApplyCUDEMOConfig()
    {
        if (remoteConfig == null)
            return;

        cudemoBepInEx.url = remoteConfig.cudemoBepInEx;
        cudemoBepInEx.fileName = "BepInEx.zip";

        cudemoAP.url = remoteConfig.cudemoAP;
        cudemoAP.fileName = "CUAP.zip";

        cudemoApworld.url = CUDEMO_APWORLD_URL;
        cudemoApworld.fileName = "casualtiesunknown.apworld";
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
        cudemoPath = GetCUDEMOPath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ap = installArchipelagoToggle == null || installArchipelagoToggle.isOn;
        bool bep = installBepInExToggle == null || installBepInExToggle.isOn;
        bool needsGamePath = ap || bep;

        if (needsGamePath && (string.IsNullOrEmpty(cudemoPath) || !Directory.Exists(cudemoPath)))
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
            ShowInfo("Launching Casualties Unknown Demo...");
            LaunchCUDEMO();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        cudemoPath = GetCUDEMOPath();

        if (string.IsNullOrEmpty(cudemoPath))
            return;

        string pluginsPath = Path.Combine(cudemoPath, "BepInEx", "plugins");

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

            string cuapPath = Path.Combine(pluginsPath, "CUAP");
            if (Directory.Exists(cuapPath))
            {
                SafeDeleteDirectory(cuapPath);
            }

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(cudemoPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(cudemoPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(cudemoPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(cudemoPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(cudemoPath, ".doorstop_version"));

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
            ShowInfo("Installing Casualties Unknown Archipelago Mod...");
            yield return InstallCUDEMOAP();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing casualtiesunknown.apworld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(cudemoBepInEx.url, cudemoAP.url, cudemoApworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete! Launching Casualties Unknown Demo...");
            LaunchCUDEMO();
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

        yield return downloader.DownloadAndExtract(cudemoBepInEx, Application.persistentDataPath, extractPath);

        // Extraire tout le contenu du .zip dans le dossier racine du jeu
        string bepInExSourcePath = Path.Combine(extractPath, "BepInEx");
        string bepInExTargetPath = Path.Combine(cudemoPath, "BepInEx");

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
            string targetDll = Path.Combine(cudemoPath, Path.GetFileName(dll));
            File.Copy(dll, targetDll, true);
        }

        string[] iniFiles = Directory.GetFiles(extractPath, "*.ini");
        foreach (string ini in iniFiles)
        {
            string targetIni = Path.Combine(cudemoPath, Path.GetFileName(ini));
            File.Copy(ini, targetIni, true);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallCUDEMOAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "CUDEMOAPTemp");
        yield return downloader.DownloadAndExtract(cudemoAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(cudemoPath, "BepInEx", "plugins");

        // Créer le dossier "plugins" s'il n'existe pas
        if (!Directory.Exists(pluginsPath))
            Directory.CreateDirectory(pluginsPath);

        if (Directory.Exists(extractPath))
        {
            // Le contenu du .zip va directement dans BepInEx\plugins
            string[] apDirs = Directory.GetDirectories(extractPath);
            foreach (string apDir in apDirs)
            {
                string dirName = Path.GetFileName(apDir);
                string targetDirPath = Path.Combine(pluginsPath, dirName);

                if (Directory.Exists(targetDirPath))
                    SafeDeleteDirectory(targetDirPath);

                CopyDirectory(apDir, targetDirPath);
            }

            string[] apFiles = Directory.GetFiles(extractPath);
            foreach (string file in apFiles)
            {
                string targetFile = Path.Combine(pluginsPath, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            UnityEngine.Debug.Log("Copied AP mods to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("Extracted CUAP archive not found");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        string fileName = "casualtiesunknown.apworld";

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "APWorldTemp");
        Directory.CreateDirectory(tempDownloadPath);

        yield return downloader.DownloadToFolder(cudemoApworld, tempDownloadPath);

        string[] apWorldFiles = Directory.GetFiles(tempDownloadPath, "*.apworld");

        if (apWorldFiles.Length == 0)
        {
            UnityEngine.Debug.LogWarning("casualtiesunknown.apworld not found in download");
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
            ApplyCUDEMOConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        cudemoPath = GetCUDEMOPath();
    }

    void LaunchCUDEMO()
    {
        string exePath = Path.Combine(cudemoPath, "CasualtiesUnknown.exe");

        if (File.Exists(exePath))
        {
            try
            {
                cudemoProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Casualties Unknown Demo launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Casualties Unknown Demo:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseCUDEMO()
    {
        try
        {
            if (cudemoProcess != null && !cudemoProcess.HasExited)
            {
                cudemoProcess.Kill();
                cudemoProcess.Dispose();
                cudemoProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseCUDEMO();
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

    string GetCUDEMOPath()
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

    void CreateVersionFile(string bepinexUrl, string cudemoApUrl, string apWorldUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string cudemoApVersion = ExtractVersionFromUrl(cudemoApUrl);
            string apWorldVersion = ExtractVersionFromUrl(apWorldUrl);

            string versionFileName = "Casualties Unknown Demo Archipelago Version " + cudemoApVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== CASUALTIES UNKNOWN ARCHIPELAGO ===\n";
            content += "Downloaded from: " + cudemoApUrl + "\n";
            content += "Version: " + cudemoApVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apWorldUrl + "\n";
            content += "Version: " + apWorldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(cudemoPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(cudemoPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Casualties Unknown Demo Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(cudemoPath);
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

            string pluginsPath = Path.Combine(cudemoPath, "BepInEx", "plugins");
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