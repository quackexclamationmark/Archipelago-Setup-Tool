using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameTabsUI : MonoBehaviour
{
    [Header("PANELS")]
    public GameObject setupPanel;
    public GameObject infosPanel;

    [Header("AUTO OBJECTS ON SETUP")]
    public GameObject revertInfos;
    public GameObject revertButton;

    [Header("TAB BUTTONS")]
    public Button setupButton;
    public Button infosButton;

    [Header("COLORS")]
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    public Color highlightedColor = new Color(0.85f, 0.85f, 0.85f);

    private bool setupIsActive = true;

    void Start()
    {
        AddHoverEvents(setupButton, true);
        AddHoverEvents(infosButton, false);

        OpenSetup();
    }

    void OnEnable()
    {
        OpenSetup();
    }

    // ---------------- TABS ----------------

    public void OpenSetup()
    {
        setupIsActive = true;

        if (setupPanel != null)
            setupPanel.SetActive(true);

        if (infosPanel != null)
            infosPanel.SetActive(false);

        // Active aussi ces objets
        if (revertInfos != null)
            revertInfos.SetActive(true);

        if (revertButton != null)
            revertButton.SetActive(true);

        UpdateButtonColors();
    }

    public void OpenInfos()
    {
        setupIsActive = false;

        if (setupPanel != null)
            setupPanel.SetActive(false);

        if (infosPanel != null)
            infosPanel.SetActive(true);

        // Cache ces objets en mode infos
        if (revertInfos != null)
            revertInfos.SetActive(false);

        if (revertButton != null)
            revertButton.SetActive(false);

        UpdateButtonColors();
    }

    // ---------------- COLORS ----------------

    void UpdateButtonColors()
    {
        if (setupButton != null)
            SetButtonColor(setupButton, setupIsActive ? activeColor : inactiveColor);

        if (infosButton != null)
            SetButtonColor(infosButton, setupIsActive ? inactiveColor : activeColor);
    }

    void SetButtonColor(Button button, Color color)
    {
        Image img = button.GetComponent<Image>();

        if (img != null)
            img.color = color;
    }

    // ---------------- HOVER ----------------

    void AddHoverEvents(Button button, bool isSetupButton)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddEvent(trigger, EventTriggerType.PointerEnter, () =>
        {
            bool isActive =
                (isSetupButton && setupIsActive) ||
                (!isSetupButton && !setupIsActive);

            if (!isActive)
                SetButtonColor(button, highlightedColor);
        });

        AddEvent(trigger, EventTriggerType.PointerExit, () =>
        {
            UpdateButtonColors();
        });
    }

    void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener((data) => action());
        trigger.triggers.Add(entry);
    }
}