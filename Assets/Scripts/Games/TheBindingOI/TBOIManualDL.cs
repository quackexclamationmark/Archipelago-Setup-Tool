using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class TBOIManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("TBOI FILES")]
    public FileDownloader.FileData tboiApworld;
    public FileDownloader.FileData tboiSaveInstaller;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installSaveInstallerToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("LAUNCH")]
    public Button launchButton;

    [Header("STEAM WORKSHOP")]
    [SerializeField]
    private string steamWorkshopUrl = "steam://url/CommunityFilePage/3640861678";

    private string archipelagoPath;
    private string pendingAction;

    private TBOIConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class TBOIConfig
    {
        public string tboiApworld;
        public string tboiSaveInstaller;
        public string[] apSearchPaths;
        public string[] gameSearchPaths;
    }


    void Start()
    {
        archipelagoPath = GetArchipelagoPath();

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (installSaveInstallerToggle != null)
            installSaveInstallerToggle.isOn = false;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchButtonClicked);

        StartCoroutine(LoadRemoteConfig());
    }

    // ---------------------------------------------------------------
    // BUTTON ENTRY POINTS
    // ---------------------------------------------------------------

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingAction))
            return;

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // LAUNCH BUTTON HANDLER
    // ---------------------------------------------------------------

    public void OnLaunchButtonClicked()
    {
        ShowInfo("Launching The Binding of Isaac: Rebirth...");
        LaunchTBOI();
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath))
        {
            ShowInfo("ERROR: Could not find or create the Archipelago folder!");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool saveInstaller = installSaveInstallerToggle == null || installSaveInstallerToggle.isOn;

        if (!apworld && !saveInstaller)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld, saveInstaller));
    }

    IEnumerator InstallFlow(bool apworld, bool saveInstaller)
    {
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (saveInstaller)
        {
            ShowInfo("Installing Isaac Save Installer...");
            yield return InstallSaveInstaller();
        }

        ShowInfo("Installation complete!");

        OpenSteamWorkshopPage();
    }

    // ---------------------------------------------------------------
    // STEAM WORKSHOP
    // ---------------------------------------------------------------

    public void OpenSteamWorkshopPage()
    {
        if (string.IsNullOrEmpty(steamWorkshopUrl))
            return;

        try
        {
            Application.OpenURL(steamWorkshopUrl);
            UnityEngine.Debug.Log("Opened Steam Workshop page: " + steamWorkshopUrl);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to open Steam Workshop page: " + e.Message);
        }
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + tboiApworld.url);

        if (string.IsNullOrEmpty(tboiApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = tboiApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = tboiApworld.url.Substring(tboiApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + tboiApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(tboiApworld.url, localPath);

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

    // ---------------------------------------------------------------
    // ISAAC SAVE INSTALLER (single .exe, not a zip/folder)
    // ---------------------------------------------------------------

    IEnumerator InstallSaveInstaller()
    {
        UnityEngine.Debug.Log("START InstallSaveInstaller");

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(tboiSaveInstaller.url))
        {
            ShowInfo("ERROR: Save Installer URL is empty!");
            yield break;
        }

        string fileName = tboiSaveInstaller.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = "isaac-save-installer.exe";

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Isaac Save Installer from: " + tboiSaveInstaller.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(tboiSaveInstaller.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Isaac Save Installer download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string targetDir = Path.Combine(documentsPath, "IsaacSaveInstaller");

        try
        {
            Directory.CreateDirectory(targetDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to create IsaacSaveInstaller folder: " + e.Message);
            ShowInfo("ERROR: Could not create IsaacSaveInstaller folder\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(targetDir, fileName);
        UnityEngine.Debug.Log("Using target path: " + target);

        if (File.Exists(target))
        {
            try
            {
                File.Delete(target);
                UnityEngine.Debug.Log("Deleted old save installer file");
            }
            catch { }
        }

        try
        {
            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("Isaac Save Installer copied to: " + target);

            ShowInfo("Isaac Save Installer installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Isaac Save Installer: " + e.Message);
            ShowInfo("ERROR: Failed to install Isaac Save Installer\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);

        UnityEngine.Debug.Log("END InstallSaveInstaller");
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                UnityEngine.Debug.Log("Cleaned up temporary file: " + localPath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    // ---------------------------------------------------------------
    // LAUNCH
    // ---------------------------------------------------------------

    void LaunchTBOI()
    {
        try
        {
            string gameDir = GetGameDirectoryPath();

            if (string.IsNullOrEmpty(gameDir))
            {
                UnityEngine.Debug.LogError("The Binding of Isaac Rebirth folder not found.");
                ShowInfo("The Binding of Isaac Rebirth folder not found! Make sure the game is installed.");
                return;
            }

            string exePath = Path.Combine(gameDir, "isaac-ng.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("The Binding of Isaac Rebirth launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("isaac-ng.exe not found under: " + gameDir);
                ShowInfo("isaac-ng.exe not found! Please make sure the game is installed correctly.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch The Binding of Isaac Rebirth: " + e.Message);
            ShowInfo("ERROR: Failed to launch The Binding of Isaac Rebirth!");
        }
    }

    // Locates the game's install folder dynamically by scanning fixed drives for
    // common Steam library layouts, e.g. "Steam\steamapps\common\The Binding of Isaac Rebirth".
    string GetGameDirectoryPath()
    {
        const string gameFolderName = "The Binding of Isaac Rebirth";

        string[] defaultRelativePaths = new string[]
        {
            Path.Combine("Program Files (x86)", "Steam", "steamapps", "common", gameFolderName),
            Path.Combine("Program Files", "Steam", "steamapps", "common", gameFolderName),
            Path.Combine("Steam", "steamapps", "common", gameFolderName),
            Path.Combine("SteamLibrary", "steamapps", "common", gameFolderName),
        };

        string[] relativePaths = (remoteConfig != null && remoteConfig.gameSearchPaths != null && remoteConfig.gameSearchPaths.Length > 0)
            ? remoteConfig.gameSearchPaths
            : defaultRelativePaths;

        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                foreach (string relativePath in relativePaths)
                {
                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    try
                    {
                        string path = Path.Combine(drive.Name, relativePath);
                        if (Directory.Exists(path))
                        {
                            UnityEngine.Debug.Log("Found The Binding of Isaac Rebirth folder at: " + path);
                            return path;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("The Binding of Isaac Rebirth folder not found.");
        return "";
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
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

    string GetArchipelagoPath()
    {
        string[] candidates = new string[]
        {
            @"C:\ProgramData\Archipelago",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        foreach (string candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    UnityEngine.Debug.Log("Found Archipelago folder at: " + candidate);
                    return candidate;
                }
            }
            catch { }
        }

        foreach (string candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                UnityEngine.Debug.Log("Created Archipelago folder at: " + candidate);
                return candidate;
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Could not find or create an Archipelago folder.");
        return "";
    }

    // ---------------------------------------------------------------
    // Remote config loader (optional)
    // ---------------------------------------------------------------
    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("TBOI config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<TBOIConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("TBOI remote config loaded successfully");
                ApplyTBOIConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("TBOI config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyTBOIConfig()
    {
        if (remoteConfig == null)
            return;

        if (tboiApworld != null && !string.IsNullOrEmpty(remoteConfig.tboiApworld))
            tboiApworld.url = remoteConfig.tboiApworld;

        if (tboiSaveInstaller != null && !string.IsNullOrEmpty(remoteConfig.tboiSaveInstaller))
            tboiSaveInstaller.url = remoteConfig.tboiSaveInstaller;
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