using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplinePicker : MonoBehaviour
{
    [Header("Spline Settings")]
    public int pointsPerSpline = 32;
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public GameObject controlPointSpherePrefab;

    [Header("Gizmo Settings")]
    public GameObject moveGizmoPrefab; // Assign a gizmo prefab in the inspector
    public GameObject planarGizmoPrefab; // Assign a planar gizmo prefab in the inspector
    Vector3 gizmoPrefabScale; // Store the original scale of the gizmo prefab
    Vector3 planarGizmoPrefabScale; // Store the original scale of the planar gizmo prefab
    private float gizmoOffsetDistance = .15f; // Offset distance for gizmos
    public float gizmoScale = .5f; // Scale for gizmos
    public Color highlightColor = Color.yellow;
    public float gizmoEmissionIntensity = 0.5f; // Emission intensity for gizmos
    private List<GameObject> gizmoList = new();
    private List<GameObject> controlSpheres = new List<GameObject>();

    // Dragging gizmos
    private int activeGizmoAxis = -1; // 0=X, 1=Y, 2=Z
    private Vector3 dragStartPoint;
    Plane dragPlane;


    private List<int> controlIndices = new List<int>();
    private List<Color> originalColors = new List<Color>();
    private GameObject lastHighlighted = null;
    private Color lastOriginalColor;

    //input variables
    public PlayerInputActions playerControls;
    private InputAction fire;


    void Awake()
    {
        playerControls = new PlayerInputActions();
        playerControls.Player.Fire.canceled += ctx => CancelFire();
    }

    void OnEnable()
    {
        playerControls.Enable();
        fire = playerControls.Player.Fire;
        fire.Enable();
        fire.performed += Fire;
    }

    void OnDisable()
    {
        fire.Disable();
    }

    void CancelFire()
    {
        activeGizmoAxis = -1; // Reset the active gizmo axis
        if (lastHighlighted != null)
        {
            EventHub.Publish(new GizmoDragEnded(lastHighlighted, lastHighlighted.transform.position));
        }
        
    }

    void Fire(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                int idx = controlSpheres.IndexOf(hit.collider.gameObject);
                if (idx != -1)
                {
                    // Un-highlight previous
                    UnHighlightLast();

                    // Highlight new
                    var rendNew = hit.collider.GetComponent<Renderer>();
                    if (rendNew != null)
                    {
                        lastHighlighted = hit.collider.gameObject;
                        lastOriginalColor = originalColors[idx];
                        rendNew.material.color = highlightColor;
                        rendNew.material.SetColor("_EmissionColor", highlightColor * gizmoEmissionIntensity);
                        ShowMoveGizmos(lastHighlighted.transform.position); // Show move gizmos at the highlighted control point
                    }

                    Debug.Log("Clicked control point: " + controlIndices[idx]);
                }

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
                // If no control point or gizmo was clicked, un-highlight the last highlighted control point
                UnHighlightLast();
                DestroyGizmos(); // Clear existing gizmos
                activeGizmoAxis = -1; // Reset the active gizmo axis
            }
        }

    }

    void Start()
    {
        BezierSpline[] splineList = FindObjectsByType<BezierSpline>(FindObjectsSortMode.None);
        gizmoPrefabScale = moveGizmoPrefab.transform.localScale;
        planarGizmoPrefabScale = planarGizmoPrefab.transform.localScale;

        foreach (var spline in splineList)
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

                controlSpheres.Add(sphere);
                controlIndices.Add(i);

                // Set and store original color as white
                var rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = Color.white;
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", Color.white * gizmoEmissionIntensity);
                    originalColors.Add(Color.white);
                }
                else
                {
                    originalColors.Add(Color.white);
                }
            }
        }
    }

    void Update()
    {
        foreach (var gizmo in gizmoList)
        {
            float distance = Vector3.Distance(Camera.main.transform.position, lastHighlighted.transform.position);
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
                    gizmo.transform.position = lastHighlighted.transform.position + (gizmo.transform.up+gizmo.transform.right*.5f) * gizmoOffsetDistance * distance; // Offset the gizmo position
                }
            }
        }
        if (lastHighlighted != null)
        {
            //float distance = Vector3.Distance(Camera.main.transform.position, lastHighlighted.transform.position);
            //lastHighlighted.transform.localScale = .6f * distance * gizmoScale * Vector3.one;
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
            Vector3 newControlPos;
            FindGizmoAxisHitPoint(out newControlPos, ray, dragPlane, activeGizmoAxis, lastHighlighted.transform.position);

            lastHighlighted.transform.position = newControlPos;
            //update spline data
            var spline = lastHighlighted.transform.parent.parent.GetComponent<BezierSpline>();
            int cpIdx = controlIndices[controlSpheres.IndexOf(lastHighlighted)];
            spline.points[cpIdx] = -lastHighlighted.transform.parent.parent.transform.position + newControlPos;

            // --- Update the LineRenderer for this spline ---
            // Find the LineRenderer (assumes it's on a child of the spline GameObject)
            LineRenderer lr = spline.GetComponentInChildren<LineRenderer>();
            if (lr != null)
            {
                for (int i = 0; i <= pointsPerSpline; i++)
                {
                    float t = i / (float)pointsPerSpline;
                    lr.SetPosition(i, spline.GetPoint(t));
                }
            }
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
                rend.material.color = lastOriginalColor;
                rend.material.SetColor("_EmissionColor", lastOriginalColor * gizmoEmissionIntensity);
            }
            lastHighlighted.transform.localScale = Vector3.one; // Reset scale
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