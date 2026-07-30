using UnityEngine;
using UnityEngine.UI;

public class FirstPanelManager : MonoBehaviour
{
    public GameObject panel1;
    public GameObject panel2;
    public GameObject panel3;

    public GameObject conteneurBoutonsPanel2;
    public GameObject conteneurBoutonsPanel3;

    void Start()
    {
        panel1.SetActive(true);
        panel2.SetActive(false);
        panel3.SetActive(false);

        WireButtons(conteneurBoutonsPanel2, OpenPanel2);
        WireButtons(conteneurBoutonsPanel3, OpenPanel3);
    }

    void WireButtons(GameObject conteneur, UnityEngine.Events.UnityAction action)
    {
        if (conteneur == null) return;

        Button[] boutons = conteneur.GetComponentsInChildren<Button>(true);
        foreach (Button btn in boutons)
        {
            btn.onClick.AddListener(action);
        }
    }

    public void OpenPanel2()
    {
        panel2.SetActive(true);
        panel1.SetActive(false);
    }

    public void OpenPanel3()
    {
        panel3.SetActive(true);
        panel1.SetActive(false);
    }
}