using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the spawning, visual scaling, and mathematical dragging logic for 3D manipulation gizmos
/// (Move, Planar, and Rotate handles) used to edit spline control points.
/// </summary>
public class GizmoController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplinePickerData SPD; // Central data container for the selection state

    [Header("Gizmo Prefabs")]
    public GameObject moveGizmoPrefab;     // Standard 1D arrow handles
    public GameObject planarGizmoPrefab;   // 2D plane handles (the squares between axes)
    public GameObject rotateGizmoPrefab;   // rotation rings

    [Header("Visual Settings")]
    public float gizmoScale = 0.1f;                  // Base multiplier for how large gizmos appear
    private float gizmoOffsetDistance = 0.15f;       // Distance to push translation gizmos out from the pivot
    //private float rotateGizmoOffsetDistance = 0.6f;  // Distance to push rotation rings out from the pivot

    // Cached prefab scales so we can dynamically scale them up/down without losing their original proportions
    private Vector3 gizmoPrefabScale;
    private Vector3 planarGizmoPrefabScale;
    public float rotateGizmoPrefabScale;

    // We use a fixed 9-slot array corresponding exactly to the SplinePickerData.GizmoType enum (3 axes * 3 types (Move, Planar, Rotate))
    private GameObject[] gizmoList;

    [Header("Drag State")]
    private int activeGizmoAxis = -1; // -1 means nothing is currently being dragged
    private Plane dragPlane;          // The invisible mathematical plane we cast our mouse ray against
    private float distanceToCamera;   // Used to keep gizmos the same visual size on screen regardless of zoom
    private Vector3 initialOffset;    // Distance from the exact mouse click point to the gizmo's origin

    // --- State Tracking to Prevent Jitter ---
    private Vector3 initialHitVector; // Rotation: The exact directional vector from pivot to mouse when the drag started
    private Dictionary<GameObject, Vector3> initialPointPositions = new Dictionary<GameObject, Vector3>(); // Caches starting positions of selected points (rotation)

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
        //rotateGizmoPrefabScale = rotateGizmoPrefab.transform.localScale;

        // Initialize array to exact size of the GizmoType enum (9 handles total)
        gizmoList = new GameObject[9];
    }

    void Update()
    {
        // ---------------------------------------------------------------------
        // PART 1: VISUAL GIZMO UPDATES (Runs every frame if something is selected)
        // ---------------------------------------------------------------------
        if (SPD.lastHighlighted != null)
        {
            // Calculate distance to camera so we can scale gizmos up as you zoom out (keeps them selectable)
            distanceToCamera = Vector3.Distance(Camera.main.transform.position, SPD.lastHighlighted.transform.position);
        }
        else
        {
            DestroyGizmos(); // If nothing is selected, destroy all gizmos to prevent them from floating in space
        }

        if (gizmoList != null && SPD.lastHighlighted != null)
        {
            foreach (var gizmo in gizmoList)
            {
                if (gizmo == null) continue;

                // Dynamically update position and scale of each gizmo based on camera distance
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
            }
        }

        // ---------------------------------------------------------------------
        // PART 2: ACTIVE DRAGGING LOGIC (Runs only while a gizmo is clicked)
        // ---------------------------------------------------------------------
        if (activeGizmoAxis != -1 && SPD.lastHighlighted != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            SetDragPlane();

            Vector3 pivot = SPD.lastHighlighted.transform.position;
            SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;

            // --- CONFLICT RESOLUTION PREP (For Translation Only) ---
            // Track which knots are selected ON WHICH SPLINE to prevent cross-contamination
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
                Vector3 lastHighlightedPos = SPD.lastHighlighted.transform.position; 
                
                foreach (var sphereGroup in SPD.controlSphereGroup)
                {
                    if (!sphereGroup.isSelected) continue;

                    var sphere = sphereGroup.sphereObject;
                    var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();

                    // TRANSLATION RULE: If an Anchor is selected, let the internal Bezier math handle its Tangents.
                    if (sphereGroup.index % 3 != 0)
                    {
                        int parentKnotIndex = (sphereGroup.index % 3 == 1) ? sphereGroup.index - 1 : sphereGroup.index + 1;
                        
                        // Check if THIS specific spline has THIS parent knot selected
                        if (selectedKnots.ContainsKey(spline) && selectedKnots[spline].Contains(parentKnotIndex)) 
                            continue; // Prevent double-movement
                    }

                    Vector3 diff = sphere.transform.position - lastHighlightedPos;
                    Vector3 offset = newControlPos - lastHighlightedPos - initialOffset;
                    
                    Vector3 newWorldPos = lastHighlightedPos + diff + offset;
                    Vector3 localPosition = spline.transform.InverseTransformPoint(newWorldPos);
                    spline.SetControlPoint(sphereGroup.index, localPosition);
                }
                
                SyncSpherePositions(); 
            }

            // --- ROTATION (Rings) ---
            else
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

                    // ROTATION RULE: We cannot skip tangents. We must rigidly rotate everything.
                    
                    // PASS 1: Rotate Anchors (Knots) First
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 != 0) continue; 

                        ApplyRotationToPoint(sphereGroup, pivot, rotationDelta);
                    }

                    // PASS 2: Rotate Tangents Second
                    foreach (var sphereGroup in SPD.controlSphereGroup)
                    {
                        if (!sphereGroup.isSelected || sphereGroup.index % 3 == 0) continue;

                        ApplyRotationToPoint(sphereGroup, pivot, rotationDelta);
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
        UndoManager.Instance.RecordState(); // Record the current scene state for undo before any changes are applied

        if (SPD.lastHighlighted == null) return;

        activeGizmoAxis = SPD.activeGizmoAxis;
        Vector3 pivot = SPD.lastHighlighted.transform.position;

        // Cache the exact offset of the mouse to prevent snapping when the user first clicks
        initialOffset = e.position - pivot;

        // Cache initial positions of all selected points. 
        // Crucial for rotation math so points don't incrementally drift over time.
        initialPointPositions.Clear();
        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup.isSelected)
            {
                initialPointPositions[sphereGroup.sphereObject] = sphereGroup.sphereObject.transform.position;
            }
        }

        // Setup for Rotation: Cache the starting direction vector of the mouse
        if (activeGizmoAxis >= (int)SplinePickerData.GizmoType.RotateX)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            SetDragPlane();

            if (dragPlane.Raycast(ray, out float enter))
            {
                initialHitVector = (ray.GetPoint(enter) - pivot).normalized;
            }
        }
    }

    void OnGizmoDragEnded(GizmoDragEnded e)
    {
        // Reset state
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();

        SaveLoadUI.Instance.NotifyActionPerformed(); //autosave
    }

    void OnClearSelection(ClearSelection e)
    {
        // Wipe everything
        DestroyGizmos();
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
        initialPointPositions.Clear();
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Helper method to apply mathematical rotation to a single control point.
    /// </summary>
    void ApplyRotationToPoint(ControlPointGroup sphereGroup, Vector3 pivot, Quaternion rotationDelta)
    {
        var sphere = sphereGroup.sphereObject;
        var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();

        // Calculate rotation relative to the original click state
        Vector3 initialPos = initialPointPositions[sphereGroup.sphereObject];
        Vector3 dirFromPivot = initialPos - pivot;
        Vector3 newWorldPos = pivot + (rotationDelta * dirFromPivot);

        // Apply it to the underlying math array
        Vector3 localPosition = spline.transform.InverseTransformPoint(newWorldPos);
        spline.SetControlPoint(sphereGroup.index, localPosition);
    }
    
    /// <summary>
    /// Instantiates all 9 gizmos (3 axes * 3 types) around the target position and colors them.
    /// </summary>
    void ShowMoveGizmos(Vector3 position)
    {
        DestroyGizmos();
        gizmoList = new GameObject[9];
        
        // Check our current mode
        bool isClassic = SPD.currentInteractionMode == SplinePickerData.InteractionMode.Classic;

        for (int i = 0; i < 3; i++)
        {
            // 1. Spawn Prefabs Conditionally
            GameObject moveGizmo = isClassic ? Instantiate(moveGizmoPrefab, position, Quaternion.identity, transform) : null;
            GameObject planarGizmo = isClassic ? Instantiate(planarGizmoPrefab, position, Quaternion.identity, transform) : null;
            
            // In DirectDrag, ONLY spawn the X-Axis rotation ring (which handles Z-plane rotation)
            GameObject rotateGizmo = (isClassic || i == 0) ? Instantiate(rotateGizmoPrefab, position, Quaternion.identity, transform) : null;

            // 2. Set Layers & Emissions
            if (moveGizmo) 
            {
                moveGizmo.layer = LayerMask.NameToLayer("PP Layer");
                moveGizmo.TryGetComponent<Renderer>(out var rend);
                if (rend) rend.material.EnableKeyword("_EMISSION");
            }
            if (planarGizmo)
            {
                planarGizmo.layer = LayerMask.NameToLayer("PP Layer");
                planarGizmo.TryGetComponent<Renderer>(out var rend);
                if (rend) rend.material.EnableKeyword("_EMISSION");
            }
            if (rotateGizmo)
            {
                rotateGizmo.layer = LayerMask.NameToLayer("PP Layer");
                rotateGizmo.TryGetComponent<Renderer>(out var rend);
                if (rend) rend.material.EnableKeyword("_EMISSION");
            }

            // 3. Orient and Color based on Axis (i)
            // X-Axis (RED) - The only one that survives in DirectDrag mode
            if (i == 0)
            {
                if (moveGizmo)
                {
                    moveGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                    var r = moveGizmo.GetComponent<Renderer>();
                    r.material.color = Color.red;
                    r.material.SetColor("_EmissionColor", Color.red * SPD.gizmoEmissionIntensity);
                    moveGizmo.name = "MoveGizmoPrefab_X";
                    gizmoList[(int)SplinePickerData.GizmoType.MoveX] = moveGizmo;
                }
                if (planarGizmo)
                {
                    planarGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                    var r = planarGizmo.GetComponent<Renderer>();
                    r.material.color = Color.red;
                    r.material.SetColor("_EmissionColor", Color.red * SPD.gizmoEmissionIntensity);
                    planarGizmo.name = "PlanarGizmoPrefab_XY";
                    gizmoList[(int)SplinePickerData.GizmoType.PlanarX] = planarGizmo;
                }
                if (rotateGizmo)
                {
                    rotateGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var r = rotateGizmo.GetComponent<Renderer>();
                    r.material.color = Color.red;
                    r.material.SetColor("_EmissionColor", Color.red * SPD.gizmoEmissionIntensity);
                    rotateGizmo.name = "RotateGizmoPrefab_XY";
                    gizmoList[(int)SplinePickerData.GizmoType.RotateX] = rotateGizmo;
                }
            }
            // Y-Axis (GREEN)
            else if (i == 1 && isClassic)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var mr = moveGizmo.GetComponent<Renderer>();
                mr.material.color = Color.green;
                mr.material.SetColor("_EmissionColor", Color.green * SPD.gizmoEmissionIntensity);
                moveGizmo.name = "MoveGizmoPrefab_Y";
                gizmoList[(int)SplinePickerData.GizmoType.MoveY] = moveGizmo;

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var pr = planarGizmo.GetComponent<Renderer>();
                pr.material.color = Color.green;
                pr.material.SetColor("_EmissionColor", Color.green * SPD.gizmoEmissionIntensity);
                planarGizmo.name = "PlanarGizmoPrefab_YZ";
                gizmoList[(int)SplinePickerData.GizmoType.PlanarY] = planarGizmo;

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var rr = rotateGizmo.GetComponent<Renderer>();
                rr.material.color = Color.green;
                rr.material.SetColor("_EmissionColor", Color.green * SPD.gizmoEmissionIntensity);
                rotateGizmo.name = "RotateGizmoPrefab_YZ";
                gizmoList[(int)SplinePickerData.GizmoType.RotateY] = rotateGizmo;
            }
            // Z-Axis (BLUE)
            else if (i == 2 && isClassic)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var mr = moveGizmo.GetComponent<Renderer>();
                mr.material.color = Color.blue;
                mr.material.SetColor("_EmissionColor", Color.blue * SPD.gizmoEmissionIntensity);
                moveGizmo.name = "MoveGizmoPrefab_Z";
                gizmoList[(int)SplinePickerData.GizmoType.MoveZ] = moveGizmo;

                planarGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var pr = planarGizmo.GetComponent<Renderer>();
                pr.material.color = Color.blue;
                pr.material.SetColor("_EmissionColor", Color.blue * SPD.gizmoEmissionIntensity);
                planarGizmo.name = "PlanarGizmoPrefab_ZX";
                gizmoList[(int)SplinePickerData.GizmoType.PlanarZ] = planarGizmo;

                rotateGizmo.transform.localRotation = Quaternion.Euler(0, 0, 0);
                var rr = rotateGizmo.GetComponent<Renderer>();
                rr.material.color = Color.blue;
                rr.material.SetColor("_EmissionColor", Color.blue * SPD.gizmoEmissionIntensity);
                rotateGizmo.name = "RotateGizmoPrefab_ZX";
                gizmoList[(int)SplinePickerData.GizmoType.RotateZ] = rotateGizmo;
            }
        }
    }

    /// <summary>
    /// Calculates the exact 3D point the user is trying to drag to, constraining 
    /// axes based on which gizmo (move arrow or planar square) is being dragged.
    /// </summary>
    void FindGizmoAxisHitPoint(out Vector3 intersection, Ray ray, Plane dragPlane, int gizmoAxisDir, Vector3 controlPointPosition)
    {
        // FIX: Default to the current position. Prevents a teleport-to-origin bug if the raycast fails.
        intersection = controlPointPosition; 
        
        if (dragPlane.Raycast(ray, out float enter))
        {
            intersection = ray.GetPoint(enter);
        }

        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)gizmoAxisDir;

        // --- 1D LINEAR MOVES (Lock 2 of the 3 axes to the original point) ---
        if (axis == SplinePickerData.GizmoType.MoveX) { intersection.y = controlPointPosition.y; intersection.z = controlPointPosition.z; }
        else if (axis == SplinePickerData.GizmoType.MoveY) { intersection.x = controlPointPosition.x; intersection.z = controlPointPosition.z; }
        else if (axis == SplinePickerData.GizmoType.MoveZ) { intersection.x = controlPointPosition.x; intersection.y = controlPointPosition.y; }

        // --- 2D PLANAR MOVES (Lock 1 of the 3 axes to the original point) ---
        else if (axis == SplinePickerData.GizmoType.PlanarX)
        {
            intersection.z = controlPointPosition.z;
        }
        else if (axis == SplinePickerData.GizmoType.PlanarY)
        {
            intersection.x = controlPointPosition.x;
        }
        else if (axis == SplinePickerData.GizmoType.PlanarZ)
        {
            intersection.y = controlPointPosition.y;
        }
    }

    /// <summary>
    /// Determines the mathematical plane to raycast against. 
    /// 1D axes use a dynamic plane that rotates to face the camera to prevent raycast skipping at shallow angles.
    /// </summary>
    void SetDragPlane()
    {
        Vector3 pivot = SPD.lastHighlighted.transform.position;
        SplinePickerData.GizmoType axis = (SplinePickerData.GizmoType)activeGizmoAxis;
        Vector3 camForward = Camera.main.transform.forward;

        switch (axis)
        {
            // --- 1D Dynamic Axes ---
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

            // --- 2D Planar / Rotation Axes (Remain Fixed) ---
            case SplinePickerData.GizmoType.PlanarX:
            case SplinePickerData.GizmoType.RotateX:
                dragPlane = new Plane(Vector3.forward, pivot); // XY plane
                break;

            case SplinePickerData.GizmoType.PlanarY:
            case SplinePickerData.GizmoType.RotateY:
                dragPlane = new Plane(Vector3.right, pivot); // YZ plane
                break;

            case SplinePickerData.GizmoType.PlanarZ:
            case SplinePickerData.GizmoType.RotateZ:
                dragPlane = new Plane(Vector3.up, pivot); // XZ plane
                break;
        }
    }

    /// <summary>
    /// Helper to find the enum index based on which physical GameObject was clicked.
    /// </summary>
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

    /// <summary>
    /// Cleans up all instantiated gizmo objects.
    /// </summary>
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

    /// <summary>
    /// Forces all control point spheres to visually align with their mathematical coordinates in the spline array.
    /// This is required because moving one tangent mathematically mirrors the opposite tangent, 
    /// and we need the unselected opposite sphere to physically update its position.
    /// </summary>
    void SyncSpherePositions()
    {
        foreach (var sphereGroup in SPD.controlSphereGroup)
        {
            if (sphereGroup.sphereObject == null) continue;
            
            var spline = sphereGroup.sphereObject.transform.parent.parent.GetComponent<BezierSpline>();
            if (spline != null)
            {
                // Retrieve the updated local coordinate from the array, convert to World Space, and snap the visual sphere
                Vector3 correctWorldPos = spline.transform.TransformPoint(spline.points[sphereGroup.index]);
                sphereGroup.sphereObject.transform.position = correctWorldPos;
            }
        }
    }
}
#endregion