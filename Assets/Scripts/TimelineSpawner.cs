using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class TimelineSpawner : MonoBehaviour
{
    [Header("References")]
    public RectTransform timelineContent;
    GameObject draggablePrefab;          // Prefab for draggable items
    public TimelineSettings timelineSettings;   // Reference to the settings scriptable object
    public RectTransform timelineScrollRectContent;
    float pixelsPerSecond;            // Matches your timeline scale
    float snapInterval;             // Seconds per snap interval

    public ParticleManager particleManager; // Reference to the ParticleManager if needed
    List<SplineParticleGroup> items; // List of spline items to spawn clips for
    private List<DraggableClip> spawnedClips = new List<DraggableClip>();

    void OnEnable()
    {
        EventHub.Subscribe<NewSplineCreated>(OnSplineCreated);
        EventHub.Subscribe<DeleteSpline>(OnSplineDeleted);
        EventHub.Subscribe<ReloadLerpUI>(OnReloadLerpUI);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnSplineCreated);
        EventHub.Unsubscribe<DeleteSpline>(OnSplineDeleted);
        EventHub.Unsubscribe<ReloadLerpUI>(OnReloadLerpUI);
    }

    void OnReloadLerpUI(ReloadLerpUI e)
    {
        foreach (var splineParticle in particleManager.splineParticleGroup)
        {
            if (splineParticle.spline.lerpSpline != null && splineParticle.spline.lerpSpline != splineParticle.spline)
            {
                foreach (var clip in spawnedClips)
                {
                    if (clip.attachedSpline == splineParticle.spline)
                    {
                        EventHub.Publish(new ClipLerpClick { clipObj = clip.gameObject, reset = false });
                    }
                }
                foreach (var clip in spawnedClips)
                {
                    if (clip.attachedSpline == splineParticle.spline.lerpSpline)
                    {
                        EventHub.Publish(new ClipLerpClick { clipObj = clip.gameObject });
                    }
                }
            }
        }
    }

    void OnSplineCreated(NewSplineCreated e)
    {
        AddClip(e);
    }
    void OnSplineDeleted(DeleteSpline e)
    {
        foreach (var clip in spawnedClips)
        {
            if (clip.attachedSpline == e.spline)
            {
                spawnedClips.Remove(clip);
                Destroy(clip.gameObject);
                break;
            }
        }

    }

    void Start()
    {
        pixelsPerSecond = timelineSettings.pixelsPerSecond;
        snapInterval = timelineSettings.snapInterval;
        draggablePrefab = timelineSettings.draggablePrefab;
        items = particleManager.splineParticleGroup; //for number of clips and clip names
        SpawnClips();
    }

    void AddClip(NewSplineCreated e)
    {

        GameObject clipObj = Instantiate(draggablePrefab, timelineContent);
        clipObj.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = e.spline.name; // Assuming BezierSpline has a name property
        clipObj.name = $"Clip_{e.spline.name}";
        clipObj.GetComponent<DraggableClip>().attachedSpline = e.spline; // Assign the spline to the draggable clip

        // Position at 0s (left of the timeline)
        RectTransform rt = clipObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, .9f);
        rt.anchorMax = new Vector2(0, .9f);
        // (rectTransform.localPosition.x - rectTransform.rect.width/2) / pixelsPerSecond
        float timePosition = e.spline.startTimeOffset * timelineSettings.pixelsPerSecond;
        rt.anchoredPosition = new Vector2(draggablePrefab.GetComponent<RectTransform>().rect.width / 2 + timePosition, -spawnedClips.Count * (rt.sizeDelta.y / 1.5f));

        // Offset vertically so they don't overlap (just for visibility)

        // Configure draggable behavior
        DraggableClip draggable = clipObj.GetComponent<DraggableClip>();
        draggable.pixelsPerSecond = pixelsPerSecond;
        draggable.snapInterval = snapInterval;

        spawnedClips.Add(draggable);
        timelineScrollRectContent.sizeDelta = new Vector2(timelineScrollRectContent.sizeDelta.x, spawnedClips.Count * draggablePrefab.GetComponent<RectTransform>().sizeDelta.y - 170);
    }

    public void SpawnClips()
    {
        // // Clear old clips
        // foreach (var clip in spawnedClips)
        // {
        //     if (clip != null)
        //         Destroy(clip.gameObject);
        // }
        // spawnedClips.Clear();

        // Spawn new clips

        for (int i = 0; i < items.Count; i++)
        {

            GameObject clipObj = Instantiate(draggablePrefab, timelineContent);
            clipObj.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = items[i].spline.name; // Assuming BezierSpline has a name property
            clipObj.name = $"Clip_{i}_{items[i].spline.name}";
            clipObj.GetComponent<DraggableClip>().attachedSpline = items[i].spline; // Assign the spline to the draggable clip

            // Position at 0s (left of the timeline)
            RectTransform rt = clipObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, .9f);
            rt.anchorMax = new Vector2(0, .9f);
            // (rectTransform.localPosition.x - rectTransform.rect.width/2) / pixelsPerSecond
            float timePosition = items[i].spline.startTimeOffset * timelineSettings.pixelsPerSecond;
            rt.anchoredPosition = new Vector2(draggablePrefab.GetComponent<RectTransform>().rect.width / 2 + timePosition, -i * (rt.sizeDelta.y / 1.5f));

            // Offset vertically so they don't overlap (just for visibility)

            // Configure draggable behavior
            DraggableClip draggable = clipObj.GetComponent<DraggableClip>();
            draggable.pixelsPerSecond = pixelsPerSecond;
            draggable.snapInterval = snapInterval;

            spawnedClips.Add(draggable);




        }
        timelineScrollRectContent.sizeDelta = new Vector2(timelineScrollRectContent.sizeDelta.x, spawnedClips.Count * draggablePrefab.GetComponent<RectTransform>().sizeDelta.y - 170);
    }
}
