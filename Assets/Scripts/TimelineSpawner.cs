using UnityEngine;
using System.Collections.Generic;

public class TimelineSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform timelineContent;       // The scrollable timeline content area
    public GameObject draggablePrefab;          // Prefab for draggable items

    [Header("Items To Spawn")]
    public List<string> items = new List<string>();  // Example list, can be anything

    [Header("Timeline Settings")]
    public float pixelsPerSecond = 100f;        // Matches your timeline scale
    public float snapInterval = 1f;             // Seconds per snap interval

    private List<DraggableClip> spawnedClips = new List<DraggableClip>();

    void Start()
    {
        SpawnClips();
    }

    public void SpawnClips()
    {
        // Clear old clips
        foreach (var clip in spawnedClips)
        {
            if (clip != null)
                Destroy(clip.gameObject);
        }
        spawnedClips.Clear();

        // Spawn new clips
        for (int i = 0; i < items.Count; i++)
        {
            GameObject clipObj = Instantiate(draggablePrefab, timelineContent);
            clipObj.name = $"Clip_{i}_{items[i]}";

            // Position at 0s (left of the timeline)
            RectTransform rt = clipObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -i * (rt.sizeDelta.y + 10)); 
            // Offset vertically so they don't overlap (just for visibility)

            // Configure draggable behavior
            DraggableClip draggable = clipObj.GetComponent<DraggableClip>();
            draggable.pixelsPerSecond = pixelsPerSecond;
            draggable.snapInterval = snapInterval;

            spawnedClips.Add(draggable);
        }
    }
}
