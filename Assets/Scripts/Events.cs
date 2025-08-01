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

public struct GizmoDragEnded
{
    public Vector3 position;
    public GameObject gizmo;
    public GizmoDragEnded(GameObject gizmo, Vector3 position)
    {
        this.position = position;
        this.gizmo = gizmo;
    }
}