using UnityEngine;

[CreateAssetMenu(fileName = "TimelineSettings", menuName = "Timeline/Settings")]
public class TimelineSettings : ScriptableObject
{
    public float pixelsPerSecond = 100f;
    public float snapInterval = 1f;
}
