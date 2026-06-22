using UnityEngine;
using UnityEngine.UI;

public class PanelOpener : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    /*
    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
    */

    public void OpenPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void ClosePanel()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
}