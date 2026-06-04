using UnityEngine;

public class GamePanelManager : MonoBehaviour
{
    public GameObject subnauticaPanel;
    public GameObject repoPanel;
    public GameObject powerwashPanel;
    public GameObject coe33Panel;
    public GameObject lethalcompanyPanel;
    public GameObject sts2Panel;
    public GameObject contentwarningPanel;
    public GameObject peaksofyorePanel;
    public GameObject btd6Panel;
    public GameObject votvPanel;

    void Start()
    {
        HideAllPanels();
    }

    public void ShowPanel(string gameName)
    {
        HideAllPanels();

        switch (gameName)
        {
            case "Subnautica":
                subnauticaPanel.SetActive(true);
                break;
            case "REPO":
                repoPanel.SetActive(true);
                break;
            case "PowerwashSimulator":
                powerwashPanel.SetActive(true);
                break;
            case "COE33":
                coe33Panel.SetActive(true);
                break;
            case "LethalCompany":
                lethalcompanyPanel.SetActive(true);
                break;
            case "Slay The Spire 2":
                sts2Panel.SetActive(true);
                break;
            case "ContentWarning":
                contentwarningPanel.SetActive(true);
                break;
            case "PeaksOfYore":
                peaksofyorePanel.SetActive(true);
                break;
            case "BTD6":
                btd6Panel.SetActive(true);
                break;
            case "VoicesOfTheVoid":
                votvPanel.SetActive(true);
                break;
        }
    }

    void HideAllPanels()
    {
        if (subnauticaPanel != null) subnauticaPanel.SetActive(false);
        if (repoPanel != null) repoPanel.SetActive(false);
        if (powerwashPanel != null) powerwashPanel.SetActive(false);
        if (coe33Panel != null) coe33Panel.SetActive(false);
        if (lethalcompanyPanel != null) lethalcompanyPanel.SetActive(false);
        if (sts2Panel != null) sts2Panel.SetActive(false);
        if (contentwarningPanel != null) contentwarningPanel.SetActive(false);
        if (peaksofyorePanel != null) peaksofyorePanel.SetActive(false);
        if (btd6Panel != null) btd6Panel.SetActive(false);
        if (votvPanel != null) votvPanel.SetActive(false);
    }
}