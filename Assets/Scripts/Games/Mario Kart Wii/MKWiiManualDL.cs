using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;

public class MKWiiManualDL : MonoBehaviour
{
    public FileDownloader downloader;
    public FileDownloader.FileData mkwiiApworld;
    public FileDownloader.FileData mkwiiAP;
    public FileDownloader.FileData mkwiiDolphin;

    [Header("PANEL")]
    public GameObject installationPanel;

    [Header("INSTALLATION UI")]
    public Toggle installApworldToggle;
    public Toggle installMKWiiAPToggle;
    public Toggle installDolphinToggle;
    public Toggle launchAfterSetupToggle;

    public Button installButton;
    public Button cancelButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmPanel;
    public TextMeshProUGUI confirmText;
    public Button confirmOkButton;
    public Button confirmNoButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";

    private string mkwiiApworldDownloadUrl = "";
    private string mkwiiAPDownloadUrl = "";
    private string mkwiiDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool mkwiiapInstalled { get; private set; } = false;
    public bool mkwiiPackageInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;

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
        public string mkwiiApworld;
        public string mkwiiAP;
        public string mkwiiDolphin;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! MK Wii Archipelago is ready to use.";

    void Start()
    {
        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);

        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (installationPanel != null) installationPanel.SetActive(true);

        if (installApworldToggle != null) installApworldToggle.isOn = true;
        if (installMKWiiAPToggle != null) installMKWiiAPToggle.isOn = true;
        if (installDolphinToggle != null) installDolphinToggle.isOn = false;
        if (launchAfterSetupToggle != null) launchAfterSetupToggle.isOn = false;

        if (cancelButton != null) cancelButton.interactable = false;

        if (infoText != null) infoDefaultFontSize = infoText.fontSize;

