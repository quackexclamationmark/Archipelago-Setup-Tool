using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;

public class DownloadPanelSimple : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("PANEL")]
    public GameObject downloadPanel;
    public Button triggerButton;
    public Button downloadButton;
    public Button okButton;
    public Button closeButton;
    public TextMeshProUGUI descriptionText;

    [Header("CONFIRMATION")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    private bool hasBeenShown = false;
    private string extractFolderPath;
    private string r2modmanDownloadUrl;
    private bool configLoaded = false;

    [System.Serializable]
    public class DownloadConfig
    {
        public string r2modmanDL;
    }

    void Start()
    {
        if (downloadPanel != null)
            downloadPanel.SetActive(false);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        // Charger la config distante
        StartCoroutine(LoadRemoteConfig());

        if (triggerButton != null)
            triggerButton.onClick.AddListener(OnTriggerButtonClicked);

        if (downloadButton != null)
            downloadButton.onClick.AddListener(OnDownloadClicked);

        if (okButton != null)
            okButton.onClick.AddListener(OnOkClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmDownload);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelDownload);
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Config load failed: " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            DownloadConfig config = JsonUtility.FromJson<DownloadConfig>(request.downloadHandler.text);
            r2modmanDownloadUrl = config.r2modmanDL;
            Debug.Log("r2modman URL loaded: " + r2modmanDownloadUrl);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Config parsing failed: " + e.Message);
        }

        configLoaded = true;
    }

    void OnTriggerButtonClicked()
    {
        if (hasBeenShown)
            return;

        hasBeenShown = true;
        ShowDownloadPanel();
    }

    void ShowDownloadPanel()
    {
        if (downloadPanel != null)
            downloadPanel.SetActive(true);
    }

    void OnDownloadClicked()
    {
        if (confirmationPanel != null)
        {
            confirmationMessage.text = "Télécharger r2modman?";
            confirmationPanel.SetActive(true);
        }
    }

    void OnConfirmDownload()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        StartCoroutine(DownloadAndExtractFlow());
    }

    void OnCancelDownload()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }

    IEnumerator DownloadAndExtractFlow()
    {
        // Attendre que la config soit chargée
        while (!configLoaded)
        {
            Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (string.IsNullOrEmpty(r2modmanDownloadUrl))
        {
            confirmationMessage.text = "ERREUR: URL r2modman vide!";
            confirmationPanel.SetActive(true);
            yield break;
        }

        extractFolderPath = Path.Combine(Application.persistentDataPath, "r2modman");
        string zipFolder = Application.persistentDataPath;

        FileDownloader.FileData r2modmanFile = new FileDownloader.FileData
        {
            url = r2modmanDownloadUrl,
            fileName = "r2modman.zip"
        };

        yield return downloader.DownloadAndExtract(r2modmanFile, zipFolder, extractFolderPath);

        if (confirmationPanel != null)
        {
            confirmationMessage.text = "Téléchargement r2modman terminé!";
            confirmationPanel.SetActive(true);
        }
    }

    void OnOkClicked()
    {
        ClosePanel();
    }

    void ClosePanel()
    {
        if (downloadPanel != null)
            downloadPanel.SetActive(false);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }
}