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
    private bool lastApWorldInstallSuccess = false;

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
        public string[] apSearchPaths;
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
            if (confirmText != null) confirmText.text = "Are you sure you want to setup?";
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
        StartCoroutine(InstallFlow());
    }

    void ExecuteSetup()
    {
        bool dolphin = installDolphinToggle != null && installDolphinToggle.isOn;
        bool apworld = installApworldToggle != null && installApworldToggle.isOn;

        int count = (dolphin ? 1 : 0) + (apworld ? 1 : 0);

        if (!dolphin && !apworld)
        {
            ShowInfo("Please select at least one install option.");
            return;
        }

        if (dolphin && count == 1)
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string tempPath = Path.Combine(documentsPath, "PMTTYDAPTemp");
            if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

            StartCoroutine(InstallDolphin(documentsPath, tempPath));
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallFlow()
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
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
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

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + pmttydApworld.url);

        if (string.IsNullOrEmpty(pmttydApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pmttydApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = pmttydApworld.url.Substring(pmttydApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pmttydApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pmttydApworld.url, localPath);

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
