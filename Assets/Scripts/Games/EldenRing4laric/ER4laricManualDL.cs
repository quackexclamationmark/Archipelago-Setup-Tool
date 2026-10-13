using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ER4laricManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("ELDEN RING FILES")]
    public FileDownloader.FileData eldenring4laricApworld;
    public FileDownloader.FileData eldenring4laricME3;
    public FileDownloader.FileData eldenring4laricAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installME3Toggle;
    public Toggle installAPToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private const string ApWorldFileName = "eldenring.apworld";
    private const string ME3InstallerFileName = "me3_installer.exe";
    private const string GameFolderName = "Elden Ring AP";
    private const string LaunchFileRelativeFolder = "me3";
    private const string LaunchFileName = "ap.me3";

    private string pendingAction;

    private ER4laricConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastME3InstallSuccess = false;
    private bool lastAPInstallSuccess = false;

    [System.Serializable]
    public class ER4laricConfig
    {
        public string eldenring4laricApworld;
        public string eldenring4laricME3;
        public string eldenring4laricAP;
        public string[] apSearchPaths;
    }


    void Start()
    {
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

    // ---------------------------------------------------------------
    // BUTTON ENTRY POINTS
    // ---------------------------------------------------------------

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
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

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool me3 = installME3Toggle == null || installME3Toggle.isOn;
        bool ap = installAPToggle == null || installAPToggle.isOn;

        if (!apworld && !me3 && !ap)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld, me3, ap));
    }

    IEnumerator InstallFlow(bool apworld, bool me3, bool ap)
    {
        yield return new WaitUntil(() => configLoaded);

        if (apworld)
        {
            ShowInfo("Installing Elden Ring APWorld...");
            yield return new WaitForSeconds(1f);
            yield return InstallAPWorld();

            if (!lastApWorldInstallSuccess)
                yield break;
        }

        if (ap)
        {
            ShowInfo("Downloading Elden Ring AP files...");
            yield return InstallAPFiles();

            if (!lastAPInstallSuccess)
                yield break;
        }

        if (me3)
        {
            ShowInfo("Downloading ME3 installer...");
            yield return InstallME3();

            if (!lastME3InstallSuccess)
                yield break;
        }

        ShowInfo("Installation complete!");
    }

    // ---------------------------------------------------------------
    // APWORLD
    // ---------------------------------------------------------------

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + eldenring4laricApworld.url);

        if (string.IsNullOrEmpty(eldenring4laricApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = ApWorldFileName;
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + eldenring4laricApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(eldenring4laricApworld.url, localPath);

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
    // ME3 INSTALLER (simple .exe téléchargé dans Downloads puis lancé)
    // ---------------------------------------------------------------

    IEnumerator InstallME3()
    {
        UnityEngine.Debug.Log("START InstallME3");

        lastME3InstallSuccess = false;

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(eldenring4laricME3.url))
        {
            ShowInfo("ERROR: ME3 download URL is empty!");
            yield break;
        }

        string downloadsPath = GetDownloadsPath();
        if (string.IsNullOrEmpty(downloadsPath) || !Directory.Exists(downloadsPath))
        {
            ShowInfo("ERROR: Could not find the Downloads folder!");
            yield break;
        }

        string exePath = Path.Combine(downloadsPath, ME3InstallerFileName);

        // Supprime un ancien installeur pour éviter les conflits
        try
        {
            if (File.Exists(exePath))
                File.Delete(exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete old ME3 installer: " + e.Message);
        }

        yield return DownloadFile(eldenring4laricME3.url, exePath);

        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("ME3 download failed: file not found at " + exePath);
            ShowInfo("ERROR: ME3 installer download failed!");
            yield break;
        }

        ShowInfo("Launching ME3 installer...");

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
            startInfo.WorkingDirectory = downloadsPath;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
            UnityEngine.Debug.Log("ME3 installer launched: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch ME3 installer: " + e.Message);
            ShowInfo("ERROR: Failed to launch the ME3 installer!\n" + e.Message);
            yield break;
        }

        lastME3InstallSuccess = true;

        UnityEngine.Debug.Log("END InstallME3");
        ShowInfo("ME3 installer downloaded and launched!");
    }

    // ---------------------------------------------------------------
    // AP FILES (zip -> Documents\Elden Ring AP)
    // ---------------------------------------------------------------

    IEnumerator InstallAPFiles()
    {
        UnityEngine.Debug.Log("START InstallAPFiles");

        lastAPInstallSuccess = false;

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(eldenring4laricAP.url))
        {
            ShowInfo("ERROR: Download URL is empty!");
            yield break;
        }

        string documentsPath = GetDocumentsPath();
        if (string.IsNullOrEmpty(documentsPath) || !Directory.Exists(documentsPath))
        {
            ShowInfo("ERROR: Could not find the Documents folder!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "EldenRingAP_Extract");

        // Nettoie une éventuelle extraction précédente restée sur le disque
        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(eldenring4laricAP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extraction failed: " + extractPath + " does not exist");
            ShowInfo("ERROR: Download/extraction failed!");
            yield break;
        }

        // Tout le contenu du zip va dans Documents\Elden Ring AP
        string gameFolder = Path.Combine(documentsPath, GameFolderName);

        bool copied = CopyExtractedFiles(extractPath, gameFolder);

        SafeDeleteDirectory(extractPath);

        if (!copied)
        {
            ShowInfo("ERROR: Failed to copy Elden Ring AP files to Documents!");
            yield break;
        }

        lastAPInstallSuccess = true;

        UnityEngine.Debug.Log("END InstallAPFiles");
        ShowInfo("Elden Ring AP files installed successfully!");
    }

    bool CopyExtractedFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyExtractedFiles: " + extractPath + " -> " + targetPath);

        try
        {
            Directory.CreateDirectory(targetPath);

            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("All extracted files copied to " + targetPath);
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying extracted files: " + e.Message);
            return false;
        }
    }

    // ---------------------------------------------------------------
    // REVERT (supprime le dossier "Elden Ring AP" et l'installeur ME3)
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        ShowInfo("Removing Elden Ring AP files...");

        bool removedSomething = false;

        string documentsPath = GetDocumentsPath();
        if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
        {
            string gameFolder = Path.Combine(documentsPath, GameFolderName);
            if (Directory.Exists(gameFolder))
            {
                SafeDeleteDirectory(gameFolder);
                removedSomething = true;
            }
        }

        string downloadsPath = GetDownloadsPath();
        if (!string.IsNullOrEmpty(downloadsPath))
        {
            string installerPath = Path.Combine(downloadsPath, ME3InstallerFileName);
            try
            {
                if (File.Exists(installerPath))
                {
                    File.Delete(installerPath);
                    removedSomething = true;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete ME3 installer: " + e.Message);
            }
        }

        if (removedSomething)
            ShowInfo("Elden Ring AP files removed successfully!");
        else
            ShowInfo("No Elden Ring AP installation found to remove.");
    }

    // ---------------------------------------------------------------
    // LAUNCH (public, à brancher sur un bouton)
    // ---------------------------------------------------------------

    public void LaunchGame()
    {
        try
        {
            string documentsPath = GetDocumentsPath();
            string me3Path = Path.Combine(documentsPath, GameFolderName, LaunchFileRelativeFolder, LaunchFileName);

            if (File.Exists(me3Path))
            {
                ShowInfo("Launching Elden Ring...");

                ProcessStartInfo startInfo = new ProcessStartInfo(me3Path);
                startInfo.WorkingDirectory = Path.GetDirectoryName(me3Path);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                UnityEngine.Debug.Log("Elden Ring launched: " + me3Path);
            }
            else
            {
                UnityEngine.Debug.LogError("ap.me3 not found at: " + me3Path);
                ShowInfo("ap.me3 not found! Please make sure you setup first!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Elden Ring: " + e.Message);
            ShowInfo("ERROR: Failed to launch Elden Ring!\n" + e.Message);
        }
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

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
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

    public string GetGamePath()
    {
        try
        {
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), GameFolderName);
        }
        catch
        {
            return "";
        }
    }

    string GetDocumentsPath()
    {
        try
        {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        }
        catch
        {
            return "";
        }
    }

    string GetDownloadsPath()
    {
        try
        {
            string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Downloads");
        }
        catch
        {
            return "";
        }
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
                UnityEngine.Debug.LogWarning("Elden Ring config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<ER4laricConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Elden Ring remote config loaded successfully");
                ApplyER4laricConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Elden Ring config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyER4laricConfig()
    {
        if (remoteConfig == null)
            return;

        if (eldenring4laricApworld != null && !string.IsNullOrEmpty(remoteConfig.eldenring4laricApworld))
            eldenring4laricApworld.url = remoteConfig.eldenring4laricApworld;

        if (eldenring4laricME3 != null && !string.IsNullOrEmpty(remoteConfig.eldenring4laricME3))
            eldenring4laricME3.url = remoteConfig.eldenring4laricME3;

        if (eldenring4laricAP != null && !string.IsNullOrEmpty(remoteConfig.eldenring4laricAP))
            eldenring4laricAP.url = remoteConfig.eldenring4laricAP;
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