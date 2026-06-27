using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameTabsUI : MonoBehaviour
{
    public static GameTabsUI Instance;

    [Header("PANELS")]
    public GameObject setupPanel;
    public GameObject infosPanel;
    public GameObject popTrackerPanel;
    public GameObject downloadPanel;

    [Header("AUTO OBJECTS ON SETUP")]
    public GameObject revertInfos;
    public GameObject revertButton;

    [Header("TAB BUTTONS")]
    public Button setupButton;
    public Button infosButton;
    public Button popTrackerButton;
    public Button downloadButton;

    [Header("ROM PANEL (openable via button)")]
    public GameObject romPanel;
    public Button romPanelButton;

    [Header("COLORS")]
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    public Color highlightedColor = new Color(0.85f, 0.85f, 0.85f);

    private bool setupIsActive = true;
    private bool infosIsActive = false;
    private bool popTrackerIsActive = false;
    private bool downloadIsActive = false;
    private bool romIsActive = false;

    void Awake()
    {
        // Singleton-ish convenience
        Instance = this;

        // Ensure an EventSystem exists so UI receives clicks
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("EventSystem créé automatiquement par GameTabsUI.Awake()");
        }
    }

    void Start()
    {
        // Add hover events
        AddHoverEvents(setupButton, "setup");
        AddHoverEvents(infosButton, "infos");

        if (popTrackerButton != null)
            AddHoverEvents(popTrackerButton, "poptracker");

        if (downloadButton != null)
            AddHoverEvents(downloadButton, "download");

        if (romPanelButton != null)
            AddHoverEvents(romPanelButton, "rom");

        // Bind onClick handlers so clicks change tabs
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

        if (popTrackerButton != null)
        {
            popTrackerButton.onClick.RemoveListener(OpenPopTracker);
            popTrackerButton.onClick.AddListener(OpenPopTracker);
        }

        if (downloadButton != null)
        {
            downloadButton.onClick.RemoveListener(OpenDownload);
            downloadButton.onClick.AddListener(OpenDownload);
        }

        if (romPanelButton != null)
        {
            romPanelButton.onClick.RemoveListener(OpenRomPanel);
            romPanelButton.onClick.AddListener(OpenRomPanel);
        }

        // Default: if romPanel is assigned -> show romPanel by default, else setup
        if (romPanel != null)
            OpenRomPanel();
        else
            OpenSetup();
    }

    void OnEnable()
    {
        if (romPanel != null)
            OpenRomPanel();
        else
            OpenSetup();
    }

    // ---------------- TABS ----------------

    public void OpenSetup()
    {
        setupIsActive = true;
        infosIsActive = false;
        popTrackerIsActive = false;
        downloadIsActive = false;
        romIsActive = false;

        if (setupPanel != null) setupPanel.SetActive(true);
        if (infosPanel != null) infosPanel.SetActive(false);
        if (popTrackerPanel != null) popTrackerPanel.SetActive(false);
        if (downloadPanel != null) downloadPanel.SetActive(false);

        if (revertInfos != null) revertInfos.SetActive(true);
        if (revertButton != null) revertButton.SetActive(true);

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    public void OpenInfos()
    {
        setupIsActive = false;
        infosIsActive = true;
        popTrackerIsActive = false;
        downloadIsActive = false;
        romIsActive = false;

        if (setupPanel != null) setupPanel.SetActive(false);
        if (infosPanel != null) infosPanel.SetActive(true);
        if (popTrackerPanel != null) popTrackerPanel.SetActive(false);
        if (downloadPanel != null) downloadPanel.SetActive(false);

        if (revertInfos != null) revertInfos.SetActive(false);
        if (revertButton != null) revertButton.SetActive(false);

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    public void OpenPopTracker()
    {
        setupIsActive = false;
        infosIsActive = false;
        popTrackerIsActive = true;
        downloadIsActive = false;
        romIsActive = false;

        if (setupPanel != null) setupPanel.SetActive(false);
        if (infosPanel != null) infosPanel.SetActive(false);
        if (popTrackerPanel != null) popTrackerPanel.SetActive(true);
        if (downloadPanel != null) downloadPanel.SetActive(false);

        if (revertInfos != null) revertInfos.SetActive(false);
        if (revertButton != null) revertButton.SetActive(false);

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    public void OpenDownload()
    {
        setupIsActive = false;
        infosIsActive = false;
        popTrackerIsActive = false;
        downloadIsActive = true;
        romIsActive = false;

        if (setupPanel != null) setupPanel.SetActive(false);
        if (infosPanel != null) infosPanel.SetActive(false);
        if (popTrackerPanel != null) popTrackerPanel.SetActive(false);
        if (downloadPanel != null) downloadPanel.SetActive(true);

        if (revertInfos != null) revertInfos.SetActive(false);
        if (revertButton != null) revertButton.SetActive(false);

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    public void OpenRomPanel()
    {
        setupIsActive = false;
        infosIsActive = false;
        popTrackerIsActive = false;
        downloadIsActive = false;
        romIsActive = true;

        if (setupPanel != null) setupPanel.SetActive(false);
        if (infosPanel != null) infosPanel.SetActive(false);
        if (popTrackerPanel != null) popTrackerPanel.SetActive(false);
        if (downloadPanel != null) downloadPanel.SetActive(false);

        if (revertInfos != null) revertInfos.SetActive(false);
        if (revertButton != null) revertButton.SetActive(false);

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    // ---------------- ROM PANEL STATE ----------------

    // Méthode publique pour que d'autres scripts notifient l'ouverture / fermeture du romPanel
    public void SetRomPanelActive(bool active)
    {
        romIsActive = active;

        // Si le romPanel a été fermé de l'extérieur, on retombe sur Setup par défaut (ou on laisse tout fermé)
        if (!active)
        {
            // Nous choisissons de désélectionner le bouton ROM et de garder les autres inactifs
            setupIsActive = false;
            infosIsActive = false;
            popTrackerIsActive = false;
            downloadIsActive = false;
        }

        UpdateButtonColors();
        UpdateRomPanelState();
    }

    void UpdateRomPanelState()
    {
        if (romPanel == null)
            return;

        romPanel.SetActive(romIsActive);
    }



    // ---------------- COLORS ----------------

    void UpdateButtonColors()
    {
        if (setupButton != null) SetButtonColor(setupButton, setupIsActive ? activeColor : inactiveColor);
        if (infosButton != null) SetButtonColor(infosButton, infosIsActive ? activeColor : inactiveColor);
        if (popTrackerButton != null) SetButtonColor(popTrackerButton, popTrackerIsActive ? activeColor : inactiveColor);
        if (downloadButton != null) SetButtonColor(downloadButton, downloadIsActive ? activeColor : inactiveColor);
        if (romPanelButton != null) SetButtonColor(romPanelButton, romIsActive ? activeColor : inactiveColor);
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
                case "poptracker": isActive = popTrackerIsActive; break;
                case "download": isActive = downloadIsActive; break;
                case "rom": isActive = romIsActive; break;
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