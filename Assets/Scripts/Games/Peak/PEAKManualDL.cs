using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class PEAKManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("PLATFORM")]
    public PlatformSelection platformSelection;

    [Header("PEAK FILES")]
    public FileDownloader.FileData peakapworld;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData peakAP;
    public FileDownloader.FileData peakVersion;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "PEAK";
    public string steamAppId = "3527290";

    [Header("FEATURE TOGGLES")]
    public Toggle installPeakapworldToggle;
    public Toggle installBepInExToggle;
    public Toggle installPeakAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process peakProcess;
    private string peakPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private PEAKConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class PEAKConfig
    {
        public string peakApworld;
        public string peakBepInEx;
        public string peakBepInExLinux;
        public string peakAP;
        public string peakVersion;
        public string[] steamSearchPaths;
        public string[] steamSearchPathsLinuxHome;
        public string[] steamSearchPathsLinuxExt;
        public string[] apSearchPaths;
    }

    void Start()
    {
        if (platformSelection == null)
            platformSelection = FindFirstObjectByType<PlatformSelection>();

        peakPath = GetPEAKPath();
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

    void CleanupProcesses()
    {
        ClosePEAK();
    }

    void ApplyPEAKConfig()
    {
        if (remoteConfig == null)
            return;

        peakapworld.url = remoteConfig.peakApworld;

        bool isLinux = platformSelection != null && platformSelection.isLinux;
        bepInEx.url = (isLinux && !string.IsNullOrEmpty(remoteConfig.peakBepInExLinux))
            ? remoteConfig.peakBepInExLinux
            : remoteConfig.peakBepInEx;

        peakAP.url = remoteConfig.peakAP;
        peakVersion.url = remoteConfig.peakVersion;
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
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        peakPath = GetPEAKPath();

        bool apworld = installPeakapworldToggle == null || installPeakapworldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool peakmod = installPeakAPToggle != null && installPeakAPToggle.isOn;
        bool needsGamePath = bep || peakmod;

        if (needsGamePath && (string.IsNullOrEmpty(peakPath) || !Directory.Exists(peakPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (peakmod ? 1 : 0);

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

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (peakmod && count == 1)
        {
            StartCoroutine(PEAKAPOnlyFlow());
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
            LaunchPEAK();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        peakPath = GetPEAKPath();

        if (string.IsNullOrEmpty(peakPath))
            return;

        string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, "peakpelago"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteDirectory(Path.Combine(pluginsPath, "peakpelago"));

        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(peakPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(peakPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(peakPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(peakPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(peakPath, ".doorstop_version"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(peakPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(peakPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(peakPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(peakPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(peakPath, ".doorstop_version"));

            ShowInfo("Revert completed!");
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("PEAK APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name == "Archipelago.MultiClient.Net.dll" ||
                name == "Newtonsoft.Json.dll" ||
                name == "YamlDotNet.dll" ||
                name == "PeakArchipelago.dll")
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "PeakArchipelago")
                return true;
        }

        return false;
    }


    IEnumerator InstallFlow()
    {
        if (installPeakapworldToggle == null || installPeakapworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installPeakAPToggle != null && installPeakAPToggle.isOn)
        {
            ShowInfo("Installing PEAK AP Mod...");
            yield return InstallPEAKAP();

            ShowInfo("Installing Version Bypass...");
            yield return InstallPeakVersionBypass();
        }

        CreateVersionFile(peakapworld.url, bepInEx.url, peakAP.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching PEAK...");
            LaunchPEAK();
            yield return new WaitForSeconds(2f);
            ShowInfo("Installation complete!");
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + peakapworld.url);

        if (string.IsNullOrEmpty(peakapworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = peakapworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = peakapworld.url.Substring(peakapworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + peakapworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(peakapworld.url, localPath);

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

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, peakPath);

        SafeDeleteDirectory(extractPath);

        string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsPath))
        {
            Directory.CreateDirectory(pluginsPath);
            UnityEngine.Debug.Log("plugins folder was missing from the archive, created manually at: " + pluginsPath);
        }

        bool isLinux = platformSelection != null && platformSelection.isLinux;
        if (isLinux)
        {
            ChmodExecutable(Path.Combine(peakPath, "run_bepinex.sh"));
        }
    }

    IEnumerator InstallPEAKAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "PEAKAPTemp");

        yield return downloader.DownloadAndExtract(peakAP, Application.persistentDataPath, extractPath);

        if (Directory.Exists(extractPath))
        {
            UnityEngine.Debug.Log("=== PEAKAP extracted content of: " + extractPath + " ===");
            foreach (string f in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
                UnityEngine.Debug.Log("  FILE: " + f);
            foreach (string d in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                UnityEngine.Debug.Log("  DIR:  " + d);
        }
        else
        {
            UnityEngine.Debug.LogError("PEAKAP extractPath does not exist after extraction: " + extractPath);
            ShowInfo("ERROR: PEAK AP Mod extraction failed, folder missing!");
            yield break;
        }

        string peakpelagoFolder = FindPeakpelagoFolder(extractPath);

        UnityEngine.Debug.Log("PEAKAP peakpelagoFolder result: '" + peakpelagoFolder + "'");

        if (string.IsNullOrEmpty(peakpelagoFolder))
        {
            UnityEngine.Debug.LogError("peakpelago folder not found in package!");
            ShowInfo("ERROR: peakpelago folder not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        int filesToMove = Directory.GetFiles(peakpelagoFolder, "*", SearchOption.AllDirectories).Length;
        UnityEngine.Debug.Log("PEAKAP files found to move from peakpelagoFolder: " + filesToMove);

        string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        try
        {
            string targetPath = Path.Combine(pluginsPath, "PeakArchipelago");

            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            MoveDirectory(peakpelagoFolder, targetPath);

            int filesMoved = Directory.Exists(targetPath) ? Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories).Length : 0;
            UnityEngine.Debug.Log("PEAKArchipelago installed to: " + targetPath + " (files present after move: " + filesMoved + ")");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install PEAKArchipelago: " + e.Message);
            ShowInfo("ERROR: Failed to install PEAKArchipelago\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallPeakVersionBypass()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(peakVersion.url))
        {
            UnityEngine.Debug.LogWarning("peakVersion URL not set, skipping PeakVersionBypass install.");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "PeakVersionTemp");

        yield return downloader.DownloadAndExtract(peakVersion, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("PeakVersionBypass extractPath does not exist after extraction: " + extractPath);
            ShowInfo("ERROR: PeakVersionBypass extraction failed!");
            yield break;
        }

        string dllPath = FindFileByName(extractPath, "PeakVersionBypass.dll");

        UnityEngine.Debug.Log("PeakVersionBypass.dll found at: '" + dllPath + "'");

        if (string.IsNullOrEmpty(dllPath))
        {
            UnityEngine.Debug.LogError("PeakVersionBypass.dll not found in downloaded package!");
            ShowInfo("ERROR: PeakVersionBypass.dll not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string targetPath = Path.Combine(pluginsPath, "PeakVersionBypass.dll");

        try
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Copy(dllPath, targetPath, true);
            UnityEngine.Debug.Log("PeakVersionBypass.dll installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install PeakVersionBypass.dll: " + e.Message);
            ShowInfo("ERROR: Failed to install PeakVersionBypass.dll\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching PEAK...");
            LaunchPEAK();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
    }

    IEnumerator PEAKAPOnlyFlow()
    {
        peakPath = GetPEAKPath();

        if (string.IsNullOrEmpty(peakPath))
            yield break;

        ShowInfo("Installing PEAK AP Mod...");
        yield return InstallPEAKAP();

        ShowInfo("Installing Version Bypass...");
        yield return InstallPeakVersionBypass();

        CreateVersionFile(peakapworld.url, bepInEx.url, peakAP.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchPEAK();
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
            remoteConfig = JsonUtility.FromJson<PEAKConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyPEAKConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        peakPath = GetPEAKPath();
    }

    void ChmodExecutable(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = "+x \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
            }

            UnityEngine.Debug.Log("chmod +x applied to: " + path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to chmod +x " + path + ": " + e.Message);
        }
    }

    void LaunchPEAK()
    {
        bool isLinux = platformSelection != null && platformSelection.isLinux;

        if (isLinux)
        {
            LaunchPEAKLinux();
            return;
        }

        string exePath = Path.Combine(peakPath, "PEAK.exe");

        if (File.Exists(exePath))
            peakProcess = Process.Start(exePath);
    }

    void LaunchPEAKLinux()
    {
        string steamUrl = "steam://run/" + steamAppId;

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = steamUrl,
                UseShellExecute = true
            };

            peakProcess = Process.Start(psi);
            UnityEngine.Debug.Log("Launching PEAK via Steam (Linux): " + steamUrl);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Process.Start(UseShellExecute) failed for steam:// url, trying xdg-open: " + e.Message);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = steamUrl,
                    UseShellExecute = false
                };

                peakProcess = Process.Start(psi);
                UnityEngine.Debug.Log("Launching PEAK via xdg-open: " + steamUrl);
            }
            catch (System.Exception e2)
            {
                UnityEngine.Debug.LogError("Failed to launch PEAK via Steam on Linux: " + e2.Message);
                ShowInfo("ERROR: Failed to launch PEAK via Steam\n" + e2.Message);
            }
        }
    }

    void ClosePEAK()
    {
        try
        {
            if (peakProcess != null && !peakProcess.HasExited)
            {
                peakProcess.Kill();
                peakProcess.Dispose();
                peakProcess = null;
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

    string FindFileByName(string root, string fileName)
    {
        string[] matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);

        if (matches.Length > 0)
            return matches[0];

        return "";
    }

    string FindPeakpelagoFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "peakpelago")
                return dir;
        }

        return "";
    }

    void CreateVersionFile(string apworldUrl, string bepinexUrl, string peakApUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string peakApVersion = ExtractVersionFromUrl(peakApUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "PEAK APMod Version " + peakApVersion + ".txt";
            string content = "PEAK Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== PEAK APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== PEAK AP MOD ===\n";
            content += "Downloaded from: " + peakApUrl + "\n";
            content += "Version: " + peakApVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"PEAK APMod Version .+\.txt");

            string pluginsPath = Path.Combine(peakPath, "BepInEx", "plugins");
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

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string GetPEAKPath()
    {
        bool isLinux = platformSelection != null && platformSelection.isLinux;

        if (isLinux)
            return GetPEAKPathLinux();

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

    string GetPEAKPathLinux()
    {
        string homeDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        // 1. Recherche dans /home/(User)/...
        if (remoteConfig != null && remoteConfig.steamSearchPathsLinuxHome != null)
        {
            foreach (string relativePath in remoteConfig.steamSearchPathsLinuxHome)
            {
                if (string.IsNullOrEmpty(relativePath))
                    continue;

                try
                {
                    string path = Path.Combine(homeDir, relativePath, steamGameFolderName);
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Found Game (Steam, Linux home) at: " + path);
                        return path;
                    }
                }
                catch { }
            }
        }

        // 2. Recherche dans /mnt/(disque externe)/...
        if (remoteConfig != null && remoteConfig.steamSearchPathsLinuxExt != null)
        {
            try
            {
                string mntRoot = "/mnt";
                if (Directory.Exists(mntRoot))
                {
                    string[] mountedDrives = Directory.GetDirectories(mntRoot);

                    foreach (string drive in mountedDrives)
                    {
                        foreach (string relativePath in remoteConfig.steamSearchPathsLinuxExt)
                        {
                            if (string.IsNullOrEmpty(relativePath))
                                continue;

                            try
                            {
                                string path = Path.Combine(drive, relativePath, steamGameFolderName);
                                if (Directory.Exists(path))
                                {
                                    UnityEngine.Debug.Log("Found Game (Steam, Linux ext) at: " + path);
                                    return path;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Steam, Linux) not found.");
        return "";
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