using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;

public class SMGManualDL : MonoBehaviour
{
    public FileDownloader downloader;
    public FileDownloader.FileData smgApworld;
    public FileDownloader.FileData smgDolphin;

    [Header("PANEL")]
    public GameObject installationPanel;

    [Header("INSTALLATION UI")]
    public Toggle installApworldToggle;
    public Toggle installDolphinToggle;

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

    [Header("7ZIP")]
    [Tooltip("Chemin relatif a Application.streamingAssetsPath")]
    public string sevenZipRelativePath = "7z/7za.exe";

    private string smgApworldDownloadUrl = "";
    private string smgDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    public bool smgApworldInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;

    public bool installationCancelled { get; private set; } = false;
    public bool installationComplete { get; private set; } = false;

    private List<Process> launchedProcesses = new List<Process>();

    [System.Serializable]
    public class RemoteConfig
    {
        public string smgApworld;
        public string smgDolphin;
        public string[] apSearchPaths;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete!";

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
        if (installDolphinToggle != null) installDolphinToggle.isOn = false;

        if (cancelButton != null) cancelButton.interactable = false;

        if (infoText != null) infoDefaultFontSize = infoText.fontSize;

        if (smgApworld != null && string.IsNullOrEmpty(smgApworld.fileName))
        {
            smgApworld.fileName = "smgalaxy.apworld";
        }

        StartCoroutine(LoadRemoteConfig());
    }

