using System.Collections.Generic;
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

public struct ReloadParticles
{
    public bool reload;
    public ReloadParticles(bool reload)
    {
        this.reload = reload;
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

public struct NewSplineCreated
{
    public BezierSpline spline;
    public NewSplineCreated(BezierSpline spline)
    { this.spline = spline; }
}

public struct SplineUpdated
{
    public BezierSpline spline;
    public List<int> updatedIndices;
    public SplineUpdated(BezierSpline spline, List<int> updatedIndices)
    {
        this.spline = spline;
        this.updatedIndices = updatedIndices;
    }
}

public struct SelectSpline
{
    public BezierSpline spline;
    public SelectSpline(BezierSpline spline)
    {
        this.spline = spline;
    }
}