        StartCoroutine(LoadRemoteConfig());
    }

    IEnumerator LoadRemoteConfig()
    {
        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(configUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed (optional): " + request.error);
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
                mkwiiApworldDownloadUrl = remoteConfig.mkwiiApworld;
                mkwiiAPDownloadUrl = remoteConfig.mkwiiAP;
                mkwiiDolphinDownloadUrl = remoteConfig.mkwiiDolphin;
                UnityEngine.Debug.Log("MK Wii config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (optional): " + e.Message);
        }
    }

    void OnInstallButtonClicked()
    {
        if (confirmPanel != null)
        {
            if (confirmText != null) confirmText.text = "Do you want to start the setup now?";
            confirmPanel.SetActive(true);
        }
        else ExecuteInstallation();
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

        bool installApworldSelected = installApworldToggle != null && installApworldToggle.isOn;
        bool installMKWiiAPSelected = installMKWiiAPToggle != null && installMKWiiAPToggle.isOn;
        bool installDolphinSelected = installDolphinToggle != null && installDolphinToggle.isOn;
        bool launchAfterSetup = launchAfterSetupToggle != null && launchAfterSetupToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "MKWiiAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        mkwiiapInstalled = false;
        mkwiiPackageInstalled = false;
        dolphinInstalled = false;

        if (installApworldSelected && !installationCancelled)
        {
            ShowInfo("Installing MK Wii APWorld...");
            yield return InstallApworld(tempDownloadPath);
        }

        if (installMKWiiAPSelected && !installationCancelled)
        {
            ShowInfo("Installing MK Wii AP package...");
            yield return InstallMKWiiPackage(documentsPath, tempDownloadPath);
        }

        if (installDolphinSelected && !installationCancelled)
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

        if (launchAfterSetup && mkwiiPackageInstalled)
        {
            ShowInfo("Launching MK Wii Client...");
            yield return LaunchMKWiiClient(documentsPath);
        }

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallApworld(string tempPath)
    {
        mkwiiapInstalled = false;

        while (!configLoaded) yield return null;

        if (string.IsNullOrEmpty(mkwiiApworldDownloadUrl))
        {
            ShowInfo("ERROR: mkwii.apworld URL not loaded!");
            yield break;
        }

        string apworldFileName = mkwiiApworld.fileName;
        if (string.IsNullOrEmpty(apworldFileName))
        {
            apworldFileName = mkwiiApworldDownloadUrl.Substring(mkwiiApworldDownloadUrl.LastIndexOf('/') + 1);
            if (apworldFileName.Contains("?")) apworldFileName = apworldFileName.Substring(0, apworldFileName.IndexOf("?"));
        }

        string localApworldPath = Path.Combine(Application.persistentDataPath, apworldFileName);

        yield return DownloadFile(mkwiiApworldDownloadUrl, localApworldPath);

        if (!File.Exists(localApworldPath))
        {
            UnityEngine.Debug.LogError("APWorld download failed: file not found at " + localApworldPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string[] targetPaths = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", apworldFileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", apworldFileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", apworldFileName),
        };

        string apworldTarget = "";
        foreach (string path in targetPaths)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                apworldTarget = path;
                UnityEngine.Debug.Log("Using APWorld target path: " + apworldTarget);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(apworldTarget))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            UnityEngine.Debug.LogError("No valid target directory found for apworld!");
            yield break;
        }

        try
        {
            if (File.Exists(apworldTarget)) File.Delete(apworldTarget);
            File.Copy(localApworldPath, apworldTarget, true);
            UnityEngine.Debug.Log("APWorld file copied to: " + apworldTarget);
            ShowInfo("APWorld installed successfully!");
            mkwiiapInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            mkwiiapInstalled = false;
            yield break;
        }

        try { if (File.Exists(localApworldPath)) File.Delete(localApworldPath); } catch { }

        yield return null;
    }

    IEnumerator InstallMKWiiPackage(string documentsPath, string tempPath)
    {
        mkwiiPackageInstalled = false;

        while (!configLoaded) yield return null;

        if (string.IsNullOrEmpty(mkwiiAPDownloadUrl))
        {
            ShowInfo("ERROR: MK Wii AP package URL not loaded!");
            yield break;
        }

        string zipFileName = mkwiiAP.fileName;
        if (string.IsNullOrEmpty(zipFileName))
        {
            zipFileName = mkwiiAPDownloadUrl.Substring(mkwiiAPDownloadUrl.LastIndexOf('/') + 1);
            if (zipFileName.Contains("?")) zipFileName = zipFileName.Substring(0, zipFileName.IndexOf("?"));
        }

        string localZipPath = Path.Combine(tempPath, zipFileName);
        string extractPath = Path.Combine(tempPath, "extract");

        yield return DownloadFile(mkwiiAPDownloadUrl, localZipPath);

        if (!File.Exists(localZipPath))
        {
            UnityEngine.Debug.LogError("AP package download failed: " + localZipPath);
            ShowInfo("ERROR: AP package download failed!");
            yield break;
        }

        var extractTask = StartZipExtraction(localZipPath, extractPath);
        while (!extractTask.Complete) yield return null;
        if (extractTask.Exception != null)
        {
            UnityEngine.Debug.LogError("Extraction error: " + extractTask.Exception.Message);
            ShowInfo("ERROR: Failed to extract AP package: " + extractTask.Exception.Message);
            yield break;
        }

        string mkwiiDocsFolder = Path.Combine(documentsPath, "MK Wii Archipelago");
        try
        {
            if (!Directory.Exists(mkwiiDocsFolder)) Directory.CreateDirectory(mkwiiDocsFolder);
            PerformDirectoryCopyWithSkip(extractPath, mkwiiDocsFolder, new string[] { "mkwii.apworld" });
            UnityEngine.Debug.Log("AP package files copied to: " + mkwiiDocsFolder);
            ShowInfo("MK Wii AP package installed to Documents!");
            mkwiiPackageInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying AP package files: " + e.Message);
            ShowInfo("ERROR: Failed to install AP package: " + e.Message);
            yield break;
        }

        try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }
        SafeDeleteDirectory(extractPath);

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(mkwiiDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        yield return DownloadFile(mkwiiDolphinDownloadUrl, sevenZPath);

        if (!File.Exists(sevenZPath) || installationCancelled)
        {
            ShowInfo("ERROR: Dolphin download failed!");
            yield break;
        }

        try
        {
            string dolphinDestPath = Path.Combine(dolphinDocumentsPath, "dolphin.7z");
            if (File.Exists(dolphinDestPath)) File.Delete(dolphinDestPath);
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

        if (File.Exists(sevenZPath))
        {
            try { File.Delete(sevenZPath); } catch { }
        }

        yield return null;
    }

    IEnumerator LaunchMKWiiClient(string documentsPath)
    {
        string clientPath = Path.Combine(documentsPath, "MK Wii Archipelago", "mkwii client.exe");

        if (!File.Exists(clientPath))
        {
            UnityEngine.Debug.LogError("MK Wii client not found at: " + clientPath);
            ShowInfo("ERROR: Could not find MK Wii client executable!");
            yield break;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = clientPath,
                WorkingDirectory = Path.GetDirectoryName(clientPath),
                UseShellExecute = true,
                CreateNoWindow = false
            };

            Process process = Process.Start(psi);
            launchedProcesses.Add(process);
            UnityEngine.Debug.Log("MK Wii client launched: " + clientPath);
            ShowInfo("MK Wii client launched successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error launching MK Wii client: " + e.Message);
            ShowInfo("ERROR: Failed to launch MK Wii client: " + e.Message);
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
            else UnityEngine.Debug.Log("Download complete: " + savePath);
        }
    }

    private ThreadTask StartZipExtraction(string zipPath, string extractPath)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try
            {
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                ZipFile.ExtractToDirectory(zipPath, extractPath);
            }
            catch (System.Exception e) { UnityEngine.Debug.LogError("Error extracting ZIP: " + e.Message); task.Exception = e; }
            finally { task.Complete = true; }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    void PerformDirectoryCopyWithSkip(string sourceDir, string targetDir, string[] skipFileNamesLowercase)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            bool skip = false;
            foreach (var s in skipFileNamesLowercase)
            {
                if (fileName.ToLower() == s.ToLower()) { skip = true; break; }
            }
            if (skip) continue;
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            PerformDirectoryCopyWithSkip(dir, targetSubDir, skipFileNamesLowercase);
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
