using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class Overcooked2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("OVERCOOKED2 FILE")]
    public FileDownloader.FileData oc2AP; // ex: oc2-modding-v1.9.9.zip

    [Header("PLATFORM SELECTION")]
    public Button steamButton;
    public Button epicButton;
    public TextMeshProUGUI platformStatus;

    [Header("FEATURE TOGGLES")]
    public Toggle installPackageToggle; // controls downloading/extracting the zip
    public Toggle installModdingToggle; // runs oc2-modding-install.bat when enabled
    public Toggle launchGameToggle;     // launches Overcooked2.exe after winhttp.dll appears (only interactable if installModdingToggle is enabled)

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("TIMEOUTS / SETTINGS")]
    public float installWaitForWinhttpTimeout = 120f;
    public float uninstallTimeout = 120f;

    private string gameCorePath; // folder containing Overcooked2.exe
    private string pendingAction;
    private Process gameProcess;
    private Process installerProcess;
    private bool isEpic = false;
    private Overcooked2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class Overcooked2Config
    {
        public string oc2AP;
    }

    void Start()
    {
        // Platform buttons
        if (steamButton != null)
            steamButton.onClick.AddListener(OnSteamButtonClicked);
        if (epicButton != null)
            epicButton.onClick.AddListener(OnEpicButtonClicked);

        // default to Steam
        SelectSteam();

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

        // Defaults for toggles
        if (installPackageToggle != null)
            installPackageToggle.isOn = true;

        if (installModdingToggle != null)
            installModdingToggle.isOn = false;

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        // Wire toggle events
        if (installPackageToggle != null)
            installPackageToggle.onValueChanged.AddListener((v) => UpdateTogglesInteractable());

        if (installModdingToggle != null)
            installModdingToggle.onValueChanged.AddListener((v) => {
                if (launchGameToggle != null)
                    launchGameToggle.interactable = installModdingToggle.isOn && installModdingToggle.interactable;
            });

        StartCoroutine(LoadRemoteConfig());
        UpdateTogglesInteractable();
    }

    void ApplyOvercooked2Config()
    {
        if (remoteConfig == null)
            return;

        oc2AP.url = remoteConfig.oc2AP;
        oc2AP.fileName = "oc2-modding.zip";
    }

    // -----------------------
    // Platform selection
    // -----------------------
    void OnSteamButtonClicked() => SelectSteam();
    void OnEpicButtonClicked() => SelectEpic();

    void SelectSteam()
    {
        isEpic = false;
        gameCorePath = GetOvercooked2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Steam - Path: " + gameCorePath);
        UpdateTogglesInteractable();
    }

    void SelectEpic()
    {
        isEpic = true;
        gameCorePath = GetOvercooked2Path();
        UpdatePlatformStatus();
        UnityEngine.Debug.Log("Switched to Epic - Path: " + gameCorePath);
        UpdateTogglesInteractable();
    }

    void UpdatePlatformStatus()
    {
        if (platformStatus != null)
        {
            string platform = isEpic ? "Epic Games" : "Steam";
            string status = string.IsNullOrEmpty(gameCorePath) ? "Not Found" : "Found";
            platformStatus.text = $"Platform: {platform}\nStatus: {status}";
        }
    }

    // -----------------------
    // Toggles interactability
    // -----------------------
    void UpdateTogglesInteractable()
    {
        string targetPath = Path.Combine(gameCorePath ?? GetOvercooked2Path(), "oc2-modding-v1.9.9");
        bool folderExists = Directory.Exists(targetPath);

        if (installModdingToggle != null)
        {
            bool interactable = (installPackageToggle != null && installPackageToggle.isOn) || folderExists;
            installModdingToggle.interactable = interactable;
        }

        if (launchGameToggle != null)
        {
            launchGameToggle.interactable = (installModdingToggle != null && installModdingToggle.isOn && installModdingToggle.interactable);
        }
    }

    // -----------------------
    // UI & confirmation
    // -----------------------
    public void RunSetup()
    {
        ShowConfirmation("Install oc2-modding-v1.9.9 (if selected) and run installer if enabled?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Run oc2-modding-uninstall.bat (if present)", "Revert");
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

    // -----------------------
    // Setup / Revert logic
    // -----------------------
    private void ExecuteSetup()
    {
        gameCorePath = GetOvercooked2Path();

        if (string.IsNullOrEmpty(gameCorePath))
        {
            string platform = isEpic ? "Epic" : "Steam";
            ShowInfo("Overcooked 2 not found on " + platform + ". Please check installation.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // Revert requirements:
    // - Always search the game core (gameCorePath) for oc2-modding-uninstall.bat and run it if found.
    // - If oc2-modding-v1.9.9 exists, delete it (bonus), but its presence is NOT required to run the uninstaller.
    private void ExecuteRevert()
    {
        gameCorePath = GetOvercooked2Path();

        if (string.IsNullOrEmpty(gameCorePath))
        {
            ShowInfo("Overcooked 2 path not found. Cannot run uninstaller.");
            return;
        }

        string target = Path.Combine(gameCorePath, "oc2-modding-v1.9.9");

        // Search for .bat with priority: game root, then mod folder, then nested
        string[] possibleUninstallPaths = new string[]
        {
            Path.Combine(gameCorePath, "oc2-modding-uninstall.bat"), // primary: game core
            Path.Combine(target, "oc2-modding-uninstall.bat"),       // mod folder
            Path.Combine(target, "oc2-modding-v1.9.9", "oc2-modding-uninstall.bat") // nested fallback
        };

        string foundBat = null;
        foreach (var p in possibleUninstallPaths)
        {
            try
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    foundBat = p;
                    break;
                }
            }
            catch { }
        }

        if (foundBat != null)
        {
            ShowInfo("Starting oc2-modding-uninstall.bat...");
            Process proc = RunBatch(foundBat, gameCorePath);
            // pass both the bat path and the mod folder so coroutine can remove both after finish
            StartCoroutine(WaitForUninstallAndDelete(proc, foundBat, target));
        }
        else
        {
            // No .bat found in any of the expected places.
            // If the mod folder exists, delete it silently (bonus). Otherwise remain silent.
            if (Directory.Exists(target))
            {
                SafeDeleteDirectory(target);
                // Deletion silent on success
            }
            // else: nothing to do
        }
    }

    // InstallFlow: deletes oc2-modding-v1.9.9 ONLY at the end of setup when:
    // - installModdingToggle was active AND
    // - winhttp.dll was detected (installer success)
    // IMPORTANT: presence of the oc2-modding-v1.9.9 folder is NOT required for the flow to proceed.
    IEnumerator InstallFlow()
    {
        ShowInfo("Preparing installation...");

        string extractPath = Path.Combine(Application.persistentDataPath, "OC2ModdingTemp");
        string targetPath = Path.Combine(gameCorePath, "oc2-modding-v1.9.9");

        // If user requested to install the package (download+extract)
        if (installPackageToggle != null && installPackageToggle.isOn)
        {
            // Wait for config to load
            while (!configLoaded)
            {
                UnityEngine.Debug.Log("Waiting for config to load...");
                yield return new WaitForSeconds(0.5f);
            }

            ShowInfo("Downloading and extracting oc2-modding package...");
            yield return downloader.DownloadAndExtract(oc2AP, Application.persistentDataPath, extractPath);

            // Find oc2-modding-v1.9.9 folder in extracted content
            string modFolder = FindFolder(extractPath, "oc2-modding-v1.9.9");

            if (string.IsNullOrEmpty(modFolder))
            {
                UnityEngine.Debug.LogError("oc2-modding-v1.9.9 folder not found in package!");
                ShowInfo("ERROR: oc2-modding-v1.9.9 folder not found in package!");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            try
            {
                if (Directory.Exists(targetPath))
                    SafeDeleteDirectory(targetPath); // overwrite existing install before placing new

                // Move the folder into game core
                MoveDirectory(modFolder, targetPath);
                ShowInfo("oc2-modding-v1.9.9 placed in game core.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to move mod folder: " + e.Message);
                ShowInfo("ERROR: Failed to install mod folder\n" + e.Message);
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            // Clean up extraction temp
            SafeDeleteDirectory(extractPath);
        }
        else
        {
            // Package install was skipped. DO NOT block if the folder doesn't exist.
            // We will still attempt to run the installer .bat if requested, but if it's missing we only warn.
            if (installModdingToggle != null && installModdingToggle.isOn)
            {
                if (!Directory.Exists(targetPath))
                {
                    UnityEngine.Debug.LogWarning("Mod folder not present; continuing. Installer will be attempted but may not exist.");
                    ShowInfo("Mod folder not present; continuing. Installer will be attempted if present.");
                    // do NOT yield break here — continue to attempt installer
                }
            }
        }

        // If toggle enabled, run the install batch (attempt even if folder didn't previously exist)
        if (installModdingToggle != null && installModdingToggle.isOn)
        {
            // expected bat path: oc2-modding-v1.9.9\oc2-modding-v1.9.9\oc2-modding-install.bat or oc2-modding-v1.9.9\oc2-modding-install.bat
            string batPath = Path.Combine(targetPath, "oc2-modding-v1.9.9", "oc2-modding-install.bat");
            if (!File.Exists(batPath))
                batPath = Path.Combine(targetPath, "oc2-modding-install.bat");

            if (!File.Exists(batPath))
            {
                UnityEngine.Debug.LogWarning("Install .bat not found at expected locations.");
                ShowInfo("WARNING: oc2-modding-install.bat not found. Mod folder installed (if any) but automatic install skipped.");
            }
            else
            {
                ShowInfo("Running oc2-modding-install.bat...");
                Process proc = RunBatch(batPath, gameCorePath);

                // Monitor winhttp.dll creation
                string winhttpPath = Path.Combine(gameCorePath, "winhttp.dll");
                bool created = false;
                float timer = 0f;
                while (timer < installWaitForWinhttpTimeout)
                {
                    if (File.Exists(winhttpPath))
                    {
                        created = true;
                        break;
                    }

                    timer += 1f;
                    yield return new WaitForSeconds(1f);
                }

                if (created)
                {
                    ShowInfo("winhttp.dll detected.");

                    // ONLY remove the installed oc2-modding-v1.9.9 folder if the user requested the installer to run
                    // (installModdingToggle must be true - we are already inside that block) and winhttp.dll was found.
                    // Delete if it exists; if not, do nothing (do not treat absence as error).
                    try
                    {
                        string nested = Path.Combine(targetPath, "oc2-modding-v1.9.9");
                        if (Directory.Exists(nested))
                            SafeDeleteDirectory(nested);

                        if (Directory.Exists(targetPath))
                        {
                            SafeDeleteDirectory(targetPath);
                            ShowInfo("Mod installer folder removed after successful installation.");
                        }
                        else
                        {
                            // folder did not exist, that's fine — nothing to delete
                            UnityEngine.Debug.Log("oc2-modding-v1.9.9 did not exist at cleanup time; skipping deletion.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Failed to clean installer folder after success: " + e.Message);
                        ShowInfo("Installation succeeded but cleanup failed: " + e.Message);
                    }

                    // Launch game if toggle on
                    if (launchGameToggle != null && launchGameToggle.isOn)
                    {
                        string exePath = Path.Combine(gameCorePath, "Overcooked2.exe");
                        if (File.Exists(exePath))
                        {
                            ShowInfo("Launching Overcooked 2...");
                            LaunchGame(exePath);
                        }
                        else
                        {
                            ShowInfo("Overcooked2.exe not found; cannot launch game.");
                        }
                    }
                }
                else
                {
                    ShowInfo("Timeout waiting for winhttp.dll. Check installer output and try again. The oc2-modding-v1.9.9 folder was not removed.");
                }
            }
        }
        else
        {
            ShowInfo("Mod folder placement complete (automatic installer not run).");
        }

        UpdateTogglesInteractable();
    }

    // Wait for uninstaller to finish, then attempt to delete the uninstall .bat and the mod folder (if present).
    IEnumerator WaitForUninstallAndDelete(Process proc, string batPath, string targetFolder)
    {
        // Remember whether winhttp.dll existed before revert started
        string winhttpPath = Path.Combine(gameCorePath ?? GetOvercooked2Path(), "winhttp.dll");
        bool winhttpExistedBefore = File.Exists(winhttpPath);

        float timer = 0f;
        if (proc != null)
        {
            while (!proc.HasExited && timer < uninstallTimeout)
            {
                timer += 1f;
                yield return new WaitForSeconds(1f);
            }

            if (!proc.HasExited)
            {
                UnityEngine.Debug.LogWarning("Uninstall process did not exit within timeout.");
                ShowInfo("Uninstaller still running (timeout). Please finish it manually.");
                yield break;
            }
        }

        // small delay to let system release handles
        yield return new WaitForSeconds(0.5f);

        // 1) attempt to delete the .bat (if present) with retries
        if (!string.IsNullOrEmpty(batPath))
        {
            float delTimer = 0f;
            bool batDeleted = false;
            while (File.Exists(batPath) && delTimer < 6f)
            {
                try
                {
                    File.SetAttributes(batPath, FileAttributes.Normal);
                    File.Delete(batPath);

                    if (!File.Exists(batPath))
                    {
                        batDeleted = true;
                        break;
                    }
                }
                catch { }

                delTimer += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (!batDeleted && File.Exists(batPath))
            {
                UnityEngine.Debug.LogWarning("Could not delete uninstall .bat: " + batPath);
                ShowInfo("Uninstaller finished but could not remove uninstall .bat. Please remove it manually.");
            }
            // if deleted, keep silent
        }

        // 2) attempt to delete the mod folder (if present) with retries
        if (!string.IsNullOrEmpty(targetFolder))
        {
            float delTimer2 = 0f;
            bool folderDeleted = false;
            while (Directory.Exists(targetFolder) && delTimer2 < 6f)
            {
                try
                {
                    SafeDeleteDirectory(targetFolder);
                    if (!Directory.Exists(targetFolder))
                    {
                        folderDeleted = true;
                        break;
                    }
                }
                catch { }

                delTimer2 += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (!folderDeleted && Directory.Exists(targetFolder))
            {
                UnityEngine.Debug.LogWarning("Could not delete mod folder after uninstall: " + targetFolder);
                ShowInfo("Uninstaller finished but could not remove mod folder. Please remove it manually.");
            }
            // if deleted, keep silent
        }

        // After cleanup attempts, if winhttp.dll existed before and is now gone => show "Revert complete!"
        bool winhttpExistsNow = File.Exists(winhttpPath);
        if (winhttpExistedBefore && !winhttpExistsNow)
        {
            ShowInfo("Revert complete!");
        }

        UpdateTogglesInteractable();
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
            remoteConfig = JsonUtility.FromJson<Overcooked2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyOvercooked2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    Process RunBatch(string batPath, string workingDirectory)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = batPath;
            psi.WorkingDirectory = workingDirectory;
            psi.UseShellExecute = true; // let the .bat run with default shell
            psi.CreateNoWindow = false;
            return Process.Start(psi);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to start .bat: " + e.Message);
            ShowInfo("ERROR: Failed to start .bat\n" + e.Message);
            return null;
        }
    }

    void LaunchGame(string exePath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            psi.WorkingDirectory = Path.GetDirectoryName(exePath);
            psi.UseShellExecute = true;
            gameProcess = Process.Start(psi);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch game: " + e.Message);
            ShowInfo("ERROR: Failed to launch game\n" + e.Message);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete directory " + path + " - " + e.Message);
        }
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

    string FindFolder(string root, string folderName)
    {
        if (!Directory.Exists(root))
            return "";

        // check root itself
        string candidate = Path.Combine(root, folderName);
        if (Directory.Exists(candidate))
            return candidate;

        // search recursively
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(dir).Equals(folderName))
                return dir;
        }

        return "";
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

    // -----------------------
    // Path detection
    // -----------------------
    string GetOvercooked2Path()
    {
        return isEpic ? GetOvercooked2EpicPath() : GetOvercooked2SteamPath();
    }

    string GetOvercooked2SteamPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Overcooked! 2"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Overcooked! 2"),
            @"D:\Steam\steamapps\common\Overcooked! 2",
            @"D:\SteamLibrary\steamapps\common\Overcooked! 2",
            @"E:\Steam\steamapps\common\Overcooked! 2",
            @"E:\SteamLibrary\steamapps\common\Overcooked! 2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Overcooked! 2 (Steam) at: " + path);
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
                    string subPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Overcooked! 2");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Overcooked! 2");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "steamapps", "common", "Overcooked! 2");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Overcooked! 2");
                    if (Directory.Exists(subPath))
                        return subPath;

                    subPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Overcooked! 2");
                    if (Directory.Exists(subPath))
                        return subPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Overcooked! 2 (Steam) not found.");
        return "";
    }

    string GetOvercooked2EpicPath()
    {
        string[] quickPaths = new string[]
        {
            @"C:\Program Files\Epic Games\Overcooked2",
            @"D:\Epic Games\Overcooked2",
            @"E:\Epic Games\Overcooked2",
            @"C:\Games\Epic\Overcooked2",
            @"D:\Games\Epic\Overcooked2",
            @"E:\Games\Epic\Overcooked2",
            @"C:\Epic\Overcooked2",
            @"D:\Epic\Overcooked2",
            @"E:\Epic\Overcooked2",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Overcooked2 (Epic) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        // Search Epic manifests for install location
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
                        if (content.Contains("Overcooked") || content.Contains("Overcooked2"))
                        {
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"""InstallLocation"":""([^""]+)""");
                            System.Text.RegularExpressions.Match match = regex.Match(content);

                            if (match.Success)
                            {
                                string epicPath = match.Groups[1].Value;
                                if (Directory.Exists(epicPath))
                                {
                                    UnityEngine.Debug.Log("Found Overcooked2 (Epic) via manifest at: " + epicPath);
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

        // Scan drives
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    string epicPath = Path.Combine(drive.Name, "Epic Games", "Overcooked2");
                    if (Directory.Exists(epicPath))
                        return epicPath;

                    epicPath = Path.Combine(drive.Name, "Games", "Epic", "Overcooked2");
                    if (Directory.Exists(epicPath))
                        return epicPath;
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Overcooked2 (Epic) not found.");
        return "";
    }
}
