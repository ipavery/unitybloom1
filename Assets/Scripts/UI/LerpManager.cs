using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Needed for reading mouse position

[System.Serializable]
public class LerpConnectionVisual
{
    public BezierSpline source;
    public BezierSpline target;
    public LineRenderer lineRenderer;
}

public class LerpManager : MonoBehaviour
{
    [Header("Dependencies")]
    public Button connectButton;
    public CurveEditorUI curveEditorUI;
    public ParticleManager particleManager; // Used to rebuild visuals on load

    [Header("Visual Connections")]
    public Material scrollLineMaterial; 
    public float scrollSpeed = 1f;
    public float arrowWorldLength = 1.5f; // Adjust to space out arrows
    public float lineWidth = 0.1f;
    private Color tetherColor = new Color(1.2f, 1.2f, 1.2f, 0.6f); // HDR color for bloom/opacity
    
    // State Tracking
    private List<LerpConnectionVisual> connectionVisuals = new List<LerpConnectionVisual>();
    private bool isConnecting = false;
    private BezierSpline connectingSource;
    private LineRenderer activeMouseLine; // The temporary line drawn to the mouse
    
    void OnEnable()
    {
        EventHub.Subscribe<EnterExitLerping>(OnEnterExitLerping);
        EventHub.Subscribe<LerpConnectionMade>(OnLerpConnectionMade);
        EventHub.Subscribe<ReloadLerpUI>(OnReloadLerpUI); // Subscribed for save loading
        EventHub.Subscribe<HideShow3DUI>(OnHideShow3DUI);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<EnterExitLerping>(OnEnterExitLerping);
        EventHub.Unsubscribe<LerpConnectionMade>(OnLerpConnectionMade);
        EventHub.Unsubscribe<ReloadLerpUI>(OnReloadLerpUI);
        EventHub.Unsubscribe<HideShow3DUI>(OnHideShow3DUI);
    }

