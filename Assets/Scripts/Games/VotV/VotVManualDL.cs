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

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    private string votVPath;
    private string downloadPath;
    private string pendingAction;
    private VotVConfig remoteConfig;
    private bool configLoaded = false;

    private const string VOTVDOWNLOAD_FILENAME = "Voices Of The Void 0.9.0j.zip";

    [System.Serializable]
    public class VotVConfig
    {
        public string votvAP;
        public string votvUE4SS;
        public string votvApworld;
        public string votvDownloadUrl;
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

        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(OnSelectDirectoryClick);

        if (selectDownloadDirectoryButton != null)
            selectDownloadDirectoryButton.onClick.AddListener(OnSelectDownloadDirectoryClick);

        // Initialiser les chemins par défaut
        InitializeDefaultPaths();

        StartCoroutine(LoadRemoteConfig());
    }

    void InitializeDefaultPaths()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        if (selectedPathInputField != null && string.IsNullOrEmpty(selectedPathInputField.text))
        {
            selectedPathInputField.text = documentsPath;
            votVPath = documentsPath;
            UnityEngine.Debug.Log("Default VotV path set to: " + documentsPath);
        }

        if (downloadPathInputField != null && string.IsNullOrEmpty(downloadPathInputField.text))
        {
            downloadPathInputField.text = documentsPath;
            downloadPath = documentsPath;
            UnityEngine.Debug.Log("Default download path set to: " + documentsPath);
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

    public void RunSetup()
    {
        if (string.IsNullOrEmpty(votVPath))
        {
            ShowInfo("Please select VotV directory first!");
            return;
        }

        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    }

    public void DownloadVotV()
    {
        if (string.IsNullOrEmpty(downloadPath))
        {
            ShowInfo("Please select download directory first!");
            return;
        }

        ShowConfirmation("Download Voices of the Void v0.9.0j?", "DownloadVotV");
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

            case "DownloadVotV":
                StartCoroutine(DownloadVotVFlow());
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

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installUE4SSToggle != null && installUE4SSToggle.isOn)
        {
            ShowInfo("Installing UE4SS...");
            yield return InstallUE4SS();
        }

        if (installAPModToggle == null || installAPModToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
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
        yield return InstallAPWorld();
    }

    IEnumerator UE4SSOnlyFlow()
    {
        yield return InstallUE4SS();
    }

    IEnumerator APModOnlyFlow()
    {
        yield return InstallAPMod();
    }

    IEnumerator DownloadVotVFlow()
    {
        ShowInfo("Opening download page...");

        string downloadUrl = "https://store.steampowered.com/app/1958810/Voices_of_the_Void/";

        if (remoteConfig != null && !string.IsNullOrEmpty(remoteConfig.votvDownloadUrl))
        {
            downloadUrl = remoteConfig.votvDownloadUrl;
        }

        Application.OpenURL(downloadUrl);

        ShowInfo($"Download link opened in your browser.\n\nSave the file to:\n{downloadPath}\n\nThe file will be renamed to:\n{VOTVDOWNLOAD_FILENAME}");

        yield return null;
    }

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

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            startPath,
            "Select VotV Directory (folder with VotV.exe)"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];

            if (File.Exists(Path.Combine(selectedPath, "VotV.exe")))
            {
                votVPath = selectedPath;
                if (selectedPathInputField != null)
                    selectedPathInputField.text = votVPath;

                UnityEngine.Debug.Log("VotV directory selected: " + votVPath);
                ShowInfo("VotV directory selected successfully!");
            }
            else
            {
                ShowInfo("ERROR: VotV.exe not found in this directory!");
            }
        }
    }

    IEnumerator ShowDownloadFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

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
            ShowInfo($"Download directory selected successfully!\n\nFile will be saved as:\n{VOTVDOWNLOAD_FILENAME}");
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

    string RenameDownloadedFile(string originalPath)
    {
        try
        {
            string directory = Path.GetDirectoryName(originalPath);
            string newPath = Path.Combine(directory, VOTVDOWNLOAD_FILENAME);

            // Supprimer le fichier destination s'il existe
            if (File.Exists(newPath))
            {
                File.Delete(newPath);
                UnityEngine.Debug.Log("Existing file deleted: " + newPath);
            }

            // Renommer le fichier
            if (File.Exists(originalPath))
            {
                File.Move(originalPath, newPath);
                UnityEngine.Debug.Log($"File renamed from {Path.GetFileName(originalPath)} to {VOTVDOWNLOAD_FILENAME}");
                return newPath;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error renaming file: " + e.Message);
        }

        return originalPath;
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

            string rootVersionPath = Path.Combine(votVPath, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created: " + rootVersionPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
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
