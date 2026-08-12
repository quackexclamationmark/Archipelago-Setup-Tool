using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using Microsoft.Win32;

public class DOOMEternalManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("DOOM ETERNAL FILES")]
    public FileDownloader.FileData doometernalModInjector;
    public FileDownloader.FileData doometernalAP;
    public FileDownloader.FileData doometernalMeathook;
    public FileDownloader.FileData doometernalApworld;

    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "DOOMEternal";

    [Header("FEATURE TOGGLES")]
    public Toggle installModInjectorToggle;
    public Toggle installAPToggle;
    public Toggle installMeathookToggle;
    public Toggle installAPWorldToggle;

    [Header("LAUNCH OPTIONS")]
    public Toggle secondLaunchToggle;
    public Toggle launchInjectorAfterSetupToggle;

    [Header("REVERT OPTIONS")]
    public Toggle removeAPModsOnlyToggle;
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

    private const string DOOM_ETERNAL_STEAM_APPID = "782330";

    private Process doometernalProcess;
    private string doometernalPath;
    private string doometernalBasePath;
    private string modsPath;
    private string documentsPath;
    private string pendingAction;
    private bool pendingFullCleanConfirmation = false;
    private DOOMEternalConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class DOOMEternalConfig
    {
        public string doometernalModInjector;
        public string doometernalAP;
        public string doometernalMeathook;
        public string doometernalApworld;
        public string[] steamSearchPaths;
    }

    void Start()
    {
        doometernalPath = GetDOOMEternalPath();
        doometernalBasePath = Path.Combine(doometernalPath, "base");
        modsPath = Path.Combine(doometernalPath, "Mods");
        documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (launchInjectorAfterSetupToggle != null)
            launchInjectorAfterSetupToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (removeAPModsOnlyToggle != null)
            removeAPModsOnlyToggle.isOn = true;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.isOn = false;

        if (fullCleanModsToggle != null)
            fullCleanModsToggle.onValueChanged.AddListener(OnFullCleanChanged);
    }

    void CleanupProcesses()
    {
        CloseDOOMEternal();
    }

    void ApplyDOOMEternalConfig()
    {
        if (remoteConfig == null)
            return;

        doometernalModInjector.url = remoteConfig.doometernalModInjector;
        doometernalAP.url = remoteConfig.doometernalAP;
        doometernalMeathook.url = remoteConfig.doometernalMeathook;
        doometernalApworld.url = remoteConfig.doometernalApworld;
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
        doometernalPath = GetDOOMEternalPath();

        bool modInjector = installModInjectorToggle == null || installModInjectorToggle.isOn;
        bool ap = installAPToggle != null && installAPToggle.isOn;
        bool meathook = installMeathookToggle != null && installMeathookToggle.isOn;
        bool apworld = installAPWorldToggle != null && installAPWorldToggle.isOn;
        bool needsGamePath = modInjector || ap || meathook;

        if (needsGamePath && (string.IsNullOrEmpty(doometernalPath) || !Directory.Exists(doometernalPath)))
        {
            ShowInfo("Game path not found. Please check your installation.");
            return;
        }

        if (needsGamePath)
        {
            CreateSteamAppIdFile();
        }

        int count =
            (modInjector ? 1 : 0) +
            (ap ? 1 : 0) +
            (meathook ? 1 : 0) +
            (apworld ? 1 : 0);

        if (modInjector && count == 1)
        {
            StartCoroutine(ModInjectorOnlyFlow());
            return;
        }

        if (ap && count == 1)
        {
            StartCoroutine(APOnlyFlow());
            return;
        }

        if (meathook && count == 1)
        {
            StartCoroutine(MeathookOnlyFlow());
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    // Cree le fichier steam_appid.txt dans le dossier du jeu avec l'AppID de DOOM Eternal
    void CreateSteamAppIdFile()
    {
        try
        {
            if (string.IsNullOrEmpty(doometernalPath))
                return;

            if (!Directory.Exists(doometernalPath))
                Directory.CreateDirectory(doometernalPath);

            string appIdPath = Path.Combine(doometernalPath, "steam_appid.txt");
            File.WriteAllText(appIdPath, DOOM_ETERNAL_STEAM_APPID);

            UnityEngine.Debug.Log("steam_appid.txt created at: " + appIdPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to create steam_appid.txt: " + e.Message);
        }
    }

    // Lance start_injector_windows.bat avec les permissions administrateur
    void LaunchInjectorAsAdmin()
    {
        string injectorPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago", "client", "start_injector_windows.bat");

        if (!File.Exists(injectorPath))
        {
            UnityEngine.Debug.LogWarning("Injector script not found at: " + injectorPath);
            ShowInfo("ERROR: start_injector_windows.bat not found!");
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = injectorPath,
                WorkingDirectory = Path.GetDirectoryName(injectorPath),
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            UnityEngine.Debug.Log("Launched injector as admin: " + injectorPath);
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // Generalement declenche si l'utilisateur refuse l'invite UAC
            UnityEngine.Debug.LogWarning("Injector launch cancelled or failed (UAC): " + e.Message);
            ShowInfo("Injector launch was cancelled.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch injector as admin: " + e.Message);
            ShowInfo("ERROR: Failed to launch injector\n" + e.Message);
        }
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing AP World...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal...");
            LaunchDOOMEternal();
            yield return new WaitForSeconds(2f);
        }

        if (launchInjectorAfterSetupToggle != null && launchInjectorAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Injector (Admin)...");
            LaunchInjectorAsAdmin();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");
    }

    private void ExecuteRevert()
    {
        doometernalPath = GetDOOMEternalPath();

        doometernalBasePath = Path.Combine(doometernalPath, "base");
        modsPath = Path.Combine(doometernalPath, "Mods");

        if (string.IsNullOrEmpty(doometernalPath))
            return;

        bool removeAP = removeAPModsOnlyToggle != null && removeAPModsOnlyToggle.isOn;
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

            ShowInfo("Removing DOOM Eternal AP mods...");

            // Supprime le zip AP en se basant sur le marqueur (nom variable), plus fiable qu'un nom fixe
            RemoveOldAPModZip();
            SafeDeleteFile(Path.Combine(modsPath, "apmod_zip_name.txt"));

            DeleteOldVersionFiles();

            ShowInfo("DOOM Eternal AP mods removed successfully!");
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

        ShowInfo("Removing all mod files...");

        // Remove Mod Injector files from root
        SafeDeleteFile(Path.Combine(doometernalPath, "EternalModInjector Settings.txt"));
        SafeDeleteFile(Path.Combine(doometernalPath, "EternalModInjector.bat"));
        SafeDeleteFile(Path.Combine(doometernalPath, "XINPUT1_3.dll"));
        SafeDeleteFile(Path.Combine(doometernalPath, "EternalModManager.exe"));

        // Remove files from base directory
        SafeDeleteFile(Path.Combine(doometernalBasePath, "BlangParser.dll"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "DEternal_loadMods.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "DEternal_patchManifest.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.def"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "EternalPatcher.exe.config"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "idRehash.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "Newtonsoft.Json.dll"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "opusdec.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "opusenc.exe"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "rs_data"));
        SafeDeleteFile(Path.Combine(doometernalBasePath, "zlib64.dll"));

        // Remove Mods directory completely
        SafeDeleteDirectory(modsPath);

        // Remove DOOM Eternal Archipelago directory from Documents
        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago");
        SafeDeleteDirectory(apInstallPath);

        // Remove version files
        DeleteOldVersionFiles();

        // Remove steam_appid.txt
        SafeDeleteFile(Path.Combine(doometernalPath, "steam_appid.txt"));

        ShowInfo("Full clean completed!");
    }

    bool HasOtherMods()
    {
        if (!Directory.Exists(modsPath))
            return false;

        string knownApZipName = "";
        string markerPath = Path.Combine(modsPath, "apmod_zip_name.txt");
        if (File.Exists(markerPath))
        {
            try { knownApZipName = File.ReadAllText(markerPath).Trim(); } catch { }
        }

        string[] files = Directory.GetFiles(modsPath);
        string[] dirs = Directory.GetDirectories(modsPath);

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);

            if (name.StartsWith("DOOM Eternal APMod Version") && name.EndsWith(".txt"))
                continue;

            if (name == "apmod_zip_name.txt")
                continue;

            if (!string.IsNullOrEmpty(knownApZipName) && name == knownApZipName)
                continue;

            // Compat rétro : ancien nom fixe
            if (name == "DoomEternalArchipelagoPreAlpha.zip")
                continue;

            return true;
        }

        foreach (string dir in dirs)
        {
            string name = Path.GetFileName(dir);

            if (name != "DOOM Eternal Archipelago")
                return true;
        }

        return false;
    }

    IEnumerator InstallFlow()
    {
        if (installModInjectorToggle == null || installModInjectorToggle.isOn)
        {
            ShowInfo("Installing Mod Injector...");
            yield return InstallModInjector();
        }

        if (installAPToggle != null && installAPToggle.isOn)
        {
            ShowInfo("Installing DOOM Eternal Archipelago...");
            yield return InstallDOOMEternalAP();
        }

        if (installMeathookToggle != null && installMeathookToggle.isOn)
        {
            ShowInfo("Installing Meathook...");
            yield return InstallMeathook();
        }

        if (installAPWorldToggle != null && installAPWorldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        CreateVersionFile(doometernalModInjector.url, doometernalAP.url, doometernalMeathook.url);

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal...");
            LaunchDOOMEternal();
            yield return new WaitForSeconds(2f);
        }

        if (launchInjectorAfterSetupToggle != null && launchInjectorAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Injector (Admin)...");
            LaunchInjectorAsAdmin();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");
        yield break;
    }

    IEnumerator InstallModInjector()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("Config loaded. Mod Injector URL: " + doometernalModInjector.url);

        if (string.IsNullOrEmpty(doometernalModInjector.url))
        {
            ShowInfo("ERROR: Mod Injector URL is empty!");
            UnityEngine.Debug.LogError("Mod Injector URL not set!");
            yield break;
        }

        string fileName = doometernalModInjector.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "EternalModInjector.zip";
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Mod Injector from: " + doometernalModInjector.url);

        yield return DownloadFile(doometernalModInjector.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Mod Injector download failed!");
            yield break;
        }

        FileInfo fileInfo = new FileInfo(localPath);
        UnityEngine.Debug.Log("File downloaded successfully: " + localPath + " (size: " + fileInfo.Length + " bytes)");

        try
        {
            if (!Directory.Exists(doometernalPath))
                Directory.CreateDirectory(doometernalPath);

            System.IO.Compression.ZipFile.ExtractToDirectory(localPath, doometernalPath, true);
            UnityEngine.Debug.Log("ZIP extracted to: " + doometernalPath);

            UnityEngine.Debug.Log("Mod Injector installed to: " + doometernalPath);
            ShowInfo("Mod Injector installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install Mod Injector: " + e.Message);
            UnityEngine.Debug.LogError("Stack trace: " + e.StackTrace);
            ShowInfo("ERROR: Failed to install Mod Injector\n" + e.Message);
            yield break;
        }

        try
        {
            File.Delete(localPath);
        }
        catch { }
    }

    IEnumerator InstallDOOMEternalAP()
    {
        while (!configLoaded)
            yield return null;

        string apInstallPath = Path.Combine(documentsPath, "DOOM Eternal Archipelago");
        string downloadedFile = Path.Combine(Application.persistentDataPath, "DoomEternalArchipelago.zip");

        UnityEngine.Debug.Log("Downloading DOOM Eternal AP from: " + doometernalAP.url);

        yield return DownloadFile(doometernalAP.url, downloadedFile);

        if (!File.Exists(downloadedFile))
        {
            UnityEngine.Debug.LogError("Download failed!");
            ShowInfo("ERROR: DOOM Eternal AP download failed!");
            yield break;
        }

        try
        {
            if (Directory.Exists(apInstallPath))
            {
                UnityEngine.Debug.Log("Deleting old installation: " + apInstallPath);
                Directory.Delete(apInstallPath, true);
            }

            Directory.CreateDirectory(apInstallPath);
            System.IO.Compression.ZipFile.ExtractToDirectory(downloadedFile, apInstallPath, true);

            UnityEngine.Debug.Log("DOOM Eternal AP installed to: " + apInstallPath);
            ShowInfo("DOOM Eternal AP installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to install DOOM Eternal AP: " + e.Message);
            ShowInfo("ERROR: Failed to install DOOM Eternal AP\n" + e.Message);
            yield break;
        }

        // On cherche le .zip qui se trouve A L'INTERIEUR du zip qu'on vient d'extraire,
        // c'est celui-ci (et non le zip téléchargé lui-même) qui doit aller dans Mods.
        string nestedZipPath = "";
        try
        {
            nestedZipPath = FindFirstZip(apInstallPath);

            if (!string.IsNullOrEmpty(nestedZipPath))
                UnityEngine.Debug.Log("Found nested zip inside extracted AP folder: " + nestedZipPath);
            else
                UnityEngine.Debug.LogWarning("No .zip found inside extracted AP folder.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Error while searching for nested zip: " + e.Message);
            nestedZipPath = "";
        }

        if (string.IsNullOrEmpty(nestedZipPath))
        {
            UnityEngine.Debug.LogWarning("Nothing to copy to Mods folder (no nested zip found).");

            try
            {
                if (File.Exists(downloadedFile))
                    File.Delete(downloadedFile);
            }
            catch { }

            yield break;
        }

        string modZipFileName = Path.GetFileName(nestedZipPath);

        try
        {
            if (string.IsNullOrEmpty(doometernalPath))
                doometernalPath = GetDOOMEternalPath();

            if (!string.IsNullOrEmpty(doometernalPath))
            {
                modsPath = Path.Combine(doometernalPath, "Mods");
                Directory.CreateDirectory(modsPath);

                // Nettoie l'éventuel ancien zip AP installé (le nom peut changer d'une version à l'autre)
                RemoveOldAPModZip();

                string modsZipPath = Path.Combine(modsPath, modZipFileName);
                if (File.Exists(modsZipPath))
                    File.Delete(modsZipPath);

                File.Copy(nestedZipPath, modsZipPath, true);
                UnityEngine.Debug.Log("Copied nested AP zip into DOOM Eternal Mods folder: " + modsZipPath);

                // Mémorise le nom exact du zip installé pour le revert et la détection d'autres mods
                File.WriteAllText(Path.Combine(modsPath, "apmod_zip_name.txt"), modZipFileName);
            }
            else
            {
                UnityEngine.Debug.LogWarning("DOOM Eternal path not found; skipped copying AP zip to Mods folder.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not copy zip into DOOM Eternal Mods folder: " + e.Message);
        }

        try
        {
            if (File.Exists(downloadedFile))
                File.Delete(downloadedFile);
        }
        catch { }

        yield break;
    }

    IEnumerator InstallMeathook()
    {
        while (!configLoaded)
            yield return null;

        UnityEngine.Debug.Log("Config loaded. Meathook URL: " + doometernalMeathook.url);

        if (string.IsNullOrEmpty(doometernalMeathook.url))
        {
            ShowInfo("ERROR: Meathook URL is empty!");
            UnityEngine.Debug.LogError("Meathook URL not set!");
            yield break;
        }

        string fileName = "XINPUT1_3.dll";
        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Meathook from: " + doometernalMeathook.url);

        yield return DownloadFile(doometernalMeathook.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Meathook download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        try
        {
            string targetPath = Path.Combine(doometernalPath, fileName);
            File.Copy(localPath, targetPath, true);
            UnityEngine.Debug.Log("Meathook copied to: " + targetPath);
            ShowInfo("Meathook installed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy Meathook: " + e.Message);
            ShowInfo("ERROR: Failed to install Meathook\n" + e.Message);
        }
    }

    IEnumerator InstallAPWorld()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + doometernalApworld.url);

        if (string.IsNullOrEmpty(doometernalApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = doometernalApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = doometernalApworld.url.Substring(doometernalApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + doometernalApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(doometernalApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + localPath);

        // Target paths
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

        UnityEngine.Debug.Log("Target path: " + target);

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

    IEnumerator ModInjectorOnlyFlow()
    {
        doometernalPath = GetDOOMEternalPath();

        if (string.IsNullOrEmpty(doometernalPath))
            yield break;

        ShowInfo("Installing Mod Injector...");
        yield return InstallModInjector();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchDOOMEternal();
            yield return new WaitForSeconds(2f);
        }

        if (launchInjectorAfterSetupToggle != null && launchInjectorAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Injector (Admin)...");
            LaunchInjectorAsAdmin();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator APOnlyFlow()
    {
        ShowInfo("Installing DOOM Eternal Archipelago...");
        yield return InstallDOOMEternalAP();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching DOOM Eternal...");
            LaunchDOOMEternal();
            yield return new WaitForSeconds(2f);
        }

        if (launchInjectorAfterSetupToggle != null && launchInjectorAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Injector (Admin)...");
            LaunchInjectorAsAdmin();
            yield return new WaitForSeconds(1f);
        }

        ShowInfo("Installation complete!");

        yield break;
    }

    IEnumerator MeathookOnlyFlow()
    {
        doometernalPath = GetDOOMEternalPath();

        if (string.IsNullOrEmpty(doometernalPath))
            yield break;

        ShowInfo("Installing Meathook...");
        yield return InstallMeathook();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            LaunchDOOMEternal();
            yield return new WaitForSeconds(2f);
        }

        if (launchInjectorAfterSetupToggle != null && launchInjectorAfterSetupToggle.isOn)
        {
            ShowInfo("Launching Injector (Admin)...");
            LaunchInjectorAsAdmin();
            yield return new WaitForSeconds(1f);
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
            remoteConfig = JsonUtility.FromJson<DOOMEternalConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyDOOMEternalConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;

        doometernalPath = GetDOOMEternalPath();
    }

    void LaunchDOOMEternal()
    {
        string exePath = Path.Combine(doometernalPath, "DOOMEternalx64vk.exe");

        if (!File.Exists(exePath))
            exePath = Path.Combine(doometernalPath, "DOOMEternal.exe");

        if (File.Exists(exePath))
            doometernalProcess = Process.Start(exePath);
    }

    void CloseDOOMEternal()
    {
        try
        {
            if (doometernalProcess != null && !doometernalProcess.HasExited)
            {
                doometernalProcess.Kill();
                doometernalProcess.Dispose();
                doometernalProcess = null;
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
        if (removeAPModsOnlyToggle != null)
        {
            removeAPModsOnlyToggle.isOn = false;
            removeAPModsOnlyToggle.interactable = !value;
        }
    }

    string FindFile(string root, string fileName)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                if (Path.GetFileName(file) == fileName)
                    return file;
        }
        catch { }

        return "";
    }

    // Cherche le premier fichier .zip trouvé dans le dossier (recherche récursive)
    string FindFirstZip(string root)
    {
        try
        {
            foreach (string file in Directory.GetFiles(root, "*.zip", SearchOption.AllDirectories))
                return file;
        }
        catch { }

        return "";
    }

    // Supprime l'ancien zip AP installé dans Mods, en se basant sur le marqueur
    // (le nom du zip imbriqué peut changer d'une version à l'autre)
    void RemoveOldAPModZip()
    {
        if (!Directory.Exists(modsPath))
            return;

        string markerPath = Path.Combine(modsPath, "apmod_zip_name.txt");

        if (File.Exists(markerPath))
        {
            try
            {
                string oldName = File.ReadAllText(markerPath).Trim();
                if (!string.IsNullOrEmpty(oldName))
                    SafeDeleteFile(Path.Combine(modsPath, oldName));
            }
            catch { }
        }

        // Compat rétro : au cas où l'ancien nom fixe traîne encore d'une version précédente
        SafeDeleteFile(Path.Combine(modsPath, "DoomEternalArchipelagoPreAlpha.zip"));
    }

    void CreateVersionFile(string modInjectorUrl, string apUrl, string meathookUrl)
    {
        try
        {
            string modInjectorVersion = ExtractVersionFromUrl(modInjectorUrl, @"/([^/]+)\.zip");
            string apVersion = ExtractVersionFromUrl(apUrl, @"/([^/]+)\.zip");
            string meathookVersion = "XINPUT1_3.dll";

            string versionFileName = "DOOM Eternal APMod Version " + apVersion + ".txt";
            string content = "DOOM Eternal Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/DOOM-Eternal-Setup-Tool\n";
            content += "\n";
            content += "=== MOD INJECTOR ===\n";
            content += "Downloaded from: " + modInjectorUrl + "\n";
            content += "Version: " + modInjectorVersion + "\n";
            content += "\n";
            content += "=== DOOM ETERNAL ARCHIPELAGO ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "=== MEATHOOK ===\n";
            content += "File: " + meathookVersion + "\n";
            content += "Downloaded from: " + meathookUrl + "\n";
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"DOOM Eternal APMod Version .+\.txt");

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

    string GetDOOMEternalPath()
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