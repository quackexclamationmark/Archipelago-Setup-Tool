using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class ContentWarningManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;
    public FileDownloader.FileData mycelium;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;
    public Toggle installMyceliumToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanBepInExToggle;
    public Toggle removeAPModsOnlyToggle;

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
    private bool pendingFullCleanConfirmation = false;
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class GameConfig
    {
        public string contentwarningApworld;
        public string contentwarningAP;
        public string contentwarningBepInEx;
        public string contentwarningMycelium;
    }

    void Start()
    {
        gamePath = GetGamePath();
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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            if (value)
            {
                removeAPModsOnlyToggle.isOn = false;
                removeAPModsOnlyToggle.interactable = false;
            }
            else
            {
                removeAPModsOnlyToggle.interactable = true;
            }
        }
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.contentwarningApworld;
        apMod.url = remoteConfig.contentwarningAP;
        bepInEx.url = remoteConfig.contentwarningBepInEx;
        mycelium.url = remoteConfig.contentwarningMycelium;
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

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;
            case "Revert":
                ExecuteRevert();
                break;
            case "ForceFullClean":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        // Réinitialiser gamePath pour s'assurer qu'on a le chemin à jour
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Content Warning path not found. Please check Steam installation.");
            return;
        }

        bool apworldToggle = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool myceliumToggle = installMyceliumToggle != null && installMyceliumToggle.isOn;

        int count =
            (apworldToggle ? 1 : 0) +
            (bep ? 1 : 0) +
            (apmod ? 1 : 0) +
            (myceliumToggle ? 1 : 0);

        if (apworldToggle && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        if (myceliumToggle && count == 1)
        {
            StartCoroutine(MyceliumOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            return;

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (removeAP)
        {
            CleanupProcesses();

            ShowInfo("Removing AP mods...");

            SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "ContentWarningArchipelago.dll"));
            SafeDeleteFile(Path.Combine(pluginsPath, "MyceliumNetworkingForCW-Debug.dll"));
            DeleteOldVersionFiles();

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClean &&
            hasOtherMods &&
            !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        if (fullClean)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx"));
            SafeDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(gamePath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(gamePath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(gamePath, ".doorstop_version"));
            DeleteOldVersionFiles();

            ShowInfo("Full clean completed!");
            return;
        }

        ShowInfo("Removing mods...");

        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "ContentWarningArchipelago.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "MyceliumNetworkingForCW-Debug.dll"));
        DeleteOldVersionFiles();

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

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "SW_CreeperKing.ArchipelagoMod")
                return true;
        }

        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);

            if (fileName.StartsWith("Content Warning APMod Version") && fileName.EndsWith(".txt"))
                continue;

            if (fileName != "Archipelago.MultiClient.Net.dll" &&
                fileName != "ContentWarningArchipelago.dll" &&
                fileName != "MyceliumNetworkingForCW-Debug.dll")
                return true;
        }

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installBepInExToggle != null && installBepInExToggle.isOn)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (installMyceliumToggle != null && installMyceliumToggle.isOn)
        {
            ShowInfo("Installing Mycelium...");
            yield return InstallMycelium();
        }

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url, mycelium.url);

        ShowInfo("Installation complete!");

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Ready to play!");
        }
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

        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string targetFolder = null;

        string[] possiblePaths =
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData), "Archipelago", "custom_worlds"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds"),
        };

        foreach (string path in possiblePaths)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                targetFolder = path;
                UnityEngine.Debug.Log("Using target folder: " + targetFolder);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + path + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    string path = Path.Combine(drive.RootDirectory.FullName, "Archipelago", "custom_worlds");

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    targetFolder = path;
                    UnityEngine.Debug.Log("Using target folder on drive: " + targetFolder);
                    break;
                }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            ShowInfo("Archipelago Launcher is not installed.\nPlease install it before using APWorld.");
            UnityEngine.Debug.LogError("No valid Archipelago folder found!");
            yield break;
        }

        string target = Path.Combine(targetFolder, fileName);

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
            UnityEngine.Debug.LogError("APWorld install failed: " + e.Message);
            ShowInfo("Failed to install APWorld.");
            yield break;
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

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "ContentWarningAPModTemp");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");

        Directory.CreateDirectory(pluginsPath);

        // Copier les DLLs nécessaires
        string multiClientPath = Path.Combine(extractPath, "Archipelago.MultiClient.Net.dll");
        if (File.Exists(multiClientPath))
        {
            File.Copy(multiClientPath, Path.Combine(pluginsPath, "Archipelago.MultiClient.Net.dll"), true);
            UnityEngine.Debug.Log("Archipelago.MultiClient.Net.dll installed!");
        }

        string contentWarningPath = Path.Combine(extractPath, "ContentWarningArchipelago.dll");
        if (File.Exists(contentWarningPath))
        {
            File.Copy(contentWarningPath, Path.Combine(pluginsPath, "ContentWarningArchipelago.dll"), true);
            UnityEngine.Debug.Log("ContentWarningArchipelago.dll installed!");
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        // Vérifier que downloader est assigné
        if (downloader == null)
        {
            ShowInfo("ERROR: FileDownloader not found!");
            UnityEngine.Debug.LogError("downloader is null!");
            yield break;
        }

        // Vérifier que gamePath est valide
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("ERROR: Game path not found!");
            UnityEngine.Debug.LogError("GamePath is empty in InstallBepInEx!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, gamePath);

        UnityEngine.Debug.Log("BepInEx installed successfully!");

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMycelium()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("Mycelium URL: " + mycelium.url);

        if (string.IsNullOrEmpty(mycelium.url))
        {
            ShowInfo("ERROR: Mycelium URL is empty!");
            UnityEngine.Debug.LogError("Mycelium URL not set!");
            yield break;
        }

        string fileName = "MyceliumNetworkingForCW-Debug.dll";
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Mycelium from: " + mycelium.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(mycelium.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Mycelium download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string targetPath = Path.Combine(pluginsPath, fileName);

        try
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Copy(localPath, targetPath, true);
            UnityEngine.Debug.Log("Mycelium file copied to: " + targetPath);
            ShowInfo("Mycelium installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Mycelium install failed: " + e.Message);
            ShowInfo("Failed to install Mycelium.");
            yield break;
        }
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();
        yield break;
    }

    IEnumerator APModOnlyFlow()
    {
        ShowInfo("Installing AP Mod...");
        yield return InstallAPMod();

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url, mycelium.url);

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        ShowInfo("Installing BepInEx...");
        yield return InstallBepInEx();

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator MyceliumOnlyFlow()
    {
        ShowInfo("Installing Mycelium...");
        yield return InstallMycelium();

        CreateVersionFile(apMod.url, bepInEx.url, apworld.url, mycelium.url);

        ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGameConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void CleanupProcesses()
    {
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

        Directory.Delete(source, true);
    }

    string GetGamePath()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Content Warning"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Content Warning"),
        @"D:\Steam\steamapps\common\Content Warning",
        @"D:\SteamLibrary\steamapps\common\Content Warning",
        @"D:\steamapps\common\Content Warning",
        @"E:\Steam\steamapps\common\Content Warning",
        @"E:\SteamLibrary\steamapps\common\Content Warning",
        @"E:\steamapps\common\Content Warning",
        @"E:\Program Files (x86)\steamapps\common\Content Warning",
        @"E:\Program Files\steamapps\common\Content Warning",
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Game path found: " + path);
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
                try
                {
                    // Cherche Steam\steamapps
                    string path = Path.Combine(drive.RootDirectory.FullName, "Steam", "steamapps", "common", "Content Warning");
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Game path found: " + path);
                        return path;
                    }

                    // Cherche SteamLibrary\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "Content Warning");
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Game path found: " + path);
                        return path;
                    }

                    // Cherche directement steamapps à la racine du disque
                    path = Path.Combine(drive.RootDirectory.FullName, "steamapps", "common", "Content Warning");
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Game path found: " + path);
                        return path;
                    }

                    // Cherche dans Program Files (x86)\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "steamapps", "common", "Content Warning");
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Game path found: " + path);
                        return path;
                    }

                    // Cherche dans Program Files\steamapps
                    path = Path.Combine(drive.RootDirectory.FullName, "Program Files", "steamapps", "common", "Content Warning");
                    if (Directory.Exists(path))
                    {
                        UnityEngine.Debug.Log("Game path found: " + path);
                        return path;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogError("Game path NOT found! Checked all standard locations.");
        return "";
    }

    // =========================================================
    // VERSION FILE MANAGEMENT
    // =========================================================

    void CreateVersionFile(string apmodUrl, string bepinexUrl, string apworldUrl, string myceliumUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl);
            string bepinexVersion = ExtractVersionFromUrl(bepinexUrl);
            string apworldVersion = ExtractVersionFromUrl(apworldUrl);
            string myceliumVersion = ExtractVersionFromUrl(myceliumUrl);

            string versionFileName = "Content Warning APMod Version " + apmodVersion + ".txt";
            string content = "Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== BEPINEX ===\n";
            content += "Downloaded from: " + bepinexUrl + "\n";
            content += "Version: " + bepinexVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Version: " + apworldVersion + "\n";
            content += "\n";
            content += "=== MYCELIUM ===\n";
            content += "Downloaded from: " + myceliumUrl + "\n";
            content += "Version: " + myceliumVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(gamePath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created in root: " + rootVersionPath);

            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string pluginsVersionPath = Path.Combine(pluginsPath, versionFileName);
                File.WriteAllText(pluginsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in plugins: " + pluginsVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Content Warning APMod Version .+\.txt");

            string[] rootFiles = Directory.GetFiles(gamePath);
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

            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
            if (Directory.Exists(pluginsPath))
            {
                string[] pluginsFiles = Directory.GetFiles(pluginsPath);
                foreach (string file in pluginsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in plugins: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in plugins: " + e.Message);
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

    string ExtractVersionFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown";

        // Essayer d'abord le pattern GitHub releases: /releases/download/v1.0.0/
        System.Text.RegularExpressions.Regex githubPattern = new System.Text.RegularExpressions.Regex(@"/releases/download/([^/]+)/");
        System.Text.RegularExpressions.Match githubMatch = githubPattern.Match(url);

        if (githubMatch.Success)
            return githubMatch.Groups[1].Value;

        // Essayer le pattern Thunderstore: /package/download/Author/Package/1.0.0/
        System.Text.RegularExpressions.Regex thunderstorePattern = new System.Text.RegularExpressions.Regex(@"/package/download/[^/]+/[^/]+/([^/]+)/");
        System.Text.RegularExpressions.Match thunderstoreMatch = thunderstorePattern.Match(url);

        if (thunderstoreMatch.Success)
            return thunderstoreMatch.Groups[1].Value;

        // Si aucun pattern ne correspond, retourner "Unknown"
        return "Unknown";
    }
}
