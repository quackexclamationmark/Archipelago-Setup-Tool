using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

public class RandomizerSetup : MonoBehaviour
{
    [Header("DS2 REFERENCE")]
    public DS2ManualDL ds2Manual;

    [Header("ZIP SELECTION UI")]
    public Button selectZipButton;
    public TMP_InputField zipPathInputField;
    public TextMeshProUGUI zipPathPlaceholder;
    public Button installButton;

    [Header("TOGGLE -> BUTTON VISIBILITY")]
    public Toggle visibilityToggle;
    public Button toggledButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    [Header("EXPECTED CONTENT")]
    public string expectedZipFileName = "DS2SRandomizer-1317-3-3-1-1747310715.zip";
    public string randomizerFolderName = "randomizer";
    public string dinputFileName = "dinput8.dll";
    public string heapFileName = "ds2s_heap_x.dll";
    public string modEngineFileName = "modengine.ini";

    private string selectedZipPath = "";
    private bool isInstalling = false;

    void Start()
    {
        if (selectZipButton != null) selectZipButton.onClick.AddListener(SelectZip);
        if (zipPathInputField != null) zipPathInputField.onValueChanged.AddListener(OnZipPathInputChanged);
        if (installButton != null) installButton.onClick.AddListener(OnInstallClicked);
        if (infoOkButton != null) infoOkButton.onClick.AddListener(CloseInfoPanel);
        if (infoPanel != null) infoPanel.SetActive(false);

        if (visibilityToggle != null)
        {
            visibilityToggle.onValueChanged.AddListener(OnVisibilityToggleChanged);
            OnVisibilityToggleChanged(visibilityToggle.isOn);
        }

        UpdateInstallButtonInteractable();
    }

    // ---------------------------------------------------------------- SELECTION

