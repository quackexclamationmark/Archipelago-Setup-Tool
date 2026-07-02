using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO.Compression;

public class SHARManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER")]
    public FileDownloader downloader;
    public FileDownloader.FileData sharApworld;
    public FileDownloader.FileData sharAP;
    public FileDownloader.FileData sharAPMod;
    public FileDownloader.FileData sharModLauncher;

    [Header("GAME SELECTION PANEL")]
    public GameObject gamePanel;
    public Toggle haveGameToggle;
    public Toggle noGameToggle;
    public GameObject noGamePanel;

    [Header("SETUP PANEL")]
    public GameObject setupPanel;
    public Button directoryButton;
    public TMP_InputField directoryInputField;
    public TextMeshProUGUI directoryPlaceholder;
    public Button setupButton;

    [Header("SETUP TOGGLES")]
    public Toggle installModLauncherToggle;
    public Toggle installRandomizerAndModToggle;
    public Toggle installApworldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;
    public Toggle launchModLauncherToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmPanel;
    public TextMeshProUGUI confirmText;
    public Button confirmOkButton;
    public Button confirmNoButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;
    public Button cancelButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";

    private string selectedGamePath = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;
    private bool haveGame = false;
    private System.Collections.Generic.List<Process> launchedProcesses = new System.Collections.Generic.List<Process>();
    private Coroutine currentSetupCoroutine;
    private bool isSetupRunning = false;

    private class ThreadTask
    {
        public volatile bool Complete = false;
        public System.Exception Exception = null;
    }

    [System.Serializable]
    public class RemoteConfig
    {
        public string sharApworld;
        public string sharAP;
        public string sharAPMod;
        public string sharModLauncher;
    }

    void Start()
    {
        // Setup listeners
        if (haveGameToggle != null)
            haveGameToggle.onValueChanged.AddListener(OnHaveGameToggled);
        if (noGameToggle != null)
            noGameToggle.onValueChanged.AddListener(OnNoGameToggled);

        if (directoryButton != null)
            directoryButton.onClick.AddListener(SelectDirectory);
        if (directoryInputField != null)
            directoryInputField.onEndEdit.AddListener(OnDirectoryInputChanged);

        if (setupButton != null)
            setupButton.onClick.AddListener(OnSetupButtonClicked);
        if (confirmOkButton != null)
            confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // Force panel states - Game panel first
        if (setupPanel != null)
            setupPanel.SetActive(false);
        if (gamePanel != null)
            gamePanel.SetActive(true);
        if (noGamePanel != null)
            noGamePanel.SetActive(false);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (infoPanel != null)
            infoPanel.SetActive(false);

        // Reset toggles
        if (haveGameToggle != null)
            haveGameToggle.isOn = false;
        if (noGameToggle != null)
            noGameToggle.isOn = false;

        // Setup toggle defaults
        if (installModLauncherToggle != null)
            installModLauncherToggle.isOn = true;
        if (installRandomizerAndModToggle != null)
            installRandomizerAndModToggle.isOn = true;
        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (launchGameToggle != null)
            launchGameToggle.isOn = true;
        if (launchModLauncherToggle != null)
            launchModLauncherToggle.isOn = false;

        StartCoroutine(LoadRemoteConfig());
    }

    void OnHaveGameToggled(bool value)
    {
        if (value)
        {
            haveGame = true;
            if (noGameToggle != null)
            {
                noGameToggle.onValueChanged.RemoveListener(OnNoGameToggled);
                noGameToggle.isOn = false;
                noGameToggle.onValueChanged.AddListener(OnNoGameToggled);
            }
            if (noGamePanel != null)
                noGamePanel.SetActive(false);
        }
    }

    void OnNoGameToggled(bool value)
    {
        if (value)
        {
            haveGame = false;
            if (haveGameToggle != null)
            {
                haveGameToggle.onValueChanged.RemoveListener(OnHaveGameToggled);
                haveGameToggle.isOn = false;
                haveGameToggle.onValueChanged.AddListener(OnHaveGameToggled);
            }
            if (noGamePanel != null)
                noGamePanel.SetActive(true);
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        UnityEngine.Debug.Log("Loading SHAR config from: " + configUrl);

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(configUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
            configLoaded = true;
            yield break;
        }

        ParseRemoteConfig(request.downloadHandler.text);
        configLoaded = true;
    }

    void ParseRemoteConfig(string jsonText)
    {
        try
        {
            remoteConfig = JsonUtility.FromJson<RemoteConfig>(jsonText);
            if (remoteConfig != null)
            {
                sharApworld.url = remoteConfig.sharApworld;
                sharAP.url = remoteConfig.sharAP;
                sharAPMod.url = remoteConfig.sharAPMod;
                sharModLauncher.url = remoteConfig.sharModLauncher;
                UnityEngine.Debug.Log("SHAR config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }
    }

    void SelectDirectory()
    {
        StartCoroutine(ShowFileBrowser());
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;
        else
            UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        string startPath = "C:\\";

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.FilesAndFolders, false, startPath, "Select Simpsons.exe or game directory");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];

            if (File.Exists(selectedPath) && selectedPath.EndsWith("Simpsons.exe", System.StringComparison.OrdinalIgnoreCase))
            {
                selectedPath = Path.GetDirectoryName(selectedPath);
            }

            SetDirectory(selectedPath);
        }
    }

    void SetDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ShowInfo("Please select a valid directory.");
            return;
        }

        if (!Directory.Exists(path))
        {
            ShowInfo("Directory does not exist.");
            return;
        }

        string simpsonsExePath = Path.Combine(path, "Simpsons.exe");
        if (!File.Exists(simpsonsExePath))
        {
            ShowInfo("Simpsons.exe not found in selected directory.");
            return;
        }

        selectedGamePath = path;
        if (directoryInputField != null)
        {
            directoryInputField.text = path;
            if (directoryPlaceholder != null)
                directoryPlaceholder.gameObject.SetActive(false);
        }
        UnityEngine.Debug.Log("Game directory set: " + path);
    }

    void OnDirectoryInputChanged(string newValue)
    {
        if (string.IsNullOrEmpty(newValue))
        {
            selectedGamePath = "";
            return;
        }

        if (!Directory.Exists(newValue))
        {
            return;
        }

        string simpsonsExePath = Path.Combine(newValue, "Simpsons.exe");
        if (!File.Exists(simpsonsExePath))
        {
            return;
        }

        selectedGamePath = newValue;
    }

    void OnSetupButtonClicked()
    {
        bool haveGameToggleActive = haveGameToggle != null && haveGameToggle.isOn;
        bool noGameToggleActive = noGameToggle != null && noGameToggle.isOn;

        if (!haveGameToggleActive && !noGameToggleActive)
        {
            ShowInfo("Please select if you have the game or not.");
            return;
        }

        if (string.IsNullOrEmpty(selectedGamePath))
        {
            ShowInfo("Please select a game directory.");
            return;
        }

        if (confirmPanel != null)
        {
            if (confirmText != null)
                confirmText.text = "Do you want to start the setup now?";
            confirmPanel.SetActive(true);
        }
        else
        {
            ExecuteSetup();
        }
    }

    void OnConfirmOk()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        ExecuteSetup();
    }

    void OnConfirmNo()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    void OnCancelButtonClicked()
    {
        if (isSetupRunning)
        {
            if (currentSetupCoroutine != null)
            {
                StopCoroutine(currentSetupCoroutine);
                currentSetupCoroutine = null;
            }
            isSetupRunning = false;
        }

        // Tuer tous les processus lancés
        foreach (Process proc in launchedProcesses)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to kill process: " + e.Message);
            }
        }
        launchedProcesses.Clear();

        // Arrêter tous les coroutines
        StopAllCoroutines();

        CloseInfoPanel();
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        ShowInfo("Setup cancelled!");
        UnityEngine.Debug.Log("All operations cancelled.");
    }

    void ExecuteSetup()
    {
        if (isSetupRunning)
        {
            ShowInfo("Setup is already running!");
            return;
        }

        isSetupRunning = true;
        currentSetupCoroutine = StartCoroutine(SetupFlow());
    }

    IEnumerator SetupFlow()
    {
        try
        {
            while (!configLoaded)
            {
                UnityEngine.Debug.Log("Waiting for config to load...");
                yield return new WaitForSeconds(0.5f);
            }

            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string tempPath = Path.Combine(documentsPath, "SHARAPTemp");

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            ShowInfo("Installing SHAR Archipelago files...");

            bool haveGameToggleActive = haveGameToggle != null && haveGameToggle.isOn;
            bool noGameToggleActive = noGameToggle != null && noGameToggle.isOn;

            bool installModLauncher = installModLauncherToggle != null && installModLauncherToggle.isOn;
            bool installRandomizerAndMod = installRandomizerAndModToggle != null && installRandomizerAndModToggle.isOn;
            bool installApworld = installApworldToggle != null && installApworldToggle.isOn;

            if (haveGameToggleActive)
            {
                ShowInfo("You selected: Already have the game. Installing selected files...");
                yield return new WaitForSeconds(1f);

                // Installer le randomizer et mod même si on a le jeu
                if (installRandomizerAndMod)
                {
                    yield return InstallAPRandomizer(selectedGamePath, tempPath);
                    yield return InstallAPMod();
                }

                // Installer le mod launcher si sélectionné
                if (installModLauncher)
                    yield return InstallModLauncher();

                // Installer l'APWorld si sélectionné
                if (installApworld)
                    yield return InstallApworld();
            }
            else if (noGameToggleActive)
            {
                ShowInfo("You selected: Need to install/setup. Installing selected files...");
                yield return new WaitForSeconds(1f);

                if (installModLauncher)
                    yield return InstallModLauncher();

                if (installRandomizerAndMod)
                {
                    yield return InstallAPRandomizer(selectedGamePath, tempPath);
                    yield return InstallAPMod();
                }

                if (installApworld)
                    yield return InstallApworld();
            }

            bool shouldLaunchGame = launchGameToggle != null && launchGameToggle.isOn;
            if (shouldLaunchGame && installRandomizerAndMod)
            {
                ShowInfo("Launching SHARRandomizer...");
                yield return new WaitForSeconds(1f);
                LaunchSHARRandomizer(selectedGamePath);
            }

            bool shouldLaunchModLauncher = launchModLauncherToggle != null && launchModLauncherToggle.isOn;
            if (shouldLaunchModLauncher)
            {
                ShowInfo("Launching Lucas' Simpsons Hit & Run Mod Launcher...");
                yield return new WaitForSeconds(1f);
                LaunchModLauncher();
            }

            ShowInfo("Setup complete!");
            yield return new WaitForSeconds(2f);

            SafeDeleteDirectory(tempPath);
        }
        finally
        {
            isSetupRunning = false;
            currentSetupCoroutine = null;
        }
    }

    IEnumerator InstallApworld()
    {
        ShowInfo("Downloading and installing APWorld...");

        if (string.IsNullOrEmpty(sharApworld.url))
        {
            ShowInfo("ERROR: APWorld URL not loaded!");
            yield break;
        }

        string fileName = sharApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = sharApworld.url.Substring(sharApworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), fileName);

        yield return DownloadFile(sharApworld.url, localPath);

        if (!File.Exists(localPath))
        {
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
                UnityEngine.Debug.Log("Using APWorld target path: " + target);
                break;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find Archipelago custom_worlds directory!");
            yield break;
        }

        if (File.Exists(target))
        {
            try { File.Delete(target); } catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
            UnityEngine.Debug.Log("APWorld installed to: " + target);
            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }
    }

    IEnumerator InstallModLauncher()
    {
        ShowInfo("Downloading Lucas' Simpsons Hit & Run Mod Launcher...");

        if (string.IsNullOrEmpty(sharModLauncher.url))
        {
            ShowInfo("ERROR: Mod Launcher URL not loaded!");
            yield break;
        }

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempPath = Path.Combine(documentsPath, "SHARAPTemp");

        if (!Directory.Exists(tempPath))
            Directory.CreateDirectory(tempPath);

        string zipPath = Path.Combine(tempPath, "ModLauncher.zip");

        yield return DownloadFile(sharModLauncher.url, zipPath);

        if (!File.Exists(zipPath))
        {
            ShowInfo("ERROR: Mod Launcher download failed!");
            yield break;
        }

        ShowInfo("Extracting Mod Launcher...");

        string extractPath = Path.Combine(tempPath, "launcher_extract");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        ThreadTask extractTask = StartZipExtraction(zipPath, extractPath);
        while (!extractTask.Complete)
            yield return new WaitForSeconds(0.1f);

        if (extractTask.Exception != null)
        {
            ShowInfo("ERROR: Failed to extract Mod Launcher: " + extractTask.Exception.Message);
            yield break;
        }

        string targetDir = Path.Combine(documentsPath, "Lucas' Simpsons Hit & Run Mod Launcher");
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, true);

        // Créer le dossier "Lucas' Simpsons Hit & Run Mod Launcher"
        Directory.CreateDirectory(targetDir);

        // Copier TOUT le contenu du .zip dans ce dossier
        ThreadTask copyTask = StartCopyDirectoryTask(extractPath, targetDir);
        while (!copyTask.Complete)
            yield return new WaitForSeconds(0.1f);

        if (copyTask.Exception != null)
        {
            ShowInfo("ERROR: Failed to copy Mod Launcher: " + copyTask.Exception.Message);
            yield break;
        }

        UnityEngine.Debug.Log("Mod Launcher installed to: " + targetDir);
        ShowInfo("Mod Launcher installed successfully!");

        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPRandomizer(string gamePath, string tempPath)
    {
        ShowInfo("Downloading SHARRandomizer...");

        if (string.IsNullOrEmpty(sharAP.url))
        {
            ShowInfo("ERROR: SHARRandomizer URL not loaded!");
            yield break;
        }

        string zipPath = Path.Combine(tempPath, "SHARRandomizer.zip");

        yield return DownloadFile(sharAP.url, zipPath);

        if (!File.Exists(zipPath))
        {
            ShowInfo("ERROR: SHARRandomizer download failed!");
            yield break;
        }

        ShowInfo("Extracting SHARRandomizer...");

        string extractPath = Path.Combine(tempPath, "shar_extract");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        ThreadTask extractTask = StartZipExtraction(zipPath, extractPath);
        while (!extractTask.Complete)
            yield return new WaitForSeconds(0.1f);

        if (extractTask.Exception != null)
        {
            ShowInfo("ERROR: Failed to extract SHARRandomizer: " + extractTask.Exception.Message);
            yield break;
        }

        string targetDir = Path.Combine(gamePath, "Archipelago");
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, true);

        // Créer le dossier "Archipelago"
        Directory.CreateDirectory(targetDir);

        // Copier TOUT le contenu du .zip dans ce dossier
        ThreadTask copyTask = StartCopyDirectoryTask(extractPath, targetDir);
        while (!copyTask.Complete)
            yield return new WaitForSeconds(0.1f);

        if (copyTask.Exception != null)
        {
            ShowInfo("ERROR: Failed to copy SHARRandomizer: " + copyTask.Exception.Message);
            yield break;
        }

        UnityEngine.Debug.Log("SHARRandomizer installed to: " + targetDir);
        ShowInfo("SHARRandomizer installed successfully!");

        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        ShowInfo("Downloading SHARAP.lmlm mod file...");

        if (string.IsNullOrEmpty(sharAPMod.url))
        {
            ShowInfo("ERROR: SHARAP.lmlm URL not loaded!");
            yield break;
        }

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string modsDir = Path.Combine(documentsPath, "My Games", "Lucas' Simpsons Hit & Run Mod Launcher", "Mods");

        try
        {
            if (!Directory.Exists(modsDir))
                Directory.CreateDirectory(modsDir);
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to create mod directory: " + e.Message);
            yield break;
        }

        string modFilePath = Path.Combine(modsDir, "SHARAP.lmlm");

        // Supprimer le fichier s'il existe déjà
        if (File.Exists(modFilePath))
        {
            try
            {
                File.Delete(modFilePath);
                UnityEngine.Debug.Log("Deleted existing SHARAP.lmlm");
            }
            catch (System.Exception e)
            {
                ShowInfo("ERROR: Failed to delete existing SHARAP.lmlm: " + e.Message);
                yield break;
            }
        }

        yield return DownloadFile(sharAPMod.url, modFilePath);

        if (!File.Exists(modFilePath))
        {
            ShowInfo("ERROR: SHARAP.lmlm download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("SHARAP.lmlm installed to: " + modFilePath);
        ShowInfo("SHARAP.lmlm installed successfully!");

        yield return new WaitForSeconds(1f);
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            request.timeout = 300;
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete!");
            }
        }
    }

    private ThreadTask StartZipExtraction(string zipPath, string extractPath)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error extracting ZIP: " + e.Message);
                task.Exception = e;
            }
            finally
            {
                task.Complete = true;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    private ThreadTask StartCopyDirectoryTask(string sourceDir, string targetDir)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try
            {
                PerformDirectoryCopy(sourceDir, targetDir);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
                task.Exception = e;
            }
            finally
            {
                task.Complete = true;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    void PerformDirectoryCopy(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            PerformDirectoryCopy(dir, targetSubDir);
        }
    }

    void LaunchSHARRandomizer(string gamePath)
    {
        string launcherPath = Path.Combine(gamePath, "Archipelago", "SHARRandomizerFrontend.exe");
        if (File.Exists(launcherPath))
        {
            try
            {
                Process proc = Process.Start(launcherPath);
                if (proc != null)
                {
                    launchedProcesses.Add(proc);
                }
                UnityEngine.Debug.Log("SHARRandomizer Frontend launched!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching SHARRandomizer: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("SHARRandomizer Frontend not found at: " + launcherPath);
        }
    }

    void LaunchModLauncher()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string launcherPath = Path.Combine(documentsPath, "Lucas' Simpsons Hit & Run Mod Launcher", "Lucas Simpsons Hit & Run Mod Launcher.exe");

        if (File.Exists(launcherPath))
        {
            try
            {
                Process proc = Process.Start(launcherPath);
                if (proc != null)
                {
                    launchedProcesses.Add(proc);
                }
                UnityEngine.Debug.Log("Lucas' Simpsons Hit & Run Mod Launcher launched!");
            }
            catch (System.Exception e)
            {
                ShowInfo("Error launching Mod Launcher: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Mod Launcher not found at: " + launcherPath);
        }
    }

    void ShowInfo(string message)
    {
        if (infoText != null)
            infoText.text = message;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        UnityEngine.Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
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
}