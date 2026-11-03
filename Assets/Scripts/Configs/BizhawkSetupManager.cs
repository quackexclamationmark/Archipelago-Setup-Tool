using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;

public class BizHawkSetupManager : MonoBehaviour
{
    [Header("DOWNLOADER (fallback d'extraction, optionnel)")]
    public FileDownloader downloader;

    [Header("TOGGLE BIZHAWK (celui du setup panel)")]
    public Toggle bizHawkToggle;

    [Header("PANEL 1 : CONFIRMATION BIZHAWK")]
    public GameObject bizHawkConfirmPanel;
    public TextMeshProUGUI bizHawkConfirmText;
    public Button yesButton;
    public Button noButton;

    [Header("PANEL 2 : SELECTION DU DOSSIER / EmuHawk.exe")]
    public GameObject selectPanel;
    public Button browseButton;
    public TMP_InputField pathInputField;
    public TextMeshProUGUI pathPlaceholder;
    public TextMeshProUGUI statusText;
    public Button validateButton;
    public Button backButton;

    [Header("FILE BROWSER")]
    public UISkin darkSkin;

    [Header("INSTALL")]
    public string installFolderName = "BizHawk";
    public string confirmMessage = "Do you already have BizHawk Emulator installed?";

    private Action<string> showInfo;
    private bool listenersAdded = false;
    private bool isLocked = false;
    private string pendingDirectory = "";

    private class ThreadTask
    {
        public volatile bool Complete = false;
        public Exception Exception = null;
    }

    public bool IsBizHawkLocked { get { return isLocked; } }

    public string BizHawkDirectory
    {
        get
        {
            bool installed; string dir;
            Read(out installed, out dir);
            return installed ? dir : "";
        }
    }

    public string EmuHawkPath
    {
        get
        {
            string dir = BizHawkDirectory;
            return string.IsNullOrEmpty(dir) ? "" : Path.Combine(dir, EmuHawkExe);
        }
    }

    public void Initialize(Action<string> infoCallback)
    {
        showInfo = infoCallback;

        if (!listenersAdded)
        {
            listenersAdded = true;

            if (yesButton != null) yesButton.onClick.AddListener(OnYes);
            if (noButton != null) noButton.onClick.AddListener(OnNo);
            if (browseButton != null) browseButton.onClick.AddListener(() => StartCoroutine(ShowFileBrowser()));
            if (validateButton != null) validateButton.onClick.AddListener(OnValidate);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (pathInputField != null) pathInputField.onValueChanged.AddListener(OnPathTyped);
        }

        if (bizHawkConfirmPanel != null) bizHawkConfirmPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);
        if (validateButton != null) validateButton.gameObject.SetActive(false);

        EnsureEntries();

        bool installed; string dir;
        Read(out installed, out dir);

        if (installed && !string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            UnityEngine.Debug.LogWarning("Saved BizHawk directory no longer exists: " + dir);
            SetState(false, "");
            dir = "";
        }

        RefreshState();

