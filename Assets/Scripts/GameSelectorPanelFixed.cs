using UnityEngine;
using UnityEngine.UI;

public class GameSelectorPanelFixed : MonoBehaviour
{
    [SerializeField] private float minWidth = 400f;
    [SerializeField] private float minHeight = 600f;

    private RectTransform rectTransform;
    private LayoutElement layoutElement;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
    }

    void Update()
    {
        // LE PANEL NE RÉTRÉCIT JAMAIS
        layoutElement.minWidth = minWidth;
        layoutElement.minHeight = minHeight;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        // Force minimum
        Vector2 size = rectTransform.sizeDelta;
        if (size.x < minWidth) size.x = minWidth;
        if (size.y < minHeight) size.y = minHeight;
        rectTransform.sizeDelta = size;
    }
}