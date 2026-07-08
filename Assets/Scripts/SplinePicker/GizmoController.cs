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
    private Vector3 gizmoPrefabScale;
    private Vector3 planarGizmoPrefabScale;
    private float gizmoOffsetDistance = .15f;
    public float gizmoScale = .1f;
    private List<GameObject> gizmoList = new();

    private int activeGizmoAxis = -1;
    private Plane dragPlane;
    private float distance;
    private Vector3 initialOffset;

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
    }

    void Update()
    {
        if (SPD.lastHighlighted != null)
        {
            distance = Vector3.Distance(Camera.main.transform.position, SPD.lastHighlighted.transform.position);
        }

        foreach (var gizmo in gizmoList)
        {
            if (gizmo != null && SPD.lastHighlighted != null)
            {
                if (gizmo.name.Contains("MoveGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * gizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + gizmo.transform.up * gizmoOffsetDistance * distance;
                }
                else if (gizmo.name.Contains("PlanarGizmoPrefab"))
                {
                    gizmo.transform.localScale = distance * gizmoScale * planarGizmoPrefabScale;
                    gizmo.transform.position = SPD.lastHighlighted.transform.position + (gizmo.transform.up + gizmo.transform.right * .5f) * gizmoOffsetDistance * distance;
                }
            }
        }

        if (activeGizmoAxis != -1 && SPD.lastHighlighted != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            SetDragPlane();
            FindGizmoAxisHitPoint(out Vector3 newControlPos, ray, dragPlane, activeGizmoAxis, SPD.lastHighlighted.transform.position);

            Vector3 lastHighlightedPos = SPD.lastHighlighted.transform.position;
            foreach (var sphereGroup in SPD.controlSphereGroup)
            {
                if (!sphereGroup.isSelected)
                {
                    continue;
                }

                var sphere = sphereGroup.sphereObject;
                Vector3 diff = sphere.transform.position - lastHighlightedPos;
                Vector3 offset = newControlPos - lastHighlightedPos - initialOffset;
                sphere.transform.position = lastHighlightedPos + diff + offset;

                var spline = sphere.transform.parent.parent.GetComponent<BezierSpline>();
                spline.points[sphereGroup.index] += offset;
            }
        }
    }

    void OnShowMoveGizmos(ShowMoveGizmosEvent e)
    {
        ShowMoveGizmos(e.position);
    }

    void OnGizmoDragStarted(GizmoDragStarted e)
    {
        if (SPD.lastHighlighted == null)
        {
            return;
        }

        activeGizmoAxis = SPD.activeGizmoAxis;
        initialOffset = e.position - SPD.lastHighlighted.transform.position;
    }

    void OnGizmoDragEnded(GizmoDragEnded e)
    {
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
    }

    void OnClearSelection(ClearSelection e)
    {
        DestroyGizmos();
        activeGizmoAxis = -1;
        SPD.activeGizmoAxis = -1;
        initialOffset = Vector3.zero;
    }

    void ShowMoveGizmos(Vector3 position)
    {
        DestroyGizmos();

        for (int i = 0; i < 3; i++)
        {
            GameObject moveGizmo = Instantiate(moveGizmoPrefab, position, Quaternion.identity);
            gizmoList.Add(moveGizmo);
            moveGizmo.transform.localScale *= gizmoScale;
            moveGizmo.transform.SetParent(transform, false);
            moveGizmo.layer = LayerMask.NameToLayer("PP Layer");
            Renderer rend = moveGizmo.GetComponent<Renderer>();

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

            if (i == 0)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                rend.material.color = Color.red;
                rend.material.SetColor("_EmissionColor", Color.red * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 0, -90);
                planarRend.material.color = Color.red;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);
            }
            else if (i == 1)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                rend.material.color = Color.green;
                rend.material.SetColor("_EmissionColor", Color.green * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(0, 90, 0);
                planarRend.material.color = Color.green;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);
            }
            else if (i == 2)
            {
                moveGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                rend.material.color = Color.blue;
                rend.material.SetColor("_EmissionColor", Color.blue * SPD.gizmoEmissionIntensity);

                planarGizmo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                planarRend.material.color = Color.blue;
                planarRend.material.SetColor("_EmissionColor", planarRend.material.color * SPD.gizmoEmissionIntensity);
            }

            moveGizmo.transform.position = position + moveGizmo.transform.up * 3f;
            planarGizmo.transform.position = position + planarGizmo.transform.up * 2f + planarGizmo.transform.right * 2f;
        }

        gizmoList = new List<GameObject>
        {
            gizmoList[0], // X-axis
            gizmoList[2], // Y-axis
            gizmoList[4], // Z-axis
            gizmoList[5],
            gizmoList[3],
            gizmoList[1]
        };
    }

    void FindGizmoAxisHitPoint(out Vector3 intersection, Ray ray, Plane dragPlane, int gizmoAxisDir, Vector3 controlPointPosition)
    {
        intersection = Vector3.zero;
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
        else if (gizmoAxisDir == 3)
        {
            intersection.y = controlPointPosition.y;
        }
        else if (gizmoAxisDir == 4)
        {
            intersection.x = controlPointPosition.x;
        }
        else if (gizmoAxisDir == 5)
        {
            intersection.z = controlPointPosition.z;
        }
    }

    void SetDragPlane()
    {
        if (activeGizmoAxis == 0)
        {
            dragPlane = new Plane(Vector3.up, SPD.lastHighlighted.transform.position);
        }
        else if (activeGizmoAxis == 1)
        {
            dragPlane = new Plane(Vector3.right, SPD.lastHighlighted.transform.position);
        }
        else if (activeGizmoAxis == 2)
        {
            dragPlane = new Plane(Vector3.forward, SPD.lastHighlighted.transform.position);
        }
        else if (activeGizmoAxis == 3)
        {
            dragPlane = new Plane(Vector3.up, SPD.lastHighlighted.transform.position);
        }
        else if (activeGizmoAxis == 4)
        {
            dragPlane = new Plane(Vector3.right, SPD.lastHighlighted.transform.position);
        }
        else if (activeGizmoAxis == 5)
        {
            dragPlane = new Plane(Vector3.forward, SPD.lastHighlighted.transform.position);
        }
    }

    public bool TryGetGizmoAxisIndex(GameObject candidate, out int axisIndex)
    {
        for (int i = 0; i < gizmoList.Count; i++)
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
        foreach (var gizmo in gizmoList)
        {
            Destroy(gizmo);
        }
        gizmoList.Clear();
    }
}
