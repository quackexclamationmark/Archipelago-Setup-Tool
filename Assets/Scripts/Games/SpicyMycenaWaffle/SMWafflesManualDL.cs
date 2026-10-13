using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SMWafflesManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SMW WAFFLES FILES")]
    public FileDownloader.FileData spicymycenawafflesApworld;
    public FileDownloader.FileData spicymycenawafflesSNESEmu;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installSNESEmuToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle; // lance Snes9x à la fin du setup

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private const string ApworldDefaultName = "waffles.apworld";
    private const string EmuFolderName = "Snes9x";
    private const string EmuTempFolderName = "Snes9xTemp";
    private const string EmuSubFolderName = "snes9x-1.63-nwa-win32-x64";
    private const string EmuExeName = "snes9x-x64.exe";
    private const string EmuExeRelativePath = EmuSubFolderName + "\\" + EmuExeName; // Snes9x\snes9x-1.63-nwa-win32-x64\snes9x-x64.exe
    private const string ConfTempName = "snes9x.txt";
    private const string ConfName = "snes9x.conf";

    private Process emuProcess;
    private string pendingAction;
    private SMWafflesConfig remoteConfig;
    private bool configLoaded = false;
    private bool lastApWorldInstallSuccess = false;
    private bool lastEmuInstallSuccess = false;
    private bool lastDownloadSuccess = false;

    [System.Serializable]
    public class SMWafflesConfig
    {
        public string spicymycenawafflesApworld;
        public string spicymycenawafflesSNESEmu;
        public string[] apSearchPaths;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installAPWorldToggle != null)
            installAPWorldToggle.isOn = true;

        if (installSNESEmuToggle != null)
            installSNESEmuToggle.isOn = true;
    }

    void ApplyConfig()
    {
        if (remoteConfig == null)
            return;

        spicymycenawafflesApworld.url = remoteConfig.spicymycenawafflesApworld;
        spicymycenawafflesSNESEmu.url = remoteConfig.spicymycenawafflesSNESEmu;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
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

        switch (pendingAction)
        {
            case "Setup":
                ExecuteSetup();
                break;
        }
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // =====================================================================
    // SETUP
    // =====================================================================

    private void ExecuteSetup()
    {
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool emu = installSNESEmuToggle != null && installSNESEmuToggle.isOn;

        if (!apworld && !emu)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return null;

        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool emu = installSNESEmuToggle != null && installSNESEmuToggle.isOn;

        CleanupProcesses();

        // 1) SNES emulator (zip -> Documents\Snes9x + snes9x.conf)
        if (emu)
        {
            ShowInfo("Installing Snes9x...");
            yield return InstallEmulator();

            if (!lastEmuInstallSuccess)
                yield break;
        }

        // 2) APWorld
        if (apworld)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();

            if (!lastApWorldInstallSuccess)
                yield break;
        }

        // 3) Launch
        if (secondLaunchToggle != null && secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Snes9x...");
            yield return new WaitForSeconds(1f);

            if (!LaunchEmulator())
            {
                ShowInfo("Installation complete, but Snes9x could not be launched.");
                yield break;
            }
        }

        ShowInfo("Installation complete!");
    }

    // =====================================================================
    // EMULATOR
    // =====================================================================

    string GetDocumentsPath()
    {
        return System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
    }

    string GetEmuRootPath()
    {
        return Path.Combine(GetDocumentsPath(), EmuFolderName);
    }

    string GetEmuExePath()
    {
        return Path.Combine(GetEmuRootPath(), EmuExeRelativePath);
    }

    IEnumerator InstallEmulator()
    {
        lastEmuInstallSuccess = false;

        if (string.IsNullOrEmpty(spicymycenawafflesSNESEmu.url))
        {
            ShowInfo("ERROR: Emulator URL is empty!");
            UnityEngine.Debug.LogError("spicymycenawafflesSNESEmu URL not set!");
            yield break;
        }

        string documents = GetDocumentsPath();
        string tempExtract = Path.Combine(documents, EmuTempFolderName);
        string emuRoot = GetEmuRootPath();

        SafeDeleteDirectory(tempExtract);

        // Download + extract into Documents\Snes9xTemp
        yield return downloader.DownloadAndExtract(spicymycenawafflesSNESEmu, Application.persistentDataPath, tempExtract);

        if (!Directory.Exists(tempExtract) || Directory.GetFileSystemEntries(tempExtract).Length == 0)
        {
            ShowInfo("ERROR: Emulator download failed!");
            SafeDeleteDirectory(tempExtract);
            yield break;
        }

        // Rename the extracted folder to "Snes9x" (merge if it already exists so ROMs/saves are kept)
        try
        {
            if (!Directory.Exists(emuRoot))
            {
                Directory.Move(tempExtract, emuRoot);
                UnityEngine.Debug.Log("Renamed " + tempExtract + " -> " + emuRoot);
            }
            else
            {
                MoveDirectory(tempExtract, emuRoot);
                UnityEngine.Debug.Log("Merged " + tempExtract + " into " + emuRoot);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to rename emulator folder: " + e.Message);
            ShowInfo("ERROR: Failed to install Snes9x.\n" + e.Message);
            SafeDeleteDirectory(tempExtract);
            yield break;
        }

        // Locate the sub-folder (snes9x-1.63-nwa-win32-x64), fallback: search the exe
        string subFolder = Path.Combine(emuRoot, EmuSubFolderName);

        if (!Directory.Exists(subFolder))
        {
            string[] found = Directory.GetFiles(emuRoot, EmuExeName, SearchOption.AllDirectories);

            if (found.Length == 0)
            {
                ShowInfo("ERROR: " + EmuExeName + " not found after extraction.");
                yield break;
            }

            subFolder = Path.GetDirectoryName(found[0]);
        }

        // Create the text file with the config content, then rename it to snes9x.conf
        try
        {
            WriteSnes9xConf(subFolder);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to write snes9x.conf: " + e.Message);
            ShowInfo("ERROR: Failed to write snes9x.conf.\n" + e.Message);
            yield break;
        }

        lastEmuInstallSuccess = true;
        UnityEngine.Debug.Log("Snes9x installed successfully in: " + emuRoot);
    }

    void WriteSnes9xConf(string folder)
    {
        string txtPath = Path.Combine(folder, ConfTempName);
        string confPath = Path.Combine(folder, ConfName);

        string content = SMWafflesSnes9xConf.Content
            .Replace("\r\n", "\n")
            .Replace("\n", "\r\n");

        File.WriteAllText(txtPath, content);

        if (File.Exists(confPath))
        {
            File.SetAttributes(confPath, FileAttributes.Normal);
            File.Delete(confPath);
        }

        File.Move(txtPath, confPath);
        UnityEngine.Debug.Log("snes9x.conf created: " + confPath);
    }

    // =====================================================================
    // UI
    // =====================================================================

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

    public void QuitLauncher()
    {
        Application.Quit();
    }

    // =====================================================================
    // APWORLD
    // =====================================================================

    string GetApworldFileName()
    {
        string fileName = spicymycenawafflesApworld.fileName;

        if (string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(spicymycenawafflesApworld.url))
            fileName = GetFileNameFromUrl(spicymycenawafflesApworld.url, ApworldDefaultName);

        if (string.IsNullOrEmpty(fileName))
            fileName = ApworldDefaultName;

        return fileName;
    }

    string GetFileNameFromUrl(string url, string fallback)
    {
        try
        {
            string name = url.Substring(url.LastIndexOf('/') + 1);

            if (name.Contains("?"))
                name = name.Substring(0, name.IndexOf("?"));

            if (!string.IsNullOrEmpty(name))
                return name;
        }
        catch { }

        return fallback;
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + spicymycenawafflesApworld.url);

        if (string.IsNullOrEmpty(spicymycenawafflesApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = GetApworldFileName();
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + spicymycenawafflesApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(spicymycenawafflesApworld.url, localPath);

        if (!lastDownloadSuccess || !File.Exists(localPath))
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

    IEnumerator DownloadFile(string url, string savePath)
    {
        lastDownloadSuccess = false;

        UnityEngine.Debug.Log("Starting download from: " + url);

        try
        {
            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(savePath))
                File.Delete(savePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not prepare download path: " + e.Message);
        }

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
                lastDownloadSuccess = true;
                UnityEngine.Debug.Log("Download complete! File size: " + new System.IO.FileInfo(savePath).Length + " bytes");
            }
        }
    }

    // =====================================================================
    // PROCESSES
    // =====================================================================

    bool LaunchEmulator()
    {
        string exePath = GetEmuExePath();

        if (!File.Exists(exePath))
        {
            // Fallback: exe elsewhere in the Snes9x folder
            string root = GetEmuRootPath();
            string[] found = Directory.Exists(root)
                ? Directory.GetFiles(root, EmuExeName, SearchOption.AllDirectories)
                : new string[0];

            if (found.Length == 0)
            {
                UnityEngine.Debug.LogWarning("Snes9x exe not found: " + exePath);
                return false;
            }

            exePath = found[0];
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(exePath);
            startInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
            startInfo.UseShellExecute = true;
            emuProcess = Process.Start(startInfo);
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to launch Snes9x: " + e.Message);
            return false;
        }
    }

    void CloseEmulator()
    {
        try
        {
            if (emuProcess != null && !emuProcess.HasExited)
            {
                emuProcess.Kill();
                emuProcess.Dispose();
                emuProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseEmulator();

        try
        {
            foreach (Process p in Process.GetProcessesByName("snes9x-x64"))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
    }

    // =====================================================================
    // FILE HELPERS
    // =====================================================================

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

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }

        try
        {
            Directory.Delete(source, true);
        }
        catch { }
    }

    // =====================================================================
    // REMOTE CONFIG
    // =====================================================================

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
            remoteConfig = JsonUtility.FromJson<SMWafflesConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
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