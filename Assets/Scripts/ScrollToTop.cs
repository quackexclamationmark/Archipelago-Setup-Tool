using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    private IEnumerator ResetScroll()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    public void ScrollToTopNow()
    {
        StartCoroutine(ResetScroll());
    }
}