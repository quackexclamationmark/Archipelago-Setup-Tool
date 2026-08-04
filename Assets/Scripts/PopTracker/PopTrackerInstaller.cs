using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;

public class PopTrackerInstaller : MonoBehaviour
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

    [Header("DROPDOWN REFERENCE")]
    public ManualDropdown linuxDropdown;

    [Header("PLATFORM SELECTION REFERENCE")]
    public PlatformSelection platformSelection;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";

    [Tooltip("Name of the field in the remote config JSON that contains the game pack URL to download.")]
    public string remoteConfigField = "";

    [Tooltip("File name to use when copying the downloaded pack into the PopTracker 'packs' folder.")]
    public string packFileName = "";

    private string selectedDirectory = "";
    private string poptrackerWinDownloadUrl = "";
    private string gamepackDownloadUrl = "";
    private string rawConfigJson = "";
    private bool configLoaded = false;

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

        if (platformSelection == null)
            platformSelection = FindFirstObjectByType<PlatformSelection>();

        if (linuxDropdown == null)
            linuxDropdown = FindFirstObjectByType<ManualDropdown>();

        string defaultDocumentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        SetDirectory(defaultDocumentsPath);

        StartCoroutine(LoadRemoteConfig());
    }

    void OnValidate()
    {
        if (configLoaded && !string.IsNullOrEmpty(rawConfigJson))
        {
            UpdateGamepackUrlFromField();
            UpdateUI();
        }
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
            rawConfigJson = request.downloadHandler.text;
            poptrackerWinDownloadUrl = GetJsonStringField(rawConfigJson, "poptrackerWinDL");
            UpdateGamepackUrlFromField();

            Debug.Log("PopTracker config loaded successfully.");
            Debug.Log("PopTracker Win URL: " + poptrackerWinDownloadUrl);
            Debug.Log($"Game pack field '{remoteConfigField}' resolved to: {gamepackDownloadUrl}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
        UpdateUI();
    }

    void UpdateGamepackUrlFromField()
    {
        if (string.IsNullOrEmpty(rawConfigJson) || string.IsNullOrEmpty(remoteConfigField))
        {
            gamepackDownloadUrl = "";
            return;
        }

        string resolved = GetJsonStringField(rawConfigJson, remoteConfigField);
        gamepackDownloadUrl = resolved ?? "";
    }

    string GetJsonStringField(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return "";

        string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"";
        Match m = Regex.Match(json, pattern);
        if (m.Success && m.Groups.Count > 1)
            return m.Groups[1].Value;

        return "";
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
            ShowInfo($"Game Pack download URL not loaded for field '{remoteConfigField}'");
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

        string popTrackerPath = "";
        bool isUserInPacksFolder = new DirectoryInfo(selectedDirectory).Name.Equals("packs", StringComparison.OrdinalIgnoreCase);

        if (isUserInPacksFolder)
        {
            popTrackerPath = Directory.GetParent(selectedDirectory).FullName;
            Debug.Log("User selected packs folder. PopTracker path: " + popTrackerPath);
        }
        else
        {
            popTrackerPath = DeterminPopTrackerPath(selectedDirectory);
        }

        Debug.Log("PopTracker path: " + popTrackerPath);

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

        if (needsPopTrackerDownload)
        {
            string popTrackerUrlToUse = "";

            if (platformSelection != null && platformSelection.isWindows)
            {
                popTrackerUrlToUse = poptrackerWinDownloadUrl;
                Debug.Log("Using Windows PopTracker URL");
            }
            else if (platformSelection != null && platformSelection.isLinux)
            {
                Debug.Log("Using Linux PopTracker URL");
                popTrackerUrlToUse = GetLinuxPopTrackerUrl();
            }

            if (string.IsNullOrEmpty(popTrackerUrlToUse))
            {
                ShowInfo("ERROR: PopTracker URL is empty!");
                installButton.interactable = true;
                SafeDeleteDirectory(tempDownloadPath);
                yield break;
            }

            ShowInfo("Downloading PopTracker...");
            string fileName = ExtractFileNameFromUrl(popTrackerUrlToUse);
            string downloadPath = Path.Combine(tempDownloadPath, fileName);

            yield return DownloadFile(popTrackerUrlToUse, downloadPath);

            if (!File.Exists(downloadPath))
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

            if (fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            {
                yield return ExtractTarXzAsync(downloadPath, extractPath);
            }
            else if (fileName.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            {
                yield return CopyAppImageAsync(downloadPath, popTrackerPath);
                SafeDeleteDirectory(tempDownloadPath);
                ShowInfo("Installing game pack...");
                yield return InstallGamePackOnly(popTrackerPath, tempDownloadPath);
                installButton.interactable = true;
                yield break;
            }
            else
            {
                yield return ExtractZipAsync(downloadPath, extractPath);
            }

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

        if (string.IsNullOrEmpty(gamepackDownloadUrl))
        {
            ShowInfo("ERROR: Game Pack URL not loaded");
            installButton.interactable = true;
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        yield return InstallGamePackOnly(popTrackerPath, tempDownloadPath);
        installButton.interactable = true;
    }

    IEnumerator InstallGamePackOnly(string popTrackerPath, string tempDownloadPath)
    {
        ShowInfo("Downloading game pack...");
        string gamepackZipPath = Path.Combine(tempDownloadPath, "GamePack.zip");

        yield return DownloadFile(gamepackDownloadUrl, gamepackZipPath);

        if (!File.Exists(gamepackZipPath))
        {
            ShowInfo("ERROR: Game pack download failed!");
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        ShowInfo("Installing game pack...");
        string packsPath = Path.Combine(popTrackerPath, "packs");
        if (!Directory.Exists(packsPath))
            Directory.CreateDirectory(packsPath);

        string safeFileName = SanitizeFileName(packFileName);
        if (string.IsNullOrEmpty(safeFileName))
            safeFileName = "PopTrackerGamePack.zip";

        if (!safeFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            safeFileName += ".zip";

        string packDestZipPath = Path.Combine(packsPath, safeFileName);
        File.Copy(gamepackZipPath, packDestZipPath, true);
        Debug.Log("Pack copied to: " + packDestZipPath);

        ShowInfo("Installation complete in:\n" + popTrackerPath);
        SafeDeleteDirectory(tempDownloadPath);
    }

    string GetLinuxPopTrackerUrl()
    {
        if (linuxDropdown == null)
        {
            Debug.LogWarning("Linux dropdown not assigned!");
            return "";
        }

        if (linuxDropdown.Option1Selected)
        {
            return GetJsonStringField(rawConfigJson, "poptrackerLinuxUbuntu2204");
        }
        else if (linuxDropdown.Option2Selected)
        {
            return GetJsonStringField(rawConfigJson, "poptrackerLinuxAppImage");
        }
        else if (linuxDropdown.Option3Selected)
        {
            return GetJsonStringField(rawConfigJson, "poptrackerLinuxAarch64");
        }

        return "";
    }

    string DeterminPopTrackerPath(string selectedPath)
    {
        if (new DirectoryInfo(selectedPath).Name.Equals("poptracker", StringComparison.OrdinalIgnoreCase))
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
                if (folderName.Equals("poptracker", StringComparison.OrdinalIgnoreCase))
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
                if (folderName.IndexOf("poptracker", StringComparison.OrdinalIgnoreCase) >= 0)
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

    IEnumerator ExtractTarXzAsync(string tarXzPath, string extractPath)
    {
        bool extractionComplete = false;
        System.Exception extractionException = null;

        System.Threading.Thread extractThread = new System.Threading.Thread(() =>
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = "tar",
                    Arguments = $"-xf \"{tarXzPath}\" -C \"{extractPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new System.Exception($"tar extraction failed with exit code {process.ExitCode}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error extracting tar.xz: " + e.Message);
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
            Debug.LogError("tar.xz extraction failed: " + extractionException.Message);
    }

    IEnumerator CopyAppImageAsync(string appImagePath, string popTrackerPath)
    {
        bool copyComplete = false;
        System.Exception copyException = null;

        System.Threading.Thread copyThread = new System.Threading.Thread(() =>
        {
            try
            {
                if (!Directory.Exists(popTrackerPath))
                    Directory.CreateDirectory(popTrackerPath);

                string destPath = Path.Combine(popTrackerPath, Path.GetFileName(appImagePath));
                File.Copy(appImagePath, destPath, true);

                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{destPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(psi).WaitForExit();
                }
                catch { }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error copying AppImage: " + e.Message);
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
            Debug.LogError("AppImage copy failed: " + copyException.Message);
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

    string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c.ToString(), "");
        return name.Trim();
    }
}