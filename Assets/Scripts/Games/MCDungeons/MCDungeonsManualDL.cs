using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class MCDungeonsManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("MINECRAFT DUNGEONS FILES")]
    public FileDownloader.FileData minecraftdungeonsApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle launchAfterSetupToggle;

    [Header("PYTHON PACKAGES")]
    public string pipPackages = "pymem websocket-client pywin32";

    [Header("GAME")]
    public string gameProcessName = "Dungeons";
    public string steamAppId = "1672970";
    public string microsoftStoreAppId = @"shell:AppsFolder\Microsoft.Lovika_8wekyb3d8bbwe!Game";
    public float launchDetectTimeout = 20f;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("LAUNCH")]
    public Button launchButton;

    private string archipelagoPath;
    private string pendingAction;

    private MCDungeonsConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool launchInProgress = false;

    [System.Serializable]
    public class MCDungeonsConfig
    {
        public string minecraftdungeonsApworld;
        public string[] apSearchPaths;
    }

    void Start()
    {
        archipelagoPath = GetArchipelagoPath();

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchButtonClicked);

        StartCoroutine(LoadRemoteConfig());
    }

    // ---------------------------------------------------------------
    // BUTTON ENTRY POINTS
    // ---------------------------------------------------------------

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
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
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // LAUNCH BUTTON HANDLER
    // ---------------------------------------------------------------

    public void OnLaunchButtonClicked()
    {
        ShowInfo("Launching Minecraft Dungeons...");
        LaunchGame();
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath))
        {
            ShowInfo("ERROR: Could not find or create the Archipelago folder!");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;

        if (!apworld)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld));
    }

    IEnumerator InstallFlow(bool apworld)
    {
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();

            if (lastApWorldInstallSuccess)
            {
                ShowInfo("Installing Python packages (" + pipPackages + ")...");
                yield return InstallPythonPackages();
            }
        }

        if (lastApWorldInstallSuccess && lastPipSuccess)
            ShowInfo("Installation complete!");

        if (launchAfterSetupToggle != null && launchAfterSetupToggle.isOn)
        {
            yield return new WaitForSeconds(1.5f);
            LaunchGame();
        }
    }

    // ---------------------------------------------------------------
    // PYTHON PACKAGES (pip)
    // ---------------------------------------------------------------

    private bool lastPipSuccess = false;

    IEnumerator InstallPythonPackages()
    {
        lastPipSuccess = false;

        string[][] attempts = new string[][]
        {
            new string[] { "python", "-m pip install " + pipPackages },
            new string[] { "py", "-m pip install " + pipPackages },
        };

        string lastError = "";

        foreach (string[] attempt in attempts)
        {
            Process process = null;
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            bool started = false;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = attempt[0],
                    Arguments = attempt[1],
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                started = true;

                UnityEngine.Debug.Log("Running: " + attempt[0] + " " + attempt[1]);
            }
            catch (System.Exception e)
            {
                lastError = attempt[0] + " not found: " + e.Message;
                UnityEngine.Debug.LogWarning("Could not start " + attempt[0] + ": " + e.Message);
                if (process != null)
                    process.Dispose();
            }

            if (!started)
                continue;

            while (!process.HasExited)
                yield return null;

            process.WaitForExit();

            int exitCode = process.ExitCode;
            process.Dispose();

            UnityEngine.Debug.Log("pip output:\n" + output);

            if (exitCode == 0)
            {
                UnityEngine.Debug.Log("Python packages installed successfully.");
                lastPipSuccess = true;
                yield break;
            }

            lastError = "pip exited with code " + exitCode + "\n" + error;
            UnityEngine.Debug.LogError("pip failed: " + lastError);
        }

        ShowInfo("WARNING: APWorld installed, but the Python packages (" + pipPackages +
                 ") could not be installed.\nMake sure Python is installed and in your PATH, then run:\n" +
                 "pip install " + pipPackages);
    }

    // ---------------------------------------------------------------
    // APWORLD
    // ---------------------------------------------------------------

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + minecraftdungeonsApworld.url);

        if (string.IsNullOrEmpty(minecraftdungeonsApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = minecraftdungeonsApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "mcdungeons.apworld";
            UnityEngine.Debug.Log("Using default filename: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + minecraftdungeonsApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(minecraftdungeonsApworld.url, localPath);

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
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    // ---------------------------------------------------------------
    // LAUNCH
    // ---------------------------------------------------------------

    public void LaunchGame()
    {
        if (launchInProgress)
            return;

        StartCoroutine(LaunchGameRoutine());
    }

    IEnumerator LaunchGameRoutine()
    {
        launchInProgress = true;

        if (IsGameRunning())
        {
            UnityEngine.Debug.Log("Minecraft Dungeons is already running.");
            launchInProgress = false;
            yield break;
        }

        bool steamStarted = false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://launch/" + steamAppId,
                UseShellExecute = true
            });
            steamStarted = true;
            UnityEngine.Debug.Log("Launch requested via Steam (app id " + steamAppId + ").");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Steam launch failed: " + e.Message);
        }

        if (steamStarted)
        {
            float elapsed = 0f;
            while (elapsed < launchDetectTimeout)
            {
                if (IsGameRunning())
                {
                    launchInProgress = false;
                    yield break;
                }
                yield return new WaitForSeconds(1f);
                elapsed += 1f;
            }

            UnityEngine.Debug.LogWarning("Dungeons non détecté après le lancement Steam.");
            launchInProgress = false;
            yield break;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = microsoftStoreAppId,
                UseShellExecute = true
            });
            UnityEngine.Debug.Log("Launch requested via Microsoft Store (explorer.exe).");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Minecraft Dungeons: " + e.Message);
            ShowInfo("ERROR: Failed to launch Minecraft Dungeons!");
        }

        launchInProgress = false;
    }

    bool IsGameRunning()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName(gameProcessName);
            bool running = processes.Length > 0;
            foreach (Process p in processes)
                p.Dispose();
            return running;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
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

    string GetArchipelagoPath()
    {
        string[] candidates = new string[]
        {
            @"C:\ProgramData\Archipelago",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        foreach (string candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    UnityEngine.Debug.Log("Found Archipelago folder at: " + candidate);
                    return candidate;
                }
            }
            catch { }
        }

        foreach (string candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                UnityEngine.Debug.Log("Created Archipelago folder at: " + candidate);
                return candidate;
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Could not find or create an Archipelago folder.");
        return "";
    }

    // ---------------------------------------------------------------
    // Remote config loader (optional)
    // ---------------------------------------------------------------
    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("MCDungeons config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<MCDungeonsConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("MCDungeons remote config loaded successfully");
                ApplyConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("MCDungeons config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyConfig()
    {
        if (remoteConfig == null)
            return;

        if (minecraftdungeonsApworld != null && !string.IsNullOrEmpty(remoteConfig.minecraftdungeonsApworld))
            minecraftdungeonsApworld.url = remoteConfig.minecraftdungeonsApworld;
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