using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UCGManualDL : MonoBehaviour
{
    [Header("UCG FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apZip;
    public FileDownloader.FileData coolerModsZip;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPZipToggle;
    public Toggle installCoolerModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string ucgPath;
    private string pendingAction;
    private UCGConfig remoteConfig;
    private bool configLoaded = false;

    private const string APZIP_PATTERN = "UncannyCatAP";
    private const string COOLERMODS_PATTERN = "CoolerMods";

    private const string APMOD_ID = "jeffdev.uncannycatap";
    private const string COOLERMODS_ID = "justsomejello.coolermods";

    [System.Serializable]
    public class UCGConfig
    {
        public string uncannycatApworld;
        public string uncannycatAP;
        public string uncannycatCoolerMods;
        public string[] apSearchPaths;
    }

    [System.Serializable]
    public class ModsJsonData
    {
        public bool activated;
        public List<string> enabled = new List<string>();
        public List<string> disabled = new List<string>();
    }

    void Start()
    {
        ucgPath = GetUCGPath();
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
    }

    void ApplyUCGConfig()
    {
        if (remoteConfig == null)
            return;

        apworld.url = remoteConfig.uncannycatApworld;
        apZip.url = remoteConfig.uncannycatAP;
        coolerModsZip.url = remoteConfig.uncannycatCoolerMods;
    }

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

    // =========================================================
    // SETUP
    // =========================================================

    private void ExecuteSetup()
    {
        ucgPath = GetUCGPath();

        bool apworldOn = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool apZipOn = installAPZipToggle != null && installAPZipToggle.isOn;
        bool coolerModsOn = installCoolerModsToggle != null && installCoolerModsToggle.isOn;

        int count =
            (apworldOn ? 1 : 0) +
            (apZipOn ? 1 : 0) +
            (coolerModsOn ? 1 : 0);

        if (apworldOn && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (apZipOn && count == 1)
        {
            StartCoroutine(APZipOnlyFlow());
            return;
        }

        if (coolerModsOn && count == 1)
        {
            StartCoroutine(CoolerModsOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installAPZipToggle != null && installAPZipToggle.isOn)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallAPZip();
        }

        if (installCoolerModsToggle != null && installCoolerModsToggle.isOn)
        {
            ShowInfo("Installing Cooler Mods...");
            yield return InstallCoolerMods();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        ShowInfo("Installation complete!");
    }

    IEnumerator APZipOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP Mod...");
        yield return InstallAPZip();

        ShowInfo("Installation complete!");
    }

    IEnumerator CoolerModsOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing Cooler Mods...");
        yield return InstallCoolerMods();

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = apworld.url.Substring(apworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        yield return DownloadFile(apworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string customWorldsDir = GetApCustomWorldsPath();

        if (string.IsNullOrEmpty(customWorldsDir))
        {
            ShowInfo("Archipelago directory not found. Please report it on the Discord server.");
            UnityEngine.Debug.LogError("No existing custom_worlds folder found, installation cancelled.");
            DeleteTempFile(localPath);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);

        if (File.Exists(target))
        {
            try { File.Delete(target); } catch { }
        }

        try
        {
            File.Copy(localPath, target, true);
            ShowInfo("APWorld installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
        }

        DeleteTempFile(localPath);
    }

    IEnumerator InstallAPZip()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(apZip.url))
        {
            ShowInfo("ERROR: AP mod URL is empty!");
            UnityEngine.Debug.LogError("AP mod URL not set!");
            yield break;
        }

        yield return InstallZipDirectly(apZip, "AP Mod", APZIP_PATTERN, APMOD_ID);
    }

    IEnumerator InstallCoolerMods()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(coolerModsZip.url))
        {
            ShowInfo("ERROR: Cooler Mods URL is empty!");
            UnityEngine.Debug.LogError("Cooler Mods URL not set!");
            yield break;
        }

        yield return InstallZipDirectly(coolerModsZip, "Cooler Mods", COOLERMODS_PATTERN, COOLERMODS_ID);
    }

    IEnumerator InstallZipDirectly(FileDownloader.FileData fileData, string label, string existingFilePattern, string modId)
    {
        string modsPath = Path.Combine(ucgPath, "mods");
        Directory.CreateDirectory(modsPath);

        string fileName = fileData.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = fileData.url.Substring(fileData.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string tempPath = Path.Combine(Application.persistentDataPath, fileName);

        yield return DownloadFile(fileData.url, tempPath);

        if (!File.Exists(tempPath))
        {
            UnityEngine.Debug.LogError(label + " download failed: file not found at " + tempPath);
            ShowInfo("ERROR: " + label + " download failed!");
            yield break;
        }

        DeleteMatchingFiles(modsPath, existingFilePattern);

        string target = Path.Combine(modsPath, fileName);

        try
        {
            File.Copy(tempPath, target, true);
            UnityEngine.Debug.Log(label + " copié vers: " + target);

            UpdateModsJson(modId);

            ShowInfo(label + " installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy " + label + ": " + e.Message);
            ShowInfo("ERROR: Failed to install " + label + "\n" + e.Message);
        }

        DeleteTempFile(tempPath);
    }

    void DeleteTempFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary file: " + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
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
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
        }
    }

    // =========================================================
    // MODS.JSON (AppData\Roaming\Uncanny Cat Golf\mods.json)
    // =========================================================

    void UpdateModsJson(string modId)
    {
        string path = Path.Combine(ucgPath, "mods.json");
        ModsJsonData data = null;

        if (File.Exists(path))
        {
            try
            {
                string existingJson = File.ReadAllText(path);
                data = JsonUtility.FromJson<ModsJsonData>(existingJson);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("mods.json illisible, il sera recréé: " + e.Message);
            }
        }

        if (data == null)
            data = new ModsJsonData();

        if (data.enabled == null)
            data.enabled = new List<string>();

        if (data.disabled == null)
            data.disabled = new List<string>();

        data.activated = true;

        data.disabled.RemoveAll(id => string.Equals(id, modId, System.StringComparison.OrdinalIgnoreCase));

        bool alreadyEnabled = data.enabled.Exists(id => string.Equals(id, modId, System.StringComparison.OrdinalIgnoreCase));
        if (!alreadyEnabled)
            data.enabled.Add(modId);

        EnsureApModIsFirst(data.enabled);

        try
        {
            Directory.CreateDirectory(ucgPath);
            string output = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, output);
            UnityEngine.Debug.Log("mods.json mis à jour: " + path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Impossible d'écrire mods.json: " + e.Message);
        }
    }

    void EnsureApModIsFirst(List<string> enabledList)
    {
        int index = enabledList.FindIndex(id => string.Equals(id, APMOD_ID, System.StringComparison.OrdinalIgnoreCase));

        if (index > 0)
        {
            string value = enabledList[index];
            enabledList.RemoveAt(index);
            enabledList.Insert(0, value);
        }
    }

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        ucgPath = GetUCGPath();

        if (string.IsNullOrEmpty(ucgPath))
            return;

        string modsPath = Path.Combine(ucgPath, "mods");

        if (!Directory.Exists(modsPath))
        {
            ShowInfo("Mods already removed!");
            return;
        }

        ShowInfo("Removing AP mods...");

        DeleteMatchingFiles(modsPath, APZIP_PATTERN);
        DeleteMatchingFiles(modsPath, COOLERMODS_PATTERN);

        ShowInfo("Revert complete!");
    }

    void DeleteMatchingFiles(string folder, string substring)
    {
        if (!Directory.Exists(folder))
            return;

        try
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file);

                if (name.IndexOf(substring, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        File.Delete(file);
                        UnityEngine.Debug.Log("Deleted: " + name);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning("Could not delete " + name + ": " + e.Message);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error while scanning mods folder: " + e.Message);
        }
    }

    // =========================================================
    // CONFIG / PATHS
    // =========================================================

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
            remoteConfig = JsonUtility.FromJson<UCGConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyUCGConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
        ucgPath = GetUCGPath();
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

    string GetUCGPath()
    {
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Uncanny Cat Golf");

        return path;
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
                                UnityEngine.Debug.Log("Found Archipelago custom_worlds at: " + path);
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