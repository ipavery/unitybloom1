using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the move gizmos and updates selected control points while dragging.
/// </summary>
public class GizmoController : MonoBehaviour
{
    [SerializeField] private SplinePickerData SPD;

    [Header("Gizmo Settings")]
    public GameObject moveGizmoPrefab;
    public GameObject planarGizmoPrefab;
    public GameObject rotateGizmoPrefab;
    private Vector3 gizmoPrefabScale;
    private Vector3 planarGizmoPrefabScale;
    private Vector3 rotateGizmoPrefabScale;
    private float rotateGizmoOffsetDistance = .6f;
    private float gizmoOffsetDistance = .15f;
    public float gizmoScale = .1f;
    private GameObject[] gizmoList; //array of all gizmos

    private int activeGizmoAxis = -1;
    private Plane dragPlane;
    private float distance;
    private Vector3 initialOffset;

    // rotation variables state tracking
    private Vector3 initialHitVector; 
    private Dictionary<int, Vector3> initialPointPositions = new Dictionary<int, Vector3>();

    void OnEnable()
    {
        EventHub.Subscribe<ShowMoveGizmosEvent>(OnShowMoveGizmos);
        EventHub.Subscribe<GizmoDragStarted>(OnGizmoDragStarted);
        EventHub.Subscribe<GizmoDragEnded>(OnGizmoDragEnded);
        EventHub.Subscribe<ClearSelection>(OnClearSelection);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ShowMoveGizmosEvent>(OnShowMoveGizmos);
        EventHub.Unsubscribe<GizmoDragStarted>(OnGizmoDragStarted);
        EventHub.Unsubscribe<GizmoDragEnded>(OnGizmoDragEnded);
        EventHub.Unsubscribe<ClearSelection>(OnClearSelection);
    }

    void Start()
    {
        gizmoPrefabScale = moveGizmoPrefab.transform.localScale;
        planarGizmoPrefabScale = planarGizmoPrefab.transform.localScale;
        rotateGizmoPrefabScale = rotateGizmoPrefab.transform.localScale;

        gizmoList = new GameObject[9]; // Exactly 9 slots
    }

