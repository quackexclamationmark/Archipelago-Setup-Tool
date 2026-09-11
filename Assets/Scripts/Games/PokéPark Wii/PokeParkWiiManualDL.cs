using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;

public class PokeParkWiiManualDL : MonoBehaviour
{
    public FileDownloader downloader;
    public FileDownloader.FileData pokeparkwiiApworld;
    public FileDownloader.FileData pokeparkwiiAP;
    public FileDownloader.FileData pokeparkwiiDolphin;

    [Header("PANEL")]
    public GameObject installationPanel;

    [Header("INSTALLATION UI")]
    public Toggle installApworldToggle;
    public Toggle installPokeParkAPToggle;
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

    private string pokeparkwiiApworldDownloadUrl = "";
    private string pokeparkwiiAPDownloadUrl = "";
    private string pokeparkwiiDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    public bool pokeparkwiiApworldInstalled { get; private set; } = false;
    public bool pokeparkPackageInstalled { get; private set; } = false;
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
        public string pokeparkwiiApworld;
        public string pokeparkwiiAP;
        public string pokeparkwiiDolphin;
        public string[] apSearchPaths;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! PokéPark Wii AP is ready to use.";

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
        if (installPokeParkAPToggle != null) installPokeParkAPToggle.isOn = true;
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
                pokeparkwiiApworldDownloadUrl = remoteConfig.pokeparkwiiApworld;
                pokeparkwiiAPDownloadUrl = remoteConfig.pokeparkwiiAP;
                pokeparkwiiDolphinDownloadUrl = remoteConfig.pokeparkwiiDolphin;
                UnityEngine.Debug.Log("PokePark config loaded successfully.");
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
        bool installPokeParkAPSelected = installPokeParkAPToggle != null && installPokeParkAPToggle.isOn;
        bool installDolphinSelected = installDolphinToggle != null && installDolphinToggle.isOn;
        bool launchAfterSetup = launchAfterSetupToggle != null && launchAfterSetupToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "PokeParkAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        pokeparkwiiApworldInstalled = false;
        pokeparkPackageInstalled = false;
        dolphinInstalled = false;

        if (installApworldSelected && !installationCancelled)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installPokeParkAPSelected && !installationCancelled)
        {
            ShowInfo("Installing PokéPark AP Patcher...");
            yield return InstallPokeParkPackage(documentsPath, tempDownloadPath);
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

        if (launchAfterSetup && pokeparkPackageInstalled)
        {
            ShowInfo("Launching PokéPark client...");
            yield return LaunchPokeParkClient(documentsPath);
        }

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + pokeparkwiiApworld.url);

        if (string.IsNullOrEmpty(pokeparkwiiApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pokeparkwiiApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = pokeparkwiiApworld.url.Substring(pokeparkwiiApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pokeparkwiiApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pokeparkwiiApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            UnityEngine.Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);
        UnityEngine.Debug.Log("Using target path: " + target);

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                UnityEngine.Debug.Log("Deleted old apworld file");
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);

            ShowInfo("APWorld installed successfully!");
            lastApWorldInstallSuccess = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary APWorld file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
        }
    }

    IEnumerator InstallPokeParkPackage(string documentsPath, string tempPath)
    {
        pokeparkPackageInstalled = false;

        while (!configLoaded) yield return null;

        if (string.IsNullOrEmpty(pokeparkwiiAPDownloadUrl))
        {
            ShowInfo("ERROR: PokéPark AP package URL not loaded!");
            yield break;
        }

        string zipFileName = pokeparkwiiAP.fileName;
        if (string.IsNullOrEmpty(zipFileName))
        {
            zipFileName = pokeparkwiiAPDownloadUrl.Substring(pokeparkwiiAPDownloadUrl.LastIndexOf('/') + 1);
            if (zipFileName.Contains("?")) zipFileName = zipFileName.Substring(0, zipFileName.IndexOf("?"));
        }

        string localZipPath = Path.Combine(tempPath, zipFileName);
        string extractPath = Path.Combine(tempPath, "extract");

        yield return DownloadFile(pokeparkwiiAPDownloadUrl, localZipPath);

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

        // Search for pokeparkrando.exe inside the extracted contents
        string[] found = new string[0];
        try
        {
            found = Directory.GetFiles(extractPath, "pokeparkrando.exe", SearchOption.AllDirectories);
        }
        catch { }

        if (found == null || found.Length == 0)
        {
            UnityEngine.Debug.LogError("pokeparkrando.exe not found inside the extracted AP package.");
            ShowInfo("ERROR: pokeparkrando.exe not found in AP package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        string sourceExe = found[0];
        string targetFolder = Path.Combine(documentsPath, "PokéPark Wii AP Patcher");
        string targetExePath = Path.Combine(targetFolder, "pokeparkrando.exe");

        try
        {
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
            if (File.Exists(targetExePath)) File.Delete(targetExePath);
            File.Copy(sourceExe, targetExePath, true);
            UnityEngine.Debug.Log("pokeparkrando.exe copied to: " + targetExePath);
            ShowInfo("PokéPark AP package installed to Documents!");
            pokeparkPackageInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying pokeparkrando.exe: " + e.Message);
            ShowInfo("ERROR: Failed to install PokéPark AP package: " + e.Message);
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        try { if (File.Exists(localZipPath)) File.Delete(localZipPath); } catch { }
        SafeDeleteDirectory(extractPath);

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(pokeparkwiiDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        yield return DownloadFile(pokeparkwiiDolphinDownloadUrl, sevenZPath);

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

    IEnumerator LaunchPokeParkClient(string documentsPath)
    {
        string clientFolder = Path.Combine(documentsPath, "PokéPark Wii AP Patcher");
        string clientPath = Path.Combine(clientFolder, "pokeparkrando.exe");

        if (!File.Exists(clientPath))
        {
            UnityEngine.Debug.LogError("PokéPark client not found at: " + clientPath);
            ShowInfo("ERROR: Could not find PokéPark client executable!");
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
            UnityEngine.Debug.Log("PokéPark client launched: " + clientPath);
            ShowInfo("PokéPark client launched successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error launching PokéPark client: " + e.Message);
            ShowInfo("ERROR: Failed to launch PokéPark client: " + e.Message);
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

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.apSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, "custom_worlds");
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Archipelago custom_worlds (via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Archipelago custom_worlds directory not found.");
        return "";
    }
}