using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UnderYellowManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("UNDERTALE YELLOW FILES")]
    public FileDownloader.FileData undertaleyellowApworld;
    public FileDownloader.FileData undertaleyellowDL;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installUndertaleYellowToggle;
    public Toggle launchGameToggle;

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

    private const string InstalledFoldersKey = "UndertaleYellow_InstalledFolders";

    private const string GameFolderName = "Undertale Yellow AP";
    private const string GameExeName = "Undertale Yellow.exe";

    private string pendingAction;

    private UndertaleYellowConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastGameInstallSuccess = false;

    [System.Serializable]
    public class UndertaleYellowConfig
    {
        public string undertaleyellowApworld;
        public string undertaleyellowDL;
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

        if (launchGameToggle != null)
            launchGameToggle.isOn = false;

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
    // LAUNCH BUTTON HANDLER
    // ---------------------------------------------------------------

    public void OnLaunchButtonClicked()
    {
        ShowInfo("Launching Undertale Yellow...");
        LaunchUndertaleYellow();
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool undertaleYellow = installUndertaleYellowToggle == null || installUndertaleYellowToggle.isOn;
        bool launchGame = launchGameToggle != null && launchGameToggle.isOn;

        if (!apworld && !undertaleYellow && !launchGame)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld, undertaleYellow, launchGame));
    }

    IEnumerator InstallFlow(bool apworld, bool undertaleYellow, bool launchGame)
    {
        yield return new WaitUntil(() => configLoaded);

        if (apworld)
        {
            ShowInfo("Installing Undertale Yellow APWorld...");
            yield return new WaitForSeconds(1f);
            yield return InstallAPWorld();

            if (!lastApWorldInstallSuccess)
                yield break;
        }

        if (undertaleYellow)
        {
            ShowInfo("Downloading Undertale Yellow...");
            yield return InstallUndertaleYellowDL();

            if (!lastGameInstallSuccess)
                yield break;
        }

        ShowInfo("Installation complete!");

        if (launchGame)
        {
            yield return new WaitForSeconds(1.5f);
            ShowInfo("Launching Undertale Yellow...");
            LaunchUndertaleYellow();
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

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + undertaleyellowApworld.url);

        if (string.IsNullOrEmpty(undertaleyellowApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = undertaleyellowApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = undertaleyellowApworld.url.Substring(undertaleyellowApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + undertaleyellowApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(undertaleyellowApworld.url, localPath);

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

    // Télécharge le .zip, l'extrait, puis copie son contenu dans le dossier Documents de Windows.
    IEnumerator InstallUndertaleYellowDL()
    {
        UnityEngine.Debug.Log("START InstallUndertaleYellowDL");

        lastGameInstallSuccess = false;

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(undertaleyellowDL.url))
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

        string extractPath = Path.Combine(Application.persistentDataPath, "UndertaleYellow_Extract");

        // Nettoie une éventuelle extraction précédente restée sur le disque
        SafeDeleteDirectory(extractPath);

        yield return downloader.DownloadAndExtract(undertaleyellowDL, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extraction failed: " + extractPath + " does not exist");
            ShowInfo("ERROR: Download/extraction failed!");
            yield break;
        }

        // Mémorise les dossiers de premier niveau du zip pour pouvoir les retrouver au Revert
        string[] topLevelFolders = GetTopLevelDirNames(extractPath);

        // Copie le contenu du zip directement dans Documents
        bool copied = CopyExtractedFiles(extractPath, documentsPath);

        SafeDeleteDirectory(extractPath);

        if (!copied)
        {
            ShowInfo("ERROR: Failed to copy Undertale Yellow files to Documents!");
            yield break;
        }

        if (topLevelFolders.Length > 0)
        {
            PlayerPrefs.SetString(InstalledFoldersKey, string.Join(",", topLevelFolders));
            PlayerPrefs.Save();
        }

        lastGameInstallSuccess = true;

        UnityEngine.Debug.Log("END InstallUndertaleYellowDL");
        ShowInfo("Undertale Yellow installed successfully!");
    }

    bool CopyExtractedFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyExtractedFiles: " + extractPath + " -> " + targetPath);

        try
        {
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("All extracted files copied to Documents");
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying extracted files: " + e.Message);
            return false;
        }
    }

    string[] GetTopLevelDirNames(string path)
    {
        try
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            string[] names = new string[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
                names[i] = Path.GetFileName(dirs[i]);
            return names;
        }
        catch
        {
            return new string[0];
        }
    }

    // ---------------------------------------------------------------
    // REVERT (supprime les dossiers placés dans Documents par le zip)
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        string documentsPath = GetDocumentsPath();

        if (string.IsNullOrEmpty(documentsPath) || !Directory.Exists(documentsPath))
        {
            ShowInfo("Documents folder not found.");
            return;
        }

        ShowInfo("Removing Undertale Yellow files...");

        bool removedSomething = false;

        string stored = PlayerPrefs.GetString(InstalledFoldersKey, "");
        if (!string.IsNullOrEmpty(stored))
        {
            string[] folders = stored.Split(',');
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string fullPath = Path.Combine(documentsPath, folder);
                if (Directory.Exists(fullPath))
                {
                    SafeDeleteDirectory(fullPath);
                    removedSomething = true;
                }
            }

            PlayerPrefs.DeleteKey(InstalledFoldersKey);
            PlayerPrefs.Save();
        }

        if (!removedSomething)
        {
            // Fallback si on n'a pas d'enregistrement : on supprime le dossier connu du jeu
            string gameFolder = Path.Combine(documentsPath, GameFolderName);
            if (Directory.Exists(gameFolder))
            {
                SafeDeleteDirectory(gameFolder);
                removedSomething = true;
            }
        }

        if (removedSomething)
            ShowInfo("Undertale Yellow files removed successfully!");
        else
            ShowInfo("No Undertale Yellow installation found to remove.");
    }

    // ---------------------------------------------------------------
    // LAUNCH
    // ---------------------------------------------------------------

    void LaunchUndertaleYellow()
    {
        try
        {
            string documentsPath = GetDocumentsPath();
            string exePath = Path.Combine(documentsPath, GameFolderName, GameExeName);

            if (File.Exists(exePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
                startInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                UnityEngine.Debug.Log("Undertale Yellow launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Undertale Yellow.exe not found at: " + exePath);
                ShowInfo("Undertale Yellow.exe not found! Please make sure you setup first!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Undertale Yellow: " + e.Message);
            ShowInfo("ERROR: Failed to launch Undertale Yellow!");
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
                UnityEngine.Debug.LogWarning("Undertale Yellow config load failed (this is OK, config is optional): " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<UndertaleYellowConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Undertale Yellow remote config loaded successfully");
                ApplyUndertaleYellowConfig();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Undertale Yellow config parsing failed (this is OK, config is optional): " + e.Message);
            }

            configLoaded = true;
        }
    }

    void ApplyUndertaleYellowConfig()
    {
        if (remoteConfig == null)
            return;

        if (undertaleyellowApworld != null && !string.IsNullOrEmpty(remoteConfig.undertaleyellowApworld))
            undertaleyellowApworld.url = remoteConfig.undertaleyellowApworld;

        if (undertaleyellowDL != null && !string.IsNullOrEmpty(remoteConfig.undertaleyellowDL))
            undertaleyellowDL.url = remoteConfig.undertaleyellowDL;
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