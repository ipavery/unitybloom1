using UnityEngine;

/// <summary>
/// Manages control points and lines
/// </summary>

public class ControlPointController : MonoBehaviour
{
    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;

    [SerializeField] private GameObject controlPointSpherePrefab;

    void OnEnable()
    {
        EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Subscribe<ControlPointSelected>(OnControlPointSelected);
        EventHub.Subscribe<ClearSelection>(OnClearSelection);
        EventHub.Subscribe<DeleteSpline>(OnDeleteSpline);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<ControlPointSelected>(OnControlPointSelected);
        EventHub.Unsubscribe<ClearSelection>(OnClearSelection);
        EventHub.Unsubscribe<DeleteSpline>(OnDeleteSpline);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // var splineList = particleManager.splineParticleGroup.Select(g => g.spline).ToList();
        // Save System scripts call the create spline event, so don't need to create control points here.
        SPD.controlSphereGroup.Clear(); // Clear the control sphere group list at the start
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        Debug.Log("OnNewSplineCreated called in ControlPointController");
        GameObject sphereContainer = new("SphereContainer");
        sphereContainer.transform.SetParent(e.spline.transform, false);

        for (int i = 0; i < e.spline.points.Length; i++)
        {
            Vector3 point = e.spline.points[i];
            GameObject sphere = Instantiate(controlPointSpherePrefab, point, Quaternion.identity);
            sphere.transform.SetParent(sphereContainer.transform, false);

            // Set the sphere to the "PP Layer"
            sphere.layer = LayerMask.NameToLayer("PP Layer");
            SPD.controlSphereGroup.Add(new ControlPointGroup
            {
                sphereObject = sphere,
                isSelected = false,
                index = i
            });

            // Set and store original color as white
            if (sphere.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = Color.white;
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", Color.white * SPD.gizmoEmissionIntensity);
            }
        }
    }

    void OnControlPointSelected(ControlPointSelected e)
    {
        var sphereGroup = e.sphereGroup;
        if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rendNew))
        {
            SPD.lastHighlighted = sphereGroup.sphereObject;
            sphereGroup.isSelected = true; // Mark the control point as selected
            rendNew.material.color = SPD.highlightColor;
            rendNew.material.SetColor("_EmissionColor", SPD.highlightColor * SPD.gizmoEmissionIntensity);
        }
    }

    void OnClearSelection(ClearSelection e)
    {
        UnselectAllPoints();
    }

    void OnDeleteSpline(DeleteSpline e)
    {
        //FIX THIS
        Debug.Log("OnDeleteSpline called in ControlPointController");
        Debug.Log(e.spline.transform.GetChild(0).childCount);
        for (int i = 0; i < e.spline.transform.GetChild(0).childCount; i++)
        {
            var child = e.spline.transform.GetChild(0).GetChild(i).gameObject;
            SPD.controlSphereGroup.RemoveAll(sphereGroup => sphereGroup.sphereObject == child);
        }
        //splineList.Remove(e.spline);
    }

    /// <summary>
    /// Called when the user clicks somewhere empty to clear the selection and make
    /// the highlighted sphere return to normal color
    /// </summary>
    /// ISSUE: if the user clicks on load or anything on the save load ui, this unselect thing gets triggered.
    void UnselectAllPoints()
    {
        SPD.lastHighlighted = null; // Clear last highlighted
        
        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup == null || sphereGroup.sphereObject == null)
                continue;
            sphereGroup.isSelected = false; // Unselect current control point
            if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = SPD.unselectedOriginalColor; // Reset to original color
                rend.material.SetColor("_EmissionColor", SPD.unselectedOriginalColor * SPD.gizmoEmissionIntensity);
            }
        }
    }
    
}
