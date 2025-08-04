using UnityEngine;

public struct ClipDragEnded
{
    public BezierSpline spline;
    public float timePosition;

    public ClipDragEnded(BezierSpline spline, float timePosition)
    {
        this.spline = spline;
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

public struct SplineSelectionChange
{
    public BezierSpline spline;
    public bool isSelected;
    public SplineSelectionChange(BezierSpline spline, bool isSelected)
    {
        this.spline = spline;
        this.isSelected = isSelected;
    }
}