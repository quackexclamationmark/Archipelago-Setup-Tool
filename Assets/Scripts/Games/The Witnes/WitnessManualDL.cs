using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class WitnessManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("WITNESS FILES")]
    public FileDownloader.FileData witnessAP;

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "The Witness";
    public string epicGameFolderName = "TheWitness";

    [Header("FEATURE TOGGLES")]
    public Toggle installWitnessAPToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchAfterSetupToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string witnessPath;
    private string pendingAction;
    private WitnessConfig remoteConfig;
    private bool configLoaded = false;
    private bool isEpic = false;

    [System.Serializable]
    public class WitnessConfig
    {
        public string thewitnessAP;
        public string[] steamSearchPaths;
        public string[] epicSearchPaths;
    }

    void Start()
    {
        // Initialize platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);

        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // Select Steam by default
        SelectSteam();

        witnessPath = GetWitnessPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchAfterSetupToggle != null)
            launchAfterSetupToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    // =========================================================
    // PLATFORM SELECTION
    // =========================================================

    void OnSteamButtonClicked()
    {
        SelectSteam();
    }

    void OnEpicButtonClicked()
    {
        SelectEpic();
    }

    void SelectSteam()
    {
        isEpic = false;
        witnessPath = GetWitnessPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + witnessPath);
    }

    void SelectEpic()
    {
        isEpic = true;
        witnessPath = GetWitnessPath();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + witnessPath);
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(witnessPath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform} \n {status}";
        }
    }

    // =========================================================

    void ApplyWitnessConfig()
    {
        if (remoteConfig == null)
            return;

        witnessAP.url = remoteConfig.thewitnessAP;
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
        witnessPath = GetWitnessPath();

        if (string.IsNullOrEmpty(witnessPath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("The Witness not found in " + platform + ". Please check installation.");
            return;
        }

        bool witnessap = installWitnessAPToggle != null && installWitnessAPToggle.isOn;
        bool launchAfter = launchAfterSetupToggle != null && launchAfterSetupToggle.isOn;

        if (!witnessap && !launchAfter)
        {
            ShowInfo("Please select an option to install or enable 'Launch After Setup'.");
            return;
        }

        StartCoroutine(InstallFlow(witnessap, launchAfter));
    }

    private void ExecuteRevert()
    {
        witnessPath = GetWitnessPath();

        if (string.IsNullOrEmpty(witnessPath))
            return;

        ShowInfo("Removing Witness Archipelago files...");

        SafeDeleteFile(Path.Combine(witnessPath, "The.Witness.Randomizer.for.Archipelago.exe"));
        SafeDeleteFile(Path.Combine(witnessPath, "FindProcessErrorLog.txt"));
        SafeDeleteFile(Path.Combine(witnessPath, "WitnessRandomizerLog.txt"));

        ShowInfo("Witness Archipelago files removed successfully!");
    }

    IEnumerator InstallFlow(bool installWitnessAp, bool launchAfter)
    {
        if (installWitnessAp)
        {
            ShowInfo("Installing Witness Archipelago...");
            yield return StartCoroutine(InstallWitnessAP());
        }
        else
        {
            UnityEngine.Debug.Log("[InstallFlow] Skipping installation (user did not request install).");
        }

        if (launchAfter)
        {
            ShowInfo("You can now launch 'The.Witness.Randomizer.for.Archipelago.exe' in your game folder.");
            yield return StartCoroutine(LaunchWitness());

            ShowInfo("Launch finished.");
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallWitnessAP()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("[InstallWitnessAP] Waiting for config to load...");
            yield return null;
        }

        UnityEngine.Debug.Log("[InstallWitnessAP] Config loaded!");
        UnityEngine.Debug.Log("[InstallWitnessAP] witnessAP URL: " + witnessAP.url);

        if (string.IsNullOrEmpty(witnessAP.url))
        {
            UnityEngine.Debug.LogError("[InstallWitnessAP] ERROR: witnessAP.url is empty!");
            ShowInfo("ERROR: Witness Archipelago URL is not set!");
            yield break;
        }

        string exeFileName = "The.Witness.Randomizer.for.Archipelago.exe";
        string exePath = Path.Combine(witnessPath, exeFileName);

        UnityEngine.Debug.Log("[InstallWitnessAP] Downloading EXE from: " + witnessAP.url);
        UnityEngine.Debug.Log("[InstallWitnessAP] Saving to: " + exePath);

        yield return StartCoroutine(DownloadFile(witnessAP.url, exePath));

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("[InstallWitnessAP] Download failed: EXE not found at " + exePath);
            ShowInfo("ERROR: Witness Archipelago download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("[InstallWitnessAP] EXE downloaded successfully");
        ShowInfo("Witness Archipelago installed successfully!");
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
            remoteConfig = JsonUtility.FromJson<WitnessConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyWitnessConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        witnessPath = GetWitnessPath();
        UpdatePlatformStatus();
    }

    IEnumerator LaunchWitness()
    {
        string witnessExePath = Path.Combine(witnessPath, "witness64_d3d11.exe");

        if (!File.Exists(witnessExePath))
        {
            UnityEngine.Debug.LogWarning("Witness executable not found at: " + witnessExePath);
            ShowInfo("ERROR: Witness executable not found!");
            yield break;
        }

        UnityEngine.Debug.Log("Launching Witness: " + witnessExePath);

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = witnessExePath,
            WorkingDirectory = witnessPath,
            UseShellExecute = true
        };

        Process witnessProcess = null;

        try
        {
            witnessProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Witness launched successfully");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Witness: " + e.Message);
            ShowInfo("ERROR: Failed to launch Witness\n" + e.Message);
            yield break;
        }

        if (witnessProcess != null)
        {
            UnityEngine.Debug.Log("Waiting for Witness to close...");

            // Wait for the Witness process to exit
            while (!witnessProcess.HasExited)
            {
                yield return new WaitForSeconds(0.5f);
            }

            UnityEngine.Debug.Log("Witness has closed.");
            // Small pause to let file handles settle
            yield return new WaitForSeconds(0.5f);
        }
    }

    void LaunchAP()
    {
        // The method is retained but is no longer invoked automatically.
        string randomizeExePath = Path.Combine(witnessPath, "The.Witness.Randomizer.for.Archipelago.exe");

        if (!File.Exists(randomizeExePath))
        {
            UnityEngine.Debug.LogWarning("Witness Randomizer executable not found at: " + randomizeExePath);
            ShowInfo("ERROR: Witness Randomizer executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Launching Witness Randomizer: " + randomizeExePath);

        ProcessStartInfo startInfo = new ProcessStartInfo()
        {
            FileName = randomizeExePath,
            WorkingDirectory = witnessPath,
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
            UnityEngine.Debug.Log("Witness Randomizer launched successfully");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Witness Randomizer: " + e.Message);
            ShowInfo("ERROR: Failed to launch Witness Randomizer\n" + e.Message);
        }
    }

    // =========================================================
    // PATH DETECTION
    // =========================================================

    string GetWitnessPath()
    {
        if (isEpic)
            return GetWitnessEpicPath();
        else
            return GetWitnessSteamPath();
    }

    string GetWitnessSteamPath()
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

    string GetWitnessEpicPath()
    {
        string[] quickPaths = new string[]
       {
            @"C:\Program Files\Epic Games\TheWitness",
            @"C:\Games\Epic\TheWitness",
       };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        try
        {
            string epicBaseDir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
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
                        if (content.Contains("TheWitness") || content.Contains("TheWitness"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Game (Epic) at: " + epicPath);
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

        if (remoteConfig != null && remoteConfig.epicSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.epicSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string epicPath = Path.Combine(drive.Name, relativePath, epicGameFolderName);
                            if (Directory.Exists(epicPath))
                            {
                                UnityEngine.Debug.Log("Found Game (Epic, via remote config) at: " + epicPath);
                                return epicPath;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Epic) not found.");
        return "";
    }
}