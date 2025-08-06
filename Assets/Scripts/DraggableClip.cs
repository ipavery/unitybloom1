using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class DraggableClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;
    public TimelineSettings timelineSettings;
    public float pixelsPerSecond; // Horizontal scale of the timeline
    public float snapInterval;     // Seconds per snap interval
    public BezierSpline attachedSpline; // Reference to the current spline
    public Button lerpButton;


    private Vector2 offset;

    void SnapToInterval()
    {
        float time = rectTransform.localPosition.x / pixelsPerSecond;
        float snappedTime = Mathf.Round(time / snapInterval) * snapInterval;
        rectTransform.localPosition = new Vector3(snappedTime * pixelsPerSecond + rectTransform.rect.width/2, rectTransform.localPosition.y, 0);
    }

    void Awake()
    {
        pixelsPerSecond = timelineSettings.pixelsPerSecond;
        snapInterval = timelineSettings.snapInterval;
    }

    void Start()
    {
        lerpButton.onClick.AddListener(LerpButton);
    }

    void LerpButton()
    {
        EventHub.Publish(new ClipLerpClick(gameObject));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Vector2 localMousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localMousePosition);

        // Calculate offset between mouse and the clip's current position
        offset = localMousePosition - rectTransform.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localMousePosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localMousePosition))
        {
            Vector2 newPosition = localMousePosition - offset;
            newPosition.y = rectTransform.anchoredPosition.y; // Keep the vertical position unchanged
            // Clamp to the timeline bounds
            newPosition.x = Mathf.Clamp(newPosition.x, 0, rectTransform.parent.GetComponent<RectTransform>().rect.width - rectTransform.rect.width);

            rectTransform.anchoredPosition = newPosition;
        }

        SnapToInterval();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //restart the particle system
        EventHub.Publish(new ClipDragEnded(attachedSpline, (rectTransform.anchoredPosition.x - rectTransform.rect.width/2) / pixelsPerSecond));
    }
}