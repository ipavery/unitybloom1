using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;
    public float pixelsPerSecond = 100f;
    public float snapInterval = 1f;   // seconds

    private Vector2 offset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out offset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            rectTransform.localPosition = new Vector3(localPoint.x - offset.x, rectTransform.localPosition.y, 0);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Snap to nearest interval
        float time = rectTransform.localPosition.x / pixelsPerSecond;
        float snappedTime = Mathf.Round(time / snapInterval) * snapInterval;
        rectTransform.localPosition = new Vector3(snappedTime * pixelsPerSecond, rectTransform.localPosition.y, 0);
    }
}