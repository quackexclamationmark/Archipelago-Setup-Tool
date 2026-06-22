using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

public class ToolVersionManager : MonoBehaviour
{
    [Header("VERSION CONFIG")]
    public string currentToolVersion = "1.0.0"; // À mettre à jour à chaque nouvelle version

    [Header("UPDATE PANEL")]
    public GameObject updatePanel;
    public TextMeshProUGUI updateMessage;
    public Button updateDownloadButton;
    public Button updateIgnoreButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private RemoteConfig remoteConfig;
    private string applicationExePath = "";
    private string applicationVersionFolder = "";

    [System.Serializable]
    public class RemoteConfig
    {
        public Dictionary<string, string> toolVersions = new Dictionary<string, string>();
        public string toolLatestDownloadUrl;
    }

    void Start()
    {
        // Déterminer le chemin de l'exe actuel et son dossier parent
        GetApplicationPaths();

        if (updatePanel != null)
            updatePanel.SetActive(false);

        if (updateDownloadButton != null)
            updateDownloadButton.onClick.AddListener(OnUpdateDownloadClick);

        if (updateIgnoreButton != null)
            updateIgnoreButton.onClick.AddListener(OnUpdateIgnore);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        StartCoroutine(LoadRemoteConfig());
    }

    void GetApplicationPaths()
    {
        try
        {
            applicationExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            // applicationVersionFolder est le dossier parent de l'exe (ex: "Archipelago Setup Tool v0.2.4.2")
            applicationVersionFolder = System.IO.Path.GetDirectoryName(applicationExePath);
            UnityEngine.Debug.Log("Application exe path: " + applicationExePath);
            UnityEngine.Debug.Log("Application version folder: " + applicationVersionFolder);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error getting application path: " + e);
            applicationExePath = "";
            applicationVersionFolder = "";
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
            yield break;
        }

        bool parseSuccess = ParseRemoteConfig(request.downloadHandler.text);

        if (parseSuccess)
        {
            CheckToolVersion();
        }
    }

    bool ParseRemoteConfig(string json)
    {
        try
        {
            UnityEngine.Debug.Log("JSON loaded, parsing...");

            // Parser les versions
            Dictionary<string, string> versions = new Dictionary<string, string>();
            int versionsStart = json.IndexOf("\"toolVersions\"");
            if (versionsStart != -1)
            {
                int openBrace = json.IndexOf("{", versionsStart);
                int closeBrace = json.IndexOf("}", openBrace);
                string versionsJson = json.Substring(openBrace + 1, closeBrace - openBrace - 1);

                // Split par virgule et parser chaque ligne
                string[] entries = versionsJson.Split(',');
                foreach (string entry in entries)
                {
                    string trimmed = entry.Trim();
                    if (trimmed.Contains(":"))
                    {
                        int colonIndex = trimmed.LastIndexOf(':');
                        string versionKey = trimmed.Substring(0, colonIndex).Trim().Trim('"');
                        string versionStatus = trimmed.Substring(colonIndex + 1).Trim().Trim('"');

                        if (!string.IsNullOrEmpty(versionKey) && !string.IsNullOrEmpty(versionStatus))
                        {
                            versions[versionKey] = versionStatus;
                            UnityEngine.Debug.Log($"Parsed version: {versionKey} = {versionStatus}");
                        }
                    }
                }
            }

            // Parser l'URL de téléchargement
            string downloadUrl = "";
            int urlStart = json.IndexOf("\"toolLatestDownloadUrl\"");
            if (urlStart != -1)
            {
                int colonIndex = json.IndexOf(":", urlStart);
                int firstQuote = json.IndexOf("\"", colonIndex);
                int lastQuote = json.IndexOf("\"", firstQuote + 1);
                downloadUrl = json.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                UnityEngine.Debug.Log("Download URL: " + downloadUrl);
            }

            RemoteConfig remoteConfig = new RemoteConfig();
            remoteConfig.toolVersions = versions;
            remoteConfig.toolLatestDownloadUrl = downloadUrl;

            this.remoteConfig = remoteConfig;

            UnityEngine.Debug.Log("Remote config loaded successfully");
            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Config parsing failed: " + e.Message);
            return false;
        }
    }

