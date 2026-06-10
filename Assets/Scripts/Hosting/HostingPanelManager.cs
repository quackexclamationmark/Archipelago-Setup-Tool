using UnityEngine;

public class HostingPanelManager : MonoBehaviour
{
    public GameObject apworldPanel;
    public GameObject corePanel;
    public GameObject yamlPanel;
    public GameObject ressourcesPanel;
    public GameObject clientPanel;
    public GameObject optionscreatorPanel;
    public GameObject deathlinkPanel;
    public GameObject aplauncherPanel;

    void Start()
    {
        HideAllPanels();
    }

    public void ShowPanel(string hostingName)
    {
        HideAllPanels();

        switch (hostingName)
        {
            case "apworld":
                apworldPanel.SetActive(true);
                break;
            case "CoreGames":
                corePanel.SetActive(true);
                break;
            case "yaml":
                yamlPanel.SetActive(true);
                break;
            case "Ressources":
                ressourcesPanel.SetActive(true);
                break;
            case "Client":
                clientPanel.SetActive(true);
                break;
            case "OptionsCreator":
                optionscreatorPanel.SetActive(true);
                break;
            case "DeathLink":
                deathlinkPanel.SetActive(true);
                break;
            case "APLauncher":
                aplauncherPanel.SetActive(true);
                break;
        }
    }

    void HideAllPanels()
    {
        if (apworldPanel != null) apworldPanel.SetActive(false);
        if (corePanel != null) corePanel.SetActive(false);
        if (yamlPanel != null) yamlPanel.SetActive(false);
        if (ressourcesPanel != null) ressourcesPanel.SetActive(false);
        if (clientPanel != null) clientPanel.SetActive(false);
        if (optionscreatorPanel != null) optionscreatorPanel.SetActive(false);
        if (deathlinkPanel != null) deathlinkPanel.SetActive(false);
        if (aplauncherPanel != null) aplauncherPanel.SetActive(false);
    }
}
