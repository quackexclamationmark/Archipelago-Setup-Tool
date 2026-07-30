using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public class DS2ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DS2 FILES")]
    public FileDownloader.FileData ds2Apworld;
    public FileDownloader.FileData ds2ScholarAP;
    public FileDownloader.FileData ds2VanillaAP;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderNameVanilla = "DARK SOULS II";
    public string steamGameFolderNameScholar = "Dark Souls II Scholar of the First Sin";

    [Header("GAME VERSION SELECTION")]
    public Button ds2ScholarButton;
    public Button ds2VanillaButton;
    public TextMeshProUGUI versionStatusText;

    [Header("FEATURE TOGGLES")]
    public Toggle installDS2ApworldToggle;
    public Toggle installDS2APToggle;

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

    private Process ds2Process;
    private string ds2Path;
    private string pendingAction;
    private bool isScholarVersion = true;
    private Ds2Config remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class Ds2Config
    {
        public string ds2scholarAP;
        public string ds2AP;
        public string ds2Apworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (secondLaunchToggle != null) secondLaunchToggle.isOn = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancel);

        if (ds2ScholarButton != null)
        {
            ds2ScholarButton.onClick.AddListener(SelectScholarVersion);
        }
        if (ds2VanillaButton != null)
        {
            ds2VanillaButton.onClick.AddListener(SelectVanillaVersion);
        }

        SelectScholarVersion();
    }

    void SelectScholarVersion()
    {
        isScholarVersion = true;
        ds2Path = GetDS2Path(isScholarVersion);
        UpdateVersionStatusText();
        UnityEngine.Debug.Log("Selected: Dark Souls II Scholar of the First Sin - Path: " + ds2Path);
    }

    void SelectVanillaVersion()
    {
        isScholarVersion = false;
        ds2Path = GetDS2Path(isScholarVersion);
        UpdateVersionStatusText();
        UnityEngine.Debug.Log("Selected: DARK SOULS II (Vanilla) - Path: " + ds2Path);
    }

    void UpdateVersionStatusText()
    {
        if (versionStatusText != null)
        {
            string versionName = isScholarVersion ?
                "Dark Souls II Scholar of the First Sin" :
                "DARK SOULS II (Vanilla)";
            string status = string.IsNullOrEmpty(ds2Path) ? "Not Found" : "Found";
            versionStatusText.text = $"Selected: {versionName} \n {status}";
        }
    }

    void ApplyDs2Config()
    {
        if (remoteConfig == null) return;
        ds2ScholarAP.url = remoteConfig.ds2scholarAP;
        ds2VanillaAP.url = remoteConfig.ds2AP;
        ds2Apworld.url = remoteConfig.ds2Apworld;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
    public void RevertAll() => ShowConfirmation("Are you sure you want to revert?", "Revert");

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

        switch (pendingAction)
        {
            case "Setup": ExecuteSetup(); break;
            case "Revert": ExecuteRevert(); break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        ds2Path = GetDS2Path(isScholarVersion);

        bool apworld = installDS2ApworldToggle == null || installDS2ApworldToggle.isOn;
        bool ds2ap = installDS2APToggle != null && installDS2APToggle.isOn;
        bool needsGamePath = ds2ap || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(ds2Path))
        {
            string versionName = isScholarVersion ? "Dark Souls II Scholar of the First Sin" : "Dark Souls II";
            ShowInfo($"{versionName} path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ds2ap ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (ds2ap && count == 1) { StartCoroutine(DS2APOnlyFlow()); return; }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        ds2Path = GetDS2Path(isScholarVersion);

        if (string.IsNullOrEmpty(ds2Path))
        {
            ShowInfo("Dark Souls II path not found.");
            return;
        }

        CleanupProcesses();

        ShowInfo("Removing DS2 Archipelago files...");

        // Remove dinput8.dll
        SafeDeleteFile(Path.Combine(ds2Path, "dinput8.dll"));

        // Remove archipelago folder if it exists
        SafeDeleteDirectory(Path.Combine(ds2Path, "archipelago"));

        // Remove version files
        DeleteVersionFiles();

        ShowInfo("Revert completed!");
    }

    void DeleteVersionFiles()
    {
        try
        {
            foreach (string file in Directory.GetFiles(ds2Path))
            {
                string fn = Path.GetFileName(file);
                if (fn.StartsWith("DS2 APMod Version") && fn.EndsWith(".txt"))
                    SafeDeleteFile(file);
            }
        }
        catch { }
    }

    IEnumerator InstallFlow()
    {
        // attendre la config chargée
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        bool installApworld = installDS2ApworldToggle == null || installDS2ApworldToggle.isOn;
        bool installDs2ap = installDS2APToggle != null && installDS2APToggle.isOn;

        if (installApworld)
        {
            ShowInfo("Installing AP World...");
            yield return InstallAPWorld();
        }

        if (installDs2ap)
        {
            ShowInfo("Installing DS2 AP Client...");
            yield return InstallDS2AP();
        }

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;
        CreateVersionFile(apUrl, ds2Apworld.url);

        ShowInfo("Installation complete!");
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            yield return new WaitForSeconds(2f);
            LaunchDS2(false);
        }
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallAPWorld();

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;
        CreateVersionFile(apUrl, ds2Apworld.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ds2Path = GetDS2Path(isScholarVersion);

            if (!string.IsNullOrEmpty(ds2Path))
            {
                LaunchDS2(false);
                yield return new WaitForSeconds(2f);
            }
            else
            {
                string versionName = isScholarVersion ? "Dark Souls II Scholar of the First Sin" : "Dark Souls II";
                UnityEngine.Debug.LogWarning(versionName + " not found, skipping launch.");
            }
        }

        ShowInfo("AP World installed successfully!");
    }

    IEnumerator DS2APOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing DS2 AP Client...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallDS2AP();

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;
        CreateVersionFile(apUrl, ds2Apworld.url);
        ShowInfo("DS2 AP Client installed successfully!");
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(ds2Apworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = ds2Apworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ds2Apworld.url.Substring(ds2Apworld.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?")) fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + ds2Apworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(ds2Apworld.url, localPath);

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

        if (File.Exists(target))
        {
            try { File.Delete(target); } catch { }
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
            yield break;
        }

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

    IEnumerator InstallDS2AP()
    {
        while (!configLoaded)
            yield return null;

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;

        if (string.IsNullOrEmpty(apUrl))
        {
            ShowInfo("ERROR: DS2 AP URL is empty!");
            UnityEngine.Debug.LogError("DS2 AP URL not set!");
            yield break;
        }

        string tempFileName = isScholarVersion ? "dinput8_sotfs.dll" : "dinput8_vanilla.dll";
        string localPath = Path.Combine(Application.persistentDataPath, tempFileName);

        UnityEngine.Debug.Log("Downloading DS2 AP from: " + apUrl);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(apUrl, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: DS2 AP download failed!");
            yield break;
        }

        string targetPath = Path.Combine(ds2Path, "dinput8.dll");

        if (File.Exists(targetPath))
        {
            try { File.Delete(targetPath); } catch { }
        }

        try
        {
            File.Copy(localPath, targetPath, true);
            UnityEngine.Debug.Log("DS2 AP file copied and renamed to: " + targetPath);
            ShowInfo("DS2 AP Client installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install DS2 AP: " + e.Message);
            ShowInfo("ERROR: Failed to install DS2 AP\n" + e.Message);
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
            remoteConfig = JsonUtility.FromJson<Ds2Config>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDs2Config();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }
        
        configLoaded = true;

        ds2Path = GetDS2Path(isScholarVersion);
        UpdateVersionStatusText();
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
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! " + savePath);
            }
        }
    }

    void LaunchDS2(bool asHelper = false)
    {
        string exePath = Path.Combine(ds2Path, "DarkSoulsII.exe");
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                ds2Process = Process.Start(psi);
                UnityEngine.Debug.Log("Dark Souls II launched.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Launch error: " + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("Dark Souls II executable not found: " + exePath);
        }
    }

    void CleanupProcesses()
    {
        try
        {
            if (ds2Process != null && !ds2Process.HasExited)
            {
                ds2Process.Dispose();
                ds2Process = null;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("CleanupProcesses error: " + ex.Message);
        }
    }

    void SafeDeleteFile(string path) => StartCoroutine(DeleteFileForce(path));

    IEnumerator DeleteFileForce(string path)
    {
        float timer = 0f;
        while (File.Exists(path) && timer < 6f)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                if (!File.Exists(path)) yield break;
            }
            catch { }
            timer += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoPanel == null || infoText == null) return;
        infoText.text = message;
        infoPanel.SetActive(true);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }

    void CreateVersionFile(string ds2apUrl, string apworldUrl)
    {
        try
        {
            string versionType = isScholarVersion ? "Scholar" : "Vanilla";
            string ds2apVersion = ExtractVersionFromUrl(ds2apUrl, @"/([^/]+)\.dll");
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");

            string versionFileName = $"DS2 APMod Version {ds2apVersion} ({versionType}).txt";
            string content = "Dark Souls II Archipelago Setup Tool by quack!\n\n";
            content += "=== DS2 AP CLIENT ===\nDownloaded from: " + ds2apUrl + "\nVersion: " + ds2apVersion + "\nVariant: " + versionType + "\n\n";
            content += "=== APWORLD ===\nDownloaded from: " + apworldUrl + "\nName: " + apworldVersion + ".apworld\n\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            string rootVersionPath = Path.Combine(ds2Path, versionFileName);
            File.WriteAllText(rootVersionPath, content);
            UnityEngine.Debug.Log("Version file created: " + rootVersionPath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        if (string.IsNullOrEmpty(url)) return "Unknown";
        Match match = Regex.Match(url, pattern);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    string GetDS2Path(bool isScholar)
    {
        string gameDir = isScholar ? steamGameFolderNameScholar : steamGameFolderNameVanilla;

        string[] baseDirs = new string[]
        {
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common"),
        };

        foreach (string baseDir in baseDirs)
        {
            try
            {
                string path = Path.Combine(baseDir, gameDir);
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Dark Souls II (Steam) at: " + path);
                    return path;
                }
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
                            string path = Path.Combine(drive.Name, relativePath, gameDir);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Dark Souls II (Steam, via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning($"Dark Souls II ({(isScholar ? "Scholar" : "Vanilla")}) not found.");
        return "";
    }
}