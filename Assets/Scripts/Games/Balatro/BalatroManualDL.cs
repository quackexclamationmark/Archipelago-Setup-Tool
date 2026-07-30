using Microsoft.Win32;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BalatroManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("BALATRO FILES")]
    public FileDownloader.FileData balatroapworld;
    public FileDownloader.FileData lovely;
    public FileDownloader.FileData steamodded;
    public FileDownloader.FileData balatroapMod;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "Balatro";

    [Header("FEATURE TOGGLES")]
    public Toggle installBalatroapworldToggle;
    public Toggle installLovelyToggle;
    public Toggle installSteamoddedToggle;
    public Toggle installBalatroapModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeBalatroapModsOnlyToggle;
    public Toggle fullCleanModsToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process balatroProcess;
    private string balatroPath;
    private string modsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private BalatroConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class BalatroConfig
    {
        public string balatroApworld;
        public string balatroLovely;
        public string balatroSteamodded;
        public string balatroAP;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        balatroPath = GetBalatroPath();
        modsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Balatro", "Mods");
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (removeBalatroapModsOnlyToggle != null)
            removeBalatroapModsOnlyToggle.isOn = true;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.isOn = false;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseBalatro();
    }

    void ApplyBalatroConfig()
    {
        if (remoteConfig == null)
            return;

        balatroapworld.url = remoteConfig.balatroApworld;
        lovely.url = remoteConfig.balatroLovely;
        steamodded.url = remoteConfig.balatroSteamodded;
        balatroapMod.url = remoteConfig.balatroAP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup all the files?", "Setup");
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

            case "ForceFullClean":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullCleanConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        balatroPath = GetBalatroPath();

        bool apworld = installBalatroapworldToggle == null || installBalatroapworldToggle.isOn;
        bool lovely = installLovelyToggle != null && installLovelyToggle.isOn;
        bool steamodded = installSteamoddedToggle != null && installSteamoddedToggle.isOn;
        bool apmod = installBalatroapModToggle != null && installBalatroapModToggle.isOn;

        bool needsGamePath = lovely || apmod || steamodded;

        if (needsGamePath && (string.IsNullOrEmpty(balatroPath) || !Directory.Exists(balatroPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        int count =
            (apworld ? 1 : 0) +
            (lovely ? 1 : 0) +
            (steamodded ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (lovely && count == 1)
        {
            StartCoroutine(LovelyOnlyFlow());
            return;
        }

        if (steamodded && count == 1)
        {
            StartCoroutine(SteamoddedOnlyFlow());
            return;
        }

        if (apmod && count == 1)
        {
            StartCoroutine(BalatroapModOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchBalatro();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        balatroPath = GetBalatroPath();
        modsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Balatro", "Mods");

        if (string.IsNullOrEmpty(balatroPath))
            return;

        bool removeAP = removeBalatroapModsOnlyToggle != null && removeBalatroapModsOnlyToggle.isOn;
        bool fullClean = fullCleanModsToggle != null && fullCleanModsToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            if (!Directory.Exists(modsPath))
                return;

            ShowInfo("Removing Balatro AP mods...");

            SafeDeleteDirectory(Path.Combine(modsPath, "BalatroAP"));
            SafeDeleteDirectory(Path.Combine(modsPath, "steamodded"));

            DeleteOldVersionFiles();

            ShowInfo("Balatro AP mods removed successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (fullClean && hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete all mods?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        ShowInfo("Removing mods...");

        SafeDeleteDirectory(Path.Combine(modsPath, "BalatroAP"));
        SafeDeleteDirectory(Path.Combine(modsPath, "steamodded"));

        SafeDeleteFile(Path.Combine(balatroPath, "version.dll"));

        DeleteOldVersionFiles();

        if (fullClean)
        {
            ShowInfo("Cleaning Mods directory...");

            SafeDeleteDirectory(modsPath);

            ShowInfo("Full clean completed!");
            return;
        }

        ShowInfo("Revert completed!");
    }

    bool HasOtherMods()
    {
        if (!Directory.Exists(modsPath))
            return false;

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("Balatro APMod Version") && name.EndsWith(".txt"))
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "BalatroAP" && name != "steamodded" && name != "lovely")
                return true;
        }

        return false;
    }


    IEnumerator InstallFlow()
    {
        if (installBalatroapworldToggle == null || installBalatroapworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installLovelyToggle != null && installLovelyToggle.isOn)
        {
            ShowInfo("Installing Lovely...");
            yield return InstallLovely();
        }

        if (installSteamoddedToggle != null && installSteamoddedToggle.isOn)
        {
            ShowInfo("Installing Steamodded...");
            yield return InstallSteamodded();
        }

        if (installBalatroapModToggle != null && installBalatroapModToggle.isOn)
        {
            ShowInfo("Installing BalatroAP Mod...");
            yield return InstallBalatroapMod();
        }

        CreateVersionFile(balatroapworld.url, lovely.url, steamodded.url, balatroapMod.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Balatro...");
            LaunchBalatro();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. Balatro APWorld URL: " + balatroapworld.url);

        if (string.IsNullOrEmpty(balatroapworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = balatroapworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = balatroapworld.url.Substring(balatroapworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + balatroapworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(balatroapworld.url, localPath);

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

    IEnumerator InstallLovely()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "LovelyTemp");

        yield return downloader.DownloadAndExtract(lovely, Application.persistentDataPath, extractPath);

        string versionDllPath = FindFile(extractPath, "version.dll");

        if (string.IsNullOrEmpty(versionDllPath))
        {
            UnityEngine.Debug.LogError("version.dll not found in Lovely package!");
            ShowInfo("ERROR: version.dll not found in Lovely package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        try
        {
            string targetPath = Path.Combine(balatroPath, "version.dll");
            File.Copy(versionDllPath, targetPath, true);
            UnityEngine.Debug.Log("version.dll copied to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy version.dll: " + e.Message);
            ShowInfo("ERROR: Failed to install Lovely\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallSteamodded()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SteamoddedTemp");

        yield return downloader.DownloadAndExtract(steamodded, Application.persistentDataPath, extractPath);

        string smodsFolder = FindSmodsFolder(extractPath);

        if (string.IsNullOrEmpty(smodsFolder))
        {
            UnityEngine.Debug.LogError("Steamodded folder not found in package!");
            ShowInfo("ERROR: Steamodded folder not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        Directory.CreateDirectory(modsPath);

        try
        {
            string targetPath = Path.Combine(modsPath, "steamodded");

            // Remove old steamodded if it exists
            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            MoveDirectory(smodsFolder, targetPath);
            UnityEngine.Debug.Log("Steamodded installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Steamodded: " + e.Message);
            ShowInfo("ERROR: Failed to install Steamodded\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBalatroapMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BalatroapModTemp");

        yield return downloader.DownloadAndExtract(balatroapMod, Application.persistentDataPath, extractPath);

        // Find the BalatroAP folder
        string balatroapFolder = FindBalatroapFolder(extractPath);

        if (string.IsNullOrEmpty(balatroapFolder))
        {
            UnityEngine.Debug.LogError("BalatroAP folder not found in package!");
            ShowInfo("ERROR: BalatroAP folder not found in package!");
            SafeDeleteDirectory(extractPath);
            yield break;
        }

        // Create mods directory if it doesn't exist
        Directory.CreateDirectory(modsPath);

        // Copy BalatroAP folder to Mods directory
        try
        {
            string targetPath = Path.Combine(modsPath, "BalatroAP");

            // Remove old BalatroAP if it exists
            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            MoveDirectory(balatroapFolder, targetPath);
            UnityEngine.Debug.Log("BalatroAP installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install BalatroAP: " + e.Message);
            ShowInfo("ERROR: Failed to install BalatroAP\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator LovelyOnlyFlow()
    {
        ShowInfo("Installing Lovely...");
        yield return InstallLovely();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Balatro...");
            LaunchBalatro();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");

        yield break;
    }

    IEnumerator SteamoddedOnlyFlow()
    {
        ShowInfo("Installing Steamodded...");
        yield return InstallSteamodded();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Balatro...");
            LaunchBalatro();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");

        yield break;
    }

    IEnumerator BalatroapModOnlyFlow()
    {
        balatroPath = GetBalatroPath();

        if (string.IsNullOrEmpty(balatroPath))
            yield break;

        ShowInfo("Installing BalatroAP Mod...");
        yield return InstallBalatroapMod();

        CreateVersionFile(balatroapworld.url, lovely.url, steamodded.url, balatroapMod.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchBalatro();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
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
            remoteConfig = JsonUtility.FromJson<BalatroConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyBalatroConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        balatroPath = GetBalatroPath();
    }

    void LaunchBalatro()
    {
        string exePath = Path.Combine(balatroPath, "Balatro.exe");

        if (File.Exists(exePath))
            balatroProcess = Process.Start(exePath);
    }

    void CloseBalatro()
    {
        try
        {
            if (balatroProcess != null && !balatroProcess.HasExited)
            {
                balatroProcess.Kill();
                balatroProcess.Dispose();
                balatroProcess = null;
            }
        }
        catch { }
    }

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

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(source.Length + 1);
            string dest = Path.Combine(target, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
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

    void OnFullCleanChanged(bool value)
    {
        if (removeBalatroapModsOnlyToggle != null)
        {
            removeBalatroapModsOnlyToggle.isOn = false;
            removeBalatroapModsOnlyToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    string FindSmodsFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name.StartsWith("smods-"))
                return dir;
        }

        return "";
    }

    string FindBalatroapFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "BalatroAP")
                return dir;
        }

        return "";
    }

    void CreateVersionFile(string apworldUrl, string lovelyUrl, string steamoddedUrl, string balatroapUrl)
    {
        try
        {
            string apworldVersion = ExtractVersionFromUrl(apworldUrl, @"/([^/]+)\.apworld");
            string lovelyVersion = ExtractVersionFromUrl(lovelyUrl, @"/([^/]+)\.zip");
            string steamoddedVersion = ExtractVersionFromUrl(steamoddedUrl, @"/([^/]+)\.zip");
            string balatroapVersion = ExtractVersionFromUrl(balatroapUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "Balatro APMod Version " + balatroapVersion + ".txt";
            string content = "Balatro Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Balatro-Setup-Tool\n";
            content += "\n";
            content += "=== BALATRO AP MOD ===\n";
            content += "Downloaded from: " + balatroapUrl + "\n";
            content += "Version: " + balatroapVersion + "\n";
            content += "\n";
            content += "=== BALATRO APWORLD ===\n";
            content += "Downloaded from: " + apworldUrl + "\n";
            content += "Name: " + apworldVersion + ".apworld\n";
            content += "\n";
            content += "=== LOVELY ===\n";
            content += "Downloaded from: " + lovelyUrl + "\n";
            content += "Version: " + lovelyVersion + "\n";
            content += "\n";
            content += "=== STEAMODDED ===\n";
            content += "Downloaded from: " + steamoddedUrl + "\n";
            content += "Version: " + steamoddedVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            // Create in mods directory
            Directory.CreateDirectory(modsPath);
            string modsVersionPath = Path.Combine(modsPath, versionFileName);
            File.WriteAllText(modsVersionPath, content);
            UnityEngine.Debug.Log("Version file created in mods: " + modsVersionPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating version file: " + e.Message);
        }
    }

    void DeleteOldVersionFiles()
    {
        try
        {
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Balatro APMod Version .+\.txt");

            if (Directory.Exists(modsPath))
            {
                string[] modsFiles = Directory.GetFiles(modsPath);
                foreach (string file in modsFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (pattern.IsMatch(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            UnityEngine.Debug.Log("Deleted old version file in mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in mods: " + e.Message);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error cleaning up old version files: " + e.Message);
        }
    }

    string ExtractVersionFromUrl(string url, string pattern)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(pattern);
        System.Text.RegularExpressions.Match match = regex.Match(url);

        if (match.Success)
            return match.Groups[1].Value;

        return "Unknown";
    }

    string GetBalatroPath()
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
                {
                    UnityEngine.Debug.Log("Found Game (Steam) at: " + path);
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
                            string path = Path.Combine(drive.Name, relativePath, steamGameFolderName);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Game (Steam, via remote config) at: " + path);
                                return path;
                            }
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
}
