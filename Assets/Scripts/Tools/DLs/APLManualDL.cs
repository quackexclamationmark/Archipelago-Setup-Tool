using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class APLManualDL : MonoBehaviour
{
    [Header("FILES")]
    public Button aplDLButton;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string pendingAction;
    private APLConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class APLConfig
    {
        public string aplauncherDL;
    }

    void Start()
    {
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

        if (aplDLButton != null)
            aplDLButton.onClick.AddListener(OnDownloadLauncherClicked);
    }

    void OnDownloadLauncherClicked()
    {
        ShowConfirmation("Are you sure you want to download the AP Launcher installer?", "DownloadLauncher");
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
            case "DownloadLauncher":
                StartCoroutine(InstallLauncher());
                break;
        }

        pendingAction = "";
    }

    private void OnCancel()
    {
        confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ------------------------------------------------------------------
    // Downloads folder
    // ------------------------------------------------------------------

    private string GetDownloadsFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    // ------------------------------------------------------------------
    // Install = download .exe into Downloads + launch it
    // ------------------------------------------------------------------

    IEnumerator InstallLauncher()
    {
        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (remoteConfig == null || string.IsNullOrEmpty(remoteConfig.aplauncherDL))
        {
            ShowInfo("ERROR: aplauncherDL URL is empty!");
            UnityEngine.Debug.LogError("aplauncherDL URL not set!");
            yield break;
        }

        string url = remoteConfig.aplauncherDL;

        string fileName = url;
        int queryIndex = fileName.IndexOf('?');
        if (queryIndex >= 0)
            fileName = fileName.Substring(0, queryIndex);
        fileName = fileName.Substring(fileName.LastIndexOf('/') + 1);

        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            fileName = "APLauncher.exe";

        string downloadsDir = GetDownloadsFolder();

        try
        {
            Directory.CreateDirectory(downloadsDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Cannot access Downloads folder: " + e.Message);
            ShowInfo("ERROR: Cannot access Downloads folder\n" + e.Message);
            yield break;
        }

        string finalPath = Path.Combine(downloadsDir, fileName);

        if (File.Exists(finalPath))
        {
            try
            {
                File.Delete(finalPath);
            }
            catch
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                finalPath = Path.Combine(downloadsDir, nameNoExt + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".exe");
                UnityEngine.Debug.LogWarning("Old file is locked, using new name: " + finalPath);
            }
        }

        string partPath = finalPath + ".part";

        UnityEngine.Debug.Log("Downloading AP Launcher from: " + url);
        UnityEngine.Debug.Log("Saving to: " + finalPath);

        ShowInfo("Downloading AP Launcher...");

        bool success = false;
        yield return DownloadFile(url, partPath, result => success = result);

        if (!success || !File.Exists(partPath))
        {
            ShowInfo("ERROR: AP Launcher download failed!");
            TryDelete(partPath);
            yield break;
        }

        try
        {
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(partPath, finalPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to finalize download: " + e.Message);
            ShowInfo("ERROR: Failed to save AP Launcher\n" + e.Message);
            TryDelete(partPath);
            yield break;
        }

        UnityEngine.Debug.Log("File downloaded successfully: " + finalPath);

        // Launch the exe (it is never closed or deleted by this script)
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = finalPath,
                WorkingDirectory = downloadsDir,
                UseShellExecute = true
            };
            Process.Start(psi);

            ShowInfo("AP Launcher downloaded and started!\n" + finalPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch AP Launcher: " + e.Message);
            ShowInfo("AP Launcher downloaded to:\n" + finalPath + "\n\nBut it could not be started:\n" + e.Message);
        }
    }

    void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete " + path + ": " + e.Message);
        }
    }

    IEnumerator DownloadFile(string url, string savePath, Action<bool> onDone)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            var handler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            handler.removeFileOnAbort = true;
            request.downloadHandler = handler;
            request.timeout = 300;

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
                onDone(false);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
                onDone(true);
            }
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

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("Config load failed: " + request.error);
                configLoaded = true;
                yield break;
            }

            try
            {
                remoteConfig = JsonUtility.FromJson<APLConfig>(request.downloadHandler.text);
                UnityEngine.Debug.Log("Remote config loaded successfully");
                UnityEngine.Debug.Log("aplauncherDL: " + remoteConfig.aplauncherDL);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("Config parsing failed: " + e.Message);
            }
        }

        configLoaded = true;
    }
}