    void Start()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(() => EventHub.Publish(new EnterExitLerping(true)));
    }

    void Update()
    {
        // 1. Update Established Connections
        for (int i = connectionVisuals.Count - 1; i >= 0; i--)
        {
            var visual = connectionVisuals[i];
            
            // Cleanup if a spline was completely deleted by the user
            if (visual.source == null || visual.target == null)
            {
                if (visual.lineRenderer != null) Destroy(visual.lineRenderer.gameObject);
                connectionVisuals.RemoveAt(i);
                continue;
            }

            // --- THE NEW VALIDATION CHECK ---
            // If points were deleted (or added) to either spline, break the connection
            if (visual.source.points.Length != visual.target.points.Length)
            {
                Debug.LogWarning($"Connection broken between {visual.source.name} and {visual.target.name} due to point count mismatch.");
                
                // Reset the source spline's lerp target back to itself to prevent errors
                visual.source.lerpSpline = visual.source;
                
                // Destroy the visual line and remove from the list
                if (visual.lineRenderer != null) Destroy(visual.lineRenderer.gameObject);
                connectionVisuals.RemoveAt(i);
                continue;
            }

            // Update positions and scroll math (adding points[0] is used to make it start at first spline point)
            UpdateLineVisuals(visual.lineRenderer, visual.source.transform.position+visual.source.points[0], visual.target.transform.position+visual.target.points[0]);
        }

        // 2. Update Mouse Tether if connecting
        if (isConnecting && connectingSource != null && activeMouseLine != null)
        {
            Vector3 sourcePos = connectingSource.transform.position+connectingSource.points[0];
            
            // Cast a ray from the mouse to a mathematical plane facing the camera at the source's depth
            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            Plane plane = new Plane(Camera.main.transform.forward, sourcePos);
            
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 mouseWorldPos = ray.GetPoint(enter);
                UpdateLineVisuals(activeMouseLine, sourcePos, mouseWorldPos);
            }
        }
    }

    /// <summary>
    /// Helper method to apply spacing and scrolling math to any line
    /// </summary>
    void UpdateLineVisuals(LineRenderer lr, Vector3 startPos, Vector3 endPos)
    {
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, endPos);

        float distance = Vector3.Distance(startPos, endPos);
        
        // Prevent division by zero if points are perfectly stacked
        if (distance <= 0.01f) return; 

        // Tiling Math
        float repeats = distance / arrowWorldLength;
        lr.material.mainTextureScale = new Vector2(repeats, 1f);

        // Scrolling Math
        float offset = (Time.time * scrollSpeed) / arrowWorldLength;
        lr.material.mainTextureOffset = new Vector2(-offset, 0); 
    }

    // --- EVENT HANDLERS ---

    void OnEnterExitLerping(EnterExitLerping e)
    {
        isConnecting = e.isConnecting;

        if (isConnecting)
        {
            connectingSource = curveEditorUI.selectedObject;
            if (connectingSource == null) return; // Failsafe

            // Initialize or enable the temporary mouse line
            if (activeMouseLine == null)
            {
                GameObject lineObj = new GameObject("MouseLerpVisual");
                lineObj.transform.SetParent(this.transform); 
                activeMouseLine = SetupLineRenderer(lineObj);
            }
            activeMouseLine.gameObject.SetActive(true);
        }
        else
        {
            connectingSource = null;
            if (activeMouseLine != null) activeMouseLine.gameObject.SetActive(false);
        }
    }
    
    void OnLerpConnectionMade(LerpConnectionMade e)
    {
        if (connectingSource == null) return;
        
        if (connectingSource.Guid == e.splineClicked.Guid)
        {
            Debug.LogWarning("Cannot lerp a spline to itself.");
        }
        else if (connectingSource.points.Length != e.splineClicked.points.Length)
        {
            Debug.LogWarning("Spline point arrays must be the same length.");
        } 
        else
        {
            UndoManager.Instance.RecordState(); // Remember to record state before modifying!
            
            // Connect the data
            connectingSource.lerpSpline = e.splineClicked;

            // Connect the visuals
            CreateVisualLine(connectingSource, e.splineClicked);

            SaveLoadUI.Instance.NotifyActionPerformed();
        }
        
        // Always exit connection mode after a click, whether successful or invalid
        EventHub.Publish(new EnterExitLerping(false));
    }

    void OnReloadLerpUI(ReloadLerpUI e)
    {
        // 1. Wipe out existing visuals
        foreach (var visual in connectionVisuals)
        {
            if (visual.lineRenderer != null) Destroy(visual.lineRenderer.gameObject);
        }
        connectionVisuals.Clear();

        // 2. Rebuild all connections from the loaded save data
        if (particleManager != null)
        {
            foreach (var group in particleManager.splineParticleGroup)
            {
                BezierSpline src = group.spline;
                // If it has a target, and it's not targeting itself (self-targeting is often a default fallback)
                if (src != null && src.lerpSpline != null && src.lerpSpline != src)
                {
                    CreateVisualLine(src, src.lerpSpline);
                }
            }
        }
    }
    
    void OnHideShow3DUI(HideShow3DUI e)
    {
        // 1. Toggle all established connection lines
        foreach (var visual in connectionVisuals)
        {
            if (visual.lineRenderer != null)
            {
                // If e.hide is true, enabled becomes false. 
                // If e.hide is false, enabled becomes true.
                visual.lineRenderer.enabled = !e.hide; 
            }
        }

        // 2. Toggle the temporary mouse tether line (if it exists)
        if (activeMouseLine != null)
        {
            activeMouseLine.enabled = !e.hide;
        }
    }
    
    // --- VISUAL SETUP ---

    void CreateVisualLine(BezierSpline source, BezierSpline target)
    {
        // 1. Enforce "One Target Per Spline": Destroy any existing line originating from this source
        int existingIndex = connectionVisuals.FindIndex(v => v.source == source);
        if (existingIndex >= 0)
        {
            if (connectionVisuals[existingIndex].lineRenderer != null) 
                Destroy(connectionVisuals[existingIndex].lineRenderer.gameObject);
            
            connectionVisuals.RemoveAt(existingIndex);
        }

        // 2. Create the new Line
        GameObject lineObj = new GameObject($"LerpVisual_{source.name}_to_{target.name}");
        lineObj.transform.SetParent(this.transform); 
        
        LineRenderer lr = SetupLineRenderer(lineObj);

        connectionVisuals.Add(new LerpConnectionVisual 
        { 
            source = source, 
            target = target, 
            lineRenderer = lr 
        });
    }

    LineRenderer SetupLineRenderer(GameObject obj)
    {
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.material = scrollLineMaterial;
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = tetherColor;
        lr.endColor = tetherColor;
        lr.textureMode = LineTextureMode.Tile; 
        lr.generateLightingData = false;
        return lr;
    }
}