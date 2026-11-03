using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AWManualDL : MonoBehaviour
{
    [Header("ANIMAL WELL FILES")]
    public FileDownloader.FileData animalwellApworld;

    [Header("REMOTE CONFIG")]
    public string remoteConfigUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

    [Header("GAME FOLDER NAME")]
    public string steamGameFolderName = "Animal Well";

    [Header("GAME EXECUTABLE")]
    public string gameExeName = "Animal Well.exe";

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process animalwellProcess;
    private string animalwellPath;
    private string pendingAction;
    private AWConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class AWConfig
    {
        public string animalwellApworld;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        animalwellPath = GetAnimalWellPath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);
    }

    // =========================================================

    void ApplyConfig()
    {
        if (remoteConfig == null) return;
        if (!string.IsNullOrEmpty(remoteConfig.animalwellApworld))
            animalwellApworld.url = remoteConfig.animalwellApworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup?", "Setup");

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null) confirmationMessage.text = message;
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (string.IsNullOrEmpty(pendingAction)) return;

        if (pendingAction == "Setup")
            ExecuteSetup();

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        animalwellPath = GetAnimalWellPath();
        StartCoroutine(APWorldFlow());
    }

    // =========================================================
    // INSTALL FLOW
    // =========================================================

    IEnumerator APWorldFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Installation complete!\nLaunching Animal Well...");
            yield return new WaitForSeconds(2f);
            LaunchAnimalWell();
        }
        else
        {
            ShowInfo("Installation complete!");
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + animalwellApworld.url);

        if (string.IsNullOrEmpty(animalwellApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        // The apworld is always saved/installed as animal_well.apworld
        string fileName = string.IsNullOrEmpty(animalwellApworld.fileName) ? "animal_well.apworld" : animalwellApworld.fileName;

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + animalwellApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(animalwellApworld.url, localPath);

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

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            yield return request.SendWebRequest();
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                UnityEngine.Debug.LogError("Download error: " + request.error);
            else
                UnityEngine.Debug.Log("Download complete! " + savePath);
        }
    }

    // =========================================================
    // LAUNCH
    // =========================================================

    public void LaunchAnimalWell()
    {
        if (string.IsNullOrEmpty(animalwellPath))
        {
            UnityEngine.Debug.LogWarning("Animal Well path not found, cannot launch.");
            return;
        }

        string exePath = Path.Combine(animalwellPath, gameExeName);
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    WorkingDirectory = animalwellPath,
                    UseShellExecute = true
                };

                animalwellProcess = Process.Start(psi);
                UnityEngine.Debug.Log("Animal Well launched.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Animal Well executable not found: " + exePath);
        }
    }

    // =========================================================
    // UI
    // =========================================================

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    // =========================================================
    // REMOTE CONFIG
    // =========================================================

    IEnumerator LoadRemoteConfig()
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(remoteConfigUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<AWConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                ApplyConfig();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
            }
        }

        configLoaded = true;

        animalwellPath = GetAnimalWellPath();
    }

    // =========================================================
    // PATH FINDING
    // =========================================================

    string GetAnimalWellPath()
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
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed)
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

    string GetApCustomWorldsPath()
    {
        if (remoteConfig != null && remoteConfig.apSearchPaths != null)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed)
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