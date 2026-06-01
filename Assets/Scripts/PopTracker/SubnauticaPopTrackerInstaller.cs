using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.IO;
using System.IO.Compression;

public class SubnauticaPopTrackerInstaller : MonoBehaviour
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
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

    private string selectedDirectory = "";
    private string poptrackerDownloadUrl = "";
    private string subnauticapoptrackerDownloadUrl = "";
    private SubnauticaPopTrackerConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class SubnauticaPopTrackerConfig
    {
        public string poptrackerDL;
        public string subnauticapoptrackerDL;
    }

    void Start()
    {
        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(SelectDirectory);

        if (installButton != null)
            installButton.onClick.AddListener(InstallSubnauticaPopTracker);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (selectedPathPlaceholder != null)
            selectedPathPlaceholder.gameObject.SetActive(true);

        if (selectedPathInputField != null)
        {
            selectedPathInputField.onValueChanged.AddListener(OnPathInputChanged);
        }

        string defaultDocumentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        SetDirectory(defaultDocumentsPath);

        StartCoroutine(LoadRemoteConfig());
    }

    IEnumerator LoadRemoteConfig()
    {
        Debug.Log("Loading Subnautica PopTracker config from: " + configUrl);

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
            remoteConfig = JsonUtility.FromJson<SubnauticaPopTrackerConfig>(request.downloadHandler.text);
            poptrackerDownloadUrl = remoteConfig.poptrackerDL;
            subnauticapoptrackerDownloadUrl = remoteConfig.subnauticapoptrackerDL;
            Debug.Log("Subnautica PopTracker config loaded successfully.");
            Debug.Log("PopTracker URL: " + poptrackerDownloadUrl);
            Debug.Log("Subnautica PopTracker URL: " + subnauticapoptrackerDownloadUrl);
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

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            documentsPath,
            "Select Subnautica PopTracker Installation Folder"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];
            Debug.Log("DEBUG: Path from FileBrowser: " + selectedPath);
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
            Debug.Log("Selected path text updated to: " + path);
        }

        Debug.Log("Directory selected: " + path);
        UpdateUI();
    }

    void OnPathInputChanged(string newValue)
    {
        if (IsValidPath(newValue))
        {
            selectedDirectory = newValue;
        }
        else
        {
            selectedDirectory = "";
        }
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

    void InstallSubnauticaPopTracker()
    {
        if (string.IsNullOrEmpty(selectedDirectory) || !IsValidPath(selectedDirectory))
        {
            ShowInfo("Please select a valid directory first");
            return;
        }

        if (string.IsNullOrEmpty(subnauticapoptrackerDownloadUrl))
        {
            ShowInfo("Subnautica PopTracker download URL not loaded");
            return;
        }

        StartCoroutine(DownloadAndInstallSubnauticaPopTracker());
    }

    IEnumerator DownloadAndInstallSubnauticaPopTracker()
    {
        while (!configLoaded)
        {
            Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(subnauticapoptrackerDownloadUrl))
        {
            ShowInfo("ERROR: Subnautica PopTracker URL is empty!");
            Debug.LogError("Subnautica PopTracker URL not set!");
            yield break;
        }

        ShowInfo("Checking for Subnautica PopTracker...");
        installButton.interactable = false;

        string tempDownloadPath = Path.Combine(Application.persistentDataPath, "SubnauticaPopTrackerTemp");
        if (!Directory.Exists(tempDownloadPath))
        {
            Directory.CreateDirectory(tempDownloadPath);
        }

        // Déterminer le répertoire cible
        string targetPath = selectedDirectory;
        bool isPacksDirectory = new DirectoryInfo(selectedDirectory).Name.Equals("packs", System.StringComparison.OrdinalIgnoreCase);

        if (isPacksDirectory)
        {
            // L'utilisateur a sélectionné le dossier packs
            targetPath = Directory.GetParent(selectedDirectory).FullName;
            Debug.Log("Detected packs directory. Target path set to: " + targetPath);
        }
        else
        {
            // Chercher un dossier contenant "poptracker" dans le nom
            string popTrackerFolder = FindPopTrackerFolderInDirectory(selectedDirectory);
            if (!string.IsNullOrEmpty(popTrackerFolder))
            {
                targetPath = popTrackerFolder;
                Debug.Log("Found poptracker folder: " + targetPath);
            }
        }

        // Vérifier si PopTracker.exe existe dans le dossier cible
        string popTrackerExePath = Path.Combine(targetPath, "PopTracker.exe");
        bool popTrackerExists = File.Exists(popTrackerExePath);

        Debug.Log("Checking for PopTracker.exe at: " + popTrackerExePath);
        Debug.Log("PopTracker.exe exists: " + popTrackerExists);

        // Si PopTracker n'existe pas, télécharger et installer
        if (!popTrackerExists)
        {
            if (string.IsNullOrEmpty(poptrackerDownloadUrl))
            {
                ShowInfo("ERROR: PopTracker URL is empty!");
                Debug.LogError("PopTracker URL not set!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Downloading PopTracker...");

            string fileName = ExtractFileNameFromUrl(poptrackerDownloadUrl);
            string zipPath = Path.Combine(tempDownloadPath, fileName);

            Debug.Log("Downloading PopTracker from: " + poptrackerDownloadUrl);
            Debug.Log("Saving to: " + zipPath);

            yield return DownloadFile(poptrackerDownloadUrl, zipPath);

            if (!File.Exists(zipPath))
            {
                Debug.LogError("Download failed: file not found at " + zipPath);
                ShowInfo("ERROR: PopTracker download failed!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            Debug.Log("File downloaded successfully: " + zipPath);
            ShowInfo("Extracting PopTracker...");

            string extractPath = Path.Combine(tempDownloadPath, "extracted");
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            Directory.CreateDirectory(extractPath);

            yield return ExtractZipAsync(zipPath, extractPath);

            string popTrackerSourceFolder = FindPopTrackerFolder(extractPath);

            if (string.IsNullOrEmpty(popTrackerSourceFolder))
            {
                Debug.LogError("Could not find 'poptracker' folder in extracted files!");
                ShowInfo("ERROR: Could not find PopTracker folder in archive!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            Debug.Log("Found PopTracker folder at: " + popTrackerSourceFolder);
            ShowInfo("Installing PopTracker...");

            yield return CopyDirectoryRecursive(popTrackerSourceFolder, targetPath);

            Debug.Log("PopTracker installed to: " + targetPath);
            ShowInfo("PopTracker installation complete!");
            Debug.Log("Installation complete in: " + targetPath);
        }
        else
        {
            Debug.Log("PopTracker.exe found. Skipping PopTracker installation.");
            ShowInfo("PopTracker found! Installing Subnautica pack only...");
        }

        // Télécharger et installer le pack Subnautica
        if (string.IsNullOrEmpty(subnauticapoptrackerDownloadUrl))
        {
            Debug.LogWarning("Subnautica PopTracker URL is empty, skipping Subnautica pack installation");
        }
        else
        {
            ShowInfo("Downloading Subnautica PopTracker pack...");

            string subnauticaFileName = "SubnauticaArchipelagoPopTracker.zip";
            string subnauticaZipPath = Path.Combine(tempDownloadPath, subnauticaFileName);

            Debug.Log("Downloading Subnautica PopTracker from: " + subnauticapoptrackerDownloadUrl);
            Debug.Log("Saving to: " + subnauticaZipPath);

            yield return DownloadFile(subnauticapoptrackerDownloadUrl, subnauticaZipPath);

            if (!File.Exists(subnauticaZipPath))
            {
                Debug.LogError("Subnautica download failed: file not found at " + subnauticaZipPath);
                ShowInfo("ERROR: Subnautica PopTracker download failed!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            Debug.Log("Subnautica file downloaded successfully: " + subnauticaZipPath);
            ShowInfo("Installing Subnautica pack...");

            string packsPath = Path.Combine(targetPath, "packs");
            Directory.CreateDirectory(packsPath);

            Debug.Log("Created/verified packs directory at: " + packsPath);

            string packsZipPath = Path.Combine(packsPath, subnauticaFileName);

            try
            {
                File.Copy(subnauticaZipPath, packsZipPath, true);
                Debug.Log("Subnautica pack copied to: " + packsZipPath);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error copying Subnautica pack: " + e.Message);
                ShowInfo("ERROR: Failed to copy Subnautica pack!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Installation complete in:\n" + targetPath);
            Debug.Log("Installation complete in: " + targetPath);
        }

        SafeDeleteDirectory(tempDownloadPath);

        installButton.interactable = true;
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            request.timeout = 300;

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Download error: " + request.error);
                Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                Debug.Log("Download complete! File size: " + new System.IO.FileInfo(savePath).Length + " bytes");
            }
        }
    }

    IEnumerator ExtractZipAsync(string zipPath, string extractPath)
    {
        Debug.Log("Starting ZIP extraction from: " + zipPath + " to: " + extractPath);

        bool extractionComplete = false;
        System.Exception extractionException = null;

        System.Threading.Thread extractThread = new System.Threading.Thread(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
                Debug.Log("ZIP extracted successfully");
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
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (extractionException != null)
        {
            Debug.LogError("ZIP extraction failed: " + extractionException.Message);
        }
    }

    string FindPopTrackerFolder(string rootPath)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(rootPath, "*", System.IO.SearchOption.AllDirectories))
            {
                string folderName = new DirectoryInfo(dir).Name;
                if (folderName.Equals("poptracker", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("Found poptracker folder: " + dir);
                    return dir;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error searching for poptracker folder: " + e.Message);
        }

        Debug.LogWarning("No 'poptracker' folder found in: " + rootPath);
        return "";
    }

    string FindPopTrackerFolderInDirectory(string rootPath)
    {
        try
        {
            // Chercher dans le répertoire racine d'abord (sans récursion)
            foreach (string dir in Directory.GetDirectories(rootPath))
            {
                string folderName = new DirectoryInfo(dir).Name;
                if (folderName.IndexOf("poptracker", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("Found poptracker folder: " + dir);
                    return dir;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error searching for poptracker folder: " + e.Message);
        }

        Debug.Log("No 'poptracker' folder found in: " + rootPath);
        return "";
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
                Debug.Log("Directory copy complete: " + sourceDir + " -> " + targetDir);
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
        {
            yield return new WaitForSeconds(0.1f);
        }

        if (copyException != null)
        {
            Debug.LogError("Directory copy failed: " + copyException.Message);
        }
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

        Debug.Log("Extracted filename from URL: " + fileName);
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

        Debug.Log("UI Updated - ConfigLoaded: " + configLoaded + ", PathValid: " + (!string.IsNullOrEmpty(selectedDirectory) && IsValidPath(selectedDirectory)));
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
