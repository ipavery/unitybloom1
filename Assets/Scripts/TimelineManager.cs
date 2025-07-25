using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TimelineManager : MonoBehaviour
{
    public RectTransform content;            // The main timeline track
    public RectTransform timestampContainer; // Container for timestamps
    public GameObject timestampPrefab;       // Prefab (TextMeshProUGUI)

    [Header("Timeline Settings")]
    public float duration = 10f;        // Total timeline duration in seconds
    public float pixelsPerSecond = 100; // Horizontal scale of timeline
    public float tickInterval = 1f;     // Spacing between timestamps (in seconds)

    private void Start()
    {
        GenerateTimestamps();
    }

    public void GenerateTimestamps()
    {
        // Clear existing timestamps
        foreach (Transform child in timestampContainer)
        {
            Destroy(child.gameObject);
        }

        // Set the content width based on duration
        float totalWidth = duration * pixelsPerSecond;
        content.sizeDelta = new Vector2(totalWidth, content.sizeDelta.y);

        // Generate timestamps
        for (float t = 0; t <= duration; t += tickInterval)
        {
            GameObject tick = Instantiate(timestampPrefab, timestampContainer);
            var text = tick.GetComponent<TextMeshProUGUI>();
            text.text = $"{t:0.00}s";

            // Position the timestamp
            RectTransform tickRect = tick.GetComponent<RectTransform>();
            tickRect.anchoredPosition = new Vector2(t * pixelsPerSecond, 0);
        }
    }

    // Optional: Call this when you change duration or zoom
    public void UpdateTimeline(float newDuration, float newPixelsPerSecond)
    {
        duration = newDuration;
        pixelsPerSecond = newPixelsPerSecond;
        GenerateTimestamps();
    }
}
