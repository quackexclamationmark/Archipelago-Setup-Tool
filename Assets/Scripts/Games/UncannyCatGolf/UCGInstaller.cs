using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.IO.Compression;
using System.Collections;

public class UCGInstaller : MonoBehaviour
{
    [Header("INSTALL BUTTON")]
    public Button installButton;

    [Header("CONFIRMATION PANEL - DL")]
    public GameObject dlConfirmationPanel;
    public TextMeshProUGUI dlConfirmationMessage;
    public Button dlConfirmButton;
    public Button dlCancelButton;

    [Header("INFO PANEL - DL")]
    public GameObject dlInfoPanel;
    public TextMeshProUGUI dlInfoText;
    public Button dlInfoOkButton;

    private UCGConfig remoteConfig;
    private bool configLoaded = false;
    private string uncannycatDL;

    private const string InstallFolderName = "Uncanny Cat Golf";

    [System.Serializable]
    public class UCGConfig
    {
        public string uncannycatDL;
    }

    void Start()
    {
        StartCoroutine(LoadRemoteConfig());

        if (dlInfoPanel != null)
            dlInfoPanel.SetActive(false);

        if (dlInfoOkButton != null)
            dlInfoOkButton.onClick.AddListener(CloseDLInfoPanel);

        if (dlConfirmationPanel != null)
            dlConfirmationPanel.SetActive(false);

        if (dlConfirmButton != null)
            dlConfirmButton.onClick.AddListener(OnDLConfirm);

        if (dlCancelButton != null)
            dlCancelButton.onClick.AddListener(OnDLCancel);

        if (installButton != null)
            installButton.onClick.AddListener(RunInstall);
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogWarning("Config load failed: " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<UCGConfig>(request.downloadHandler.text);

            if (!string.IsNullOrEmpty(remoteConfig.uncannycatDL))
            {
                uncannycatDL = remoteConfig.uncannycatDL;
                UnityEngine.Debug.Log("Uncanny Cat Golf Download URL from config: " + uncannycatDL);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Config parsing failed: " + e.Message);
        }

        configLoaded = true;
    }

    string GetInstallPath()
    {
        return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), InstallFolderName);
    }

    public void RunInstall()
    {
        dlConfirmationMessage.text = "Are you sure you want to install Uncanny Cat Golf?";
        dlConfirmationPanel.SetActive(true);
    }

    private void OnDLConfirm()
    {
        dlConfirmationPanel.SetActive(false);
        StartCoroutine(InstallFlow());
    }

    private void OnDLCancel()
    {
        dlConfirmationPanel.SetActive(false);
    }

    IEnumerator InstallFlow()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(uncannycatDL))
        {
            ShowDLInfo("ERROR: Uncanny Cat Golf download URL not configured!");
            yield break;
        }

        ShowDLInfo("Downloading Uncanny Cat Golf...");
        yield return new WaitForSeconds(0.5f);

        yield return DownloadAndExtract();
    }

    IEnumerator DownloadAndExtract()
    {
        string targetFolder = GetInstallPath();
        string tempZipPath = Path.Combine(Path.GetTempPath(), "uncannycat_" + System.Guid.NewGuid().ToString("N") + ".zip");

        UnityEngine.Debug.Log("Downloading Uncanny Cat Golf from: " + uncannycatDL);
        UnityEngine.Debug.Log("Temp zip path: " + tempZipPath);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(uncannycatDL))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(tempZipPath);
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                ShowDLInfo("ERROR: Uncanny Cat Golf download failed!");
                yield break;
            }
        }

        if (!File.Exists(tempZipPath))
        {
            ShowDLInfo("ERROR: Uncanny Cat Golf download failed!");
            yield break;
        }

        ShowDLInfo("Extracting Uncanny Cat Golf...");
        yield return null;

        bool failed = false;
        string errorMessage = "";

        try
        {
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);
            else if (Directory.GetFileSystemEntries(targetFolder).Length > 0)
            {
                Directory.Delete(targetFolder, true);
                Directory.CreateDirectory(targetFolder);
            }

            ZipFile.ExtractToDirectory(tempZipPath, targetFolder);
            UnityEngine.Debug.Log("Extraction complete to: " + targetFolder);
        }
        catch (System.Exception e)
        {
            failed = true;
            errorMessage = e.Message;
            UnityEngine.Debug.LogError("Extraction failed: " + e.Message);
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }

        if (failed)
        {
            ShowDLInfo("ERROR: Extraction failed - " + errorMessage);
            yield break;
        }

        ShowDLInfo("Uncanny Cat Golf installed successfully at:\n" + targetFolder);
    }

    void ShowDLInfo(string message)
    {
        if (dlInfoPanel == null || dlInfoText == null)
            return;

        dlInfoText.text = message;
        dlInfoPanel.SetActive(true);
    }

    void CloseDLInfoPanel()
    {
        if (dlInfoPanel != null)
            dlInfoPanel.SetActive(false);
    }
}