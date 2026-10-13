using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SideBarTab : MonoBehaviour
{
    [Header("TAB BUTTONS")]
    public Button firstTabButton;
    public Button secondTabButton;
    public Button thirdTabButton;

    [Header("TAB PANELS")]
    public GameObject firstTabPanel;
    public GameObject secondTabPanel;
    public GameObject thirdTabPanel;

    [Header("TAB TEXTS")]
    public TextMeshProUGUI firstTabText;
    public TextMeshProUGUI secondTabText;
    public TextMeshProUGUI thirdTabText;

    private ColorBlock firstTabOriginalColors;
    private ColorBlock secondTabOriginalColors;
    private ColorBlock thirdTabOriginalColors;

    private Color firstTabTextOriginalColor;
    private Color secondTabTextOriginalColor;
    private Color thirdTabTextOriginalColor;

    void Start()
    {
        firstTabOriginalColors = firstTabButton.colors;
        secondTabOriginalColors = secondTabButton.colors;
        thirdTabOriginalColors = thirdTabButton.colors;

        firstTabTextOriginalColor = firstTabText.color;
        secondTabTextOriginalColor = secondTabText.color;
        thirdTabTextOriginalColor = thirdTabText.color;

        firstTabButton.onClick.AddListener(OnFirstTabClicked);
        secondTabButton.onClick.AddListener(OnSecondTabClicked);
        thirdTabButton.onClick.AddListener(OnThirdTabClicked);

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

    void OnThirdTabClicked()
    {
        SelectThirdTab();
    }

    void SelectFirstTab()
    {
        if (firstTabPanel != null)
            firstTabPanel.SetActive(true);
        if (secondTabPanel != null)
            secondTabPanel.SetActive(false);
        if (thirdTabPanel != null)
            thirdTabPanel.SetActive(false);

        SetButtonAsActive(firstTabButton, firstTabOriginalColors, firstTabText, firstTabTextOriginalColor);
        SetButtonAsInactive(secondTabButton, secondTabOriginalColors, secondTabText, secondTabTextOriginalColor);
        SetButtonAsInactive(thirdTabButton, thirdTabOriginalColors, thirdTabText, thirdTabTextOriginalColor);
    }

    void SelectSecondTab()
    {
        if (firstTabPanel != null)
            firstTabPanel.SetActive(false);
        if (secondTabPanel != null)
            secondTabPanel.SetActive(true);
        if (thirdTabPanel != null)
            thirdTabPanel.SetActive(false);

        SetButtonAsInactive(firstTabButton, firstTabOriginalColors, firstTabText, firstTabTextOriginalColor);
        SetButtonAsActive(secondTabButton, secondTabOriginalColors, secondTabText, secondTabTextOriginalColor);
        SetButtonAsInactive(thirdTabButton, thirdTabOriginalColors, thirdTabText, thirdTabTextOriginalColor);
    }

    void SelectThirdTab()
    {
        if (firstTabPanel != null)
            firstTabPanel.SetActive(false);
        if (secondTabPanel != null)
            secondTabPanel.SetActive(false);
        if (thirdTabPanel != null)
            thirdTabPanel.SetActive(true);

        SetButtonAsInactive(firstTabButton, firstTabOriginalColors, firstTabText, firstTabTextOriginalColor);
        SetButtonAsInactive(secondTabButton, secondTabOriginalColors, secondTabText, secondTabTextOriginalColor);
        SetButtonAsActive(thirdTabButton, thirdTabOriginalColors, thirdTabText, thirdTabTextOriginalColor);
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