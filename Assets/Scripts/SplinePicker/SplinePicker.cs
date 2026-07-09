using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

//splitting the script into:
// SelectionController (input + raycasts)
// GizmoController (show/move/destroy gizmos)
// SplineRenderer (line renderer updates)
// ControlPointController (highlighting, grouping)
//   └── they all tap into SharedData (ScriptableObject)

public class SplinePicker : MonoBehaviour
{
    [Header("Spline Settings")]
    public int pointsPerSpline = 32;
    public Material lineMaterial;
    public Material highlightedLineMaterial;
    public float lineWidth = 0.01f;
    public GameObject controlPointSpherePrefab;
    public ParticleManager particleManager;
    private List<BezierSpline> splineList = new();

    [Header("Gizmo Settings")]
    public GameObject moveGizmoPrefab; // Assign a gizmo prefab in the inspector
    public GameObject planarGizmoPrefab; // Assign a planar gizmo prefab in the inspector
    Vector3 gizmoPrefabScale; // Store the original scale of the gizmo prefab
    Vector3 planarGizmoPrefabScale; // Store the original scale of the planar gizmo prefab
    private float gizmoOffsetDistance = .15f; // Offset distance for gizmos
    public float gizmoScale = .1f; // Scale for gizmos
    public Color highlightColor = Color.yellow;
    public float gizmoEmissionIntensity = 0.6f; // Emission intensity for gizmos
    private List<GameObject> gizmoList = new();
    private List<ControlPointGroup> controlSphereGroup = new();

    // Dragging gizmos
    private int activeGizmoAxis = -1; // 0=X, 1=Y, 2=Z
    private Vector3 dragStartPoint;
    Plane dragPlane;
    float distance;


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
        // for (int i = 0; i < e.spline.transform.GetChild(0).childCount; i++)
        // {
        //     var child = e.spline.transform.GetChild(0).GetChild(i).gameObject;
        //     controlSphereGroup.RemoveAll(sphereGroup => sphereGroup.sphereObject == child);
        // }
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
        DestroyGizmos(); // Clear existing gizmos
        foreach (var sphereGroup in controlSphereGroup)
        {
            sphereGroup.isSelected = false; // Unselect current control point
            if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.color = unselectedOriginalColor; // Reset to original color
                rend.material.SetColor("_EmissionColor", unselectedOriginalColor * gizmoEmissionIntensity);
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
                ShowMoveGizmos(lastHighlighted.transform.position); // Show move gizmos at the highlighted control point
            }

