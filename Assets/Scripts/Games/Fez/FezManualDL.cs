using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.IO.Compression;

public class FEZManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("FEZ FILES")]
    public FileDownloader.FileData fezHAT;
    public FileDownloader.FileData fezAP;
    public FileDownloader.FileData fezApworld;

    [Header("PLATFORMS")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installHATToggle;
    public Toggle installFEZAPToggle;
    public Toggle installAPWorldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchAfterSetupToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearHATToggle;
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

    private Process fezProcess;
    private string fezPath;
    private string pendingAction;
    private FezConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class FezConfig
    {
        public string fezHAT;
        public string fezAP;
        public string fezApworld;
    }

    void Start()
    {
        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullClearHATToggle != null)
            fullClearHATToggle.isOn = false;

        if (fullClearHATToggle != null)
            fullClearHATToggle.onValueChanged.AddListener(OnFullClearHATChanged);

        if (launchAfterSetupToggle != null)
            launchAfterSetupToggle.isOn = false;

        if (infoPanel != null)
            infoPanel.SetActive(false);

        // Default to Steam
        SelectSteam();

        fezPath = GetFezPath();
        StartCoroutine(LoadRemoteConfig());
    }

    // =========================================================
    // PLATFORM SELECTION (Steam / Epic)
    // =========================================================

    void OnEnable()
    {
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);
        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);
    }

    void OnDisable()
    {
        if (steamButton != null)
            steamButton.onClick.RemoveListener(OnSteamButtonClicked);
        if (epicButton != null)
            epicButton.onClick.RemoveListener(OnEpicButtonClicked);
    }

    void OnSteamButtonClicked() => SelectSteam();
    void OnEpicButtonClicked() => SelectEpic();

    void SelectSteam()
    {
        isEpic = false;
        fezPath = GetFezPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + fezPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        fezPath = GetFezPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + fezPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(fezPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullClearHATChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================
    // APPLY REMOTE CONFIG
    // =========================================================

    void ApplyFezConfig()
    {
        if (remoteConfig == null)
            return;

        fezHAT.url = remoteConfig.fezHAT;
        fezAP.url = remoteConfig.fezAP;
        fezApworld.url = remoteConfig.fezApworld;
    }

    // =========================================================
    // CONFIRMATION HANDLING
    // =========================================================

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
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // =========================================================
    // SETUP / REVERT
    // =========================================================

    private void ExecuteSetup()
    {
        fezPath = GetFezPath();

        if (string.IsNullOrEmpty(fezPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("FEZ not found in " + platform + ". Please check installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        fezPath = GetFezPath();

        if (string.IsNullOrEmpty(fezPath))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClear = fullClearHATToggle != null && fullClearHATToggle.isOn;

        string modsPath = Path.Combine(fezPath, "Mods");

        if (!removeAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();
            ShowInfo("Removing AP mod from Mods...");

            if (Directory.Exists(modsPath))
            {
                try
                {
                    // Supprimer le dossier fezap
                    string fezapFolder = Path.Combine(modsPath, "fezap");
                    if (Directory.Exists(fezapFolder))
                    {
                        SafeDeleteDirectory(fezapFolder);
                    }

                    // Supprimer aussi les anciens fichiers .zip pour rétrocompatibilité
                    foreach (string file in Directory.GetFiles(modsPath))
                    {
                        string fileName = Path.GetFileName(file).ToLower();
                        if (fileName.Contains("fezap") || fileName.Contains("ap"))
                        {
                            SafeDeleteFile(file);
                        }
                    }
                }
                catch { }
            }

            // Supprimer aussi les fichiers de version (root + Mods)
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClear)
        {
            CleanupProcesses();
            ShowInfo("Cleaning HAT...");

            SafeDeleteDirectory(Path.Combine(fezPath, "HATDependencies"));

            SafeDeleteFile(Path.Combine(fezPath, "hat_install.bat"));
            SafeDeleteFile(Path.Combine(fezPath, "hat_install.sh"));
            SafeDeleteFile(Path.Combine(fezPath, "FEZ.HAT.mm.dll"));
            SafeDeleteFile(Path.Combine(fezPath, "FEZ.HAT.mm.pdb"));
            SafeDeleteFile(Path.Combine(fezPath, "FEZ.Hooks.mm.dll"));

            // Added requested deletions
            SafeDeleteFile(Path.Combine(fezPath, "HAT.exe"));
            SafeDeleteFile(Path.Combine(fezPath, "HAT.pdb"));
            SafeDeleteFile(Path.Combine(fezPath, "MONOMODDED_FEZ.exe"));
            SafeDeleteFile(Path.Combine(fezPath, "MONOMODDED_FEZ.pdb"));

            // Supprimer aussi les fichiers de version (root + Mods)
            DeleteOldVersionFiles();

            ShowInfo("Full clear HAT completed!");
        }
    }

    // =========================================================
    // INSTALL FLOW
    // =========================================================

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        // refresh path in case it changed
        fezPath = GetFezPath();

        if (installHATToggle == null || installHATToggle.isOn)
        {
            ShowInfo("Installing HAT...");
            yield return InstallHAT();
        }

        if (installFEZAPToggle == null || installFEZAPToggle.isOn)
        {
            ShowInfo("Installing FEZ AP mod into Mods folder...");
            yield return InstallFEZAP();
        }

        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing FEZ .apworld...");
            yield return InstallAPWorld();
        }

        // Create version files after installations
        CreateVersionFile(fezHAT != null ? fezHAT.url : null,
                          fezAP != null ? fezAP.url : null,
                          fezApworld != null ? fezApworld.url : null);

        ShowInfo("Setup complete!");
    }

    IEnumerator InstallHAT()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "FEZHATTemp");

        yield return downloader.DownloadAndExtract(fezHAT, Application.persistentDataPath, extractPath);

        MoveDirectoryContents(extractPath, fezPath);

        SafeDeleteDirectory(extractPath);

        string hatInstallBat = Path.Combine(fezPath, "hat_install.bat");

        if (File.Exists(hatInstallBat))
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = hatInstallBat;
                psi.WorkingDirectory = fezPath;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = true; // keep shell for .bat
                Process.Start(psi);
                UnityEngine.Debug.Log("Started hat_install.bat");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Error starting hat_install.bat: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("hat_install.bat not found in FEZ folder after extracting HAT.");
        }

        string monomoddedExe = Path.Combine(fezPath, "MONOMODDED_FEZ.exe");
        float timer = 0f;
        float timeout = 120f;

        while (!File.Exists(monomoddedExe) && timer < timeout)
        {
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        if (!File.Exists(monomoddedExe))
        {
            UnityEngine.Debug.LogWarning("MONOMODDED_FEZ.exe was not detected after running hat_install.bat.");
            ShowInfo("HAT installation may have failed or taken too long. Check the FEZ folder.");
            yield break;
        }

        UnityEngine.Debug.Log("MONOMODDED_FEZ.exe detected.");

        string modsPath = Path.Combine(fezPath, "Mods");
        Directory.CreateDirectory(modsPath);

        if (!string.IsNullOrEmpty(fezAP != null ? fezAP.url : null))
        {
            string tempApFolder = Path.Combine(Application.persistentDataPath, "FEZAPTemp");
            Directory.CreateDirectory(tempApFolder);

            yield return downloader.DownloadToFolder(fezAP, tempApFolder);

            string[] zips = Directory.GetFiles(tempApFolder, "*.zip");
            if (zips.Length > 0)
            {
                string target = Path.Combine(modsPath, Path.GetFileName(zips[0]));
                try
                {
                    File.Copy(zips[0], target, true);
                    UnityEngine.Debug.Log("Copied FEZ AP zip into Mods: " + target);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Failed to copy FEZ AP zip to Mods: " + e.Message);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("No .zip found in downloaded FEZ AP folder.");
            }

            SafeDeleteDirectory(tempApFolder);
        }

        if (launchAfterSetupToggle != null && launchAfterSetupToggle.isOn)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = monomoddedExe;
                startInfo.WorkingDirectory = fezPath;
                startInfo.UseShellExecute = true; // required to reliably launch the exe on Windows
                startInfo.CreateNoWindow = false;
                Process.Start(startInfo);
                UnityEngine.Debug.Log("Launched MONOMODDED_FEZ.exe");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Error launching MONOMODDED_FEZ.exe: " + e.Message);
            }
        }
    }

    IEnumerator InstallFEZAP()
    {
        while (!configLoaded)
            yield return null;

        // Télécharger fezap.zip dans un dossier temporaire
        string tempApFolder = Path.Combine(Application.persistentDataPath, "FEZAPTemp");
        Directory.CreateDirectory(tempApFolder);

        yield return downloader.DownloadToFolder(fezAP, tempApFolder);

        string[] zips = Directory.GetFiles(tempApFolder, "*.zip");

        string modsPath = Path.Combine(fezPath, "Mods");
        Directory.CreateDirectory(modsPath);

        if (zips.Length > 0)
        {
            // Créer le dossier fezap dans Mods
            string fezapFolder = Path.Combine(modsPath, "fezap");
            Directory.CreateDirectory(fezapFolder);

            // Extraire chaque zip dans le dossier fezap
            foreach (string zip in zips)
            {
                try
                {
                    ZipFile.ExtractToDirectory(zip, fezapFolder, true);
                    UnityEngine.Debug.Log("Extracted FEZ AP zip into Mods/fezap: " + zip);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Failed to extract FEZ AP zip to Mods/fezap: " + e.Message);
                }
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("No .zip found when downloading FEZ AP.");
        }

        SafeDeleteDirectory(tempApFolder);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        string apWorldsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "worlds");
        Directory.CreateDirectory(apWorldsPath);

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "FEZAPWorldTemp");
        Directory.CreateDirectory(tempDownloadPath);

        yield return downloader.DownloadToFolder(fezApworld, tempDownloadPath);

        string[] apWorldFiles = Directory.GetFiles(tempDownloadPath, "*.apworld");

        if (apWorldFiles.Length > 0)
        {
            string targetPath = Path.Combine(apWorldsPath, "fez.apworld");
            try
            {
                File.Copy(apWorldFiles[0], targetPath, true);
                UnityEngine.Debug.Log("Copied fez.apworld to Archipelago worlds");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to copy fez.apworld: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("fez.apworld not found in download");
        }

        SafeDeleteDirectory(tempDownloadPath);
    }

    // =========================================================
    // REMOTE CONFIG LOADER
    // =========================================================

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
            remoteConfig = JsonUtility.FromJson<FezConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyFezConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    // =========================================================
    // LAUNCH / CLOSE / CLEANUP
    // =========================================================

    public void LaunchFEZ()
    {
        string exeName = "MONOMODDED_FEZ.exe";
        string exePath = Path.Combine(fezPath, exeName);

        if (!File.Exists(exePath))
        {
            ShowInfo(exeName + " not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = exePath;
            startInfo.WorkingDirectory = fezPath;
            startInfo.UseShellExecute = true; // required for reliable launch on Windows
            startInfo.CreateNoWindow = false;
            fezProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("FEZ launched successfully!");
        }
        catch (Exception e)
        {
            ShowInfo("Error launching FEZ:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseFEZ()
    {
        try
        {
            if (fezProcess != null && !fezProcess.HasExited)
            {
                fezProcess.Kill();
                fezProcess.Dispose();
                fezProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseFEZ();
    }

    // =========================================================
    // FILE UTILS
    // =========================================================

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

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);

            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(file, destination);
        }
    }

    // Move only contents of a folder (files + subfolders) into target root
    void MoveDirectoryContents(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destDir = Path.Combine(target, rel);
            Directory.CreateDirectory(destDir);
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destFile = Path.Combine(target, rel);

            try
            {
                string destDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                if (File.Exists(destFile))
                    File.Delete(destFile);

                File.Move(file, destFile);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to move file during HAT installation: " + e.Message);
            }
        }
    }

    // =========================================================
    // INFO UI
    // =========================================================

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
    // PATH DETECTION (Steam & Epic)
    // =========================================================

    string GetFezPath()
    {
        if (isEpic)
            return GetFezEpicPath();
        else
            return GetFezSteamPath();
    }

    string GetFezSteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "FEZ"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "FEZ"),
            @"D:\Steam\steamapps\common\FEZ",
            @"D:\SteamLibrary\steamapps\common\FEZ",
            @"E:\Steam\steamapps\common\FEZ",
            @"E:\SteamLibrary\steamapps\common\FEZ",
            @"E:\steamapps\common\FEZ",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found FEZ (Steam) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType != DriveType.Fixed)
                    continue;

                try
                {
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "FEZ");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "FEZ");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "steamapps", "common", "FEZ");
                    if (Directory.Exists(subPath))
                        return subPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("FEZ (Steam) not found.");
        return "";
    }

    string GetFezEpicPath()
    {
        // Use "Fez" folder name for Epic installs (case requested)
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Fez",
            @"D:\Epic Games\Fez",
            @"E:\Epic Games\Fez",
            @"C:\Games\Epic\Fez",
            @"D:\Games\Epic\Fez",
            @"E:\Games\Epic\Fez",
            @"C:\Epic\Fez",
            @"D:\Epic\Fez",
            @"E:\Epic\Fez",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Fez (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            string epicBaseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
            );

            if (Directory.Exists(epicBaseDir))
            {
                string[] manifests = Directory.GetFiles(epicBaseDir, "*.item");
                foreach (string manifest in manifests)
                {
                    try
                    {
                        string content = File.ReadAllText(manifest);
                        if (content.Contains("Fez") || content.Contains("FEZ") || content.Contains("FEZ.exe") || content.Contains("MONOMODDED_FEZ"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Fez (Epic) at: " + epicPath);
                                    return epicPath;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType != DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Fez");
                    if (Directory.Exists(epicPath))
                        return epicPath;

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Fez");
                    if (Directory.Exists(epicPath))
                        return epicPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Fez (Epic) not found.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT (optional)
    // =========================================================

    public void CreateVersionFile(string hatUrl, string apUrl, string apworldUrl)
    {
        try
        {
            // Ensure current path
            fezPath = GetFezPath();
            if (string.IsNullOrEmpty(fezPath))
            {
                UnityEngine.Debug.LogWarning("Fez path empty, cannot create version files.");
                return;
            }

            string hatVersion = ExtractVersionFromUrl(hatUrl);
            string apVersion = ExtractVersionFromUrl(apUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);

            string versionFileName = "FEZ Archipelago Version " + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n\n";
            content += "=== HAT ===\n";
            content += "Downloaded from: " + hatUrl + "\n";
            content += "Version: " + hatVersion + "\n\n";
            content += "=== FEZ AP ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n\n";
            content += "Downloaded at: " + DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(fezPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(fezPath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods: " + modsVersionPath);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"FEZ Archipelago Version .+\.txt");

            if (!string.IsNullOrEmpty(fezPath))
            {
                string[] rootFiles = Directory.GetFiles(fezPath);
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
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in root: " + e.Message);
                        }
                    }
                }

                string modsPath = Path.Combine(fezPath, "Mods");
                if (Directory.Exists(modsPath))
                {
                    string[] modsFiles = Directory.GetFiles(modsPath);
                    foreach (string file in modsFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (pattern.IsMatch(fileName))
                        {
                            try
                            {
                                File.Delete(file);
                                UnityEngine.Debug.Log("Deleted old version file in Mods: " + fileName);
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.LogWarning("Could not delete old version file in Mods: " + e.Message);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"thunderstore\.io/package/download/[^/]+/[^/]+/([^/]+)/?$");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        System.Text.RegularExpressions.Regex fileVersionPattern = new System.Text.RegularExpressions.Regex(@"[-_]?v?(\d+\.\d+\.\d+)");
        System.Text.RegularExpressions.Match fileVersionMatch = fileVersionPattern.Match(url);
        if (fileVersionMatch.Success)
            return fileVersionMatch.Groups[1].Value;

        return "Unknown";
    }
}
