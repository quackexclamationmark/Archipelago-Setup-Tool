using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class COE33ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData modFiles;
    public FileDownloader.FileData apworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installModsToggle;

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

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string clairobscurAP;
        public string clairobscurApworld;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
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

        modFiles.url = remoteConfig.clairobscurAP;
        apworld.url = remoteConfig.clairobscurApworld;
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
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Please check Steam installation.");
            return;
        }

        // ✅ FLOWS individuels
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool mods = installModsToggle == null || installModsToggle.isOn;

        int count = (apworld ? 1 : 0) + (mods ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (mods && count == 1)
        {
            StartCoroutine(ModsOnlyFlow());
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator SetupWithTracking()
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow();

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found. Cannot revert.\nPlease reinstall the game if needed.");
            return;
        }

        CleanupProcesses();
        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");

        if (!File.Exists(manifestPath))
        {
            ShowInfo("No installation record found.");
            yield break;
        }

        ShowInfo("Reverting modifications...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            string json = File.ReadAllText(manifestPath);
            InstalledFilesManifest manifest = JsonUtility.FromJson<InstalledFilesManifest>(json);

            if (string.IsNullOrEmpty(manifest.gameInstallPath))
            {
                ShowInfo("Error: Game install path not found in manifest.");
                yield break;
            }

            int successCount = 0;
            int errorCount = 0;

            foreach (string filePath in manifest.installedFiles)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        successCount++;
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Failed to delete: " + filePath + " - " + e.Message);
                        errorCount++;
                    }
                }
            }

            RemoveEmptyDirectories(Path.Combine(manifest.gameInstallPath, "Sandfall", "Content"));
            RemoveEmptyDirectories(Path.Combine(manifest.gameInstallPath, "Sandfall", "Binaries"));

            DeleteOldVersionFiles();

            ShowInfo($"Mods removed successfully!\n({successCount} files deleted)");
            UnityEngine.Debug.Log($"Revert complete: {successCount} files deleted, {errorCount} errors");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
        finally
        {
            try
            {
                File.Delete(manifestPath);
            }
            catch { }
        }
    }

    void RemoveEmptyDirectories(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir);
                }
            }
        }
        catch { }
    }

    IEnumerator APWorldOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator ModsOnlyFlow()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            yield break;

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return InstallMods();

        SaveInstalledFilesManifest(currentManifest);

        if (launchGameToggle == null || launchGameToggle.isOn)
            LaunchGame();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installModsToggle == null || installModsToggle.isOn)
            yield return InstallMods();

        CreateVersionFile(modFiles.url, apworld.url);

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
    }

    IEnumerator InstallMods()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "ModFilesTemp");

        yield return downloader.DownloadAndExtract(modFiles, Application.persistentDataPath, extractPath);

        string sandfallPath = Path.Combine(extractPath, "Sandfall");

        if (!Directory.Exists(sandfallPath))
        {
            ShowInfo("ERROR: Sandfall folder not found in extraction!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Installe Content
        string contentSource = Path.Combine(sandfallPath, "Content");
        if (Directory.Exists(contentSource))
        {
            string contentTarget = Path.Combine(gamePath, "Sandfall", "Content");
            MoveDirectoryAndTrack(contentSource, contentTarget);

            if (!VerifyRandomizerPaks(contentTarget))
            {
                ShowInfo("WARNING: RandomizerMods pak files not found!");
            }
        }

        // Installe Binaries
        string binariesSource = Path.Combine(sandfallPath, "Binaries");
        if (Directory.Exists(binariesSource))
        {
            string binariesTarget = Path.Combine(gamePath, "Sandfall", "Binaries");
            MoveDirectoryAndTrack(binariesSource, binariesTarget);

            if (!VerifyUE4SS(binariesTarget))
            {
                ShowInfo("WARNING: UE4SS folder not found!");
            }
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("Installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    bool VerifyRandomizerPaks(string contentPath)
    {
        string[] requiredFiles = new string[]
        {
            "ClairObscurRandomizer.pak",
            "ClairObscurRandomizer.ucas",
            "ClairObscurRandomizer.utoc"
        };

        string logicModsPath = Path.Combine(contentPath, "Paks", "LogicMods");

        if (!Directory.Exists(logicModsPath))
        {
            UnityEngine.Debug.LogError("LogicMods folder not found: " + logicModsPath);
            return false;
        }

        foreach (string fileName in requiredFiles)
        {
            string filePath = Path.Combine(logicModsPath, fileName);
            if (!File.Exists(filePath))
            {
                UnityEngine.Debug.LogError("Missing file: " + filePath);
                return false;
            }
        }

        UnityEngine.Debug.Log("✓ All RandomizerMods pak files verified!");
        return true;
    }

    bool VerifyUE4SS(string binariesPath)
    {
        string[] platformFolders = new string[] { "Win64", "WinGDK" };

        foreach (string platform in platformFolders)
        {
            string ue4ssPath = Path.Combine(binariesPath, platform, "ue4ss");

            if (Directory.Exists(ue4ssPath))
            {
                UnityEngine.Debug.Log("✓ UE4SS folder verified in: " + platform);
                return true;
            }
        }

        UnityEngine.Debug.LogError("UE4SS folder not found in Win64 or WinGDK!");
        return false;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + apworld.url);

        if (string.IsNullOrEmpty(apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        // ✅ Télécharge directement avec UnityWebRequest
        yield return DownloadFile(apworld.url, localPath);

        // Vérifie que le fichier existe
        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // ✅ Cibles possibles
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

        // Nettoie l'ancien fichier s'il existe
        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                UnityEngine.Debug.Log("Deleted old apworld file");
            }
            catch { }
        }

        // Copie le fichier téléchargé
        try
        {
            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
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
        UnityEngine.Debug.Log("Config marked as loaded");
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

        string[] possiblePaths = new string[]
        {
            Path.Combine(currentGamePath, "Expedition33_Steam.exe"),
            Path.Combine(currentGamePath, "Binaries", "Win64", "Expedition33-Win64-Shipping.exe"),
        };

        string exePath = "";
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                exePath = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            ShowInfo("Game executable not found. Checked:\n" + string.Join("\n", possiblePaths));
            UnityEngine.Debug.LogError("Executable not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking exe at: " + exePath);

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

    void MoveDirectoryAndTrack(string source, string target)
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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(dest);
        }
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "InstalledFilesManifest.json");
        string json = JsonUtility.ToJson(manifest, true);

        try
        {
            File.WriteAllText(manifestPath, json);
            UnityEngine.Debug.Log("Installation manifest saved: " + manifestPath);
            UnityEngine.Debug.Log("Tracked " + manifest.installedFiles.Count + " files for future revert");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to save manifest: " + e.Message);
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
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Expedition 33"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Expedition 33"),
        @"D:\Steam\steamapps\common\Expedition 33",
        @"D:\SteamLibrary\steamapps\common\Expedition 33",
        @"D:\steamapps\common\Expedition 33",
        @"E:\Steam\steamapps\common\Expedition 33",
        @"E:\SteamLibrary\steamapps\common\Expedition 33",
        @"E:\steamapps\common\Expedition 33",
        @"E:\Program Files (x86)\steamapps\common\Expedition 33",
        @"E:\Program Files\steamapps\common\Expedition 33",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                    return path;
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
                    // Cherche Steam\steamapps
                    string gamePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Expedition 33");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Cherche SteamLibrary\steamapps
                    gamePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Expedition 33");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Cherche directement steamapps à la racine du disque
                    gamePath = Path.Combine(drive.Name, "steamapps", "common", "Expedition 33");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Cherche dans Program Files (x86)\steamapps
                    gamePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Expedition 33");
                    if (Directory.Exists(gamePath))
                        return gamePath;

                    // Cherche dans Program Files\steamapps
                    gamePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Expedition 33");
                    if (Directory.Exists(gamePath))
                        return gamePath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string modFilesUrl, string apworldUrl)
    {
        try
        {
            string modFilesVersion = ExtractVersionFromUrl(modFilesUrl, "");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, "");

            string versionFileName = "ClairObscur APMod Version " + modFilesVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== MOD FILES ===\n";
            content += "Downloaded from: " + modFilesUrl + "\n";
            content += "Version: " + modFilesVersion + "\n";
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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(coreVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"ClairObscur APMod Version .+\.txt");

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