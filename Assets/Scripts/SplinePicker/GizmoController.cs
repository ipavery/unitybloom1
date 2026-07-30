using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the spawning, visual scaling, and mathematical dragging logic for 3D manipulation gizmos
/// (Move, Planar, Rotate, and Scale handles) used to edit spline control points.
/// </summary>
public class GizmoController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplinePickerData SPD; // Central data container for the selection state

    [Header("Gizmo Prefabs")]
    public GameObject moveGizmoPrefab;     // Standard 1D arrow handles
    public GameObject planarGizmoPrefab;   // 2D plane handles (the squares between axes)
    public GameObject rotateGizmoPrefab;   // rotation rings
    public GameObject scaleGizmoPrefab;    // 2D scaling handles

    [Header("Visual Settings")]
    public float gizmoScale = 0.1f;                  // Base multiplier for how large gizmos appear
    private float gizmoOffsetDistance = 0.15f;       // Distance to push translation gizmos out from the pivot

    // Cached prefab scales so we can dynamically scale them up/down without losing their original proportions
    private Vector3 gizmoPrefabScale;
    private Vector3 planarGizmoPrefabScale;
    public float rotateGizmoPrefabScale;
    public float scaleGizmoPrefabScale;

    // We use a fixed 12-slot array corresponding exactly to the SplinePickerData.GizmoType enum (3 axes * 4 types)
    private GameObject[] gizmoList;

    [Header("Drag State")]
    private int activeGizmoAxis = -1; // -1 means nothing is currently being dragged
    private Plane dragPlane;          // The invisible mathematical plane we cast our mouse ray against
    private float distanceToCamera;   // Used to keep gizmos the same visual size on screen regardless of zoom
    
    private Vector3 initialOffset;    // Distance from the exact mouse click point to the gizmo's origin
    private Vector3 cachedDragPivot;  // <--- THE FIX: A frozen anchor point so scaling doesn't feedback loop

    // --- State Tracking to Prevent Jitter ---
    private Vector3 initialHitVector; // Rotation: The exact directional vector from pivot to mouse when the drag started
    private float initialHitDistance; // Scale: The exact distance from pivot to mouse when scale drag started
    private Dictionary<GameObject, Vector3> initialPointPositions = new Dictionary<GameObject, Vector3>(); // Caches starting positions of selected points

    #region Event Subscription
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
    #endregion

    void Start()
    {
        // Cache original prefab scales so dynamic scaling doesn't distort them
        gizmoPrefabScale = moveGizmoPrefab.transform.localScale;
        planarGizmoPrefabScale = planarGizmoPrefab.transform.localScale;

        // Initialize array to exact size of the GizmoType enum (12 handles total)
        gizmoList = new GameObject[12];
    }

    void Update()
    {
        // ---------------------------------------------------------------------
        // PART 1: VISUAL GIZMO UPDATES (Runs every frame if something is selected)
        // ---------------------------------------------------------------------
        if (SPD.lastHighlighted != null)
        {
            distanceToCamera = Vector3.Distance(Camera.main.transform.position, SPD.lastHighlighted.transform.position);
        }
        else
        {
            DestroyGizmos(); 
        }

        if (gizmoList != null && SPD.lastHighlighted != null)
        {
            foreach (var gizmo in gizmoList)
            {
                if (gizmo == null) continue;

                if (gizmo.name.Contains("MoveGizmoPrefab"))
                {
                    gizmo.transform.localScale = distanceToCamera * gizmoScale * gizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + distanceToCamera * gizmoOffsetDistance * gizmo.transform.up;
                }
                else if (gizmo.name.Contains("PlanarGizmoPrefab"))
                {
                    gizmo.transform.localScale = distanceToCamera * gizmoScale * planarGizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + distanceToCamera * gizmoOffsetDistance * (gizmo.transform.up + gizmo.transform.right * 0.5f);
                }
                else if (gizmo.name.Contains("RotateGizmoPrefab"))
                {
                    gizmo.transform.localScale = distanceToCamera * gizmoScale * rotateGizmoPrefabScale * Vector3.one;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position;
                }
                else if (gizmo.name.Contains("ScaleGizmoPrefab"))
                {
                    gizmo.transform.localScale = distanceToCamera * gizmoScale * scaleGizmoPrefabScale * Vector3.one;
                    // Push scale gizmo slightly further out than planar so they don't overlap
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + distanceToCamera * gizmoOffsetDistance *1.2f* gizmo.transform.up;
                }
            }
        }

        // ---------------------------------------------------------------------
        // PART 2: ACTIVE DRAGGING LOGIC (Runs only while a gizmo is clicked)
        // ---------------------------------------------------------------------
        if (activeGizmoAxis != -1 && SPD.lastHighlighted != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            
            // STRICTLY USE CACHED PIVOT. This stops the infinity feedback loop.
            Vector3 pivot = cachedDragPivot; 
            SetDragPlane(pivot); 

            SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;

            // --- CONFLICT RESOLUTION PREP (For Translation Only) ---
            Dictionary<BezierSpline, HashSet<int>> selectedKnots = new Dictionary<BezierSpline, HashSet<int>>();
            foreach (var group in SPD.controlSphereGroup)
            {
                if (group.isSelected && group.index % 3 == 0)
                {
                    var spline = group.sphereObject.transform.parent.parent.GetComponent<BezierSpline>();
                    if (spline != null)
                    {
                        if (!selectedKnots.ContainsKey(spline))
                            selectedKnots[spline] = new HashSet<int>();
                        
                        selectedKnots[spline].Add(group.index);
                    }
                }
            }

            // --- TRANSLATION (1D Move Arrows & 2D Planar Squares) ---
            if (activeGizmoAxis < (int)SplinePickerData.GizmoType.RotateX)
            {
                FindGizmoAxisHitPoint(out Vector3 newControlPos, ray, dragPlane, activeGizmoAxis, pivot);
                
                // Calculate absolute offset based entirely on the frozen start points
                Vector3 offset = newControlPos - pivot - initialOffset;

                foreach (var sphereGroup in SPD.controlSphereGroup)
                {
                    if (!sphereGroup.isSelected) continue;
                    var sphere = sphereGroup.sphereObject;
                    var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();
                    
                    if (sphereGroup.index % 3 != 0)
                    {
                        int parentKnotIndex = (sphereGroup.index % 3 == 1) ? sphereGroup.index - 1 : sphereGroup.index + 1;
                        if (selectedKnots.ContainsKey(spline) && selectedKnots[spline].Contains(parentKnotIndex)) 
                            continue; 
                    }

                    // Use the exact starting location of each sphere to prevent drift
                    Vector3 initialPos = initialPointPositions[sphere];
                    Vector3 newWorldPos = initialPos + offset;
                    Vector3 localPosition = spline.transform.InverseTransformPoint(newWorldPos);
                    spline.SetControlPoint(sphereGroup.index, localPosition);
                }
                SyncSpherePositions(); 
            }
            // --- ROTATION (Rings) ---
            else if (activeGizmoAxis >= (int)SplinePickerData.GizmoType.RotateX && activeGizmoAxis < (int)SplinePickerData.GizmoType.ScaleX)
            {
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 currentHitPoint = ray.GetPoint(enter);
                    Vector3 currentVector = (currentHitPoint - pivot).normalized;
                    Vector3 rotAxis = Vector3.zero;

                    if (axis == SplinePickerData.GizmoType.RotateX) rotAxis = Vector3.forward;
                    else if (axis == SplinePickerData.GizmoType.RotateY) rotAxis = Vector3.right; 
                    else if (axis == SplinePickerData.GizmoType.RotateZ) rotAxis = Vector3.up;
                    
                    float angle = Vector3.SignedAngle(initialHitVector, currentVector, rotAxis);
                    Quaternion rotationDelta = Quaternion.AngleAxis(angle, rotAxis);

                    // PASS 1: Anchors
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 != 0) continue; 
                        ApplyRotationToPoint(sphereGroup, pivot, rotationDelta);
                    }

                    // PASS 2: Tangents
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 == 0) continue;
                        ApplyRotationToPoint(sphereGroup, pivot, rotationDelta);
                    }

                    SyncSpherePositions();
                }
            }
            // --- SCALE (Planar) ---
            else if (activeGizmoAxis >= (int)SplinePickerData.GizmoType.ScaleX)
            {
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 currentHitPoint = ray.GetPoint(enter);
                    float currentDist = Vector3.Distance(currentHitPoint, pivot);
                    
                    // Prevent division by zero
                    float scaleFactor = currentDist / Mathf.Max(initialHitDistance, 0.001f);

                    // PASS 1: Anchors
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 != 0) continue; 
                        ApplyScaleToPoint(sphereGroup, pivot, scaleFactor, axis);
                    }

                    // PASS 2: Tangents
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 == 0) continue;
                        ApplyScaleToPoint(sphereGroup, pivot, scaleFactor, axis);
                    }

                    SyncSpherePositions();
                }
            }
        }
    }

    #region Event Handlers
    void OnShowMoveGizmos(ShowMoveGizmosEvent e)
    {
        ShowMoveGizmos(e.position);
    }

    void OnGizmoDragStarted(GizmoDragStarted e)
    {
        UndoManager.Instance.RecordState();
        if (SPD.lastHighlighted == null) return;

        activeGizmoAxis = SPD.activeGizmoAxis;
        
        // --- THE FIX ---
        // Lock the pivot position when the click starts. This never changes until the click is released.
        cachedDragPivot = SPD.lastHighlighted.transform.position; 
        Vector3 pivot = cachedDragPivot;

        initialOffset = e.position - pivot;

        initialPointPositions.Clear();
        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup.isSelected)
            {
                initialPointPositions[sphereGroup.sphereObject] = sphereGroup.sphereObject.transform.position;
            }
        }

        // Setup for Rotation & Scale: Cache starting vectors and distances
        if (activeGizmoAxis >= (int)SplinePickerData.GizmoType.RotateX)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            SetDragPlane(pivot);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                initialHitVector = (hitPoint - pivot).normalized;
                initialHitDistance = Mathf.Max(Vector3.Distance(hitPoint, pivot), 0.001f);
            }
        }
    }

    void OnGizmoDragEnded(GizmoDragEnded e)
    {
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();

        SaveLoadUI.Instance.NotifyActionPerformed(); 
    }

    void OnClearSelection(ClearSelection e)
    {
        DestroyGizmos();
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();
    }
    #endregion

    #region Helper Methods

    void ApplyRotationToPoint(ControlPointGroup sphereGroup, Vector3 pivot, Quaternion rotationDelta)
    {
        var sphere = sphereGroup.sphereObject;
        var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();

        Vector3 initialPos = initialPointPositions[sphereGroup.sphereObject];
        Vector3 dirFromPivot = initialPos - pivot;

        Vector3 newWorldPos = pivot + (rotationDelta * dirFromPivot);
        Vector3 localPosition = spline.transform.InverseTransformPoint(newWorldPos);
        spline.SetControlPoint(sphereGroup.index, localPosition);
    }
    
    void ApplyScaleToPoint(ControlPointGroup sphereGroup, Vector3 pivot, float scaleFactor, SplinePickerData.GizmoType axis)
    {
        var sphere = sphereGroup.sphereObject;
        var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();

        Vector3 initialPos = initialPointPositions[sphereGroup.sphereObject];
        Vector3 dirFromPivot = initialPos - pivot;

        // Multiply the outward distance based on which plane is active
        if (axis == SplinePickerData.GizmoType.ScaleX) { dirFromPivot.x *= scaleFactor; dirFromPivot.y *= scaleFactor; }
        else if (axis == SplinePickerData.GizmoType.ScaleY) { dirFromPivot.y *= scaleFactor; dirFromPivot.z *= scaleFactor; }
        else if (axis == SplinePickerData.GizmoType.ScaleZ) { dirFromPivot.x *= scaleFactor; dirFromPivot.z *= scaleFactor; }

        Vector3 newWorldPos = pivot + dirFromPivot;
        Vector3 localPosition = spline.transform.InverseTransformPoint(newWorldPos);
        spline.SetControlPoint(sphereGroup.index, localPosition);
    }

    void ShowMoveGizmos(Vector3 position)
    {
        DestroyGizmos();
        gizmoList = new GameObject[12];
        
        bool isClassic = SPD.currentInteractionMode == SplinePickerData.InteractionMode.Classic;

        for (int i = 0; i < 3; i++)
        {
            GameObject moveGizmo = isClassic ? Instantiate(moveGizmoPrefab, position, Quaternion.identity, transform) : null;
            GameObject planarGizmo = isClassic ? Instantiate(planarGizmoPrefab, position, Quaternion.identity, transform) : null;
            
            // In DirectDrag, ONLY spawn the X-Axis items (Z-plane manipulators)
            GameObject rotateGizmo = (isClassic || i == 0) ? Instantiate(rotateGizmoPrefab, position, Quaternion.identity, transform) : null;
            GameObject scaleGizmo = (isClassic || i == 0) && scaleGizmoPrefab != null ? Instantiate(scaleGizmoPrefab, position, Quaternion.identity, transform) : null;

            if (moveGizmo) { moveGizmo.layer = LayerMask.NameToLayer("PP Layer"); moveGizmo.TryGetComponent<Renderer>(out var r); if (r) r.material.EnableKeyword("_EMISSION"); }
            if (planarGizmo) { planarGizmo.layer = LayerMask.NameToLayer("PP Layer"); planarGizmo.TryGetComponent<Renderer>(out var r); if (r) r.material.EnableKeyword("_EMISSION"); }
            if (rotateGizmo) { rotateGizmo.layer = LayerMask.NameToLayer("PP Layer"); rotateGizmo.TryGetComponent<Renderer>(out var r); if (r) r.material.EnableKeyword("_EMISSION"); }
            if (scaleGizmo) { scaleGizmo.layer = LayerMask.NameToLayer("PP Layer"); scaleGizmo.TryGetComponent<Renderer>(out var r); if (r) r.material.EnableKeyword("_EMISSION"); }

            // X-Axis (RED) 
            if (i == 0)
            {
                if (moveGizmo)
                {
                    moveGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                    var r = moveGizmo.GetComponent<Renderer>(); r.material.color = Color.red; r.material.SetColor("_EmissionColor", new Color(.47f,.11f,.12f));
                    moveGizmo.name = "MoveGizmoPrefab_X"; gizmoList[(int)SplinePickerData.GizmoType.MoveX] = moveGizmo;
                }
                if (planarGizmo)
                {
                    planarGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                    var r = planarGizmo.GetComponent<Renderer>(); r.material.color = Color.red; r.material.SetColor("_EmissionColor", new Color(.47f,.11f,.12f));
                    planarGizmo.name = "PlanarGizmoPrefab_XY"; gizmoList[(int)SplinePickerData.GizmoType.PlanarX] = planarGizmo;
                }
                if (rotateGizmo)
                {
                    rotateGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var r = rotateGizmo.GetComponent<Renderer>(); r.material.color = Color.red; r.material.SetColor("_EmissionColor", new Color(.47f,.11f,.12f));
                    rotateGizmo.name = "RotateGizmoPrefab_XY"; gizmoList[(int)SplinePickerData.GizmoType.RotateX] = rotateGizmo;
                }
                if (scaleGizmo)
                {
                    scaleGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                    var r = scaleGizmo.GetComponent<Renderer>(); r.material.color = Color.red; r.material.SetColor("_EmissionColor", new Color(.47f,.11f,.12f));
                    scaleGizmo.name = "ScaleGizmoPrefab_XY"; gizmoList[(int)SplinePickerData.GizmoType.ScaleX] = scaleGizmo;
                }
            }
            // Y-Axis (GREEN)
            else if (i == 1 && isClassic)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var mr = moveGizmo.GetComponent<Renderer>(); mr.material.color = Color.green; mr.material.SetColor("_EmissionColor", new Color(.12f,.47f,.12f));
                moveGizmo.name = "MoveGizmoPrefab_Y"; gizmoList[(int)SplinePickerData.GizmoType.MoveY] = moveGizmo;

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var pr = planarGizmo.GetComponent<Renderer>(); pr.material.color = Color.green; pr.material.SetColor("_EmissionColor", new Color(.12f,.47f,.12f));
                planarGizmo.name = "PlanarGizmoPrefab_YZ"; gizmoList[(int)SplinePickerData.GizmoType.PlanarY] = planarGizmo;

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var rr = rotateGizmo.GetComponent<Renderer>(); rr.material.color = Color.green; rr.material.SetColor("_EmissionColor", new Color(.12f,.47f,.12f));
                rotateGizmo.name = "RotateGizmoPrefab_YZ"; gizmoList[(int)SplinePickerData.GizmoType.RotateY] = rotateGizmo;
                
                if (scaleGizmo)
                {
                    scaleGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    var sr = scaleGizmo.GetComponent<Renderer>(); sr.material.color = Color.green; sr.material.SetColor("_EmissionColor", new Color(.12f,.47f,.12f));
                    scaleGizmo.name = "ScaleGizmoPrefab_YZ"; gizmoList[(int)SplinePickerData.GizmoType.ScaleY] = scaleGizmo;
                }
            }
            // Z-Axis (BLUE)
            else if (i == 2 && isClassic)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var mr = moveGizmo.GetComponent<Renderer>(); mr.material.color = Color.blue; mr.material.SetColor("_EmissionColor", new Color(.12f,.12f,.47f));
                moveGizmo.name = "MoveGizmoPrefab_Z"; gizmoList[(int)SplinePickerData.GizmoType.MoveZ] = moveGizmo;

                planarGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var pr = planarGizmo.GetComponent<Renderer>(); pr.material.color = Color.blue; pr.material.SetColor("_EmissionColor", new Color(.12f,.12f,.47f));
                planarGizmo.name = "PlanarGizmoPrefab_ZX"; gizmoList[(int)SplinePickerData.GizmoType.PlanarZ] = planarGizmo;

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 0, 0);
                var rr = rotateGizmo.GetComponent<Renderer>(); rr.material.color = Color.blue; rr.material.SetColor("_EmissionColor", new Color(.12f,.12f,.47f));
                rotateGizmo.name = "RotateGizmoPrefab_ZX"; gizmoList[(int)SplinePickerData.GizmoType.RotateZ] = rotateGizmo;

                if (scaleGizmo)
                {
                    scaleGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var sr = scaleGizmo.GetComponent<Renderer>(); sr.material.color = Color.blue; sr.material.SetColor("_EmissionColor", new Color(.12f,.12f,.47f));
                    scaleGizmo.name = "ScaleGizmoPrefab_ZX"; gizmoList[(int)SplinePickerData.GizmoType.ScaleZ] = scaleGizmo;
                }
            }
        }
    }

    void FindGizmoAxisHitPoint(out Vector3 intersection, Ray ray, Plane dragPlane, int gizmoAxisDir, Vector3 controlPointPosition)
    {
        intersection = controlPointPosition; 
        
        if (dragPlane.Raycast(ray, out float enter))
        {
            intersection = ray.GetPoint(enter);
        }

        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)gizmoAxisDir;

        if (axis == SplinePickerData.GizmoType.MoveX) { intersection.y = controlPointPosition.y; intersection.z = controlPointPosition.z; }
        else if (axis == SplinePickerData.GizmoType.MoveY) { intersection.x = controlPointPosition.x; intersection.z = controlPointPosition.z; }
        else if (axis == SplinePickerData.GizmoType.MoveZ) { intersection.x = controlPointPosition.x; intersection.y = controlPointPosition.y; }
        else if (axis == SplinePickerData.GizmoType.PlanarX) { intersection.z = controlPointPosition.z; }
        else if (axis == SplinePickerData.GizmoType.PlanarY) { intersection.x = controlPointPosition.x; }
        else if (axis == SplinePickerData.GizmoType.PlanarZ) { intersection.y = controlPointPosition.y; }
    }

    void SetDragPlane(Vector3 pivot)
    {
        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;
        Vector3 camForward = Camera.main.transform.forward;

        switch (axis)
        {
            case SplinePickerData.GizmoType.MoveX:
                Vector3 crossX = Vector3.Cross(Vector3.right, camForward);
                dragPlane = new Plane(Vector3.Cross(crossX, Vector3.right).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.MoveY:
                Vector3 crossY = Vector3.Cross(Vector3.up, camForward);
                dragPlane = new Plane(Vector3.Cross(crossY, Vector3.up).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.MoveZ:
                Vector3 crossZ = Vector3.Cross(Vector3.forward, camForward);
                dragPlane = new Plane(Vector3.Cross(crossZ, Vector3.forward).normalized, pivot);
                break;
            case SplinePickerData.GizmoType.PlanarX:
            case SplinePickerData.GizmoType.RotateX:
            case SplinePickerData.GizmoType.ScaleX:
                dragPlane = new Plane(Vector3.forward, pivot); // XY plane
                break;
            case SplinePickerData.GizmoType.PlanarY:
            case SplinePickerData.GizmoType.RotateY:
            case SplinePickerData.GizmoType.ScaleY:
                dragPlane = new Plane(Vector3.right, pivot); // YZ plane
                break;
            case SplinePickerData.GizmoType.PlanarZ:
            case SplinePickerData.GizmoType.RotateZ:
            case SplinePickerData.GizmoType.ScaleZ:
                dragPlane = new Plane(Vector3.up, pivot); // XZ plane
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
                if (gizmo != null) Destroy(gizmo);
            }
        }
        gizmoList = null;
    }

    void SyncSpherePositions()
    {
        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup.sphereObject == null) continue;
            
            var spline = sphereGroup.sphereObject.transform.parent.parent.GetComponent<BezierSpline>();
            if (spline != null)
            {
                Vector3 correctWorldPos = spline.transform.TransformPoint(spline.points[sphereGroup.index]);
                sphereGroup.sphereObject.transform.position = correctWorldPos;
            }
        }
    }
}
#endregion