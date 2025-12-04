using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class GizmoController : MonoBehaviour
{
    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;

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

    [Header("Player Input Variables")]
    public PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;

    // Selection variables
    private Vector2 selectionStart;
    private Vector2 selectionEnd;
    private bool raycastHit = false;

    // Dragging gizmos variables
    private int activeGizmoAxis = -1; // 0=X, 1=Y, 2=Z
    private Vector3 dragStartPoint;
    Plane dragPlane;
    private float distance;

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

        // Subscribe to pertinent events in OnEnable and unsubscribe in OnDisable to prevent errors
        EventHub.Subscribe<ShowMoveGizmosEvent>(OnShowMoveGizmos);
        EventHub.Subscribe<SelectionStartEnd>(OnSelectionStartEnd);
    }

    void OnDisable()
    {
        playerControls.Disable();
        mousePosition.Disable();
        select.Disable();
        EventHub.Unsubscribe<ShowMoveGizmosEvent>(OnShowMoveGizmos);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gizmoPrefabScale = moveGizmoPrefab.transform.localScale;
        planarGizmoPrefabScale = planarGizmoPrefab.transform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var gizmo in gizmoList)
        {

            if (gizmo != null && SPD.lastHighlighted != null)
            {
                // Update the gizmo position and scale based on the camera distance
                // This assumes the gizmo is a child of the SplinePicker object
                // and that it has been instantiated with the correct position
                if (gizmo.name.Contains("MoveGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * gizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + gizmo.transform.up * gizmoOffsetDistance * distance; // Offset the gizmo position
                }
                else if (gizmo.name.Contains("PlanarGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * planarGizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + (gizmo.transform.up + gizmo.transform.right * .5f) * gizmoOffsetDistance * distance; // Offset the gizmo position
                }
            }
        }
        if (activeGizmoAxis != -1)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (activeGizmoAxis == 0) //Axis movers
            {
                dragPlane = new Plane(Vector3.up, SPD.lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 1)
            {
                dragPlane = new Plane(Vector3.right, SPD.lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 2)
            {
                dragPlane = new Plane(Vector3.forward, SPD.lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 3) //Planar movers
            {
                dragPlane = new Plane(Vector3.up, SPD.lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 4)
            {
                dragPlane = new Plane(Vector3.right, SPD.lastHighlighted.transform.position);
            }
            else if (activeGizmoAxis == 5)
            {
                dragPlane = new Plane(Vector3.forward, SPD.lastHighlighted.transform.position);
            }
            // Vector3 newControlPos;
            // FindGizmoAxisHitPoint(out newControlPos, ray, dragPlane, activeGizmoAxis, lastHighlighted.transform.position);
            // Vector3 offset = newControlPos - lastHighlighted.transform.position;

            // 
            FindGizmoAxisHitPoint(out Vector3 newControlPos, ray, dragPlane, activeGizmoAxis, SPD.lastHighlighted.transform.position);
        }
    }

    void OnShowMoveGizmos(ShowMoveGizmosEvent e)
    {
        ShowMoveGizmos(e.position);
    }

    void OnSelectionStartEnd(SelectionStartEnd e)
    {
        Debug.Log($"selection started at {e.position}");
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

    void DestroyGizmos()
    {
        foreach (var gizmo in gizmoList)
        {
            Destroy(gizmo);
        }
        gizmoList.Clear();
    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        //maybe make the code below in splinepickerdata bc it's repetitive
        if (SPD.isInputBlocked) return;
        selectionStart = mousePosition.ReadValue<Vector2>();

        Ray ray = Camera.main.ScreenPointToRay(selectionStart);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 300f))
        {
            raycastHit = true;
            // Check if the clicked point is one of the gizmos
            for (int i = 0; i < gizmoList.Count; i++)
            {
                if (hit.collider != null && hit.collider.gameObject == gizmoList[i])
                {
                    activeGizmoAxis = i; // Set the active gizmo axis based on the clicked gizmo
                    dragStartPoint = hit.point;
                }
            }
        }
        else
        {
            raycastHit = false;
        }
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (SPD.isInputBlocked) return;
        selectionEnd = mousePosition.ReadValue<Vector2>();

        activeGizmoAxis = -1; // Reset the active gizmo axis
        if (SPD.lastHighlighted != null)
        {
            //this might not be necessary in independent mode
            EventHub.Publish(new GizmoDragEnded(SPD.lastHighlighted, SPD.lastHighlighted.transform.position));
        }

        // If no control point or gizmo was clicked and the mouse was not dragged too much, destroy gizmos
        float selectDist = (selectionEnd - selectionStart).magnitude;
        if (raycastHit == false && selectDist < 5)
        {
            DestroyGizmos(); // Clear existing gizmos
            activeGizmoAxis = -1; // Reset the active gizmo axis
        }
    }
}
