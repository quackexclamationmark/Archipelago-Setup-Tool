using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;

public class PMTTYDManualDL : MonoBehaviour
{
    public FileDownloader downloader;
    public FileDownloader.FileData pmttydApworld;
    public FileDownloader.FileData pmttydDolphin;

    [Header("PANEL")]
    public GameObject installationPanel;

    [Header("INSTALLATION UI")]
    public Toggle installApworldToggle;
    public Toggle installDolphinToggle;

    public Button installButton;
    public Button patchDolphinButton;
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

    private string pmttydApworldDownloadUrl = "";
    private string pmttydDolphinDownloadUrl = "";
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    public bool pmttydApworldInstalled { get; private set; } = false;
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
        public string pmttydApworld;
        public string pmttydDolphin;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete!";

    void Start()
    {
        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (patchDolphinButton != null) patchDolphinButton.onClick.AddListener(OnPatchDolphinClicked);
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
        if (patchDolphinButton != null) patchDolphinButton.interactable = true;

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
                pmttydApworldDownloadUrl = remoteConfig.pmttydApworld;
                pmttydDolphinDownloadUrl = remoteConfig.pmttydDolphin;
                UnityEngine.Debug.Log("Paper Mario TTYD config loaded successfully.");
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

    void OnPatchDolphinClicked()
    {
        StartCoroutine(PatchDolphinConfig());
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
        bool installDolphinSelected = installDolphinToggle != null && installDolphinToggle.isOn;

        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string tempDownloadPath = Path.Combine(documentsPath, "PMTTYDAPTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);

        pmttydApworldInstalled = false;
        dolphinInstalled = false;

        if (installApworldSelected && !installationCancelled)
        {
            ShowInfo("Installing Paper Mario TTYD APWorld...");
            yield return InstallApworld(tempDownloadPath);
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

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;

        SafeDeleteDirectory(tempDownloadPath);
    }

    IEnumerator InstallApworld(string tempPath)
    {
        pmttydApworldInstalled = false;

        while (!configLoaded) yield return null;

        if (string.IsNullOrEmpty(pmttydApworldDownloadUrl))
        {
            ShowInfo("ERROR: pmttydApworld URL not loaded!");
            yield break;
        }

        string apworldFileName = pmttydApworld.fileName;
        if (string.IsNullOrEmpty(apworldFileName))
        {
            apworldFileName = pmttydApworldDownloadUrl.Substring(pmttydApworldDownloadUrl.LastIndexOf('/') + 1);
            if (apworldFileName.Contains("?")) apworldFileName = apworldFileName.Substring(0, apworldFileName.IndexOf("?"));
        }

        string localApworldPath = Path.Combine(Application.persistentDataPath, apworldFileName);

        yield return DownloadFile(pmttydApworldDownloadUrl, localApworldPath);

        if (!File.Exists(localApworldPath))
        {
            UnityEngine.Debug.LogError("APWorld download failed: file not found at " + localApworldPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string[] targetPaths = new string[]
        {
            Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", apworldFileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds", apworldFileName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago", "custom_worlds", apworldFileName),
        };

        string apworldTarget = "";
        foreach (string path in targetPaths)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                apworldTarget = path;
                UnityEngine.Debug.Log("Using APWorld target path: " + apworldTarget);
                break;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Cannot create directory: " + Path.GetDirectoryName(path) + " - " + e.Message);
            }
        }

        if (string.IsNullOrEmpty(apworldTarget))
        {
            ShowInfo("ERROR: Cannot find a valid Archipelago custom_worlds directory!");
            UnityEngine.Debug.LogError("No valid target directory found for apworld!");
            yield break;
        }

        try
        {
            if (File.Exists(apworldTarget)) File.Delete(apworldTarget);
            File.Copy(localApworldPath, apworldTarget, true);
            UnityEngine.Debug.Log("APWorld file copied to: " + apworldTarget);
            ShowInfo("APWorld installed successfully!");
            pmttydApworldInstalled = true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            pmttydApworldInstalled = false;
            yield break;
        }

        try { if (File.Exists(localApworldPath)) File.Delete(localApworldPath); } catch { }

        yield return null;
    }

    IEnumerator InstallDolphin(string documentsPath, string tempPath)
    {
        dolphinInstalled = false;

        if (string.IsNullOrEmpty(pmttydDolphinDownloadUrl))
        {
            ShowInfo("ERROR: Dolphin download URL not loaded!");
            yield break;
        }

        string sevenZPath = Path.Combine(tempPath, "dolphin.7z");
        string dolphinDocumentsPath = documentsPath;

        yield return DownloadFile(pmttydDolphinDownloadUrl, sevenZPath);

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

    IEnumerator PatchDolphinConfig()
    {
        string dolphinIniPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Dolphin Emulator", "Config", "Dolphin.ini"
        );

        if (!File.Exists(dolphinIniPath))
        {
            ShowInfo("ERROR: Dolphin.ini not found at:\n" + dolphinIniPath);
            UnityEngine.Debug.LogError("Dolphin.ini not found at: " + dolphinIniPath);
            yield break;
        }

        try
        {
            var lines = new System.Collections.Generic.List<string>(File.ReadAllLines(dolphinIniPath));
            int coreIndex = -1;

            // Find the [Core] section
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("[Core]"))
                {
                    coreIndex = i;
                    break;
                }
            }

            if (coreIndex == -1)
            {
                ShowInfo("ERROR: [Core] section not found in Dolphin.ini!");
                UnityEngine.Debug.LogError("[Core] section not found in Dolphin.ini");
                yield break;
            }

            // Define the settings to apply
            var settingsToApply = new System.Collections.Generic.Dictionary<string, string>()
            {
                { "CPUThread", "False" },
                { "GFXBackend", "OGL" },
                { "OverclockEnable", "False" },
                { "RAMOverrideEnable", "True" },
                { "MEM1Size", "0x04000000" },
                { "MMU", "True" }
            };

            // Process each setting
            foreach (var setting in settingsToApply)
            {
                string key = setting.Key;
                string value = setting.Value;
                bool found = false;

                // Search for the setting in the [Core] section
                for (int i = coreIndex + 1; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith("[")) break; // Stop at next section

                    if (lines[i].StartsWith(key + " =") || lines[i].StartsWith(key + "="))
                    {
                        lines[i] = key + " = " + value;
                        found = true;
                        UnityEngine.Debug.Log("Updated: " + lines[i]);
                        break;
                    }
                }

                // If not found, add the setting after [Core]
                if (!found)
                {
                    lines.Insert(coreIndex + 1, key + " = " + value);
                    UnityEngine.Debug.Log("Added: " + key + " = " + value);
                }
            }

            // Write back to file
            File.WriteAllLines(dolphinIniPath, lines);
            UnityEngine.Debug.Log("Dolphin.ini patched successfully!");
            ShowInfo("Dolphin.ini patched successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error patching Dolphin.ini: " + e.Message);
            ShowInfo("ERROR: Failed to patch Dolphin.ini:\n" + e.Message);
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
}
