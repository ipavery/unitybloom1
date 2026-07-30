using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BezierSpline))]
public class DrawSpline : MonoBehaviour
{
    [Header("Actions")]
    public Button drawSplineButton;

    [Header("Line Rendering (Live Feedback)")]
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    private LineRenderer lineRenderer;

    [Header("Spline Generation Settings")]
    public BezierSpline splinePrefab;
    
    [Tooltip("Minimum distance mouse must move to capture a new raw point.")]
    public float pointCaptureDistance = 0.1f;
    
    [Tooltip("Higher = fewer points, less accurate. Lower = more points, highly accurate.")]
    public float errorThreshold = 0.5f;

    [Header("Input & Planes")]
    public Plane drawPlane = new Plane(Vector3.forward, Vector3.zero);

    [Header("Cursor Settings")]
    public Texture2D cursorTexture;
    public Vector2 cursorHotspot = new Vector2(0f, 0f);
    private Texture2D whiteCursorTexture;
    private bool cursorIsCustom = false;
    
    // State variables
    private PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;
    private bool drawingSpline;
    
    private List<Vector3> rawPoints = new List<Vector3>();

    void Awake()
    {
        playerControls = new PlayerInputActions();
        mousePosition = playerControls.Player.MousePosition;
        select = playerControls.Player.Select;
        
        select.started += OnSelectStart;
        select.canceled += OnSelectEnd;

        // Initialize LineRenderer dynamically if needed
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Configure LineRenderer
        if (lineMaterial != null) lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
    }

    void OnEnable()
    {
        playerControls.Enable();
        mousePosition.Enable();
        select.Enable();
    }

