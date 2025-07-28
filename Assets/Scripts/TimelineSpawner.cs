using UnityEngine;
using System.Collections.Generic;
using Mono.Cecil.Cil;

public class TimelineSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform timelineContent;
    GameObject draggablePrefab;          // Prefab for draggable items
    public TimelineSettings timelineSettings;   // Reference to the settings scriptable object
    

    [Header("Items To Spawn")]
    public List<string> items = new List<string>();  // Example list, can be anything

    float pixelsPerSecond;            // Matches your timeline scale
    
    float snapInterval;             // Seconds per snap interval

    private List<DraggableClip> spawnedClips = new List<DraggableClip>();

    void Start()
    {
        pixelsPerSecond = timelineSettings.pixelsPerSecond;
        snapInterval = timelineSettings.snapInterval;
        draggablePrefab = timelineSettings.draggablePrefab;
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
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(draggablePrefab.GetComponent<RectTransform>().rect.width / 2, -i * (rt.sizeDelta.y + 10)); 
            // Offset vertically so they don't overlap (just for visibility)

            // Configure draggable behavior
            DraggableClip draggable = clipObj.GetComponent<DraggableClip>();
            draggable.pixelsPerSecond = pixelsPerSecond;
            draggable.snapInterval = snapInterval;

            spawnedClips.Add(draggable);
        }
    }
}
