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

    [Header("TEXT TRIGGER (si linkType = 2)")]
    public string triggerText = "";

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

            case 2: // TextTrigger
                TriggerByText();
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
        if (targetButton != null && buttonToDeactivate != null)
        {
            // Les deux restent toujours interactifs
            targetButton.interactable = true;
            buttonToDeactivate.interactable = true;

            UnityEngine.Debug.Log("Activated: " + targetButton.gameObject.name);
            UnityEngine.Debug.Log("Kept active: " + buttonToDeactivate.gameObject.name);

            // Simuler un vrai clic sur le bouton cible
            targetButton.onClick.Invoke();
            UnityEngine.Debug.Log("Clicked: " + targetButton.gameObject.name);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Target Button or Button To Deactivate is not assigned!");
        }
    }

    void TriggerByText()
    {
        if (!string.IsNullOrEmpty(triggerText))
        {
            // Cherche tous les boutons dans la scène (sans tri pour plus de performance)
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);

            foreach (Button btn in allButtons)
            {
                // Cherche le texte du bouton
                Text buttonText = btn.GetComponentInChildren<Text>();

                if (buttonText != null && buttonText.text == triggerText)
                {
                    btn.onClick.Invoke();
                    UnityEngine.Debug.Log("Triggered button with text: " + triggerText);
                    return;
                }
            }

            UnityEngine.Debug.LogWarning("No button found with text: " + triggerText);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Trigger text is empty!");
        }
    }
}