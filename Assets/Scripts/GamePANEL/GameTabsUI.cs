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

    [Header("PLATFORM SELECTION")]
    [Tooltip("Si laissé vide, sera cherché automatiquement dans la scène via FindObjectOfType.")]
    public PlatformSelection platformSelection;

    [Header("LINUX SETUP PANEL (optionnel)")]
    [Tooltip("N'a d'effet que si isLinux = true ET que ce champ est assigné dans l'inspecteur. Sinon le setupPanel par défaut (Windows) est utilisé.")]
    public GameObject linuxSetupPanel;

    [Header("COLORS")]
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    public Color highlightedColor = new Color(0.85f, 0.85f, 0.85f);

    private bool setupIsActive = true;
    private bool infosIsActive = false;
    private bool popTrackerIsActive = false;
    private bool downloadIsActive = false;
    private bool romIsActive = false;

    // Panel réellement utilisé pour l'onglet "setup" (Windows par défaut, ou Linux si applicable)
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
        // IMPORTANT : appelé ici (et non dans Awake) car PlatformSelection fixe isWindows/isLinux
        // dans son propre Awake(). Comme tous les Awake() de la scène s'exécutent avant tous les
        // Start(), on est garanti que platformSelection.isWindows/isLinux sont déjà à jour ici,
        // quel que soit l'ordre relatif des Awake() entre les deux scripts.
        ResolveSetupPanel();

        AddHoverEvents(setupButton, "setup");
        AddHoverEvents(infosButton, "infos");

        if (popTrackerButton != null)
            AddHoverEvents(popTrackerButton, "poptracker");

        if (downloadButton != null)
            AddHoverEvents(downloadButton, "download");

        if (romPanelButton != null)
            AddHoverEvents(romPanelButton, "rom");

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

    // ---------------- PLATFORM / SETUP PANEL RESOLUTION ----------------

    // Détermine quel panel de setup utiliser en fonction de PlatformSelection.
    // - isLinux == true ET linuxSetupPanel assigné  -> on utilise linuxSetupPanel, le setupPanel par défaut est désactivé/écarté du flow.
    // - isWindows == true (ou linuxSetupPanel non assigné) -> on utilise setupPanel par défaut, linuxSetupPanel est désactivé s'il existe.
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

            // Le setup panel par défaut est "supprimé" du flow : on le désactive et on ne le
            // réutilisera plus tant que Linux est actif.
            if (setupPanel != null)
                setupPanel.SetActive(false);

            Debug.Log("[GameTabsUI] Linux détecté + linuxSetupPanel assigné -> utilisation du setup panel Linux.");
        }
        else
        {
            activeSetupPanel = setupPanel;

            // Le panel Linux (s'il existe) n'est pas utilisé dans ce cas.
            if (linuxSetupPanel != null)
                linuxSetupPanel.SetActive(false);

            if (platformSelection != null && platformSelection.isLinux && linuxSetupPanel == null)
                Debug.Log("[GameTabsUI] Linux détecté mais linuxSetupPanel non assigné -> fallback sur le setup panel par défaut.");
            else
                Debug.Log("[GameTabsUI] Windows détecté (ou par défaut) -> utilisation du setup panel par défaut.");
        }
    }

    // ---------------- TABS ----------------

    public void OpenSetup()
    {
        setupIsActive = true;
        infosIsActive = false;
        popTrackerIsActive = false;
        downloadIsActive = false;
        romIsActive = false;

        if (activeSetupPanel != null) activeSetupPanel.SetActive(true);
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

        if (activeSetupPanel != null) activeSetupPanel.SetActive(false);
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

        if (activeSetupPanel != null) activeSetupPanel.SetActive(false);
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

        if (activeSetupPanel != null) activeSetupPanel.SetActive(false);
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

        if (activeSetupPanel != null) activeSetupPanel.SetActive(false);
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

        if (!active)
        {
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