        bool known = installed && !string.IsNullOrEmpty(dir);
        if (!known)
            OpenConfirmPanel();
    }

    public void RefreshState()
    {
        bool installed; string dir;
        Read(out installed, out dir);

        isLocked = installed && !string.IsNullOrEmpty(dir);

        if (bizHawkToggle != null)
        {
            bizHawkToggle.interactable = !isLocked;
            if (isLocked)
                bizHawkToggle.SetIsOnWithoutNotify(false);
        }
    }

    void OpenConfirmPanel()
    {
        if (bizHawkConfirmText != null)
            bizHawkConfirmText.text = confirmMessage;
        if (bizHawkConfirmPanel != null)
            bizHawkConfirmPanel.SetActive(true);
    }

    void OnYes()
    {
        pendingDirectory = "";
        if (validateButton != null) validateButton.gameObject.SetActive(false);
        if (pathInputField != null) pathInputField.SetTextWithoutNotify("");
        if (pathPlaceholder != null) pathPlaceholder.gameObject.SetActive(true);
        SetStatus("");

        if (selectPanel != null)
            selectPanel.SetActive(true);
    }

    void OnNo()
    {
        if (bizHawkConfirmPanel != null) bizHawkConfirmPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);
    }

    void OnBack()
    {
        if (selectPanel != null) selectPanel.SetActive(false);
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(pendingDirectory))
            return;

        SetState(true, pendingDirectory);
        UnityEngine.Debug.Log("BizHawk directory saved: " + pendingDirectory);

        if (selectPanel != null) selectPanel.SetActive(false);
        if (bizHawkConfirmPanel != null) bizHawkConfirmPanel.SetActive(false);

        RefreshState();
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
            FileBrowser.Skin = darkSkin;

        string startPath = "C:\\";
        if (pathInputField != null && Directory.Exists(pathInputField.text))
            startPath = pathInputField.text;

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.FilesAndFolders,
            false,
            startPath,
            null,
            "Select EmuHawk.exe or the BizHawk directory");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selected = FileBrowser.Result[0];
            string resolved;

            if (TryResolveDirectory(selected, out resolved))
            {
                if (pathInputField != null)
                    pathInputField.SetTextWithoutNotify(resolved);
                if (pathPlaceholder != null)
                    pathPlaceholder.gameObject.SetActive(false);

                ApplyPath(resolved);
            }
            else
            {
                if (pathInputField != null)
                    pathInputField.SetTextWithoutNotify(selected);
                if (pathPlaceholder != null)
                    pathPlaceholder.gameObject.SetActive(false);

                ApplyPath(selected);
            }
        }
    }

    void OnPathTyped(string value)
    {
        ApplyPath(value);
    }

    void ApplyPath(string value)
    {
        string resolved;

        if (TryResolveDirectory(value, out resolved))
        {
            pendingDirectory = resolved;
            if (validateButton != null) validateButton.gameObject.SetActive(true);
            SetStatus("EmuHawk.exe found.");
        }
        else
        {
            pendingDirectory = "";
            if (validateButton != null) validateButton.gameObject.SetActive(false);

            if (!string.IsNullOrWhiteSpace(value))
                Warn("EmuHawk.exe not found in the selected location.");
            else
                SetStatus("");
        }
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    void Warn(string message)
    {
        if (statusText != null)
            statusText.text = message;
        else if (showInfo != null)
            showInfo(message);
    }

    public IEnumerator InstallBizHawk(FileDownloader.FileData file, Action<string> info, Action<bool> onComplete)
    {
        Action<string> say = info ?? showInfo ?? (_ => { });

        if (file == null || string.IsNullOrEmpty(file.url))
        {
            Fail(say, onComplete, "ERROR: BizHawk URL is empty!");
            yield break;
        }

        string fileName = file.fileName;
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = file.url.Substring(file.url.LastIndexOf('/') + 1);
            int q = fileName.IndexOf('?');
            if (q >= 0) fileName = fileName.Substring(0, q);
        }

        string localPath = Path.Combine(Application.persistentDataPath, fileName);
        string extractPath = Path.Combine(Application.persistentDataPath, "BizHawkTemp");

        say("Downloading BizHawk...");
        bool downloaded = false;
        yield return DownloadFile(file.url, localPath, ok => downloaded = ok);

        if (!downloaded || !File.Exists(localPath))
        {
            SafeDeleteFile(localPath);
            Fail(say, onComplete, "ERROR: BizHawk download failed!");
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        yield return null;
        Directory.CreateDirectory(extractPath);

        say("Extracting BizHawk...");
        ThreadTask extractTask = StartZipExtraction(localPath, extractPath);
        while (!extractTask.Complete)
            yield return new WaitForSeconds(0.1f);

        if (extractTask.Exception != null)
        {
            UnityEngine.Debug.LogWarning("Zip extraction failed: " + extractTask.Exception.Message);

            if (downloader == null)
            {
                SafeDeleteFile(localPath);
                SafeDeleteDirectory(extractPath);
                Fail(say, onComplete, "ERROR: Extraction failed.\n" + extractTask.Exception.Message);
                yield break;
            }

            yield return downloader.DownloadAndExtract(file, Application.persistentDataPath, extractPath);
        }

        if (!Directory.Exists(extractPath) ||
            (Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories).Length == 0 &&
             Directory.GetDirectories(extractPath).Length == 0))
        {
            SafeDeleteFile(localPath);
            Fail(say, onComplete, "ERROR: Extraction produced no files.");
            yield break;
        }

        string sourcePath = extractPath;
        string[] topDirs = Directory.GetDirectories(extractPath);
        string[] topFiles = Directory.GetFiles(extractPath);
        if (topDirs.Length == 1 && topFiles.Length == 0)
            sourcePath = topDirs[0];

        string targetPath = DefaultInstallDirectory(installFolderName);

        SafeDeleteDirectory(targetPath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            try
            {
                Directory.Move(sourcePath, targetPath);
            }
            catch
            {
                CopyDirectoryRecursive(sourcePath, targetPath);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to place BizHawk: " + e.Message);
            Fail(say, onComplete, "ERROR: Failed to install BizHawk\n" + e.Message);
            yield break;
        }

        SafeDeleteDirectory(extractPath);
        SafeDeleteFile(localPath);

        if (!File.Exists(Path.Combine(targetPath, EmuHawkExe)))
        {
            Fail(say, onComplete, "ERROR: EmuHawk.exe not found after installation.");
            yield break;
        }

        RegisterManagedInstall(targetPath);
        RefreshState();

        say("BizHawk installed to: " + targetPath);
        yield return new WaitForSeconds(1f);

        if (onComplete != null) onComplete(true);
    }

    void Fail(Action<string> say, Action<bool> onComplete, string message)
    {
        say(message);
        if (onComplete != null) onComplete(false);
    }

    IEnumerator DownloadFile(string url, string savePath, Action<bool> done)
    {
        UnityEngine.Debug.Log("Starting download from: " + url);

        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(savePath);
            request.timeout = 300;

            yield return request.SendWebRequest();

            bool ok = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
            if (!ok)
            {
                UnityEngine.Debug.LogError("Download error: " + request.error + " (code " + request.responseCode + ")");
            }

            if (done != null) done(ok);
        }
    }

    private ThreadTask StartZipExtraction(string zipPath, string extractPath)
    {
        ThreadTask task = new ThreadTask();
        Thread thread = new Thread(() =>
        {
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            }
            catch (Exception e)
            {
                task.Exception = e;
            }
            finally
            {
                task.Complete = true;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        return task;
    }

    void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);

        foreach (string dir in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }

    void SafeDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    void SafeDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public const string ConfigFileName = "config.json";
    public const string InstalledKey = "bizhawk_already_installed";
    public const string DirectoryKey = "bizhawk_directory";
    public const string EmuHawkExe = "EmuHawk.exe";

    public static bool EscapeBackslashes = true;

    private static readonly Regex InstalledRegex =
        new Regex("\"" + InstalledKey + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);

    private static readonly Regex DirectoryRegex =
        new Regex("\"" + DirectoryKey + "\"\\s*:\\s*\"([^\"]*)\"");

    public static string ExeDirectory
    {
        get { return Path.GetFullPath(Path.GetDirectoryName(Application.dataPath)); }
    }

    public static string ConfigPath
    {
        get { return Path.Combine(ExeDirectory, ConfigFileName); }
    }

    public static string DefaultInstallDirectory(string folderName = "BizHawk")
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), folderName);
    }

    public static void EnsureEntries()
    {
        try
        {
            string path = ConfigPath;

            if (!File.Exists(path))
            {
                WriteAll(
                    "{" + Environment.NewLine +
                    "  \"" + InstalledKey + "\": false," + Environment.NewLine +
                    "  \"" + DirectoryKey + "\": \"\"" + Environment.NewLine +
                    "}");
                return;
            }

            string text = File.ReadAllText(path);
            List<string> missing = new List<string>();

            if (!InstalledRegex.IsMatch(text))
                missing.Add("\"" + InstalledKey + "\": false");

            if (!DirectoryRegex.IsMatch(text))
                missing.Add("\"" + DirectoryKey + "\": \"\"");

            if (missing.Count == 0)
                return;

            WriteAll(InsertLines(text, missing));
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("EnsureEntries failed: " + e.Message);
        }
    }

    public static void Read(out bool alreadyInstalled, out string directory)
    {
        alreadyInstalled = false;
        directory = "";

        try
        {
            if (!File.Exists(ConfigPath))
                return;

            string text = File.ReadAllText(ConfigPath);

            Match mi = InstalledRegex.Match(text);
            if (mi.Success)
                alreadyInstalled = mi.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

            Match md = DirectoryRegex.Match(text);
            if (md.Success)
                directory = Unescape(md.Groups[1].Value);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Read failed: " + e.Message);
        }
    }

    public static void SetState(bool alreadyInstalled, string directory)
    {
        try
        {
            EnsureEntries();

            string text = File.ReadAllText(ConfigPath);
            string installedValue = alreadyInstalled ? "true" : "false";
            string escapedDir = Escape(directory ?? "");

            text = InstalledRegex.Replace(text, m => "\"" + InstalledKey + "\": " + installedValue);
            text = DirectoryRegex.Replace(text, m => "\"" + DirectoryKey + "\": \"" + escapedDir + "\"");

            WriteAll(text);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("SetState failed: " + e.Message);
        }
    }

    public static void RegisterManagedInstall(string installDirectory)
    {
        bool installed;
        string dir;
        Read(out installed, out dir);

        if (installed && !string.IsNullOrEmpty(dir))
            return;

        SetState(true, installDirectory);
    }

    public static bool TryResolveDirectory(string input, out string bizHawkDirectory)
    {
        bizHawkDirectory = "";

        if (string.IsNullOrWhiteSpace(input))
            return false;

        string path = input.Trim().Trim('"');

        try
        {
            if (File.Exists(path))
            {
                if (Path.GetFileName(path).Equals(EmuHawkExe, StringComparison.OrdinalIgnoreCase))
                {
                    bizHawkDirectory = Path.GetDirectoryName(path);
                    return true;
                }
                return false;
            }

            if (Directory.Exists(path) && File.Exists(Path.Combine(path, EmuHawkExe)))
            {
                bizHawkDirectory = path.TrimEnd('\\', '/');
                return true;
            }
        }
        catch { }

        return false;
    }

    private static string InsertLines(string content, List<string> lines)
    {
        string block = string.Join("," + Environment.NewLine, lines.ToArray());
        string trimmed = content.TrimEnd();

        if (trimmed.Length == 0)
            return "{" + Environment.NewLine + "  " + block + Environment.NewLine + "}";

        if (trimmed.EndsWith("}"))
        {
            int close = content.LastIndexOf('}');
            string before = content.Substring(0, close).TrimEnd();
            bool needComma = before.Length > 0
                             && before[before.Length - 1] != '{'
                             && before[before.Length - 1] != ',';

            return before + (needComma ? "," : "") + Environment.NewLine + "  " + block
                   + Environment.NewLine + content.Substring(close);
        }

        if (!trimmed.EndsWith(","))
            trimmed += ",";

        return "{" + Environment.NewLine + "  " + trimmed + Environment.NewLine + "  " + block
               + Environment.NewLine + "}";
    }

    private static void WriteAll(string text)
    {
        File.WriteAllText(ConfigPath, text, new UTF8Encoding(false));
    }

    private static string Escape(string value)
    {
        return EscapeBackslashes ? value.Replace("\\", "\\\\") : value;
    }

    private static string Unescape(string value)
    {
        return EscapeBackslashes ? value.Replace("\\\\", "\\") : value;
    }
}