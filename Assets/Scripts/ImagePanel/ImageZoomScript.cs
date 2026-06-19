using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ImageZoomScript : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private GameObject zoomPanel;
    [SerializeField] private Image zoomImageDisplay;

    void Start()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        EventTrigger trigger = targetImage.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = targetImage.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((data) => OnImageClicked());
        trigger.triggers.Add(entry);

        zoomPanel.SetActive(false);
    }

    void OnImageClicked()
    {
        // Afficher le panel
        zoomPanel.SetActive(true);

        // Copier l'image sur le panel
        zoomImageDisplay.sprite = targetImage.sprite;
    }
}