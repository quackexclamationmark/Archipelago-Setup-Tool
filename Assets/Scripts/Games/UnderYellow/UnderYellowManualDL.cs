using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UnderYellowManualDL : MonoBehaviour
{
    public FileDownloader downloader;

    [Header("UNDERTALE YELLOW FILES")]
    public FileDownloader.FileData undertaleyellowApworld; // undertale_yellow.apworld
    public FileDownloader.FileData undertaleyellowDL;      // zip containing the game/installer

    [Header("FEATURE TOGGLES")]
    public Toggle installAPWorldToggle;
    public Toggle installUndertaleYellowToggle;

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

    private const string InstalledFoldersKey = "UndertaleYellow_InstalledFolders";

    private string archipelagoPath;
    private string pendingAction;

    void Start()
    {
        archipelagoPath = GetArchipelagoPath();

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (secondLaunchToggle != null)
            secondLaunchToggle.isOn = false;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    // ---------------------------------------------------------------
    // BUTTON ENTRY POINTS
    // ---------------------------------------------------------------

    public void RunSetup()
    {
        ShowConfirmation("Are you sure you want to setup Undertale Yellow?", "Setup");
    }

    public void RevertAll()
    {
        ShowConfirmation("Are you sure you want to remove the installed Undertale Yellow folder?", "Revert");
    }

    private void ShowConfirmation(string message, string action)
    {
        pendingAction = action;
        if (confirmationMessage != null)
            confirmationMessage.text = message;
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (confirmationPanel != null)
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

        pendingAction = "";
    }

    private void OnCancel()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        pendingAction = "";
    }

    // ---------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------

    private void ExecuteSetup()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath))
        {
            ShowInfo("ERROR: Could not find or create the Archipelago folder!");
            return;
        }

        bool apworld = installAPWorldToggle == null || installAPWorldToggle.isOn;
        bool undertaleYellow = installUndertaleYellowToggle == null || installUndertaleYellowToggle.isOn;

        if (!apworld && !undertaleYellow)
        {
            ShowInfo("Please select at least one component to install.");
            return;
        }

        StartCoroutine(InstallFlow(apworld, undertaleYellow));
    }

    IEnumerator InstallFlow(bool apworld, bool undertaleYellow)
    {
        if (apworld)
        {
            ShowInfo("Installing Undertale Yellow APWorld...");
            yield return InstallAPWorld();
        }

        if (undertaleYellow)
        {
            ShowInfo("Downloading Undertale Yellow...");
            yield return InstallUndertaleYellowDL();
        }

        if (secondLaunchToggle == null || secondLaunchToggle.isOn)
        {
            ShowInfo("Launching Undertale Yellow...");
            yield return new WaitForSeconds(1f);
            LaunchUndertaleYellow();
        }
        else
        {
            ShowInfo("Installation complete!");
        }
    }

    IEnumerator InstallAPWorld()
    {
        string url = undertaleyellowApworld.url;

        if (string.IsNullOrEmpty(url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = undertaleyellowApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = url.Substring(url.LastIndexOf('/') + 1);
            if (fileName.Contains("?"))
                fileName = fileName.Substring(0, fileName.IndexOf("?"));
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + url);

        yield return DownloadFile(url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

        string customWorldsDir = Path.Combine(archipelagoPath, "custom_worlds");

        try
        {
            if (!Directory.Exists(customWorldsDir))
                Directory.CreateDirectory(customWorldsDir);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Cannot create custom_worlds directory: " + e.Message);
            ShowInfo("ERROR: Cannot create custom_worlds folder!\n" + e.Message);
            yield break;
        }

        string target = Path.Combine(customWorldsDir, fileName);

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
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
            yield break;
        }

        try
        {
            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
        }
    }

    IEnumerator InstallUndertaleYellowDL()
    {
        UnityEngine.Debug.Log("START InstallUndertaleYellowDL");

        if (string.IsNullOrEmpty(undertaleyellowDL.url))
        {
            ShowInfo("ERROR: Download URL is empty!");
            yield break;
        }

        string extractPath = Path.Combine(Application.persistentDataPath, "UndertaleYellow_Extract");

        yield return downloader.DownloadAndExtract(undertaleyellowDL, Application.persistentDataPath, extractPath);

        if (!Directory.Exists(extractPath))
        {
            UnityEngine.Debug.LogError("Extraction failed: " + extractPath + " does not exist");
            ShowInfo("ERROR: Download/extraction failed!");
            yield break;
        }

        // Remember which top-level folder(s) came out of the zip, so Revert/Launch
        // can find them again even if the version (and therefore the folder name) changes.
        string[] topLevelFolders = GetTopLevelDirNames(extractPath);

        // Copy the extracted content directly into the Archipelago root folder
        // (one level above custom_worlds), NOT into custom_worlds.
        CopyExtractedFiles(extractPath, archipelagoPath);

        if (topLevelFolders.Length > 0)
        {
            PlayerPrefs.SetString(InstalledFoldersKey, string.Join(",", topLevelFolders));
            PlayerPrefs.Save();
        }

        SafeDeleteDirectory(extractPath);

        UnityEngine.Debug.Log("END InstallUndertaleYellowDL");
        ShowInfo("Undertale Yellow installed successfully!");
    }

    void CopyExtractedFiles(string extractPath, string targetPath)
    {
        UnityEngine.Debug.Log("START CopyExtractedFiles: " + extractPath + " -> " + targetPath);

        try
        {
            foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(targetPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file, dest);
            }

            UnityEngine.Debug.Log("All extracted files copied to Archipelago directory");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error copying extracted files: " + e.Message);
        }

        UnityEngine.Debug.Log("END CopyExtractedFiles");
    }

    string[] GetTopLevelDirNames(string path)
    {
        try
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            string[] names = new string[dirs.Length];
            for (int i = 0; i < dirs.Length; i++)
                names[i] = Path.GetFileName(dirs[i]);
            return names;
        }
        catch
        {
            return new string[0];
        }
    }

    // ---------------------------------------------------------------
    // REVERT (only removes the folder placed into Archipelago by the DL zip)
    // ---------------------------------------------------------------

    private void ExecuteRevert()
    {
        archipelagoPath = GetArchipelagoPath();

        if (string.IsNullOrEmpty(archipelagoPath) || !Directory.Exists(archipelagoPath))
        {
            ShowInfo("Archipelago folder not found.");
            return;
        }

        ShowInfo("Removing Undertale Yellow files...");

        bool removedSomething = false;

        string stored = PlayerPrefs.GetString(InstalledFoldersKey, "");
        if (!string.IsNullOrEmpty(stored))
        {
            string[] folders = stored.Split(',');
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string fullPath = Path.Combine(archipelagoPath, folder);
                if (Directory.Exists(fullPath))
                {
                    SafeDeleteDirectory(fullPath);
                    removedSomething = true;
                }
            }

            PlayerPrefs.DeleteKey(InstalledFoldersKey);
            PlayerPrefs.Save();
        }

        if (!removedSomething)
        {
            // Fallback if we have no record (e.g. fresh app run): look for a folder
            // whose name still looks like an Undertale Yellow install.
            try
            {
                string[] dirs = Directory.GetDirectories(archipelagoPath);
                foreach (string dir in dirs)
                {
                    string name = Path.GetFileName(dir).ToLower();
                    if (name.Contains("undertale") && name.Contains("yellow"))
                    {
                        SafeDeleteDirectory(dir);
                        removedSomething = true;
                    }
                }
            }
            catch { }
        }

        if (removedSomething)
            ShowInfo("Undertale Yellow files removed successfully!");
        else
            ShowInfo("No Undertale Yellow installation found to remove.");
    }

    // ---------------------------------------------------------------
    // LAUNCH
    // ---------------------------------------------------------------

    void LaunchUndertaleYellow()
    {
        try
        {
            string exePath = FindUndertaleYellowExecutable();

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(exePath);
                UnityEngine.Debug.Log("Undertale Yellow launched: " + exePath);
            }
            else
            {
                UnityEngine.Debug.LogError("Undertale Yellow.exe not found under: " + archipelagoPath);
                ShowInfo("ERROR: Undertale Yellow.exe not found!");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch Undertale Yellow: " + e.Message);
            ShowInfo("ERROR: Failed to launch Undertale Yellow!");
        }
    }

    // Locates the exe dynamically since the install folder / exe name changes with game version
    // (e.g. "Archipelago\Undertale Yellow v-1-3-1\Undertale Yellow.exe" today, something else tomorrow).
    string FindUndertaleYellowExecutable()
    {
        if (string.IsNullOrEmpty(archipelagoPath) || !Directory.Exists(archipelagoPath))
            return "";

        string stored = PlayerPrefs.GetString(InstalledFoldersKey, "");
        if (!string.IsNullOrEmpty(stored))
        {
            string[] folders = stored.Split(',');
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder))
                    continue;

                string exe = FindExeInFolder(Path.Combine(archipelagoPath, folder));
                if (!string.IsNullOrEmpty(exe))
                    return exe;
            }
        }

        // Fallback: scan the Archipelago root for a folder that looks like Undertale Yellow
        try
        {
            string[] dirs = Directory.GetDirectories(archipelagoPath);
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir).ToLower();
                if (name.Contains("undertale") && name.Contains("yellow"))
                {
                    string exe = FindExeInFolder(dir);
                    if (!string.IsNullOrEmpty(exe))
                        return exe;
                }
            }
        }
        catch { }

        return "";
    }

    string FindExeInFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return "";

            string[] exes = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);

            // Prefer an exe whose name actually says "Undertale Yellow"
            foreach (string exe in exes)
            {
                string name = Path.GetFileNameWithoutExtension(exe).ToLower();
                if (name.Contains("undertale") && name.Contains("yellow"))
                    return exe;
            }

            if (exes.Length > 0)
                return exes[0];
        }
        catch { }

        return "";
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------

    IEnumerator DownloadFile(string url, string savePath)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error);
                UnityEngine.Debug.LogError("Response code: " + request.responseCode);
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
        }
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

    // Finds (or creates) the Archipelago root folder, e.g. C:\ProgramData\Archipelago
    string GetArchipelagoPath()
    {
        string[] candidates = new string[]
        {
            @"C:\ProgramData\Archipelago",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Archipelago"),
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Archipelago"),
        };

        // Prefer an Archipelago folder that already exists
        foreach (string candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    UnityEngine.Debug.Log("Found Archipelago folder at: " + candidate);
                    return candidate;
                }
            }
            catch { }
        }

        // Otherwise create the first one we're able to
        foreach (string candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                UnityEngine.Debug.Log("Created Archipelago folder at: " + candidate);
                return candidate;
            }
            catch { }
        }

        UnityEngine.Debug.LogWarning("Could not find or create an Archipelago folder.");
        return "";
    }
}