    // ------------------------------------------------------------------
    // CONFIG
    // ------------------------------------------------------------------

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
                smgApworldDownloadUrl = remoteConfig.smgApworld;
                smgDolphinDownloadUrl = remoteConfig.smgDolphin;
                UnityEngine.Debug.Log("SMG config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (optional): " + e.Message);
        }
    }

    // ------------------------------------------------------------------
    // UI CALLBACKS
    // ------------------------------------------------------------------

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

    // ------------------------------------------------------------------
    // INSTALLATION FLOW
    // ------------------------------------------------------------------

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
        bool installDolphinSelected = installDolphinToggle != null && installDolphinToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "SMGAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        smgApworldInstalled = false;
        dolphinInstalled = false;

        if (installApworldSelected && !installationCancelled)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld(tempDownloadPath);
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
            SafeDeleteDirectory(tempDownloadPath);
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;

        SafeDeleteDirectory(tempDownloadPath);
    }

    // ------------------------------------------------------------------
    // APWORLD
    // ------------------------------------------------------------------

    IEnumerator InstallAPWorld(string tempPath)
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        // URL de la remote config en priorite, sinon celle de l'inspector
        string apworldUrl = !string.IsNullOrEmpty(smgApworldDownloadUrl)
            ? smgApworldDownloadUrl
            : (smgApworld != null ? smgApworld.url : "");

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + apworldUrl);

        if (string.IsNullOrEmpty(apworldUrl))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = smgApworld != null ? smgApworld.fileName : null;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "smgalaxy.apworld";
        }

        string localPath = Path.Combine(tempPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + apworldUrl);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apworldUrl, localPath);

        if (installationCancelled) yield break;

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
            smgApworldInstalled = true;
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

    // ------------------------------------------------------------------
    // DOLPHIN
    // ------------------------------------------------------------------

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        string dolphinUrl = !string.IsNullOrEmpty(smgDolphinDownloadUrl)
            ? smgDolphinDownloadUrl
            : (smgDolphin != null ? smgDolphin.url : "");

        if (string.IsNullOrEmpty(dolphinUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        // 1) Telechargement du .7z
        string sevenZArchive = Path.Combine(tempPath, "dolphin.7z");
        string extractPath = Path.Combine(tempPath, "dolphin_extract");

        ShowInfo("Downloading Dolphin...");
        yield return DownloadFile(dolphinUrl, sevenZArchive);

        if (installationCancelled) yield break;

        if (!File.Exists(sevenZArchive))
        {
            ShowInfo("ERROR: Dolphin download failed!");
            yield break;
        }

        // 2) Extraction avec 7za.exe
        string sevenZipExe = Path.Combine(Application.streamingAssetsPath, sevenZipRelativePath);
        if (!File.Exists(sevenZipExe))
        {
            UnityEngine.Debug.LogError("7za.exe not found at: " + sevenZipExe);
            ShowInfo("ERROR: 7za.exe not found!");
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        Directory.CreateDirectory(extractPath);

        ShowInfo("Extracting Dolphin...");

        Process proc = null;
        bool startFailed = false;

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = sevenZipExe,
                Arguments = "x \"" + sevenZArchive + "\" -o\"" + extractPath + "\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            proc = Process.Start(psi);
            if (proc != null) launchedProcesses.Add(proc);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to start 7za: " + e.Message);
            startFailed = true;
        }

        if (startFailed || proc == null)
        {
            ShowInfo("ERROR: Failed to start 7za.exe!");
            yield break;
        }

        // Attente non bloquante
        while (!proc.HasExited)
        {
            if (installationCancelled) yield break;
            yield return null;
        }

        launchedProcesses.Remove(proc);

        if (installationCancelled) yield break;

        if (proc.ExitCode != 0)
        {
            UnityEngine.Debug.LogError("7za exit code: " + proc.ExitCode);
            ShowInfo("ERROR: Dolphin extraction failed (code " + proc.ExitCode + ")");
            yield break;
        }

        try { File.Delete(sevenZArchive); } catch { }

        // 3) Recuperation du sous-dossier et deplacement dans Documents
        string sourceFolder = extractPath;
        string folderName = "Dolphin-SMG";

        string[] subDirs = Directory.GetDirectories(extractPath);
        if (subDirs.Length > 0)
        {
            sourceFolder = subDirs[0];
            folderName = Path.GetFileName(sourceFolder);
        }

        string destFolder = Path.Combine(documentsPath, folderName);

        try
        {
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, true);
                UnityEngine.Debug.Log("Deleted old Dolphin folder: " + destFolder);
            }

            Directory.Move(sourceFolder, destFolder);
            UnityEngine.Debug.Log("Dolphin moved to: " + destFolder);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error moving Dolphin folder: " + e.Message);
            ShowInfo("ERROR: Failed to move Dolphin folder:\n" + e.Message);
            yield break;
        }

        SafeDeleteDirectory(extractPath);

        // 4) Configuration de Dolphin.ini
        try
        {
            EnsureDolphinIni();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error editing Dolphin.ini: " + e.Message);
            ShowInfo("ERROR: Failed to configure Dolphin.ini:\n" + e.Message);
            yield break;
        }

        dolphinInstalled = true;
        ShowInfo("Dolphin installed successfully!");

        yield return null;
    }

    // ------------------------------------------------------------------
    // DOLPHIN.INI
    // ------------------------------------------------------------------

    void EnsureDolphinIni()
    {
        string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        string configDir = Path.Combine(appData, "Dolphin Emulator", "Config");
        string iniPath = Path.Combine(configDir, "Dolphin.ini");

        if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

        List<string> lines = new List<string>();
        if (File.Exists(iniPath))
        {
            lines.AddRange(File.ReadAllLines(iniPath));
        }
        else
        {
            UnityEngine.Debug.Log("Dolphin.ini not found, creating: " + iniPath);
        }

        SetIniValue(lines, "Core", "MMU", "False");
        SetIniValue(lines, "Core", "RAMOverrideEnable", "False");

        File.WriteAllLines(iniPath, lines.ToArray(), new UTF8Encoding(false));
        UnityEngine.Debug.Log("Dolphin.ini updated: " + iniPath);
    }

    /// <summary>
    /// Met a jour (ou ajoute) "key = value" dans la section donnee,
    /// en preservant le reste du fichier. Cree la section si absente.
    /// </summary>
    void SetIniValue(List<string> lines, string section, string key, string value)
    {
        string header = "[" + section + "]";
        int sectionStart = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(header, System.StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
                break;
            }
        }

        // Section absente : on l'ajoute a la fin
        if (sectionStart < 0)
        {
            lines.Add(header);
            lines.Add(key + " = " + value);
            return;
        }

        // Fin de la section = prochaine ligne "[...]" ou fin du fichier
        int sectionEnd = lines.Count;
        for (int i = sectionStart + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("["))
            {
                sectionEnd = i;
                break;
            }
        }

        // Cle existante : on remplace
        for (int i = sectionStart + 1; i < sectionEnd; i++)
        {
            string line = lines[i];
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string existingKey = line.Substring(0, eq).Trim();
            if (existingKey.Equals(key, System.StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = key + " = " + value;
                return;
            }
        }

        // Cle absente : on l'insere apres la derniere ligne non vide de la section
        int insertAt = sectionEnd;
        while (insertAt > sectionStart + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
            insertAt--;

        lines.Insert(insertAt, key + " = " + value);
    }

    // ------------------------------------------------------------------
    // UTILS
    // ------------------------------------------------------------------

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
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
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