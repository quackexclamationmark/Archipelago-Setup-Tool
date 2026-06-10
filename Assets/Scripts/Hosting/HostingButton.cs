using UnityEngine;
using UnityEngine.UI;

public class HostingButton : MonoBehaviour
{
    public string hostingName;
    private HostingPanelManager panelManager;
    private Button button;
    private ColorBlock originalColors;

    // Static pour garder la référence du bouton actif
    private static HostingButton currentSelectedButton;

    void Start()
    {
        panelManager = FindFirstObjectByType<HostingPanelManager>();
        button = GetComponent<Button>();
        originalColors = button.colors;
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        // Réinitialiser l'ancien bouton
        if (currentSelectedButton != null && currentSelectedButton != this)
        {
            currentSelectedButton.ResetButtonColor();
        }

        // Appliquer la couleur pressed au nouveau bouton
        ApplyPressedColor();
        currentSelectedButton = this;

        // Afficher le panel
        if (panelManager != null)
        {
            panelManager.ShowPanel(hostingName);
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