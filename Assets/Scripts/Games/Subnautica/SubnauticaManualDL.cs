using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.LightTransport;
using UnityEngine.UI;

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

    [Header("REVERT OPTIONS")]
    public Toggle fullCleanBepInExToggle;
    public Toggle removeAPModsOnlyToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

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

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.isOn = false;

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    // =========================================================
    // TOGGLE RULE
    // =========================================================

    void OnFullCleanChanged(bool value)
    {
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    // =========================================================

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

    // =========================================================
    // SETUP
    // =========================================================

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

    // =========================================================
    // REVERT
    // =========================================================

    private void ExecuteRevert()
    {
        subnauticaPath = GetSubnauticaPath();

        if (string.IsNullOrEmpty(subnauticaPath))
            return;

        string pluginsPath = Path.Combine(subnauticaPath, "BepInEx", "plugins");

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
        bool fullClean = fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn;

        if (!removeAP && !fullClean)
        {
            ShowInfo("Please select at least one revert option.");
            return;
        }

        if (removeAP)
        {
            CleanupProcesses();

            SafeDeleteDirectory(Path.Combine(pluginsPath, "SubnauticaAP"));
            SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

            return;
        }

        bool hasOtherMods = HasOtherMods(pluginsPath);

        if (hasOtherMods && !pendingFullCleanConfirmation)
        {
            pendingFullCleanConfirmation = true;

            ShowConfirmation(
                "Other mods were detected in BepInEx/plugins.\nDo you want to continue?",
                "ForceFullClean"
            );

            return;
        }

        pendingFullCleanConfirmation = false;

        CleanupProcesses();

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SubnauticaAP"));
        SafeDeleteDirectory(Path.Combine(pluginsPath, "Archipelago"));

        if (fullClean)
        {
            SafeDeleteDirectory(Path.Combine(subnauticaPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(subnauticaPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(subnauticaPath, ".doorstop_version"));
        }
    }

    // =========================================================
    // INFO
    // =========================================================

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

    // =========================================================
    // OTHER LOGIC (UNCHANGED)
    // =========================================================

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        foreach (string dir in Directory.GetDirectories(pluginsPath))
        {
            string name = Path.GetFileName(dir);

            if (name != "SubnauticaAP" && name != "Archipelago")
                return true;
        }

        foreach (string file in Directory.GetFiles(pluginsPath))
            return true;

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
            yield break;

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

        while ((!File.Exists(cfg) ||
                !Directory.Exists(pluginsDir) ||
                !Directory.Exists(patchersDir)) && timer < timeout)
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
            yield break;

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

        return Directory.Exists(path) ? path : "";
    }
}