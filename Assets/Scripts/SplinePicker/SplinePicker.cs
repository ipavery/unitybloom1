using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// splitting the script into:
// SelectionController (input + raycasts)
// GizmoController (show/move/destroy gizmos)
// SplineRenderer (line renderer updates)
// ControlPointController (highlighting, grouping)
//   └── they all tap into SharedData (ScriptableObject)

// idea - make each script as independent as possible, each getting its own input if needed,  
// so that scripts call functions in other scripts as little as possible

public class SplinePicker : MonoBehaviour
{
    [Header("Spline Settings")]
    public int pointsPerSpline = 32;
    public Material lineMaterial;
    public Material highlightedLineMaterial;
    public float lineWidth = 0.01f;
    public GameObject controlPointSpherePrefab;
    public ParticleManager particleManager;
    private List<BezierSpline> splineList;

    
    private List<ControlPointGroup> controlSphereGroup = new();

    // Dragging gizmos
    private Color highlightColor = Color.yellow;

    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;


    private List<Color> originalColors = new List<Color>();
    private GameObject lastHighlighted = null;
    public Color unselectedOriginalColor;

    //input variables
    public PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;

    // Selection box variables
    private Vector2 selectionStart;
    private Vector2 selectionEnd;
    private bool isSelecting = false;
    public GameObject selectionBoxUI; // Reference to the SelectionBoxUI component
    private bool raycastHit = false;
    private BezierSpline selectedSpline = null;
    private bool isInputBlocked = false;


    void Awake()
    {
        playerControls = new PlayerInputActions();

        mousePosition = playerControls.Player.MousePosition;
        select = playerControls.Player.Select;
        select.started += OnSelectStart;
        select.canceled += OnSelectEnd;
    }

    void OnEnable()
    {
        playerControls.Enable();

        mousePosition.Enable();
        select.Enable();
        EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Subscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Subscribe<SelectSpline>(OnSelectSpline);
        EventHub.Subscribe<SplineSelectionChange>(OnSplineSelectionChanged);
        EventHub.Subscribe<DeleteSpline>(OnDeleteSpline);
        EventHub.Subscribe<HideShow3DUI>(OnHideShow3DUI);
    }

    void OnDisable()
    {
        mousePosition.Disable();
        select.Disable();
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<SplineUpdated>(OnSplineUpdated);
        EventHub.Unsubscribe<SelectSpline>(OnSelectSpline);
        EventHub.Unsubscribe<SplineSelectionChange>(OnSplineSelectionChanged);
        EventHub.Unsubscribe<DeleteSpline>(OnDeleteSpline);
        EventHub.Unsubscribe<HideShow3DUI>(OnHideShow3DUI);



    }





    void OnHideShow3DUI(HideShow3DUI e)
    {


        if (e.hide == true)


        {


            foreach (var sphereGroup in controlSphereGroup)


            {


                sphereGroup.sphereObject.GetComponent<Renderer>().enabled = false;


            }


            foreach (var spline in splineList)


            {


                spline.GetComponentInChildren<LineRenderer>().enabled = false;


            }


        }


        else


        {


            foreach (var sphereGroup in controlSphereGroup)


            {


                sphereGroup.sphereObject.GetComponent<Renderer>().enabled = true;


            }


            foreach (var spline in splineList)


            {


                spline.GetComponentInChildren<LineRenderer>().enabled = true;


            }


        }
    }

    void OnDeleteSpline(DeleteSpline e)
    {
        for (int i = 0; i < e.spline.transform.GetChild(0).childCount; i++)
        {
            var child = e.spline.transform.GetChild(0).GetChild(i).gameObject;
            controlSphereGroup.RemoveAll(sphereGroup => sphereGroup.sphereObject == child);
        }
        splineList.Remove(e.spline);
    }

    void OnSplineSelectionChanged(SplineSelectionChange e)
    {
        if (e.isSelected == true)
        {
            selectedSpline = e.spline;
            e.spline.GetComponentInChildren<LineRenderer>().material = highlightedLineMaterial;
        }
        else if (e.isSelected == false)
        {
            e.spline.GetComponentInChildren<LineRenderer>().material = lineMaterial;
            selectedSpline = null;
        }

    }


    void OnSelectSpline(SelectSpline e)
    {
        //unselect all control points and then select just the control points that are on this spline
        UnHighlightLast();
        foreach (var sphereGroup in controlSphereGroup)
        {
            sphereGroup.isSelected = false; // Unselect current control point
            if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = unselectedOriginalColor; // Reset to original color
                rend.material.SetColor("_EmissionColor", unselectedOriginalColor * .1f);
            }
        }

