using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class VotVManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("VOTV FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData ue4ss;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installUE4SSToggle;
    public Toggle downloadVotVToggle;

    [Header("DIRECTORY SELECTION - VotV")]
    public Button selectDirectoryButton;
    public TMP_InputField selectedPathInputField;

    [Header("DIRECTORY SELECTION - Download")]
    public Button selectDownloadDirectoryButton;
    public TMP_InputField downloadPathInputField;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsToggle;
    public Toggle fullCleanUE4SSToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("CONFIRMATION PANEL - DOWNLOADER")]
    public GameObject downloadConfirmationPanel;
    public TextMeshProUGUI downloadConfirmationMessage;
    public Button downloadConfirmButton;
    public Button downloadCancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("DOWNLOAD BUTTON")]
    public Button downloadButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    private string votVPath;
    private string downloadPath;
    private string pendingAction;
    private VotVConfig remoteConfig;
    private bool configLoaded = false;
    private bool hasRunSetup = false;

    private const string VOTVDOWNLOAD_FILENAME = "Voices Of The Void 0.9.0j.7z";

    [System.Serializable]
    public class VotVConfig
    {
        public string votvAP;
        public string votvUE4SS;
        public string votvApworld;
        public string votvDL;
    }

    void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (downloadConfirmationPanel != null)
            downloadConfirmationPanel.SetActive(false);

        if (downloadConfirmButton != null)
            downloadConfirmButton.onClick.AddListener(OnDownloadConfirm);

        if (downloadCancelButton != null)
            downloadCancelButton.onClick.AddListener(OnDownloadCancel);

        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(OnSelectDirectoryClick);

        if (selectDownloadDirectoryButton != null)
            selectDownloadDirectoryButton.onClick.AddListener(OnSelectDownloadDirectoryClick);

        if (downloadButton != null)
            downloadButton.onClick.AddListener(OnDownloadClick);

        if (removeAPModsToggle != null)
            removeAPModsToggle.isOn = true;

        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.isOn = false;

        if (fullCleanUE4SSToggle != null)
            fullCleanUE4SSToggle.onValueChanged.AddListener(OnFullCleanChanged);

        InitializeDefaultPaths();

        StartCoroutine(LoadRemoteConfig());
    }

    void InitializeDefaultPaths()
    {
        string defaultDocsPath = Path.Combine("C:\\Users", System.Environment.UserName, "Documents");

        if (selectedPathInputField != null)
        {
            selectedPathInputField.text = defaultDocsPath;
            votVPath = defaultDocsPath;
            UnityEngine.Debug.Log("VotV path initialized to: " + votVPath);
        }

        if (downloadPathInputField != null)
        {
            downloadPathInputField.text = defaultDocsPath;
            downloadPath = defaultDocsPath;
            UnityEngine.Debug.Log("Download path initialized to: " + downloadPath);
        }
    }

    void ApplyVotVConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.votvAP;
        ue4ss.url = remoteConfig.votvUE4SS;
        apworld.url = remoteConfig.votvApworld;
    }

    // ========== SETUP & REVERT ==========

    public void RunSetup()
    {
        if (string.IsNullOrEmpty(votVPath))
        {
            ShowInfo("Please select VotV directory first!");
            return;
        }

        if (hasRunSetup)
        {
            ShowInfo("Setup has already been run. Please revert first if you want to reinstall.");
            return;
        }

        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void RevertAll()
    {
        if (string.IsNullOrEmpty(votVPath))
        {
            ShowInfo("Please select VotV directory first!");
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
                hasRunSetup = true;
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
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(votVPath))
        {
            ShowInfo("VotV path not found.");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool ue4ssToggle = installUE4SSToggle != null && installUE4SSToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (ue4ssToggle ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (ue4ssToggle && count == 1)
        {
            StartCoroutine(UE4SSOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        if (string.IsNullOrEmpty(votVPath))
        {
            ShowInfo("VotV path not set.");
            return;
        }

        bool removeAP = removeAPModsToggle != null && removeAPModsToggle.isOn;
        bool fullClean = fullCleanUE4SSToggle != null && fullCleanUE4SSToggle.isOn;

        UnityEngine.Debug.Log($"ExecuteRevert - removeAP: {removeAP}, fullClean: {fullClean}, votVPath: {votVPath}");

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (fullClean)
        {
            ShowInfo("Removing UE4SS and dwmapi.dll...");

            string win64Path = Path.Combine(votVPath, "VotV", "Binaries", "Win64");
            string ue4ssPath = Path.Combine(win64Path, "ue4ss");
            string dwmapiPath = Path.Combine(win64Path, "dwmapi.dll");

            UnityEngine.Debug.Log($"Deleting: {ue4ssPath}");
            UnityEngine.Debug.Log($"Deleting: {dwmapiPath}");

            SafeDeleteDirectory(ue4ssPath);
            SafeDeleteFile(dwmapiPath);

            UnityEngine.Debug.Log("Full clean completed!");
            ShowInfo("Full clean completed!");
            hasRunSetup = false;
            return;
        }

        if (removeAP)
        {
            ShowInfo("Removing AP mod...");

            string modsPath = Path.Combine(votVPath, "VotV", "Binaries", "Win64", "ue4ss", "Mods");
            string apModPath = Path.Combine(modsPath, "votv_ap-main");

            UnityEngine.Debug.Log($"Deleting: {apModPath}");

            if (Directory.Exists(modsPath))
            {
                SafeDeleteDirectory(apModPath);
            }

            DeleteOldVersionFiles();
            UnityEngine.Debug.Log("AP mod removed!");
            ShowInfo("AP mod removed successfully!");
            hasRunSetup = false;
        }
    }

    bool HasOtherMods()
    {
        string modsPath = Path.Combine(votVPath, "VotV", "Binaries", "Win64", "ue4ss", "Mods");

        if (!Directory.Exists(modsPath))
            return false;

        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string dir in dirs)
        {
            string dirName = Path.GetFileName(dir);
            if (dirName != "votv_ap-main")
                return true;
        }

        return false;
    }

    // ========== INSTALL FLOWS ==========

    IEnumerator InstallFlow()
    {
        ShowInfo("Please wait...\n\nInstalling APWorld...");
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            yield return InstallAPWorld();
        }

        ShowInfo("Please wait...\n\nInstalling UE4SS...");
        if (installUE4SSToggle != null && installUE4SSToggle.isOn)
        {
            yield return InstallUE4SS();
        }

        ShowInfo("Please wait...\n\nInstalling AP Mod...");
        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            yield return InstallAPMod();
        }

        CreateVersionFile(apMod.url, ue4ss.url, apworld.url);

        ShowInfo("Installation complete!");

        yield return null;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            yield break;
        }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        yield return DownloadFile(apworld.url, localPath);

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
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find Archipelago custom_worlds directory!");
            yield break;
        }

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }
    }

    IEnumerator InstallUE4SS()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "UE4SSTemp");
        yield return downloader.DownloadAndExtract(ue4ss, Application.persistentDataPath, extractPath);

        string targetPath = Path.Combine(votVPath, "VotV", "Binaries", "Win64");
        Directory.CreateDirectory(targetPath);

        string sourceUE4SSFolder = FindDirectory(extractPath, "ue4ss");
        if (!string.IsNullOrEmpty(sourceUE4SSFolder))
        {
            string destUE4SSFolder = Path.Combine(targetPath, "ue4ss");
            if (Directory.Exists(destUE4SSFolder))
                SafeDeleteDirectory(destUE4SSFolder);

            MoveDirectory(sourceUE4SSFolder, destUE4SSFolder);
            UnityEngine.Debug.Log("UE4SS folder moved to: " + destUE4SSFolder);
        }

        string dwmapiSource = FindFile(extractPath, "dwmapi.dll");
        if (!string.IsNullOrEmpty(dwmapiSource))
        {
            string dwmapiDest = Path.Combine(targetPath, "dwmapi.dll");
            File.Copy(dwmapiSource, dwmapiDest, true);
            UnityEngine.Debug.Log("dwmapi.dll copied to: " + dwmapiDest);
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("UE4SS installed successfully!");
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "VotVAPModTemp");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string modsPath = Path.Combine(votVPath, "VotV", "Binaries", "Win64", "ue4ss", "Mods");
        Directory.CreateDirectory(modsPath);

        string sourceModFolder = FindDirectory(extractPath, "votv_ap-main");
        if (string.IsNullOrEmpty(sourceModFolder))
        {
            sourceModFolder = FindDirectory(extractPath, "votv_ap");
        }

        if (!string.IsNullOrEmpty(sourceModFolder))
        {
            string destModFolder = Path.Combine(modsPath, "votv_ap-main");

            if (Directory.Exists(destModFolder))
                SafeDeleteDirectory(destModFolder);

            if (Directory.Exists(Path.Combine(sourceModFolder, "votv_ap-main")))
            {
                MoveDirectory(Path.Combine(sourceModFolder, "votv_ap-main"), destModFolder);
            }
            else
            {
                MoveDirectory(sourceModFolder, destModFolder);
            }

            UnityEngine.Debug.Log("AP Mod folder moved to: " + destModFolder);
        }

        SafeDeleteDirectory(extractPath);

        ShowInfo("AP Mod installed successfully!");
    }

    IEnumerator APWorldOnlyFlow()
    {
        ShowInfo("Please wait...\n\nInstalling APWorld...");
        yield return InstallAPWorld();
    }

    IEnumerator UE4SSOnlyFlow()
    {
        ShowInfo("Please wait...\n\nInstalling UE4SS...");
        yield return InstallUE4SS();
    }

    IEnumerator APModOnlyFlow()
    {
        ShowInfo("Please wait...\n\nInstalling AP Mod...");
        yield return InstallAPMod();
    }

    // ========== DOWNLOADER ==========

    void OnDownloadClick()
    {
        downloadPath = downloadPathInputField != null ? downloadPathInputField.text : "";

        if (string.IsNullOrEmpty(downloadPath))
        {
            ShowInfo("Please select download directory first!");
            return;
        }

        ShowDownloadConfirmation("Download Voices of the Void v0.9.0j?");
    }

    private void ShowDownloadConfirmation(string message)
    {
        if (downloadConfirmationMessage != null)
            downloadConfirmationMessage.text = message;

        if (downloadConfirmationPanel != null)
            downloadConfirmationPanel.SetActive(true);
    }

    private void OnDownloadConfirm()
    {
        if (downloadConfirmationPanel != null)
            downloadConfirmationPanel.SetActive(false);

        StartCoroutine(DownloadVotVFile());
    }

    private void OnDownloadCancel()
    {
        if (downloadConfirmationPanel != null)
            downloadConfirmationPanel.SetActive(false);
    }

    IEnumerator DownloadVotVFile()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (remoteConfig == null || string.IsNullOrEmpty(remoteConfig.votvDL))
        {
            ShowInfo("ERROR: Download URL not available!");
            yield break;
        }

        ShowInfo("Please wait...\n\nDownloading Voices of the Void...");

        string savePath = Path.Combine(downloadPath, VOTVDOWNLOAD_FILENAME);

        yield return DownloadFile(remoteConfig.votvDL, savePath);

        if (File.Exists(savePath))
        {
            ShowInfo($"Download complete!\nFile saved to:\n{savePath}");
        }
        else
        {
            ShowInfo("ERROR: Download failed!");
        }
    }

    // ========== FILE OPERATIONS ==========

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
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

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed: " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<VotVConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyVotVConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed: " + e.Message);
        }

        configLoaded = true;
    }

    void OnSelectDirectoryClick()
    {
        StartCoroutine(ShowVotVFileBrowser());
    }

    void OnSelectDownloadDirectoryClick()
    {
        StartCoroutine(ShowDownloadFileBrowser());
    }

    IEnumerator ShowVotVFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;

        string startPath = string.IsNullOrEmpty(votVPath)
            ? Path.Combine("C:\\Users", System.Environment.UserName, "Documents")
            : votVPath;

        FileBrowser.SetFilters(true, new FileBrowser.Filter("Executable", ".exe"), new FileBrowser.Filter("All Files", "*"));

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.FilesAndFolders,
            false,
            startPath,
            "Select VotV Directory or VotV.exe"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];

            if (selectedPath.EndsWith(".exe"))
            {
                selectedPath = Path.GetDirectoryName(selectedPath);
            }

            if (File.Exists(Path.Combine(selectedPath, "VotV.exe")))
            {
                votVPath = selectedPath;
                if (selectedPathInputField != null)
                    selectedPathInputField.text = votVPath;

                UnityEngine.Debug.Log("VotV directory selected: " + votVPath);
            }
            else
            {
                ShowInfo("ERROR: VotV.exe not found in the selected directory!\nPlease select the correct VotV installation folder.");
                UnityEngine.Debug.LogError("VotV.exe not found at: " + Path.Combine(selectedPath, "VotV.exe"));
            }
        }
    }

    IEnumerator ShowDownloadFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;

        string startPath = string.IsNullOrEmpty(downloadPath)
            ? Path.Combine("C:\\Users", System.Environment.UserName, "Documents")
            : downloadPath;

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            startPath,
            "Select Download Folder for VotV"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            downloadPath = FileBrowser.Result[0];
            if (downloadPathInputField != null)
                downloadPathInputField.text = downloadPath;

            UnityEngine.Debug.Log("Download directory selected: " + downloadPath);
        }
    }

    void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                UnityEngine.Debug.Log("File deleted: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error deleting file: " + path + " - " + e.Message);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                UnityEngine.Debug.Log("Directory deleted: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error deleting directory: " + path + " - " + e.Message);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }
    }

    public void ShowInfo(string message)
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

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsToggle != null)
        {
            removeAPModsToggle.isOn = false;
            removeAPModsToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName)
                    return file;
        }
        catch { }

        return "";
    }

    string FindDirectory(string root, string dirName)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(dir) == dirName)
                    return dir;
            }
        }
        catch { }

        return "";
    }

    void CreateVersionFile(string apmodUrl, string ue4ssUrl, string apworldUrl)
    {
        try
        {
            string apmodVersion = ExtractVersionFromUrl(apmodUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string ue4ssVersion = ExtractVersionFromUrl(ue4ssUrl, @"(?:/releases/download/|/download/[^/]+/[^/]+/)([^/]+)/?$");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = "VotV APMod Version " + apmodVersion + ".txt";
            string content = "Voices of the Void Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== AP MOD ===\n";
            content += "Downloaded from: " + apmodUrl + "\n";
            content += "Version: " + apmodVersion + "\n";
            content += "\n";
            content += "=== APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== UE4SS ===\n";
            content += "Downloaded from: " + ue4ssUrl + "\n";
            content += "Version: " + ue4ssVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string rootVersionPath = Path.Combine(votVPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created: " + rootVersionPath);
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"VotV APMod Version .+\.txt");

            if (Directory.Exists(votVPath))
            {
                string[] rootFiles = Directory.GetFiles(votVPath);
                foreach (string file in rootFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
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
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }
}