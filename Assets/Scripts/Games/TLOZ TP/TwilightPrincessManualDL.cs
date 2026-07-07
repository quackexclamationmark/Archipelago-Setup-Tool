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

public class TwilightPrincessManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER (optionnel)")]
    public FileDownloader downloader;
    public FileDownloader.FileData tloztpApworld;
    public FileDownloader.FileData tloztpAp;
    public FileDownloader.FileData tloztpRel;
    public FileDownloader.FileData tloztpDolphin;

    [Header("PANELS")]
    public GameObject romSelectionPanel;
    public GameObject installationPanel;

    [Header("ROM SELECTION UI")]
    public Button selectRomButton;
    public TMP_InputField romPathInputField;
    public TextMeshProUGUI romPathPlaceholder;
    public Button nextPanelButton;

    [Header("INSTALLATION UI")]
    public Toggle installApworldToggle;
    public Toggle installApworldGciToggle;
    public Toggle installApToggle;
    public Toggle installRelToggle;
    public Toggle installDolphinToggle;

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

    private string selectedRomPath = "";
    private string tloztpApworldDownloadUrl = "";
    private string tloztpApDownloadUrl = "";
    private string tloztpRelDownloadUrl = "";
    private string tloztpDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool apworldInstalled { get; private set; } = false;
    public bool apworldGciInstalled { get; private set; } = false;
    public bool apInstalled { get; private set; } = false;
    public bool relInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;

    private bool installationCancelled = false;
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
        public string tloztpApworld;
        public string tloztpAP;
        public string tloztpDolphin;
        public string tloztpREL;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! Twilight Princess AP Randomizer is ready to use.";

    void Start()
    {
        SetupFileBrowserQuickLinks();

        if (selectRomButton != null) selectRomButton.onClick.AddListener(SelectRom);
        if (nextPanelButton != null) nextPanelButton.onClick.AddListener(ValidateAndShowNextMessage);
        if (romPathInputField != null) romPathInputField.onValueChanged.AddListener(OnRomPathInputChanged);

        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (romSelectionPanel != null) romSelectionPanel.SetActive(true);
        if (installationPanel != null) installationPanel.SetActive(false);

        if (installApworldToggle != null) installApworldToggle.isOn = true;
        if (installApworldGciToggle != null) installApworldGciToggle.isOn = true;
        if (installApToggle != null) installApToggle.isOn = true;
        if (installRelToggle != null) installRelToggle.isOn = true;
        if (installDolphinToggle != null) installDolphinToggle.isOn = false;

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
        UnityEngine.Debug.Log("Loading Twilight Princess config from: " + configUrl);

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
                tloztpApworldDownloadUrl = remoteConfig.tloztpApworld;
                tloztpApDownloadUrl = remoteConfig.tloztpAP;
                tloztpRelDownloadUrl = remoteConfig.tloztpREL;
                tloztpDolphinDownloadUrl = remoteConfig.tloztpDolphin;
                UnityEngine.Debug.Log("Twilight Princess config loaded successfully.");
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

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select Twilight Princess ROM (.iso)");

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

        if (!fileName.Contains("Twilight Princess", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("ROM filename must contain 'Twilight Princess'.");
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

        if (!fileName.Contains("Twilight Princess", System.StringComparison.OrdinalIgnoreCase)) return false;

        if (!fileName.Contains("USA", System.StringComparison.OrdinalIgnoreCase) && !fileName.Contains("US", System.StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    void ValidateAndShowNextMessage()
    {
        if (!IsValidRom(selectedRomPath)) return;
        ShowInfo("Valid ROM - You can move on to the next step! (Setup panel)");
    }

    void OnInstallButtonClicked()
    {
        if (!IsValidRom(selectedRomPath))
        {
            ShowInfo("Please select a valid ROM file.");
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

        bool installApworld = installApworldToggle != null && installApworldToggle.isOn;
        bool installApworldGci = installApworldGciToggle != null && installApworldGciToggle.isOn;
        bool installAp = installApToggle != null && installApToggle.isOn;
        bool installRel = installRelToggle != null && installRelToggle.isOn;
        bool installDolphin = installDolphinToggle != null && installDolphinToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        string tempDownloadPath = Path.Combine(documentsPath, "TwilightPrincessAPTemp");
        string dolphinEmulatorPath = Path.Combine(appDataPath, "Dolphin Emulator");
        string gcUsaCardAPath = Path.Combine(dolphinEmulatorPath, "GC", "USA", "Card A");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        apworldInstalled = false;
        apworldGciInstalled = false;
        apInstalled = false;
        relInstalled = false;
        dolphinInstalled = false;

        // Télécharger le ZIP une seule fois et l'extraire
        string zipPath = Path.Combine(tempDownloadPath, "tpapworld.zip");
        string extractPath = Path.Combine(tempDownloadPath, "tpapworld_extracted");
        bool zipDownloadedAndExtracted = false;

        if ((installApworld || installApworldGci) && !installationCancelled)
        {
            if (string.IsNullOrEmpty(tloztpApworldDownloadUrl))
            {
                ShowInfo("ERROR: Twilight Princess APWorld download URL not loaded!");
                if (installButton != null) installButton.interactable = true;
                if (cancelButton != null) cancelButton.interactable = false;
                yield break;
            }

            ShowInfo("Downloading Twilight Princess APWorld...");
            yield return DownloadFile(tloztpApworldDownloadUrl, zipPath);

            if (!File.Exists(zipPath) || installationCancelled)
            {
                ShowInfo("ERROR: Twilight Princess APWorld download failed!");
                if (installButton != null) installButton.interactable = true;
                if (cancelButton != null) cancelButton.interactable = false;
                yield break;
            }

            ShowInfo("Extracting Twilight Princess APWorld...");
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);

            ThreadTask extractTask = StartZipExtraction(zipPath, extractPath);
            while (!extractTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.1f);

            if (installationCancelled)
            {
                if (installButton != null) installButton.interactable = true;
                if (cancelButton != null) cancelButton.interactable = false;
                yield break;
            }

            if (extractTask.Exception != null)
            {
                UnityEngine.Debug.LogError("ZIP extraction failed: " + extractTask.Exception.Message);
                ShowInfo("ERROR: Failed to extract APWorld ZIP: " + extractTask.Exception.Message);
                if (installButton != null) installButton.interactable = true;
                if (cancelButton != null) cancelButton.interactable = false;
                yield break;
            }

            zipDownloadedAndExtracted = true;
        }

        if (installApworld && !installationCancelled && zipDownloadedAndExtracted)
        {
            ShowInfo("Installing Twilight Princess APWorld...");
            yield return InstallApworld(extractPath);
        }

        if (installApworldGci && !installationCancelled && zipDownloadedAndExtracted)
        {
            ShowInfo("Installing Twilight Princess APWorld GCI...");
            yield return InstallApworldGci(extractPath, gcUsaCardAPath);
        }

        if (installAp && !installationCancelled)
        {
            ShowInfo("Installing Twilight Princess AP...");
            yield return InstallAp(gcUsaCardAPath, tempDownloadPath);
        }

        if (installRel && !installationCancelled)
        {
            ShowInfo("Installing REL Loader...");
            yield return InstallRel(gcUsaCardAPath, tempDownloadPath);
        }

        if (installDolphin && !installationCancelled)
        {
            ShowInfo("Installing Dolphin...");
            yield return InstallDolphin(documentsPath, tempDownloadPath);
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

    IEnumerator InstallApworld(string extractPath)
    {
        apworldInstalled = false;

        // Chercher le fichier "Twilight Princess.apworld" spécifiquement
        string apworldPath = Path.Combine(extractPath, "Twilight Princess.apworld");

        UnityEngine.Debug.Log("Looking for APWorld at: " + apworldPath);

        if (!File.Exists(apworldPath))
        {
            UnityEngine.Debug.LogError("ERROR: Twilight Princess.apworld not found at: " + apworldPath);

            // Try recursive search as fallback
            string[] apworldFiles = Directory.GetFiles(extractPath, "*.apworld", System.IO.SearchOption.AllDirectories);
            UnityEngine.Debug.Log("Recursive search found " + apworldFiles.Length + " .apworld files");

            if (apworldFiles.Length == 0)
            {
                UnityEngine.Debug.LogError("ERROR: No .apworld file found anywhere in: " + extractPath);
                ShowInfo("ERROR: No .apworld file found in extracted ZIP!");
                apworldInstalled = false;
                yield break;
            }

            apworldPath = apworldFiles[0];
            UnityEngine.Debug.Log("Using fallback file: " + apworldPath);
        }

        try
        {
            string fileName = Path.GetFileName(apworldPath);
            UnityEngine.Debug.Log("Processing APWorld: " + fileName);

            // Try multiple target paths (like DREDGE does)
            string[] targetPaths = new string[]
            {
                Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", fileName),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", fileName),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", fileName),
            };

            string destPath = "";
            foreach (string path in targetPaths)
            {
                try
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    destPath = path;
                    UnityEngine.Debug.Log("Using target path: " + destPath);
                    break;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message);
                }
            }

            if (string.IsNullOrEmpty(destPath))
            {
                UnityEngine.Debug.LogError("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
                ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
                apworldInstalled = false;
                yield break;
            }

            // Supprimer l'ancien fichier s'il existe
            if (File.Exists(destPath))
            {
                try
                {
                    File.Delete(destPath);
                    UnityEngine.Debug.Log("Deleted old APWorld: " + fileName);
                }
                catch { }
            }

            File.Copy(apworldPath, destPath, true);
            UnityEngine.Debug.Log("APWorld copied to: " + destPath);
            UnityEngine.Debug.Log("File size: " + new FileInfo(destPath).Length + " bytes");

            apworldInstalled = true;
            ShowInfo("Twilight Princess APWorld installed!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying APWorld: " + e.Message);
            UnityEngine.Debug.LogError("Stack trace: " + e.StackTrace);
            ShowInfo("ERROR: Failed to copy APWorld: " + e.Message);
            apworldInstalled = false;
            yield break;
        }

        yield return null;
    }

    IEnumerator InstallApworldGci(string extractPath, string gcUsaCardAPath)
    {
        apworldGciInstalled = false;

        // Créer la structure de dossiers si nécessaire
        if (!Directory.Exists(gcUsaCardAPath)) Directory.CreateDirectory(gcUsaCardAPath);

        // Chercher le fichier .gci dans le dossier extrait
        string[] gciFiles = Directory.GetFiles(extractPath, "*.gci", System.IO.SearchOption.AllDirectories);

        if (gciFiles.Length == 0)
        {
            UnityEngine.Debug.LogError("ERROR: No .gci file found in extracted ZIP!");
            ShowInfo("ERROR: No .gci file found in extracted ZIP!");
            apworldGciInstalled = false;
            yield break;
        }

        try
        {
            foreach (string gciFile in gciFiles)
            {
                string fileName = Path.GetFileName(gciFile);
                string destPath = Path.Combine(gcUsaCardAPath, fileName);

                // Supprimer l'ancien fichier s'il existe
                if (File.Exists(destPath))
                {
                    try
                    {
                        File.Delete(destPath);
                    }
                    catch { }
                }

                File.Copy(gciFile, destPath, true);
                UnityEngine.Debug.Log("APWorld GCI copied: " + destPath);
            }

            apworldGciInstalled = true;
            ShowInfo("Twilight Princess APWorld GCI installed!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying APWorld GCI: " + e.Message);
            ShowInfo("ERROR: Failed to copy APWorld GCI: " + e.Message);
            apworldGciInstalled = false;
            yield break;
        }

        yield return null;
    }

    IEnumerator InstallAp(string gcUsaCardAPath, string tempPath)
    {
        apInstalled = false;

        if (string.IsNullOrEmpty(tloztpApDownloadUrl))
        {
            ShowInfo("ERROR: Twilight Princess AP download URL not loaded!");
            yield break;
        }

        string gciPath = Path.Combine(tempPath, "tpap.gci");

        ShowInfo("Downloading Twilight Princess AP...");
        yield return DownloadFile(tloztpApDownloadUrl, gciPath);

        if (!File.Exists(gciPath) || installationCancelled)
        {
            ShowInfo("ERROR: Twilight Princess AP download failed!");
            yield break;
        }

        ShowInfo("Installing Twilight Princess AP...");

        // Créer la structure de dossiers si nécessaire
        if (!Directory.Exists(gcUsaCardAPath)) Directory.CreateDirectory(gcUsaCardAPath);

        try
        {
            string destPath = Path.Combine(gcUsaCardAPath, "Tpr-E-APTest_APT-aptest.gci");

            // Supprimer l'ancien fichier s'il existe
            if (File.Exists(destPath))
            {
                try
                {
                    File.Delete(destPath);
                }
                catch { }
            }

            File.Copy(gciPath, destPath, true);
            UnityEngine.Debug.Log("Twilight Princess AP installed: " + destPath);
            apInstalled = true;
            ShowInfo("Twilight Princess AP installed!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing AP: " + e.Message);
            ShowInfo("ERROR: Failed to install AP: " + e.Message);
            apInstalled = false;
            yield break;
        }

        if (File.Exists(gciPath)) File.Delete(gciPath);

        yield return null;
    }

    IEnumerator InstallRel(string gcUsaCardAPath, string tempPath)
    {
        relInstalled = false;

        if (string.IsNullOrEmpty(tloztpRelDownloadUrl))
        {
            ShowInfo("ERROR: REL Loader download URL not loaded!");
            yield break;
        }

        string gciPath = Path.Combine(tempPath, "tprel.gci");

        ShowInfo("Downloading REL Loader...");
        yield return DownloadFile(tloztpRelDownloadUrl, gciPath);

        if (!File.Exists(gciPath) || installationCancelled)
        {
            ShowInfo("ERROR: REL Loader download failed!");
            yield break;
        }

        ShowInfo("Installing REL Loader...");

        // Créer la structure de dossiers si nécessaire
        if (!Directory.Exists(gcUsaCardAPath)) Directory.CreateDirectory(gcUsaCardAPath);

        try
        {
            string destPath = Path.Combine(gcUsaCardAPath, "GZ2E01_REL_Loader_v2.gci");

            // Supprimer l'ancien fichier s'il existe
            if (File.Exists(destPath))
            {
                try
                {
                    File.Delete(destPath);
                }
                catch { }
            }

            File.Copy(gciPath, destPath, true);
            UnityEngine.Debug.Log("REL Loader installed: " + destPath);
            relInstalled = true;
            ShowInfo("REL Loader installed!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing REL Loader: " + e.Message);
            ShowInfo("ERROR: Failed to install REL Loader: " + e.Message);
            relInstalled = false;
            yield break;
        }

        if (File.Exists(gciPath)) File.Delete(gciPath);

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(tloztpDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        ShowInfo("Downloading Dolphin...");
        yield return DownloadFile(tloztpDolphinDownloadUrl, sevenZPath);

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
