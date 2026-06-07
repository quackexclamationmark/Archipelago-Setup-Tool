using UnityEngine;
using UnityEngine.UI;

public class SideBarTab : MonoBehaviour
{
    [Header("TAB BUTTONS")]
    public Button firstTabButton;
    public Button secondTabButton;

    [Header("TAB PANELS")]
    public GameObject firstTabPanel;
    public GameObject secondTabPanel;

    void Start()
    {
        if (firstTabButton != null)
            firstTabButton.onClick.AddListener(OnFirstTabClicked);

        if (secondTabButton != null)
            secondTabButton.onClick.AddListener(OnSecondTabClicked);

        // Sélectionner le premier tab par défaut
        SelectFirstTab();
    }

    void OnFirstTabClicked()
    {
        SelectFirstTab();
    }

    void OnSecondTabClicked()
    {
        SelectSecondTab();
    }

    void SelectFirstTab()
    {
        // Afficher le premier panel
        if (firstTabPanel != null)
            firstTabPanel.SetActive(true);

        // Masquer le second panel
        if (secondTabPanel != null)
            secondTabPanel.SetActive(false);

        // Mettre en avant le bouton du premier tab
        if (firstTabButton != null)
            firstTabButton.interactable = false;

        // Rendre clickable le bouton du second tab
        if (secondTabButton != null)
            secondTabButton.interactable = true;
    }

    void SelectSecondTab()
    {
        // Masquer le premier panel
        if (firstTabPanel != null)
            firstTabPanel.SetActive(false);

        // Afficher le second panel
        if (secondTabPanel != null)
            secondTabPanel.SetActive(true);

        // Rendre clickable le bouton du premier tab
        if (firstTabButton != null)
            firstTabButton.interactable = true;

        // Mettre en avant le bouton du second tab
        if (secondTabButton != null)
            secondTabButton.interactable = false;
    }
}
