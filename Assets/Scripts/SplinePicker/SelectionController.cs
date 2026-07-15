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

    public BezierSpline selectedSpline { get; private set; } = null; // Reference to the spline that is currently selected

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
        EventHub.Subscribe<SelectSpline>(OnSelectSpline);
    }

    void OnDisable()
    {
        playerControls.Disable();
        mousePosition.Disable();
        select.Disable();
        EventHub.Unsubscribe<SplineSelectionChange>(OnSplineSelectionChanged);
        EventHub.Unsubscribe<SelectSpline>(OnSelectSpline);
    }

    #region Event Handlers
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

    void OnSelectSpline(SelectSpline e)
    {
        // Use the spline passed through the event to easily support multi-selection later
        BezierSpline targetSpline = e.spline;
        if (targetSpline == null) return;

        EventHub.Publish(new SplineSelectionChange(targetSpline, true)); //maybe change to allow multiselect spline ability

        // Search for sphereContainer which has this spline's control points as children
        Transform sphereContainer = targetSpline.transform.Find("SphereContainer");

        if (sphereContainer == null)
        {
            Debug.LogWarning("SphereContainer not found on this spline!");
            return;
        }

        // Iterate through all the spheres attached to this spline
        foreach (Transform sphereTransform in sphereContainer)
        {
            GameObject sphere = sphereTransform.gameObject;
            
            // Find the matching data group in SPD
            var sphereGroupRef = SPD.controlSphereGroup.Find(item => item.sphereObject == sphere);
            
            if (sphereGroupRef != null)
            {
                // Efficiency check: Only process points that aren't already selected
                if (!sphereGroupRef.isSelected)
                {
                    // If nothing is highlighted yet, make this the anchor for the Gizmos
                    if (SPD.lastHighlighted == null)
                    {
                        SPD.lastHighlighted = sphere;
                        EventHub.Publish(new ShowMoveGizmosEvent(sphere.transform.position));
                    }

                    // Fire the selection event (which will now correctly color it without overwriting lastHighlighted)
                    EventHub.Publish(new ControlPointSelected(sphereGroupRef));
                }
            }
        }
    }

    #endregion

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
        Vector3 camForward = Camera.main.transform.forward;
        
        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)gizmoAxisDir;
        Plane plane;

        // Use dynamic camera-facing planes for 1D moves, fixed planes for the rest
        switch (axis)
        {
            case SplinePickerData.GizmoType.MoveX:
                Vector3 crossX = Vector3.Cross(Vector3.right, camForward);
                plane = new Plane(Vector3.Cross(crossX, Vector3.right).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.MoveY:
                Vector3 crossY = Vector3.Cross(Vector3.up, camForward);
                plane = new Plane(Vector3.Cross(crossY, Vector3.up).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.MoveZ:
                Vector3 crossZ = Vector3.Cross(Vector3.forward, camForward);
                plane = new Plane(Vector3.Cross(crossZ, Vector3.forward).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.PlanarX:
            case SplinePickerData.GizmoType.RotateX:
                plane = new Plane(Vector3.forward, pivot);
                break;
            case SplinePickerData.GizmoType.PlanarY:
            case SplinePickerData.GizmoType.RotateY:
                plane = new Plane(Vector3.right, pivot);
                break;
            case SplinePickerData.GizmoType.PlanarZ:
            case SplinePickerData.GizmoType.RotateZ:
                plane = new Plane(Vector3.up, pivot);
                break;
            default:
                plane = new Plane(Vector3.up, pivot);
                break;
        }

        Vector3 hitPoint = pivot; // Default to pivot

        if (plane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
        }

        // Clamp the initial hit point exactly like the GizmoController to prevent snapping
        if (axis == SplinePickerData.GizmoType.MoveX) { hitPoint.y = pivot.y; hitPoint.z = pivot.z; }
        else if (axis == SplinePickerData.GizmoType.MoveY) { hitPoint.x = pivot.x; hitPoint.z = pivot.z; }
        else if (axis == SplinePickerData.GizmoType.MoveZ) { hitPoint.x = pivot.x; hitPoint.y = pivot.y; }
        else if (axis == SplinePickerData.GizmoType.PlanarX) { hitPoint.z = pivot.z; }
        else if (axis == SplinePickerData.GizmoType.PlanarY) { hitPoint.x = pivot.x; }
        else if (axis == SplinePickerData.GizmoType.PlanarZ) { hitPoint.y = pivot.y; }

        return hitPoint;
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
                    EventHub.Publish(new SplineSelectionChange(sphere.GetComponentInParent<BezierSpline>(), true)); // Select the spline of the first highlighted control point
                }
                EventHub.Publish(new ControlPointSelected(sphereGroup));
                //Debug.Log("Selected control point: " + sphere.name);
            }
        }
    }
}
