using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO.Compression;

public class SM64ManualDL : MonoBehaviour
{
    [Header("FILEDOWNLOADER (optionnel)")]
    public FileDownloader downloader; // assigne ton FileDownloader (comme dans Balatro)
    public FileDownloader.FileData sm64APLauncherFile; // fichier ZIP du launcher (optionnel, utilisé si downloader assigné)

    [Header("PANELS")]
    public GameObject romSelectionPanel;
    public GameObject installationPanel;

    [Header("ROM SELECTION UI")]
    public Button selectRomButton;
    public TMP_InputField romPathInputField;
    public TextMeshProUGUI romPathPlaceholder;
    public Button nextPanelButton; // bouton de validation

    [Header("INSTALLATION UI")]
    public Toggle installMsysToggle;
    public Toggle installSM64APLauncherToggle;
    public Toggle launchAppsToggle;
    public Button installButton;
    public Button cancelButton;
    public Button installDependenciesButton; // Nouveau bouton pour installer les dépendances

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmPanel; // Panel with OK / No
    public TextMeshProUGUI confirmText;
    public Button confirmOkButton;
    public Button confirmNoButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("CONFIG")]
    public string configUrl = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/main/RemoteConfig/config.json";

    private string selectedRomPath = "";
    private string sm64MSYSDownloadUrl = "";
    private string sm64APLauncherDownloadUrl = "";
    private SM64Config sm64DefConfig;
    private RemoteConfig remoteConfig;
    private bool configLoaded = false;

    private bool msysInstalled = false;
    private bool launcherInstalled = false;

    private bool msysLaunched = false;
    private bool launcherLaunched = false;

    // Installation cancellation
    private bool installationCancelled = false;
    private System.Collections.Generic.List<Process> launchedProcesses = new System.Collections.Generic.List<Process>();

    // Track installation completion
    private bool installationComplete = false;

    // Helper state class for threaded tasks
    private class ThreadTask
    {
        public volatile bool Complete = false;
        public System.Exception Exception = null;
    }

    [System.Serializable]
    public class RomPaths { public string jp; public string us; }

    [System.Serializable]
    public class SM64Config
    {
        public bool advanced;
        public System.Collections.Generic.Dictionary<string, object> builds;
        public string default_build_root;
        public string msys_path;
        public RomPaths rom_paths;
        public string version;
    }

    [System.Serializable]
    public class RemoteConfig
    {
        public string sm64MSYS;
        public string sm64APLauncher;
        public SM64Config sm64DefConfig;
    }

    private float infoDefaultFontSize = 0f;
    private readonly string specialCompleteMessage = "Installation complete! You can now press the 'DL DEPENDENCIES' button. When you're done, check the 'Connection Infos' on the infos tab";

    void Start()
    {
        // Listeners
        if (selectRomButton != null) selectRomButton.onClick.AddListener(SelectRom);
        if (nextPanelButton != null) nextPanelButton.onClick.AddListener(ValidateAndShowNextMessage);
        if (romPathInputField != null) romPathInputField.onValueChanged.AddListener(OnRomPathInputChanged);

        if (installButton != null) installButton.onClick.AddListener(OnInstallButtonClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelButtonClicked);
        if (installDependenciesButton != null) installDependenciesButton.onClick.AddListener(OnInstallDependenciesClicked);

        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (confirmOkButton != null) confirmOkButton.onClick.AddListener(OnConfirmOk);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        if (romSelectionPanel != null) romSelectionPanel.SetActive(true);
        if (installationPanel != null) installationPanel.SetActive(false);

        if (installMsysToggle != null) installMsysToggle.isOn = true;
        if (installSM64APLauncherToggle != null) installSM64APLauncherToggle.isOn = true;
        if (launchAppsToggle != null) launchAppsToggle.isOn = true;

        if (cancelButton != null) cancelButton.interactable = false;
        if (installDependenciesButton != null) installDependenciesButton.interactable = false;

        UpdateNextButtonInteractable();

        // store default font size (if TMP text assigned)
        if (infoText != null) infoDefaultFontSize = infoText.fontSize;

        StartCoroutine(LoadRemoteConfig());
    }

    // Met à jour l'interactable du bouton Next sans afficher un message d'erreur
    void UpdateNextButtonInteractable()
    {
        if (nextPanelButton == null) return;
        nextPanelButton.interactable = IsValidRomSilent(selectedRomPath);
    }

    IEnumerator LoadRemoteConfig()
    {
        UnityEngine.Debug.Log("Loading SM64 config from: " + configUrl);

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(configUrl);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed (this is OK, config is optional): " + request.error);
            configLoaded = true;
            yield break;
        }

        ParseRemoteConfig(request.downloadHandler.text);
        configLoaded = true;
    }

    void ParseRemoteConfig(string jsonText)
    {
        try
        {
            remoteConfig = JsonUtility.FromJson<RemoteConfig>(jsonText);
            if (remoteConfig != null)
            {
                sm64MSYSDownloadUrl = remoteConfig.sm64MSYS;
                sm64APLauncherDownloadUrl = remoteConfig.sm64APLauncher;
                sm64DefConfig = remoteConfig.sm64DefConfig;
                UnityEngine.Debug.Log("SM64 config loaded successfully.");
            }
            else UnityEngine.Debug.LogWarning("RemoteConfig JSON parsed to null.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }
    }

    void SelectRom() { StartCoroutine(ShowFileBrowser()); }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null) FileBrowser.Skin = darkSkin;
        else UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select Super Mario 64 ROM (.z64)");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            SetRomPath(FileBrowser.Result[0]);
        }
    }

    void SetRomPath(string path)
    {
        selectedRomPath = path;
        if (romPathInputField != null) romPathInputField.text = path;
        if (romPathPlaceholder != null) romPathPlaceholder.gameObject.SetActive(false);
        UnityEngine.Debug.Log("ROM path set: " + path);
        UpdateNextButtonInteractable();
    }

    void OnRomPathInputChanged(string newValue)
    {
        selectedRomPath = newValue;
        UpdateNextButtonInteractable();
    }

    bool IsValidRom(string romPath)
    {
        if (string.IsNullOrEmpty(romPath))
        {
            ShowInfo("Please select a ROM file.");
            return false;
        }

        if (!File.Exists(romPath))
        {
            ShowInfo("ROM file does not exist.");
            return false;
        }

        if (!romPath.EndsWith(".z64", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("ROM must be a .z64 file.");
            return false;
        }

        string fileName = Path.GetFileName(romPath);

        if (!fileName.Contains("Super Mario 64", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("ROM filename must contain 'Super Mario 64'.");
            return false;
        }

        if (fileName.Contains("Europe", System.StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("Shindou", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowInfo("European and Shindou versions are not supported.");
            return false;
        }

        return true;
    }

    bool IsValidRomSilent(string romPath)
    {
        if (string.IsNullOrEmpty(romPath)) return false;
        if (!File.Exists(romPath)) return false;
        if (!romPath.EndsWith(".z64", System.StringComparison.OrdinalIgnoreCase)) return false;

        string fileName = Path.GetFileName(romPath);

        if (!fileName.Contains("Super Mario 64", System.StringComparison.OrdinalIgnoreCase)) return false;

        if (fileName.Contains("Europe", System.StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("Shindou", System.StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    // Next button behaviour: validate + show info (no panel swap)
    void ValidateAndShowNextMessage()
    {
        if (!IsValidRom(selectedRomPath)) return;
        ShowInfo("Valid ROM - You can move on to the next step! (Setup panel)");
    }

    void OnInstallButtonClicked()
    {
        if (!IsValidRom(selectedRomPath)) return;

        if (confirmPanel != null)
        {
            if (confirmText != null) confirmText.text = "Do you want to start the setup now?";
            confirmPanel.SetActive(true);
        }
        else ExecuteInstallation();
    }

    void OnCancelButtonClicked()
    {
        installationCancelled = true;
        ShowInfo("Installation cancelled. Killing processes...");

        // Tuer tous les processus lancés
        foreach (Process proc in launchedProcesses)
        {
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    proc.Kill();
                    UnityEngine.Debug.Log("Process killed: " + proc.ProcessName);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error killing process: " + e.Message);
            }
        }
        launchedProcesses.Clear();

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;
    }

    void OnInstallDependenciesClicked()
    {
        ShowInfo("Installing dependencies via MSYS2 pacman...");
        StartCoroutine(InstallDependenciesInMSYS());
    }

    IEnumerator InstallDependenciesInMSYS()
    {
        string tempDirPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SM64APTemp");
        string tempScriptPath = Path.Combine(tempDirPath, "install_deps.sh");

        // script robuste (utilise une fonction on_error pour éviter les problèmes de quoting)
        string scriptContent = @"#!/usr/bin/env bash
#!/bin/bash
# install_deps.sh — Installs SM64AP dependencies via pacman (MSYS2)
# Writes a log to $HOME/install_deps.log and leaves an interactive shell open
set -euo pipefail

LOG=""$HOME/install_deps.log""

on_error() {
  local lineno=""${1:-?}""
  echo """"
  echo ""=== ERROR during installation (line ${lineno}) ===""
  echo ""See the log: $LOG""
  echo ""---- log tail ----""
  tail -n 200 ""$LOG"" 2>/dev/null || true
  echo ""Opening an interactive shell for debugging...""
  exec /usr/bin/bash
}
trap 'on_error $LINENO' ERR

echo ""=== SM64AP dependency installer started at $(date) ==="" | tee -a ""$LOG""
echo ""User: $(whoami) Home: $HOME"" | tee -a ""$LOG""
echo ""Working dir: $(pwd)"" | tee -a ""$LOG""
echo """" | tee -a ""$LOG""

echo ""Updating pacman keyring and DB..."" | tee -a ""$LOG""
pacman -Sy --noconfirm archlinux-keyring 2>&1 | tee -a ""$LOG"" || true

echo ""Running full system update (this may take a while)..."" | tee -a ""$LOG""
pacman -Syu --noconfirm 2>&1 | tee -a ""$LOG""

echo ""Installing required packages..."" | tee -a ""$LOG""
pacman -S --needed --noconfirm \
  unzip \
  mingw-w64-x86_64-gcc \
  mingw-w64-x86_64-glew \
  mingw-w64-x86_64-SDL2 \
  git \
  make \
  python \
  mingw-w64-x86_64-cmake \
  2>&1 | tee -a ""$LOG""

echo """" | tee -a ""$LOG""
echo ""Installation completed at $(date)"" | tee -a ""$LOG""
echo ""Detailed outputs can be found in $LOG"" | tee -a ""$LOG""
echo """" 

echo ""Displaying the last 80 lines of the log:"" | tee -a ""$LOG""
tail -n 80 ""$LOG"" 2>/dev/null | tee -a ""$LOG"" || true
echo """" 

echo ""The shell remains open to allow checking or running commands manually.""
echo ""Type 'exit' or Ctrl-D to close the window.""
exec /usr/bin/bash --login
";

        try
        {
            if (!Directory.Exists(tempDirPath)) Directory.CreateDirectory(tempDirPath);

            // write the temporary copy in Documents for reference (CRLF/LF doesn't matter here)
            File.WriteAllText(tempScriptPath, scriptContent);

            ShowInfo("Preparing MSYS script and launching MSYS (will try common locations)...");

            // Detect common msysRoot locations (adjust or add more if needed)
            string[] commonRoots = new string[] { @"C:\msys64", @"D:\msys64", @"C:\MSYS2", @"C:\msys" };
            string msysRoot = null;
            foreach (var r in commonRoots) if (Directory.Exists(r)) { msysRoot = r; break; }
            if (msysRoot == null) msysRoot = @"C:\msys64"; // fallback, may not exist

            // Ensure MSYS home exists
            string msysHomeWin = Path.Combine(msysRoot, "home", System.Environment.UserName);
            try { if (!Directory.Exists(msysHomeWin)) Directory.CreateDirectory(msysHomeWin); } catch { }

            // Write a unix-line-ending copy into MSYS home so bash can read it reliably
            string msysScriptWinPath = Path.Combine(msysHomeWin, "install_deps.sh");
            try
            {
                string unixScript = scriptContent.Replace("\r\n", "\n");
                File.WriteAllText(msysScriptWinPath, unixScript);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Failed to write script into msys home: " + e.Message);
                ShowInfo("Failed to write script into MSYS home: " + e.Message);
                yield break;
            }

            // Convert that MSYS-home path to MSYS style (/c/...)
            string msysScriptPath = ConvertWindowsPathToMSys(msysScriptWinPath);

            // For debug: run an ls to show whether the file is visible to MSYS, then execute it and keep shell open
            string mintty = Path.Combine(msysRoot, "usr", "bin", "mintty.exe");
            string bash = Path.Combine(msysRoot, "usr", "bin", "bash.exe");
            string msys2cmd = Path.Combine(msysRoot, "msys2_shell.cmd");

            ProcessStartInfo psi = null;

            // NOTE: We avoid Verb = "runas" by default to prevent UAC popups; enable it only if you need elevation.
            if (File.Exists(mintty))
            {
                psi = new ProcessStartInfo
                {
                    FileName = mintty,
                    Arguments = $"-i /msys2.ico /usr/bin/bash -lc \"ls -la {msysScriptPath} || echo 'ls failed'; echo '--- running script ---'; /usr/bin/bash {msysScriptPath}; exec /usr/bin/bash\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = msysRoot
                };
            }
            else if (File.Exists(bash))
            {
                psi = new ProcessStartInfo
                {
                    FileName = bash,
                    Arguments = $"-lc \"ls -la {msysScriptPath} || echo 'ls failed'; echo '--- running script ---'; /usr/bin/bash {msysScriptPath}; exec /usr/bin/bash\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = msysRoot
                };
            }
            else if (File.Exists(msys2cmd))
            {
                psi = new ProcessStartInfo
                {
                    FileName = msys2cmd,
                    Arguments = $"-here -msys -c \"bash -lc \\\"ls -la {msysScriptPath} || echo 'ls failed'; echo '--- running script ---'; /usr/bin/bash {msysScriptPath}; exec /usr/bin/bash\\\"\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = msysRoot
                };
            }
            else
            {
                ShowInfo($"MSYS not found in common locations (tried {string.Join(", ", commonRoots)}). Please install MSYS2 or adjust msysRoot.");
                yield break;
            }

            Process proc = null;
            try
            {
                proc = Process.Start(psi);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Failed to start MSYS process: " + e.Message);
                ShowInfo("Failed to start MSYS process: " + e.Message);
                yield break;
            }

            if (proc != null)
            {
                launchedProcesses.Add(proc);
                ShowInfo("MSYS launched for dependency installation. Window will remain open until you close it.");
                // Wait for the user to close the terminal (so Unity flow knows when it's done)
                proc.WaitForExit();
                ShowInfo("Dependency installation process finished or terminal closed.");
            }
            else
            {
                ShowInfo("Failed to start MSYS process.");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error preparing/installing dependencies: " + e.Message);
            ShowInfo("Error preparing/installing dependencies: " + e.Message);
        }
        finally
        {
            // Keep the temp script in Documents for reference but remove the msys home copy if you want (optional)
            try { /* File.Delete(tempScriptPath); */ } catch { }
        }

        yield return null;
    }

    string ConvertWindowsPathToMSys(string winPath)
    {
        // Retourne un chemin que bash/MSYS comprend : C:\Users\Name -> /c/Users/Name
        if (string.IsNullOrEmpty(winPath)) return winPath;
        string full = Path.GetFullPath(winPath).Replace('\\', '/'); // "C:/Users/..."
        if (full.Length >= 2 && full[1] == ':')
        {
            char drive = char.ToLower(full[0]);
            string rest = full.Substring(2); // skip "C:"
            if (!rest.StartsWith("/")) rest = "/" + rest;
            return $"/{drive}{rest}";
        }
        return full;
    }

    void OnConfirmOk()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        ExecuteInstallation();
    }

    void OnConfirmNo()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void ExecuteInstallation()
    {
        installationCancelled = false;
        installationComplete = false;
        StartCoroutine(InstallationFlow());
    }

    IEnumerator InstallationFlow()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (installButton != null) installButton.interactable = false;
        if (cancelButton != null) cancelButton.interactable = true;
        if (installDependenciesButton != null) installDependenciesButton.interactable = false;

        bool installMsys = installMsysToggle != null && installMsysToggle.isOn;
        bool installSM64AP = installSM64APLauncherToggle != null && installSM64APLauncherToggle.isOn;
        bool launchApps = launchAppsToggle != null && launchAppsToggle.isOn;

        // Documents paths (request)
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string installPath = Path.Combine(documentsPath, "SM64AP");
        string tempDownloadPath = Path.Combine(documentsPath, "SM64APTemp");

        if (!Directory.Exists(tempDownloadPath)) Directory.CreateDirectory(tempDownloadPath);
        if (!Directory.Exists(installPath)) Directory.CreateDirectory(installPath);

        // reset flags
        msysInstalled = false;
        launcherInstalled = false;
        msysLaunched = false;
        launcherLaunched = false;

        // Lance les deux installations EN PARALLELE (sans yield return)
        if (installMsys && !installationCancelled)
        {
            ShowInfo("Installing MSYS...");
            StartCoroutine(InstallMSYS(installPath, tempDownloadPath));
        }

        if (installSM64AP && !installationCancelled)
        {
            ShowInfo("Installing SM64AP Launcher...");
            StartCoroutine(InstallSM64APLauncherWithConfig(installPath, tempDownloadPath, launchApps));
        }

        // Attends que les deux soient lancées
        while (!installationCancelled && ((installMsys && !msysLaunched) || (installSM64AP && !launcherLaunched)))
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (installationCancelled)
        {
            ShowInfo("Installation cancelled.");
            if (installButton != null) installButton.interactable = true;
            if (cancelButton != null) cancelButton.interactable = false;
            yield break;
        }

        // Si l'utilisateur a demandé de lancer les apps et que MSYS n'a pas été lancé yet, tente de lancer MSYS
        if (launchApps && installMsys && !msysLaunched)
        {
            ShowInfo("Launching MSYS...");
            LaunchMSYS(installPath);
        }

        // small delay to allow both to start
        yield return new WaitForSeconds(1.5f);

        ShowInfo(specialCompleteMessage);

        installationComplete = true;

        if (installButton != null) installButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = false;
        if (installDependenciesButton != null) installDependenciesButton.interactable = true;

        SafeDeleteDirectory(tempDownloadPath);
    }


    IEnumerator InstallMSYS(string installPath, string tempPath)
    {
        msysInstalled = false; // reset

        if (string.IsNullOrEmpty(sm64MSYSDownloadUrl))
        {
            ShowInfo("ERROR: MSYS download URL not loaded!");
            yield break;
        }

        string exePath = Path.Combine(tempPath, "msys2-installer.exe");

        ShowInfo("Downloading MSYS installer...");

        // MSYS exe: use UnityWebRequest download (safe fallback)
        yield return DownloadFile(sm64MSYSDownloadUrl, exePath);

        if (!File.Exists(exePath) || installationCancelled)
        {
            ShowInfo("ERROR: MSYS download failed!");
            yield break;
        }

        ShowInfo("Running MSYS installer...");

        // background process to avoid freezing UI
        // Lancer sans attendre la fin: waitForExit = false, et signaler msysLaunched via onStarted
        ThreadTask procTask = StartProcessTask(exePath, installPath, false, proc => { msysLaunched = true; });
        while (!procTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.2f);

        if (procTask.Exception != null)
        {
            UnityEngine.Debug.LogError("MSYS installer failed: " + procTask.Exception.Message);
            ShowInfo("ERROR: MSYS installer failed: " + procTask.Exception.Message);
            msysInstalled = false;
            yield break;
        }

        // Optionally: check for some installed files here to be more robust

        msysInstalled = true;

        try { if (File.Exists(exePath)) File.Delete(exePath); } catch { }

        yield return null;
    }

    IEnumerator InstallSM64APLauncherWithConfig(string installPath, string tempPath, bool launchApps)
    {
        yield return InstallSM64APLauncher(installPath, tempPath);

        if (launcherInstalled && !installationCancelled)
        {
            // Crée la config
            try
            {
                ShowInfo("Creating configuration...");
                CreateSM64APConfig(installPath);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Config creation failed: " + e.Message);
            }

            // Lance le launcher IMMEDIATEMENT après installation ET config
            if (launchApps)
            {
                ShowInfo("Launching SM64AP Launcher...");
                LaunchSM64APLauncher(installPath);
            }
        }
    }

    // Install launcher: if downloader assigned & sm64APLauncherFile.url set -> use downloader.DownloadAndExtract
    IEnumerator InstallSM64APLauncher(string installPath, string tempPath)
    {
        launcherInstalled = false; // reset

        // If downloader available and file data provided -> use it
        if (downloader != null && sm64APLauncherFile != null && !string.IsNullOrEmpty(sm64APLauncherFile.url))
        {
            ShowInfo("Downloading and extracting SM64AP Launcher (via FileDownloader)...");
            string extractPath = Path.Combine(tempPath, "launcher_extracted");

            // Ensure clean extractPath
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);

            // NOTE: FileDownloader.DownloadAndExtract signature must match your FileDownloader implementation (as in Balatro)
            yield return downloader.DownloadAndExtract(sm64APLauncherFile, tempPath, extractPath);

            if (installationCancelled) yield break;

            // After extraction, copy to installPath/SM64APLauncher
            string launcherPath = Path.Combine(installPath, "SM64APLauncher");
            if (!Directory.Exists(launcherPath)) Directory.CreateDirectory(launcherPath);

            // Copy extracted files
            yield return CopyDirectoryRecursive(extractPath, launcherPath);

            SafeDeleteDirectory(extractPath);
            ShowInfo("SM64AP Launcher installed!");
            launcherInstalled = true;
            yield break;
        }

        // Fallback: use direct download of ZIP via UnityWebRequest and extract
        if (string.IsNullOrEmpty(sm64APLauncherDownloadUrl))
        {
            ShowInfo("ERROR: SM64AP Launcher download URL not loaded!");
            yield break;
        }

        string launcherDir = Path.Combine(installPath, "SM64APLauncher");
        string zipPath = Path.Combine(tempPath, "sm64aplauncher.zip");

        ShowInfo("Downloading SM64AP Launcher...");
        yield return DownloadFile(sm64APLauncherDownloadUrl, zipPath);

        if (!File.Exists(zipPath) || installationCancelled)
        {
            ShowInfo("ERROR: SM64AP Launcher download failed!");
            yield break;
        }

        ShowInfo("Extracting SM64AP Launcher...");
        string extractPathFallback = Path.Combine(tempPath, "launcher_extracted");
        if (Directory.Exists(extractPathFallback)) Directory.Delete(extractPathFallback, true);
        Directory.CreateDirectory(extractPathFallback);

        ThreadTask extractTask = StartZipExtraction(zipPath, extractPathFallback);
        while (!extractTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.1f);

        if (installationCancelled)
        {
            launcherInstalled = false;
            yield break;
        }

        if (extractTask.Exception != null)
        {
            UnityEngine.Debug.LogError("ZIP extraction failed: " + extractTask.Exception.Message);
            ShowInfo("ERROR: Failed to extract launcher ZIP: " + extractTask.Exception.Message);
            launcherInstalled = false;
            yield break;
        }

        ShowInfo("Installing SM64AP Launcher...");
        if (!Directory.Exists(launcherDir)) Directory.CreateDirectory(launcherDir);

        ThreadTask copyTask = StartCopyDirectoryTask(extractPathFallback, launcherDir);
        while (!copyTask.Complete && !installationCancelled) yield return new WaitForSeconds(0.1f);

        if (installationCancelled)
        {
            launcherInstalled = false;
            yield break;
        }

        if (copyTask.Exception != null)
        {
            UnityEngine.Debug.LogError("Directory copy failed: " + copyTask.Exception.Message);
            ShowInfo("ERROR: Failed to copy launcher files: " + copyTask.Exception.Message);
            launcherInstalled = false;
            yield break;
        }

        launcherInstalled = true;

        SafeDeleteDirectory(extractPathFallback);
        if (File.Exists(zipPath)) File.Delete(zipPath);
    }

    void CreateSM64APConfig(string installPath)
    {
        try
        {
            string launcherPath = Path.Combine(installPath, "SM64APLauncher");
            string configPath = Path.Combine(launcherPath, "SM64APLauncher_config.json");

            string romFileName = Path.GetFileName(selectedRomPath);
            bool isJapanese = romFileName.Contains("(JP)") || romFileName.Contains("(J)");

            SM64Config config = new SM64Config
            {
                advanced = sm64DefConfig != null ? sm64DefConfig.advanced : false,
                builds = new System.Collections.Generic.Dictionary<string, object>(),
                default_build_root = sm64DefConfig != null ? sm64DefConfig.default_build_root : "",
                msys_path = sm64DefConfig != null ? sm64DefConfig.msys_path : "",
                rom_paths = new RomPaths
                {
                    jp = sm64DefConfig != null && sm64DefConfig.rom_paths != null ? sm64DefConfig.rom_paths.jp : "",
                    us = sm64DefConfig != null && sm64DefConfig.rom_paths != null ? sm64DefConfig.rom_paths.us : ""
                },
                version = sm64DefConfig != null ? sm64DefConfig.version : ""
            };

            if (isJapanese) config.rom_paths.jp = selectedRomPath.Replace("\\", "/");
            else config.rom_paths.us = selectedRomPath.Replace("\\", "/");

            if (!Directory.Exists(Path.Combine(installPath, "SM64APLauncher"))) Directory.CreateDirectory(Path.Combine(installPath, "SM64APLauncher"));

            string jsonConfig = JsonUtility.ToJson(config, true);
            File.WriteAllText(configPath, jsonConfig);
            UnityEngine.Debug.Log("SM64AP config created at: " + configPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating SM64AP config: " + e.Message);
            throw;
        }
    }

    void LaunchSM64APLauncher(string installPath)
    {
        string launcherPath = Path.Combine(installPath, "SM64APLauncher", "SM64APLauncher.exe");
        if (File.Exists(launcherPath))
        {
            try
            {
                Process proc = Process.Start(launcherPath);
                if (proc != null)
                {
                    launchedProcesses.Add(proc);
                    launcherLaunched = true; // marque comme lancé immédiatement
                }
                UnityEngine.Debug.Log("SM64AP Launcher launched!");
            }
            catch (System.Exception e) { ShowInfo("Error launching SM64AP Launcher: " + e.Message); }
        }
        else UnityEngine.Debug.LogWarning("SM64AP Launcher executable not found at: " + launcherPath);
    }

    // Try to launch MSYS after installation. We try multiple plausible locations.
    void LaunchMSYS(string installPath)
    {
        try
        {
            System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string>();

            if (sm64DefConfig != null && !string.IsNullOrEmpty(sm64DefConfig.msys_path))
            {
                string cfg = sm64DefConfig.msys_path;
                if (Path.IsPathRooted(cfg)) candidates.Add(cfg);
                else candidates.Add(Path.Combine(installPath, cfg));
            }

            candidates.Add(Path.Combine(installPath, "msys2.exe"));
            candidates.Add(Path.Combine(installPath, "msys2", "msys2.exe"));
            candidates.Add(Path.Combine(installPath, "MSYS2", "msys2.exe"));
            candidates.Add(Path.Combine(installPath, "msys2_shell.cmd"));
            candidates.Add(Path.Combine(installPath, "msys2_shell.bat"));
            candidates.Add(Path.Combine(installPath, "usr", "bin", "bash.exe"));

            string found = null;
            foreach (string c in candidates)
            {
                if (string.IsNullOrEmpty(c)) continue;
                try { if (File.Exists(c)) { found = c; break; } } catch { }
            }

            if (found == null)
            {
                UnityEngine.Debug.LogWarning("MSYS executable not found in candidates; aborting MSYS launch.");
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = found,
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(found)
            };

            Process proc = Process.Start(psi);
            if (proc != null) launchedProcesses.Add(proc);
            // Marque MSYS comme lancé si on a démarré un processus
            if (proc != null) msysLaunched = true;
            UnityEngine.Debug.Log("MSYS launched: " + found);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch MSYS: " + e.Message);
            ShowInfo("Error launching MSYS: " + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            request.timeout = 300;
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
            }
            else UnityEngine.Debug.Log("Download complete!");
        }
    }

    IEnumerator ExtractZipAsync(string zipPath, string extractPath)
    {
        ThreadTask extractTask = StartZipExtraction(zipPath, extractPath);
        while (!extractTask.Complete) yield return new WaitForSeconds(0.1f);
        if (extractTask.Exception != null) UnityEngine.Debug.LogError("ZIP extraction failed: " + extractTask.Exception.Message);
    }

    private ThreadTask StartZipExtraction(string zipPath, string extractPath)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try { ZipFile.ExtractToDirectory(zipPath, extractPath, true); }
            catch (System.Exception e) { UnityEngine.Debug.LogError("Error extracting ZIP: " + e.Message); task.Exception = e; }
            finally { task.Complete = true; }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    IEnumerator CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        ThreadTask copyTask = StartCopyDirectoryTask(sourceDir, targetDir);
        while (!copyTask.Complete) yield return new WaitForSeconds(0.1f);
        if (copyTask.Exception != null) UnityEngine.Debug.LogError("Directory copy failed: " + copyTask.Exception.Message);
    }

    private ThreadTask StartCopyDirectoryTask(string sourceDir, string targetDir)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try { PerformDirectoryCopy(sourceDir, targetDir); }
            catch (System.Exception e) { UnityEngine.Debug.LogError("Error copying directory: " + e.Message); task.Exception = e; }
            finally { task.Complete = true; }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    void PerformDirectoryCopy(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(dir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            PerformDirectoryCopy(dir, targetSubDir);
        }
    }

    // Background process starter (pour ne pas bloquer l'UI)
    // Ajout de waitForExit et callback onStarted pour permettre le mode non-blocking
    private ThreadTask StartProcessTask(string exePath, string workingDirectory = null, bool waitForExit = true, System.Action<System.Diagnostics.Process> onStarted = null)
    {
        var task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory
                };

                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    launchedProcesses.Add(proc);
                    try { onStarted?.Invoke(proc); } catch { }
                    if (waitForExit)
                    {
                        // comportement d'origine : attendre que le processus se termine
                        proc.WaitForExit();
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("StartProcessTask error: " + e.Message);
                task.Exception = e;
            }
            finally { task.Complete = true; }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    void ShowInfo(string message)
    {
        if (infoText != null)
        {
            // Assure qu'on a bien la valeur par défaut
            if (infoDefaultFontSize == 0f) infoDefaultFontSize = infoText.fontSize;

            // If message matches the special message exactly, set font size to 28, else restore
            if (message == specialCompleteMessage) infoText.fontSize = 28f;
            else if (infoDefaultFontSize != 0f) infoText.fontSize = infoDefaultFontSize;

            infoText.text = message;
        }

        if (infoPanel != null) infoPanel.SetActive(true);
        UnityEngine.Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (infoText != null && infoDefaultFontSize != 0f) infoText.fontSize = infoDefaultFontSize;
    }

    void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}