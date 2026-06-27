using UnityEngine;
using TMPro;

public class VersionDisplayer : MonoBehaviour
{
    [Header("VERSION DISPLAY")]
    public TextMeshProUGUI versionText;

    private ToolVersionManager toolVersionManager;

    void Start()
    {
        toolVersionManager = FindAnyObjectByType<ToolVersionManager>();

        if (toolVersionManager == null)
        {
            UnityEngine.Debug.LogError("ToolVersionManager not found in scene!");
            return;
        }

        if (versionText == null)
        {
            UnityEngine.Debug.LogError("TextMeshProUGUI reference not assigned!");
            return;
        }

        UpdateVersionDisplay();
    }

    void UpdateVersionDisplay()
    {
        if (toolVersionManager != null && versionText != null)
        {
            versionText.text = $"quack! - Archipelago Setup Tool v{toolVersionManager.currentToolVersion}";
            UnityEngine.Debug.Log("Version display updated: " + versionText.text);
        }
    }
}