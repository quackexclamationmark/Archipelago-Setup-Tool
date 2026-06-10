using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

public class CelesteManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("CELESTE FILES")]
    public FileDownloader.FileData celesteAP;

    [Header("FEATURE TOGGLES")]
    public Toggle installArchipelagoToggle;

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

    private string celesteGamePath;
    private string pendingAction;
    private CelesteConfig remoteConfig;
    private bool configLoaded = false;
    private List<string> installedFiles = new List<string>();
    private Process celesteProcess;

    private const string INSTALL_TRACKER_FILE = "CelesteAP_installed.txt";

    [System.Serializable]
    public class CelesteConfig
    {
        public string celesteowAP;
    }

    void Start()
    {
        celesteGamePath = FindCelesteGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        if (installArchipelagoToggle != null)
            installArchipelagoToggle.isOn = true;

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;
    }

    void OnDestroy()
    {
        CloseCeleste();
    }

    string FindCelesteGamePath()
    {
        // Essaye les chemins rapides d'abord
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Celeste"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Celeste"),
            @"C:\Program Files (x86)\Steam\steamapps\common\Celeste",
            @"C:\Program Files\Steam\steamapps\common\Celeste",
        };

        foreach (string path in quickPaths)
        {
            if (Directory.Exists(path))
            {
                UnityEngine.Debug.Log("Celeste game path found: " + path);
                return path;
            }
        }

        // Cherche sur tous les disques
        try
        {
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.DriveType != System.IO.DriveType.Fixed)
                    continue;

                try
                {
                    // Cherche Steam\steamapps
                    string celestePath = Path.Combine(drive.Name, "Steam", "steamapps", "common", "Celeste");
                    if (Directory.Exists(celestePath))
                    {
                        UnityEngine.Debug.Log("Celeste game path found: " + celestePath);
                        return celestePath;
                    }

                    // Cherche SteamLibrary\steamapps
                    celestePath = Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Celeste");
                    if (Directory.Exists(celestePath))
                    {
                        UnityEngine.Debug.Log("Celeste game path found: " + celestePath);
                        return celestePath;
                    }

                    // Cherche directement steamapps à la racine du disque
                    celestePath = Path.Combine(drive.Name, "steamapps", "common", "Celeste");
                    if (Directory.Exists(celestePath))
                    {
                        UnityEngine.Debug.Log("Celeste game path found: " + celestePath);
                        return celestePath;
                    }

                    // Cherche dans Program Files (x86)\steamapps
                    celestePath = Path.Combine(drive.Name, "Program Files (x86)", "steamapps", "common", "Celeste");
                    if (Directory.Exists(celestePath))
                    {
                        UnityEngine.Debug.Log("Celeste game path found: " + celestePath);
                        return celestePath;
                    }

                    // Cherche dans Program Files\steamapps
                    celestePath = Path.Combine(drive.Name, "Program Files", "steamapps", "common", "Celeste");
                    if (Directory.Exists(celestePath))
                    {
                        UnityEngine.Debug.Log("Celeste game path found: " + celestePath);
                        return celestePath;
                    }
                }
                catch { }
            }
        }
        catch { }

        UnityEngine.Debug.LogWarning("Celeste game path not found in common locations");
        return "";
    }

    void ApplyCelesteConfig()
    {
        if (remoteConfig == null)
            return;

        celesteAP.url = remoteConfig.celesteowAP;
        celesteAP.fileName = "CelesteArchipelagoMod.zip";
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup the Celeste Archipelago Mod?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to remove the Celeste Archipelago Mod? This will delete all files added by the setup.", "Revert");
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
        }
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    private void ExecuteSetup()
    {
        if (string.IsNullOrEmpty(celesteGamePath))
        {
            ShowInfo("Celeste game path not found. Please ensure Celeste is installed via Steam.");
            return;
        }

        StartCoroutine(InstallFlow());
    }

    private void ExecuteRevert()
    {
        celesteGamePath = FindCelesteGamePath();

        if (string.IsNullOrEmpty(celesteGamePath))
        {
            ShowInfo("Celeste game path not found.");
            return;
        }

        // Charger la liste des fichiers installés
        LoadInstalledFilesList();

        if (installedFiles.Count == 0)
        {
            ShowInfo("No Celeste Archipelago Mod installation found. Nothing to revert.");
            return;
        }

        ShowInfo("Removing Celeste Archipelago Mod...");

        // Supprimer tous les fichiers listés
        foreach (string file in installedFiles)
        {
            SafeDeleteFile(file);
        }

        // Supprimer le fichier de tracking
        SafeDeleteFile(Path.Combine(celesteGamePath, INSTALL_TRACKER_FILE));

        // Supprimer le dossier Mods s'il est vide
        try
        {
            string modsPath = Path.Combine(celesteGamePath, "Mods");
            if (Directory.Exists(modsPath) && Directory.GetFiles(modsPath).Length == 0 && Directory.GetDirectories(modsPath).Length == 0)
            {
                Directory.Delete(modsPath);
                UnityEngine.Debug.Log("Deleted empty Mods directory");
            }
        }
        catch { }

        ShowInfo("Celeste Archipelago Mod removed successfully!");
    }

    void LoadInstalledFilesList()
    {
        installedFiles.Clear();

        string trackerPath = Path.Combine(celesteGamePath, INSTALL_TRACKER_FILE);

        if (!File.Exists(trackerPath))
        {
            UnityEngine.Debug.LogWarning("Installation tracker file not found: " + trackerPath);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(trackerPath);
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#"))
                {
                    installedFiles.Add(trimmedLine);
                }
            }

            UnityEngine.Debug.Log("Loaded " + installedFiles.Count + " installed files from tracker");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error reading installation tracker: " + e.Message);
        }
    }

    IEnumerator InstallFlow()
    {
        installedFiles.Clear();

        if (installArchipelagoToggle != null && installArchipelagoToggle.isOn)
        {
            ShowInfo("Installing Celeste Archipelago Mod...");
            yield return InstallCelesteAP();
        }

        if (!string.IsNullOrEmpty(celesteAP.url))
        {
            CreateVersionFile(celesteAP.url);
        }

        // Sauvegarder la liste des fichiers installés
        SaveInstalledFilesList();

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Celeste...");
            LaunchCeleste();
        }
        else
        {
            ShowInfo("Celeste Archipelago Mod installed successfully!");
        }

        yield break;
    }

    IEnumerator InstallCelesteAP()
    {
        while (!configLoaded)
            yield return null;

        if (string.IsNullOrEmpty(celesteAP.url))
        {
            UnityEngine.Debug.LogError("celesteAP.url is empty or null!");
            ShowInfo("ERROR: Celeste AP URL not configured. Check remote config.");
            yield break;
        }

        UnityEngine.Debug.Log("Installing from URL: " + celesteAP.url);

        string extractPath = Path.Combine(Application.persistentDataPath, "CelesteAPTemp");

        // Crée le répertoire d'extraction s'il n'existe pas
        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }

        yield return downloader.DownloadAndExtract(celesteAP, Application.persistentDataPath, extractPath);

        try
        {
            // Cherche le dossier "Preloaded-CelesteAP" à l'intérieur de l'archive
            string[] modDirs = Directory.GetDirectories(extractPath, "Preloaded-CelesteAP", SearchOption.AllDirectories);

            if (modDirs.Length > 0)
            {
                string modSourcePath = modDirs[0];

                // Copie TOUT le contenu du dossier vers la racine du jeu Celeste
                CopyDirectory(modSourcePath, celesteGamePath);

                UnityEngine.Debug.Log("Celeste Archipelago Mod installed to: " + celesteGamePath);
                ShowInfo("Celeste Archipelago Mod installed successfully!");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Preloaded-CelesteAP folder not found in archive");

                // Affiche la structure pour debug
                string[] allDirs = Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories);
                UnityEngine.Debug.Log("Available folders in archive:");
                foreach (string dir in allDirs)
                {
                    UnityEngine.Debug.Log("  " + dir);
                }

                ShowInfo("ERROR: Preloaded-CelesteAP folder not found in the zip!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error installing Celeste AP: " + e.Message);
            ShowInfo("ERROR: Failed to install Celeste mod!\n" + e.Message);
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
            remoteConfig = JsonUtility.FromJson<CelesteConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            UnityEngine.Debug.Log("Celeste AP URL from config: " + (string.IsNullOrEmpty(remoteConfig.celesteowAP) ? "EMPTY" : remoteConfig.celesteowAP));
            ApplyCelesteConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed (this is OK, config is optional): " + e.Message);
        }

        configLoaded = true;
    }

    void LaunchCeleste()
    {
        string exePath = Path.Combine(celesteGamePath, "Celeste.exe");

        if (File.Exists(exePath))
        {
            celesteProcess = Process.Start(exePath);
            UnityEngine.Debug.Log("Launched Celeste from: " + exePath);
        }
        else
        {
            UnityEngine.Debug.LogError("Celeste.exe not found at: " + exePath);
            ShowInfo("ERROR: Celeste.exe not found!");
        }
    }

    void CloseCeleste()
    {
        try
        {
            if (celesteProcess != null && !celesteProcess.HasExited)
            {
                celesteProcess.Kill();
                celesteProcess.Dispose();
                celesteProcess = null;
            }
        }
        catch { }
    }

    void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                UnityEngine.Debug.Log("Deleted file: " + path);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete file: " + path + " - " + e.Message);
        }
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
            UnityEngine.Debug.Log("Deleted directory: " + path);
        }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        if (!Directory.Exists(target))
            Directory.CreateDirectory(target);

        try
        {
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, destFile, true);
                UnityEngine.Debug.Log("Copied file: " + Path.GetFileName(file));

                // Tracer le fichier installé
                installedFiles.Add(destFile);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying directory: " + e.Message);
        }
    }

    void SaveInstalledFilesList()
    {
        try
        {
            string trackerPath = Path.Combine(celesteGamePath, INSTALL_TRACKER_FILE);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# Celeste Archipelago Mod - Installed Files Tracker");
            sb.AppendLine("# Auto-generated on: " + System.DateTime.Now);
            sb.AppendLine("# Do not edit manually");
            sb.AppendLine("");

            foreach (string file in installedFiles)
            {
                sb.AppendLine(file);
            }

            File.WriteAllText(trackerPath, sb.ToString());
            UnityEngine.Debug.Log("Saved installation tracker with " + installedFiles.Count + " files to: " + trackerPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error saving installation tracker: " + e.Message);
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

    void CreateVersionFile(string apUrl)
    {
        try
        {
            string apVersion = ExtractVersionFromUrl(apUrl, @"/releases/download/([^/]+)/");

            string versionFileName = "Celeste APMod Version " + apVersion + ".txt";
            string content = "Celeste Archipelago Setup Tool by quack!\n";
            content += "https://github.com/quackexclamationmark/Archipelago-Setup-Tool\n";
            content += "\n";
            content += "=== CELESTE ARCHIPELAGO MOD ===\n";
            content += "Downloaded from: " + apUrl + "\n";
            content += "Version: " + apVersion + "\n";
            content += "\n";
            content += "Downloaded at: " + System.DateTime.Now + "\n";

            DeleteOldVersionFiles();

            string modsPath = Path.Combine(celesteGamePath, "Mods");
            if (Directory.Exists(modsPath))
            {
                string modsVersionPath = Path.Combine(modsPath, versionFileName);
                File.WriteAllText(modsVersionPath, content);
                UnityEngine.Debug.Log("Version file created in Mods: " + modsVersionPath);

                // Tracer le fichier de version
                installedFiles.Add(modsVersionPath);
            }
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
            System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(@"Celeste APMod Version .+\.txt");

            string modsPath = Path.Combine(celesteGamePath, "Mods");
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
                            UnityEngine.Debug.Log("Deleted old version file in Mods: " + fileName);
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogWarning("Could not delete old version file in Mods: " + e.Message);
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
}
