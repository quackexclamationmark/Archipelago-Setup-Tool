using UnityEngine;
using UnityEngine.UI;

public class ToolsButton : MonoBehaviour
{
    public string toolsName;
    private ToolsPanelManager panelManager;
    private Button button;
    private ColorBlock originalColors;

    private static ToolsButton currentSelectedButton;

    void Start()
    {
        panelManager = FindFirstObjectByType<ToolsPanelManager>();
        button = GetComponent<Button>();
        originalColors = button.colors;
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (currentSelectedButton != null && currentSelectedButton != this)
        {
            currentSelectedButton.ResetButtonColor();
        }

        ApplyPressedColor();
        currentSelectedButton = this;

        if (panelManager != null)
        {
            panelManager.ShowPanel(toolsName);
        }
    }

    void ApplyPressedColor()
    {
        ColorBlock colors = button.colors;
        colors.normalColor = originalColors.pressedColor;
        colors.highlightedColor = originalColors.pressedColor;
        colors.pressedColor = originalColors.pressedColor;
        button.colors = colors;
    }

    void ResetButtonColor()
    {
        button.colors = originalColors;
    }
}