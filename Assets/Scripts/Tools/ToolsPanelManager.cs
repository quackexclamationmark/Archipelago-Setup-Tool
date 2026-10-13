using UnityEngine;

public class ToolsPanelManager : MonoBehaviour
{
    public GameObject archipelagolauncherPanel;
    public GameObject universaltrackerPanel;

    void Start()
    {
        HideAllPanels();
    }

    public void ShowPanel(string toolsName)
    {
        HideAllPanels();

        switch (toolsName)
        {
            case "ArchipelagoLauncher":
                archipelagolauncherPanel.SetActive(true);
                break;
            case "UniversalTracker":
                universaltrackerPanel.SetActive(true);
                break;
        }
    }

    void HideAllPanels()
    {
        if (archipelagolauncherPanel != null) archipelagolauncherPanel.SetActive(false);
        if (universaltrackerPanel != null) universaltrackerPanel.SetActive(false);
    }
}