            var sphereGroupRef = controlSphereGroup.Find(item => item.sphereObject == sphere);
            sphereGroupRef.isSelected = true;
            var rendNew = sphereGroupRef.sphereObject.GetComponent<Renderer>();
            rendNew.material.color = highlightColor;
            rendNew.material.SetColor("_EmissionColor", highlightColor * gizmoEmissionIntensity);

        }
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        splineList.Add(e.spline);
        InitializeSpline(e.spline);
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
        if (Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            raycastHit = true;
            int idx = controlSphereGroup.FindIndex(group => group.sphereObject == hit.collider.gameObject);
            if (idx != -1)
            {
                // Un-highlight previous
                //UnHighlightLast();
                Debug.Log($"hitspheresplinep: {idx}");
                var sphere = controlSphereGroup[idx];
                UpdateSelectedSpline(sphere.sphereObject);

                // Highlight new
                if (sphere.sphereObject.TryGetComponent<Renderer>(out var rendNew))
                {
                    lastHighlighted = hit.collider.gameObject;
                    sphere.isSelected = true; // Mark the control point as selected
                    rendNew.material.color = highlightColor;
                    rendNew.material.SetColor("_EmissionColor", highlightColor * gizmoEmissionIntensity);
                    ShowMoveGizmos(lastHighlighted.transform.position); // Show move gizmos at the highlighted control point
                }

                //Debug.Log("Clicked control point: " + controlIndices[idx]);
            }

            // Check if the clicked point is one of the gizmos
            for (int i = 0; i < gizmoList.Count; i++)
            {
                if (hit.collider != null && hit.collider.gameObject == gizmoList[i])
                {
                    activeGizmoAxis = i; // Set the active gizmo axis based on the clicked gizmo
                    dragStartPoint = hit.point;
                    isSelecting = false;
                }
            }
        }
        else
        {
            raycastHit = false;

        }
        if (isSelecting)
        {
            //selectionBoxUI.GetComponent<SelectionBoxUI>().BeginSelection(selectionStart);

        }
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (isInputBlocked) return;
        selectionEnd = mousePosition.ReadValue<Vector2>();

        isSelecting = false;
        //selectionBoxUI.GetComponent<SelectionBoxUI>().EndSelection();
        //SelectControlPointsInRect();
        activeGizmoAxis = -1; // Reset the active gizmo axis
        if (lastHighlighted != null)
        {
            EventHub.Publish(new GizmoDragEnded(lastHighlighted, lastHighlighted.transform.position));
        }

        // If no control point or gizmo was clicked and the mouse was not dragged too much, unselect all control points
        float selectDist = (selectionEnd - selectionStart).magnitude;
        if (raycastHit == false && selectDist < 5)
        {
            UnHighlightLast();
            DestroyGizmos(); // Clear existing gizmos
            activeGizmoAxis = -1; // Reset the active gizmo axis
            foreach (var sphereGroup in controlSphereGroup)
            {
                sphereGroup.isSelected = false; // Unselect current control point
                if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
                {
                    rend.material.color = unselectedOriginalColor; // Reset to original color
                    rend.material.SetColor("_EmissionColor", unselectedOriginalColor * gizmoEmissionIntensity);
                }
            }

            if (selectedSpline != null)
            {
                EventHub.Publish(new SplineSelectionChange(selectedSpline, false));
            }
        }
    }

    void InitializeSpline(BezierSpline spline)
    {
        
    }

    void Start()
    {
        // splineList = particleManager.splineParticleGroup.Select(g => g.spline).ToList();
        // gizmoPrefabScale = moveGizmoPrefab.transform.localScale;
        // planarGizmoPrefabScale = planarGizmoPrefab.transform.localScale;

        // foreach (var spline in splineList)
        // {
        //     InitializeSpline(spline);
        // }
    }

    void Update()
    {

        isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");
        //Debug.Log($"spheres ({controlSphereGroup.Count}): {string.Join(", ", controlSphereGroup.Select(s => s.isSelected))}");

        if (lastHighlighted != null)
        {
            distance = Vector3.Distance(Camera.main.transform.position, lastHighlighted.transform.position);
        }
        foreach (var gizmo in gizmoList)
        {

            if (gizmo != null && lastHighlighted != null)
            {
                // Update the gizmo position and scale based on the camera distance
                // This assumes the gizmo is a child of the SplinePicker object
                // and that it has been instantiated with the correct position
                if (gizmo.name.Contains("MoveGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * gizmoPrefabScale;
                    gizmo.transform.position = lastHighlighted.transform.position + gizmo.transform.up * gizmoOffsetDistance * distance; // Offset the gizmo position
                }
                else if (gizmo.name.Contains("PlanarGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * planarGizmoPrefabScale;
                    gizmo.transform.position = lastHighlighted.transform.position + (gizmo.transform.up + gizmo.transform.right * .5f) * gizmoOffsetDistance * distance; // Offset the gizmo position
                }
            }
        }

        if (activeGizmoAxis != -1)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (activeGizmoAxis == 0) //Axis movers
            {
                dragPlane = new Plane(Vector3.up, lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 1)
            {
                dragPlane = new Plane(Vector3.right, lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 2)
            {
                dragPlane = new Plane(Vector3.forward, lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 3) //Planar movers
            {
                dragPlane = new Plane(Vector3.up, lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 4)
            {
                dragPlane = new Plane(Vector3.right, lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 5)
            {
                dragPlane = new Plane(Vector3.forward, lastHighlighted.transform.position);
            }
            // Vector3 newControlPos;
            // FindGizmoAxisHitPoint(out newControlPos, ray, dragPlane, activeGizmoAxis, lastHighlighted.transform.position);
            // Vector3 offset = newControlPos - lastHighlighted.transform.position;

            FindGizmoAxisHitPoint(out Vector3 newControlPos, ray, dragPlane, activeGizmoAxis, lastHighlighted.transform.position);
            

            foreach (var spline in splineList)
            {
                // --- Update the LineRenderer for this spline ---
                // Find the LineRenderer (assumes it's on a child of the spline GameObject)
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
        else if (isSelecting)
        {
            // Update the selection box UI only if not dragging
            Vector2 currentMousePos = mousePosition.ReadValue<Vector2>();
            //selectionBoxUI.GetComponent<SelectionBoxUI>().UpdateSelection(currentMousePos);
        }


    }

    void ShowMoveGizmos(Vector3 position)
    {
        DestroyGizmos(); // Clear existing gizmos

        // Create a new move gizmo at the specified position
        for (int i = 0; i < 3; i++)
        {
            GameObject moveGizmo = Instantiate(moveGizmoPrefab, position, Quaternion.identity);
            gizmoList.Add(moveGizmo);
            moveGizmo.transform.localScale *= gizmoScale; // Adjust scale as needed
            moveGizmo.transform.SetParent(transform, false); // Set parent to the SplinePicker object
            // Set the sphere to the "PP Layer"
            moveGizmo.layer = LayerMask.NameToLayer("PP Layer");
            Renderer rend = moveGizmo.GetComponent<Renderer>();

            // Fourth gizmo: 2D plane mover (e.g., XZ plane)
            GameObject planarGizmo = Instantiate(planarGizmoPrefab, position, Quaternion.identity);
            planarGizmo.transform.SetParent(transform, false);
            planarGizmo.layer = LayerMask.NameToLayer("PP Layer");

            if (planarGizmo.TryGetComponent<Renderer>(out var planarRend))
            {
                planarRend.material.EnableKeyword("_EMISSION");
            }

            gizmoList.Add(planarGizmo);

            if (rend != null)
            {
                rend.material.EnableKeyword("_EMISSION");
            }

            // Set rotation and position based on index
            if (i == 0)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90); // Forward
                rend.material.color = Color.red;
                rend.material.SetColor("_EmissionColor", Color.red * gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                planarRend.material.color = Color.red;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * gizmoEmissionIntensity);
            }
            else if (i == 1)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0); // Right
                rend.material.color = Color.green;
                rend.material.SetColor("_EmissionColor", Color.green * gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                planarRend.material.color = Color.green;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * gizmoEmissionIntensity);
            }
            else if (i == 2)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0); // Up
                rend.material.color = Color.blue;
                rend.material.SetColor("_EmissionColor", Color.blue * gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                planarRend.material.color = Color.blue;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * gizmoEmissionIntensity);
            }

            moveGizmo.transform.position = position + moveGizmo.transform.up * 3f; // Add control point position and offset
            planarGizmo.transform.position = position + planarGizmo.transform.up * 2f + planarGizmo.transform.right * 2f; // Slight offset to avoid z-fighting

        }
        // re-order gizmo list
        gizmoList = new List<GameObject>
        {
            gizmoList[0], // X-axis
            gizmoList[2], // Y-axis
            gizmoList[4], // Z-axis
            gizmoList[5],
            gizmoList[3],
            gizmoList[1]  // Planar mover
        };

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
                rend.material.SetColor("_EmissionColor", unselectedOriginalColor * gizmoEmissionIntensity);
            }
            //lastHighlighted.transform.localScale = Vector3.one; // Reset scale
            lastHighlighted = null; // Clear last highlighted
        }
    }

    void DestroyGizmos()
    {
        foreach (var gizmo in gizmoList)
        {
            Destroy(gizmo);
        }
        gizmoList.Clear();
    }
}