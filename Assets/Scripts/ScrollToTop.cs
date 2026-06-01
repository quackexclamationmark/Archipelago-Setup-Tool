using UnityEngine;
using UnityEngine.UI;

public class ScrollToTop : MonoBehaviour
{
    public ScrollRect scrollRect;

    void Awake()
    {
        Canvas.ForceUpdateCanvases();
    }

    void Start()
    {
        StartCoroutine(ResetScroll());
    }

    System.Collections.IEnumerator ResetScroll()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}