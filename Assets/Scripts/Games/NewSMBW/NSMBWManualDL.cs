using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;

public class NSMBWManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER (optionnel)")]
    public FileDownloader downloader;
    public FileDownloader.FileData nsmbwApworld;
    public FileDownloader.FileData nsmbwDolphin;

    [Header("INSTALLATION UI")]
    public Toggle installDolphinToggle;

    public Button patchDolphinButton;
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

    private string nsmbwApworldDownloadUrl = "";
    private string nsmbwDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool nsmbwapInstalled { get; private set; } = false;
    public bool dolphinInstalled { get; private set; } = false;

    public bool nsmbwapLaunched { get; private set; } = false;
    public bool dolphinLaunched { get; private set; } = false;

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
        public string nsmbwApworld;
        public string nsmbwDolphin;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! New Super Mario Bros Wii AP Randomizer is ready to use.";

    void Start()
    {
        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);
        if (patchDolphinButton != null) patchDolphinButton.onClick.AddListener(OnPatchDolphinClicked);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (installDolphinToggle != null) installDolphinToggle.isOn = false;

        if (cancelButton != null) cancelButton.interactable = false;
        if (patchDolphinButton != null) patchDolphinButton.interactable = false;

        if (infoText != null) infoDefaultFontSize = infoText.fontSize;

        StartCoroutine(LoadRemoteConfig());
    }

    IEnumerator LoadRemoteConfig()
    {
        UnityEngine.Debug.Log("Loading NSMBW config from: " + configUrl);

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(configUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
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
                nsmbwApworldDownloadUrl = remoteConfig.nsmbwApworld;
                nsmbwDolphinDownloadUrl = remoteConfig.nsmbwDolphin;
                UnityEngine.Debug.Log("NSMBW config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
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

    void OnPatchDolphinClicked()
    {
        StartCoroutine(PatchDolphinConfig());
    }

    IEnumerator PatchDolphinConfig()
    {
        string dolphinConfigPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Dolphin Emulator", "Config", "Dolphin.ini"
        );

        if (!File.Exists(dolphinConfigPath))
        {
            ShowInfoWithFontSize("Config file not found, please install Dolphin and launch it first.", 26f);
            yield break;
        }

        try
        {
            var lines = new System.Collections.Generic.List<string>(File.ReadAllLines(dolphinConfigPath));

            int generalIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "[General]")
                {
                    generalIndex = i;
                    break;
                }
            }

            if (generalIndex != -1)
            {
                int generalEnd = lines.Count;
                for (int i = generalIndex + 1; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("[") && lines[i].TrimEnd().EndsWith("]"))
                    {
                        generalEnd = i;
                        break;
                    }
                }

                // Look for HotkeysRequireFocus
                int hotkeysIndex = -1;
                for (int i = generalIndex + 1; i < generalEnd; i++)
                {
                    if (lines[i].StartsWith("HotkeysRequireFocus ="))
                    {
                        hotkeysIndex = i;
                        break;
                    }
                }

                if (hotkeysIndex != -1)
                {
                    lines[hotkeysIndex] = "HotkeysRequireFocus = False";
                }
                else
                {
                    lines.Insert(generalIndex + 1, "HotkeysRequireFocus = False");
                }
            }
            else
            {
                // Create [General] section with HotkeysRequireFocus
                int firstSection = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("[") && lines[i].TrimEnd().EndsWith("]"))
                    {
                        firstSection = i;
                        break;
                    }
                }

                var toInsert = new System.Collections.Generic.List<string>();
                toInsert.Add("");
                toInsert.Add("[General]");
                toInsert.Add("HotkeysRequireFocus = False");

                if (firstSection != -1)
                {
                    lines.InsertRange(firstSection, toInsert);
                }
                else
                {
                    lines.AddRange(toInsert);
                }
            }

            File.WriteAllLines(dolphinConfigPath, lines.ToArray());
            ShowInfo("Dolphin configuration patched successfully! HotkeysRequireFocus set to False.");
            UnityEngine.Debug.Log("Dolphin config patched successfully - HotkeysRequireFocus set to False");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error patching Dolphin config: " + e.Message);
            ShowInfo("ERROR: Failed to patch Dolphin config: " + e.Message);
        }

        yield return null;
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
        if (patchDolphinButton != null) patchDolphinButton.interactable = false;

        bool installDolphin = installDolphinToggle != null && installDolphinToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "NSMBWAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        nsmbwapInstalled = false;
        dolphinInstalled = false;
        nsmbwapLaunched = false;
        dolphinLaunched = false;

        if (!installationCancelled)
        {
            ShowInfo("Installing NSMBW AP Randomizer...");
            yield return InstallNSMBWAP(documentsPath, tempDownloadPath);
        }

        if (installDolphin && !installationCancelled)
        {
            ShowInfo("Installing Dolphin...");
            yield return InstallDolphin(documentsPath, tempDownloadPath);
        }

        if (installationCancelled)
        {
            ShowInfo("Installation cancelled.");
            if (installButton != null) installButton.interactable = true;
            if (cancelButton != null) cancelButton.interactable = false;
            if (patchDolphinButton != null) patchDolphinButton.interactable = true;
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;
        if (patchDolphinButton != null) patchDolphinButton.interactable = true;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallNSMBWAP(string documentsPath, string tempPath)
    {
        nsmbwapInstalled = false;

        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(nsmbwApworldDownloadUrl))
        {
            ShowInfo("ERROR: NSMBW AP download URL not loaded!");
            yield break;
        }

        string fileName = nsmbwApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = nsmbwApworldDownloadUrl.Substring(nsmbwApworldDownloadUrl.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        ShowInfo("Downloading NSMBW APWorld...");
        yield return DownloadFile(nsmbwApworldDownloadUrl, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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

        try
        {
            if (File.Exists(target))
                File.Delete(target);

            File.Copy(localPath, target, true);

            UnityEngine.Debug.Log("APWorld file copied to: " + target);
            ShowInfo("APWorld installed successfully!");
            nsmbwapInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            nsmbwapInstalled = false;
            yield break;
        }

        try { if (File.Exists(localPath)) File.Delete(localPath); } catch { }

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(nsmbwDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        ShowInfo("Downloading Dolphin...");
        yield return DownloadFile(nsmbwDolphinDownloadUrl, sevenZPath);

        if (!File.Exists(sevenZPath) || installationCancelled)
        {
            ShowInfo("ERROR: Dolphin download failed!");
            yield break;
        }

        try
        {
            string dolphinDestPath = Path.Combine(dolphinDocumentsPath, "dolphin.7z");

            if (File.Exists(dolphinDestPath))
            {
                File.Delete(dolphinDestPath);
            }

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
            else UnityEngine.Debug.Log("Download complete!");
        }
    }

    void PerformDirectoryCopy(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            PerformDirectoryCopy(dir, targetSubDir);
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

    void ShowInfoWithFontSize(string message, float fontSize)
    {
        if (infoText != null)
        {
            infoText.fontSize = fontSize;
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
