using System.Collections;
using System.Diagnostics;
using System.IO;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SubnauticaManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SUBNAUTICA FILES")]
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;
    public Toggle nonVRToggle;

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

    private Process subnauticaProcess;
    private string subnauticaPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private SubnauticaConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class SubnauticaConfig
    {
        public string subnauticaAP;
        public string subnauticaBepInEx;
    }

    void Start()
    {
        subnauticaPath = GetSubnauticaPath();
        StartCoroutine(LoadRemoteConfig());

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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================

    void ApplySubnauticaConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.subnauticaAP;
        bepInEx.url = remoteConfig.subnauticaBepInEx;
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

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        subnauticaPath = GetSubnauticaPath();

        if (string.IsNullOrEmpty(subnauticaPath))
        {
            ShowInfo("Subnautica not found. Please check Steam/Epic installation.");
            return;
        }

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool mod = installAPModToggle == null || installAPModToggle.isOn;

        int count = (bep ? 1 : 0) + (mod ? 1 : 0);

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (mod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        subnauticaPath = GetSubnauticaPath();

        if (string.IsNullOrEmpty(subnauticaPath))
            return;

        string pluginsPath = Path.Combine(subnauticaPath, "BepInEx", "plugins");

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

            SafeDeleteDirectory(Path.Combine(pluginsPath, "SubnauticaAP"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected in BepInEx/plugins.\nDo you want to continue?",
                "ForceFullClean"
            );

            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SubnauticaAP"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(subnauticaPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(subnauticaPath, ".doorstop_version"));

            ShowInfo("Full clean completed!");
        }
        else
        {
            ShowInfo("Revert completed!");
        }
    }

    // =========================================================
    // INFO
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
    // OTHER LOGIC
    // =========================================================

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        foreach (string dir in Directory.GetDirectories(pluginsPath))
        {
            string name = Path.GetFileName(dir);

            if (name != "SubnauticaAP" && name != "Archipelago")
                return true;
        }

        foreach (string file in Directory.GetFiles(pluginsPath))
            return true;

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installBepInExToggle == null || installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        ShowInfo("Launching Subnautica...");
        LaunchSubnautica();

        yield return WaitForConfigFiles();

        CloseSubnautica();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchSubnautica();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, subnauticaPath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "SubnauticaAPTemp");

        CreateVersionFile(apMod.url, bepInEx.url);

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(subnauticaPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string archipelagoPath = Path.Combine(extractPath, "BepInEx", "plugins", "Archipelago");

        if (!Directory.Exists(archipelagoPath))
        {
            UnityEngine.Debug.LogError("Archipelago folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string targetPath = Path.Combine(pluginsPath, "Archipelago");

        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        CopyDirectory(archipelagoPath, targetPath);

        UnityEngine.Debug.Log("AP Mod installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        ShowInfo("Launching Subnautica...");
        LaunchSubnautica();
        yield return WaitForConfigFiles();
        CloseSubnautica();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Second launch...");
            LaunchSubnautica();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator APModOnlyFlow()
    {
        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(subnauticaPath, "BepInEx", "config", "BepInEx.cfg");
        string pluginsDir = Path.Combine(subnauticaPath, "BepInEx", "plugins");
        string patchersDir = Path.Combine(subnauticaPath, "BepInEx", "patchers");

        float timer = 0f;
        float timeout = 30f;

        while ((!File.Exists(cfg) ||
                !Directory.Exists(pluginsDir) ||
                !Directory.Exists(patchersDir)) && timer < timeout)
        {
            timer += 1f;
            yield return new WaitForSeconds(1f);
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
            remoteConfig = JsonUtility.FromJson<SubnauticaConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySubnauticaConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchSubnautica()
    {
        string exePath = Path.Combine(subnauticaPath, "Subnautica.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Subnautica executable not found!");
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = exePath;
        startInfo.WorkingDirectory = subnauticaPath;

        if (nonVRToggle != null && nonVRToggle.isOn)
            startInfo.Arguments = "-vrmode none";

        try
        {
            subnauticaProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Subnautica launched successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Subnautica:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseSubnautica()
    {
        try
        {
            if (subnauticaProcess != null && !subnauticaProcess.HasExited)
            {
                subnauticaProcess.Kill();
                subnauticaProcess.Dispose();
                subnauticaProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseSubnautica();
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

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string bepinexUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);
            string bepinexVersion = ExtractBepInExVersion(bepinexUrl);

            string versionFileName = "Subnautica APMod Version " + apmodVersion + ".txt";

            DeleteOldVersionFiles();

            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            // Créer dans le root avec l'exe
            string rootVersionPath = Path.Combine(subnauticaPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            // Créer dans le dossier Archipelago
            string archipelagoPath = Path.Combine(subnauticaPath, "BepInEx", "plugins", "Archipelago");
            if (Directory.Exists(archipelagoPath))
            {
                string archipelagoVersionPath = Path.Combine(archipelagoPath, versionFileName);
                File.WriteAllText(archipelagoVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Archipelago folder: " + archipelagoVersionPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Archipelago folder not found for version file creation");
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Subnautica APMod Version .+\.txt");

            // Supprimer dans le root
            string[] rootFiles = Directory.GetFiles(subnauticaPath);
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

            // Supprimer dans le dossier Archipelago
            string archipelagoPath = Path.Combine(subnauticaPath, "BepInEx", "plugins", "Archipelago");
            if (Directory.Exists(archipelagoPath))
            {
                string[] archipelagoFiles = Directory.GetFiles(archipelagoPath);
                foreach (string file in archipelagoFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in Archipelago folder: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Archipelago folder: " + e.Message);
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
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"/download/(\d+\.\d+\.\d+)/");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string ExtractBepInExVersion(string url)
    {
        // Cherche le pattern GitHub releases: /releases/download/VERSION/filename
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetSubnauticaPath()
    {
        // 1. Steam
        string steam = GetSteamPath();
        if (!string.IsNullOrEmpty(steam))
        {
            UnityEngine.Debug.Log("Found Subnautica in Steam: " + steam);
            return steam;
        }

        // 2. Epic (manifest)
        string epic = GetEpicPath();
        if (!string.IsNullOrEmpty(epic))
        {
            UnityEngine.Debug.Log("Found Subnautica in Epic: " + epic);
            return epic;
        }

        // 3. Check other common Steam paths
        string[] otherPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Subnautica"),
            @"D:\Steam\steamapps\common\Subnautica",
            @"D:\SteamLibrary\steamapps\common\Subnautica",
            @"E:\Steam\steamapps\common\Subnautica",
            @"E:\SteamLibrary\steamapps\common\Subnautica",
        };

        foreach (string path in otherPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Subnautica at: " + path);
                    return path;
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Subnautica not found.");
        return "";
    }

    string GetSteamPath()
    {
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "Subnautica"
        );

        if (Directory.Exists(path))
            return path;

        return "";
    }

    string GetEpicPath()
    {
        string manifestFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests"
        );

        if (!Directory.Exists(manifestFolder))
            return "";

        string[] files = Directory.GetFiles(manifestFolder, "*.item");

        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);

                if (!json.Contains("Subnautica"))
                    continue;

                int index = json.IndexOf("InstallLocation");

                if (index == -1)
                    continue;

                int start = json.IndexOf(":", index) + 2;
                int end = json.IndexOf("\"", start);

                string path = json.Substring(start, end - start);

                if (Directory.Exists(path))
                    return path;
            }
            catch { }
        }

        return "";
    }
}