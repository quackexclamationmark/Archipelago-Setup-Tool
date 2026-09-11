using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class DOOMManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData doomAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Ultimate Doom";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("WAD COPY (separate button, not part of the setup flow)")]
    public Button copyWadFilesButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    private string DoomArchipelagoPath =>
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "DOOM & Heretic AP");

    [System.Serializable]
    public class GameConfig
    {
        public string doomAP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (copyWadFilesButton != null)
            copyWadFilesButton.onClick.AddListener(CopyWadFiles);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        doomAP.url = remoteConfig.doomAP;
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
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        bool installAPMod = installAPModToggle == null || installAPModToggle.isOn;

        if (!installAPMod)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        StartCoroutine(SetupCoroutine());
    }

    IEnumerator SetupCoroutine()
    {
        ShowInfo("Downloading and installing files...");

        yield return InstallFlow();

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        if (!Directory.Exists(DoomArchipelagoPath))
        {
            ShowInfo("DOOM Archipelago folder found.\nPlease setup again if needed.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting modifications...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            if (Directory.Exists(DoomArchipelagoPath))
            {
                Directory.Delete(DoomArchipelagoPath, true);
                UnityEngine.Debug.Log("Deleted DOOM Archipelago folder: " + DoomArchipelagoPath);
            }

            ShowInfo("Revert complete!");
            UnityEngine.Debug.Log("Revert complete.");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator InstallFlow()
    {
        bool installAPMod = installAPModToggle == null || installAPModToggle.isOn;

        if (installAPMod)
        {
            yield return InstallDoomArchipelago();
        }

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallDoomArchipelago()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "DOOMModFilesTemp");

        yield return downloader.DownloadAndExtract(doomAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Extraction failed, folder not found!");
            yield break;
        }

        Directory.CreateDirectory(DoomArchipelagoPath);

        MoveDirectoryContents(extractPath, DoomArchipelagoPath);

        CreateVersionFile(doomAP.url);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    // =========================================================
    // WAD COPY — dedicated to a SEPARATE button, not part of RunSetup/RevertAll
    // =========================================================

    public void CopyWadFiles()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Ultimate Doom install not found. Please check your Steam installation.");
            return;
        }

        string doomWadSource = Path.Combine(gamePath, "base", "DOOM.wad");
        string doom2WadSource = Path.Combine(gamePath, "base", "doom2", "DOOM2.wad");

        if (!File.Exists(doomWadSource))
        {
            ShowInfo("DOOM.wad not found at:\n" + doomWadSource);
            return;
        }

        if (!File.Exists(doom2WadSource))
        {
            ShowInfo("DOOM2.wad not found at:\n" + doom2WadSource);
            return;
        }

        try
        {
            Directory.CreateDirectory(DoomArchipelagoPath);

            string doomWadTarget = Path.Combine(DoomArchipelagoPath, "DOOM.wad");
            string doom2WadTarget = Path.Combine(DoomArchipelagoPath, "DOOM2.wad");

            File.Copy(doomWadSource, doomWadTarget, true);
            File.Copy(doom2WadSource, doom2WadTarget, true);

            ShowInfo("WAD files copied successfully!");
            UnityEngine.Debug.Log("Copied DOOM.wad and DOOM2.wad to: " + DoomArchipelagoPath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error copying WAD files:\n" + e.Message);
            UnityEngine.Debug.LogError("WAD copy error: " + e);
        }
    }

    void LaunchGame()
    {
        string exePath = Path.Combine(DoomArchipelagoPath, "apdoom-launcher.exe");

        UnityEngine.Debug.Log("LaunchGame called. ExePath: " + exePath);

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found. Checked:\n" + exePath);
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            gameProcess = Process.Start(exePath);
            UnityEngine.Debug.Log("Game launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseGame()
    {
        try
        {
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
            }
        }
        catch { }
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

    // Moves ALL contents (files + subfolders) of "source" directly into "target",
    // preserving the relative structure, and tracks every moved file for revert.
    void MoveDirectoryContents(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(source.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    string GetGamePath()
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

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string modsUrl)
    {
        try
        {
            string modsVersion = ExtractVersionFromUrl(modsUrl, "");

            string versionFileName = "DOOM APMod Version " + modsVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== RANDOMIZER MOD ===\n";
            content += "Downloaded from: " + modsUrl + "\n";
            content += "Version: " + modsVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string versionPath = Path.Combine(DoomArchipelagoPath, versionFileName);
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
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
            if (!Directory.Exists(DoomArchipelagoPath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DOOM APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(DoomArchipelagoPath);
            foreach (string file in rootFiles)
            {
                string fileName = Path.GetFileName(file);
                if (pattern.IsMatch(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted old version file: " + fileName);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete old version file: " + e.Message);
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
        // Pattern pour Thunderstore: https://thunderstore.io/package/download/Author/Package/VERSION/
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

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Config load failed: " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGameConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
        }

        configLoaded = true;

        gamePath = GetGamePath();
    }
}