using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ExtraPanel : MonoBehaviour
{
    [Header("UI REFERENCES")]
    public GameObject extraPanel;
    public Button okButton;
    public Button showExtraPanelButton;
    public GameObject setupPanel;
    public GameObject revertPanel;

    void Start()
    {
        if (okButton != null)
            okButton.onClick.AddListener(CloseExtraPanel);

        if (showExtraPanelButton != null)
            showExtraPanelButton.onClick.AddListener(ShowExtraPanelAgain);

        // Masquer le extraPanel au démarrage
        if (extraPanel != null)
            extraPanel.SetActive(false);

        // Afficher setupPanel/revertPanel
        if (setupPanel != null)
            setupPanel.SetActive(true);
        if (revertPanel != null)
            revertPanel.SetActive(true);
    }

    void CloseExtraPanel()
    {
        if (extraPanel != null)
            extraPanel.SetActive(false);
        if (setupPanel != null)
            setupPanel.SetActive(true);
        if (revertPanel != null)
            revertPanel.SetActive(true);
    }

    void ShowExtraPanelAgain()
    {
        if (extraPanel != null)
            extraPanel.SetActive(true);
        if (setupPanel != null)
            setupPanel.SetActive(false);
        if (revertPanel != null)
            revertPanel.SetActive(false);
    }
}