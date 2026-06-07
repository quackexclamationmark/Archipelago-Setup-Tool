using UnityEngine;

public class HostingPanelManager : MonoBehaviour
{
    public GameObject nitradoPanel;
    public GameObject aternosPanel;
    public GameObject aklxPanel;
    public GameObject gtxgamingPanel;
    // Ajoute d'autres panels ici selon tes besoins

    void Start()
    {
        HideAllPanels();
    }

    public void ShowPanel(string hostingName)
    {
        HideAllPanels();

        switch (hostingName)
        {
            case "Nitrado":
                nitradoPanel.SetActive(true);
                break;
            case "Aternos":
                aternosPanel.SetActive(true);
                break;
            case "AKLX":
                aklxPanel.SetActive(true);
                break;
            case "GTXGaming":
                gtxgamingPanel.SetActive(true);
                break;
                // Ajoute d'autres cases ici
        }
    }

    void HideAllPanels()
    {
        if (nitradoPanel != null) nitradoPanel.SetActive(false);
        if (aternosPanel != null) aternosPanel.SetActive(false);
        if (aklxPanel != null) aklxPanel.SetActive(false);
        if (gtxgamingPanel != null) gtxgamingPanel.SetActive(false);
        // Ajoute d'autres vérifications ici
    }
}
