using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class SubnauticaManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("SUBNAUTICA FILES")]
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;
    public Toggle nonVRToggle;

    [Header("ADVANCED OPTIONS")]
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    private Process subnauticaProcess;
    private string subnauticaPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private SubnauticaConfig remoteConfig;
    private bool configLoaded = false;
    
    [System.Serializable]
    public class SubnauticaConfig
    {
        public string subnauticaAP;
        public string subnauticaBepInEx;
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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = true;
    }

    void ApplySubnauticaConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.subnauticaAP;
        bepInEx.url = remoteConfig.subnauticaBepInEx;
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
        subnauticaPath = GetSubnauticaPath();
        if (string.IsNullOrEmpty(subnauticaPath))
            return;

        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool mod = installAPModToggle == null || installAPModToggle.isOn;

        int count = (bep ? 1 : 0) + (mod ? 1 : 0);

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (mod && count == 1)
        {
            StartCoroutine(APModOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        subnauticaPath = GetSubnauticaPath();

        if (string.IsNullOrEmpty(subnauticaPath))
            return;

        string pluginsPath = Path.Combine(subnauticaPath, "BepInEx", "plugins");
        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (fullCleanBepInExToggle != null &&
            fullCleanBepInExToggle.isOn &&
            hasOtherMods &&
            !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected.\nDo you REALLY want to fully delete BepInEx?",
                "ForceFullClean"
            );
            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SubnauticaAP"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn)
        {
            SafeDeleteDirectory(Path.Combine(subnauticaPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(subnauticaPath, ".doorstop_version"));
            return;
        }

        if (!hasOtherMods)
        {
            SafeDeleteDirectory(Path.Combine(subnauticaPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(subnauticaPath, ".doorstop_version"));
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "SubnauticaAP" && name != "Archipelago")
                return true;
        }

        foreach (string file in files)
        {
            return true;
        }

        return false;
    }

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        bool secondLaunch = secondLaunchToggle == null || secondLaunchToggle.isOn;

        if (installBepInExToggle == null || installBepInExToggle.isOn)
            yield return InstallBepInEx();

        if (installAPModToggle == null || installAPModToggle.isOn)
            yield return InstallAPMod();

        LaunchSubnautica();

        yield return WaitForConfigFiles();

        CloseSubnautica();

        yield return new WaitForSeconds(1f);

        if (secondLaunch)
            LaunchSubnautica();
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, subnauticaPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "SubnauticaAPTemp");

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string pluginsPath = Path.Combine(subnauticaPath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        string archipelagoPath = Path.Combine(extractPath, "BepInEx", "plugins", "Archipelago");

        if (!Directory.Exists(archipelagoPath))
        {
            UnityEngine.Debug.LogError("Archipelago folder not found in zip");
            yield break;
        }

        string targetPath = Path.Combine(pluginsPath, "Archipelago");

        if (Directory.Exists(targetPath))
            Directory.Delete(targetPath, true);

        CopyDirectory(archipelagoPath, targetPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator BepInExOnlyFlow()
    {
        yield return InstallBepInEx();

        LaunchSubnautica();
        yield return WaitForConfigFiles();
        CloseSubnautica();
    }

    IEnumerator APModOnlyFlow()
    {
        yield return InstallAPMod();
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(subnauticaPath, "BepInEx", "config", "BepInEx.cfg");
        string pluginsDir = Path.Combine(subnauticaPath, "BepInEx", "plugins");
        string patchersDir = Path.Combine(subnauticaPath, "BepInEx", "patchers");

        float timer = 0f;
        float timeout = 30f;

        while (
            (!File.Exists(cfg) ||
            !Directory.Exists(pluginsDir) ||
            !Directory.Exists(patchersDir))
            && timer < timeout
        )
        {
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Config load failed: " + request.error);
            yield break;
        }

        remoteConfig = JsonUtility.FromJson<SubnauticaConfig>(request.downloadHandler.text);

        ApplySubnauticaConfig();

        configLoaded = true;
    }

    void LaunchSubnautica()
    {
        string exePath = Path.Combine(subnauticaPath, "Subnautica.exe");

        if (!File.Exists(exePath))
            return;

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = exePath;
        startInfo.WorkingDirectory = subnauticaPath;

        if (nonVRToggle != null && nonVRToggle.isOn)
            startInfo.Arguments = "-vrmode none";

        subnauticaProcess = Process.Start(startInfo);
    }

    void CloseSubnautica()
    {
        try
        {
            if (subnauticaProcess != null && !subnauticaProcess.HasExited)
            {
                subnauticaProcess.Kill();
                subnauticaProcess.Dispose();
                subnauticaProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        CloseSubnautica();
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

        if (File.Exists(path))
            UnityEngine.Debug.LogError("FAILED TO DELETE: " + path);
    }

    void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);
            File.Copy(file, destination, true);
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

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = file.Replace(source, target);

            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(file, destination);
        }
    }

    string GetSubnauticaPath()
    {
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "Subnautica"
        );

        if (Directory.Exists(path))
            return path;

        return Directory.Exists(path) ? path : "";
    }
}