    void CheckToolVersion()
    {
        UnityEngine.Debug.Log("CheckToolVersion called");

        if (remoteConfig == null)
        {
            UnityEngine.Debug.LogError("remoteConfig is NULL");
            return;
        }

        if (remoteConfig.toolVersions == null)
        {
            UnityEngine.Debug.LogError("toolVersions is NULL");
            return;
        }

        UnityEngine.Debug.Log("Current tool version: " + currentToolVersion);
        UnityEngine.Debug.Log("Available versions in config: " + string.Join(", ", remoteConfig.toolVersions.Keys));

        // Vérifier si la version actuelle existe dans la config
        if (!remoteConfig.toolVersions.ContainsKey(currentToolVersion))
        {
            UnityEngine.Debug.LogError($"Current version {currentToolVersion} NOT found in config");
            return;
        }

        string versionStatus = remoteConfig.toolVersions[currentToolVersion];
        UnityEngine.Debug.Log($"Tool version {currentToolVersion} status: {versionStatus}");

        // Si la version est marquée obsolète, afficher le popup
        if (versionStatus.ToLower() == "obsolete")
        {
            string latestVersion = GetLatestToolVersion();
            UnityEngine.Debug.Log("Latest version found: " + (latestVersion ?? "NULL"));

            if (!string.IsNullOrEmpty(latestVersion))
            {
                UnityEngine.Debug.Log($"New version available: {latestVersion}");
                ShowUpdatePopup(latestVersion);
            }
        }
        else if (versionStatus.ToLower() == "latest")
        {
            UnityEngine.Debug.Log("Tool is up to date!");
        }
    }

    string GetLatestToolVersion()
    {
        if (remoteConfig == null || remoteConfig.toolVersions == null)
            return null;

        // Chercher la version marquée "latest"
        foreach (KeyValuePair<string, string> entry in remoteConfig.toolVersions)
        {
            if (entry.Value.ToLower() == "latest")
                return entry.Key;
        }

        return null;
    }

    void ShowUpdatePopup(string latestVersion)
    {
        if (updatePanel == null || updateMessage == null)
            return;

        updateMessage.text = $"A new version is available!\n\nCurrent: {currentToolVersion}\nLatest: {latestVersion}\n\nDo you want to download it?";
        updatePanel.SetActive(true);
    }

    void OnUpdateDownloadClick()
    {
        StartCoroutine(DownloadAndUpdateTool());
    }

    IEnumerator DownloadAndUpdateTool()
    {
        if (string.IsNullOrEmpty(remoteConfig?.toolLatestDownloadUrl))
        {
            ShowInfo("ERROR: Download URL not configured!");
            yield break;
        }

        ShowInfo("Closing current application...");
        yield return new WaitForSeconds(1f);

        // Créer un script de mise à jour qui sera exécuté après la fermeture
        string parentFolder = Path.GetDirectoryName(applicationVersionFolder);
        string updateScriptPath = Path.Combine(parentFolder, "update_tool.bat");
        CreateUpdateScript(updateScriptPath, parentFolder);

        yield return new WaitForSeconds(0.5f);

        // Lancer le script de mise à jour
        LaunchUpdateScript(updateScriptPath);
    }

