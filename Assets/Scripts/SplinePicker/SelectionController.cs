using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// maybe just make events for selection start and end here so all scripts can use that data

public class SelectionController : MonoBehaviour
{
    //reference to splinepickerdata scriptableobject
    [SerializeField] private SplinePickerData SPD;


    [Header("Player Input Variables")]
    public PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;

    // Selection variables
    public GameObject selectionBoxUI; // Reference to the SelectionBoxUI component
    private Vector2 selectionStart;
    private Vector2 selectionEnd;
    private bool raycastHit = false;


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
    }

    void OnDisable()
    {
        playerControls.Disable();
        mousePosition.Disable();
        select.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        if (SPD.isInputBlocked) return;
        selectionStart = mousePosition.ReadValue<Vector2>();
        EventHub.Publish(new SelectionStartEnd(selectionStart, true));

        SPD.isSelecting = true;

        Ray ray = Camera.main.ScreenPointToRay(selectionStart);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 300f))
        {
            raycastHit = true;
            int idx = SPD.controlSphereGroup.FindIndex(group => group.sphereObject == hit.collider.gameObject);
            if (idx != -1)
            {
                // Un-highlight previous
                //UnHighlightLast();

                // var sphere = SPD.controlSphereGroup[idx];
                // UpdateSelectedSpline(sphere.sphereObject);

                // Highlight new
                // if (sphere.sphereObject.TryGetComponent<Renderer>(out var rendNew))
                // {
                //     SPD.lastHighlighted = hit.collider.gameObject;
                //     sphere.isSelected = true; // Mark the control point as selected
                //     rendNew.material.color = highlightColor;
                //     rendNew.material.SetColor("_EmissionColor", highlightColor * gizmoEmissionIntensity);
                //     ShowMoveGizmos(SPD.lastHighlighted.transform.position); // Show move gizmos at the highlighted control point
                // }

                //Debug.Log("Clicked control point: " + controlIndices[idx]);
            }


            // Check if the clicked point is one of the gizmos
            // for (int i = 0; i < gizmoList.Count; i++)
            // {
            //     if (hit.collider != null && hit.collider.gameObject == gizmoList[i])
            //     {
            //         activeGizmoAxis = i; // Set the active gizmo axis based on the clicked gizmo
            //         dragStartPoint = hit.point;
            //         SPD.isSelecting = false;
            //     }
            // }
        }
        else
        {
            raycastHit = false;

        }
        if (SPD.isSelecting)
        {
            selectionBoxUI.GetComponent<SelectionBoxUI>().BeginSelection(selectionStart);
        }
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (SPD.isInputBlocked) return;
        selectionEnd = mousePosition.ReadValue<Vector2>();
        EventHub.Publish(new SelectionStartEnd(selectionEnd, false));

        SPD.isSelecting = false;
        selectionBoxUI.GetComponent<SelectionBoxUI>().EndSelection();
        // //SelectControlPointsInRect();
        // activeGizmoAxis = -1; // Reset the active gizmo axis
        if (SPD.lastHighlighted != null)
        {
            EventHub.Publish(new GizmoDragEnded(SPD.lastHighlighted, SPD.lastHighlighted.transform.position));
        }

        // If no control point or gizmo was clicked and the mouse was not dragged too much, unselect all control points
        float selectDist = (selectionEnd - selectionStart).magnitude;
        if (raycastHit == false && selectDist < 5)
        {
            // UnHighlightLast();
            // DestroyGizmos(); // Clear existing gizmos
            // activeGizmoAxis = -1; // Reset the active gizmo axis
            // foreach (var sphereGroup in SPD.controlSphereGroup)
            // {
            //     sphereGroup.isSelected = false; // Unselect current control point
            //     if (sphereGroup.sphereObject.TryGetComponent<Renderer>(out var rend))
            //     {
            //         rend.material.color = unselectedOriginalColor; // Reset to original color
            //         rend.material.SetColor("_EmissionColor", unselectedOriginalColor * gizmoEmissionIntensity);
            //     }
            // }

            // if (selectedSpline != null)
            // {
            //     EventHub.Publish(new SplineSelectionChange(selectedSpline, false));
            // }
        }
    }
}
