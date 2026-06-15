using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.IO;
using System.IO.Compression;

public class ULTRAKILLPopTrackerInstaller : MonoBehaviour
{
    [Header("UI ELEMENTS")]
    public Button selectDirectoryButton;
    public TMP_InputField selectedPathInputField;
    public TextMeshProUGUI selectedPathPlaceholder;
    public Button installButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";

    private string selectedDirectory = "";
    private string poptrackerDownloadUrl = "";
    private string gamepackDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class RemoteConfig
    {
        public string poptrackerDL;
        public string ultrakillPopTracker;
    }

    void Start()
    {
        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(SelectDirectory);

        if (installButton != null)
            installButton.onClick.AddListener(InstallPopTracker);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (selectedPathPlaceholder != null)
            selectedPathPlaceholder.gameObject.SetActive(true);

        if (selectedPathInputField != null)
            selectedPathInputField.onValueChanged.AddListener(OnPathInputChanged);

        string defaultDocumentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        SetDirectory(defaultDocumentsPath);

        StartCoroutine(LoadRemoteConfig());
    }

    IEnumerator LoadRemoteConfig()
    {
        Debug.Log("Loading PopTracker config from: " + configUrl);

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(configUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<RemoteConfig>(request.downloadHandler.text);
            poptrackerDownloadUrl = remoteConfig.poptrackerDL;
            gamepackDownloadUrl = remoteConfig.ultrakillPopTracker;
            Debug.Log("PopTracker config loaded successfully.");
            Debug.Log("PopTracker URL: " + poptrackerDownloadUrl);
            Debug.Log("ULTRAKILL Pack URL: " + gamepackDownloadUrl);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
        UpdateUI();
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
            Debug.Log("DarkSkin assigned!");
        }
        else
        {
            Debug.LogWarning("DarkSkin not assigned!");
        }

        // Utiliser le chemin actuellement sélectionné si valide, sinon Documents
        string startPath = selectedDirectory;
        if (string.IsNullOrEmpty(startPath) || !IsValidPath(startPath))
        {
            startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        }

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            startPath,
            "Select PopTracker Installation Folder"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];
            Debug.Log("Path from FileBrowser: " + selectedPath);
            SetDirectory(selectedPath);
        }
    }

    void SetDirectory(string path)
    {
        if (!IsValidPath(path))
        {
            ShowInfo("Please select a valid path that exists");
            return;
        }

        selectedDirectory = path;

        if (selectedPathPlaceholder != null)
            selectedPathPlaceholder.gameObject.SetActive(false);

        if (selectedPathInputField != null)
        {
            selectedPathInputField.gameObject.SetActive(true);
            selectedPathInputField.text = path;
        }

        Debug.Log("Directory selected: " + path);
        UpdateUI();
    }

    void OnPathInputChanged(string newValue)
    {
        if (IsValidPath(newValue))
            selectedDirectory = newValue;
        else
            selectedDirectory = "";
        UpdateUI();
    }

    bool IsValidPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        char[] invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0)
            return false;

        if (!Directory.Exists(path))
        {
            Debug.LogWarning("Directory does not exist: " + path);
            return false;
        }

        return true;
    }

    void InstallPopTracker()
    {
        if (string.IsNullOrEmpty(selectedDirectory) || !IsValidPath(selectedDirectory))
        {
            ShowInfo("Please select a valid directory first");
            return;
        }

        if (string.IsNullOrEmpty(gamepackDownloadUrl))
        {
            ShowInfo("Game Pack download URL not loaded");
            return;
        }

        StartCoroutine(DownloadAndInstall());
    }

    IEnumerator DownloadAndInstall()
    {
        while (!configLoaded)
        {
            Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        ShowInfo("Analyzing directory...");
        installButton.interactable = false;

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "PopTrackerTemp");
        if (!Directory.Exists(tempDownloadPath))
        {
            Directory.CreateDirectory(tempDownloadPath);
        }

        // STEP 1: Determine PopTracker path and if we need to download PopTracker
        string popTrackerPath = "";
        bool isUserInPacksFolder = new DirectoryInfo(selectedDirectory).Name.Equals("packs", System.StringComparison.OrdinalIgnoreCase);

        if (isUserInPacksFolder)
        {
            // User selected the packs folder directly
            popTrackerPath = Directory.GetParent(selectedDirectory).FullName;
            Debug.Log("User selected packs folder. PopTracker path: " + popTrackerPath);
        }
        else
        {
            popTrackerPath = DeterminPopTrackerPath(selectedDirectory);
        }

        Debug.Log("PopTracker path: " + popTrackerPath);

        // STEP 2: Validate PopTracker (only if user didn't select packs folder)
        bool needsPopTrackerDownload = false;

        if (!isUserInPacksFolder)
        {
            needsPopTrackerDownload = !IsPopTrackerValid(popTrackerPath);
            if (needsPopTrackerDownload)
            {
                ShowInfo("PopTracker incomplete. Downloading...");
            }
            else
            {
                ShowInfo("PopTracker valid!");
            }
        }
        else
        {
            ShowInfo("Installing pack only...");
        }

        // STEP 3: Download PopTracker if needed
        if (needsPopTrackerDownload)
        {
            if (string.IsNullOrEmpty(poptrackerDownloadUrl))
            {
                ShowInfo("ERROR: PopTracker URL is empty!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Downloading PopTracker...");
            string fileName = ExtractFileNameFromUrl(poptrackerDownloadUrl);
            string zipPath = Path.Combine(tempDownloadPath, fileName);

            yield return DownloadFile(poptrackerDownloadUrl, zipPath);

            if (!File.Exists(zipPath))
            {
                ShowInfo("ERROR: PopTracker download failed!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Extracting PopTracker...");
            string extractPath = Path.Combine(tempDownloadPath, "extracted");
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);

            yield return ExtractZipAsync(zipPath, extractPath);

            string popTrackerSourceFolder = FindPopTrackerFolder(extractPath);
            if (string.IsNullOrEmpty(popTrackerSourceFolder))
            {
                ShowInfo("ERROR: PopTracker folder not found in archive!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Installing PopTracker...");
            if (!Directory.Exists(popTrackerPath))
                Directory.CreateDirectory(popTrackerPath);

            yield return CopyDirectoryRecursive(popTrackerSourceFolder, popTrackerPath);
        }

        // STEP 4: Download and install game pack
        if (string.IsNullOrEmpty(gamepackDownloadUrl))
        {
            ShowInfo("ERROR: Game Pack URL not loaded");
            installButton.interactable = true;
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        ShowInfo("Downloading game pack...");
        string gamepackZipPath = Path.Combine(tempDownloadPath, "GamePack.zip");

        yield return DownloadFile(gamepackDownloadUrl, gamepackZipPath);

        if (!File.Exists(gamepackZipPath))
        {
            ShowInfo("ERROR: Game pack download failed!");
            installButton.interactable = true;
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        ShowInfo("Installing game pack...");
        string packsPath = Path.Combine(popTrackerPath, "packs");
        if (!Directory.Exists(packsPath))
            Directory.CreateDirectory(packsPath);

        // COPIER LE ZIP DIRECTEMENT DANS packs/ AVEC LE BON NOM
        string packDestZipPath = Path.Combine(packsPath, "ULTRAKILLAPPoptracker.zip");
        File.Copy(gamepackZipPath, packDestZipPath, true);
        Debug.Log("Pack copied to: " + packDestZipPath);

        ShowInfo("Installation complete in:\n" + popTrackerPath);
        SafeDeleteDirectory(tempDownloadPath);
        installButton.interactable = true;
    }

    string DeterminPopTrackerPath(string selectedPath)
    {
        if (new DirectoryInfo(selectedPath).Name.Equals("poptracker", System.StringComparison.OrdinalIgnoreCase))
            return selectedPath;

        string popTrackerFolder = FindPopTrackerFolderInDirectory(selectedPath);
        if (!string.IsNullOrEmpty(popTrackerFolder))
            return popTrackerFolder;

        return Path.Combine(selectedPath, "poptracker");
    }

    bool IsPopTrackerValid(string popTrackerPath)
    {
        if (!Directory.Exists(popTrackerPath))
            return false;

        string popTrackerExePath = Path.Combine(popTrackerPath, "PopTracker.exe");
        if (!File.Exists(popTrackerExePath))
            return false;

        string packsPath = Path.Combine(popTrackerPath, "packs");
        if (!Directory.Exists(packsPath))
            return false;

        return true;
    }

    string FindPopTrackerFolder(string rootPath)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(rootPath, "*", System.IO.SearchOption.AllDirectories))
            {
                string folderName = new DirectoryInfo(dir).Name;
                if (folderName.Equals("poptracker", System.StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error searching for poptracker folder: " + e.Message);
        }

        return "";
    }

    string FindPopTrackerFolderInDirectory(string rootPath)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(rootPath))
            {
                string folderName = new DirectoryInfo(dir).Name;
                if (folderName.IndexOf("poptracker", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return dir;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error searching for poptracker folder: " + e.Message);
        }

        return "";
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            request.timeout = 300;

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                Debug.LogError("Download error: " + request.error);
            else
                Debug.Log("Download complete!");
        }
    }

    IEnumerator ExtractZipAsync(string zipPath, string extractPath)
    {
        bool extractionComplete = false;
        System.Exception extractionException = null;

        System.Threading.Thread extractThread = new System.Threading.Thread(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error extracting ZIP: " + e.Message);
                extractionException = e;
            }
            finally
            {
                extractionComplete = true;
            }
        });

        extractThread.Start();

        while (!extractionComplete)
            yield return new WaitForSeconds(0.1f);

        if (extractionException != null)
            Debug.LogError("ZIP extraction failed: " + extractionException.Message);
    }

    IEnumerator CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        yield return null;

        bool copyComplete = false;
        System.Exception copyException = null;

        System.Threading.Thread copyThread = new System.Threading.Thread(() =>
        {
            try
            {
                PerformDirectoryCopy(sourceDir, targetDir);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error copying directory: " + e.Message);
                copyException = e;
            }
            finally
            {
                copyComplete = true;
            }
        });

        copyThread.Start();

        while (!copyComplete)
            yield return new WaitForSeconds(0.1f);

        if (copyException != null)
            Debug.LogError("Directory copy failed: " + copyException.Message);
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

    string ExtractFileNameFromUrl(string url)
    {
        string fileName = url.Substring(url.LastIndexOf('/') + 1);
        if (fileName.Contains("?"))
            fileName = fileName.Substring(0, fileName.IndexOf("?"));
        return fileName;
    }

    void ShowInfo(string message)
    {
        if (infoText != null)
            infoText.text = message;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    void UpdateUI()
    {
        if (installButton != null)
            installButton.interactable = configLoaded && !string.IsNullOrEmpty(selectedDirectory) && IsValidPath(selectedDirectory);
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
