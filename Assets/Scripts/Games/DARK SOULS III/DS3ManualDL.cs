using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;

public class DS3ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DS3 FILE")]
    public FileDownloader.FileData ds3AP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "DARK SOULS III";

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string ds3Path;
    private string pendingAction;
    private DS3Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class DS3Config
    {
        public string ds3AP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        ds3Path = GetDS3Path();

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

        StartCoroutine(LoadRemoteConfig());
    }

    void ApplyDS3Config()
    {
        if (remoteConfig == null)
            return;

        ds3AP.url = remoteConfig.ds3AP;
        ds3AP.fileName = "DS3-Archipelago.zip";
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to install the DS3-Archipelago folder?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to remove the DS3-Archipelago folder?", "Revert");
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
            case "Revert":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        ds3Path = GetDS3Path();

        if (string.IsNullOrEmpty(ds3Path))
        {
            ShowInfo("DS3 path not found. Please check Steam installation.");
            return;
        }

        StartCoroutine(DS3APInstallFlow());
    }

    private void ExecuteRevert()
    {
        ds3Path = GetDS3Path();

        if (string.IsNullOrEmpty(ds3Path))
        {
            ShowInfo("DS3 path not found. Cannot revert.");
            return;
        }

        string target = Path.Combine(ds3Path, "DS3-Archipelago");

        if (!Directory.Exists(target))
        {
            ShowInfo("No DS3-Archipelago folder found to remove.");
            return;
        }

        ShowInfo("Removing DS3-Archipelago...");
        SafeDeleteDirectory(target);
        ShowInfo("DS3-Archipelago removed.");
    }

    IEnumerator DS3APInstallFlow()
    {
        ShowInfo("Installing DS3 Archipelago files...");
        yield return InstallDS3AP();
        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator InstallDS3AP()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "DS3APTemp");

        yield return downloader.DownloadAndExtract(ds3AP, Application.persistentDataPath, extractPath);

        // Create target folder DS3-Archipelago under game path
        string targetPath = Path.Combine(ds3Path, "DS3-Archipelago");

        try
        {
            // Remove existing target if present
            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            Directory.CreateDirectory(targetPath);

            // Move all extracted contents into DS3-Archipelago (flatten sequence: if zip has single root folder, its contents will be moved under DS3-Archipelago)
            // If the extraction produced a single top-level directory, move its contents instead of nesting.
            string[] topLevelDirs = Directory.GetDirectories(extractPath);
            string[] topLevelFiles = Directory.GetFiles(extractPath);

            if (topLevelDirs.Length == 1 && topLevelFiles.Length == 0)
            {
                // Move contents of the single root folder
                string singleRoot = topLevelDirs[0];
                MoveDirectory(singleRoot, targetPath);
            }
            else
            {
                // Move everything extracted
                MoveDirectory(extractPath, targetPath);
            }

            ShowInfo("DS3 Archipelago installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install DS3 Archipelago: " + e.Message);
            ShowInfo("ERROR: Failed to install DS3 Archipelago\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<DS3Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDS3Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        ds3Path = GetDS3Path();
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete directory " + path + " - " + e.Message);
        }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(source.Length + 1);
            string dest = Path.Combine(target, relativePath);

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

    string GetDS3Path()
    {
        string[] quickPaths = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", steamGameFolderName),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", steamGameFolderName),
        };

        foreach (string path in quickPaths)
        {
            UnityEngine.Debug.Log("Checking path: [" + path + "] Exists=" + Directory.Exists(path));
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Steam) at: " + path);
                    return path;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log("Exception checking path: " + e.Message);
            }
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
}
