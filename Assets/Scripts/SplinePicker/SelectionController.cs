using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles input selection and owns the selection box UI.
/// It raycasts first and decides whether the interaction is a control point selection,
/// a gizmo drag, or a drag-select box.
/// </summary>
[RequireComponent(typeof(GizmoController))]
public class SelectionController : MonoBehaviour
{
    [SerializeField] private SplinePickerData SPD;

    [Header("Player Input Variables")]
    public PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;

    public GameObject selectionBoxUI;
    private Vector2 selectionStart;
    private Vector2 selectionEnd;
    private bool raycastHit = false;
    private GizmoController gizmoController;

    private BezierSpline selectedSpline = null; // Reference to the spline that is currently selected

    void Awake()
    {
        playerControls = new PlayerInputActions();
        mousePosition = playerControls.Player.MousePosition;
        select = playerControls.Player.Select;
        select.started += OnSelectStart;
        select.canceled += OnSelectEnd;

        gizmoController = GetComponent<GizmoController>();
        if (gizmoController == null)
        {
            gizmoController = FindFirstObjectByType<GizmoController>();
        }
    }

    void OnEnable()
    {
        playerControls.Enable();
        mousePosition.Enable();
        select.Enable();
        EventHub.Subscribe<SplineSelectionChange>(OnSplineSelectionChanged);
    }

    void OnDisable()
    {
        playerControls.Disable();
        mousePosition.Disable();
        select.Disable();
        EventHub.Unsubscribe<SplineSelectionChange>(OnSplineSelectionChanged);
    }

    void OnSplineSelectionChanged(SplineSelectionChange e)
    {
        if (e.isSelected == true)
        {
            selectedSpline = e.spline;
        }
        else if (e.isSelected == false)
        {
            selectedSpline = null;
        }

    }

    void Update()
    {
        SPD.isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");

        if (SPD.isSelecting && SPD.activeGizmoAxis == -1)
        {
            Vector2 currentMousePos = mousePosition.ReadValue<Vector2>();
            selectionBoxUI.GetComponent<SelectionBoxUI>().UpdateSelection(currentMousePos);
            SelectControlPointsInRect();
        }
    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        if (SPD.isInputBlocked) return;

        selectionStart = mousePosition.ReadValue<Vector2>();
        raycastHit = false;
        SPD.isSelecting = false;
        //EventHub.Publish(new SelectionStartEnd(selectionStart, true)); //No listeners for this yet but might need it in future

        Ray ray = Camera.main.ScreenPointToRay(selectionStart);
        if (Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            raycastHit = true;

            int idx = SPD.controlSphereGroup.FindIndex(group => group.sphereObject == hit.collider.gameObject);
            if (idx != -1)
            {
                var sphereGroup = SPD.controlSphereGroup[idx];
                EventHub.Publish(new ControlPointSelected(sphereGroup));
                UpdateSelectedSpline(sphereGroup.sphereObject);
                if (SPD.lastHighlighted != null)
                {
                    EventHub.Publish(new ShowMoveGizmosEvent(SPD.lastHighlighted.transform.position));
                }
                return;
            }

            if (gizmoController != null && gizmoController.TryGetGizmoAxisIndex(hit.collider.gameObject, out int gizmoAxisIndex))
            {
                SPD.activeGizmoAxis = gizmoAxisIndex;
                Debug.Log("The user clicked on: " + ((SplinePickerData.GizmoType)gizmoAxisIndex).ToString());
                EventHub.Publish(new GizmoDragStarted(GetDragPlanePoint(selectionStart, gizmoAxisIndex))); // this needs to be changed somehow to have all selected splines in it if the splinerenderer should only update selected splines
                return;
            }
        }

        SPD.isSelecting = true;
        selectionBoxUI.GetComponent<SelectionBoxUI>().BeginSelection(selectionStart);
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (SPD.isInputBlocked) return;

        selectionEnd = mousePosition.ReadValue<Vector2>();
        // EventHub.Publish(new SelectionStartEnd(selectionEnd, false)); //no listeners for this yet but might need it in future

        if (SPD.isSelecting)
        {
            SPD.isSelecting = false;
            selectionBoxUI.GetComponent<SelectionBoxUI>().EndSelection();
        }

        if (SPD.lastHighlighted != null)
        {
            EventHub.Publish(new GizmoDragEnded(SPD.lastHighlighted, SPD.lastHighlighted.transform.position));
        }

        float selectDist = (selectionEnd - selectionStart).magnitude;
        if (!raycastHit && selectDist < 5f)
        {
            EventHub.Publish(new ClearSelection());
            if (selectedSpline != null)
            {
                EventHub.Publish(new SplineSelectionChange(selectedSpline, false));
            }
        }
    }

