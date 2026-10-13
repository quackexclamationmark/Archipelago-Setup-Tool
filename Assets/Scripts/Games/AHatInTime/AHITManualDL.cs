using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AHITManualDL : MonoBehaviour
{
    [Header("GAME FOLDER NAMES")]
    public string steamGameFolderName = "HatinTime";

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("BUTTONS")]
    public Button launchButton;
    public Button workshopButton;

    [Header("STEAM WORKSHOP")]
    [SerializeField]
    private string steamWorkshopUrl = "steam://url/CommunityFilePage/3026842601";

    private AHITConfig remoteConfig;

    [System.Serializable]
    public class AHITConfig
    {
        public string[] steamSearchPaths;
    }

    void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (launchButton != null)
            launchButton.onClick.AddListener(OnLaunchButtonClicked);

        if (workshopButton != null)
            workshopButton.onClick.AddListener(OpenSteamWorkshopPage);
    }

    // ---------------------------------------------------------------
    // LAUNCH BUTTON HANDLER
    // ---------------------------------------------------------------

    public void OnLaunchButtonClicked()
    {
        ShowInfo("Launching A Hat in Time...");
        LaunchGame();
    }

    // ---------------------------------------------------------------
    // STEAM WORKSHOP
    // ---------------------------------------------------------------

    public void OpenSteamWorkshopPage()
    {
        if (string.IsNullOrEmpty(steamWorkshopUrl))
            return;

        try
        {
            Application.OpenURL(steamWorkshopUrl);
            UnityEngine.Debug.Log("Opened Steam Workshop page: " + steamWorkshopUrl);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Failed to open Steam Workshop page: " + e.Message);
        }
    }

    // ---------------------------------------------------------------
    // LAUNCH (HatinTime/Binaries/Win64/HatinTimeGame.exe)
    // ---------------------------------------------------------------

    void LaunchGame()
    {
        try
        {
            string gameDir = GetGamePath();

            if (string.IsNullOrEmpty(gameDir))
            {
                UnityEngine.Debug.LogError("A Hat in Time folder not found.");
                ShowInfo("A Hat in Time folder not found! Make sure the game is installed.");
                return;
            }

            string exeDir = Path.Combine(gameDir, "Binaries", "Win64");
            string exePath = Path.Combine(exeDir, "HatinTimeGame.exe");

            if (!File.Exists(exePath))
            {
                UnityEngine.Debug.LogError("HatinTimeGame.exe not found at: " + exePath);
                ShowInfo("HatinTimeGame.exe not found!\n" + exePath);
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = exeDir,
                UseShellExecute = false
            };

            Process.Start(startInfo);
            UnityEngine.Debug.Log("A Hat in Time started from: " + exePath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch A Hat in Time: " + e.Message);
            ShowInfo("ERROR: Failed to launch A Hat in Time!\n" + e.Message);
        }
    }

    string GetGamePath()
    {
        string[] quickPaths = new string[]
        {
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", steamGameFolderName),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", steamGameFolderName),
        };

        foreach (string path in quickPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    UnityEngine.Debug.Log("Found Game (Steam) at: " + path);
                    return path;
                }
            }
            catch { }
        }

        if (remoteConfig != null && remoteConfig.steamSearchPaths != null)
        {
            try
            {
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (System.IO.DriveInfo drive in drives)
                {
                    if (drive.DriveType != System.IO.DriveType.Fixed)
                        continue;

                    foreach (string relativePath in remoteConfig.steamSearchPaths)
                    {
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        try
                        {
                            string path = Path.Combine(drive.Name, relativePath, steamGameFolderName);
                            if (Directory.Exists(path))
                            {
                                UnityEngine.Debug.Log("Found Game (Steam, via remote config) at: " + path);
                                return path;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Game (Steam) not found.");
        return "";
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

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
}