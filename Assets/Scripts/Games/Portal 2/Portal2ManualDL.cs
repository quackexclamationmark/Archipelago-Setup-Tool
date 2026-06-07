using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class Portal2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("PORTAL 2 FILES")]
    public FileDownloader.FileData portal2AP;
    public FileDownloader.FileData portal2Apworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installArchipelagoToggle;
    public Toggle installApworldToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process steamProcess;
    private string sourcemodsPath;
    private string apworldPath;
    private string pendingAction;
    private Portal2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class Portal2Config
    {
        public string portal2AP;
        public string portal2Apworld;
    }

    void Start()
    {
        sourcemodsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "sourcemods");
        apworldPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds");
        StartCoroutine(LoadRemoteConfig());

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

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installArchipelagoToggle != null)
            installArchipelagoToggle.isOn = true;
    }

    void ApplyPortal2Config()
    {
        if (remoteConfig == null)
            return;

        portal2AP.url = remoteConfig.portal2AP;
        portal2AP.fileName = "Portal2ArchipelagoMod.zip";

        portal2Apworld.url = remoteConfig.portal2Apworld;
        portal2Apworld.fileName = "portal2.apworld";
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup the mod?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to remove the Portal 2 Archipelago Mod?", "Revert");
    }

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        confirmationMessage.text = message;
        confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
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
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(sourcemodsPath))
        {
            ShowInfo("Steam sourcemods path not found.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        if (string.IsNullOrEmpty(sourcemodsPath))
            return;

        ShowInfo("Removing Portal 2 Archipelago Mod...");
        SafeDeleteDirectory(Path.Combine(sourcemodsPath, "Portal2ArchipelagoMod"));

        ShowInfo("Mod removed successfully!");
    }

    IEnumerator InstallFlow()
    {
        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing Portal 2 Archipelago Mod...");
            yield return InstallPortal2AP();
        }

        if (installApworldToggle != null && installApworldToggle.isOn)
        {
            ShowInfo("Installing Portal 2 APWorld...");
            yield return InstallPortal2Apworld();
        }

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Closing Steam...");
            CloseAllSteamProcesses();

            yield return new WaitForSeconds(1f);

            ShowInfo("Relaunching Steam...");
            LaunchSteam();
        }

        ShowInfo("You can now start Portal 2 Archipelago Mod in your library!");
    }

    IEnumerator InstallPortal2AP()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "Portal2APTemp");
        yield return downloader.DownloadAndExtract(portal2AP, Application.persistentDataPath, extractPath);

        Directory.CreateDirectory(sourcemodsPath);

        try
        {
            // Chercher le dossier "Portal2ArchipelagoMod" à l'intérieur de l'archive
            string[] modDirs = Directory.GetDirectories(extractPath, "Portal2ArchipelagoMod", SearchOption.AllDirectories);

            if (modDirs.Length > 0)
            {
                string modSourcePath = modDirs[0];
                string modTargetPath = Path.Combine(sourcemodsPath, "Portal2ArchipelagoMod");

                if (Directory.Exists(modTargetPath))
                    SafeDeleteDirectory(modTargetPath);

                CopyDirectory(modSourcePath, modTargetPath);
                UnityEngine.Debug.Log("Copied Portal2ArchipelagoMod to sourcemods");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Portal2ArchipelagoMod folder not found in archive");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing Portal 2 AP: " + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallPortal2Apworld()
    {
        while (!configLoaded)
            yield return null;

        string fileName = portal2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = portal2Apworld.url.Substring(portal2Apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + portal2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(portal2Apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        string[] targetPaths = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", fileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", fileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", fileName),
        };

        string target = "";
        foreach (string path in targetPaths)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                target = path;
                UnityEngine.Debug.Log("Using target path: " + target);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(target))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            UnityEngine.Debug.LogError("No valid target directory found!");
            yield break;
        }

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
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
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
            remoteConfig = JsonUtility.FromJson<Portal2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyPortal2Config();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void CloseAllSteamProcesses()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("steam");
            foreach (Process p in processes)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                    UnityEngine.Debug.Log("Closed Steam process");
                }
                catch { }
            }
        }
        catch { }
    }

    void LaunchSteam()
    {
        try
        {
            // Cherche steam.exe dans les chemins courants
            string[] steamPaths = new string[]
            {
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe"),
                @"C:\Program Files (x86)\Steam\steam.exe",
                @"C:\Program Files\Steam\steam.exe",
            };

            foreach (string steamPath in steamPaths)
            {
                if (File.Exists(steamPath))
                {
                    steamProcess = Process.Start(steamPath);
                    UnityEngine.Debug.Log("Steam launched successfully!");
                    return;
                }
            }

            UnityEngine.Debug.LogWarning("Steam executable not found in common locations");
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching Steam:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
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
}
