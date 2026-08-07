using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

public class BombRushManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("BOMB RUSH FILES")]
    public FileDownloader.FileData bombRushBepInEx;
    public FileDownloader.FileData bombRushAP;
    public FileDownloader.FileData bombRushModLocalizer;
    public FileDownloader.FileData bombRushMoreMap;
    public FileDownloader.FileData bombRushFasterLoadTimes;
    public FileDownloader.FileData bombRushCutsceneSkip;
    public FileDownloader.FileData bombRushGimmeMyBoost;
    public FileDownloader.FileData bombRushDisableAnnoyingCutscenes;
    public FileDownloader.FileData bombRushFastTravel;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "BombRushCyberfunk";

    [Header("FEATURE TOGGLES")]
    public Toggle installBepInExToggle;
    public Toggle installArchipelagoToggle;
    public Toggle installModLocalizerToggle;
    public Toggle installMoreMapToggle;
    public Toggle installFasterLoadTimesToggle;
    public Toggle installCutsceneSkipToggle;
    public Toggle installGimmeMyBoostToggle;
    public Toggle installDisableAnnoyingCutscenesToggle;
    public Toggle installFastTravelToggle;

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

    private Process bombRushProcess;
    private string bombRushPath;
    private string pendingAction;
    private BRCConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class BRCConfig
    {
        public string bombrushBepInEx;
        public string bombrushAP;
        public string bombrushModLocalizer;
        public string bombrushMoreMap;
        public string bombrushFasterLoadTimes;
        public string bombrushCutsceneSkip;
        public string bombrushGimmeMyBoost;
        public string bombrushDisableAnnoyingCutscenes;
        public string bombrushFastTravel;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        bombRushPath = GetBombRushPath();
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

    void ApplyBombRushConfig()
    {
        if (remoteConfig == null)
            return;

        bombRushBepInEx.url = remoteConfig.bombrushBepInEx;
        bombRushBepInEx.fileName = "BepInEx.zip";

        bombRushAP.url = remoteConfig.bombrushAP;
        bombRushAP.fileName = "BRC-Archipelago.zip";

        bombRushModLocalizer.url = remoteConfig.bombrushModLocalizer;
        bombRushModLocalizer.fileName = "ModLocalizer.dll";

        bombRushMoreMap.url = remoteConfig.bombrushMoreMap;
        bombRushMoreMap.fileName = "MoreMap-1.1.0.zip";

        bombRushFasterLoadTimes.url = remoteConfig.bombrushFasterLoadTimes;
        bombRushFasterLoadTimes.fileName = "cspotcode-FasterLoadTimes-0.0.1.zip";

        bombRushCutsceneSkip.url = remoteConfig.bombrushCutsceneSkip;
        bombRushCutsceneSkip.fileName = "CutsceneSkip.dll";

        bombRushGimmeMyBoost.url = remoteConfig.bombrushGimmeMyBoost;
        bombRushGimmeMyBoost.fileName = "Yuri-GimmeMyBoost-1.0.0.zip";

        bombRushDisableAnnoyingCutscenes.url = remoteConfig.bombrushDisableAnnoyingCutscenes;
        bombRushDisableAnnoyingCutscenes.fileName = "viliger-DisableAnnoyingCutscenes-0.7.0.zip";

        bombRushFastTravel.url = remoteConfig.bombrushFastTravel;
        bombRushFastTravel.fileName = "tari-FastTravel-1.0.1.zip";
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
        bombRushPath = GetBombRushPath();

        if (string.IsNullOrEmpty(bombRushPath))
        {
            ShowInfo("Bomb Rush Cyberfunk path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        bombRushPath = GetBombRushPath();

        if (string.IsNullOrEmpty(bombRushPath))
            return;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, "TRPG-Archipelago"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "FasterLoadTimes"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "CutsceneSkip"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "GimmeMyBoost"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "DisableAnnoyingCutscenes"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "FastTravel"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        // Full clean
        CleanupProcesses();

        ShowInfo("Cleaning BepInEx...");

        SafeDeleteDirectory(Path.Combine(bombRushPath, "BepInEx"));
        SafeDeleteFile(Path.Combine(bombRushPath, "winhttp.dll"));
        SafeDeleteFile(Path.Combine(bombRushPath, "changelog.txt"));
        SafeDeleteFile(Path.Combine(bombRushPath, "doorstop_config.ini"));
        SafeDeleteFile(Path.Combine(bombRushPath, ".doorstop_version"));

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
            ShowInfo("Installing Bomb Rush Archipelago Mod...");
            yield return InstallBombRushAP();
        }

        if (installModLocalizerToggle != null && installModLocalizerToggle.isOn)
        {
            ShowInfo("Installing ModLocalizer...");
            yield return InstallModLocalizer();
        }

        if (installMoreMapToggle != null && installMoreMapToggle.isOn)
        {
            ShowInfo("Installing MoreMap...");
            yield return InstallMoreMap();
        }

        if (installFasterLoadTimesToggle != null && installFasterLoadTimesToggle.isOn)
        {
            ShowInfo("Installing FasterLoadTimes...");
            yield return InstallFasterLoadTimes();
        }

        if (installCutsceneSkipToggle != null && installCutsceneSkipToggle.isOn)
        {
            ShowInfo("Installing CutsceneSkip...");
            yield return InstallCutsceneSkip();
        }

        if (installGimmeMyBoostToggle != null && installGimmeMyBoostToggle.isOn)
        {
            ShowInfo("Installing GimmeMyBoost...");
            yield return InstallGimmeMyBoost();
        }

        if (installDisableAnnoyingCutscenesToggle != null && installDisableAnnoyingCutscenesToggle.isOn)
        {
            ShowInfo("Installing DisableAnnoyingCutscenes...");
            yield return InstallDisableAnnoyingCutscenes();
        }

        if (installFastTravelToggle != null && installFastTravelToggle.isOn)
        {
            ShowInfo("Installing FastTravel...");
            yield return InstallFastTravel();
        }

        CreateVersionFile(bombRushBepInEx.url, bombRushAP.url, bombRushModLocalizer.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching BRC...");
            LaunchBombRush();
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

        yield return downloader.DownloadAndExtract(bombRushBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, bombRushPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBombRushAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BombRushAPTemp");
        yield return downloader.DownloadAndExtract(bombRushAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // Chercher le dossier "TRPG-Archipelago" à l'intérieur de l'archive
        string[] archiDirs = Directory.GetDirectories(extractPath, "TRPG-Archipelago", SearchOption.AllDirectories);

        if (archiDirs.Length > 0)
        {
            string archipelagoSourcePath = archiDirs[0];
            string archipelagoTargetPath = Path.Combine(pluginsPath, "TRPG-Archipelago");

            if (Directory.Exists(archipelagoTargetPath))
                SafeDeleteDirectory(archipelagoTargetPath);

            CopyDirectory(archipelagoSourcePath, archipelagoTargetPath);
            UnityEngine.Debug.Log("Copied TRPG-Archipelago to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("TRPG-Archipelago folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallModLocalizer()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "ModLocalizerTemp");

        // Utilise DownloadToFolder pour télécharger le .dll
        yield return downloader.DownloadToFolder(bombRushModLocalizer, tempDownloadPath);

        // Cherche le fichier ModLocalizer.dll
        string[] dllFiles = Directory.GetFiles(tempDownloadPath, "ModLocalizer.dll");

        if (dllFiles.Length > 0)
        {
            string targetPath = Path.Combine(pluginsPath, "ModLocalizer.dll");
            File.Copy(dllFiles[0], targetPath, true);
            UnityEngine.Debug.Log("Copied ModLocalizer.dll to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("ModLocalizer.dll not found in download");
        }

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallMoreMap()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "MoreMapTemp");
        yield return downloader.DownloadAndExtract(bombRushMoreMap, Application.persistentDataPath, extractPath);

        // Le contenu du zip est copié directement dans BepInEx/plugins
        MoveDirectory(extractPath, pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallFasterLoadTimes()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        string targetPath = Path.Combine(pluginsPath, "FasterLoadTimes");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "FasterLoadTimesTemp");
        yield return downloader.DownloadAndExtract(bombRushFasterLoadTimes, Application.persistentDataPath, extractPath);

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        // Le contenu du zip est copié dans un nouveau dossier "FasterLoadTimes"
        MoveDirectory(extractPath, targetPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallCutsceneSkip()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        string targetFolder = Path.Combine(pluginsPath, "CutsceneSkip");
        Directory.CreateDirectory(targetFolder);

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "CutsceneSkipTemp");

        yield return downloader.DownloadToFolder(bombRushCutsceneSkip, tempDownloadPath);

        string[] dllFiles = Directory.GetFiles(tempDownloadPath, "CutsceneSkip.dll");

        if (dllFiles.Length > 0)
        {
            string targetPath = Path.Combine(targetFolder, "CutsceneSkip.dll");
            File.Copy(dllFiles[0], targetPath, true);
            UnityEngine.Debug.Log("Copied CutsceneSkip.dll to plugins/CutsceneSkip");
        }
        else
        {
            UnityEngine.Debug.LogWarning("CutsceneSkip.dll not found in download");
        }

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallGimmeMyBoost()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "GimmeMyBoostTemp");
        yield return downloader.DownloadAndExtract(bombRushGimmeMyBoost, Application.persistentDataPath, extractPath);

        // Cherche le dossier "GimmeMyBoost" à l'intérieur de l'archive (peu importe la profondeur)
        string[] boostDirs = Directory.GetDirectories(extractPath, "GimmeMyBoost", SearchOption.AllDirectories);

        if (boostDirs.Length > 0)
        {
            string sourcePath = boostDirs[0];
            string targetPath = Path.Combine(pluginsPath, "GimmeMyBoost");

            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            CopyDirectory(sourcePath, targetPath);
            UnityEngine.Debug.Log("Copied GimmeMyBoost to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("GimmeMyBoost folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallDisableAnnoyingCutscenes()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "DisableAnnoyingCutscenesTemp");
        yield return downloader.DownloadAndExtract(bombRushDisableAnnoyingCutscenes, Application.persistentDataPath, extractPath);

        // Cherche le dossier contenant "DisableAnnoyingCutscenes" à l'intérieur de l'archive (peu importe le préfixe ou la profondeur)
        // Le zip contient un dossier racine (ex: "viliger-DisableAnnoyingCutscenes-0.7.0") ET le vrai dossier du plugin
        // plus profond (ex: "plugins\viliger-DisableAnnoyingCutscenes"), qui matchent tous les deux le pattern.
        // On prend donc le chemin le plus profond, qui correspond au vrai dossier du plugin.
        string[] cutsceneDirs = Directory.GetDirectories(extractPath, "*DisableAnnoyingCutscenes*", SearchOption.AllDirectories);

        if (cutsceneDirs.Length > 0)
        {
            System.Array.Sort(cutsceneDirs, (a, b) => b.Length.CompareTo(a.Length));
            string sourcePath = cutsceneDirs[0];
            string targetPath = Path.Combine(pluginsPath, "DisableAnnoyingCutscenes");

            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            CopyDirectory(sourcePath, targetPath);
            UnityEngine.Debug.Log("Copied DisableAnnoyingCutscenes to plugins");
        }
        else
        {
            UnityEngine.Debug.LogWarning("DisableAnnoyingCutscenes folder not found in archive");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallFastTravel()
    {
        while (!configLoaded)
            yield return null;

        string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
        string targetPath = Path.Combine(pluginsPath, "FastTravel");
        Directory.CreateDirectory(pluginsPath);

        string extractPath = Path.Combine(Application.persistentDataPath, "FastTravelTemp");
        yield return downloader.DownloadAndExtract(bombRushFastTravel, Application.persistentDataPath, extractPath);

        if (Directory.Exists(targetPath))
            SafeDeleteDirectory(targetPath);

        // Le contenu du zip est copié dans un nouveau dossier "FastTravel"
        MoveDirectory(extractPath, targetPath);

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<BRCConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyBombRushConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        bombRushPath = GetBombRushPath();
    }

    void LaunchBombRush()
    {
        string exePath = Path.Combine(bombRushPath, "Bomb Rush Cyberfunk.exe");

        if (File.Exists(exePath))
        {
            try
            {
                bombRushProcess = Process.Start(exePath);
                UnityEngine.Debug.Log("Bomb Rush Cyberfunk launched successfully!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Bomb Rush Cyberfunk:\n" + e.Message);
                UnityEngine.Debug.LogError("Launch error: " + e);
            }
        }
    }

    void CloseBRC()
    {
        try
        {
            if (bombRushProcess != null && !bombRushProcess.HasExited)
            {
                bombRushProcess.Kill();
                bombRushProcess.Dispose();
                bombRushProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseBRC();
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

    string GetBombRushPath()
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

    void CreateVersionFile(string bepinexUrl, string brcapUrl, string modLocalizerUrl)
    {
        try
        {
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string brcapVersion = ExtractVersionFromUrl(brcapUrl);
            string modLocalizerVersion = ExtractVersionFromUrl(modLocalizerUrl);

            string versionFileName = "Bomb Rush Archipelago Version " + brcapVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== MOD LOCALIZER ===\n";
            content += "Downloaded from: " + modLocalizerUrl + "\n";
            content += "Version: " + modLocalizerVersion + "\n";
            content += "\n";
            content += "=== BOMB RUSH ARCHIPELAGO ===\n";
            content += "Downloaded from: " + brcapUrl + "\n";
            content += "Version: " + brcapVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(bombRushPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Bomb Rush Archipelago Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(bombRushPath);
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

            string pluginsPath = Path.Combine(bombRushPath, "BepInEx", "plugins");
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