    void Update()
    {
        if (SPD.lastHighlighted != null)
        {
            distance = Vector3.Distance(Camera.main.transform.position, SPD.lastHighlighted.transform.position);
        }

        if (gizmoList != null)
        {
            foreach (var gizmo in gizmoList)
            {
                if (gizmo != null && SPD.lastHighlighted != null)
                {
                    if (gizmo.name.Contains("MoveGizmoPrefab"))
                    {
                        gizmo.transform.localScale = distance * gizmoScale * gizmoPrefabScale;
                        gizmo.transform.position = SPD.lastHighlighted.transform.position + distance * gizmoOffsetDistance * gizmo.transform.up;
                    }
                    else if (gizmo.name.Contains("PlanarGizmoPrefab"))
                    {
                        gizmo.transform.localScale = distance * gizmoScale * planarGizmoPrefabScale;
                        gizmo.transform.position = SPD.lastHighlighted.transform.position + distance * gizmoOffsetDistance * (gizmo.transform.up + gizmo.transform.right * .5f);
                    }
                    else if (gizmo.name.Contains("RotateGizmoPrefab"))
                    {
                        gizmo.transform.localScale = distance * gizmoScale * rotateGizmoPrefabScale;
                        gizmo.transform.position = SPD.lastHighlighted.transform.position + distance * rotateGizmoOffsetDistance * (gizmo.transform.up * .5f);
                    }
                }
            }
        }

        if (activeGizmoAxis != -1 && SPD.lastHighlighted != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            SetDragPlane();
            Vector3 pivot = SPD.lastHighlighted.transform.position;
            SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;

            // --- TRANSLATION (Move & Planar) ---
            if (activeGizmoAxis < (int)SplinePickerData.GizmoType.RotateX)
            {
                FindGizmoAxisHitPoint(out Vector3 newControlPos, ray, dragPlane, activeGizmoAxis, pivot);
                Vector3 lastHighlightedPos = SPD.lastHighlighted.transform.position;

                foreach (var sphereGroup in SPD.controlSphereGroup)
                {
                    if (!sphereGroup.isSelected) continue;

                    var sphere = sphereGroup.sphereObject;
                    Vector3 diff = sphere.transform.position - lastHighlightedPos;
                    Vector3 offset = newControlPos - lastHighlightedPos - initialOffset;
                    sphere.transform.position = lastHighlightedPos + diff + offset;

                    var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();
                    spline.points[sphereGroup.index] += offset;
                }
            }
            // --- ROTATION ---
            else
            {
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 currentHitPoint = ray.GetPoint(enter);
                    Vector3 currentVector = (currentHitPoint - pivot).normalized;

                    // Figure out what axis we are rotating AROUND based on the visual rings
                    Vector3 rotAxis = Vector3.zero;
                    if (axis == SplinePickerData.GizmoType.RotateX) rotAxis = Vector3.forward; // Red (_XY Visual) rotates around Z
                    else if (axis == SplinePickerData.GizmoType.RotateY) rotAxis = Vector3.right; // Green (_YZ Visual) rotates around X
                    else if (axis == SplinePickerData.GizmoType.RotateZ) rotAxis = Vector3.up; // Blue (_ZX Visual) rotates around Y

                    // Calculate the angle difference
                    float angle = Vector3.SignedAngle(initialHitVector, currentVector, rotAxis);
                    Quaternion rotationDelta = Quaternion.AngleAxis(angle, rotAxis);

                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected) continue;

                        var sphere = sphereGroup.sphereObject;

                        // Get the starting position relative to the pivot, then rotate it
                        Vector3 initialPos = initialPointPositions[sphereGroup.index];
                        Vector3 dirFromPivot = initialPos - pivot;
                        Vector3 newPos = pivot + (rotationDelta * dirFromPivot);

                        sphere.transform.position = newPos;

                        var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();
                        Vector3 localPosition = newPos - spline.transform.position; // Convert to local position
                        spline.points[sphereGroup.index] = localPosition;
                    }
                }
            }
        }
    }

    void OnShowMoveGizmos(ShowMoveGizmosEvent e)
    {
        ShowMoveGizmos(e.position);
    }

    void OnGizmoDragStarted(GizmoDragStarted e)
{
    if (SPD.lastHighlighted == null) return;

    activeGizmoAxis = SPD.activeGizmoAxis;
    Vector3 pivot = SPD.lastHighlighted.transform.position;
    initialOffset = e.position - pivot;

    // Cache initial positions so points don't drift or double-transform while dragging
    initialPointPositions.Clear();
    foreach (var sphereGroup in SPD.controlSphereGroup)
    {
        if (sphereGroup.isSelected)
        {
            initialPointPositions[sphereGroup.index] = sphereGroup.sphereObject.transform.position;
        }
    }

    // If we clicked a Rotate Gizmo, calculate the starting angle vector
    if (activeGizmoAxis >= (int)SplinePickerData.GizmoType.RotateX)
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        SetDragPlane(); // Ensure the plane is set to the correct visual rotation ring
        if (dragPlane.Raycast(ray, out float enter))
        {
            initialHitVector = (ray.GetPoint(enter) - pivot).normalized;
        }
    }
}

    void OnGizmoDragEnded(GizmoDragEnded e)
    {
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();
    }

    void OnClearSelection(ClearSelection e)
    {
        DestroyGizmos();
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();
    }

    void ShowMoveGizmos(Vector3 position)
    {
        DestroyGizmos();
        gizmoList = new GameObject[9]; // Exactly 9 slots

        for (int i = 0; i < 3; i++)
        {
            GameObject moveGizmo = Instantiate(moveGizmoPrefab, position, Quaternion.identity);
            moveGizmo.transform.SetParent(transform, false);
            moveGizmo.layer = LayerMask.NameToLayer("PP Layer");
            if (moveGizmo.TryGetComponent<Renderer>(out var rend))
            {
                rend.material.EnableKeyword("_EMISSION");
            }

            GameObject planarGizmo = Instantiate(planarGizmoPrefab, position, Quaternion.identity);
            planarGizmo.transform.SetParent(transform, false);
            planarGizmo.layer = LayerMask.NameToLayer("PP Layer");
            if (planarGizmo.TryGetComponent<Renderer>(out var planarRend))
            {
                planarRend.material.EnableKeyword("_EMISSION");
            }

            GameObject rotateGizmo = Instantiate(rotateGizmoPrefab, position, Quaternion.identity);
            rotateGizmo.transform.SetParent(transform, false);
            rotateGizmo.layer = LayerMask.NameToLayer("PP Layer");
            if (rotateGizmo.TryGetComponent<Renderer>(out var rotateRend))
            {
                rotateRend.material.EnableKeyword("_EMISSION");
            }


            if (i == 0)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                rend.material.color = Color.red;
                rend.material.SetColor("_EmissionColor", Color.red * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                planarRend.material.color = Color.red;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                rotateRend.material.color = Color.red;
                rotateRend.material.SetColor("_EmissionColor", rotateRend.material.color * SPD.gizmoEmissionIntensity);

                moveGizmo.name = "MoveGizmoPrefab_X";
                planarGizmo.name = "PlanarGizmoPrefab_XY";
                rotateGizmo.name = "RotateGizmoPrefab_XY";

                gizmoList[(int)SplinePickerData.GizmoType.MoveX] = moveGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.PlanarX] = planarGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.RotateX] = rotateGizmo;
            }
            else if (i == 1)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                rend.material.color = Color.green;
                rend.material.SetColor("_EmissionColor", Color.green * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                planarRend.material.color = Color.green;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                rotateRend.material.color = Color.green;
                rotateRend.material.SetColor("_EmissionColor", rotateRend.material.color * SPD.gizmoEmissionIntensity);

                moveGizmo.name = "MoveGizmoPrefab_Y";
                planarGizmo.name = "PlanarGizmoPrefab_YZ";
                rotateGizmo.name = "RotateGizmoPrefab_YZ";

                gizmoList[(int)SplinePickerData.GizmoType.MoveY] = moveGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.PlanarY] = planarGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.RotateY] = rotateGizmo;
            }
            else if (i == 2)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                rend.material.color = Color.blue;
                rend.material.SetColor("_EmissionColor", Color.blue * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                planarRend.material.color = Color.blue;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);

                rotateGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                rotateRend.material.color = Color.blue;
                rotateRend.material.SetColor("_EmissionColor", rotateRend.material.color * SPD.gizmoEmissionIntensity);

                moveGizmo.name = "MoveGizmoPrefab_Z";
                planarGizmo.name = "PlanarGizmoPrefab_ZX";
                rotateGizmo.name = "RotateGizmoPrefab_ZX";

                gizmoList[(int)SplinePickerData.GizmoType.MoveZ] = moveGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.PlanarZ] = planarGizmo;
                gizmoList[(int)SplinePickerData.GizmoType.RotateZ] = rotateGizmo;
            }
        }
    }
