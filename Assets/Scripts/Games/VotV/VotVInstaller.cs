using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class VotVInstaller : MonoBehaviour
{
    [Header("INSTALLER CONFIG")]
    public FileDownloader downloader;

    [Header("DIRECTORY SELECTION")]
    public Button directoryButton;
    public TMP_InputField directoryInputField;
    public TextMeshProUGUI directoryPlaceholder;
    public UISkin darkSkin;

    [Header("INSTALL BUTTON")]
    public Button installButton;

    [Header("CONFIRMATION PANEL - DL")]
    public GameObject dlConfirmationPanel;
    public TextMeshProUGUI dlConfirmationMessage;
    public Button dlConfirmButton;
    public Button dlCancelButton;

    [Header("INFO PANEL - DL")]
    public GameObject dlInfoPanel;
    public TextMeshProUGUI dlInfoText;
    public Button dlInfoOkButton;

    private string installPath;
    private bool pathSelected = false;
    private string pendingAction;
    private VotVConfig remoteConfig;
    private bool configLoaded = false;
    private string votvDownloadUrl;
    private string votvFileName; // Nouvelle variable pour le nom du fichier

    [System.Serializable]
    public class VotVConfig
    {
        public string votvDL;
        public string votvFileName;
    }

    void Start()
    {
        string defaultPath = GetDefaultPath();
        installPath = defaultPath;
        pathSelected = true;

        StartCoroutine(LoadRemoteConfig());

        if (dlInfoPanel != null)
            dlInfoPanel.SetActive(false);

        if (dlInfoOkButton != null)
            dlInfoOkButton.onClick.AddListener(CloseDLInfoPanel);

        if (dlConfirmationPanel != null)
            dlConfirmationPanel.SetActive(false);

        if (dlConfirmButton != null)
            dlConfirmButton.onClick.AddListener(OnDLConfirm);

        if (dlCancelButton != null)
            dlCancelButton.onClick.AddListener(OnDLCancel);

        if (directoryButton != null)
            directoryButton.onClick.AddListener(SelectDirectory);

        if (installButton != null)
            installButton.onClick.AddListener(RunInstall);

        if (directoryInputField != null)
        {
            directoryInputField.onEndEdit.AddListener(OnDirectoryInputChanged);
            directoryInputField.text = defaultPath;
            directoryInputField.gameObject.SetActive(true);
        }

        if (directoryPlaceholder != null)
            directoryPlaceholder.gameObject.SetActive(false);

        UnityEngine.Debug.Log("Default path: " + defaultPath);
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
            remoteConfig = JsonUtility.FromJson<VotVConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyVotVConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void ApplyVotVConfig()
    {
        if (remoteConfig == null)
            return;

        if (!string.IsNullOrEmpty(remoteConfig.votvDL))
        {
            votvDownloadUrl = remoteConfig.votvDL;
            UnityEngine.Debug.Log("VotV Download URL from config: " + votvDownloadUrl);
        }

        if (!string.IsNullOrEmpty(remoteConfig.votvFileName))
        {
            votvFileName = remoteConfig.votvFileName + ".7z"; // Ajoute l'extension .7z
            UnityEngine.Debug.Log("VotV File Name from config: " + votvFileName);
        }
        else
        {
            // Fallback si votvFileName n'est pas dans le config
            votvFileName = "Voices Of The Void v0.9.0n.7z";
        }
    }

    string GetDefaultPath()
    {
        return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
    }

    void SelectDirectory()
    {
        StartCoroutine(ShowFileBrowser());
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
        {
            FileBrowser.Skin = darkSkin;
            UnityEngine.Debug.Log("DarkSkin assigned!");
        }

        string startPath = installPath;
        if (directoryInputField != null && !string.IsNullOrEmpty(directoryInputField.text))
        {
            string inputPath = directoryInputField.text;
            if (Directory.Exists(inputPath))
                startPath = inputPath;
        }

        if (string.IsNullOrEmpty(startPath) || !Directory.Exists(startPath))
        {
            startPath = GetDefaultPath();
        }

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            startPath,
            "Select Installation Directory"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];
            SetDirectory(selectedPath);
        }
    }

    void SetDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ShowDLInfo("Directory path is empty!");
            return;
        }

        if (!Directory.Exists(path))
        {
            ShowDLInfo("Invalid directory path!");
            return;
        }

        installPath = path;
        pathSelected = true;

        if (directoryInputField != null)
        {
            directoryInputField.gameObject.SetActive(true);
            directoryInputField.text = path;
        }

        if (directoryPlaceholder != null)
            directoryPlaceholder.gameObject.SetActive(false);

        UnityEngine.Debug.Log("Directory selected: " + path);
    }

    void OnDirectoryInputChanged(string newDirectory)
    {
        if (string.IsNullOrEmpty(newDirectory))
        {
            pathSelected = false;
            UnityEngine.Debug.Log("Directory input cleared - path invalid");
            return;
        }

        if (!Directory.Exists(newDirectory))
        {
            pathSelected = false;
            UnityEngine.Debug.Log("Directory does not exist: " + newDirectory);
            return;
        }

        installPath = newDirectory;
        pathSelected = true;
        UnityEngine.Debug.Log("Install path updated from input field: " + installPath);
    }

    public void RunInstall()
    {
        if (!pathSelected || string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
        {
            ShowDLInfo("Please select a valid directory first!");
            return;
        }

        ShowDLConfirmation("Are you sure you want to install VotV?", "Install");
    }

    private void ShowDLConfirmation(string message, string action)
    {
        pendingAction = action;
        dlConfirmationMessage.text = message;
        dlConfirmationPanel.SetActive(true);
    }

    private void OnDLConfirm()
    {
        dlConfirmationPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingAction))
            return;

        switch (pendingAction)
        {
            case "Install":
                ExecuteInstall();
                break;
        }
    }

    private void OnDLCancel()
    {
        dlConfirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteInstall()
    {
        if (!pathSelected || string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
        {
            ShowDLInfo("Please select a valid directory first!");
            pathSelected = false;
            return;
        }

        if (directoryInputField != null && string.IsNullOrEmpty(directoryInputField.text))
        {
            ShowDLInfo("Please select a valid directory first!");
            pathSelected = false;
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(votvDownloadUrl))
        {
            ShowDLInfo("ERROR: VotV download URL not configured!");
            yield break;
        }

        ShowDLInfo("Downloading VotV...");
        yield return new WaitForSeconds(0.5f);

        yield return DownloadVotV();

        ShowDLInfo("Installation complete!");
        yield return new WaitForSeconds(1f);

        ResetState();
    }

    IEnumerator DownloadVotV()
    {
        string downloadPath = Path.Combine(installPath, votvFileName);

        UnityEngine.Debug.Log("Downloading VotV from: " + votvDownloadUrl);
        UnityEngine.Debug.Log("Saving to: " + downloadPath);

        yield return DownloadFile(votvDownloadUrl, downloadPath);

        if (!File.Exists(downloadPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + downloadPath);
            ShowDLInfo("ERROR: VotV download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + downloadPath);
        ShowDLInfo("VotV installed successfully at:\n" + installPath);
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

    void ShowDLInfo(string message)
    {
        if (dlInfoPanel == null || dlInfoText == null)
            return;

        dlInfoText.text = message;
        dlInfoPanel.SetActive(true);
    }

    void CloseDLInfoPanel()
    {
        if (dlInfoPanel != null)
            dlInfoPanel.SetActive(false);
    }

    public void ResetState()
    {
        pathSelected = false;
        pendingAction = "";
        installPath = GetDefaultPath();

        if (directoryInputField != null)
        {
            directoryInputField.text = installPath;
            directoryInputField.gameObject.SetActive(true);
        }

        if (directoryPlaceholder != null)
            directoryPlaceholder.gameObject.SetActive(false);

        if (dlConfirmationPanel != null)
            dlConfirmationPanel.SetActive(false);

        if (dlInfoPanel != null)
            dlInfoPanel.SetActive(false);

        UnityEngine.Debug.Log("VotVInstaller state reset");
    }
}