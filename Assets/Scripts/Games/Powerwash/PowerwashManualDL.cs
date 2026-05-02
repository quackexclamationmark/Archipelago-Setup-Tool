using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System;

public class PowerwashManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("POWERWASH FILES")]
    public FileDownloader.FileData apworld;
    public FileDownloader.FileData apMod;
    public FileDownloader.FileData bepInEx;

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installAPModToggle;
    public Toggle installBepInExToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;

    [Header("ADVANCED OPTIONS")]
    public Toggle fullCleanBepInExToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private Process powerwashProcess;
    private string powerwashPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;

    void Start()
    {
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

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);
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
        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool bep = installBepInExToggle != null && installBepInExToggle.isOn;
        bool apmod = installAPModToggle != null && installAPModToggle.isOn;

        int count =
            (apworld ? 1 : 0) +
            (bep ? 1 : 0) +
            (apmod ? 1 : 0);

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        powerwashPath = GetPowerwashPath();

        if (string.IsNullOrEmpty(powerwashPath))
            return;

        if (bep && count == 1)
        {
            StartCoroutine(BepInExOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        powerwashPath = GetPowerwashPath();

        if (string.IsNullOrEmpty(powerwashPath))
            return;

        string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");
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

        SafeDeleteDirectory(Path.Combine(pluginsPath, "SW_CreeperKing.ArchipelagoMod"));

        hasOtherMods = HasOtherMods(pluginsPath);

        if (fullCleanBepInExToggle != null && fullCleanBepInExToggle.isOn)
        {
            SafeDeleteDirectory(Path.Combine(powerwashPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(powerwashPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(powerwashPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(powerwashPath, ".doorstop_version"));
            return;
        }

        if (!hasOtherMods)
        {
            SafeDeleteDirectory(Path.Combine(powerwashPath, "BepInEx"));
            SafeDeleteFile(Path.Combine(powerwashPath, "doorstop_config.ini"));
            SafeDeleteFile(Path.Combine(powerwashPath, "winhttp.dll"));
            SafeDeleteFile(Path.Combine(powerwashPath, ".doorstop_version"));
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

            if (name != "SW_CreeperKing.ArchipelagoMod")
                return true;
        }

        foreach (string file in files)
        {
            return true;
        }

        return false;
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

    public void QuitLauncher()
    {
        Application.Quit();
    }

    IEnumerator InstallFlow()
    {
        if (installAPWorldToggle == null || installAPWorldToggle.isOn)
            yield return InstallAPWorld();

        if (installBepInExToggle != null && installBepInExToggle.isOn)
            yield return InstallBepInEx();

        if (installAPModToggle == null || installAPModToggle.isOn)
            yield return InstallAPMod();

        LaunchPowerwash();

        yield return WaitForConfigFiles();

        ClosePowerwash();

        yield return new WaitForSeconds(1f);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
            LaunchPowerwash();
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return InstallAPWorld();
        yield break;
    }

    IEnumerator BepInExOnlyFlow()
    {
        yield return InstallBepInEx();

        LaunchPowerwash();
        yield return WaitForConfigFiles();
        ClosePowerwash();

        yield break;
    }

    IEnumerator WaitForConfigFiles()
    {
        string cfg = Path.Combine(powerwashPath, "BepInEx", "config", "BepInEx.cfg");

        float timeout = 30f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (File.Exists(cfg))
                yield break;

            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator InstallAPWorld()
    {
        string localPath = Path.Combine(Application.persistentDataPath, apworld.fileName);

        downloader.DownloadToFolder(apworld, Application.persistentDataPath);
        yield return new WaitForSeconds(2f);

        string targetFolder = null;

        string[] possiblePaths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Archipelago", "custom_worlds"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Archipelago", "custom_worlds")
        };

        foreach (string path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                targetFolder = path;
                break;
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    string path = Path.Combine(drive.RootDirectory.FullName, "Archipelago", "custom_worlds");

                    if (Directory.Exists(path))
                    {
                        targetFolder = path;
                        break;
                    }
                }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            ShowInfo("Archipelago Launcher is not installed.\nPlease install it before using APWorld.");
            yield break;
        }

        string target = Path.Combine(targetFolder, apworld.fileName);

        try
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }

            if (File.Exists(localPath))
                File.Copy(localPath, target, true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("APWorld install failed: " + e.Message);
            ShowInfo("Failed to install APWorld.");
        }
    }

    IEnumerator InstallAPMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "PowerwashAPModTemp");
        yield return downloader.DownloadAndExtract(apMod, Application.persistentDataPath, extractPath);

        string modFolder = Path.Combine(extractPath, "SW_CreeperKing.ArchipelagoMod");
        string pluginsPath = Path.Combine(powerwashPath, "BepInEx", "plugins");

        Directory.CreateDirectory(pluginsPath);

        if (Directory.Exists(modFolder))
        {
            string targetMod = Path.Combine(pluginsPath, "SW_CreeperKing.ArchipelagoMod");

            if (Directory.Exists(targetMod))
                Directory.Delete(targetMod, true);

            CopyDirectory(modFolder, targetMod);
        }

        SafeDeleteDirectory(extractPath);
    }

    IEnumerator InstallBepInEx()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "BepInExTemp");

        yield return downloader.DownloadAndExtract(bepInEx, Application.persistentDataPath, extractPath);

        MoveDirectory(extractPath, powerwashPath);

        SafeDeleteDirectory(extractPath);
    }

    void LaunchPowerwash()
    {
        string exePath = Path.Combine(powerwashPath, "PowerWashSimulator.exe");

        if (File.Exists(exePath))
            powerwashProcess = Process.Start(exePath);
    }

    void ClosePowerwash()
    {
        try
        {
            if (powerwashProcess != null && !powerwashProcess.HasExited)
            {
                powerwashProcess.Kill();
                powerwashProcess.Dispose();
                powerwashProcess = null;
            }
        }
        catch { }
    }

    void CleanupProcesses()
    {
        ClosePowerwash();
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

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
        }
    }

    string GetPowerwashPath()
    {
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "PowerWashSimulator"
        );

        return Directory.Exists(path) ? path : "";
    }
}