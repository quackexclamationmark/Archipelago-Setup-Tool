using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        CursorManager.instance.SetClickableCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorManager.instance.SetDefaultCursor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CursorManager.instance.SetDefaultCursor();
    }
}