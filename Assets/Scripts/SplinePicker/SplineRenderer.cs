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
    private bool isHidden = false;

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
        // Calculate the camera scale multiplier once per frame
        float scaleMultiplier = GetCameraScaleMultiplier();

        // this could be made more efficient by only updating the spline that is being updated, 
        // but for now it will update all splines every frame[cite: 3]
        foreach (var group in particleManager.splineParticleGroup)
        {
            if (group.spline != null)
            {
                UpdateSplineVisuals(group.spline, scaleMultiplier);
            }
        }
    }

    void OnSplineUpdated(SplineUpdated e)
    {
        if (e.spline != null)
        {
            UpdateSplineVisuals(e.spline, GetCameraScaleMultiplier());
        }
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        InitializeSpline(e.spline);
    }

    void OnHideShow3DUI(HideShow3DUI e)
    {
        isHidden = e.hide;

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
        // Only handle material swapping here; the Update loop handles the dynamic width scaling.
        if (e.isSelected == true)
        {
            var lr = e.spline.GetComponentInChildren<LineRenderer>();
            lr.sharedMaterial = highlightedLineMaterial; 
        } 
        else
        {
            foreach (var spline in particleManager.splineParticleGroup.Select(g => g.spline).Where(s => s != null))
            {
                var lr = spline.GetComponentInChildren<LineRenderer>();
                lr.sharedMaterial = lineMaterial;
            }
        }
    }

    void InitializeSpline(BezierSpline spline)
    {
        // Create the main curve line[cite: 3]
        GameObject lineObj = new GameObject("SplineLine");
        lineObj.transform.SetParent(spline.transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = pointsPerSpline + 1;
        lr.material = lineMaterial;
        
        // Initial width calculation using camera distance
        lr.widthMultiplier = lineWidth * GetCameraScaleMultiplier(); 
        lr.useWorldSpace = true;

        // Note: Tangent lines aren't initialized here because UpdateSplineVisuals 
        // will dynamically generate them based on the number of knots.[cite: 3]
        lr.enabled = !isHidden;
    }

    /// <summary>
    /// Calculates the scaling multiplier based on the camera's Z distance.
    /// </summary>
    private float GetCameraScaleMultiplier()
    {
        if (Camera.main == null) return 1f;

        float cameraDistanceToZ = Mathf.Abs(Camera.main.transform.position.z);
        return Mathf.Max(0.01f, cameraDistanceToZ);
    }

    /// <summary>
    /// Centralized method to update both the main curve and the tangent handle lines.[cite: 3]
    /// </summary>
    void UpdateSplineVisuals(BezierSpline spline, float scaleMultiplier)
    {
        // --- 1. Update the Main Curve ---[cite: 3]
        Transform mainLineTransform = spline.transform.Find("SplineLine");
        if (mainLineTransform != null && mainLineTransform.TryGetComponent<LineRenderer>(out var mainLr))
        {
            // Note: points.Length * 20 is heavily unoptimized if points array gets large,
            // but preserving your original sampling logic here.[cite: 3]
            mainLr.positionCount = spline.points.Length * 20; 
            for (int i = 0; i < mainLr.positionCount; i++)
            {
                float t = i / (float)mainLr.positionCount;
                mainLr.SetPosition(i, spline.GetPoint(t));
            }

            // Apply dynamic width based on whether the line is highlighted or not
            float currentBaseWidth = (mainLr.sharedMaterial == highlightedLineMaterial) ? (lineWidth * 1.1f) : lineWidth;
            mainLr.widthMultiplier = currentBaseWidth * scaleMultiplier;
        }

        // --- 2. Update the Tangent Lines ---[cite: 3]
        UpdateTangentLines(spline, scaleMultiplier);
    }

    void UpdateTangentLines(BezierSpline spline, float scaleMultiplier)
    {
        // Find or create a container to keep the hierarchy clean[cite: 3]
        Transform tangentContainer = spline.transform.Find("TangentLines");
        if (tangentContainer == null)
        {
            GameObject containerObj = new GameObject("TangentLines");
            containerObj.transform.SetParent(spline.transform, false);
            tangentContainer = containerObj.transform;
        }

        // Calculate how many anchors (knots) exist in this spline[cite: 3]
        int knotCount = (spline.points.Length - 1) / 3 + 1;

        // Add new LineRenderers if the user added curves[cite: 3]
        while (tangentContainer.childCount < knotCount)
        {
            GameObject tlObj = new GameObject("TangentLine_" + tangentContainer.childCount);
            tlObj.transform.SetParent(tangentContainer, false);
            
            LineRenderer tl = tlObj.AddComponent<LineRenderer>();
            tl.material = tangentMaterial;
            tl.useWorldSpace = true;
            tl.enabled = !isHidden;
        }

        // Remove excess LineRenderers if the user deleted curves[cite: 3]
        while (tangentContainer.childCount > knotCount)
        {
            // Detach it from the parent so childCount updates instantly in this frame[cite: 3]
            Transform excessLine = tangentContainer.GetChild(tangentContainer.childCount - 1);
            excessLine.SetParent(null); 
            
            Destroy(excessLine.gameObject);
        }

        // Position and scale the lines
        for (int i = 0; i < knotCount; i++)
        {
            LineRenderer tl = tangentContainer.GetChild(i).GetComponent<LineRenderer>();
            int knotIndex = i * 3;

            // Apply dynamic scaling to tangent width
            tl.widthMultiplier = tangentLineWidth * scaleMultiplier;

            // Convert local math points to world space for the LineRenderer[cite: 3]
            if (i == 0)
            {
                // First knot: [Anchor -> Forward Tangent][cite: 3]
                tl.positionCount = 2;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[0]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[1]));
            }
            else if (i == knotCount - 1)
            {
                // Last knot: [Backward Tangent -> Anchor][cite: 3]
                tl.positionCount = 2;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[knotIndex - 1]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[knotIndex]));
            }
            else
            {
                // Middle knots: [Backward Tangent -> Anchor -> Forward Tangent][cite: 3]
                tl.positionCount = 3;
                tl.SetPosition(0, spline.transform.TransformPoint(spline.points[knotIndex - 1]));
                tl.SetPosition(1, spline.transform.TransformPoint(spline.points[knotIndex]));
                tl.SetPosition(2, spline.transform.TransformPoint(spline.points[knotIndex + 1]));
            }
        }
    }
}