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
    }

    void OnDisable()
    {
        playerControls.Disable();
        mousePosition.Disable();
        select.Disable();
    }

    void Update()
    {
        SPD.isInputBlocked = InputBlocker.IsInputBlocked("Unblocked UI Layer");

        if (SPD.isSelecting && SPD.activeGizmoAxis == -1)
        {
            Vector2 currentMousePos = mousePosition.ReadValue<Vector2>();
            selectionBoxUI.GetComponent<SelectionBoxUI>().UpdateSelection(currentMousePos);
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
                if (SPD.lastHighlighted != null)
                {
                    EventHub.Publish(new ShowMoveGizmosEvent(SPD.lastHighlighted.transform.position));
                }
                return;
            }

            if (gizmoController != null && gizmoController.TryGetGizmoAxisIndex(hit.collider.gameObject, out int gizmoAxisIndex))
            {
                SPD.activeGizmoAxis = gizmoAxisIndex;
                EventHub.Publish(new GizmoDragStarted(GetDragPlanePoint(selectionStart, gizmoAxisIndex)));
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
        }
    }

    private Vector3 GetDragPlanePoint(Vector2 screenPosition, int gizmoAxisDir)
    {
        if (SPD.lastHighlighted == null)
        {
            return Vector3.zero;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        Plane plane = gizmoAxisDir switch
        {
            0 => new Plane(Vector3.up, SPD.lastHighlighted.transform.position),
            1 => new Plane(Vector3.right, SPD.lastHighlighted.transform.position),
            2 => new Plane(Vector3.forward, SPD.lastHighlighted.transform.position),
            3 => new Plane(Vector3.up, SPD.lastHighlighted.transform.position),
            4 => new Plane(Vector3.right, SPD.lastHighlighted.transform.position),
            5 => new Plane(Vector3.forward, SPD.lastHighlighted.transform.position),
            _ => new Plane(Vector3.up, SPD.lastHighlighted.transform.position)
        };

        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return SPD.lastHighlighted.transform.position;
    }
}
