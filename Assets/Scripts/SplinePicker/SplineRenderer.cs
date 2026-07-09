using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages spline selection, spline highlighting
/// </summary>

public class SplineRenderer : MonoBehaviour
{
    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;

    //local variables that affect how the spline is rendered
    [SerializeField] private int pointsPerSpline = 20; // Number of points to sample along the spline for rendering
    [SerializeField] private Material lineMaterial = null; // Material to use for the linerenderer
    [SerializeField] private float lineWidth = .01f; // Size of linerenderer
    //private BezierSpline currentlyUpdatingSpline = null; // Reference to the spline that is currently being updated

    // reference to ParticleManager to get the list of splines to render
    [SerializeField] private ParticleManager particleManager;

    void OnEnable()
    {
        EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
        // EventHub.Subscribe<ControlPointSelected>(OnControlPointSelected);
        // EventHub.Subscribe<ClearSelection>(OnClearSelection);
        // EventHub.Subscribe<DeleteSpline>(OnDeleteSpline);
        EventHub.Subscribe<SplineUpdated>(OnSplineUpdated);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        // EventHub.Unsubscribe<ControlPointSelected>(OnControlPointSelected);
        // EventHub.Unsubscribe<ClearSelection>(OnClearSelection);
        // EventHub.Unsubscribe<DeleteSpline>(OnDeleteSpline);
        EventHub.Unsubscribe<SplineUpdated>(OnSplineUpdated);
    }

    void Update()
    {

        // --- Update the LineRenderer for this spline if a gizmo drag is occuring ---
        // Find the LineRenderer (assumes it's on a child of the spline GameObject)
        foreach (var group in particleManager.splineParticleGroup)
        {
            var spline = group.spline;
            if (spline == null) continue;

            LineRenderer lr = spline.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                lr.positionCount = spline.points.Length * 20;
                for (int i = 0; i < lr.positionCount; i++)
                {
                    float t = i / (float)lr.positionCount;
                    lr.SetPosition(i, spline.GetPoint(t));
                }
            }
        }
    }

    void OnSplineUpdated(SplineUpdated e)
    {
        if (e.spline == null) return;

        var spline = e.spline;
        LineRenderer lr = spline.GetComponentInChildren<LineRenderer>();
        if (lr != null)
        {
            lr.positionCount = spline.points.Length * 20;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float t = i / (float)lr.positionCount;
                lr.SetPosition(i, spline.GetPoint(t));
            }
        }
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        InitializeSpline(e.spline);
    }

    void InitializeSpline(BezierSpline spline)
    {
        GameObject lineObj = new("SplineLine");
        lineObj.transform.SetParent(spline.transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = pointsPerSpline + 1;
        lr.material = lineMaterial;
        lr.widthMultiplier = lineWidth;
        lr.useWorldSpace = true;

        for (int i = 0; i <= pointsPerSpline; i++)
        {
            float t = i / (float)pointsPerSpline;
            lr.SetPosition(i, spline.GetPoint(t));
        }
    }
}
