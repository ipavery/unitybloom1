using UnityEngine;

public struct ClipDragEnded
{
    public GameObject clip;
    public float timePosition;

    public ClipDragEnded(GameObject clip, float timePosition)
    {
        this.clip = clip;
        this.timePosition = timePosition;
    }
}