using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class TimelineManager : MonoBehaviour
{
    public RectTransform content;            // The main timeline track
    public ScrollRect scrollRect;
    public RectTransform timestampContainer; // Container for timestamps
    GameObject timestampPrefab;       // Prefab (TextMeshProUGUI)

    public TimelineSettings timelineSettings;

    private void Start()
    {
        timestampPrefab = timelineSettings.timestampPrefab;
        GenerateTimestamps();
        scrollRect.horizontalNormalizedPosition = 0f;
    }

    public void GenerateTimestamps()
    {
        // Clear existing timestamps
        foreach (Transform child in timestampContainer)
        {
            Destroy(child.gameObject);
        }

        // Set the content width based on duration
        float totalWidth = timelineSettings.duration * timelineSettings.pixelsPerSecond;
        content.sizeDelta = new Vector2(totalWidth, content.sizeDelta.y);

        float draggableClipWidth = timelineSettings.draggablePrefab.GetComponent<RectTransform>().rect.width / 2;

        // Generate timestamps
        for (float t = 0; t <= timelineSettings.duration; t += timelineSettings.snapInterval)
        {
            GameObject tick = Instantiate(timestampPrefab, timestampContainer);
            var text = tick.GetComponent<TextMeshProUGUI>();
            text.text = $"{t:0.00}s";

            // Position the timestamp
            RectTransform tickRect = tick.GetComponent<RectTransform>();
            tickRect.anchoredPosition = new Vector2(t * timelineSettings.pixelsPerSecond + draggableClipWidth, 0);
        }
    }

    // Optional: Call this when you change duration or zoom
    public void UpdateTimeline(float newDuration, float newPixelsPerSecond)
    {
        timelineSettings.duration = newDuration;
        timelineSettings.pixelsPerSecond = newPixelsPerSecond;
        GenerateTimestamps();
    }
}
