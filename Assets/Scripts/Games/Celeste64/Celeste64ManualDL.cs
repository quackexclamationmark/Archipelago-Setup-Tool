using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Celeste64ManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("GAME FILES")]
    public FileDownloader.FileData celeste64AP;

    [Header("INSTALL FOLDER")]
    public string installFolderName = "Celeste64 Archipelago";

    [Header("FEATURE TOGGLES")]
    public Toggle installAPModToggle;

    [Header("CONFIRMATION PANEL")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationMessage;
    public Button confirmButton;
    public Button cancelButton;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string installPath;
    private string pendingAction;
    private GameConfig remoteConfig;
    private bool configLoaded = false;

    [System.Serializable]
    public class GameConfig
    {
        public string celeste64AP;
    }

    void Start()
    {
        installPath = GetGamePath();
        StartCoroutine(LoadRemoteConfig());

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (installAPModToggle != null)
            installAPModToggle.isOn = true;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    void ApplyGameConfig()
    {
        if (remoteConfig == null)
            return;

        celeste64AP.url = remoteConfig.celeste64AP;
    }

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to revert?", "Revert");
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
        installPath = GetGamePath();

        if (!configLoaded)
        {
            ShowInfo("Loading configuration, please wait...");
            StartCoroutine(WaitForConfigThenSetup());
            return;
        }

        bool installAPMod = installAPModToggle == null || installAPModToggle.isOn;

        if (!installAPMod)
        {
            ShowInfo("Please select at least one option to install.");
            return;
        }

        StartCoroutine(SetupWithTracking());
    }

    IEnumerator SetupWithTracking()
    {
        ShowInfo("Downloading and installing files...");

        yield return InstallFlow();

        ShowInfo("Installation complete!");
        yield return new WaitForSeconds(1f);
    }

    private void ExecuteRevert()
    {
        installPath = GetGamePath();

        if (!Directory.Exists(installPath))
        {
            ShowInfo("No installation found. Cannot revert.\nPlease reinstall the mod if needed.");
            return;
        }

        StartCoroutine(RemoveInstalledFilesAsync());
    }

    IEnumerator RemoveInstalledFilesAsync()
    {
        ShowInfo("Reverting...");
        yield return new WaitForSeconds(0.5f);

        try
        {
            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath, true);
                ShowInfo("Revert complete!");
                UnityEngine.Debug.Log("Revert complete: deleted " + installPath);
            }
            else
            {
                ShowInfo("Folder not found, nothing to remove.");
            }
        }
        catch (System.Exception e)
        {
            ShowInfo("Error during revert:\n" + e.Message);
            UnityEngine.Debug.LogError("Revert error: " + e);
        }
    }

    IEnumerator InstallFlow()
    {
        bool installAPMod = installAPModToggle == null || installAPModToggle.isOn;

        if (installAPMod)
        {
            yield return InstallArchipelagoMod();
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator InstallArchipelagoMod()
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "Celeste64ModFilesTemp");

        yield return downloader.DownloadAndExtract(celeste64AP, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            ShowInfo("ERROR: Extraction failed, folder not found!");
            yield break;
        }

        MoveDirectory(extractPath, installPath);

        SafeDeleteDirectory(extractPath);

        ShowInfo("Installation verified successfully!");
        yield return new WaitForSeconds(1f);
    }

    void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string dest = file.Replace(source, target);

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (File.Exists(dest))
                File.Delete(dest);

            File.Move(file, dest);
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

    IEnumerator WaitForConfigThenSetup()
    {
        while (!configLoaded)
            yield return new WaitForSeconds(0.1f);

        CloseInfoPanel();
        ShowConfirmation("Are you sure you want to setup?", "Setup");
    }

    // =========================================================
    // GAME PATH (used by OpenDetectedGameFolder via reflection)
    // =========================================================

    public string GetGamePath()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(documentsPath, installFolderName);

        return path;
    }

    public void LaunchGame()
    {
        string exePath = Path.Combine(GetGamePath(), "Celeste64.exe");

        if (!File.Exists(exePath))
        {
            ShowInfo("Game executable not found:\n" + exePath);
            UnityEngine.Debug.LogError("LaunchGame: executable not found at " + exePath);
            return;
        }

        try
        {
            Process.Start(exePath);
            UnityEngine.Debug.Log("Game launched from: " + exePath);
        }
        catch (System.Exception e)
        {
            ShowInfo("Error launching game:\n" + e.Message);
            UnityEngine.Debug.LogError("Launch error: " + e);
        }
    }

    IEnumerator LoadRemoteConfig()
    {
        string url = "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/config.json";

        UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Config load failed: " + request.error);
            configLoaded = true;
            yield break;
        }

        try
        {
            remoteConfig = JsonUtility.FromJson<GameConfig>(request.downloadHandler.text);
            UnityEngine.Debug.Log("Remote config loaded successfully");
            ApplyGameConfig();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to parse config: " + e.Message);
        }

        configLoaded = true;

        installPath = GetGamePath();
    }
}