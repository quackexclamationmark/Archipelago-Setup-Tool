using UnityEngine;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
    [Header("PANELS")]
    public GameObject dlPanel;
    public GameObject setupPanel;

    [Header("PANEL BUTTONS")]
    public Button dlPanelButton;
    public Button setupPanelButton;

    [Header("SCRIPTS")]
    public VotVInstaller votVInstaller;
    public VotVManualDL votVManualDL;

    void Start()
    {
        // Initialiser les panneaux - setupPanel actif par défaut
        if (dlPanel != null)
            dlPanel.SetActive(false);

        if (setupPanel != null)
            setupPanel.SetActive(true);

        // Désactiver VotVInstaller, activer VotVManualDL par défaut
        if (votVInstaller != null)
            votVInstaller.enabled = false;

        if (votVManualDL != null)
            votVManualDL.enabled = true;

        // Ajouter les listeners aux boutons des panneaux
        if (dlPanelButton != null)
            dlPanelButton.onClick.AddListener(ShowDLPanel);

        if (setupPanelButton != null)
            setupPanelButton.onClick.AddListener(ShowSetupPanel);
    }

    public void ShowDLPanel()
    {
        if (dlPanel != null)
            dlPanel.SetActive(true);

        if (setupPanel != null)
            setupPanel.SetActive(false);

        if (votVInstaller != null)
            votVInstaller.enabled = true;

        if (votVManualDL != null)
            votVManualDL.enabled = false;

        UnityEngine.Debug.Log("DL Panel activated - VotVInstaller enabled, VotVManualDL disabled");
    }

    public void ShowSetupPanel()
    {
        if (dlPanel != null)
            dlPanel.SetActive(false);

        if (setupPanel != null)
            setupPanel.SetActive(true);

        if (votVInstaller != null)
            votVInstaller.enabled = false;

        if (votVManualDL != null)
            votVManualDL.enabled = true;

        UnityEngine.Debug.Log("Setup Panel activated - VotVManualDL enabled, VotVInstaller disabled");
    }
}