//z-planar mover is currently x-planar mover??
    void FindGizmoAxisHitPoint(out Vector3 intersection, Ray ray, Plane dragPlane, int gizmoAxisDir, Vector3 controlPointPosition)
{
    intersection = Vector3.zero;
    if (dragPlane.Raycast(ray, out float enter))
    {
        intersection = ray.GetPoint(enter);
    }

    SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)gizmoAxisDir;

    // --- LINEAR MOVES ---
    if (axis == SplinePickerData.GizmoType.MoveX) { intersection.y = controlPointPosition.y; intersection.z = controlPointPosition.z; }
    else if (axis == SplinePickerData.GizmoType.MoveY) { intersection.x = controlPointPosition.x; intersection.z = controlPointPosition.z; }
    else if (axis == SplinePickerData.GizmoType.MoveZ) { intersection.x = controlPointPosition.x; intersection.y = controlPointPosition.y; }
    
    // --- PLANAR MOVES ---
    // Red Planar (Visually XY Plane) -> Lock Z
    else if (axis == SplinePickerData.GizmoType.PlanarX) 
    {
        intersection.z = controlPointPosition.z;
    }
    // Green Planar (Visually YZ Plane) -> Lock X
    else if (axis == SplinePickerData.GizmoType.PlanarY) 
    {
        intersection.x = controlPointPosition.x;
    }
    // Blue Planar (Visually ZX Plane) -> Lock Y
    else if (axis == SplinePickerData.GizmoType.PlanarZ) 
    {
        intersection.y = controlPointPosition.y;
    }
}

    void SetDragPlane()
{
    Vector3 pivot = SPD.lastHighlighted.transform.position;
    SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;

    switch (axis)
    {
        // --- X Axes (Red) ---
        case SplinePickerData.GizmoType.MoveX:
            dragPlane = new Plane(Vector3.up, pivot); // XZ plane to catch X moves
            break;
        case SplinePickerData.GizmoType.PlanarX:
        case SplinePickerData.GizmoType.RotateX:
            dragPlane = new Plane(Vector3.forward, pivot); // Matches your _XY visual
            break;

        // --- Y Axes (Green) ---
        case SplinePickerData.GizmoType.MoveY:
            dragPlane = new Plane(Vector3.right, pivot); // YZ plane to catch Y moves
            break;
        case SplinePickerData.GizmoType.PlanarY:
        case SplinePickerData.GizmoType.RotateY:
            dragPlane = new Plane(Vector3.right, pivot); // Matches your _YZ visual
            break;

        // --- Z Axes (Blue) ---
        case SplinePickerData.GizmoType.MoveZ:
            dragPlane = new Plane(Vector3.up, pivot); // XZ plane to catch Z moves
            break;
        case SplinePickerData.GizmoType.PlanarZ:
        case SplinePickerData.GizmoType.RotateZ:
            dragPlane = new Plane(Vector3.up, pivot); // Matches your _ZX visual
            break;
    }
}

    public bool TryGetGizmoAxisIndex(GameObject candidate, out int axisIndex)
    {
        for (int i = 0; i < gizmoList.Length; i++)
        {
            if (gizmoList[i] == candidate)
            {
                axisIndex = i;
                return true;
            }
        }

        axisIndex = -1;
        return false;
    }

    void DestroyGizmos()
    {
        if (gizmoList != null)
        {
            foreach (var gizmo in gizmoList)
            {
            Destroy(gizmo);
            }
        }
        gizmoList = null;
    }
}
