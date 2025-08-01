using UnityEngine;
using System.Collections.Generic;
using Mono.Cecil.Cil;

public class TimelineSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform timelineContent;
    GameObject draggablePrefab;          // Prefab for draggable items
    public TimelineSettings timelineSettings;   // Reference to the settings scriptable object
    float pixelsPerSecond;            // Matches your timeline scale
    float snapInterval;             // Seconds per snap interval

    public ParticleManager particleManager; // Reference to the ParticleManager if needed
    List<SplineParticleGroup> items; // List of spline items to spawn clips for
    private List<DraggableClip> spawnedClips = new List<DraggableClip>();

    void Start()
    {
        pixelsPerSecond = timelineSettings.pixelsPerSecond;
        snapInterval = timelineSettings.snapInterval;
        draggablePrefab = timelineSettings.draggablePrefab;
        items = particleManager.splineParticleGroup; //for number of clips and clip names
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
            clipObj.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = items[i].spline.name; // Assuming BezierSpline has a name property
            clipObj.name = $"Clip_{i}_{items[i]}";
            clipObj.GetComponent<DraggableClip>().attachedSpline = items[i].spline; // Assign the spline to the draggable clip

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