    private Vector3 GetDragPlanePoint(Vector2 screenPosition, int gizmoAxisDir)
    {
        if (SPD.lastHighlighted == null)
        {
            return Vector3.zero;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Vector3 pivot = SPD.lastHighlighted.transform.position;
        
        // Cast the integer to your Enum right away
        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)gizmoAxisDir;

        // Use the Enum in the switch expression with the corrected normals
        Plane plane = axis switch
        {
            // Move Axes
            SplinePickerData.GizmoType.MoveX => new Plane(Vector3.up, pivot),
            SplinePickerData.GizmoType.MoveY => new Plane(Vector3.right, pivot),
            SplinePickerData.GizmoType.MoveZ => new Plane(Vector3.up, pivot),

            // Red Planar/Rotate (_XY Visual)
            SplinePickerData.GizmoType.PlanarX or SplinePickerData.GizmoType.RotateX => new Plane(Vector3.forward, pivot),
            
            // Green Planar/Rotate (_YZ Visual)
            SplinePickerData.GizmoType.PlanarY or SplinePickerData.GizmoType.RotateY => new Plane(Vector3.right, pivot),
            
            // Blue Planar/Rotate (_ZX Visual)
            SplinePickerData.GizmoType.PlanarZ or SplinePickerData.GizmoType.RotateZ => new Plane(Vector3.up, pivot),

            _ => new Plane(Vector3.up, pivot)
        };

        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return pivot;
    }

    private void UpdateSelectedSpline(GameObject sphereObject)
    {
        if (selectedSpline == null)
        {
            selectedSpline = sphereObject.GetComponentInParent<BezierSpline>();
            EventHub.Publish(new SplineSelectionChange(selectedSpline, true));
        }
    }

    void SelectControlPointsInRect()
    {
        Vector2 min = Vector2.Min(selectionStart, mousePosition.ReadValue<Vector2>());
        Vector2 max = Vector2.Max(selectionStart, mousePosition.ReadValue<Vector2>());

        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup.isSelected == true)
            {
                continue; // Skip spheres that are selected
            }
            GameObject sphere = sphereGroup.sphereObject;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(sphere.transform.position);
            if (screenPos.z > 0 && screenPos.x >= min.x && screenPos.x <= max.x && screenPos.y >= min.y && screenPos.y <= max.y)
            {
                //UpdateSelectedSpline(sphere); //ADD this back to make inspector work
                if (SPD.lastHighlighted == null)
                {
                    SPD.lastHighlighted = sphere; // Set the first highlighted sphere
                    EventHub.Publish(new ShowMoveGizmosEvent(SPD.lastHighlighted.transform.position));
                }
                sphereGroup.isSelected = true; // Mark the control point as selected
                if (sphere.TryGetComponent<Renderer>(out var rend))
                {
                    // change color of spheres to show they are selected
                    rend.material.color = SPD.highlightColor;
                    rend.material.SetColor("_EmissionColor", SPD.highlightColor * SPD.gizmoEmissionIntensity);
                }
                else
                {
                    Debug.LogWarning("Renderer not found on control sphere: " + sphere.name);
                }
                //Debug.Log("Selected control point: " + sphere.name);
            }
        }
    }
}
