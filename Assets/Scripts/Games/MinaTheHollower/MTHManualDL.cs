using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class MTHManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData mthAP;
    public FileDownloader.FileData mthApworld;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Mina the Hollower";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installModsToggle;

    [Header("LAUNCH OPTIONS")]
    public string steamLaunchArguments = "-mod -mod-allow-code";

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
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    private const string APCLIENT_FOLDER_NAME = "apclient";
    private const string VERSION_FILE_PATTERN = @"MTH AP Mod Version .+\.txt";
    private const string VERSION_FILE_PREFIX = "MTH AP Mod Version ";

    [System.Serializable]
    public class GameConfig
    {
        public string mthAP;
        public string mthApworld;
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

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installModsToggle != null)
            installModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void CleanupProcesses()
    {
        CloseGame();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        mthAP.url = remoteConfig.mthAP;
        mthApworld.url = remoteConfig.mthApworld;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install all the files?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert and remove all mods?", "Revert");
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
        gamePath = GetGamePath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ap = installModsToggle == null || installModsToggle.isOn;

        int count = (apworld ? 1 : 0) + (ap ? 1 : 0);

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

        if (ap && count == 1)
        {
            StartCoroutine(ModsOnlyFlow());
            return;
        }

        StartCoroutine(SetupFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupFlow()
    {
        ShowInfo("Downloading and installing files...");
        yield return new WaitForSeconds(0.5f);

        yield return InstallFlow();

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        CleanupProcesses();
        StartCoroutine(RemoveApClientAsync());
    }

    IEnumerator RemoveApClientAsync()
    {
        ShowInfo("Reverting modifications...");
        yield return new WaitForSeconds(0.5f);

        string apclientPath = Path.Combine(GetModsPath(), APCLIENT_FOLDER_NAME);

        bool apclientExisted = Directory.Exists(apclientPath);

        SafeDeleteDirectory(apclientPath);

        DeleteOldVersionFiles();

        if (apclientExisted)
            ShowInfo("Mods removed successfully!");
        else
            ShowInfo("Nothing to revert: apclient folder was not found.");

        UnityEngine.Debug.Log("Revert complete: apclient folder removed (" + apclientPath + ")");
    }

    IEnumerator ModsOnlyFlow()
    {
        yield return InstallApClient();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installModsToggle == null || installModsToggle.isOn)
            yield return InstallApClient();

        if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            CreateVersionFile(mthAP.url, mthApworld.url);

        yield return new WaitForSeconds(2f);
    }

    // =========================================================
    // MTH AP CLIENT (zip -> AppData\Roaming\Yacht Club Games\Mina the Hollower\mods\apclient)
    // =========================================================

    IEnumerator InstallApClient()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(mthAP.url))
        {
            ShowInfo("ERROR: AP client URL is empty!");
            UnityEngine.Debug.LogError("mthAP URL not set!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "MTHApClientTemp");

        yield return downloader.DownloadAndExtract(mthAP, Application.persistentDataPath, extractPath);

        string apclientSourcePath = Path.Combine(extractPath, APCLIENT_FOLDER_NAME);

        if (!Directory.Exists(apclientSourcePath))
        {
            ShowInfo("ERROR: apclient folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string modsPath = GetModsPath();
        Directory.CreateDirectory(modsPath);

        string apclientTargetPath = Path.Combine(modsPath, APCLIENT_FOLDER_NAME);

        // On repart d'un dossier apclient propre à chaque installation
        SafeDeleteDirectory(apclientTargetPath);
        Directory.CreateDirectory(apclientTargetPath);

        MoveDirectory(apclientSourcePath, apclientTargetPath);

        if (!VerifyApClientFiles(apclientTargetPath))
        {
            ShowInfo("WARNING: Some AP client files may be missing!");
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("AP client installed successfully!");
        yield return new WaitForSeconds(1f);
    }

    bool VerifyApClientFiles(string apclientPath)
    {
        if (!Directory.Exists(apclientPath))
        {
            UnityEngine.Debug.LogError("apclient folder not found: " + apclientPath);
            return false;
        }

        string[] items = Directory.GetFileSystemEntries(apclientPath);
        if (items.Length == 0)
        {
            UnityEngine.Debug.LogError("apclient folder is empty!");
            return false;
        }

        UnityEngine.Debug.Log("apclient folder verified with " + items.Length + " items!");
        return true;
    }

    string GetModsPath()
    {
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Yacht Club Games", "Mina the Hollower", "mods");
    }

    // =========================================================
    // APWORLD (mina_the_hollower.apworld)
    // =========================================================

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + mthApworld.url);

        if (string.IsNullOrEmpty(mthApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = mthApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = mthApworld.url.Substring(mthApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        if (string.IsNullOrEmpty(fileName))
            fileName = "mina_the_hollower.apworld";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + mthApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(mthApworld.url, localPath);

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

    // =========================================================
    // LANCEMENT DU JEU
    // =========================================================

    private const string STEAM_APP_ID = "1875580";

    /// <summary>
    /// Fonction publique à assigner sur le OnClick de n'importe quel bouton
    /// pour lancer Mina the Hollower via Steam (comme un lancement normal
    /// depuis la bibliothèque Steam), avec les options de lancement
    /// "-mod -mod-allow-code".
    /// </summary>
    public void LaunchGameButton()
    {
        LaunchGame();
    }

    void LaunchGame()
    {
        string currentGamePath = GetGamePath();
        UnityEngine.Debug.Log("LaunchGame called. GamePath: " + currentGamePath);

        if (string.IsNullOrEmpty(currentGamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty!");
            return;
        }

        string steamUrl = "steam://run/" + STEAM_APP_ID;

        if (!string.IsNullOrEmpty(steamLaunchArguments))
            steamUrl += "//" + steamLaunchArguments + "/";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = steamUrl,
            UseShellExecute = true
        };

        try
        {
            UnityEngine.Debug.Log("Launching via Steam: " + steamUrl);

            gameProcess = Process.Start(startInfo);
            UnityEngine.Debug.Log("Steam launch command sent successfully.");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game via Steam:\n" + e.Message);
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

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

    void CreateVersionFile(string apUrl, string apworldUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = VERSION_FILE_PREFIX + apVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP CLIENT (apclient) ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string coreVersionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(coreVersionPath, content);
            UnityEngine.Debug.Log("Version file created: " + coreVersionPath);
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
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return;

            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(VERSION_FILE_PATTERN);

            string[] rootFiles = Directory.GetFiles(gamePath);
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

        // Pattern pour GitHub releases: /releases/download/VERSION/
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        return "Unknown";
    }
}