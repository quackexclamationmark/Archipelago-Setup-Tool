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

public class SSBBManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER (optionnel)")]
    public FileDownloader downloader;
    public FileDownloader.FileData ssbbApworld;
    public FileDownloader.FileData ssbbDolphin;
    public FileDownloader.FileData ssbbMinus;

    [Header("PANELS")]
    public GameObject romSelectionPanel;
    public GameObject installationPanel;

    [Header("ROM SELECTION UI")]
    public Button selectRomButton;
    public TMP_InputField romPathInputField;
    public TextMeshProUGUI romPathPlaceholder;
    public Button nextPanelButton;

    [Header("INSTALLATION UI")]
    public Toggle installSSBBAPToggle;
    public Toggle installDolphinToggle;
    public Toggle installMinusToggle;

    public Button patchDolphinButton;
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
    private string ssbbApworldDownloadUrl = "";
    private string ssbbDolphinDownloadUrl = "";
    private string ssbbMinusDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool ssbbapInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;
    public bool minusInstalled { get; private set; } = false;

    public bool ssbbapLaunched { get; private set; } = false;
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
        public string ssbbApworld;
        public string ssbbDolphin;
        public string ssbbMinus;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! Super Smash Bros Brawl AP Randomizer is ready to use.";

    void Start()
    {
        SetupFileBrowserQuickLinks();

        if (selectRomButton != null) selectRomButton.onClick.AddListener(SelectRom);
        if (nextPanelButton != null) nextPanelButton.onClick.AddListener(ValidateAndUpdateDefaultISO);
        if (romPathInputField != null) romPathInputField.onValueChanged.AddListener(OnRomPathInputChanged);

        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);
        if (patchDolphinButton != null) patchDolphinButton.onClick.AddListener(OnPatchDolphinClicked);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (romSelectionPanel != null) romSelectionPanel.SetActive(true);
        if (installationPanel != null) installationPanel.SetActive(false);

        if (installSSBBAPToggle != null) installSSBBAPToggle.isOn = true;
        if (installDolphinToggle != null) installDolphinToggle.isOn = false;
        if (installMinusToggle != null) installMinusToggle.isOn = true;

        if (cancelButton != null) cancelButton.interactable = false;
        if (patchDolphinButton != null) patchDolphinButton.interactable = false;

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
        UnityEngine.Debug.Log("Loading SSBB config from: " + configUrl);

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
                ssbbApworldDownloadUrl = remoteConfig.ssbbApworld;
                ssbbDolphinDownloadUrl = remoteConfig.ssbbDolphin;
                ssbbMinusDownloadUrl = remoteConfig.ssbbMinus;
                UnityEngine.Debug.Log("SSBB config loaded successfully.");
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

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select SSBB ROM (.iso or .wbfs)");

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

        string extension = Path.GetExtension(romPath).ToLower();
        if (extension != ".iso" && extension != ".wbfs")
        {
            ShowInfo("ROM must be .iso or .wbfs format.");
            return false;
        }

        return true;
    }

    bool IsValidRomSilent(string romPath)
    {
        if (string.IsNullOrEmpty(romPath)) return false;
        if (!File.Exists(romPath)) return false;

        string extension = Path.GetExtension(romPath).ToLower();
        if (extension != ".iso" && extension != ".wbfs") return false;

        return true;
    }

    void ValidateAndUpdateDefaultISO()
    {
        if (!IsValidRom(selectedRomPath)) return;

        // Update DefaultISO in Dolphin.ini
        StartCoroutine(UpdateDefaultISO());
    }

    IEnumerator UpdateDefaultISO()
    {
        string dolphinConfigPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Dolphin Emulator", "Config", "Dolphin.ini"
        );

        // If config doesn't exist, just show success message and don't create it yet
        if (!File.Exists(dolphinConfigPath))
        {
            UnityEngine.Debug.Log("Dolphin config not found yet, will create it when patching");
            ShowInfo("Valid ROM - You can move on to the next step! (Setup panel)");
            yield break;
        }

        try
        {
            string[] lines = File.ReadAllLines(dolphinConfigPath);
            string romPath = selectedRomPath.Replace("\\", "/");
            bool foundDefaultISO = false;

            // Update DefaultISO with full ROM path
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("DefaultISO ="))
                {
                    lines[i] = "DefaultISO = " + romPath;
                    foundDefaultISO = true;
                    break;
                }
            }

            if (!foundDefaultISO)
            {
                // Find [Core] section and add DefaultISO there
                System.Collections.Generic.List<string> lineList = new System.Collections.Generic.List<string>(lines);
                int coreIndex = -1;

                for (int i = 0; i < lineList.Count; i++)
                {
                    if (lineList[i].Trim() == "[Core]")
                    {
                        coreIndex = i;
                        break;
                    }
                }

                if (coreIndex != -1)
                {
                    // Add after [Core] section
                    lineList.Insert(coreIndex + 1, "DefaultISO = " + romPath);
                }
                else
                {
                    // Create [Core] section and add DefaultISO
                    lineList.Add("");
                    lineList.Add("[Core]");
                    lineList.Add("DefaultISO = " + romPath);
                }

                lines = lineList.ToArray();
            }

            File.WriteAllLines(dolphinConfigPath, lines);
            UnityEngine.Debug.Log("DefaultISO updated to: " + romPath);
            ShowInfo("Valid ROM - You can move on to the next step! (Setup panel)");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error updating Dolphin config: " + e.Message);
            ShowInfo("ERROR: Failed to update Dolphin config: " + e.Message);
        }

        yield return null;
    }

    void OnInstallButtonClicked()
    {
        if (!IsValidRom(selectedRomPath))
        {
            ShowInfo("Please select a valid ROM file (.iso or .wbfs).");
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

    void OnPatchDolphinClicked()
    {
        StartCoroutine(PatchDolphinConfig());
    }

    IEnumerator PatchDolphinConfig()
    {
        string dolphinConfigPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Dolphin Emulator", "Config", "Dolphin.ini"
        );

        if (!File.Exists(dolphinConfigPath))
        {
            ShowInfoWithFontSize("Config file not found, please install Dolphin, launch it and select the directory where your ROM is located.", 26f);
            yield break;
        }

        try
        {
            var lines = new System.Collections.Generic.List<string>(File.ReadAllLines(dolphinConfigPath));

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].TrimStart().StartsWith("WiiSDCardSyncFolder ="))
                    lines.RemoveAt(i);
            }

            string isoPath = "";
            int isoPathIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("ISOPath0 ="))
                {
                    isoPath = lines[i].Substring("ISOPath0 = ".Length).Trim();
                    isoPathIndex = i;
                    UnityEngine.Debug.Log("Found ISOPath0: " + isoPath);
                    break;
                }
            }

            if (string.IsNullOrEmpty(isoPath))
            {
                if (!string.IsNullOrEmpty(selectedRomPath))
                    isoPath = Path.GetDirectoryName(selectedRomPath).Replace("\\", "/");
                else
                    isoPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments).Replace("\\", "/");
            }

            isoPath = isoPath.Replace("\\", "/");
            string sdCardPath = (isoPath.EndsWith("/") ? isoPath.TrimEnd('/') : isoPath) + "/sd_card";
            sdCardPath = sdCardPath.Replace("\\", "/");

            int generalIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "[General]")
                {
                    generalIndex = i;
                    break;
                }
            }

            if (generalIndex != -1)
            {
                int generalEnd = lines.Count;
                for (int i = generalIndex + 1; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("[") && lines[i].TrimEnd().EndsWith("]"))
                    {
                        generalEnd = i;
                        break;
                    }
                }

                int insertPos = generalIndex + 1;
                int foundIndex = -1;
                for (int i = generalIndex + 1; i < generalEnd; i++)
                {
                    if (lines[i].StartsWith("ISOPath0 ="))
                    {
                        foundIndex = i;
                        break;
                    }
                }
                if (foundIndex != -1) lines[foundIndex] = "ISOPath0 = " + isoPath;
                else { lines.Insert(insertPos, "ISOPath0 = " + isoPath); insertPos++; generalEnd++; }

                foundIndex = -1;
                for (int i = generalIndex + 1; i < generalEnd; i++)
                {
                    if (lines[i].StartsWith("ISOPaths ="))
                    {
                        foundIndex = i;
                        break;
                    }
                }
                if (foundIndex != -1) lines[foundIndex] = "ISOPaths = 1";
                else { lines.Insert(insertPos, "ISOPaths = 1"); insertPos++; generalEnd++; }

                for (int i = generalIndex + 1; i < generalEnd; i++)
                {
                    if (lines[i].TrimStart().StartsWith("WiiSDCardSyncFolder ="))
                    {
                        lines.RemoveAt(i);
                        generalEnd--;
                        i--;
                    }
                }

                lines.Insert(insertPos, "WiiSDCardSyncFolder = " + sdCardPath);
            }
            else
            {
                int firstSection = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("[") && lines[i].TrimEnd().EndsWith("]"))
                    {
                        firstSection = i;
                        break;
                    }
                }

                var toInsert = new System.Collections.Generic.List<string>();
                toInsert.Add("");
                toInsert.Add("[General]");
                toInsert.Add("ISOPath0 = " + isoPath);
                toInsert.Add("ISOPaths = 1");
                toInsert.Add("WiiSDCardSyncFolder = " + sdCardPath);

                if (firstSection != -1)
                {
                    lines.InsertRange(firstSection, toInsert);
                }
                else
                {
                    lines.AddRange(toInsert);
                }
            }

            string romPath = selectedRomPath.Replace("\\", "/");
            int coreIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "[Core]")
                {
                    coreIndex = i;
                    break;
                }
            }

            if (coreIndex != -1)
            {
                int coreEnd = lines.Count;
                for (int i = coreIndex + 1; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("[") && lines[i].TrimEnd().EndsWith("]"))
                    {
                        coreEnd = i;
                        break;
                    }
                }

                int defaultIsoIndex = -1;
                for (int i = coreIndex + 1; i < coreEnd; i++)
                {
                    if (lines[i].StartsWith("DefaultISO ="))
                    {
                        defaultIsoIndex = i;
                        break;
                    }
                }

                if (defaultIsoIndex != -1) lines[defaultIsoIndex] = "DefaultISO = " + romPath;
                else lines.Insert(coreIndex + 1, "DefaultISO = " + romPath);
            }
            else
            {
                lines.Add("");
                lines.Add("[Core]");
                lines.Add("DefaultISO = " + romPath);
            }

            File.WriteAllLines(dolphinConfigPath, lines.ToArray());
            ShowInfo("Dolphin configuration patched successfully!");
            UnityEngine.Debug.Log("Dolphin config patched successfully - WiiSDCardSyncFolder set to: " + sdCardPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error patching Dolphin config: " + e.Message);
            ShowInfo("ERROR: Failed to patch Dolphin config: " + e.Message);
        }

        yield return null;
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
        if (patchDolphinButton != null) patchDolphinButton.interactable = false;

        bool installSSBBAP = installSSBBAPToggle != null && installSSBBAPToggle.isOn;
        bool installDolphin = installDolphinToggle != null && installDolphinToggle.isOn;
        bool installMinus = installMinusToggle != null && installMinusToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "SSBBAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        ssbbapInstalled = false;
        dolphinInstalled = false;
        minusInstalled = false;
        ssbbapLaunched = false;
        dolphinLaunched = false;

        if (installSSBBAP && !installationCancelled)
        {
            ShowInfo("Installing SSBB AP Randomizer...");
            yield return InstallSSBBAP(documentsPath, tempDownloadPath);
        }

        if (installDolphin && !installationCancelled)
        {
            ShowInfo("Installing Dolphin...");
            yield return InstallDolphin(documentsPath, tempDownloadPath);
        }

        if (installMinus && !installationCancelled)
        {
            ShowInfo("Installing Minus Launcher...");
            yield return InstallMinus(tempDownloadPath);
        }

        if (installationCancelled)
        {
            ShowInfo("Installation cancelled.");
            if (installButton != null) installButton.interactable = true;
            if (cancelButton != null) cancelButton.interactable = false;
            if (patchDolphinButton != null) patchDolphinButton.interactable = true;
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;
        if (patchDolphinButton != null) patchDolphinButton.interactable = true;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallSSBBAP(string documentsPath, string tempPath)
    {
        ssbbapInstalled = false;

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(ssbbApworldDownloadUrl))
        {
            ShowInfo("ERROR: SSBB AP download URL not loaded!");
            yield break;
        }

        string fileName = ssbbApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ssbbApworldDownloadUrl.Substring(ssbbApworldDownloadUrl.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        ShowInfo("Downloading SSBB APWorld...");
        yield return DownloadFile(ssbbApworldDownloadUrl, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
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

        try
        {
            if (File.Exists(target))
                File.Delete(target);

            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);
            ShowInfo("APWorld installed successfully!");
            ssbbapInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            ssbbapInstalled = false;
            yield break;
        }

        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(ssbbDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        ShowInfo("Downloading Dolphin...");
        yield return DownloadFile(ssbbDolphinDownloadUrl, sevenZPath);

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

    IEnumerator InstallMinus(string tempPath)
    {
        minusInstalled = false;

        if (string.IsNullOrEmpty(ssbbMinusDownloadUrl))
        {
            ShowInfo("ERROR: Minus Launcher download URL not loaded!");
            yield break;
        }

        string romDirectory = Path.GetDirectoryName(selectedRomPath);
        string elfPath = Path.Combine(romDirectory, "Minus Launcher.elf");

        ShowInfo("Downloading Minus Launcher...");
        yield return DownloadFile(ssbbMinusDownloadUrl, elfPath);

        if (!File.Exists(elfPath) || installationCancelled)
        {
            ShowInfo("ERROR: Minus Launcher download failed!");
            yield break;
        }

        minusInstalled = true;
        ShowInfo("Minus Launcher installed!");
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

    void ShowInfoWithFontSize(string message, float fontSize)
    {
        if (infoText != null)
        {
            infoText.fontSize = fontSize;
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