        selectedSpline = e.spline;
        EventHub.Publish(new SplineSelectionChange(selectedSpline, true));
        var lineObj = selectedSpline.transform.Find("SplineLine");

        foreach (Transform sphereTransform in lineObj)
        {
            var sphere = sphereTransform.gameObject;
            if (lastHighlighted == null)
            {
                lastHighlighted = sphere;
            }

            var sphereGroupRef = controlSphereGroup.Find(item => item.sphereObject == sphere);
            sphereGroupRef.isSelected = true;
            var rendNew = sphereGroupRef.sphereObject.GetComponent<Renderer>();
            rendNew.material.color = highlightColor;
            rendNew.material.SetColor("_EmissionColor", highlightColor * .1f);

        }
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        splineList.Add(e.spline);
        InitializeSpline(e.spline);
    }

    void OnSplineUpdated(SplineUpdated e)
    {
        if (particleManager.splineParticleGroup.Count > 0)
        {

            var spline = e.spline;
            for (int i = 0; i < e.updatedIndices.Count; i++)
            {
                Vector3 point = spline.points[e.updatedIndices[i]];
                GameObject sphere = Instantiate(controlPointSpherePrefab, point, Quaternion.identity);
                GameObject lineObj = e.spline.transform.Find("SplineLine").gameObject;
                sphere.transform.SetParent(lineObj.transform, false);

                // Set the sphere to the "PP Layer"
                sphere.layer = LayerMask.NameToLayer("PP Layer");
                controlSphereGroup.Add(new ControlPointGroup
                {
                    sphereObject = sphere,
                    isSelected = false,
                    index = e.updatedIndices[i]
                });

                // Set and store original color as white
                var rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = Color.white;
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", Color.white * .1f);
                    originalColors.Add(Color.white);
                }
                else
                {
                    originalColors.Add(Color.white);
                }
            }

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



    private void UpdateSelectedSpline(GameObject sphereObject)
    {
        if (selectedSpline == null)
        {
            selectedSpline = sphereObject.GetComponentInParent<BezierSpline>();
            EventHub.Publish(new SplineSelectionChange(selectedSpline, true));
        }
    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        if (isInputBlocked) return;
        selectionStart = mousePosition.ReadValue<Vector2>();
        isSelecting = true;

        Ray ray = Camera.main.ScreenPointToRay(selectionStart);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 300f))
        {
            raycastHit = true;
            int idx = controlSphereGroup.FindIndex(group => group.sphereObject == hit.collider.gameObject);
            if (idx != -1)
            {
                // Un-highlight previous
                //UnHighlightLast();

                var sphere = controlSphereGroup[idx];
                UpdateSelectedSpline(sphere.sphereObject);

                // Highlight new
                if (sphere.sphereObject.TryGetComponent<Renderer>(out var rendNew))
                {
                    lastHighlighted = hit.collider.gameObject;
                    sphere.isSelected = true; // Mark the control point as selected
                    rendNew.material.color = highlightColor;
                    rendNew.material.SetColor("_EmissionColor", highlightColor * .1f);
                }

                //Debug.Log("Clicked control point: " + controlIndices[idx]);
            }

            
        }
        else
        {
            raycastHit = false;

        }
        if (isSelecting)
        {
            selectionBoxUI.GetComponent<SelectionBoxUI>().BeginSelection(selectionStart);

        }
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (isInputBlocked) return;
        selectionEnd = mousePosition.ReadValue<Vector2>();

        isSelecting = false;
        selectionBoxUI.GetComponent<SelectionBoxUI>().EndSelection();
        //SelectControlPointsInRect();

        // If no control point or gizmo was clicked and the mouse was not dragged too much, unselect all control points
        float selectDist = (selectionEnd - selectionStart).magnitude;
        if (raycastHit == false && selectDist < 5)
        {
            UnHighlightLast();
            foreach (var sphereGroup in controlSphereGroup)
            {
                sphereGroup.isSelected = false; // Unselect current control point
                if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
                {
                    rend.material.color = unselectedOriginalColor; // Reset to original color
                    rend.material.SetColor("_EmissionColor", unselectedOriginalColor * .1f);
                }
            }

            if (selectedSpline != null)
            {
                EventHub.Publish(new SplineSelectionChange(selectedSpline, false));
            }
        }
    }

    void SelectControlPointsInRect()
    {
        Vector2 min = Vector2.Min(selectionStart, mousePosition.ReadValue<Vector2>());
        Vector2 max = Vector2.Max(selectionStart, mousePosition.ReadValue<Vector2>());

        foreach (var sphereGroup in controlSphereGroup)
        {
            if (sphereGroup.isSelected == true)
            {
                continue; // Skip spheres that are selected
            }
            GameObject sphere = sphereGroup.sphereObject;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(sphere.transform.position);
            if (screenPos.z > 0 && screenPos.x >= min.x && screenPos.x <= max.x && screenPos.y >= min.y && screenPos.y <= max.y)
            {
                UpdateSelectedSpline(sphere);
                if (lastHighlighted == null)
                {
                    lastHighlighted = sphere; // Set the first highlighted sphere
                    EventHub.Publish(new ShowMoveGizmosEvent(lastHighlighted.transform.position));
                }
                sphereGroup.isSelected = true; // Mark the control point as selected
                var rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = highlightColor;
                    rend.material.SetColor("_EmissionColor", highlightColor * .1f);
                }
                else
                {
                    Debug.LogWarning("Renderer not found on control sphere: " + sphere.name);
                }
                //Debug.Log("Selected control point: " + sphere.name);
            }
        }
    }

    void InitializeSpline(BezierSpline spline)
    {
        GameObject lineObj = new GameObject("SplineLine");
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

        for (int i = 0; i < spline.points.Length; i++)
        {
            Vector3 point = spline.points[i];
            GameObject sphere = Instantiate(controlPointSpherePrefab, point, Quaternion.identity);
            sphere.transform.SetParent(lineObj.transform, false);

            // Set the sphere to the "PP Layer"
            sphere.layer = LayerMask.NameToLayer("PP Layer");
            controlSphereGroup.Add(new ControlPointGroup
            {
                sphereObject = sphere,
                isSelected = false,
                index = i
            });

            // Set and store original color as white
            var rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = Color.white;
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", Color.white * .1f);
                originalColors.Add(Color.white);
            }
            else
            {
                originalColors.Add(Color.white);
            }
        }
    }

    void Start()
    {
        splineList = particleManager.splineParticleGroup.Select(g => g.spline).ToList();

        foreach (var spline in splineList)
        {
            InitializeSpline(spline);
        }
    }

    void Update()
    {

        isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");
        SPD.isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");
        //Debug.Log($"spheres ({controlSphereGroup.Count}): {string.Join(", ", controlSphereGroup.Select(s => s.isSelected))}");

        if (lastHighlighted != null)
        {
            //float distance = Vector3.Distance(Camera.main.transform.position, lastHighlighted.transform.position);
            //lastHighlighted.transform.localScale = .6f * distance * gizmoScale * Vector3.one;
        }
        else if (isSelecting)
        {
            // Update the selection box UI only if not dragging
            Vector2 currentMousePos = mousePosition.ReadValue<Vector2>();
            selectionBoxUI.GetComponent<SelectionBoxUI>().UpdateSelection(currentMousePos);
            SelectControlPointsInRect();
        }


    }

    void FindGizmoAxisHitPoint(out Vector3 intersection, Ray ray, Plane dragPlane, int gizmoAxisDir, Vector3 controlPointPosition)
    {
        intersection = Vector3.zero; // Initialize intersection
        if (dragPlane.Raycast(ray, out float enter))
        {
            intersection = ray.GetPoint(enter);

        }

        if (gizmoAxisDir == 0)
        {
            intersection.y = controlPointPosition.y;
            intersection.z = controlPointPosition.z;
        }
        else if (gizmoAxisDir == 1)
        {
            intersection.x = controlPointPosition.x;
            intersection.z = controlPointPosition.z;
        }
        else if (gizmoAxisDir == 2)
        {
            intersection.x = controlPointPosition.x;
            intersection.y = controlPointPosition.y;
        }
        else if (gizmoAxisDir == 3) // Planar mover XZ plane
        {
            intersection.y = controlPointPosition.y;
            //Debug.Log("red good");
        }
        else if (gizmoAxisDir == 4) // Planar mover YZ plane
        {
            intersection.x = controlPointPosition.x;
            //Debug.Log("green good");
        }
        else if (gizmoAxisDir == 5) // Planar mover XY plane
        {
            intersection.z = controlPointPosition.z;
            //Debug.Log("blue good");
        }

    }

    void UnHighlightLast()
    {
        if (lastHighlighted != null)
        {
            var rend = lastHighlighted.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = unselectedOriginalColor;
                rend.material.SetColor("_EmissionColor", unselectedOriginalColor * .1f);
            }
            //lastHighlighted.transform.localScale = Vector3.one; // Reset scale
            lastHighlighted = null; // Clear last highlighted
        }
    }
}