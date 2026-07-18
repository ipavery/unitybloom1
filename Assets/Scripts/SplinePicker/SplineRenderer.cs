using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages spline selection, spline highlighting, and curve/tangent rendering
/// </summary>
public class SplineRenderer : MonoBehaviour
{
    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;

    [Header("Main Curve Settings")]
    [SerializeField] private int pointsPerSpline = 20; 
    [SerializeField] private Material lineMaterial = null; 
    [SerializeField] private Material highlightedLineMaterial = null; 
    [SerializeField] private float lineWidth = .01f; 

    [Header("Tangent Handle Settings")]
    [SerializeField] private Material tangentMaterial = null; 
    [SerializeField] private float tangentLineWidth = .005f;

    // reference to ParticleManager to get the list of splines to render
    [SerializeField] private ParticleManager particleManager;

    void OnEnable()
    {
        EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Subscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Subscribe<HideShow3DUI>(OnHideShow3DUI);
        EventHub.Subscribe<SplineSelectionChange>(OnSplineSelectionChange);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Unsubscribe<HideShow3DUI>(OnHideShow3DUI);
        EventHub.Unsubscribe<SplineSelectionChange>(OnSplineSelectionChange);
    }

    void Update()
    {
        // this could be made more efficient by only updating the spline that is being updated, 
        // but for now it will update all splines every frame
        foreach (var group in particleManager.splineParticleGroup)
        {
            if (group.spline != null)
            {
                UpdateSplineVisuals(group.spline);
            }
        }
    }

    void OnSplineUpdated(SplineUpdated e)
    {
        if (e.spline != null)
        {
            UpdateSplineVisuals(e.spline);
        }
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        InitializeSpline(e.spline);
    }

    void OnHideShow3DUI(HideShow3DUI e)
    {
        if (e.hide == true)
        {
            foreach (var spline in particleManager.splineParticleGroup.Select(g => g.spline).Where(s => s != null))
            {
                foreach (var lr in spline.GetComponentsInChildren<LineRenderer>())
                {
                    lr.enabled = false;
                }
            }
        }
        else
        {
            foreach (var spline in particleManager.splineParticleGroup.Select(g => g.spline).Where(s => s != null))
            {
                foreach (var lr in spline.GetComponentsInChildren<LineRenderer>())
                {
                    lr.enabled = true;
                }
            }
        }
    }
    
    void OnSplineSelectionChange(SplineSelectionChange e)
    {
        if (e.isSelected == true)
        {
            var lr = e.spline.GetComponentInChildren<LineRenderer>();
            lr.material = highlightedLineMaterial;
            lr.widthMultiplier = lineWidth * 1.1f; // make it thicker when selected
        } else
        {
            foreach (var spline in particleManager.splineParticleGroup.Select(g => g.spline).Where(s => s != null))
            {
                var lr = spline.GetComponentInChildren<LineRenderer>();
                lr.material = lineMaterial;
                lr.widthMultiplier = lineWidth;
            }
        }
    }

    void InitializeSpline(BezierSpline spline)
    {
        // Create the main curve line
        GameObject lineObj = new GameObject("SplineLine");
        lineObj.transform.SetParent(spline.transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = pointsPerSpline + 1;
        lr.material = lineMaterial;
        lr.widthMultiplier = lineWidth;
        lr.useWorldSpace = true;

        // Note: Tangent lines aren't initialized here because UpdateSplineVisuals 
        // will dynamically generate them based on the number of knots.
    }

    /// <summary>
    /// Centralized method to update both the main curve and the tangent handle lines.
    /// </summary>
    void UpdateSplineVisuals(BezierSpline spline)
    {
        // --- 1. Update the Main Curve ---
        Transform mainLineTransform = spline.transform.Find("SplineLine");
        if (mainLineTransform != null && mainLineTransform.TryGetComponent<LineRenderer>(out var mainLr))
        {
            // Note: points.Length * 20 is heavily unoptimized if points array gets large,
            // but preserving your original sampling logic here.
            mainLr.positionCount = spline.points.Length * 20; 
            for (int i = 0; i < mainLr.positionCount; i++)
            {
                float t = i / (float)mainLr.positionCount;
                mainLr.SetPosition(i, spline.GetPoint(t));
            }
        }

        // --- 2. Update the Tangent Lines ---
        UpdateTangentLines(spline);
    }

    void UpdateTangentLines(BezierSpline spline)
    {
        // Find or create a container to keep the hierarchy clean
        Transform tangentContainer = spline.transform.Find("TangentLines");
        if (tangentContainer == null)
        {
            GameObject containerObj = new GameObject("TangentLines");
            containerObj.transform.SetParent(spline.transform, false);
            tangentContainer = containerObj.transform;
        }

        // Calculate how many anchors (knots) exist in this spline
        int knotCount = (spline.points.Length - 1) / 3 + 1;

        // Add new LineRenderers if the user added curves
        while (tangentContainer.childCount < knotCount)
        {
            GameObject tlObj = new GameObject("TangentLine_" + tangentContainer.childCount);
            tlObj.transform.SetParent(tangentContainer, false);
            
            LineRenderer tl = tlObj.AddComponent<LineRenderer>();
            tl.material = tangentMaterial;
            tl.widthMultiplier = tangentLineWidth;
            tl.useWorldSpace = true;
        }

        // Remove excess LineRenderers if the user deleted curves
        while (tangentContainer.childCount > knotCount)
        {
            // Detach it from the parent so childCount updates instantly in this frame
            Transform excessLine = tangentContainer.GetChild(tangentContainer.childCount - 1);
            excessLine.SetParent(null); 
            
            Destroy(excessLine.gameObject);
        }

        // Position the lines
        for (int i = 0; i < knotCount; i++)
        {
            LineRenderer tl = tangentContainer.GetChild(i).GetComponent<LineRenderer>();
            int knotIndex = i * 3;

            // Convert local math points to world space for the LineRenderer
            if (i == 0)
            {
                // First knot: [Anchor -> Forward Tangent]
                tl.positionCount = 2;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[0]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[1]));
            }
            else if (i == knotCount - 1)
            {
                // Last knot: [Backward Tangent -> Anchor]
                tl.positionCount = 2;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[knotIndex - 1]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[knotIndex]));
            }
            else
            {
                // Middle knots: [Backward Tangent -> Anchor -> Forward Tangent]
                tl.positionCount = 3;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[knotIndex - 1]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[knotIndex]));
                tl.SetPosition(2, spline.transform.TransformPoint(spline.points[knotIndex + 1]));
            }
        }
    }
}