using UnityEngine;

[CreateAssetMenu(fileName = "TimelineSettings", menuName = "Isaac_Timeline/Settings")]
public class TimelineSettings : ScriptableObject
{
    [Header("Timeline Settings")]
    public float pixelsPerSecond = 1000f;
    public float snapInterval = .1f;
    public float duration = 10f;

    [Header("Prefabs")]
    public GameObject timestampPrefab; // Prefab for timestamps (TextMeshProUGUI)
    public GameObject draggablePrefab; // Prefab for draggable items

}
