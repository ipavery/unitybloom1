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
        EventHub.Subscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Subscribe<HideShow3DUI>(OnHideShow3DUI);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<ControlPointSelected>(OnControlPointSelected);
        EventHub.Unsubscribe<ClearSelection>(OnClearSelection);
        EventHub.Unsubscribe<DeleteSpline>(OnDeleteSpline);
        EventHub.Unsubscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Unsubscribe<HideShow3DUI>(OnHideShow3DUI);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // var splineList = particleManager.splineParticleGroup.Select(g => g.spline).ToList();
        // SaveSystem scripts call the create spline event, so don't need to create control points here.
        SPD.controlSphereGroup.Clear(); // Clear the control sphere group list at the start
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        GameObject sphereContainer = new("SphereContainer");
        sphereContainer.transform.SetParent(e.spline.transform, false);

        for (int i = 0; i < e.spline.points.Length; i++)
        {
            CreateNewSphere(e.spline.points[i], i, sphereContainer.transform);
        }
    }

    void OnSplineUpdated(SplineUpdated e) //currently unused i think...
    {
        if (e.spline == null) return; // not sure why it would be null but it was in splinepicker so here it is

        var spline = e.spline;
        for (int i = 0; i < e.updatedIndices.Count; i++)
        {
            Vector3 point = spline.points[e.updatedIndices[i]];
            GameObject lineObj = e.spline.transform.Find("SphereContainer").gameObject;
            CreateNewSphere(point, e.updatedIndices[i], lineObj.transform);
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
        if (e.spline == null) return;

        var splineRoot = e.spline.transform; //the old spline object that will be used with IsChildOf to check if the control point spheres are children of the spline being deleted

        for (int i = SPD.controlSphereGroup.Count - 1; i >= 0; i--) //apparently going backwards through the list is the only way to safely remove items from a list while iterating through it
        {
            var group = SPD.controlSphereGroup[i];

            if (group == null || group.sphereObject == null) //remove "bad" entries from the list
            {
                SPD.controlSphereGroup.RemoveAt(i);
                continue;
            }

            if (group.sphereObject.transform.IsChildOf(splineRoot)) //if the control point sphere is a child of the spline being deleted, remove it from the list
            {
                SPD.controlSphereGroup.RemoveAt(i);
            }
        }
    }

    void OnHideShow3DUI(HideShow3DUI e)
    {
        if (e.hide == true)
        {
            foreach (var sphereGroup in SPD.controlSphereGroup)
            {
                sphereGroup.sphereObject.GetComponent<Renderer>().enabled = false;
            }
        }
        else
        {
            foreach (var sphereGroup in SPD.controlSphereGroup)
            {
                sphereGroup.sphereObject.GetComponent<Renderer>().enabled = true;
            }
        }
    }

    /// <summary>
    /// Called when the user clicks somewhere empty to clear the selection and make
    /// the highlighted sphere return to normal color
    /// </summary>
    void UnselectAllPoints()
    {
        SPD.lastHighlighted = null;

        for (int i = SPD.controlSphereGroup.Count - 1; i >= 0; i--)
        {
            var sphereGroup = SPD.controlSphereGroup[i];

            if (sphereGroup == null || sphereGroup.sphereObject == null)
            {
                SPD.controlSphereGroup.RemoveAt(i);
                continue;
            }

            sphereGroup.isSelected = false;

            if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = SPD.unselectedOriginalColor;
                rend.material.SetColor("_EmissionColor", SPD.unselectedOriginalColor * SPD.gizmoEmissionIntensity);
            }
        }
    }

    void CreateNewSphere(Vector3 point, int i, Transform parent)
    {
        GameObject sphere = Instantiate(controlPointSpherePrefab, point, Quaternion.identity);
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = new Vector3(SPD.controlPointScale, SPD.controlPointScale, SPD.controlPointScale);

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
    
    /// <summary>
    /// Updates all instantiated control point spheres to match the current SPD.controlPointScale.
    /// Can be called from UI scripts when the user finishes dragging the scale slider.
    /// </summary>
    public void RefreshControlPointScales()
    {
        Vector3 newScale = new Vector3(SPD.controlPointScale, SPD.controlPointScale, SPD.controlPointScale);

        for (int i = SPD.controlSphereGroup.Count - 1; i >= 0; i--)
        {
            var group = SPD.controlSphereGroup[i];

            if (group == null || group.sphereObject == null)
            {
                SPD.controlSphereGroup.RemoveAt(i);
                continue;
            }

            group.sphereObject.transform.localScale = newScale;
        }
    }
}
