using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SideBarTab : MonoBehaviour
{
    [Header("TAB BUTTONS")]
    public Button firstTabButton;
    public Button secondTabButton;

    [Header("TAB PANELS")]
    public GameObject firstTabPanel;
    public GameObject secondTabPanel;

    [Header("TAB TEXTS")]
    public TextMeshProUGUI firstTabText;
    public TextMeshProUGUI secondTabText;

    private ColorBlock firstTabOriginalColors;
    private ColorBlock secondTabOriginalColors;

    private Color firstTabTextOriginalColor;
    private Color secondTabTextOriginalColor;

    void Start()
    {
        firstTabOriginalColors = firstTabButton.colors;
        secondTabOriginalColors = secondTabButton.colors;

        firstTabTextOriginalColor = firstTabText.color;
        secondTabTextOriginalColor = secondTabText.color;

        firstTabButton.onClick.AddListener(OnFirstTabClicked);
        secondTabButton.onClick.AddListener(OnSecondTabClicked);

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
        if (firstTabPanel != null)
            firstTabPanel.SetActive(true);
        if (secondTabPanel != null)
            secondTabPanel.SetActive(false);

        SetButtonAsActive(firstTabButton, firstTabOriginalColors, firstTabText, firstTabTextOriginalColor);
        SetButtonAsInactive(secondTabButton, secondTabOriginalColors, secondTabText, secondTabTextOriginalColor);
    }

    void SelectSecondTab()
    {
        if (firstTabPanel != null)
            firstTabPanel.SetActive(false);
        if (secondTabPanel != null)
            secondTabPanel.SetActive(true);

        SetButtonAsInactive(firstTabButton, firstTabOriginalColors, firstTabText, firstTabTextOriginalColor);
        SetButtonAsActive(secondTabButton, secondTabOriginalColors, secondTabText, secondTabTextOriginalColor);
    }

    void SetButtonAsActive(Button button, ColorBlock originalColors, TextMeshProUGUI text, Color originalTextColor)
    {
        button.colors = originalColors;
        button.interactable = true;
        text.color = originalTextColor;
    }

    void SetButtonAsInactive(Button button, ColorBlock originalColors, TextMeshProUGUI text, Color originalTextColor)
    {
        ColorBlock colors = originalColors;
        colors.normalColor = colors.disabledColor;
        button.colors = colors;
        button.interactable = true;
        text.color = new Color(originalTextColor.r, originalTextColor.g, originalTextColor.b, originalTextColor.a * 0.6f);
    }
}