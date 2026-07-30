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
    public Button targetButton2;
    public Button buttonToDeactivate2;

    [Header("TEXT TRIGGER (si linkType = 2)")]
    public string triggerText = "";

    void Start()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void OnButtonClicked()
    {
        switch (linkType)
        {
            case 0:
                OpenURL();
                break;

            case 1:
                SwitchButton();
                break;

            case 2:
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
        bool anyAssigned = false;

        if (targetButton != null)
        {
            anyAssigned = true;
            targetButton.interactable = true;
        }
        if (buttonToDeactivate != null)
        {
            anyAssigned = true;
            buttonToDeactivate.interactable = true;
        }
        if (targetButton2 != null)
        {
            anyAssigned = true;
            targetButton2.interactable = true;
        }
        if (buttonToDeactivate2 != null)
        {
            anyAssigned = true;
            buttonToDeactivate2.interactable = true;
        }

        if (targetButton != null)
        {
            try
            {
                targetButton.onClick.Invoke();
                UnityEngine.Debug.Log("Clicked: " + targetButton.gameObject.name);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Erreur en invoquant targetButton (" + targetButton.gameObject.name + "): " + e);
            }
        }

        if (targetButton2 != null)
        {
            try
            {
                targetButton2.onClick.Invoke();
                UnityEngine.Debug.Log("Clicked (2): " + targetButton2.gameObject.name);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Erreur en invoquant targetButton2 (" + targetButton2.gameObject.name + "): " + e);
            }
        }

        if (!anyAssigned)
        {
            UnityEngine.Debug.LogWarning("No target or buttonToDeactivate assigned for SwitchButton!");
        }
    }

    void TriggerByText()
    {
        if (!string.IsNullOrEmpty(triggerText))
        {
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);

            foreach (Button btn in allButtons)
            {
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