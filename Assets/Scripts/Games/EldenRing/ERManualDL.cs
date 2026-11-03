using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class ERManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData eldenringApworld;
    public FileDownloader.FileData eldenringAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "ELDEN RING";
    public string randomizerFolderName = "randomizer";
    public string randomizerExeName = "EldenRingArchipelagoRandomizer.exe";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPToggle;
    public Toggle installApworldToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private string randomizerInstallPath;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApInstallSuccess = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class GameConfig
    {
        public string eldenringApworld;
        public string eldenringAP;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        gamePath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installAPToggle != null)
            installAPToggle.isOn = true;

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        eldenringApworld.url = remoteConfig.eldenringApworld;
        eldenringAP.url = remoteConfig.eldenringAP;
    }

    // ---------------------------------------------------------------
    // BUTTONS
    // ---------------------------------------------------------------

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

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool doAP = installAPToggle == null || installAPToggle.isOn;
        bool doApworld = installApworldToggle == null || installApworldToggle.isOn;

        if (!doAP && !doApworld)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (doAP && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(InstallFlow(doAP, doApworld));
    }

    IEnumerator InstallFlow(bool installAP, bool installApworld)
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.2f);

        lastApInstallSuccess = false;
        lastApWorldInstallSuccess = false;

        if (installAP)
        {
            yield return InstallRandomizer();
            yield return new WaitForSeconds(0.2f);
        }

        if (installApworld)
        {
            yield return InstallAPWorld();
            yield return new WaitForSeconds(0.2f);
        }

        // si une des installations demandees a echoue, on garde le message d'erreur affiche
        if ((installAP && !lastApInstallSuccess) || (installApworld && !lastApWorldInstallSuccess))
            yield break;

        if (installAP)
        {
            try
            {
                CreateVersionFile(eldenringAP.url, installApworld ? eldenringApworld.url : "");
            }
            catch { }

            ShowInfo("Launching Elden Ring Archipelago Randomizer...");
            yield return new WaitForSeconds(0.5f);
            LaunchRandomizer();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallRandomizer()
    {
        lastApInstallSuccess = false;

        while (!configLoaded)
            yield return new WaitForSeconds(0.2f);

        if (eldenringAP == null || string.IsNullOrEmpty(eldenringAP.url))
        {
            ShowInfo("ERROR: Randomizer URL is empty!");
            UnityEngine.Debug.LogError("Randomizer URL not set!");
            yield break;
        }

        string fileName = eldenringAP.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = eldenringAP.url.Substring(eldenringAP.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "ERRandomizerTemp");

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("Downloading Randomizer from: " + eldenringAP.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(eldenringAP.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Randomizer download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("Extracting Randomizer zip...");
        yield return downloader.DownloadAndExtract(eldenringAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extraction failed: " + extractPath + " not found");
            ShowInfo("ERROR: Randomizer extraction failed!");
            DeleteTempFile(localPath);
            yield break;
        }

        try
        {
            string[] dirs = Directory.GetDirectories(extractPath);
            if (dirs.Length == 0)
            {
                ShowInfo("ERROR: No folder found inside the Randomizer zip!");
                UnityEngine.Debug.LogError("No folder found inside " + extractPath);
                DeleteTempFile(localPath);
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            string sourceDir = dirs.FirstOrDefault(d =>
                string.Equals(Path.GetFileName(d), randomizerFolderName, System.StringComparison.OrdinalIgnoreCase))
                ?? dirs[0];

            string folderName = Path.GetFileName(sourceDir);
            string targetDir = Path.Combine(gamePath, folderName);

            if (!string.Equals(folderName, randomizerFolderName, System.StringComparison.OrdinalIgnoreCase))
                UnityEngine.Debug.LogWarning("Folder in zip is '" + folderName + "' but revert deletes '" + randomizerFolderName + "'");

            CopyDirectory(sourceDir, targetDir);
            randomizerInstallPath = targetDir;

            UnityEngine.Debug.Log("Randomizer installed to: " + targetDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing Randomizer: " + e.Message);
            ShowInfo("ERROR: Failed to install Randomizer\n" + e.Message);
            DeleteTempFile(localPath);
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        DeleteTempFile(localPath);

        lastApInstallSuccess = true;
        ShowInfo("Randomizer installed successfully!");
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (eldenringApworld == null || string.IsNullOrEmpty(eldenringApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = eldenringApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = eldenringApworld.url.Substring(eldenringApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + eldenringApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(eldenringApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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
        }

        DeleteTempFile(localPath);
    }

    // ---------------------------------------------------------------
    // REVERT
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(RevertAsync());
    }

    IEnumerator RevertAsync()
    {
        ShowInfo("Removing randomizer...");

        CloseRandomizer();
        yield return new WaitForSeconds(0.5f);

        string randomizerPath = Path.Combine(gamePath, randomizerFolderName);

        if (!Directory.Exists(randomizerPath))
        {
            ShowInfo("Nothing to remove: randomizer folder not found.");
            yield break;
        }

        try
        {
            Directory.Delete(randomizerPath, true);
            UnityEngine.Debug.Log("Deleted randomizer folder: " + randomizerPath);
            ShowInfo("Randomizer removed!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to delete randomizer folder: " + e.Message);
            ShowInfo("Error while removing randomizer:\n" + e.Message);
        }
    }

    // ---------------------------------------------------------------
    // DOWNLOAD / CONFIG
    // ---------------------------------------------------------------

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
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
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
        UnityEngine.Debug.Log("Config marked as loaded");
    }

    // ---------------------------------------------------------------
    // LAUNCH / CLOSE
    // ---------------------------------------------------------------

    void LaunchRandomizer()
    {
        string folder = !string.IsNullOrEmpty(randomizerInstallPath)
            ? randomizerInstallPath
            : Path.Combine(gamePath, randomizerFolderName);

        string exePath = Path.Combine(folder, randomizerExeName);

        if (!File.Exists(exePath))
        {
            ShowInfo(randomizerExeName + " not found at:\n" + exePath);
            UnityEngine.Debug.LogError(randomizerExeName + " not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = folder,
                UseShellExecute = true
            };
            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Randomizer launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Randomizer:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void CloseRandomizer()
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

        // au cas ou le randomizer a ete lance en dehors de l'outil (il verrouillerait le dossier)
        try
        {
            string processName = Path.GetFileNameWithoutExtension(randomizerExeName);
            foreach (Process p in Process.GetProcessesByName(processName))
            {
                try { p.Kill(); p.Dispose(); } catch { }
            }
        }
        catch { }
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dest = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
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

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
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
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    if (drive.DriveType != DriveType.Fixed)
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

    void CreateVersionFile(string apUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);

            string folder = !string.IsNullOrEmpty(randomizerInstallPath)
                ? randomizerInstallPath
                : Path.Combine(gamePath, randomizerFolderName);

            if (!Directory.Exists(folder))
                return;

            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== ELDEN RING ARCHIPELAGO ===\n";
            content += "AP file: " + apUrl + "\n";
            content += "AP Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "apworld: " + apworldUrl + "\n";
            content += "apworld Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles(folder);

            string versionPath = Path.Combine(folder, "ER Archipelago Version " + apVersion + ".txt");
            File.WriteAllText(versionPath, content);
            UnityEngine.Debug.Log("Version file created: " + versionPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ER Archipelago Version .+\.txt");

            foreach (string file in Directory.GetFiles(folder))
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

        string fileName = url.Substring(url.LastIndexOf('/') + 1);
        if (fileName.Contains("?"))
            fileName = fileName.Substring(0, fileName.IndexOf("?"));
        return fileName.Length > 0 ? fileName : "Unknown";
    }

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    if (drive.DriveType != DriveType.Fixed)
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