using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
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
    public FileDownloader.FileData ds2LazyLoader;
    public FileDownloader.FileData ds2Randomizer;

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
    public Toggle installRandomizerToggle;

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
    private bool lastApWorldInstallSuccess = false;

    [System.Serializable]
    public class Ds2Config
    {
        public string ds2scholarAP;
        public string ds2AP;
        public string ds2Apworld;
        public string ds2LazyLoader;
        public string ds2Randomizer;
        public string[] steamSearchPaths;
        public string[] apSearchPaths;
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
        ds2LazyLoader.url = remoteConfig.ds2LazyLoader;
        ds2Randomizer.url = remoteConfig.ds2Randomizer;
    }

    public void RunSetup() => ShowConfirmation("Are you sure you want to setup?", "Setup");
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
        bool randomizer = installRandomizerToggle != null && installRandomizerToggle.isOn;
        bool needsGamePath = ds2ap || randomizer || !apworld;

        if (needsGamePath && string.IsNullOrEmpty(ds2Path))
        {
            string versionName = isScholarVersion ? "Dark Souls II Scholar of the First Sin" : "Dark Souls II";
            ShowInfo($"{versionName} path not found. Please check your installation.");
            return;
        }

        int count = (apworld ? 1 : 0) + (ds2ap ? 1 : 0) + (randomizer ? 1 : 0);

        if (count == 0)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        if (apworld && count == 1) { StartCoroutine(APWorldOnlyFlow()); return; }
        if (ds2ap && count == 1) { StartCoroutine(DS2APOnlyFlow()); return; }
        if (randomizer && count == 1) { StartCoroutine(RandomizerOnlyFlow()); return; }

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

        string gamePath = GetDS2GamePath();

        string[] foldersToDelete = { "archipelago", "dllMods", "randomizer" };
        foreach (string folder in foldersToDelete)
        {
            SafeDeleteDirectory(Path.Combine(gamePath, folder));
        }

        string[] filesToDelete =
        {
            "dinput8.dll",
            "lazyLoad.ini",
            "LICENSES.txt",
            "README.txt",
            "ds2s_heap_x.dll",
            "modengine.ini"
        };
        foreach (string file in filesToDelete)
        {
            SafeDeleteFile(Path.Combine(gamePath, file));
        }

        DeleteVersionFiles();

        ShowInfo("Revert completed!");
    }

    void DeleteVersionFiles()
    {
        try
        {
            string gamePath = GetDS2GamePath();
            if (!Directory.Exists(gamePath)) return;

            foreach (string file in Directory.GetFiles(gamePath))
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
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Starting installation...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        bool installApworld = installDS2ApworldToggle == null || installDS2ApworldToggle.isOn;
        bool installDs2ap = installDS2APToggle != null && installDS2APToggle.isOn;
        bool installRandomizer = installRandomizerToggle != null && installRandomizerToggle.isOn;

        if (installRandomizer)
        {
            ShowInfo("Installing Lazy Loader...");
            yield return InstallLazyLoader();

            ShowInfo("Installing Randomizer...");
            yield return InstallRandomizer();

            PatchModEngineIni();
        }

        if (installApworld)
        {
            ShowInfo("Installing AP World...");
            yield return InstallAPWorld();
        }

        if (installDs2ap)
        {
            ShowInfo("Installing DS2 AP Client...");
            yield return InstallDS2AP(installRandomizer);
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

    IEnumerator RandomizerOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing Lazy Loader...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallLazyLoader();

        ShowInfo("Installing Randomizer...");
        yield return InstallRandomizer();

        PatchModEngineIni();

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;
        CreateVersionFile(apUrl, ds2Apworld.url);

        ShowInfo("Randomizer installed successfully!");
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

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

        ShowInfo("Installation complete!");
    }

    IEnumerator DS2APOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing DS2 AP Client...");
        yield return new WaitForSeconds(1f);
        CloseInfoPanel();

        yield return InstallDS2AP(false);

        string apUrl = isScholarVersion ? ds2ScholarAP.url : ds2VanillaAP.url;
        CreateVersionFile(apUrl, ds2Apworld.url);
        ShowInfo("DS2 AP Client installed successfully!");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + ds2Apworld.url);

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

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
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

    // useRandomizerDllMods: when true (Randomizer toggle active during setup), the AP client dll
    // is installed as "archipelago.dll" inside the Game\dllMods folder instead of Game\dinput8.dll.
    IEnumerator InstallDS2AP(bool useRandomizerDllMods)
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

        string gamePath = GetDS2GamePath();
        string destFolder = gamePath;
        string destFileName = "dinput8.dll";

        if (useRandomizerDllMods)
        {
            destFolder = Path.Combine(gamePath, "dllMods");
            destFileName = "archipelago.dll";
        }

        if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
        {
            try
            {
                Directory.CreateDirectory(destFolder);
                UnityEngine.Debug.Log("Created folder: " + destFolder);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to create folder: " + e.Message);
                ShowInfo("ERROR: Could not create folder\n" + e.Message);
                yield break;
            }
        }

        string targetPath = Path.Combine(destFolder, destFileName);

        if (File.Exists(targetPath))
        {
            try { File.SetAttributes(targetPath, FileAttributes.Normal); File.Delete(targetPath); } catch { }
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

    // Downloads the Lazy Loader zip and extracts the contents of its inner subfolder
    // directly into the Game folder (next to the exe). Installed before anything else.
    IEnumerator InstallLazyLoader()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(ds2LazyLoader.url))
        {
            ShowInfo("ERROR: Lazy Loader URL is empty!");
            UnityEngine.Debug.LogError("Lazy Loader URL not set!");
            yield break;
        }

        string gamePath = GetDS2GamePath();
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("ERROR: Game folder not found, cannot install Lazy Loader.");
            yield break;
        }

        if (!Directory.Exists(gamePath))
        {
            try { Directory.CreateDirectory(gamePath); } catch { }
        }

        string existingDll = Path.Combine(gamePath, "dinput8.dll");
        if (File.Exists(existingDll))
        {
            try
            {
                File.SetAttributes(existingDll, FileAttributes.Normal);
                File.Delete(existingDll);
                UnityEngine.Debug.Log("Removed existing dinput8.dll before Lazy Loader install.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("Could not delete existing dinput8.dll: " + e.Message);
            }
        }

        string fileName = ds2LazyLoader.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ds2LazyLoader.url.Substring(ds2LazyLoader.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Lazy Loader from: " + ds2LazyLoader.url);
        yield return DownloadFile(ds2LazyLoader.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Lazy Loader download failed!");
            yield break;
        }

        try
        {
            ExtractZip(localPath, gamePath, stripTopLevelFolder: true);
            UnityEngine.Debug.Log("Lazy Loader extracted to: " + gamePath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to extract Lazy Loader: " + e.Message);
            ShowInfo("ERROR: Failed to install Lazy Loader\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);
    }

    // Downloads the Randomizer zip, extracts it into the Game folder while renaming its
    // dinput8.dll to randomizer.dll, then moves randomizer.dll into Game\dllMods.
    IEnumerator InstallRandomizer()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(ds2Randomizer.url))
        {
            ShowInfo("ERROR: Randomizer URL is empty!");
            UnityEngine.Debug.LogError("Randomizer URL not set!");
            yield break;
        }

        string gamePath = GetDS2GamePath();
        if (string.IsNullOrEmpty(gamePath))
        {
            ShowInfo("ERROR: Game folder not found, cannot install Randomizer.");
            yield break;
        }

        string fileName = ds2Randomizer.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = ds2Randomizer.url.Substring(ds2Randomizer.url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading Randomizer from: " + ds2Randomizer.url);
        yield return DownloadFile(ds2Randomizer.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: Randomizer download failed!");
            yield break;
        }

        var renameMap = new Dictionary<string, string>
        {
            { "dinput8.dll", "randomizer.dll" }
        };

        try
        {
            ExtractZip(localPath, gamePath, stripTopLevelFolder: false, renameMap: renameMap);
            UnityEngine.Debug.Log("Randomizer extracted to: " + gamePath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to extract Randomizer: " + e.Message);
            ShowInfo("ERROR: Failed to install Randomizer\n" + e.Message);
            DeleteTempFile(localPath);
            yield break;
        }

        DeleteTempFile(localPath);

        string dllModsPath = Path.Combine(gamePath, "dllMods");
        if (!Directory.Exists(dllModsPath))
        {
            try { Directory.CreateDirectory(dllModsPath); } catch { }
        }

        string randomizerSrc = Path.Combine(gamePath, "randomizer.dll");
        string randomizerDst = Path.Combine(dllModsPath, "randomizer.dll");

        if (File.Exists(randomizerSrc))
        {
            try
            {
                if (File.Exists(randomizerDst))
                {
                    File.SetAttributes(randomizerDst, FileAttributes.Normal);
                    File.Delete(randomizerDst);
                }
                File.Move(randomizerSrc, randomizerDst);
                UnityEngine.Debug.Log("randomizer.dll moved to dllMods: " + randomizerDst);
                ShowInfo("Randomizer installed successfully!");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to move randomizer.dll to dllMods: " + e.Message);
                ShowInfo("ERROR: Failed to finish Randomizer install\n" + e.Message);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("randomizer.dll not found after extraction, cannot move to dllMods.");
            ShowInfo("ERROR: randomizer.dll not found after extraction.");
        }
    }

    // Sets blockNetworkAccess=0 in modengine.ini (Game folder). Must run AFTER ds2Randomizer
    // has been downloaded/extracted, since that's what brings modengine.ini onto disk.
    void PatchModEngineIni()
    {
        try
        {
            string gamePath = GetDS2GamePath();
            string iniPath = Path.Combine(gamePath, "modengine.ini");

            if (!File.Exists(iniPath))
            {
                UnityEngine.Debug.LogWarning("modengine.ini not found, cannot patch blockNetworkAccess.");
                return;
            }

            string content = File.ReadAllText(iniPath);
            string patched = Regex.Replace(content, @"blockNetworkAccess\s*=\s*1", "blockNetworkAccess=0");

            if (patched != content)
            {
                File.WriteAllText(iniPath, patched);
                UnityEngine.Debug.Log("Patched modengine.ini: blockNetworkAccess set to 0.");
            }
            else
            {
                UnityEngine.Debug.Log("modengine.ini already had blockNetworkAccess=0 (or pattern not found).");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to patch modengine.ini: " + e.Message);
        }
    }

    void ExtractZip(string zipPath, string destFolder, bool stripTopLevelFolder, Dictionary<string, string> renameMap = null)
    {
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            string topLevelFolder = null;

            if (stripTopLevelFolder)
            {
                bool consistent = true;
                foreach (var entry in archive.Entries)
                {
                    string entryPath = entry.FullName.Replace('\\', '/');
                    int slashIdx = entryPath.IndexOf('/');
                    if (slashIdx <= 0) { consistent = false; break; }
                    string folder = entryPath.Substring(0, slashIdx);
                    if (topLevelFolder == null) topLevelFolder = folder;
                    else if (topLevelFolder != folder) { consistent = false; break; }
                }
                if (!consistent) topLevelFolder = null;
            }

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                string entryPath = entry.FullName.Replace('\\', '/');

                if (!string.IsNullOrEmpty(topLevelFolder) && entryPath.StartsWith(topLevelFolder + "/"))
                {
                    entryPath = entryPath.Substring(topLevelFolder.Length + 1);
                }

                if (string.IsNullOrEmpty(entryPath)) continue;

                string fileName = Path.GetFileName(entryPath);
                string relativeDir = Path.GetDirectoryName(entryPath);

                if (renameMap != null && renameMap.ContainsKey(fileName))
                {
                    fileName = renameMap[fileName];
                }

                string destPath = string.IsNullOrEmpty(relativeDir)
                    ? Path.Combine(destFolder, fileName)
                    : Path.Combine(destFolder, relativeDir, fileName);

                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (File.Exists(destPath))
                {
                    try { File.SetAttributes(destPath, FileAttributes.Normal); File.Delete(destPath); } catch { }
                }

                entry.ExtractToFile(destPath, true);
            }
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
        string exePath = Path.Combine(GetDS2GamePath(), "DarkSoulsII.exe");
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

            string gamePath = GetDS2GamePath();

            if (!string.IsNullOrEmpty(gamePath) && !Directory.Exists(gamePath))
            {
                try { Directory.CreateDirectory(gamePath); } catch { }
            }

            string rootVersionPath = Path.Combine(gamePath, versionFileName);
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

    // Returns the "Game" subfolder inside the detected DS2 install folder,
    // where DarkSoulsII.exe, dinput8.dll, archipelago/ and the version file actually live.
    string GetDS2GamePath()
    {
        if (string.IsNullOrEmpty(ds2Path)) return "";
        return Path.Combine(ds2Path, "Game");
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