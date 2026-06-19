using UnityEngine;
using UnityEngine.UI;

public class ClosePanel : MonoBehaviour
{
    [SerializeField] private GameObject panelToClose;

    void Start()
    {
        Button closeButton = GetComponent<Button>();
        closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    void OnCloseButtonClicked()
    {
        panelToClose.SetActive(false);
    }
}