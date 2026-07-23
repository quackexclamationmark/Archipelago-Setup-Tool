using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class SULFURManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SULFUR FILES")]
    public FileDownloader.FileData sulfurApworld;
    public FileDownloader.FileData sulfurBepInEx;
    public FileDownloader.FileData sulfurAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installSulfurApworldToggle;
    public Toggle installBepInExToggle;
    public Toggle installUILibToggle;
    public Toggle installAPModToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle launchAfterSetupToggle;

    [Header("REVERT OPTIONS")]
    public Toggle clearAPModsToggle;
    public Toggle fullClearBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process sulfurProcess;
    private string sulfurGamePath;
    private string sulfurBepInExPath;
    private string pendingAction;
    private bool pendingFullClearConfirmation = false;
    private SULFURConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class SULFURConfig
    {
        public string sulfurApworld;
        public string sulfurBepInEx;
        public string sulfurAP;
    }

    void Start()
    {
        sulfurGamePath = GetSulfurPath();
        if (!string.IsNullOrEmpty(sulfurGamePath))
            sulfurBepInExPath = Path.Combine(sulfurGamePath, "BepInEx");
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchAfterSetupToggle != null)
            launchAfterSetupToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (clearAPModsToggle != null)
            clearAPModsToggle.isOn = true;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.isOn = false;

        if (fullClearBepInExToggle != null)
            fullClearBepInExToggle.onValueChanged.AddListener(OnFullClearChanged);

        if (installUILibToggle != null)
            installUILibToggle.isOn = true;

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (installBepInExToggle != null)
            installBepInExToggle.isOn = true;
    }

    void CleanupProcesses()
    {
        CloseSulfur();
    }

    void ApplySulfurConfig()
    {
        if (remoteConfig == null)
            return;

        sulfurApworld.url = remoteConfig.sulfurApworld;
        sulfurBepInEx.url = remoteConfig.sulfurBepInEx;
        sulfurAP.url = remoteConfig.sulfurAP;
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

            case "ForceFullClear":
                ExecuteRevert();
                break;
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingFullClearConfirmation = false;
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        bool installApworld = installSulfurApworldToggle != null && installSulfurApworldToggle.isOn;
        bool installBepInEx = installBepInExToggle != null && installBepInExToggle.isOn;
        bool installUILib = installUILibToggle != null && installUILibToggle.isOn;
        bool installAPMod = installAPModToggle != null && installAPModToggle.isOn;

        if (!installApworld && !installBepInEx && !installUILib && !installAPMod)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        StartCoroutine(InstallFlow(installApworld, installBepInEx, installUILib, installAPMod));
    }

    private void ExecuteRevert()
    {
        sulfurGamePath = GetSulfurPath();

        bool clearAPMods = clearAPModsToggle != null && clearAPModsToggle.isOn;
        bool fullClear = fullClearBepInExToggle != null && fullClearBepInExToggle.isOn;

        if (!clearAPMods && !fullClear)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (clearAPMods && !fullClear)
        {
            CleanupProcesses();

            ShowInfo("Clearing AP mods...");

            if (!string.IsNullOrEmpty(sulfurGamePath))
            {
                sulfurBepInExPath = Path.Combine(sulfurGamePath, "BepInEx");
                SafeDeleteDirectory(Path.Combine(sulfurBepInExPath, "plugins", "Archipelago"));
                SafeDeleteDirectory(Path.Combine(sulfurBepInExPath, "plugins", "SULFURNativeUILib"));
            }

            ShowInfo("AP mods cleared successfully!");
            return;
        }

        bool hasOtherMods = HasOtherMods();

        if (fullClear && hasOtherMods && !pendingFullClearConfirmation)
        {
            pendingFullClearConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully clear BepInEx?",
                "ForceFullClear"
            );
            return;
        }

        pendingFullClearConfirmation = false;

        CleanupProcesses();

        ShowInfo("Clearing BepInEx...");

        if (!string.IsNullOrEmpty(sulfurGamePath))
        {
            sulfurBepInExPath = Path.Combine(sulfurGamePath, "BepInEx");

            // Delete BepInEx folder
            SafeDeleteDirectory(sulfurBepInExPath);

            // Delete BepInEx related files
            SafeDeleteFile(Path.Combine(sulfurGamePath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(sulfurGamePath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(sulfurGamePath, "changelog.txt"));
            SafeDeleteFile(Path.Combine(sulfurGamePath, ".doorstop_version"));

            ShowInfo("Full BepInEx clear completed!");
            return;
        }

        ShowInfo("Revert completed!");
    }

    bool HasOtherMods()
    {
        if (string.IsNullOrEmpty(sulfurGamePath))
            return false;

        string bepinexPath = Path.Combine(sulfurGamePath, "BepInEx");
        string pluginsPath = Path.Combine(bepinexPath, "plugins");

        if (!Directory.Exists(pluginsPath))
            return false;

        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "Archipelago" && name != "SULFURNativeUILib")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow(bool installApworld, bool installBepInEx, bool installUILib, bool installAPMod)
    {
        if (installApworld)
        {
            ShowInfo("Installing SULFUR APWorld...");
            yield return InstallSulfurAPWorld();
        }

        if (installBepInEx)
        {
            ShowInfo("Installing BepInEx...");
            yield return InstallBepInEx();

            // Install sulfurAP after BepInEx only if requested
            ShowInfo("Installing SULFUR AP...");
            yield return InstallSulfurAPToGameDirectory();
        }

        if (installUILib && !installBepInEx)
        {
            ShowInfo("Installing UILib...");
            yield return InstallUILib();
        }

        if (installAPMod && !installBepInEx)
        {
            ShowInfo("Installing SULFUR AP Mod...");
            yield return InstallSulfurAPMod();
        }

        if (launchAfterSetupToggle == null || launchAfterSetupToggle.isOn)
        {
            ShowInfo("Launching SULFUR...");
            LaunchSulfur();
            yield return new WaitForSeconds(2f);
        }

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator InstallSulfurAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. SULFUR APWorld URL: " + sulfurApworld.url);

        if (string.IsNullOrEmpty(sulfurApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = sulfurApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = sulfurApworld.url.Substring(sulfurApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + sulfurApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(sulfurApworld.url, localPath);

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

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(sulfurGamePath))
        {
            ShowInfo("ERROR: SULFUR game path not found!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(sulfurBepInEx, Application.persistentDataPath, extractPath);

        try
        {
            // Extract all content from the zip into the game directory
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(extractPath.Length + 1);
                string dest = Path.Combine(sulfurGamePath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                if (File.Exists(dest))
                    File.Delete(dest);

                File.Move(file, dest);
                UnityEngine.Debug.Log("Moved file to: " + dest);
            }

            UnityEngine.Debug.Log("BepInEx installed to game directory");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install BepInEx: " + e.Message);
            ShowInfo("ERROR: Failed to install BepInEx\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallSulfurAPToGameDirectory()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(sulfurGamePath))
        {
            ShowInfo("ERROR: SULFUR game path not found!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "SulfurAPGameTemp");

        yield return downloader.DownloadAndExtract(sulfurAP, Application.persistentDataPath, extractPath);

        try
        {
            // Extract all content from the zip into the game directory
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(extractPath.Length + 1);
                string dest = Path.Combine(sulfurGamePath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                if (File.Exists(dest))
                    File.Delete(dest);

                File.Move(file, dest);
                UnityEngine.Debug.Log("Moved file to: " + dest);
            }

            UnityEngine.Debug.Log("SULFUR AP content installed to game directory");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install SULFUR AP: " + e.Message);
            ShowInfo("ERROR: Failed to install SULFUR AP\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallUILib()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(sulfurGamePath))
        {
            ShowInfo("ERROR: SULFUR game path not found!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "UILibTemp");

        yield return downloader.DownloadAndExtract(sulfurAP, Application.persistentDataPath, extractPath);

        try
        {
            string uilLibFolder = FindUILibFolder(extractPath);

            if (string.IsNullOrEmpty(uilLibFolder))
            {
                UnityEngine.Debug.LogError("SULFURNativeUILib folder not found in package!");
                ShowInfo("ERROR: SULFURNativeUILib folder not found in package!");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            sulfurBepInExPath = Path.Combine(sulfurGamePath, "BepInEx");
            Directory.CreateDirectory(sulfurBepInExPath);

            string targetPath = Path.Combine(sulfurBepInExPath, "plugins", "SULFURNativeUILib");

            // Remove old UILib if it exists
            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            MoveDirectory(uilLibFolder, targetPath);
            UnityEngine.Debug.Log("UILib installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install UILib: " + e.Message);
            ShowInfo("ERROR: Failed to install UILib\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallSulfurAPMod()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(sulfurGamePath))
        {
            ShowInfo("ERROR: SULFUR game path not found!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "SulfurAPTemp");

        yield return downloader.DownloadAndExtract(sulfurAP, Application.persistentDataPath, extractPath);

        try
        {
            string archipelagoFolder = FindArchipelagoFolder(extractPath);

            if (string.IsNullOrEmpty(archipelagoFolder))
            {
                UnityEngine.Debug.LogError("Archipelago folder not found in package!");
                ShowInfo("ERROR: Archipelago folder not found in package!");
                SafeDeleteDirectory(extractPath);
                yield break;
            }

            sulfurBepInExPath = Path.Combine(sulfurGamePath, "BepInEx");
            Directory.CreateDirectory(sulfurBepInExPath);

            string targetPath = Path.Combine(sulfurBepInExPath, "plugins", "Archipelago");

            // Remove old Archipelago if it exists
            if (Directory.Exists(targetPath))
                SafeDeleteDirectory(targetPath);

            MoveDirectory(archipelagoFolder, targetPath);
            UnityEngine.Debug.Log("SULFUR AP Mod installed to: " + targetPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install SULFUR AP Mod: " + e.Message);
            ShowInfo("ERROR: Failed to install SULFUR AP Mod\n" + e.Message);
        }

        SafeDeleteDirectory(extractPath);
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
            remoteConfig = JsonUtility.FromJson<SULFURConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplySulfurConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchSulfur()
    {
        if (string.IsNullOrEmpty(sulfurGamePath))
        {
            ShowInfo("ERROR: SULFUR game path not found. Cannot launch.");
            return;
        }

        string exePath = Path.Combine(sulfurGamePath, "Sulfur.exe");

        if (File.Exists(exePath))
            sulfurProcess = Process.Start(exePath);
        else
            ShowInfo("ERROR: Sulfur.exe not found at: " + exePath);
    }

    void CloseSulfur()
    {
        try
        {
            if (sulfurProcess != null && !sulfurProcess.HasExited)
            {
                sulfurProcess.Kill();
                sulfurProcess.Dispose();
                sulfurProcess = null;
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

    void OnFullClearChanged(bool value)
    {
        if (clearAPModsToggle != null)
        {
            clearAPModsToggle.isOn = false;
            clearAPModsToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    string FindBepInExFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "BepInEx")
                return dir;
        }

        return "";
    }

    string FindUILibFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "SULFURNativeUILib")
                return dir;
        }

        return "";
    }

    string FindArchipelagoFolder(string root)
    {
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (name == "Archipelago")
                return dir;
        }

        return "";
    }

    string GetSulfurPath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "SULFUR"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "SULFUR"),
            @"D:\Steam\steamapps\common\SULFUR",
            @"D:\SteamLibrary\steamapps\common\SULFUR",
            @"D:\steamapps\common\SULFUR",
            @"E:\Steam\steamapps\common\SULFUR",
            @"E:\SteamLibrary\steamapps\common\SULFUR",
            @"E:\steamapps\common\SULFUR",
            @"E:\Program Files (x86)\steamapps\common\SULFUR",
            @"E:\Program Files\steamapps\common\SULFUR",
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

        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    // Search Steam\steamapps
                    string sulfurPath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "SULFUR");
                    if (Directory.Exists(sulfurPath))
                        return sulfurPath;

                    // Search SteamLibrary\steamapps
                    sulfurPath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "SULFUR");
                    if (Directory.Exists(sulfurPath))
                        return sulfurPath;

                    // Search steamapps at root
                    sulfurPath = Path.Combine(drive.Name, "steamapps", "common", "SULFUR");
                    if (Directory.Exists(sulfurPath))
                        return sulfurPath;

                    // Search Program Files (x86)\steamapps
                    sulfurPath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "SULFUR");
                    if (Directory.Exists(sulfurPath))
                        return sulfurPath;

                    // Search Program Files\steamapps
                    sulfurPath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "SULFUR");
                    if (Directory.Exists(sulfurPath))
                        return sulfurPath;
                }
                catch { }
            }
        }
        catch { }

        return "";
    }
}
