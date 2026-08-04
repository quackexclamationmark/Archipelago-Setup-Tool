using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ManualDropdown : MonoBehaviour
{
    [Header("Main Button")]
    [SerializeField] private Button mainButton;
    [SerializeField] private TextMeshProUGUI mainButtonText;

    [Header("Options")]
    [SerializeField] private Button option1Button;
    [SerializeField] private TextMeshProUGUI option1ButtonText;
    [SerializeField] private GameObject option1Checkmark;

    [SerializeField] private Button option2Button;
    [SerializeField] private TextMeshProUGUI option2ButtonText;
    [SerializeField] private GameObject option2Checkmark;

    [SerializeField] private Button option3Button;
    [SerializeField] private TextMeshProUGUI option3ButtonText;
    [SerializeField] private GameObject option3Checkmark;

    [Header("Panel")]
    [SerializeField] private GameObject panelOptions;

    [Header("Platform Selection")]
    [SerializeField] private PlatformSelection platformSelection;
    [SerializeField] private GameObject windowsDropdown;
    [SerializeField] private GameObject linuxDropdown;

    [Header("State")]
    [SerializeField] private bool isOpen = false;
    [SerializeField] private bool option1Selected = false;
    [SerializeField] private bool option2Selected = false;
    [SerializeField] private bool option3Selected = false;

    public delegate void OptionSelectedDelegate(int optionNumber);
    public event OptionSelectedDelegate OnOptionSelected;

    public delegate void PanelStateChangedDelegate(bool isOpen);
    public event PanelStateChangedDelegate OnPanelStateChanged;

    private void Start()
    {
        if (panelOptions != null)
        {
            panelOptions.SetActive(false);
        }

        if (option1Checkmark != null)
        {
            option1Checkmark.SetActive(false);
        }
        if (option2Checkmark != null)
        {
            option2Checkmark.SetActive(false);
        }
        if (option3Checkmark != null)
        {
            option3Checkmark.SetActive(false);
        }

        if (mainButton != null)
        {
            mainButton.onClick.AddListener(TogglePanel);
        }

        if (option1Button != null)
        {
            option1Button.onClick.AddListener(() => SelectOption(1));
        }

        if (option2Button != null)
        {
            option2Button.onClick.AddListener(() => SelectOption(2));
        }

        if (option3Button != null)
        {
            option3Button.onClick.AddListener(() => SelectOption(3));
        }

        SelectOption(1);
        UpdatePlatformVisibility();
    }

    private void Update()
    {
        UpdatePlatformVisibility();
    }

    private void UpdatePlatformVisibility()
    {
        if (platformSelection != null)
        {
            if (windowsDropdown != null)
            {
                windowsDropdown.SetActive(platformSelection.isWindows);
            }

            if (linuxDropdown != null)
            {
                linuxDropdown.SetActive(platformSelection.isLinux);
            }
        }
    }

    private void TogglePanel()
    {
        isOpen = !isOpen;

        if (panelOptions != null)
        {
            panelOptions.SetActive(isOpen);
        }

        OnPanelStateChanged?.Invoke(isOpen);
    }

    private void SelectOption(int optionNumber)
    {
        option1Selected = false;
        option2Selected = false;
        option3Selected = false;

        if (option1Checkmark != null)
        {
            option1Checkmark.SetActive(false);
        }
        if (option2Checkmark != null)
        {
            option2Checkmark.SetActive(false);
        }
        if (option3Checkmark != null)
        {
            option3Checkmark.SetActive(false);
        }

        switch (optionNumber)
        {
            case 1:
                option1Selected = true;
                if (option1Checkmark != null)
                {
                    option1Checkmark.SetActive(true);
                }
                if (mainButtonText != null && option1ButtonText != null)
                {
                    mainButtonText.text = option1ButtonText.text;
                }
                break;
            case 2:
                option2Selected = true;
                if (option2Checkmark != null)
                {
                    option2Checkmark.SetActive(true);
                }
                if (mainButtonText != null && option2ButtonText != null)
                {
                    mainButtonText.text = option2ButtonText.text;
                }
                break;
            case 3:
                option3Selected = true;
                if (option3Checkmark != null)
                {
                    option3Checkmark.SetActive(true);
                }
                if (mainButtonText != null && option3ButtonText != null)
                {
                    mainButtonText.text = option3ButtonText.text;
                }
                break;
        }

        isOpen = false;
        if (panelOptions != null)
        {
            panelOptions.SetActive(false);
        }

        OnOptionSelected?.Invoke(optionNumber);
    }

    public bool IsOpen
    {
        get => isOpen;
        set => isOpen = value;
    }

    public bool Option1Selected
    {
        get => option1Selected;
        set => option1Selected = value;
    }

    public bool Option2Selected
    {
        get => option2Selected;
        set => option2Selected = value;
    }

    public bool Option3Selected
    {
        get => option3Selected;
        set => option3Selected = value;
    }
}