    void OnDisable()
    {
        mousePosition.Disable();
        select.Disable();
        if (cursorIsCustom)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            cursorIsCustom = false;
        }
    }

    void Start()
    {
        if (drawSplineButton != null)
            drawSplineButton.onClick.AddListener(OnDrawSpline);

        if (cursorTexture != null)
        {
            CreateWhiteCursor();
        }

        UpdateToggleButtonState();
    }

    void OnDrawSpline()
    {
        InputBlocker.inputBlockOverride = !InputBlocker.inputBlockOverride;
        UpdateToggleButtonState();
    }

    private void CreateWhiteCursor()
    {
        if (cursorTexture == null) return;
        try
        {
            whiteCursorTexture = new Texture2D(cursorTexture.width, cursorTexture.height, TextureFormat.RGBA32, false);
            Color[] pixels = cursorTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, pixels[i].a);
            }
            whiteCursorTexture.SetPixels(pixels);
            whiteCursorTexture.Apply();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create white cursor: " + e.Message);
        }
    }

    private void UpdateToggleButtonState()
    {
        bool isActive = InputBlocker.inputBlockOverride;

        if (isActive)
        {
            if (whiteCursorTexture != null)
            {
                Cursor.SetCursor(whiteCursorTexture, cursorHotspot, CursorMode.Auto);
                cursorIsCustom = true;
            }
            else if (cursorTexture != null)
            {
                CreateWhiteCursor();
                if (whiteCursorTexture != null)
                {
                    Cursor.SetCursor(whiteCursorTexture, cursorHotspot, CursorMode.Auto);
                    cursorIsCustom = true;
                }
            }
        }
        else
        {
            if (cursorIsCustom)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                cursorIsCustom = false;
            }
        }

        if (drawSplineButton != null)
        {
            var cb = drawSplineButton.colors;
            if (isActive)
            {
                cb.normalColor = new Color(2.5f, 2.5f, 2.5f, 1f);
                cb.highlightedColor = new Color(2.2f, 2.2f, 2.2f, 1f);
                cb.selectedColor = new Color(2.5f, 2.5f, 2.5f, 1f);
                cb.pressedColor = new Color(1.8f, 1.8f, 1.8f, 1f);
            }
            else
            {
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                cb.selectedColor = Color.white;
                cb.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            }
            drawSplineButton.colors = cb;
        }
    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        if (InputBlocker.inputBlockOverride)
        {                        
            drawingSpline = true;
            rawPoints.Clear();
            lineRenderer.positionCount = 0;
                         
            // FIX: Read from the unified Pointer to support both Mouse and Touch
            Vector2 pointerPos2D = Pointer.current.position.ReadValue();
            AddRawPoint(ProjectToPlane(pointerPos2D));
        }
    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (InputBlocker.inputBlockOverride && drawingSpline)
        {
            drawingSpline = false;
            FinishDrawing();
        }
    }

    void Update()
    {
        if (drawingSpline)
        {
            ContinueDrawing();
        }
    }

    void ContinueDrawing()
    {
        // FIX: Read from the unified Pointer to support both Mouse and Touch
        Vector2 pointerPos2D = Pointer.current.position.ReadValue();
        Vector3 currentPos3D = ProjectToPlane(pointerPos2D);
        
        if (rawPoints.Count == 0 || Vector3.Distance(currentPos3D, rawPoints[rawPoints.Count - 1]) > pointCaptureDistance)
        {
            AddRawPoint(currentPos3D);
        }
    }

    void AddRawPoint(Vector3 point)
    {
        rawPoints.Add(point);
        lineRenderer.positionCount = rawPoints.Count;
        lineRenderer.SetPosition(rawPoints.Count - 1, point);
    }

    void FinishDrawing()
    {
        if (rawPoints.Count < 2)
        {
            lineRenderer.positionCount = 0;
            return;
        }
        
        UndoManager.Instance.RecordState(); // Record the state before creating a new spline for undo functionality

        // 1. Spatially filter the dense mouse points using RDP
        List<Vector3> simplifiedKnots = RamerDouglasPeucker(rawPoints, errorThreshold);

        // 2. Instantiate the actual spline object at the first point
        BezierSpline newSpline = Instantiate(splinePrefab, simplifiedKnots[0], Quaternion.identity);

        // 3. Generate Bezier Control Points using Catmull-Rom derivation
        GenerateBezierFromKnots(newSpline, simplifiedKnots);

        // 4. Notify system
        EventHub.Publish(new NewSplineCreated(newSpline));
        EventHub.Publish(new ReloadParticles(true)); // reload particle system

        SaveLoadUI.Instance.NotifyActionPerformed();
        
        // Hide the temporary drawing line
        lineRenderer.positionCount = 0; 
    }

    private void GenerateBezierFromKnots(BezierSpline spline, List<Vector3> knots)
    {
        if (knots.Count < 2) return;

        List<Vector3> bezierPoints = new List<Vector3>();
        
        // Bezier points are local to the transform position. 
        // Since we instantiated the spline at knots[0], we subtract it from everything.
        Vector3 offset = spline.transform.position; 
        
        bezierPoints.Add(knots[0] - offset); // Local Zero

        for (int i = 0; i < knots.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? knots[0] - (knots[1] - knots[0]) : knots[i - 1];
            Vector3 p1 = knots[i];
            Vector3 p2 = knots[i + 1];
            Vector3 p3 = (i + 1 == knots.Count - 1) ? knots[i + 1] + (knots[i + 1] - knots[i]) : knots[i + 2];

            // Catmull-Rom finite difference derivatives for tangents
            // Divide by 3 to ensure tangent length creates smooth circular curves between points
            Vector3 t1 = p1 + (p2 - p0) / 6f;
            Vector3 t2 = p2 - (p3 - p1) / 6f;

            bezierPoints.Add(t1 - offset);
            bezierPoints.Add(t2 - offset);
            bezierPoints.Add(p2 - offset);
        }

        // Apply the generated array to the spline logic
        spline.ResetWithPoints(bezierPoints.ToArray());
    }

    // --- Recursive Error Minimization (Ramer-Douglas-Peucker Algorithm) ---
    private List<Vector3> RamerDouglasPeucker(List<Vector3> points, float epsilon)
    {
        if (points.Count < 3) return new List<Vector3>(points);

        float maxDistance = 0f;
        int index = 0;
        int end = points.Count - 1;

        for (int i = 1; i < end; i++)
        {
            float distance = PerpendicularDistance(points[i], points[0], points[end]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                index = i;
            }
        }

        List<Vector3> result = new List<Vector3>();
        if (maxDistance > epsilon)
        {
            List<Vector3> leftRecursive = RamerDouglasPeucker(points.GetRange(0, index + 1), epsilon);
            List<Vector3> rightRecursive = RamerDouglasPeucker(points.GetRange(index, points.Count - index), epsilon);

            leftRecursive.RemoveAt(leftRecursive.Count - 1);
            result.AddRange(leftRecursive);
            result.AddRange(rightRecursive);
        }
        else
        {
            result.Add(points[0]);
            result.Add(points[end]);
        }
        return result;
    }

    private float PerpendicularDistance(Vector3 pt, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = lineEnd - lineStart;
        if (lineDir.sqrMagnitude == 0f) return Vector3.Distance(pt, lineStart);
        float t = Vector3.Dot(pt - lineStart, lineDir) / lineDir.sqrMagnitude;
        Vector3 projection = lineStart + Mathf.Clamp01(t) * lineDir;
        return Vector3.Distance(pt, projection);
    }

    // --- Utilities ---
    private Vector3 ProjectToPlane(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (drawPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return Vector3.zero;
    }
}