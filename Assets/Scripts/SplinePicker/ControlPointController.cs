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
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<ControlPointSelected>(OnControlPointSelected);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

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
        // var sphereGroup = e.sphereGroup
        // sphereGroup.isSelected = true; // Mark the control point as selected
        // rendNew.material.color = highlightColor;
        // rendNew.material.SetColor("_EmissionColor", highlightColor * gizmoEmissionIntensity);
    }

    /// <summary>
    /// Called when the user clicks somewhere empty to clear the selection and make
    /// the highlighted sphere return to normal color
    /// </summary>
    // void UnHighlightLast()
    // {
    //     if (SPD.lastHighlighted != null)
    //     {
    //         var rend = SPD.lastHighlighted.GetComponent<Renderer>();
    //         if (rend != null)
    //         {
    //             rend.material.color = unselectedOriginalColor;
    //             rend.material.SetColor("_EmissionColor", unselectedOriginalColor * gizmoEmissionIntensity);
    //         }
    //         //lastHighlighted.transform.localScale = Vector3.one; // Reset scale
    //         SPD.lastHighlighted = null; // Clear last highlighted
    //     }
    // }
}
