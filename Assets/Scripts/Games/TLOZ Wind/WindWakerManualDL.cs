using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO.Compression;

public class WindWakerManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER (optionnel)")]
    public FileDownloader downloader;
    public FileDownloader.FileData tlozwwAPFile;
    public FileDownloader.FileData tlozwwDolphinFile;

    [Header("PANELS")]
    public GameObject romSelectionPanel;
    public GameObject installationPanel;

    [Header("ROM SELECTION UI")]
    public Button selectRomButton;
    public TMP_InputField romPathInputField;
    public TextMeshProUGUI romPathPlaceholder;
    public Button nextPanelButton;

    [Header("INSTALLATION UI")]
    public Button selectPatchButton;
    public TMP_InputField patchPathInputField;
    public TextMeshProUGUI patchPathPlaceholder;

    public Button selectInstallationPathButton;
    public TMP_InputField installationPathInputField;
    public TextMeshProUGUI installationPathPlaceholder;

    public Toggle installWWAPToggle;
    public Toggle installDolphinToggle;
    public Toggle launchAppsToggle;

    public Button installButton;
    public Button cancelButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmPanel;
    public TextMeshProUGUI confirmText;
    public Button confirmOkButton;
    public Button confirmNoButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";
    public string settingsTemplateUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/ConfigFilesPresets/settings.txt";

    private string selectedRomPath = "";
    private string selectedPatchPath = "";
    private string selectedInstallationPath = "";
    private string tlozwwAPDownloadUrl = "";
    private string tlozwwDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool wwapInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;

    public bool wwapLaunched { get; private set; } = false;
    public bool dolphinLaunched { get; private set; } = false;

    public bool installationCancelled { get; private set; } = false;
    private System.Collections.Generic.List<Process> launchedProcesses = new System.Collections.Generic.List<Process>();

    public bool installationComplete { get; private set; } = false;

    private class ThreadTask
    {
        public volatile bool Complete = false;
        public System.Exception Exception = null;
    }

    [System.Serializable]
    public class RemoteConfig
    {
        public string tlozwwAP;
        public string tlozwwDolphin;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! The Wind Waker AP Randomizer is ready to use.";

    void Start()
    {
        SetupFileBrowserQuickLinks();

        if (selectRomButton != null) selectRomButton.onClick.AddListener(SelectRom);
        if (nextPanelButton != null) nextPanelButton.onClick.AddListener(ValidateAndShowNextMessage);
        if (romPathInputField != null) romPathInputField.onValueChanged.AddListener(OnRomPathInputChanged);

        if (selectPatchButton != null) selectPatchButton.onClick.AddListener(SelectPatch);
        if (patchPathInputField != null) patchPathInputField.onValueChanged.AddListener(OnPatchPathInputChanged);

        if (selectInstallationPathButton != null) selectInstallationPathButton.onClick.AddListener(SelectInstallationPath);
        if (installationPathInputField != null) installationPathInputField.onValueChanged.AddListener(OnInstallationPathInputChanged);

        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (romSelectionPanel != null) romSelectionPanel.SetActive(true);
        if (installationPanel != null) installationPanel.SetActive(false);

        if (installWWAPToggle != null) installWWAPToggle.isOn = true;
        if (installDolphinToggle != null) installDolphinToggle.isOn = false;
        if (launchAppsToggle != null) launchAppsToggle.isOn = false;

        if (cancelButton != null) cancelButton.interactable = false;

        UpdateNextButtonInteractable();

        if (infoText != null) infoDefaultFontSize = infoText.fontSize;

        StartCoroutine(LoadRemoteConfig());
    }

    void SetupFileBrowserQuickLinks()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string downloadsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");

        FileBrowser.AddQuickLink("Documents", documentsPath, null);

        if (Directory.Exists(downloadsPath))
        {
            FileBrowser.AddQuickLink("Downloads", downloadsPath, null);
        }
    }

    void UpdateNextButtonInteractable()
    {
        if (nextPanelButton == null) return;
        nextPanelButton.interactable = IsValidRomSilent(selectedRomPath);
    }

    IEnumerator LoadRemoteConfig()
    {
        UnityEngine.Debug.Log("Loading Wind Waker config from: " + configUrl);

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
                tlozwwAPDownloadUrl = remoteConfig.tlozwwAP;
                tlozwwDolphinDownloadUrl = remoteConfig.tlozwwDolphin;
                UnityEngine.Debug.Log("Wind Waker config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }
    }

    void SelectRom() { StartCoroutine(ShowRomFileBrowser()); }

    IEnumerator ShowRomFileBrowser()
    {
        if (darkSkin != null) FileBrowser.Skin = darkSkin;
        else UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select Wind Waker ROM (.iso)");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            SetRomPath(FileBrowser.Result[0]);
        }
    }

    void SetRomPath(string path)
    {
        selectedRomPath = path;
        if (romPathInputField != null) romPathInputField.text = path;
        if (romPathPlaceholder != null) romPathPlaceholder.gameObject.SetActive(false);
        UnityEngine.Debug.Log("ROM path set: " + path);
        UpdateNextButtonInteractable();
    }

    void OnRomPathInputChanged(string newValue)
    {
        selectedRomPath = newValue;
        UpdateNextButtonInteractable();
    }

    bool IsValidRom(string romPath)
    {
        if (string.IsNullOrEmpty(romPath))
        {
            ShowInfo("Please select a ROM file.");
            return false;
        }

        if (!File.Exists(romPath))
        {
            ShowInfo("ROM file does not exist.");
            return false;
        }

        if (!romPath.EndsWith(".iso", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("ROM must be a .iso file.");
            return false;
        }

        string fileName = Path.GetFileName(romPath);

        if (!fileName.Contains("Wind Waker", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("ROM filename must contain 'Wind Waker'.");
            return false;
        }

        if (!fileName.Contains("USA", System.StringComparison.OrdinalIgnoreCase) && !fileName.Contains("US", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("Only USA/US versions are supported.");
            return false;
        }

        return true;
    }

    bool IsValidRomSilent(string romPath)
    {
        if (string.IsNullOrEmpty(romPath)) return false;
        if (!File.Exists(romPath)) return false;
        if (!romPath.EndsWith(".iso", System.StringComparison.OrdinalIgnoreCase)) return false;

        string fileName = Path.GetFileName(romPath);

        if (!fileName.Contains("Wind Waker", System.StringComparison.OrdinalIgnoreCase)) return false;

        if (!fileName.Contains("USA", System.StringComparison.OrdinalIgnoreCase) && !fileName.Contains("US", System.StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    void ValidateAndShowNextMessage()
    {
        if (!IsValidRom(selectedRomPath)) return;
        ShowInfo("Valid ROM - You can move on to the next step! (Setup panel)");
    }

    void SelectPatch() { StartCoroutine(ShowPatchFileBrowser()); }

    IEnumerator ShowPatchFileBrowser()
    {
        if (darkSkin != null) FileBrowser.Skin = darkSkin;
        else UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select Wind Waker Patch (.aptww)");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            SetPatchPath(FileBrowser.Result[0]);
        }
    }

    void SetPatchPath(string path)
    {
        selectedPatchPath = path;
        if (patchPathInputField != null) patchPathInputField.text = path;
        if (patchPathPlaceholder != null) patchPathPlaceholder.gameObject.SetActive(false);
        UnityEngine.Debug.Log("Patch path set: " + path);
    }

    void OnPatchPathInputChanged(string newValue)
    {
        selectedPatchPath = newValue;
    }

    void SelectInstallationPath() { StartCoroutine(ShowInstallationPathBrowser()); }

    IEnumerator ShowInstallationPathBrowser()
    {
        if (darkSkin != null) FileBrowser.Skin = darkSkin;
        else UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Folders, false, startPath, "Select Installation Path");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            SetInstallationPath(FileBrowser.Result[0]);
        }
    }

    void SetInstallationPath(string path)
    {
        selectedInstallationPath = path;
        if (installationPathInputField != null) installationPathInputField.text = path;
        if (installationPathPlaceholder != null) installationPathPlaceholder.gameObject.SetActive(false);
        UnityEngine.Debug.Log("Installation path set: " + path);
    }

    void OnInstallationPathInputChanged(string newValue)
    {
        selectedInstallationPath = newValue;
    }

    void OnInstallButtonClicked()
    {
        if (!IsValidRom(selectedRomPath))
        {
            ShowInfo("Please select a valid ROM file.");
            return;
        }

        if (string.IsNullOrEmpty(selectedPatchPath))
        {
            ShowInfo("Please select a patch file (.aptww).");
            return;
        }

        if (!File.Exists(selectedPatchPath))
        {
            ShowInfo("Patch file does not exist.");
            return;
        }

        if (string.IsNullOrEmpty(selectedInstallationPath))
        {
            ShowInfo("Please select an installation path.");
            return;
        }

        if (!Directory.Exists(selectedInstallationPath))
        {
            ShowInfo("Installation path does not exist.");
            return;
        }

        if (confirmPanel != null)
        {
            if (confirmText != null) confirmText.text = "Do you want to start the setup now?";
            confirmPanel.SetActive(true);
        }
        else ExecuteInstallation();
    }

    void OnCancelButtonClicked()
    {
        installationCancelled = true;
        ShowInfo("Installation cancelled. Killing processes...");

        foreach (Process proc in launchedProcesses)
        {
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    proc.Kill();
                    UnityEngine.Debug.Log("Process killed: " + proc.ProcessName);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error killing process: " + e.Message);
            }
        }
        launchedProcesses.Clear();

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;
    }

    void OnConfirmOk()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        ExecuteInstallation();
    }

    void OnConfirmNo()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void ExecuteInstallation()
    {
        installationCancelled = false;
        installationComplete = false;
        StartCoroutine(InstallationFlow());
    }

    IEnumerator InstallationFlow()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (installButton != null) installButton.interactable = false;
        if (cancelButton != null) cancelButton.interactable = true;

        bool installWWAP = installWWAPToggle != null && installWWAPToggle.isOn;
        bool installDolphin = installDolphinToggle != null && installDolphinToggle.isOn;
        bool launchApps = launchAppsToggle != null && launchAppsToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "WindWakerAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        wwapInstalled = false;
        dolphinInstalled = false;
        wwapLaunched = false;
        dolphinLaunched = false;

        if (installWWAP && !installationCancelled)
        {
            ShowInfo("Installing Wind Waker AP Randomizer...");
            yield return InstallWWAP(documentsPath, tempDownloadPath, launchApps);
        }

        if (installDolphin && !installationCancelled)
        {
            ShowInfo("Installing Dolphin...");
            yield return InstallDolphin(documentsPath, tempDownloadPath, launchApps);
        }

        if (installationCancelled)
        {
            ShowInfo("Installation cancelled.");
            if (installButton != null) installButton.interactable = true;
            if (cancelButton != null) cancelButton.interactable = false;
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallWWAP(string documentsPath, string tempPath, bool launchApps)
    {
        wwapInstalled = false;

        if (string.IsNullOrEmpty(tlozwwAPDownloadUrl))
        {
            ShowInfo("ERROR: Wind Waker AP download URL not loaded!");
            yield break;
        }

        string zipPath = Path.Combine(tempPath, "wwrando_ap.zip");
        string extractPath = Path.Combine(tempPath, "wwap_extracted");
        string wwapDir = Path.Combine(documentsPath, "Wind Waker AP");

        ShowInfo("Downloading Wind Waker AP Randomizer...");
        yield return DownloadFile(tlozwwAPDownloadUrl, zipPath);

        if (!File.Exists(zipPath) || installationCancelled)
        {
            ShowInfo("ERROR: Wind Waker AP download failed!");
            yield break;
        }

        ShowInfo("Extracting Wind Waker AP Randomizer...");
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        ThreadTask extractTask = StartZipExtraction(zipPath, extractPath);
        while (!extractTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.1f);

        if (installationCancelled)
        {
            wwapInstalled = false;
            yield break;
        }

        if (extractTask.Exception != null)
        {
            UnityEngine.Debug.LogError("ZIP extraction failed: " + extractTask.Exception.Message);
            ShowInfo("ERROR: Failed to extract WWAP ZIP: " + extractTask.Exception.Message);
            wwapInstalled = false;
            yield break;
        }

        ShowInfo("Installing Wind Waker AP...");
        if (!Directory.Exists(wwapDir)) Directory.CreateDirectory(wwapDir);

        ThreadTask copyTask = StartCopyDirectoryTask(extractPath, wwapDir);
        while (!copyTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.1f);

        if (installationCancelled)
        {
            wwapInstalled = false;
            yield break;
        }

        if (copyTask.Exception != null)
        {
            UnityEngine.Debug.LogError("Directory copy failed: " + copyTask.Exception.Message);
            ShowInfo("ERROR: Failed to copy WWAP files: " + copyTask.Exception.Message);
            wwapInstalled = false;
            yield break;
        }

        wwapInstalled = true;

        SafeDeleteDirectory(extractPath);
        if (File.Exists(zipPath)) File.Delete(zipPath);

        ShowInfo("Wind Waker AP Randomizer installed!");

        // Créer ou mettre à jour settings.txt
        yield return new WaitForSeconds(1f);
        ShowInfo("Configuring settings.txt...");

        string settingsPath = Path.Combine(wwapDir, "settings.txt");

        // Vérifier si le fichier existe déjà
        if (File.Exists(settingsPath))
        {
            // Le fichier existe, remplacer les 3 premières lignes
            yield return StartCoroutine(UpdateExistingSettingsFile(settingsPath));
        }
        else
        {
            // Le fichier n'existe pas, créer un nouveau depuis le template
            yield return StartCoroutine(DownloadAndCreateSettingsFile(settingsPath));
        }

        if (!File.Exists(settingsPath))
        {
            UnityEngine.Debug.LogError("ERROR: Failed to create/update settings.txt!");
            ShowInfo("ERROR: Failed to create/update settings.txt!");
            yield break;
        }

        ShowInfo("Settings configured successfully!");
    }

    IEnumerator UpdateExistingSettingsFile(string settingsPath)
    {
        try
        {
            string[] allLines = File.ReadAllLines(settingsPath);

            // Remplacer les 3 premières lignes
            if (allLines.Length > 0) allLines[0] = $"clean_iso_path: {selectedRomPath}";
            if (allLines.Length > 1) allLines[1] = $"output_folder: {selectedInstallationPath}";
            if (allLines.Length > 2) allLines[2] = $"plando_file: {selectedPatchPath}";

            File.WriteAllLines(settingsPath, allLines);
            UnityEngine.Debug.Log("settings.txt updated successfully");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error updating settings.txt: " + e.Message);
            ShowInfo("ERROR: Failed to update settings.txt: " + e.Message);
        }

        yield return null;
    }

    IEnumerator DownloadAndCreateSettingsFile(string settingsPath)
    {
        UnityEngine.Debug.Log("Downloading settings template from: " + settingsTemplateUrl);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(settingsTemplateUrl))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Failed to download settings template: " + request.error);
                ShowInfo("ERROR: Failed to download settings template!");
                yield break;
            }

            try
            {
                string templateContent = request.downloadHandler.text;
                string[] lines = templateContent.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);

                // Remplacer les 3 premières lignes
                if (lines.Length > 0) lines[0] = $"clean_iso_path: {selectedRomPath}";
                if (lines.Length > 1) lines[1] = $"output_folder: {selectedInstallationPath}";
                if (lines.Length > 2) lines[2] = $"plando_file: {selectedPatchPath}";

                File.WriteAllLines(settingsPath, lines);
                UnityEngine.Debug.Log("settings.txt created successfully at: " + settingsPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error creating settings.txt: " + e.Message);
                ShowInfo("ERROR: Failed to create settings.txt: " + e.Message);
            }
        }
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath, bool launchApps)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(tlozwwDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        ShowInfo("Downloading Dolphin...");
        yield return DownloadFile(tlozwwDolphinDownloadUrl, sevenZPath);

        if (!File.Exists(sevenZPath) || installationCancelled)
        {
            ShowInfo("ERROR: Dolphin download failed!");
            yield break;
        }

        // Ne pas extraire le .7z, juste le copier dans Documents
        try
        {
            string dolphinDestPath = Path.Combine(dolphinDocumentsPath, "dolphin.7z");

            if (File.Exists(dolphinDestPath))
            {
                File.Delete(dolphinDestPath);
            }

            File.Copy(sevenZPath, dolphinDestPath);
            UnityEngine.Debug.Log("Dolphin.7z copied to Documents: " + dolphinDestPath);
            ShowInfo("Dolphin.7z placed in Documents!");

            dolphinInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying Dolphin.7z: " + e.Message);
            ShowInfo("ERROR: Failed to copy Dolphin.7z: " + e.Message);
            dolphinInstalled = false;
            yield break;
        }

        // Nettoyer le fichier temporaire
        if (File.Exists(sevenZPath))
        {
            try { File.Delete(sevenZPath); } catch { }
        }

        yield return null;
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
            else UnityEngine.Debug.Log("Download complete!");
        }
    }

    private ThreadTask StartZipExtraction(string zipPath, string extractPath)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try { ZipFile.ExtractToDirectory(zipPath, extractPath, true); }
            catch (System.Exception e) { UnityEngine.Debug.LogError("Error extracting ZIP: " + e.Message); task.Exception = e; }
            finally { task.Complete = true; }
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
            try { PerformDirectoryCopy(sourceDir, targetDir); }
            catch (System.Exception e) { UnityEngine.Debug.LogError("Error copying directory: " + e.Message); task.Exception = e; }
            finally { task.Complete = true; }
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

    void ShowInfo(string message)
    {
        if (infoText != null)
        {
            if (infoDefaultFontSize == 0f) infoDefaultFontSize = infoText.fontSize;

            if (message == specialCompleteMessage) infoText.fontSize = 28f;
            else if (infoDefaultFontSize != 0f) infoText.fontSize = infoDefaultFontSize;

            infoText.text = message;
        }

        if (infoPanel != null) infoPanel.SetActive(true);
        UnityEngine.Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoText != null && infoDefaultFontSize != 0f) infoText.fontSize = infoDefaultFontSize;
    }

    void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
