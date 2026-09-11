using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class CyberpunkManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;

    [Header("FEATURE TOGGLES")]
    public FileDownloader.FileData redScript;
    public FileDownloader.FileData tweakXL;
    public FileDownloader.FileData archiveXL;
    public FileDownloader.FileData cet;
    public FileDownloader.FileData codeware;
    public FileDownloader.FileData red4ext;
    public FileDownloader.FileData phone;

    [Header("GAME FOLDER NAME")]
    public string steamGameFolderName = "Cyberpunk 2077";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installModsToggle;
    public Toggle installDependenciesToggle;

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
    private bool lastApWorldInstallSuccess = false;
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string cyberpunk2077Apworld;
        public string cyberpunk2077AP;
        public string cyberpunk2077RedScript;
        public string cyberpunk2077TweakXL;
        public string cyberpunk2077ArchiveXL;
        public string cyberpunk2077CET;
        public string cyberpunk2077Codeware;
        public string cyberpunk2077RED4ext;
        public string cyberpunk2077Phone;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
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

        if (installDependenciesToggle != null)
            installDependenciesToggle.isOn = true;

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

        apworld.url = remoteConfig.cyberpunk2077Apworld;
        apMod.url = remoteConfig.cyberpunk2077AP;
        redScript.url = remoteConfig.cyberpunk2077RedScript;
        tweakXL.url = remoteConfig.cyberpunk2077TweakXL;
        archiveXL.url = remoteConfig.cyberpunk2077ArchiveXL;
        cet.url = remoteConfig.cyberpunk2077CET;
        codeware.url = remoteConfig.cyberpunk2077Codeware;
        red4ext.url = remoteConfig.cyberpunk2077RED4ext;
        phone.url = remoteConfig.cyberpunk2077Phone;
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

        bool Apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool Mods = installModsToggle == null || installModsToggle.isOn;
        bool Dependencies = installDependenciesToggle == null || installDependenciesToggle.isOn;

        bool needsGamePath = Mods || Dependencies;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (Apworld ? 1 : 0) + (Mods ? 1 : 0) + (Dependencies ? 1 : 0);

        if (Apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupWithTracking(Apworld, Mods, Dependencies));
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupWithTracking(bool wantApworld, bool wantMods, bool wantDependencies)
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        yield return new WaitUntil(() => configLoaded);

        if (wantApworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (wantMods)
        {
            ShowInfo("Installing Archipelago mod...");
            yield return InstallZipToGameDir(apMod, "AP Mod");
        }

        if (wantDependencies)
        {
            ShowInfo("Installing dependencies...");
            yield return InstallZipToGameDir(redScript, "RedScript");
            yield return InstallZipToGameDir(tweakXL, "TweakXL");
            yield return InstallZipToGameDir(archiveXL, "ArchiveXL");
            yield return InstallZipToGameDir(cet, "CET");
            yield return InstallZipToGameDir(codeware, "Codeware");
            yield return InstallZipToGameDir(red4ext, "RED4ext");
            yield return InstallZipToGameDir(phone, "Phone");
        }

        SaveInstalledFilesManifest(currentManifest);

        if (wantMods || wantDependencies)
        {
            if (launchGameToggle == null || launchGameToggle.isOn)
            {
                ShowInfo("Launching game...");
                yield return new WaitForSeconds(1f);
                LaunchGame();
            }
        }
        else if (launchGameToggle != null && launchGameToggle.isOn)
        {
            ShowInfo("Launching Cyberpunk 2077...");
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

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

            RemoveEmptyDirectories(manifest.gameInstallPath);

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

    IEnumerator InstallZipToGameDir(FileDownloader.FileData fileData, string componentName)
    {
        if (fileData == null || string.IsNullOrEmpty(fileData.url))
        {
            UnityEngine.Debug.LogWarning(componentName + " URL is empty, skipping.");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, componentName + "Temp");

        yield return downloader.DownloadAndExtract(fileData, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: " + componentName + " extraction failed!");
            yield break;
        }

        MoveDirectoryAndTrack(extractPath, gamePath);

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log(componentName + " installed successfully!");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

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

        yield return DownloadFile(apworld.url, localPath);

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

        string exePath = Path.Combine(currentGamePath, "bin", "x64", "Cyberpunk2077.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found. Checked:\n" + exePath);
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

    string GetGamePath()
    {
        return GetSteamPath();
    }

    string GetSteamPath()
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