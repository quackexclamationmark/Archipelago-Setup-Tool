using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ExclusiveToggle : MonoBehaviour, IPointerClickHandler
{
    public Toggle[] allToggles;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Toggle thisToggle = GetComponent<Toggle>();

            foreach (Toggle toggle in allToggles)
            {
                if (toggle != thisToggle)
                    toggle.isOn = false;
            }

            thisToggle.isOn = true;
        }
    }
}