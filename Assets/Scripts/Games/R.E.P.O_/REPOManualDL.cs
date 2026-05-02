using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;

public class REPOManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("REPO FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;

    [Header("MODS")]
    public FileDownloader.FileData menuLib;
    public FileDownloader.FileData repoLib;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installMenuLibToggle;
    public Toggle installRepoLibToggle;
    public Toggle installBepInExToggle;

    [Header("CONFIG TOGGLES")]
    public Toggle patchBepInExConfigToggle;
    public Toggle patchRepoLibConfigToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("ADVANCED OPTIONS")]
    public Toggle patchConfigsToggle;
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    private Process repoProcess;
    private string repoPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private RepoConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class RepoConfig
    {
        public string repoAP;
        public string repoMenuLib;
        public string repoRepoLib;
        public string repoBepInEx;
        public string repoApworld;
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

        if (patchConfigsToggle != null)
            patchConfigsToggle.isOn = false;

        if (fullCleanBepInExToggle != null)
            fullCleanBepInExToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseREPO();
    }

    void ApplyRepoConfig()
    {
        if (remoteConfig == null)
            return;

        apMod.url = remoteConfig.repoAP;
        menuLib.url = remoteConfig.repoMenuLib;
        repoLib.url = remoteConfig.repoRepoLib;
        bepInEx.url = remoteConfig.repoBepInEx;
        apworld.url = remoteConfig.repoApworld;
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

    void OnFullCleanChanged(bool value)
    {
        if (patchConfigsToggle == null)
            return;

        patchConfigsToggle.isOn = false;
        patchConfigsToggle.interactable = !value;
    }

    private void ExecuteSetup()
    {
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;
        bool menulib = installMenuLibToggle != null && installMenuLibToggle.isOn;
        bool repolib = installRepoLibToggle != null && installRepoLibToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (apmod ? 1 : 0) +
            (menulib ? 1 : 0) +
            (repolib ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        repoPath = GetRepoPath();
        if (string.IsNullOrEmpty(repoPath))
            return;

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        if (repolib && count == 1)
        {
            StartCoroutine(RepoLibOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        repoPath = GetRepoPath();

        if (string.IsNullOrEmpty(repoPath))
            return;

        string pluginsPath = Path.Combine(repoPath, "BepInEx", "plugins");
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

        SafeDeleteFile(Path.Combine(pluginsPath, "Archipelago.repobundle"));
        SafeDeleteFile(Path.Combine(pluginsPath, "MenuLib.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "RepoAP.dll"));
        SafeDeleteFile(Path.Combine(pluginsPath, "RepoLib.dll"));

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn)
        {
            SafeDeleteDirectory(Path.Combine(repoPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(repoPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(repoPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(repoPath, ".doorstop_version"));
            return;
        }

        if (!hasOtherMods)
        {
            SafeDeleteDirectory(Path.Combine(repoPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(repoPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(repoPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(repoPath, ".doorstop_version"));
        }
        else
        {
            if (patchConfigsToggle != null && patchConfigsToggle.isOn)
            {
                SetDefaultBepInExConfig();
                SetDefaultRepoLibConfig();
            }
        }
    }

    bool HasOtherMods(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return false;

        string[] files = Directory.GetFiles(pluginsPath);
        string[] dirs = Directory.GetDirectories(pluginsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name != "Archipelago.repobundle" &&
                name != "MenuLib.dll" &&
                name != "RepoAP.dll" &&
                name != "RepoLib.dll")
                return true;
        }

        if (dirs.Length > 0)
            return true;

        return false;
    }

    void SetDefaultBepInExConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        if (!File.Exists(cfgPath))
            return;

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("HideManagerGameObject"))
                lines[i] = "HideManagerGameObject = false";

        File.WriteAllLines(cfgPath, lines);
    }

    void SetDefaultRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        if (!File.Exists(cfgPath))
            return;

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("VanillaDeveloperMode"))
                lines[i] = "VanillaDeveloperMode = false";

            if (lines[i].Contains("DeveloperMode"))
                lines[i] = "DeveloperMode = false";
        }

        File.WriteAllLines(cfgPath, lines);
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installBepInExToggle != null && installBepInExToggle.isOn)
            yield return InstallBepInEx();

        if (installAPModToggle == null || installAPModToggle.isOn)
            yield return InstallAPMod();

        if (installMenuLibToggle == null || installMenuLibToggle.isOn)
            yield return InstallMod(menuLib, "MenuLib.dll");

        if (installRepoLibToggle == null || installRepoLibToggle.isOn)
            yield return InstallRepoLib();

        LaunchREPO();

        yield return WaitForConfigFiles();
        yield return WaitForBepInExConfigComplete();

        CloseREPO();

        yield return new WaitForSeconds(1f);

        if (patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn)
            yield return SetBepInExConfig();

        if (patchRepoLibConfigToggle != null && patchRepoLibConfigToggle.isOn)
            yield return SetRepoLibConfig();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchREPO();
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
            yield return null;

        string localPath = Path.Combine(Application.persistentDataPath, apworld.fileName);

        downloader.DownloadToFolder(apworld, Application.persistentDataPath);
        yield return new WaitForSeconds(1f);

        string target = Path.Combine(@"C:\ProgramData\Archipelago\custom_worlds", apworld.fileName);

        SafeDeleteFile(target);

        if (File.Exists(localPath))
            File.Copy(localPath, target, true);
    }

    IEnumerator InstallAPMod()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "APModTemp");

        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string plugins = Path.Combine(repoPath, "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);

        CopyIfExists(extractPath, "RepoAP.dll", plugins);
        CopyIfExists(extractPath, "Archipelago.repobundle", plugins);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallMod(FileDownloader.FileData mod, string dllName)
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, dllName + "_temp");

        yield return downloader.DownloadAndExtract(mod, Application.persistentDataPath, extractPath);

        string dllPath = FindFile(extractPath, dllName);
        string plugins = Path.Combine(repoPath, "BepInEx", "plugins");

        Directory.CreateDirectory(plugins);

        if (!string.IsNullOrEmpty(dllPath))
            File.Copy(dllPath, Path.Combine(plugins, dllName), true);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallRepoLib()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "RepoLibTemp");

        yield return downloader.DownloadAndExtract(repoLib, Application.persistentDataPath, extractPath);

        CopyIfExists(Path.Combine(extractPath, "plugins"), "RepoLib.dll",
            Path.Combine(repoPath, "BepInEx", "plugins"));

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        while (!configLoaded)
            yield return null;

        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, repoPath);

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        yield return InstallBepInEx();

        bool shouldPatchConfig = patchBepInExConfigToggle != null && patchBepInExConfigToggle.isOn;

        if (shouldPatchConfig)
        {
            LaunchREPO();
            yield return WaitForBepInExConfigComplete();
            CloseREPO();
            yield return new WaitForSeconds(1f);
            yield return SetBepInExConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchREPO();

        yield break;
    }

    IEnumerator WaitForBepInExConfigComplete()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfgPath))
            {
                string[] lines = File.ReadAllLines(cfgPath);

                foreach (string line in lines)
                {
                    if (line.Contains("HideManagerGameObject"))
                        yield break;
                }
            }

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator RepoLibOnlyFlow()
    {
        yield return InstallRepoLib();

        bool shouldPatchConfig = patchRepoLibConfigToggle != null && patchRepoLibConfigToggle.isOn;

        if (shouldPatchConfig)
        {
            LaunchREPO();
            yield return WaitForRepoLibConfig();
            CloseREPO();
            yield return new WaitForSeconds(1f);
            yield return SetRepoLibConfig();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchREPO();

        yield break;
    }

    IEnumerator WaitForRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfgPath))
            {
                string[] lines = File.ReadAllLines(cfgPath);

                foreach (string line in lines)
                {
                    if (line.Contains("DeveloperMode"))
                        yield break;
                }
            }

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator WaitForConfigFiles()
    {
        string bepConfig = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");
        string repoConfig = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(bepConfig) && File.Exists(repoConfig))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator SetBepInExConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "BepInEx.cfg");

        yield return new WaitUntil(() => File.Exists(cfgPath));

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("HideManagerGameObject"))
                lines[i] = "HideManagerGameObject = true";

        File.WriteAllLines(cfgPath, lines);
    }

    IEnumerator SetRepoLibConfig()
    {
        string cfgPath = Path.Combine(repoPath, "BepInEx", "config", "REPOLib.cfg");

        yield return new WaitUntil(() => File.Exists(cfgPath));

        string[] lines = File.ReadAllLines(cfgPath);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("VanillaDeveloperMode"))
                lines[i] = "VanillaDeveloperMode = true";
            else if (lines[i].StartsWith("DeveloperMode"))
                lines[i] = "DeveloperMode = true";
        }

        File.WriteAllLines(cfgPath, lines);
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

        remoteConfig = JsonUtility.FromJson<RepoConfig>(request.downloadHandler.text);

        ApplyRepoConfig();

        configLoaded = true;
    }

    void LaunchREPO()
    {
        string exePath = Path.Combine(repoPath, "REPO.exe");

        if (File.Exists(exePath))
            repoProcess = Process.Start(exePath);
    }

    void CloseREPO()
    {
        try
        {
            if (repoProcess != null && !repoProcess.HasExited)
            {
                repoProcess.Kill();
                repoProcess.Dispose();
                repoProcess = null;
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

    void CopyIfExists(string root, string file, string target)
    {
        string path = Path.Combine(root, file);

        if (File.Exists(path))
            File.Copy(path, Path.Combine(target, file), true);
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

    string FindFile(string root, string fileName)
    {
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            if (Path.GetFileName(file) == fileName)
                return file;

        return "";
    }

    string GetRepoPath()
    {
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "REPO"
        );

        return Directory.Exists(path) ? path : "";
    }
}