using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using System;
using System.Text;
using System.Text.RegularExpressions;

public class BanjoTooieManualDL : MonoBehaviour
{
    private const string DefaultApworldFileName = "banjo_tooie.apworld";

    private const string BizHawkSettingsFileName = "config.ini";
    private const string BizHawkConfigPresetUrl =
        "https://raw.githubusercontent.com/quackexclamationmark/Archipelago-Setup-Tool/refs/heads/main/RemoteConfig/ConfigFilesPresets/config.in%20(bizhawk)";

    private const string N64CoreKey = "BizHawk.Emulation.Cores.Nintendo.N64.N64";
    private const string N64SyncType = "BizHawk.Emulation.Cores.Nintendo.N64.N64SyncSettings, BizHawk.Emulation.Cores";

    public FileDownloader downloader;

    [Header("BIZHAWK (script réutilisable)")]
    public BizHawkSetupManager bizHawkSetup;

    [Header("GAME FILES")]
    public FileDownloader.FileData banjotooieBizHawk;
    public FileDownloader.FileData banjotooieApworld;

    [Header("FEATURE TOGGLES")]
    public Toggle installBizToggle;
    public Toggle installApworldToggle;

    [Header("INSTALL BUTTON")]
    public Button runSetupButton;

    [Header("CONFIRMATION PANEL (setup)")]
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
    private bool isSetupRunning = false;

    [Serializable]
    public class RemoteConfig
    {
        public string banjotooieBizHawk;
        public string banjotooieApworld;
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

        if (installBizToggle != null)
            installBizToggle.isOn = true;

        if (installApworldToggle != null)
            installApworldToggle.isOn = true;

        if (runSetupButton != null)
            runSetupButton.onClick.AddListener(() => ShowConfirmation("Are you sure you want to setup?", "Setup"));

        if (bizHawkSetup != null)
        {
            if (bizHawkSetup.bizHawkToggle == null)
                bizHawkSetup.bizHawkToggle = installBizToggle;

            bizHawkSetup.Initialize(ShowInfo);
        }
        else
        {
            UnityEngine.Debug.LogWarning("BizHawkSetupManager is not assigned!");
        }

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
            ExecuteSetup();

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
        if (isSetupRunning)
        {
            ShowInfo("Setup is already running!");
            return;
        }

        bool biz = installBizToggle != null && installBizToggle.isOn && installBizToggle.interactable;
        bool apworld = installApworldToggle != null && installApworldToggle.isOn;

        if (!biz && !apworld)
        {
            ShowInfo("Please select at least one install option.");
            return;
        }

        StartCoroutine(SetupFlow(biz, apworld));
    }

    IEnumerator SetupFlow(bool biz, bool apworld)
    {
        isSetupRunning = true;

        try
        {
            if (!configLoaded)
            {
                ShowInfo("Loading configuration, please wait...");
                yield return new WaitUntil(() => configLoaded);
            }

            if (biz)
            {
                if (bizHawkSetup == null)
                {
                    ShowInfo("ERROR: BizHawkSetupManager not assigned!");
                    yield break;
                }

                bool bizOk = false;
                yield return bizHawkSetup.InstallBizHawk(banjotooieBizHawk, ShowInfo, ok => bizOk = ok);

                if (!bizOk)
                    yield break;
            }

            if (bizHawkSetup != null && !string.IsNullOrEmpty(bizHawkSetup.BizHawkDirectory))
            {
                ShowInfo("Configuring BizHawk...");
                yield return new WaitForSeconds(0.5f);

                bool settingsOk = false;
                yield return ApplyBizHawkSettings(ok => settingsOk = ok);

                if (!settingsOk)
                    yield break;
            }

            if (apworld)
            {
                ShowInfo("Installing APWorld...");
                yield return new WaitForSeconds(0.5f);

                yield return InstallAPWorld();

                if (!lastApWorldInstallSuccess)
                    yield break;

                yield return new WaitForSeconds(1f);
            }

            ShowInfo("Installation complete!");
        }
        finally
        {
            isSetupRunning = false;
        }
    }

