using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class OkamiHDManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("OKAMIHD FILES")]
    public FileDownloader.FileData okamihdApworld;
    public FileDownloader.FileData okamihdWOLF;
    public FileDownloader.FileData okamihdAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installWOLFToggle;
    public Toggle installAPToggle;
    public Toggle installAPWorldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearWOLFToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process okamiProcess;
    private string okamiPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private OkamiConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class OkamiConfig
    {
        public string okamihdApworld;
        public string okamihdWOLF;
        public string okamihdAP;
    }

    void Start()
    {
        okamiPath = GetOkamiPath();
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

        if (fullClearWOLFToggle != null)
            fullClearWOLFToggle.isOn = false;

        if (fullClearWOLFToggle != null)
            fullClearWOLFToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installWOLFToggle != null)
            installWOLFToggle.isOn = true;
    }

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void ApplyOkamiConfig()
    {
        if (remoteConfig == null)
            return;

        okamihdApworld.url = remoteConfig.okamihdApworld;
        okamihdWOLF.url = remoteConfig.okamihdWOLF;
        okamihdAP.url = remoteConfig.okamihdAP;
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
        okamiPath = GetOkamiPath();

        if (string.IsNullOrEmpty(okamiPath))
        {
            ShowInfo("Okami not found in Steam. Please check installation.");
            return;
        }

        bool wolf = installWOLFToggle != null && installWOLFToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;

        int count =
            (wolf ? 1 : 0) +
            (ap ? 1 : 0) +
            (apworld ? 1 : 0);

        if (wolf && count == 1)
        {
            StartCoroutine(WOLFOnlyFlow());
            return;
        }

        if (ap && count == 1 && !apworld)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        okamiPath = GetOkamiPath();

        if (string.IsNullOrEmpty(okamiPath))
            return;

        string modsPath = Path.Combine(okamiPath, "mods");

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearWOLFToggle != null && fullClearWOLFToggle.isOn;

        if (!clearAP && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteDirectory(Path.Combine(modsPath, "apclient"));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(modsPath);

        if (fullClear &&
            hasOtherMods &&
            !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete WOLF/mods/logs?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        if (fullClear)
        {
            ShowInfo("Clearing WOLF, mods and logs...");

            SafeDeleteDirectory(Path.Combine(okamiPath, "mods"));
            SafeDeleteDirectory(Path.Combine(okamiPath, "logs"));

            SafeDeleteFile(Path.Combine(okamiPath, "wolf-loader.exe"));
            SafeDeleteFile(Path.Combine(okamiPath, "dinput8.dll"));
            SafeDeleteFile(Path.Combine(okamiPath, "imgui.ini"));

            DeleteOldVersionFiles();

            ShowInfo("Full clear completed!");
            return;
        }

        ShowInfo("Revert completed!");
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

    bool HasOtherMods(string modsPath)
    {
        if (!Directory.Exists(modsPath))
            return false;

        System.Text.RegularExpressions.Regex versionPattern = new System.Text.RegularExpressions.Regex(@"Okami AP Version .+\.txt", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        string[] dirs = Directory.GetDirectories(modsPath);
        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);
            if (name == "apclient")
                continue;
            return true;
        }

        string[] files = Directory.GetFiles(modsPath);
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            if (versionPattern.IsMatch(fileName))
                continue;

            if (fileName.Equals("wolf-loader.exe", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("imgui.ini", StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installWOLFToggle != null && installWOLFToggle.isOn)
        {
            ShowInfo("Installing WOLF runtime...");
            yield return InstallWOLF();
        }

        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAP();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(okamihdAP.url, okamihdWOLF.url);

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Okami via wolf-loader...");
            Process p = LaunchOkami();

            if (p != null)
            {
                UnityEngine.Debug.Log("Okami launched after setup.");
                ShowInfo("Launching Okami HD.");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Okami was not launched (process is null). Installation complete.");
                ShowInfo("Installation complete!");
            }
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "OkamiAPTemp");
        yield return downloader.DownloadAndExtract(okamihdAP, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(okamiPath, "mods");
        Directory.CreateDirectory(modsPath);

        string apclientSrc = Path.Combine(extractPath, "apclient");
        if (Directory.Exists(apclientSrc))
        {
            string apclientDst = Path.Combine(modsPath, "apclient");
            if (Directory.Exists(apclientDst))
                SafeDeleteDirectory(apclientDst);

            MoveDirectory(apclientSrc, apclientDst);
            UnityEngine.Debug.Log("AP client installed in mods/apclient");
        }
        else
        {
            string[] possible = Directory.GetDirectories(extractPath, "apclient", SearchOption.AllDirectories);
            if (possible.Length > 0)
            {
                string apclientDst = Path.Combine(modsPath, "apclient");
                if (Directory.Exists(apclientDst))
                    SafeDeleteDirectory(apclientDst);

                MoveDirectory(possible[0], apclientDst);
                UnityEngine.Debug.Log("AP client installed (found nested apclient) in mods/apclient");
            }
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallWOLF()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "WOLFTemp");

        yield return downloader.DownloadAndExtract(okamihdWOLF, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, okamiPath);

        string modsPath = Path.Combine(okamiPath, "mods");
        Directory.CreateDirectory(modsPath);

        UnityEngine.Debug.Log("WOLF runtime installed and mods folder created.");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + okamihdApworld.url);

        if (string.IsNullOrEmpty(okamihdApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = okamihdApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            var url = okamihdApworld.url ?? "";
            int idx = url.LastIndexOf('/');
            if (idx >= 0 && idx + 1 < url.Length)
                fileName = url.Substring(idx + 1);
            else
                fileName = url;

            int q = fileName.IndexOf('?');
            if (q >= 0)
                fileName = fileName.Substring(0, q);

            if (string.IsNullOrEmpty(fileName))
                fileName = "okamihd.apworld";
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + okamihdApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(okamihdApworld.url, localPath);

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

    int adofaiApworldIndexSafe(FileDownloader.FileData fd)
    {
        try
        {
            string url = fd.url ?? "";
            int idx = url.LastIndexOf('/');
            if (idx >= 0 && idx + 1 < url.Length) return idx + 1;
        }
        catch { }
        return 0;
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
        ShowInfo("Installing AP Mod...");
        yield return InstallAP();

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(okamihdAP.url, okamihdWOLF.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator WOLFOnlyFlow()
    {
        ShowInfo("Installing WOLF runtime...");
        yield return InstallWOLF();

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Okami via wolf-loader...");
            Process p = LaunchOkami();

            if (p != null)
            {
                UnityEngine.Debug.Log("Okami launched after WOLF installation.");
                ShowInfo("Installation complete — Okami lancé.");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Okami was not launched (process is null). Installation complete.");
                ShowInfo("Installation complete!");
            }
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield break;
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
            remoteConfig = JsonUtility.FromJson<OkamiConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyOkamiConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    Process LaunchOkami()
    {
        string loaderPath = Path.Combine(okamiPath, "wolf-loader.exe");
        Process started = null;

        if (!string.IsNullOrEmpty(loaderPath) && File.Exists(loaderPath))
        {
#if UNITY_STANDALONE_WIN
            try
            {
                var psiElev = new ProcessStartInfo()
                {
                    FileName = loaderPath,
                    WorkingDirectory = okamiPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                UnityEngine.Debug.Log("Attempting to start wolf-loader.exe elevated (UAC)...");
                try
                {
                    started = Process.Start(psiElev);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    UnityEngine.Debug.LogWarning("Elevation canceled or failed: " + ex.Message);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("Elevation attempt error: " + ex.Message);
                }

                if (started != null)
                {
                    okamiProcess = started;
                    UnityEngine.Debug.Log("wolf-loader.exe started elevated.");
                    return okamiProcess;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("Unexpected error during elevation attempt: " + ex.Message);
            }
#endif
            try
            {
                started = Process.Start(new ProcessStartInfo()
                {
                    FileName = loaderPath,
                    WorkingDirectory = okamiPath,
                    UseShellExecute = true
                });

                if (started != null)
                {
                    okamiProcess = started;
                    UnityEngine.Debug.Log("wolf-loader.exe started without elevation (fallback).");
                    return okamiProcess;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to start wolf-loader.exe (fallback): " + e.Message);
            }
        }

        string[] possible = new string[]
        {
        Path.Combine(okamiPath, "Okami.exe"),
        Path.Combine(okamiPath, "OkamiHD.exe"),
        Path.Combine(okamiPath, "OkamiHDRemastered.exe")
        };

        foreach (var p in possible)
        {
            try
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    started = Process.Start(new ProcessStartInfo()
                    {
                        FileName = p,
                        WorkingDirectory = okamiPath,
                        UseShellExecute = true
                    });

                    if (started != null)
                    {
                        okamiProcess = started;
                        UnityEngine.Debug.Log("Launched game binary as fallback: " + p);
                        return okamiProcess;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Unable to launch fallback exe " + p + " : " + e.Message);
            }
        }

        UnityEngine.Debug.LogWarning("No executable found to launch Okami.");
        return null;
    }

    void CloseOkami()
    {
        try
        {
            if (okamiProcess != null && !okamiProcess.HasExited)
            {
                okamiProcess.Kill();
                okamiProcess.Dispose();
                okamiProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseOkami();
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
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        try
        {
            Directory.Delete(source, true);
        }
        catch { }
    }

    void CreateVersionFile(string apUrl, string wolfUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"okami-apclient-v([^/]+)");
            string wolfVersion = ExtractVersionFromUrl(wolfUrl, @"wolf-runtime-v([^/]+)");

            string versionFileName = "Okami AP Version " + apVersion + ".txt";
            string content = "Okami AP Setup Tool\n";
            content += "https://github.com/yourusername/Okami-Setup-Tool\n";
            content += "\n";
            content += "=== AP CLIENT ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== WOLF RUNTIME ===\n";
            content += "Downloaded from: " + wolfUrl + "\n";
            content += "Version: " + wolfVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(okamiPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string modsPath = Path.Combine(okamiPath, "mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in mods: " + modsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Okami AP Version .+\.txt", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!string.IsNullOrEmpty(okamiPath))
            {
                string[] rootFiles = Directory.GetFiles(okamiPath);
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

            string modsPath = Path.Combine(okamiPath, "mods");
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
                            UnityEngine.Debug.Log("Deleted old version file in mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in mods: " + e.Message);
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
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        try
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            System.Text.RegularExpressions.Match match = regex.Match(url);

            if (match.Success)
                return match.Groups[1].Value;
        }
        catch { }

        try
        {
            string fileName = url.Substring(url.LastIndexOf('/') + 1);
            int dot = fileName.LastIndexOf('.');
            if (dot > 0)
                fileName = fileName.Substring(0, dot);
            return fileName;
        }
        catch { }

        return "Unknown";
    }

    string GetOkamiPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Okami"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Okami"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Okami HD"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Okami HD"),
            @"D:\Steam\steamapps\common\Okami",
            @"D:\SteamLibrary\steamapps\common\Okami",
            @"D:\steamapps\common\Okami",
            @"E:\Steam\steamapps\common\Okami",
            @"E:\SteamLibrary\steamapps\common\Okami",
            @"E:\steamapps\common\Okami",
            @"E:\Program Files (x86)\steamapps\common\Okami",
            @"E:\Program Files\steamapps\common\Okami",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Okami (Steam) at: " + path);
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
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Okami");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }

                    subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Okami HD");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }

                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Okami");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }

                    subPath = Path.Combine(drive.Name, "steamapps", "common", "Okami");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }

                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Okami");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }

                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Okami");
                    if (Directory.Exists(subPath))
                    {
                        UnityEngine.Debug.Log("Found Okami (Steam) at: " + subPath);
                        return subPath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Okami (Steam) not found.");
        return "";
    }
}