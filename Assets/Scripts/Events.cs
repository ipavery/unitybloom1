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

/// <summary>
/// Below events are for SplinePicker folder scripts
/// </summary>
/// 
// Gizmo events:
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

public struct ShowMoveGizmosEvent
{
    public Vector3 position;
    public ShowMoveGizmosEvent(Vector3 position)
    {
        this.position = position;
    }
}

public struct GizmoDragStarted
{
    public Vector3 position;
    public GizmoDragStarted(Vector3 position)
    {
        this.position = position;
    }
}

public struct ControlPointSelected
{
    public ControlPointGroup sphereGroup;
    public ControlPointSelected(ControlPointGroup sphereGroup)
    {
        this.sphereGroup = sphereGroup;
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
    public bool isDrawSpline;
    public NewSplineCreated(BezierSpline spline, bool isDrawSpline = true)
    {
        this.spline = spline;
        this.isDrawSpline = isDrawSpline;
     }
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

public struct DeleteSpline
{
    public BezierSpline spline;
    public DeleteSpline(BezierSpline spline)
    {
        this.spline = spline;
    }
}

public struct ClipLerpClick
{
    public GameObject clipObj;
    public bool reset;
    public ClipLerpClick(GameObject clipObj, bool reset = true)
    {
        this.clipObj = clipObj;
        this.reset = reset;
    }
}

public struct ReloadLerpUI
{
    public bool reload;
    public ReloadLerpUI(bool reload)
    {
        this.reload = reload;
    }
}

public struct HideShow3DUI
{
    public bool hide;
    public HideShow3DUI(bool hide)
    {
        this.hide = hide;
    }
}

// selection events
public struct SelectionStartEnd
{
    public Vector2 position;
    public bool isStart;
    public SelectionStartEnd(Vector2 position, bool isStart)
    {
        this.position = position;
        this.isStart = isStart;
    }
}

public struct ClearSelection
{
    bool clear;
    public ClearSelection(bool clear = true)
    {
        this.clear = clear;
    }
}