using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class PsychonautsManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER")]
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData psychonautsApworld;
    public FileDownloader.FileData psychonautsAstralathe;
    public FileDownloader.FileData psychonautsAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Psychonauts";

    [Header("RANDO SEED SELECTION")]
    public Button selectRandoSeedButton;
    public TMP_InputField randoSeedInputField;
    public TextMeshProUGUI randoSeedPlaceholder;
    public UISkin darkSkin;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAstralathToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchGameToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearAstralathToggle;
    public Toggle clearAPModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string gamePath;
    private string selectedRandoSeedPath;
    private bool randoSeedSelected = false;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;
    private InstalledFilesManifest currentManifest;

    [System.Serializable]
    public class GameConfig
    {
        public string psychonautsAP;
        public string psychonautsApworld;
        public string psychonautsAstralathe;
        public string[] steamSearchPaths;
    }

    [System.Serializable]
    public class InstalledFilesManifest
    {
        public string gameInstallPath = "";
        public List<string> installedFiles = new List<string>();
        public List<string> installedDirectories = new List<string>();
    }

    void Start()
    {
        gamePath = GetGamePath();
        UnityEngine.Debug.Log("Auto-detected game path: " + (string.IsNullOrEmpty(gamePath) ? "Not found" : gamePath));

        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installAstralathToggle != null)
            installAstralathToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (fullClearAstralathToggle != null)
        {
            fullClearAstralathToggle.isOn = false;
            fullClearAstralathToggle.onValueChanged.AddListener(OnFullClearChanged);
        }

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (selectRandoSeedButton != null)
            selectRandoSeedButton.onClick.AddListener(SelectRandoSeedFile);

        if (randoSeedInputField != null)
        {
            randoSeedInputField.onEndEdit.AddListener(OnRandoSeedInputChanged);
        }

        if (randoSeedPlaceholder != null)
            randoSeedPlaceholder.gameObject.SetActive(true);

        UpdateUIState();
    }

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = !value ? clearAPModsToggle.isOn : false;
            clearAPModsToggle.interactable = !value;
        }
    }

    void UpdateUIState()
    {
        // Le RandoSeed.lua n'est requis que pour l'AP Mod : on ne bloque plus
        // toute l'interface (dont l'AP World) tant qu'il n'est pas sélectionné.
        bool canInteract = true;

        if (installAPWorldToggle != null)
            installAPWorldToggle.interactable = canInteract;

        if (installAstralathToggle != null)
            installAstralathToggle.interactable = canInteract;

        if (installAPModToggle != null)
            installAPModToggle.interactable = canInteract;

        var allButtons = GetComponentsInChildren<Button>();
        foreach (Button btn in allButtons)
        {
            if (btn.name.Contains("Setup") || btn.name.Contains("Revert"))
                btn.interactable = canInteract;
        }
    }

    void SelectRandoSeedFile()
    {
        StartCoroutine(ShowRandoSeedFileBrowser());
    }

    IEnumerator ShowRandoSeedFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        if (randoSeedInputField != null && !string.IsNullOrEmpty(randoSeedInputField.text))
        {
            string inputPath = Path.GetDirectoryName(randoSeedInputField.text);
            if (Directory.Exists(inputPath))
                startPath = inputPath;
        }

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select RandoSeed.lua");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];
            SetRandoSeedFile(selectedPath);
        }
    }

    void SetRandoSeedFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ShowInfo("File path is empty!");
            return;
        }

        if (!File.Exists(path))
        {
            ShowInfo("File does not exist!");
            return;
        }

        string fileName = Path.GetFileName(path);

        if (fileName != "RandoSeed.lua")
        {
            ShowInfo("ERROR: Selected file must be named 'RandoSeed.lua'.\nYou selected: " + fileName);
            randoSeedSelected = false;
            UpdateUIState();
            return;
        }

        if (!path.EndsWith(".lua"))
        {
            ShowInfo("ERROR: Selected file must have .lua extension.");
            randoSeedSelected = false;
            UpdateUIState();
            return;
        }

        selectedRandoSeedPath = path;
        randoSeedSelected = true;

        if (randoSeedInputField != null)
        {
            randoSeedInputField.gameObject.SetActive(true);
            randoSeedInputField.text = path;
        }

        if (randoSeedPlaceholder != null)
            randoSeedPlaceholder.gameObject.SetActive(false);

        UnityEngine.Debug.Log("RandoSeed.lua selected: " + path);
        UpdateUIState();
    }

    void OnRandoSeedInputChanged(string newPath)
    {
        if (string.IsNullOrEmpty(newPath))
        {
            randoSeedSelected = false;
            UpdateUIState();
            return;
        }

        if (!File.Exists(newPath))
        {
            randoSeedSelected = false;
            UpdateUIState();
            return;
        }

        string fileName = Path.GetFileName(newPath);
        if (fileName != "RandoSeed.lua")
        {
            randoSeedSelected = false;
            UpdateUIState();
            return;
        }

        selectedRandoSeedPath = newPath;
        randoSeedSelected = true;
        UnityEngine.Debug.Log("RandoSeed.lua updated from input field: " + selectedRandoSeedPath);
        UpdateUIState();
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        psychonautsAP.url = remoteConfig.psychonautsAP;
        psychonautsApworld.url = remoteConfig.psychonautsApworld;
        psychonautsAstralathe.url = remoteConfig.psychonautsAstralathe;
    }

    public void RunSetup()
    {
        // Le RandoSeed.lua n'est nécessaire que si on installe l'AP Mod.
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;

        if (apmod && (!randoSeedSelected || string.IsNullOrEmpty(selectedRandoSeedPath) || !File.Exists(selectedRandoSeedPath)))
        {
            ShowInfo("Please select RandoSeed.lua file. You can still download the APWorld.");
            return;
        }

        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void RevertAll()
    {
        bool fullClear = fullClearAstralathToggle != null && fullClearAstralathToggle.isOn;
        bool clearAPMods = clearAPModsToggle != null && clearAPModsToggle.isOn;

        if (!fullClear && !clearAPMods)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

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
        gamePath = GetGamePath();

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool astralathe = installAstralathToggle == null || installAstralathToggle.isOn;
        bool apmod = installAPModToggle == null || installAPModToggle.isOn;

        bool needsGamePath = astralathe || apmod;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        if (apmod && (!randoSeedSelected || string.IsNullOrEmpty(selectedRandoSeedPath) || !File.Exists(selectedRandoSeedPath)))
        {
            ShowInfo("RandoSeed.lua is not selected or file not found.");
            return;
        }

        int count = (apworld ? 1 : 0) + (apmod ? 1 : 0) + (astralathe ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(SetupWithTracking(apworld, astralathe, apmod));
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            LaunchGame();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator SetupWithTracking(bool installAPWorld, bool installAstralathe, bool installAPMod)
    {
        ShowInfo("Initializing installation tracker...");
        yield return new WaitForSeconds(0.5f);

        currentManifest = new InstalledFilesManifest();
        currentManifest.gameInstallPath = gamePath;

        ShowInfo("Downloading and installing files...");

        yield return InstallFlow(installAPWorld, installAstralathe, installAPMod);

        SaveInstalledFilesManifest(currentManifest);

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("Game path not found. Cannot revert.");
            return;
        }

        bool clearAP = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClean = fullClearAstralathToggle != null && fullClearAstralathToggle.isOn;

        if (!clearAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAP)
        {
            string modResourcePath = Path.Combine(gamePath, "ModResource");

            if (!Directory.Exists(modResourcePath))
            {
                ShowInfo("ModResource folder not found.");
                return;
            }

            ShowInfo("Removing AP mods...");

            string[] dirs = Directory.GetDirectories(modResourcePath);
            foreach (string dir in dirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Contains("PsychoRando"))
                {
                    SafeDeleteDirectory(dir);
                }
            }

            ShowInfo("AP mods removed successfully!");
            return;
        }

        if (fullClean)
        {
            ShowInfo("Removing Astralathe and all related files...");

            string[] itemsToDelete = new string[]
            {
                Path.Combine(gamePath, "ModData"),
                Path.Combine(gamePath, "ModResource"),
                Path.Combine(gamePath, "native_assets"),
                Path.Combine(gamePath, "Profiles"),
                Path.Combine(gamePath, "WidescreenResource"),
                Path.Combine(gamePath, "Astralathe.dll"),
                Path.Combine(gamePath, "astralathe.ini"),
                Path.Combine(gamePath, "astralathe.log"),
                Path.Combine(gamePath, "AstralatheLauncher.exe"),
                Path.Combine(gamePath, "AstralatheSteam.dll"),
                Path.Combine(gamePath, "Astralathe_CobwebDuster.exe"),
                Path.Combine(gamePath, "Astralathe_CobwebDuster.exe.config"),
                Path.Combine(gamePath, "astralathe_cobwebduster_log.log"),
                Path.Combine(gamePath, "astralathe_hooks.lua"),
                Path.Combine(gamePath, "astralathe_imgui.ini"),
                Path.Combine(gamePath, "astralathe_mods.txt"),
                Path.Combine(gamePath, "Astralathe_RUN IF GAME DOESNT START.bat"),
                Path.Combine(gamePath, "DisplaySettings.ini"),
                Path.Combine(gamePath, "dsound.dll"),
                Path.Combine(gamePath, "migrated.txt"),
                Path.Combine(gamePath, "PsychoPortal.dll"),
                Path.Combine(gamePath, "release_readme.txt"),
                Path.Combine(gamePath, "SOVEREIGN.log"),
                Path.Combine(gamePath, "steam_appid.txt"),
                Path.Combine(gamePath, "thirdpartylegalnotices.txt"),
            };

            foreach (string item in itemsToDelete)
            {
                if (Directory.Exists(item))
                {
                    SafeDeleteDirectory(item);
                }
                else if (File.Exists(item))
                {
                    SafeDeleteFile(item);
                }
            }

            ShowInfo("Full Astralathe cleanup completed!");
        }
    }

    IEnumerator InstallFlow(bool installAPWorld, bool installAstralathe, bool installAPMod)
    {
        if (installAstralathe)
        {
            ShowInfo("Installing Astralathe...");
            yield return InstallAstralathe();
        }

        if (installAPMod)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPMod();
        }

        if (installAPWorld)
        {
            ShowInfo("Installing Psychonauts APWorld...");
            yield return InstallAPWorld();
        }

        yield return new WaitForSeconds(2f);

        if (launchGameToggle == null || launchGameToggle.isOn)
        {
            ShowInfo("Launching game...");
            yield return new WaitForSeconds(1f);
            LaunchGame();
        }
        else
        {
            ShowInfo("Installation complete!");
        }

        yield return null;
    }

    IEnumerator InstallAstralathe()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "PsychonautsAstralatheTempExtract");

        yield return downloader.DownloadAndExtract(psychonautsAstralathe, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Astralathe extraction failed!");
            yield break;
        }

        string astralathePackPath = Path.Combine(extractPath, "Preloaded-Psychonauts-Astralathe");

        if (!Directory.Exists(astralathePackPath))
        {
            ShowInfo("ERROR: Preloaded-Psychonauts-Astralathe folder not found in extracted files!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        try
        {
            CopyAllFromExtract(astralathePackPath, gamePath);
            ShowInfo("Astralathe installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install Astralathe\n" + e.Message);
            UnityEngine.Debug.LogError("Astralathe installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "PsychonautsAPModTempExtract");

        yield return downloader.DownloadAndExtract(psychonautsAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: AP Mod extraction failed!");
            yield break;
        }

        string modResourcePath = Path.Combine(gamePath, "ModResource");

        try
        {
            Directory.CreateDirectory(modResourcePath);

            string[] extractedDirs = Directory.GetDirectories(extractPath);
            string psychoRandoSourcePath = null;

            foreach (string dir in extractedDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Contains("PsychoRando"))
                {
                    psychoRandoSourcePath = dir;
                    break;
                }
            }

            if (psychoRandoSourcePath == null)
            {
                ShowInfo("ERROR: PsychoRando folder not found in extracted files!");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            string psychoRandoFolderName = Path.GetFileName(psychoRandoSourcePath);
            string psychoRandoTargetPath = Path.Combine(modResourcePath, psychoRandoFolderName);

            CopyAllFromExtract(psychoRandoSourcePath, psychoRandoTargetPath);

            string scriptTargetPath = Path.Combine(psychoRandoTargetPath, psychoRandoFolderName, "Scripts");
            Directory.CreateDirectory(scriptTargetPath);

            string randoSeedTarget = Path.Combine(scriptTargetPath, "RandoSeed.lua");

            if (File.Exists(randoSeedTarget))
                File.Delete(randoSeedTarget);

            File.Copy(selectedRandoSeedPath, randoSeedTarget, true);

            if (currentManifest != null)
            {
                currentManifest.installedFiles.Add(randoSeedTarget);
                currentManifest.installedDirectories.Add(psychoRandoTargetPath);
            }

            ShowInfo("AP Mod installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install AP Mod\n" + e.Message);
            UnityEngine.Debug.LogError("AP Mod installation error: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + psychonautsApworld.url);

        if (string.IsNullOrEmpty(psychonautsApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = psychonautsApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = psychonautsApworld.url.Substring(psychonautsApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + psychonautsApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(psychonautsApworld.url, localPath);

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

            if (currentManifest != null)
                currentManifest.installedFiles.Add(target);

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

        gamePath = GetGamePath();
    }

    void LaunchGame()
    {
        UnityEngine.Debug.Log("LaunchGame called. GamePath: " + gamePath);

        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowInfo("Game path not found. Cannot launch.");
            UnityEngine.Debug.LogError("GamePath is empty or invalid!");
            return;
        }

        string exePath = Path.Combine(gamePath, "AstralatheLauncher.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Game launcher not found at:\n" + exePath);
            UnityEngine.Debug.LogError("Launcher not found!");
            return;
        }

        UnityEngine.Debug.Log("Checking launcher at: " + exePath);

        try
        {
            UnityEngine.Debug.Log("Starting process...");
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            Process.Start(psi);
            UnityEngine.Debug.Log("Game launched successfully from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
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

    void CopyAllFromExtract(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyAllFromExtract from " + extractPath + " to " + targetPath);

        if (!Directory.Exists(extractPath))
            return;

        foreach (string dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = dir.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destDir = Path.Combine(targetPath, rel);
            try
            {
                if (Directory.Exists(destDir))
                    SafeDeleteDirectory(destDir);
                Directory.CreateDirectory(destDir);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to create dest dir: " + destDir + " - " + e.Message);
            }
        }

        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dest = Path.Combine(targetPath, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest, true);
                UnityEngine.Debug.Log("Copied file: " + dest);

                if (currentManifest != null)
                    currentManifest.installedFiles.Add(dest);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to copy file: " + file + " - " + e.Message);
            }
        }

        UnityEngine.Debug.Log("END CopyAllFromExtract");
    }

    void SaveInstalledFilesManifest(InstalledFilesManifest manifest)
    {
        string manifestPath = Path.Combine(Application.persistentDataPath, "PsychonautsInstalledFilesManifest.json");
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

        UnityEngine.Debug.Log("Info: " + message);
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
}