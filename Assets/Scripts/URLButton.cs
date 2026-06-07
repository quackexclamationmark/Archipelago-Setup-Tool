using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class URLButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("BUTTON")]
    public Button button;

    [Header("URL")]
    public string url = "";

    [Header("LINK TYPE")]
    public int linkType = 0;

    [Header("BUTTON SWITCH (si linkType = 1)")]
    public Button targetButton;
    public Button buttonToDeactivate;

    void Start()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Change le curseur en mode clic (doigt)
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Réinitialise le curseur par défaut
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void OnButtonClicked()
    {
        switch (linkType)
        {
            case 0: // OpenURL
                OpenURL();
                break;

            case 1: // SwitchButton
                SwitchButton();
                break;
        }
    }

    void OpenURL()
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
            UnityEngine.Debug.Log("Opening URL: " + url);
        }
        else
        {
            UnityEngine.Debug.LogWarning("URL is empty!");
        }
    }

    void SwitchButton()
    {
        if (targetButton != null)
        {
            targetButton.interactable = true;
            UnityEngine.Debug.Log("Activated: " + targetButton.gameObject.name);
        }

        if (buttonToDeactivate != null)
        {
            buttonToDeactivate.interactable = false;
            UnityEngine.Debug.Log("Deactivated: " + buttonToDeactivate.gameObject.name);
        }
    }
}
