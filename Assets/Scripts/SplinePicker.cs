using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SplinePicker : MonoBehaviour
{
    [Header("Spline Settings")]
    public int pointsPerSpline = 32;
    public Material lineMaterial;
    public float lineWidth = 0.01f;
    public GameObject controlPointSpherePrefab;
    public GameObject moveGizmoPrefab; // Assign a gizmo prefab in the inspector
    public float gizmoScale = .5f; // Scale for gizmos
    public Color highlightColor = Color.yellow;
    public float gizmoEmissionIntensity = 0.7f; // Emission intensity for gizmos

    private List<GameObject> controlSpheres = new List<GameObject>();
    private List<GameObject> gizmoList = new List<GameObject>();
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
                    if (lastHighlighted != null)
                    {
                        var rend = lastHighlighted.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            rend.material.color = lastOriginalColor;
                            rend.material.SetColor("_EmissionColor", lastOriginalColor * gizmoEmissionIntensity);
                        }
                    }

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
            }
        }
    }

    void Start()
    {
        BezierSpline[] splineList = FindObjectsByType<BezierSpline>(FindObjectsSortMode.None);

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

    }

    void ShowMoveGizmos(Vector3 position)
    {
        // Clear existing gizmos
        foreach (var gizmo in gizmoList)
        {
            Destroy(gizmo);
        }
        gizmoList.Clear();

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
            }
            else if (i == 1)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0); // Right
                rend.material.color = Color.green;
                rend.material.SetColor("_EmissionColor", Color.green * gizmoEmissionIntensity);
            }
            else if (i == 2)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0); // Up
                rend.material.color = Color.blue;
                rend.material.SetColor("_EmissionColor", Color.blue * gizmoEmissionIntensity);
            }

            moveGizmo.transform.position = position + moveGizmo.transform.up * 3f; // Add control point position and offset
            
        }

        
    }
}