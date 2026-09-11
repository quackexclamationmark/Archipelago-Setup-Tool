using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SWHManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SWH FILES")]
    public FileDownloader.FileData swhApworld;
    public FileDownloader.FileData swhBepInEx;
    public FileDownloader.FileData swhAP;

    [Header("GAME FOLDER NAME")]
    public string steamGameFolderName = "Sayonara Wild Hearts";

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installBepInExToggle;
    public Toggle installApToggle;

    [Header("REVERT OPTIONS")]
    public Toggle fullClearBepInExToggle;
    public Toggle clearApModToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process gameProcess;
    private string gamePath;
    private string pendingAction;
    private SWHConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class SWHConfig
    {
        public string swhApworld;
        public string swhBepInEx;
        public string swhAP;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
    }

    void Start()
    {
        gamePath = GetGamePath();
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

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.isOn = false;

        if (clearApModToggle != null)
            clearApModToggle.isOn = true;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);
    }

    void OnFullClearChanged(bool value)
    {
        if (clearApModToggle != null)
        {
            if (value)
                clearApModToggle.isOn = false;

            clearApModToggle.interactable = !value;
        }
    }

    void ApplySWHConfig()
    {
        if (remoteConfig == null)
            return;

        swhApworld.url = remoteConfig.swhApworld;
        swhBepInEx.url = remoteConfig.swhBepInEx;
        swhAP.url = remoteConfig.swhAP;
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

    // ---------------- SETUP ----------------

    private void ExecuteSetup()
    {
        gamePath = GetGamePath();

        bool apworld = installApworldToggle == null || installApworldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool ap = installApToggle != null && installApToggle.isOn;

        bool needsGamePath = bep || ap;

        if (needsGamePath && (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (bep ? 1 : 0) + (ap ? 1 : 0);

        if (!apworld && !bep && !ap)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow(apworld, bep, ap));
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

    IEnumerator InstallFlow(bool doApworld, bool doBep, bool doAp)
    {
        yield return new WaitUntil(() => configLoaded);

        if (doApworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (doBep)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();
        }

        if (doAp)
        {
            ShowInfo("Installing AP Mod...");
            yield return InstallApMod();
        }

        if (doAp)
        {
            ShowInfo("Verifying BepInEx installation...");
            yield return WaitForBepInExFiles();

            ShowInfo("Launching game to generate APInfo.json...");
            LaunchGame();

            yield return WaitForAPInfoJson();

            ShowInfo("Closing game...");
            CloseGame();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + swhApworld.url);

        if (string.IsNullOrEmpty(swhApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = swhApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = swhApworld.url.Substring(swhApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + swhApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(swhApworld.url, localPath);

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

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SWH_BepInExTemp");

        yield return downloader.DownloadAndExtract(swhBepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, gamePath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallApMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SWH_APModTemp");

        yield return downloader.DownloadAndExtract(swhAP, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        MoveDirectory(extractPath, pluginsPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                UnityEngine.Debug.LogError("Download error: " + request.error);
        }
    }

    IEnumerator WaitForBepInExFiles()
    {
        string[] files = new string[]
        {
            Path.Combine(gamePath, ".doorstop_version"),
            Path.Combine(gamePath, "changelog.txt"),
            Path.Combine(gamePath, "doorstop_config.ini"),
            Path.Combine(gamePath, "winhttp.dll"),
        };

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            bool allExist = true;

            foreach (string f in files)
            {
                if (!File.Exists(f))
                {
                    allExist = false;
                    break;
                }
            }

            if (allExist)
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        UnityEngine.Debug.LogWarning("Timed out waiting for BepInEx files.");
    }

    IEnumerator WaitForAPInfoJson()
    {
        string apInfoPath = Path.Combine(gamePath, "APInfo.json");

        float timeout = 120f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(apInfoPath))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        UnityEngine.Debug.LogWarning("Timed out waiting for APInfo.json.");
    }

    // ---------------- REVERT ----------------

    private void ExecuteRevert()
    {
        gamePath = GetGamePath();

        if (string.IsNullOrEmpty(gamePath))
            return;

        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;
        bool clearApMod = clearApModToggle != null && clearApModToggle.isOn;

        if (!fullClear && !clearApMod)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        CloseGame();

        if (fullClear)
        {
            ShowInfo("Cleaning BepInEx...");

            SafeDeleteFile(Path.Combine(gamePath, "APInfo.json"));
            SafeDeleteFile(Path.Combine(gamePath, ".doorstop_version"));
            SafeDeleteFile(Path.Combine(gamePath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(gamePath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(gamePath, "winhttp.dll"));
            SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx"));

            ShowInfo("Full clean completed!");
            return;
        }

        if (clearApMod)
        {
            ShowInfo("Removing AP mod...");

            SafeDeleteFile(Path.Combine(gamePath, "APInfo.json"));
            SafeDeleteDirectory(Path.Combine(gamePath, "BepInEx", "plugins", "SayonaraWildHeartsRandomizer"));

            ShowInfo("AP mod removed successfully!");
        }
    }

    // ---------------- CONFIG ----------------

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
            remoteConfig = JsonUtility.FromJson<SWHConfig>(request.downloadHandler.text);
            ApplySWHConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        gamePath = GetGamePath();
    }

    // ---------------- PROCESS HELPERS ----------------

    void LaunchGame()
    {
        string exePath = Path.Combine(gamePath, "Sayonara Wild Hearts.exe");

        if (File.Exists(exePath))
            gameProcess = Process.Start(exePath);
        else
            UnityEngine.Debug.LogWarning("Game executable not found at: " + exePath);
    }

    void CloseGame()
    {
        try
        {
            if (gameProcess != null && !gameProcess.HasExited)
            {
                gameProcess.Kill();
                gameProcess.Dispose();
                gameProcess = null;
            }
        }
        catch { }
    }

    // ---------------- FILE HELPERS ----------------

    void SafeDeleteFile(string path)
    {
        StartCoroutine(DeleteFileForce(path));
    }

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;

        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);

                if (!File.Exists(path))
                    yield break;
            }
            catch { }

            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
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

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }
    }

    // ---------------- UI HELPERS ----------------

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

    // ---------------- PATH RESOLUTION ----------------

    string GetGamePath()
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
                    return path;
            }
            catch { }
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
                                return path;
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