    void LaunchUpdateScript(string scriptPath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = scriptPath;
            psi.UseShellExecute = true;
            psi.CreateNoWindow = false;

            Process.Start(psi);
            UnityEngine.Debug.Log("Update script started");

            // Fermer l'application après un délai
            System.Threading.Thread.Sleep(500);
            UnityEngine.Application.Quit();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error launching update script: " + e);
            ShowInfo("ERROR: Failed to start update!\n" + e.Message);
        }
    }

    void CreateUpdateScript(string scriptPath, string parentFolder)
    {
        try
        {
            string fileName = ExtractFileNameFromUrl(remoteConfig.toolLatestDownloadUrl);
            string tempDownloadPath = Path.Combine(parentFolder, fileName);
            string extractPath = Path.Combine(parentFolder, "ArchipelagoToolUpdate");

            string batchContent = "@echo off\n";
            batchContent += "setlocal enabledelayedexpansion\n";
            batchContent += "cd /d \"" + parentFolder + "\"\n";
            batchContent += "\n";
            batchContent += "echo Downloading new version...\n";
            batchContent += "powershell -Command \"(New-Object Net.WebClient).DownloadFile('" + remoteConfig.toolLatestDownloadUrl + "', '" + tempDownloadPath + "')\"\n";
            batchContent += "if !ERRORLEVEL! neq 0 (\n";
            batchContent += "    echo Error: Download failed\n";
            batchContent += "    pause\n";
            batchContent += "    exit /b 1\n";
            batchContent += ")\n";
            batchContent += "\n";
            batchContent += "echo Extracting files...\n";
            batchContent += "powershell -Command \"Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('" + tempDownloadPath + "', '" + extractPath + "')\"\n";
            batchContent += "if !ERRORLEVEL! neq 0 (\n";
            batchContent += "    echo Error: Extraction failed\n";
            batchContent += "    pause\n";
            batchContent += "    exit /b 1\n";
            batchContent += ")\n";
            batchContent += "\n";
            batchContent += "echo Finding new version folder (Archipelago Setup Tool v*)...\n";
            batchContent += "set \"NEW_VERSION_FOLDER=\"\n";
            batchContent += "for /d %%A in (\"" + extractPath + "\\Archipelago Setup Tool v*\") do (\n";
            batchContent += "    set \"NEW_VERSION_FOLDER=%%A\"\n";
            batchContent += "    goto :found\n";
            batchContent += ")\n";
            batchContent += ":found\n";
            batchContent += "\n";
            batchContent += "if not defined NEW_VERSION_FOLDER (\n";
            batchContent += "    echo Error: No 'Archipelago Setup Tool v*' folder found in ZIP\n";
            batchContent += "    pause\n";
            batchContent += "    exit /b 1\n";
            batchContent += ")\n";
            batchContent += "\n";
            batchContent += "echo Clearing old version folder content...\n";
            batchContent += "for /d %%D in (\"" + applicationVersionFolder + "\\*\") do rmdir /s /q \"%%D\" >nul 2>&1\n";
            batchContent += "for %%F in (\"" + applicationVersionFolder + "\\*\") do del \"%%F\" >nul 2>&1\n";
            batchContent += "\n";
            batchContent += "echo Copying new version files...\n";
            batchContent += "xcopy \"!NEW_VERSION_FOLDER!\\*\" \"" + applicationVersionFolder + "\" /E /I /Y\n";
            batchContent += "if !ERRORLEVEL! neq 0 (\n";
            batchContent += "    echo Error: Failed to copy new version files\n";
            batchContent += "    pause\n";
            batchContent += "    exit /b 1\n";
            batchContent += ")\n";
            batchContent += "\n";
            batchContent += "echo Cleaning up temporary files...\n";
            batchContent += "rmdir /s /q \"" + extractPath + "\" >nul 2>&1\n";
            batchContent += "del \"" + tempDownloadPath + "\" >nul 2>&1\n";
            batchContent += "\n";
            batchContent += "echo Launching new version...\n";
            batchContent += "timeout /t 1 /nobreak\n";
            batchContent += "start \"\" \"" + applicationVersionFolder + "\\Archipelago Setup Tool.exe\"\n";
            batchContent += "\n";
            batchContent += "echo Update complete\n";
            batchContent += "del \"" + scriptPath + "\" >nul 2>&1\n";
            batchContent += "exit /b 0\n";

            File.WriteAllText(scriptPath, batchContent);
            UnityEngine.Debug.Log("Update script created: " + scriptPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error creating update script: " + e);
        }
    }

    string ExtractFileNameFromUrl(string url)
    {
        string fileName = url.Substring(url.LastIndexOf('/') + 1);
        if (fileName.Contains("?"))
            fileName = fileName.Substring(0, fileName.IndexOf("?"));
        return fileName;
    }

    void OnUpdateIgnore()
    {
        updatePanel.SetActive(false);
    }

    void ShowInfo(string message)
    {
        if (infoText != null)
            infoText.text = message;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        UnityEngine.Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}
