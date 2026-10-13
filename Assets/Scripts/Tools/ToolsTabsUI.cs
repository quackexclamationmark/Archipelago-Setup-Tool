using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ToolsTabsUI : MonoBehaviour
{
    public static ToolsTabsUI Instance;

    [Header("PANELS")]
    public GameObject setupPanel;
    public GameObject infosPanel;

    [Header("TAB BUTTONS")]
    public Button setupButton;
    public Button infosButton;

    [Header("PLATFORM SELECTION")]
    public PlatformSelection platformSelection;

    [Header("LINUX SETUP PANEL (optional)")]
    public GameObject linuxSetupPanel;

    [Header("COLORS")]
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    public Color highlightedColor = new Color(0.85f, 0.85f, 0.85f);

    private bool setupIsActive = true;
    private bool infosIsActive = false;

    private GameObject activeSetupPanel;

    void Awake()
    {
        Instance = this;

        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("EventSystem créé automatiquement par GameTabsUI.Awake()");
        }
    }

    void Start()
    {
        OpenSetup();
        ResolveSetupPanel();

        AddHoverEvents(setupButton, "setup");
        AddHoverEvents(infosButton, "infos");

        if (setupButton != null)
        {
            setupButton.onClick.RemoveListener(OpenSetup);
            setupButton.onClick.AddListener(OpenSetup);
        }

        if (infosButton != null)
        {
            infosButton.onClick.RemoveListener(OpenInfos);
            infosButton.onClick.AddListener(OpenInfos);
        }

        void ResolveSetupPanel()
        {
            if (platformSelection == null)
                platformSelection = FindFirstObjectByType<PlatformSelection>();

            bool useLinuxPanel = platformSelection != null
                                  && platformSelection.isLinux
                                  && linuxSetupPanel != null;

            if (useLinuxPanel)
            {
                activeSetupPanel = linuxSetupPanel;

                if (setupPanel != null)
                    setupPanel.SetActive(false);

                Debug.Log("[GameTabsUI] Linux détecté + linuxSetupPanel assigné -> utilisation du setup panel Linux.");
            }
            else
            {
                activeSetupPanel = setupPanel;

                if (linuxSetupPanel != null)
                    linuxSetupPanel.SetActive(false);

                if (platformSelection != null && platformSelection.isLinux && linuxSetupPanel == null)
                    Debug.Log("[GameTabsUI] Linux détecté mais linuxSetupPanel non assigné -> fallback sur le setup panel par défaut.");
                else
                    Debug.Log("[GameTabsUI] Windows détecté (ou par défaut) -> utilisation du setup panel par défaut.");
            }
        }
    }

    // ---------------- TABS ----------------

    public void OpenSetup()
    {
        setupIsActive = true;
        infosIsActive = false;

        if (activeSetupPanel != null) activeSetupPanel.SetActive(true);
        if (infosPanel != null) infosPanel.SetActive(false);

        UpdateButtonColors();
    }

    public void OpenInfos()
    {
        setupIsActive = false;
        infosIsActive = true;

        if (activeSetupPanel != null) activeSetupPanel.SetActive(false);
        if (infosPanel != null) infosPanel.SetActive(true);

        UpdateButtonColors();
    }

    // ---------------- COLORS ----------------

    void UpdateButtonColors()
    {
        if (setupButton != null) SetButtonColor(setupButton, setupIsActive ? activeColor : inactiveColor);
        if (infosButton != null) SetButtonColor(infosButton, infosIsActive ? activeColor : inactiveColor);
    }

    void SetButtonColor(Button button, Color color)
    {
        Image img = button.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    // ---------------- HOVER ----------------

    void AddHoverEvents(Button button, string tabType)
    {
        if (button == null) return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        AddEvent(trigger, EventTriggerType.PointerEnter, () =>
        {
            bool isActive = false;
            switch (tabType)
            {
                case "setup": isActive = setupIsActive; break;
                case "infos": isActive = infosIsActive; break;
            }
            if (!isActive) SetButtonColor(button, highlightedColor);
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