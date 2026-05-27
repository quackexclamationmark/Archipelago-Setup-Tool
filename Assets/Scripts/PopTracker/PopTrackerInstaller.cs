using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SimpleFileBrowser;
using System.Collections;
using System.IO;

public class PopTrackerInstaller : MonoBehaviour
{
    [Header("UI ELEMENTS")]
    public Button selectDirectoryButton;
    public TMP_InputField selectedPathInputField;
    public TextMeshProUGUI selectedPathPlaceholder;
    public Button installButton;

    [Header("SKIN")]
    public UISkin darkSkin;

    [Header("INFO PANEL")]
    public GameObject infoPanel;
    public TextMeshProUGUI infoText;
    public Button infoOkButton;

    private string selectedDirectory = "";

    void Start()
    {
        if (selectDirectoryButton != null)
            selectDirectoryButton.onClick.AddListener(SelectDirectory);

        if (installButton != null)
            installButton.onClick.AddListener(InstallZip);

        if (infoOkButton != null)
            infoOkButton.onClick.AddListener(CloseInfoPanel);

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (selectedPathPlaceholder != null)
            selectedPathPlaceholder.gameObject.SetActive(true);

        if (selectedPathInputField != null)
        {
            selectedPathInputField.onValueChanged.AddListener(OnPathInputChanged);
        }

        UpdateUI();
    }

    void SelectDirectory()
    {
        StartCoroutine(ShowFileBrowser());
    }

    IEnumerator ShowFileBrowser()
    {
        if (darkSkin != null)
        {
            FileBrowser.Skin = darkSkin;
            Debug.Log("DarkSkin assigned!");
        }
        else
        {
            Debug.LogWarning("DarkSkin not assigned!");
        }

        yield return FileBrowser.WaitForLoadDialog(
            FileBrowser.PickMode.Folders,
            false,
            null,
            "Select PopTracker Installation Folder"
        );

        if (FileBrowser.Success && FileBrowser.Result != null && FileBrowser.Result.Length > 0)
        {
            string selectedPath = FileBrowser.Result[0];
            Debug.Log("DEBUG: Path from FileBrowser: " + selectedPath);
            SetDirectory(selectedPath);
        }
    }

    void SetDirectory(string path)
    {
        if (!IsValidPath(path))
        {
            ShowInfo("Please select a valid path");
            return;
        }

        selectedDirectory = path;

        if (selectedPathPlaceholder != null)
            selectedPathPlaceholder.gameObject.SetActive(false);

        if (selectedPathInputField != null)
        {
            selectedPathInputField.gameObject.SetActive(true);
            selectedPathInputField.text = path;
            Debug.Log("Selected path text updated to: " + path);
        }

        Debug.Log("Directory selected: " + path);
        UpdateUI();
    }

    void OnPathInputChanged(string newValue)
    {
        if (IsValidPath(newValue))
        {
            selectedDirectory = newValue;
        }
        else
        {
            selectedDirectory = "";
        }
        UpdateUI();
    }

    bool IsValidPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        char[] invalidChars = Path.GetInvalidPathChars();
        return path.IndexOfAny(invalidChars) < 0;
    }

    void InstallZip()
    {
        if (string.IsNullOrEmpty(selectedDirectory) || !IsValidPath(selectedDirectory))
        {
            ShowInfo("Please select a valid directory first");
            return;
        }

        try
        {
            if (!Directory.Exists(selectedDirectory))
            {
                Directory.CreateDirectory(selectedDirectory);
            }

            ShowInfo("Installation complete in:\n" + selectedDirectory);
            Debug.Log("Installation complete in: " + selectedDirectory);
        }
        catch (System.Exception e)
        {
            ShowInfo("Installation failed: " + e.Message);
            Debug.LogError("Installation error: " + e);
        }
    }

    void ShowInfo(string message)
    {
        if (infoText != null)
            infoText.text = message;

        if (infoPanel != null)
            infoPanel.SetActive(true);

        Debug.Log("Info: " + message);
    }

    void CloseInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    void UpdateUI()
    {
        if (installButton != null)
            installButton.interactable = !string.IsNullOrEmpty(selectedDirectory) && IsValidPath(selectedDirectory);

        Debug.Log("UI Updated");
    }
}