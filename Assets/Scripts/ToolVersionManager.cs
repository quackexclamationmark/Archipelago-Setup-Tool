using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ToolVersionManager : MonoBehaviour
{
    [Header("VERSION CONFIG")]
    public string currentToolVersion = "0.2.2"; // À mettre à jour à chaque nouvelle version

    [Header("UPDATE PANEL")]
    public GameObject updatePanel;
    public TextMeshProUGUI updateMessage;
    public Button updateDownloadButton;
    public Button updateIgnoreButton;
    public Button selectDirectoryButton;
    public TMP_InputField selectedPathInputField;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private RemoteConfig remoteConfig;
    private string selectedDownloadDirectory = "";

    [System.Serializable]
    public class RemoteConfig
    {
        public Dictionary<string, string> toolVersions = new Dictionary<string, string>();
        public string toolLatestDownloadUrl;
    }

    void Start()
    {
        if (updatePanel != null)
            updatePanel.SetActive(false);

        if (updateDownloadButton != null)
            updateDownloadButton.onClick.AddListener(OnUpdateDownloadClick);

        if (updateIgnoreButton != null)
            updateIgnoreButton.onClick.AddListener(OnUpdateIgnore);

        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(OnSelectDirectoryClick);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        // Initialiser le chemin par défaut
        InitializeDefaultPath();

        StartCoroutine(LoadRemoteConfig());
    }

    void InitializeDefaultPath()
    {
        if (selectedPathInputField != null && string.IsNullOrEmpty(selectedPathInputField.text))
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            selectedPathInputField.text = documentsPath;
            selectedDownloadDirectory = documentsPath;
            UnityEngine.Debug.Log("Default path set to: " + documentsPath);
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

        try
        {
            string json = request.downloadHandler.text;
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

            remoteConfig = new RemoteConfig();
            remoteConfig.toolVersions = versions;
            remoteConfig.toolLatestDownloadUrl = downloadUrl;

            UnityEngine.Debug.Log("Remote config loaded successfully");
            CheckToolVersion();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Config parsing failed: " + e.Message);
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

    void OnSelectDirectoryClick()
    {
        StartCoroutine(ShowFileBrowser());
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
        {
            FileBrowser.Skin = darkSkin;
            UnityEngine.Debug.Log("DarkSkin assigned!");
        }
        else
        {
            UnityEngine.Debug.LogWarning("DarkSkin not assigned!");
        }

        // Déterminer le chemin de départ
        string startPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

        // Si le champ texte contient un chemin valide, l'utiliser
        if (selectedPathInputField != null && !string.IsNullOrEmpty(selectedPathInputField.text))
        {
            string inputPath = selectedPathInputField.text.Trim();
            if (Directory.Exists(inputPath))
            {
                startPath = inputPath;
                UnityEngine.Debug.Log("Using path from input field: " + startPath);
            }
            else
            {
                UnityEngine.Debug.LogWarning("Path from input field does not exist, using Documents folder: " + inputPath);
            }
        }

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            startPath,
            "Select Download Folder"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            selectedDownloadDirectory = FileBrowser.Result[0];
            UnityEngine.Debug.Log("Download directory selected: " + selectedDownloadDirectory);

            // Mettre à jour le champ input
            if (selectedPathInputField != null)
                selectedPathInputField.text = selectedDownloadDirectory;
        }
    }

    void OnUpdateDownloadClick()
    {
        if (string.IsNullOrEmpty(selectedDownloadDirectory))
        {
            ShowInfo("Please select a download directory first!");
            return;
        }

        StartCoroutine(DownloadUpdate());
    }

    IEnumerator DownloadUpdate()
    {
        if (string.IsNullOrEmpty(remoteConfig?.toolLatestDownloadUrl))
        {
            ShowInfo("ERROR: Download URL not configured!");
            yield break;
        }

        if (string.IsNullOrEmpty(selectedDownloadDirectory) || !Directory.Exists(selectedDownloadDirectory))
        {
            ShowInfo("ERROR: Invalid download directory!");
            yield break;
        }

        ShowInfo("Downloading new version...");

        string fileName = ExtractFileNameFromUrl(remoteConfig.toolLatestDownloadUrl);
        string downloadPath = Path.Combine(selectedDownloadDirectory, fileName);

        yield return DownloadFile(remoteConfig.toolLatestDownloadUrl, downloadPath);

        if (!File.Exists(downloadPath))
        {
            ShowInfo("ERROR: Download failed!");
            yield break;
        }

        updatePanel.SetActive(false);
        ShowInfo($"Download complete!\n\nSaved to:\n{downloadPath}");
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
            else
            {
                UnityEngine.Debug.Log("Download complete!");
            }
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
        selectedDownloadDirectory = "";
        if (selectedPathInputField != null)
            selectedPathInputField.text = "";
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
