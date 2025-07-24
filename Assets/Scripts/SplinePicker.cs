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
    public Color highlightColor = Color.yellow;

    private List<GameObject> controlSpheres = new List<GameObject>();
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
                            rend.material.SetColor("_EmissionColor", lastOriginalColor * .7f);
                        }
                    }

                    // Highlight new
                    var rendNew = hit.collider.GetComponent<Renderer>();
                    if (rendNew != null)
                    {
                        lastHighlighted = hit.collider.gameObject;
                        lastOriginalColor = originalColors[idx];
                        rendNew.material.color = highlightColor;
                        rendNew.material.SetColor("_EmissionColor", highlightColor * .7f);
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
                    rend.material.SetColor("_EmissionColor", Color.white * .7f);
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
}