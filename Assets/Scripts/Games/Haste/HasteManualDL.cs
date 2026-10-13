using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class HasteManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("HASTE FILES")]
    public FileDownloader.FileData hasteApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Haste";

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
    private string steamWorkshopUrl = "steam://url/CommunityFilePage/3462307025";

    private string archipelagoPath;
    private string pendingAction;

    private HasteConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class HasteConfig
    {
        public string hasteApworld;
        public string[] apSearchPaths;
        public string[] steamSearchPaths;
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
        ShowInfo("Launching Haste...");
        LaunchHaste();
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

        if (!apworld)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld));
    }

    IEnumerator InstallFlow(bool apworld)
    {
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + hasteApworld.url);

        if (string.IsNullOrEmpty(hasteApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = hasteApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "haste.apworld";
            UnityEngine.Debug.Log("Using default filename: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + hasteApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(hasteApworld.url, localPath);

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

    void LaunchHaste()
    {
        try
        {
            string gameDir = GetHastePath();

            if (string.IsNullOrEmpty(gameDir))
            {
                UnityEngine.Debug.LogError("Haste folder not found.");
                ShowInfo("Haste folder not found! Make sure the game is installed.");
                return;
            }

            string exePath = Path.Combine(gameDir, "Haste.exe");

            if (File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Haste launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Haste.exe not found under: " + gameDir);
                ShowInfo("Haste.exe not found! Please make sure the game is installed correctly.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Haste: " + e.Message);
            ShowInfo("ERROR: Failed to launch Haste!");
        }
    }

    string GetHastePath()
{
    string[] quickPaths = new string[]
    {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", steamGameFolderName),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", steamGameFolderName),
    };

    foreach (string path in quickPaths)
    {
        try
        {
            if (Directory.Exists(path))
            {
                UnityEngine.Debug.Log("Found Game (Steam) at: " + path);
                return path;
            }
        }
        catch { }
    }

    if (remoteConfig != null && remoteConfig.steamSearchPaths != null)
    {
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                foreach (string relativePath in remoteConfig.steamSearchPaths)
                {
                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    try
                    {
                        string path = Path.Combine(drive.Name, relativePath, steamGameFolderName);
                        if (Directory.Exists(path))
                        {
                            UnityEngine.Debug.Log("Found Game (Steam, via remote config) at: " + path);
                            return path;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    UnityEngine.Debug.LogWarning("Game (Steam) not found.");
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
                UnityEngine.Debug.LogWarning("Haste config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<HasteConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Haste remote config loaded successfully");
                ApplyHasteConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Haste config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyHasteConfig()
    {
        if (remoteConfig == null)
            return;

        if (hasteApworld != null && !string.IsNullOrEmpty(remoteConfig.hasteApworld))
            hasteApworld.url = remoteConfig.hasteApworld;
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