    IEnumerator InstallAPWorld()
    {
        lastApWorldInstallSuccess = false;

        while (!configLoaded)
        {
            UnityEngine.Debug.Log("Waiting for config to load...");
            yield return new WaitForSeconds(0.5f);
        }

        if (banjotooieApworld == null || string.IsNullOrEmpty(banjotooieApworld.url))
        {
            ShowInfo("ERROR: APWorld URL is empty!");
            UnityEngine.Debug.LogError("APWorld URL not set!");
            yield break;
        }

        string fileName = banjotooieApworld.fileName;
        if (string.IsNullOrEmpty(fileName))
            fileName = DefaultApworldFileName;

        string localPath = Path.Combine(Application.persistentDataPath, fileName);

        UnityEngine.Debug.Log("Downloading APWorld from: " + banjotooieApworld.url);
        UnityEngine.Debug.Log("Saving to: " + localPath);

        yield return DownloadFile(banjotooieApworld.url, localPath);

        if (!File.Exists(localPath))
        {
            UnityEngine.Debug.LogError("Download failed: file not found at " + localPath);
            ShowInfo("ERROR: APWorld download failed!");
            yield break;
        }

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
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to copy APWorld: " + e.Message);
            ShowInfo("ERROR: Failed to install APWorld\n" + e.Message);
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
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Could not delete temporary APWorld file: " + e.Message);
        }
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

