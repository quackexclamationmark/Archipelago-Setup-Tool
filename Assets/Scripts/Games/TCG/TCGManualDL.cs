using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TCGManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("TCG FILES")]
    public FileDownloader.FileData tcgApworld;
    public FileDownloader.FileData tcgAP;
    public FileDownloader.FileData tcgBepInEx;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "TCG Card Shop Simulator";

    [Header("FEATURE TOGGLES")]
    public Toggle installTCGApworldToggle;
    public Toggle installTCGAPToggle;
    public Toggle installTCGBepInExToggle;

    [Header("CONFIG TOGGLES")]
    public Toggle hideManagerGameObjectToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle patchConfigToggle;
    public Toggle fullCleanBepInExToggle;
    public Toggle removeTCGModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process tcgProcess;
    private bool tcgLaunchedAsHelper = false;
    private string tcgPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private TcgConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class TcgConfig
    {
        public string tcgAP;
        public string tcgBepInEx;
        public string tcgApworld;
        public string[] steamSearchPaths;
    }

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    void Start()
    {
        tcgPath = GetTCGPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
        if (removeTCGModsOnlyToggle != null) removeTCGModsOnlyToggle.isOn = true;
        if (fullCleanBepInExToggle != null) fullCleanBepInExToggle.isOn = false;
        if (patchConfigToggle != null) patchConfigToggle.isOn = false;
        if (fullCleanBepInExToggle != null) fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void ApplyTcgConfig()
    {
        if (remoteConfig == null) return;
        tcgAP.url = remoteConfig.tcgAP;
        tcgBepInEx.url = remoteConfig.tcgBepInEx;
        tcgApworld.url = remoteConfig.tcgApworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    public void RevertAll() => ShowConfirmation("Are you sure you want to revert?", "Revert");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
            case "ForceFullClean": ExecuteRevert(); break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        tcgPath = GetTCGPath();

        bool apworld = installTCGApworldToggle == null || installTCGApworldToggle.isOn;
        bool ap = installTCGAPToggle != null && installTCGAPToggle.isOn;
        bool bepinex = installTCGBepInExToggle != null && installTCGBepInExToggle.isOn;
        bool needsGamePath = ap || bepinex;

        if (needsGamePath && (string.IsNullOrEmpty(tcgPath) || !Directory.Exists(tcgPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0) + (bepinex ? 1 : 0);

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (ap && count == 1) { StartCoroutine(TCGAPOnlyFlow()); return; }
        if (bepinex && count == 1) { StartCoroutine(BepInExOnlyFlow()); return;       
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
            ShowInfo("Launching TCG CGS...");
            LaunchTCG();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        tcgPath = GetTCGPath();

        if (string.IsNullOrEmpty(tcgPath)) return;

        string pluginsPath = Path.Combine(tcgPath, "BepInEx", "plugins");

        bool removeTCG = removeTCGModsOnlyToggle != null && removeTCGModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;
        bool patchConfig = patchConfigToggle != null && patchConfigToggle.isOn;

        if (!removeTCG && !fullClean && !patchConfig)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (patchConfig && !removeTCG && !fullClean)
        {
            SetDefaultBepInExConfig();
            ShowInfo("Config patched successfully!");
            return;
        }

        if (removeTCG)
        {
            CleanupProcesses();
            if (!Directory.Exists(pluginsPath)) return;

            ShowInfo("Removing TCG mods...");
            SafeDeleteFile(Path.Combine(pluginsPath, "tcg_card_shop_simulator.apworld"));
            SafeDeleteFile(Path.Combine(pluginsPath, "TCG_AP_Client.dll"));
            DeleteOldVersionFiles();
            ShowInfo("TCG mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            DeleteOldVersionFiles();
            pendingFullCleanConfirmation = true;
            ShowConfirmation("Other mods were detected.\nDo you REALLY want to fully delete BepInEx?", "ForceFullClean");
            return;
        }

        pendingFullCleanConfirmation = false;
        CleanupProcesses();

        ShowInfo("Removing mods...");
        SafeDeleteFile(Path.Combine(pluginsPath, "tcg_card_shop_simulator.apworld"));
        SafeDeleteFile(Path.Combine(pluginsPath, "TCG_AP_Client.dll"));
        DeleteOldVersionFiles();

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullClean)
        {
            DeleteOldVersionFiles();
            ShowInfo("Cleaning BepInEx...");
            SafeDeleteDirectory(Path.Combine(tcgPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(tcgPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(tcgPath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(tcgPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(tcgPath, ".doorstop_version"));
            ShowInfo("Full clean completed!");
            return;
        }

        if (!hasOtherMods)
        {
            DeleteOldVersionFiles();
            ShowInfo("Cleaning BepInEx...");
            SafeDeleteDirectory(Path.Combine(tcgPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(tcgPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(tcgPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(tcgPath, ".doorstop_version"));
            ShowInfo("Revert completed!");
        }
    }

    void SetDefaultBepInExConfig()
    {
        string cfgPath = Path.Combine(tcgPath, "BepInEx", "config", "BepInEx.cfg");

        if (!File.Exists(cfgPath))
            return;

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("HideManagerGameObject"))
                lines[i] = "HideManagerGameObject = false";

        File.WriteAllLines(cfgPath, lines);
    }

    IEnumerator InstallFlow()
    {
        // attendre la config chargée
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string pluginsPath = Path.Combine(tcgPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        bool installApworld = installTCGApworldToggle == null || installTCGApworldToggle.isOn;
        bool installTcgap = installTCGAPToggle != null && installTCGAPToggle.isOn;
        bool installBepinex = installTCGBepInExToggle != null && installTCGBepInExToggle.isOn;
        bool hideManager = hideManagerGameObjectToggle != null && hideManagerGameObjectToggle.isOn;

        if (installBepinex)
        {
            ShowInfo("Installing BepInEx...");
            string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");
            yield return downloader.DownloadAndExtract(tcgBepInEx, Application.persistentDataPath, extractPath);
            MoveDirectory(extractPath, tcgPath);
            SafeDeleteDirectory(extractPath);

            // handle config patching: ensure cfg exists, patch it, before continuing
            if (hideManager)
            {
                string cfgPath = Path.Combine(tcgPath, "BepInEx", "config", "BepInEx.cfg");
                if (!File.Exists(cfgPath))
                {
                    ShowInfo("Launching TCG to generate BepInEx config...");
                    LaunchTCG(true); // helper launch
                    // WAIT FOR CONFIG EXISTENCE + STABILITY
                    yield return StartCoroutine(WaitForConfigFiles());
                    // close helper before editing
                    CloseTCG();
                }

                // patch configuration (this will wait for file if needed and retry writes)
                ShowInfo("Patching BepInEx config...");
                yield return StartCoroutine(SetBepInExConfig(hideManager));
            }
        }

        if (installApworld)
        {
            ShowInfo("Installing AP World...");
            yield return InstallAPWorld();
        }

        if (installTcgap)
        {
            ShowInfo("Installing TCG AP Client...");
            string extractPath = Path.Combine(Application.persistentDataPath, "TCGAPTemp");
            yield return downloader.DownloadAndExtract(tcgAP, Application.persistentDataPath, extractPath);

            // Prepare sourceRoot (handle archive that creates a single top folder)
            string sourceRoot = extractPath;
            try
            {
                var topDirs = Directory.GetDirectories(extractPath);
                var topFiles = Directory.GetFiles(extractPath);
                if (topFiles.Length == 0 && topDirs.Length == 1)
                    sourceRoot = topDirs[0];
            }
            catch { }

            // Move all content into plugins (merge)
            MoveDirectory(sourceRoot, pluginsPath);
            SafeDeleteDirectory(extractPath);
        }

        CreateVersionFile(tcgAP.url, tcgBepInEx.url, tcgApworld.url);

        ShowInfo("Installation complete!");
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(2f);
            LaunchTCG(false);
        }
    }

    IEnumerator TCGAPOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing TCG AP Client...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string extractPath = Path.Combine(Application.persistentDataPath, "TCGAPTemp");
        yield return downloader.DownloadAndExtract(tcgAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(tcgPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string sourceRoot = extractPath;
        try
        {
            var topDirs = Directory.GetDirectories(extractPath);
            var topFiles = Directory.GetFiles(extractPath);
            if (topFiles.Length == 0 && topDirs.Length == 1)
                sourceRoot = topDirs[0];
        }
        catch { }

        MoveDirectory(sourceRoot, pluginsPath);
        SafeDeleteDirectory(extractPath);

        CreateVersionFile(tcgAP.url, tcgBepInEx.url, tcgApworld.url);
        ShowInfo("TCG AP Client installed successfully!");
    }

    IEnumerator BepInExOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing BepInEx...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");
        yield return downloader.DownloadAndExtract(tcgBepInEx, Application.persistentDataPath, extractPath);
        MoveDirectory(extractPath, tcgPath);
        SafeDeleteDirectory(extractPath);

        bool hideManager = hideManagerGameObjectToggle != null && hideManagerGameObjectToggle.isOn;
        if (hideManager)
        {
            string cfgPath = Path.Combine(tcgPath, "BepInEx", "config", "BepInEx.cfg");
            if (!File.Exists(cfgPath))
            {
                ShowInfo("Launching TCG to generate BepInEx config...");
                LaunchTCG(true);
                yield return StartCoroutine(WaitForConfigFiles());
                CloseTCG();
            }

            ShowInfo("Patching BepInEx config...");
            yield return StartCoroutine(SetBepInExConfig(true));
        }

        CreateVersionFile(tcgAP.url, tcgBepInEx.url, tcgApworld.url);
        ShowInfo("BepInEx installed successfully!");

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(1f);
            LaunchTCG(false);
        }
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(tcgApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = tcgApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = tcgApworld.url.Substring(tcgApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + tcgApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(tcgApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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
            try { File.Delete(target); } catch { }
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
            yield break;
        }
        
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

    IEnumerator WaitForConfigFiles()
    {
        string bepConfig = Path.Combine(tcgPath, "BepInEx", "config", "BepInEx.cfg");
        string logFile = Path.Combine(tcgPath, "BepInEx", "LogOutput.log");

        float timeout = 60f;
        float timer = 0f;

        bool cfgOk = false;
        bool logOk = false;

        while (timer < timeout)
        {
            if (!cfgOk && File.Exists(bepConfig))
            {
                UnityEngine.Debug.Log("BepInEx.cfg detected, waiting for stability...");
                yield return StartCoroutine(WaitForFileStable(bepConfig, stableDuration: 1f, timeout: 20f));
                cfgOk = true;
                UnityEngine.Debug.Log("BepInEx.cfg stable.");
            }

            if (!logOk && File.Exists(logFile))
            {
                UnityEngine.Debug.Log("LogOutput.log detected, waiting for stability...");
                yield return StartCoroutine(WaitForFileStable(logFile, stableDuration: 1f, timeout: 20f));
                logOk = true;
                UnityEngine.Debug.Log("LogOutput.log stable.");
            }

            if (cfgOk && logOk)
            {
                UnityEngine.Debug.Log("Config and log ready.");
                yield break;
            }

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        UnityEngine.Debug.LogWarning("WaitForConfigFiles timed out waiting for BepInEx.cfg and/or LogOutput.log");
    }

    // Robust config patcher: waits for file, waits for stability, retries writes if locked, replaces or appends key
    IEnumerator SetBepInExConfig(bool hideManager)
    {
        string cfgPath = Path.Combine(tcgPath, "BepInEx", "config", "BepInEx.cfg");

        // wait for existence
        yield return new WaitUntil(() => File.Exists(cfgPath));

        // wait for file to be stable (not being written)
        yield return StartCoroutine(WaitForFileStable(cfgPath, stableDuration: 1f, timeout: 20f));

        int maxAttempts = 8;
        int attempt = 0;
        bool success = false;

        while (attempt < maxAttempts && !success)
        {
            attempt++;
            bool ioFailed = false;

            try
            {
                string[] lines = File.ReadAllLines(cfgPath);
                bool replaced = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("HideManagerGameObject", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lines[i] = "HideManagerGameObject = " + (hideManager ? "true" : "false");
                        replaced = true;
                    }
                }

                if (!replaced)
                {
                    var list = new System.Collections.Generic.List<string>(lines);
                    list.Add("");
                    list.Add("HideManagerGameObject = " + (hideManager ? "true" : "false"));
                    lines = list.ToArray();
                }

                File.WriteAllLines(cfgPath, lines);
                success = true;
                UnityEngine.Debug.Log("BepInEx config patched: HideManagerGameObject = " + (hideManager ? "true" : "false"));
            }
            catch (IOException ioEx)
            {
                UnityEngine.Debug.LogWarning($"Attempt {attempt} to write BepInEx.cfg failed (IO): {ioEx.Message}");
                ioFailed = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Failed to patch BepInEx config: " + ex);
                yield break;
            }

            if (!success)
            {
                if (ioFailed)
                    yield return new WaitForSeconds(0.5f);
                else
                    yield return null;
            }
        }

        if (!success)
            UnityEngine.Debug.LogWarning("Failed to write BepInEx.cfg after multiple attempts; file may be locked by the game.");
    }

    // Waits until file is stable (not modified) for stableDuration seconds
    IEnumerator WaitForFileStable(string path, float stableDuration = 1f, float timeout = 10f)
    {
        float start = Time.time;

        if (!File.Exists(path))
            yield break;

        long lastSize;
        DateTime lastWrite;

        try
        {
            var fi = new FileInfo(path);
            lastSize = fi.Length;
            lastWrite = fi.LastWriteTimeUtc;
        }
        catch
        {
            yield break;
        }

        float stableStart = -1f;

        while (Time.time - start < timeout)
        {
            yield return new WaitForSeconds(0.25f);

            if (!File.Exists(path))
                yield break;

            var info = new FileInfo(path);
            long size = info.Length;
            DateTime writeTime = info.LastWriteTimeUtc;

            if (size == lastSize && writeTime == lastWrite)
            {
                if (stableStart < 0f) stableStart = Time.time;
                if (Time.time - stableStart >= stableDuration)
                    yield break;
            }
            else
            {
                lastSize = size;
                lastWrite = writeTime;
                stableStart = -1f;
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
            remoteConfig = JsonUtility.FromJson<TcgConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyTcgConfig();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        tcgPath = GetTCGPath();
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! " + savePath);
            }
        }
    }

    // LaunchTCG: accepts asHelper flag to mark the process as killable by CloseTCG()
    void LaunchTCG(bool asHelper = false)
    {
        string exePath = Path.Combine(tcgPath, "Card Shop Simulator.exe");
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                tcgProcess = Process.Start(psi);
                tcgLaunchedAsHelper = asHelper;
                UnityEngine.Debug.Log($"TCG launched for {(asHelper ? "config generation (helper)" : "user/interactive launch")} (minimized).");

            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("TCG executable not found: " + exePath);
        }
    }

    void CloseTCG(bool force = false)
    {
        try
        {
            if (tcgProcess != null && !tcgProcess.HasExited)
            {
                if (force || tcgLaunchedAsHelper)
                {
                    tcgProcess.Kill();
                    tcgProcess.Dispose();
                    tcgProcess = null;
                    tcgLaunchedAsHelper = false;
                    UnityEngine.Debug.Log("TCG process closed.");
                }
                else
                {
                    UnityEngine.Debug.Log("CloseTCG: process started interactively, not killing it.");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("CloseTCG error: " + ex.Message);
        }
    }

    void CleanupProcesses() => CloseTCG();

    void SafeDeleteFile(string path) => StartCoroutine(DeleteFileForce(path));

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;
        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                if (!File.Exists(path)) yield break;
            }
            catch { }
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    void OnFullCleanChanged(bool value)
    {
        if (patchConfigToggle != null) { patchConfigToggle.isOn = false; patchConfigToggle.interactable = !value; }
        if (removeTCGModsOnlyToggle != null) { removeTCGModsOnlyToggle.isOn = false; removeTCGModsOnlyToggle.interactable = !value; }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source)) return;
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(file, dest);
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath)) return false;
        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("TCG APMod Version") && name.EndsWith(".txt")) continue;
            if (name != "Archipelago.MultiClient.Net.dll" && name != "TCG_AP_Client.dll") return true;
        }
        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);
            if (!dirName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(tcgPath))
            {
                string fn = Path.GetFileName(file);
                if (fn.StartsWith("TCG APMod Version") && fn.EndsWith(".txt")) SafeDeleteFile(file);
            }
            string pluginsPath = Path.Combine(tcgPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                foreach (string file in Directory.GetFiles(pluginsPath))
                {
                    string fn = Path.GetFileName(file);
                    if (fn.StartsWith("TCG APMod Version") && fn.EndsWith(".txt")) SafeDeleteFile(file);
                }
            }
        }
        catch { }
    }

    void CreateVersionFile(string tcgapUrl, string bepinexUrl, string apworldUrl)
    {
        try
        {
            string tcgapVersion = ExtractVersionFromUrl(tcgapUrl, @"/([^/]+)\.zip");
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl, @"/releases/download/([^/]+)/");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "TCG APMod Version " + tcgapVersion + ".txt";
            string content = "TCG Card Shop Simulator Archipelago Setup Tool by quack!\n\n";
            content += "=== TCG AP CLIENT ===\nDownloaded from: " + tcgapUrl + "\nVersion: " + tcgapVersion + "\n\n";
            content += "=== APWORLD ===\nDownloaded from: " + apworldUrl + "\nName: " + apworldVersion + ".apworld\n\n";
            content += "=== BEPINEX ===\nDownloaded from: " + bepinexUrl + "\nVersion: " + bepinexVersion + "\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(tcgPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(tcgPath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url)) return "Unknown";
        Match match = Regex.Match(url, pattern);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    string GetTCGPath()
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
}