using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System;
using System.Diagnostics;

public class PokeSnapManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData pokemonsnapApworld;
    public FileDownloader.FileData pokemonsnapProject64;

    [Header("FEATURE TOGGLES")]
    public Toggle installApworldToggle;
    public Toggle installProject64Toggle;

    [Header("INSTALL BUTTON")]
    public Button runSetupButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private bool configLoaded = false;
    private RemoteConfig remoteConfig;
    private string pendingAction = "";
    private bool lastApWorldInstallSuccess = false;

    [Serializable]
    public class RemoteConfig
    {
        public string pokemonsnapApworld;
        public string pokemonsnapProject64;
        public string[] apSearchPaths;
    }

    void Start()
    {
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

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (installProject64Toggle != null)
            installProject64Toggle.isOn = true;

        if (runSetupButton != null)
            runSetupButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to run setup?", "Setup"));

        StartCoroutine(LoadRemoteConfig());
    }

    void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    void OnConfirm()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (string.IsNullOrEmpty(pendingAction))
            return;

        if (pendingAction == "Setup")
        {
            ExecuteSetup();
        }

        pendingAction = "";
    }

    void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    void ExecuteSetup()
    {
        bool project64 = installProject64Toggle != null && installProject64Toggle.isOn;
        bool apworld = installApworldToggle != null && installApworldToggle.isOn;

        int count = (project64 ? 1 : 0) + (apworld ? 1 : 0);

        if (!project64 && !apworld)
        {
            ShowInfo("Please select at least one install option.");
            return;
        }

        if (project64 && count == 1)
        {
            StartCoroutine(Project64OnlyFlow());
            return;
        }

        if (apworld && count == 1)
        {
            StartCoroutine(APWorldOnlyFlow());
            return;
        }

        StartCoroutine(InstallFlow());
    }

    IEnumerator APWorldOnlyFlow()
    {
        yield return new WaitUntil(() => configLoaded);

        ShowInfo("Installing APWorld...");
        yield return new WaitForSeconds(1f);

        yield return InstallAPWorld();

        if (!lastApWorldInstallSuccess)
            yield break;

        ShowInfo("Installation complete!");
    }

    IEnumerator InstallFlow()
    {
        if (installApworldToggle != null && installApworldToggle.isOn)
        {
            ShowInfo("Installing APWorld...");
            yield return InstallAPWorld();
        }

        if (installProject64Toggle != null && installProject64Toggle.isOn)
        {
            ShowInfo("Installing Project64...");
            yield return RunInstallProject64();
        }

        ShowInfo("Installation complete!");
    }

    IEnumerator Project64OnlyFlow()
    {
        ShowInfo("Installing Project64...");
        yield return RunInstallProject64();
        ShowInfo("Installation complete!");
    }

    IEnumerator RunInstallProject64()
    {
        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            while (!configLoaded)
                yield return new WaitForSeconds(0.1f);
            CloseInfoPanel();
        }

        ShowInfo("Starting Project64 installation...");
        yield return new WaitForSeconds(0.5f);

        if (pokemonsnapProject64 == null)
        {
            ShowInfo("ERROR: pokemonsnapProject64 not configured!");
            yield break;
        }

        if (string.IsNullOrEmpty(pokemonsnapProject64.url))
        {
            ShowInfo("ERROR: pokemonsnapProject64 URL is empty!");
            yield break;
        }

        string url = pokemonsnapProject64.url;

        string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        try
        {
            if (!Directory.Exists(downloadsPath))
                Directory.CreateDirectory(downloadsPath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not access/create Downloads folder: " + e.Message);
        }

        string localPath = "";
        yield return DownloadFileAuto(url, downloadsPath, "Setup Project64.exe", result => localPath = result);

        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
        {
            ShowInfo("ERROR: Download failed!");
            yield break;
        }

        string fileName = Path.GetFileName(localPath);

        bool looksLikeProject64Exe = fileName.IndexOf("Project64", StringComparison.OrdinalIgnoreCase) >= 0
            && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        if (!looksLikeProject64Exe)
        {
            UnityEngine.Debug.LogWarning("Downloaded file name doesn't look like a Project64 installer: " + fileName);
        }

        ShowInfo("Launching Project64 installer...");

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = localPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Project64 installer: " + e.Message);
            ShowInfo("ERROR: Failed to launch Project64 installer\n" + e.Message);
            yield break;
        }

        ShowInfo("Project64 installer launched. It will install to your Downloads folder and start automatically.");
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        UnityEngine.Debug.Log("Config loaded. APWorld URL: " + pokemonsnapApworld.url);

        if (string.IsNullOrEmpty(pokemonsnapApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = pokemonsnapApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = pokemonsnapApworld.url.Substring(pokemonsnapApworld.url.LastIndexOf('/') + 1);

            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));

            UnityEngine.Debug.Log("Extracted filename from URL: " + fileName);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + pokemonsnapApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(pokemonsnapApworld.url, localPath);

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

    IEnumerator DownloadFileAuto(string url, string saveDirectory, string fallbackFileName, Action<string> onComplete)
    {
        UnityEngine.Debug.Log("Starting download (auto-name) from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
                onComplete(null);
                yield break;
            }

            string resolvedFileName = fallbackFileName;

            string contentDisposition = request.GetResponseHeader("Content-Disposition");
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                // Typical header looks like:
                //   attachment; filename="Setup Project64 3.0.1-5664-2df3434.exe"
                // or (less common): filename*=UTF-8''Setup%20Project64...exe
                string extracted = ExtractFileNameFromContentDisposition(contentDisposition);
                if (!string.IsNullOrEmpty(extracted))
                    resolvedFileName = extracted;
            }
            else
            {
                UnityEngine.Debug.LogWarning("No Content-Disposition header returned; using fallback filename: " + fallbackFileName);
            }

            string savePath = Path.Combine(saveDirectory, resolvedFileName);

            try
            {
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                UnityEngine.Debug.Log("Download complete! Saved to: " + savePath + " (" + request.downloadHandler.data.Length + " bytes)");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to write downloaded file to " + savePath + ": " + e.Message);
                onComplete(null);
                yield break;
            }

            onComplete(savePath);
        }
    }

    string ExtractFileNameFromContentDisposition(string header)
    {
        // Handles: filename="name.ext"  |  filename=name.ext  |  filename*=UTF-8''name.ext
        string[] parts = header.Split(';');
        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();

            if (part.StartsWith("filename*=", StringComparison.OrdinalIgnoreCase))
            {
                string value = part.Substring("filename*=".Length).Trim();
                int quotePos = value.IndexOf("''");
                if (quotePos >= 0)
                    value = value.Substring(quotePos + 2);
                try { return Uri.UnescapeDataString(value.Trim('"')); }
                catch { return value.Trim('"'); }
            }

            if (part.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
            {
                string value = part.Substring("filename=".Length).Trim();
                return value.Trim('"');
            }
        }

        return null;
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

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (var request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Config load failed: " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<RemoteConfig>(request.downloadHandler.text);
                ApplyRemoteConfig();
                UnityEngine.Debug.Log("Remote config loaded successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
            }
        }

        configLoaded = true;
        UnityEngine.Debug.Log("Config marked as loaded");
    }

    void ApplyRemoteConfig()
    {
        if (remoteConfig == null)
            return;

        pokemonsnapApworld.url = remoteConfig.pokemonsnapApworld;
        pokemonsnapProject64.url = remoteConfig.pokemonsnapProject64;
        UnityEngine.Debug.Log("Applied pokemonsnapApworld URL: " + pokemonsnapApworld.url);
        UnityEngine.Debug.Log("Applied pokemonsnapProject64 URL: " + pokemonsnapProject64.url);
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