                try { if (File.Exists(savePath)) File.Delete(savePath); } catch { }
            }
            else
            {
                UnityEngine.Debug.Log("Download complete! File size: " + new FileInfo(savePath).Length + " bytes");
            }
        }
    }

    IEnumerator DownloadText(string url, Action<string> onDone)
    {
        UnityEngine.Debug.Log("Downloading text from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Text download error: " + request.error + " (code " + request.responseCode + ")");
                onDone(null);
                yield break;
            }

            onDone(request.downloadHandler.text.TrimStart('\uFEFF'));
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

        banjotooieBizHawk.url = remoteConfig.banjotooieBizHawk;
        banjotooieApworld.url = remoteConfig.banjotooieApworld;
        UnityEngine.Debug.Log("Applied banjotooieBizHawk URL: " + banjotooieBizHawk.url);
        UnityEngine.Debug.Log("Applied banjotooieApworld URL: " + banjotooieApworld.url);
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
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    if (drive.DriveType != DriveType.Fixed)
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

    // ------------------------------------------------------------------
    // BizHawk config.ini
    // ------------------------------------------------------------------

    IEnumerator ApplyBizHawkSettings(Action<bool> onComplete)
    {
        string dir = bizHawkSetup != null ? bizHawkSetup.BizHawkDirectory : "";

        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            onComplete(true);
            yield break;
        }

        string path = Path.Combine(dir, BizHawkSettingsFileName);

        // Pas de config.ini : on le crée depuis le preset, sans lancer EmuHawk.
        // S'il existe déjà, on le conserve et on se contente de le corriger.
        if (!File.Exists(path))
        {
            ShowInfo("Creating BizHawk configuration...");

            string preset = null;
            yield return DownloadText(BizHawkConfigPresetUrl, text => preset = text);

            if (string.IsNullOrEmpty(preset) || preset.IndexOf('{') < 0)
            {
                ShowInfo("ERROR: Failed to download the BizHawk configuration preset.");
                onComplete(false);
                yield break;
            }

            string writeError = null;

            try
            {
                File.WriteAllText(path, preset, new UTF8Encoding(false));
                UnityEngine.Debug.Log("BizHawk config.ini created from preset at: " + path);
            }
            catch (Exception e)
            {
                writeError = e.Message;
                UnityEngine.Debug.LogError("Failed to write config.ini: " + e.Message);
            }

            if (writeError != null)
            {
                ShowInfo("ERROR: Failed to create " + BizHawkSettingsFileName + "\n" + writeError);
                onComplete(false);
                yield break;
            }
        }

        onComplete(WriteBizHawkSettings(path));
    }

    bool WriteBizHawkSettings(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            if (text.IndexOf('{') < 0)
                text = "{}";

            string original = text;

            text = SetRootBool(text, "RunInBackground", true);
            text = SetRootBool(text, "AcceptBackgroundInput", true);
            text = SetRootBool(text, "ClockThrottle", true);
            text = SetRootBool(text, "SoundThrottle", true);
            text = EnsureExpansionSlotEnabled(text);

            if (text != original)
                File.WriteAllText(path, text, new UTF8Encoding(false));

            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to update BizHawk settings: " + e.Message);
            ShowInfo("ERROR: Failed to update BizHawk settings\n" + e.Message);
            return false;
        }
    }

    private static string SetRootBool(string text, string key, bool value)
    {
        string literal = value ? "true" : "false";
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)");

        if (m.Success)
        {
            Group g = m.Groups[1];
            if (g.Value == literal)
                return text;
            return text.Substring(0, g.Index) + literal + text.Substring(g.Index + g.Length);
        }

        return InsertIntoObject(text, text.IndexOf('{'), "\"" + key + "\": " + literal);
    }

    private static string EnsureExpansionSlotEnabled(string text)
    {
        Match m = Regex.Match(text, "\"DisableExpansionSlot\"\\s*:\\s*(true|false)");
        if (m.Success)
        {
            Group g = m.Groups[1];
            if (g.Value == "false")
                return text;
            return text.Substring(0, g.Index) + "false" + text.Substring(g.Index + g.Length);
        }

        Match sync = Regex.Match(text, "\"CoreSyncSettings\"\\s*:\\s*\\{");
        if (!sync.Success)
        {
            string entry = "\"CoreSyncSettings\": {\n" + Indent(BuildN64Block(), "  ") + "\n}";
            return InsertIntoObject(text, text.IndexOf('{'), entry);
        }

        int syncBrace = sync.Index + sync.Length - 1;
        int syncEnd = FindMatchingBrace(text, syncBrace);
        if (syncEnd < 0)
            throw new FormatException("Unbalanced braces in CoreSyncSettings");

        string syncBody = text.Substring(syncBrace, syncEnd - syncBrace + 1);
        Match n64 = Regex.Match(syncBody, "\"" + Regex.Escape(N64CoreKey) + "\"\\s*:\\s*\\{");

        if (!n64.Success)
            return InsertIntoObject(text, syncBrace, BuildN64Block());

        int n64Brace = syncBrace + n64.Index + n64.Length - 1;

        Match typeMatch = Regex.Match(text.Substring(n64Brace), "^\\{\\s*\"\\$type\"\\s*:\\s*\"[^\"]*\"");
        if (typeMatch.Success)
        {
            int at = n64Brace + typeMatch.Length;
            string indent = LineIndent(text, n64Brace) + "  ";
            return text.Substring(0, at) + ",\n" + indent + "\"DisableExpansionSlot\": false" + text.Substring(at);
        }

        return InsertIntoObject(text, n64Brace, "\"DisableExpansionSlot\": false");
    }

    private static string BuildN64Block()
    {
        return "\"" + N64CoreKey + "\": {\n" +
               "  \"$type\": \"" + N64SyncType + "\",\n" +
               "  \"DisableExpansionSlot\": false\n" +
               "}";
    }

    private static string Indent(string value, string prefix)
    {
        return prefix + value.Replace("\n", "\n" + prefix);
    }

    private static string InsertIntoObject(string text, int braceIndex, string entry)
    {
        string baseIndent = LineIndent(text, braceIndex);
        string formatted = Indent(entry, baseIndent + "  ");

        int next = braceIndex + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
            next++;

        if (next < text.Length && text[next] == '}')
            return text.Substring(0, braceIndex + 1) + "\n" + formatted + "\n" + baseIndent + text.Substring(next);

        return text.Substring(0, braceIndex + 1) + "\n" + formatted + "," + text.Substring(braceIndex + 1);
    }

    private static string LineIndent(string text, int index)
    {
        int lineStart = index > 0 ? text.LastIndexOf('\n', index - 1) + 1 : 0;
        int i = lineStart;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
            i++;
        return text.Substring(lineStart, i - lineStart);
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        int depth = 0;
        bool inString = false;

        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }
}