    void SelectZip() { StartCoroutine(ShowFileBrowser()); }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null) FileBrowser.Skin = darkSkin;
        else UnityEngine.Debug.LogWarning("DarkSkin not assigned!");

        FileBrowser.SetFilters(true, new FileBrowser.Filter("ZIP", ".zip"));
        FileBrowser.SetDefaultFilter(".zip");

        string startPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, startPath, "Select the Randomizer (.zip)");

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
            SetZipPath(FileBrowser.Result[0]);
    }

    void SetZipPath(string path)
    {
        selectedZipPath = path;
        if (zipPathInputField != null) zipPathInputField.text = path;
        if (zipPathPlaceholder != null) zipPathPlaceholder.gameObject.SetActive(false);
        UnityEngine.Debug.Log("Randomizer zip set: " + path);
        UpdateInstallButtonInteractable();
    }

    void OnZipPathInputChanged(string newValue)
    {
        selectedZipPath = newValue;
        UpdateInstallButtonInteractable();
    }

    void UpdateInstallButtonInteractable()
    {
        if (installButton == null) return;
        installButton.interactable = !isInstalling && IsValidZipSilent(selectedZipPath);
    }

    // ---------------------------------------------------------------- VALIDATION

    bool IsValidZipSilent(string zipPath)
    {
        string prefix, error;
        return TryAnalyzeZip(zipPath, out prefix, out error);
    }

    bool TryAnalyzeZip(string zipPath, out string rootPrefix, out string error)
    {
        rootPrefix = null;
        error = null;

        if (string.IsNullOrEmpty(zipPath)) { error = "Please select the Randomizer zip file."; return false; }
        if (!File.Exists(zipPath)) { error = "Zip file does not exist."; return false; }
        if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { error = "The file must be a .zip."; return false; }

        string fileName = Path.GetFileName(zipPath);
        if (!string.Equals(fileName, expectedZipFileName, StringComparison.Ordinal))
        {
            error = "The zip must be named exactly '" + expectedZipFileName + "'.";
            return false;
        }

        try
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                string bestPrefix = null;
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    if (!entry.Name.Equals(dinputFileName, StringComparison.OrdinalIgnoreCase)) continue;

                    string full = entry.FullName.Replace('\\', '/');
                    string prefix = full.Substring(0, full.Length - entry.Name.Length);
                    if (bestPrefix == null || prefix.Length < bestPrefix.Length) bestPrefix = prefix;
                }

                if (bestPrefix == null) { error = "'" + dinputFileName + "' not found in the zip."; return false; }

                bool hasRandomizerFolder = false, hasHeap = false, hasModEngine = false;

                foreach (var entry in archive.Entries)
                {
                    string full = entry.FullName.Replace('\\', '/');
                    if (!full.StartsWith(bestPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = full.Substring(bestPrefix.Length);

                    if (rel.StartsWith(randomizerFolderName + "/", StringComparison.OrdinalIgnoreCase)) hasRandomizerFolder = true;
                    else if (rel.Equals(heapFileName, StringComparison.OrdinalIgnoreCase)) hasHeap = true;
                    else if (rel.Equals(modEngineFileName, StringComparison.OrdinalIgnoreCase)) hasModEngine = true;
                }

                if (!hasRandomizerFolder) { error = "Folder '" + randomizerFolderName + "' not found in the zip."; return false; }
                if (!hasHeap) { error = "'" + heapFileName + "' not found in the zip."; return false; }
                if (!hasModEngine) { error = "'" + modEngineFileName + "' not found in the zip."; return false; }

                rootPrefix = bestPrefix;
                return true;
            }
        }
        catch (Exception e)
        {
            error = "Could not read the zip: " + e.Message;
            return false;
        }
    }

    // ---------------------------------------------------------------- INSTALL

    void OnInstallClicked()
    {
        if (isInstalling) return;

        string prefix, error;
        if (!TryAnalyzeZip(selectedZipPath, out prefix, out error))
        {
            ShowInfo(error);
            return;
        }

        if (ds2Manual == null)
        {
            ShowInfo("DS2ManualDL reference is missing!");
            return;
        }

        string gamePath = ds2Manual.GetCurrentGamePath();
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowInfo("Dark Souls II game folder not found. Please check your installation.");
            return;
        }

        StartCoroutine(InstallFlow(prefix, gamePath));
    }

    IEnumerator InstallFlow(string rootPrefix, string gamePath)
    {
        isInstalling = true;
        UpdateInstallButtonInteractable();

        ShowInfo("Installing Randomizer...");
        yield return null; // let the UI refresh

        string tempDir = Path.Combine(Application.persistentDataPath, "RandomizerTemp");
        bool success = false;
        string errorMessage = null;

        try
        {
            SafeDeleteDirectory(tempDir);
            Directory.CreateDirectory(tempDir);

            // 1. Extract only what we need (root prefix stripped)
            ExtractNeeded(selectedZipPath, rootPrefix, tempDir);

            // 2. Rename dinput8.dll -> randomizer.dll BEFORE moving anything
            string dinputTemp = Path.Combine(tempDir, dinputFileName);
            string randomizerDllTemp = Path.Combine(tempDir, "randomizer.dll");
            if (File.Exists(randomizerDllTemp)) File.Delete(randomizerDllTemp);
            File.Move(dinputTemp, randomizerDllTemp);

            // 3. randomizer.dll -> <game>/dllMods/
            string dllModsDir = Path.Combine(gamePath, "dllMods");
            Directory.CreateDirectory(dllModsDir);
            CopyFileOverwrite(randomizerDllTemp, Path.Combine(dllModsDir, "randomizer.dll"));

            // 4. randomizer folder -> <game>/randomizer
            string randomizerFolderDest = Path.Combine(gamePath, randomizerFolderName);
            SafeDeleteDirectory(randomizerFolderDest);
            CopyDirectory(Path.Combine(tempDir, randomizerFolderName), randomizerFolderDest);

            // 5. heap dll + modengine.ini -> <game>
            CopyFileOverwrite(Path.Combine(tempDir, heapFileName), Path.Combine(gamePath, heapFileName));
            string modEngineDest = Path.Combine(gamePath, modEngineFileName);
            CopyFileOverwrite(Path.Combine(tempDir, modEngineFileName), modEngineDest);

            // 6. blockNetworkAccess=1 -> blockNetworkAccess=0
            PatchModEngineIni(modEngineDest);

            success = true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Randomizer install failed: " + e);
            errorMessage = e.Message;
        }
        finally
        {
            SafeDeleteDirectory(tempDir);
        }

        isInstalling = false;
        UpdateInstallButtonInteractable();

        if (success) ShowInfo("Randomizer installed successfully!");
        else ShowInfo("ERROR: Randomizer installation failed\n" + errorMessage);
    }

    void ExtractNeeded(string zipPath, string rootPrefix, string destDir)
    {
        string destFull = Path.GetFullPath(destDir);

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                string full = entry.FullName.Replace('\\', '/');
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rel = full.Substring(rootPrefix.Length);

                bool needed =
                    rel.StartsWith(randomizerFolderName + "/", StringComparison.OrdinalIgnoreCase) ||
                    rel.Equals(dinputFileName, StringComparison.OrdinalIgnoreCase) ||
                    rel.Equals(heapFileName, StringComparison.OrdinalIgnoreCase) ||
                    rel.Equals(modEngineFileName, StringComparison.OrdinalIgnoreCase);
                if (!needed) continue;

                string destPath = Path.GetFullPath(Path.Combine(destDir, rel));
                if (!destPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase)) continue; // zip-slip protection

                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                entry.ExtractToFile(destPath, true);
            }
        }
    }

    void PatchModEngineIni(string iniPath)
    {
        if (!File.Exists(iniPath))
        {
            UnityEngine.Debug.LogWarning("modengine.ini not found, cannot patch blockNetworkAccess.");
            return;
        }

        string content = File.ReadAllText(iniPath);
        string patched = Regex.Replace(content, @"blockNetworkAccess\s*=\s*1", "blockNetworkAccess=0");

        if (patched != content)
        {
            File.WriteAllText(iniPath, patched);
            UnityEngine.Debug.Log("Patched modengine.ini: blockNetworkAccess set to 0.");
        }
        else
        {
            UnityEngine.Debug.Log("modengine.ini already had blockNetworkAccess=0 (or pattern not found).");
        }
    }

    // ---------------------------------------------------------------- HELPERS

    void OnVisibilityToggleChanged(bool isOn)
    {
        if (toggledButton != null) toggledButton.gameObject.SetActive(isOn);
    }

    void CopyFileOverwrite(string src, string dest)
    {
        if (File.Exists(dest))
        {
            try { File.SetAttributes(dest, FileAttributes.Normal); File.Delete(dest); } catch { }
        }
        File.Copy(src, dest, true);
    }

    void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        foreach (string dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
    }

    void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                Directory.Delete(path, true);
            }
            catch { }
        }
    }

    void ShowInfo(string message)
    {
        if (infoText != null) infoText.text = message;
        if (infoPanel != null) infoPanel.SetActive(true);
        UnityEngine.Debug.Log("Info: " + message);
    }

    void CloseInfoPanel() { if (infoPanel != null) infoPanel.SetActive(false); }
}