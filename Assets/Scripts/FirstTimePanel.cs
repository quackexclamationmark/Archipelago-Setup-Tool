using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class FirstTimePanel : MonoBehaviour
{
    [Header("UI REFERENCES")]
    public GameObject firstTimePanel;
    public Button okButton;
    public Button downloadButton;
    public Button showFirstTimePanelButton;  // ← NOUVEAU
    public GameObject setupPanel;
    public GameObject revertPanel;
    public GameObject infoPanel;

    [Header("DOWNLOAD")]
    public FileDownloader downloader;

    private FirstTimePanelConfig remoteConfig;
    private bool configLoaded = false;
    private bool hasSeenFirstTime = false;
    private string playerPrefKey;

    [System.Serializable]
    public class FirstTimePanelConfig
    {
        public string r2modmanDL;
    }

    void Start()
    {
        playerPrefKey = "FirstTime_LethalCompany";
        hasSeenFirstTime = PlayerPrefs.GetInt(playerPrefKey, 0) == 1;

        if (okButton != null)
            okButton.onClick.AddListener(CloseFirstTimePanel);

        if (downloadButton != null)
            downloadButton.onClick.AddListener(OnDownloadClicked);

        // ← NOUVEAU - le bouton du setupPanel
        if (showFirstTimePanelButton != null)
            showFirstTimePanelButton.onClick.AddListener(ShowFirstTimePanelAgain);

        StartCoroutine(LoadRemoteConfig());
    }

    void OnEnable()
    {
        if (!hasSeenFirstTime && firstTimePanel != null)
        {
            firstTimePanel.SetActive(true);
            setupPanel?.SetActive(false);
            revertPanel?.SetActive(false);
        }
        else
        {
            firstTimePanel?.SetActive(false);
            setupPanel?.SetActive(true);
            revertPanel?.SetActive(true);
        }
    }

    void OnDisable()
    {
        firstTimePanel?.SetActive(false);
        setupPanel?.SetActive(false);
        revertPanel?.SetActive(false);
        infoPanel?.SetActive(false);
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Config load failed");
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<FirstTimePanelConfig>(request.downloadHandler.text);
            Debug.Log("Config loaded");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Config parsing failed: " + e.Message);
        }

        configLoaded = true;
    }

    void CloseFirstTimePanel()
    {
        firstTimePanel?.SetActive(false);
        setupPanel?.SetActive(true);
        revertPanel?.SetActive(true);

        hasSeenFirstTime = true;
        PlayerPrefs.SetInt(playerPrefKey, 1);
        PlayerPrefs.Save();
    }

    // ← NOUVEAU - réaffiche le firstTimePanel
    void ShowFirstTimePanelAgain()
    {
        firstTimePanel?.SetActive(true);
        setupPanel?.SetActive(false);
        revertPanel?.SetActive(false);
    }

    void OnDownloadClicked()
    {
        if (!configLoaded)
        {
            ShowMessage("Config is loading...");
            return;
        }

        if (remoteConfig == null || string.IsNullOrEmpty(remoteConfig.r2modmanDL))
        {
            ShowMessage("ERROR: URL not found!");
            return;
        }

        StartCoroutine(DownloadR2modman());
    }

    IEnumerator DownloadR2modman()
    {
        string downloadPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads"
        );

        FileDownloader.FileData r2modmanFile = new FileDownloader.FileData
        {
            url = remoteConfig.r2modmanDL,
            fileName = "ebkr-r2modman.zip"
        };

        ShowMessage("Downloading r2modman...");
        yield return downloader.DownloadToFolder(r2modmanFile, downloadPath);

        ShowMessage("Downloaded successfully!");
        yield return new WaitForSeconds(2f);

        CloseFirstTimePanel();
    }

    void ShowMessage(string message)
    {
        if (infoPanel != null)
        {
            TextMeshProUGUI text = infoPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = message;
                infoPanel.SetActive(true);
            }
        }
    }
}