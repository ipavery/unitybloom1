using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Helper class to hold our visual tether data
[System.Serializable]
public class LerpConnectionVisual
{
    public BezierSpline source;
    public BezierSpline target;
    public LineRenderer lineRenderer;
}

public class LerpManager : MonoBehaviour
{
    [Header("UI References")]
    public Button connectButton;
    public CurveEditorUI curveEditorUI;

    [Header("Visual Connections")]
    public Material scrollLineMaterial; // Assign a material with your arrow texture here
    public float scrollSpeed = .1f;
    public float lineWidth = 0.1f;
    public float arrowWorldLength = 3f;
    
    private BezierSpline firstSelectedSpline;
    
    // Track all active visual connections
    private List<LerpConnectionVisual> connectionVisuals = new List<LerpConnectionVisual>();
    
    void OnEnable()
    {
        EventHub.Subscribe<EnterExitLerping>(OnEnterExitLerping);
        EventHub.Subscribe<LerpConnectionMade>(OnLerpConnectionMade);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<EnterExitLerping>(OnEnterExitLerping);
        EventHub.Unsubscribe<LerpConnectionMade>(OnLerpConnectionMade);
    }

    void Update()
    {
        for (int i = connectionVisuals.Count - 1; i >= 0; i--)
        {
            var visual = connectionVisuals[i];
            
            if (visual.source == null || visual.target == null)
            {
                if (visual.lineRenderer != null) Destroy(visual.lineRenderer.gameObject);
                connectionVisuals.RemoveAt(i);
                continue;
            }

            visual.lineRenderer.SetPosition(0, visual.source.transform.position);
            visual.lineRenderer.SetPosition(1, visual.target.transform.position);

            float distance = Vector3.Distance(visual.source.transform.position, visual.target.transform.position);
            
            // --- THE NEW SPACING MATH ---
            // Determine how long ONE arrow should be in world space (e.g., 1.5 meters).
            // Increase this number to space the arrows further apart.
            
            
            float repeats = distance / arrowWorldLength;
            visual.lineRenderer.material.mainTextureScale = new Vector2(repeats, 1f);

            // --- THE NEW SPEED MATH ---
            // By dividing by arrowWorldLength, a scrollSpeed of 1.0 means it moves exactly 1 unit per second in world space.
            float offset = (Time.time * scrollSpeed) / arrowWorldLength; 
            visual.lineRenderer.material.mainTextureOffset = new Vector2(-offset, 0); 
        }
    }

    void OnEnterExitLerping(EnterExitLerping e)
    {
        if (e.isConnecting)
        {
            Debug.Log("now starting connection");
        }
        else
        {
            Debug.Log("ending connection");
        }
    }
    
    void OnLerpConnectionMade(LerpConnectionMade e)
    {
        firstSelectedSpline = curveEditorUI.selectedObject; // Note: Depending on your multiselect logic, you might want curveEditorUI.selectedSplines[0]
        
        if (firstSelectedSpline.Guid == e.splineClicked.Guid)
        {
            Debug.Log("can't lerp a spline to itself");
        }
        else if (firstSelectedSpline.points.Length != e.splineClicked.points.Length)
        {
            Debug.Log("spline points arrays must be same length");
        } 
        else
        {
            firstSelectedSpline.lerpSpline = e.splineClicked;
            CreateVisualLine(firstSelectedSpline, e.splineClicked);
        }
        
        EventHub.Publish(new EnterExitLerping(false));
    } 
    
    void CreateVisualLine(BezierSpline source, BezierSpline target)
    {
        var existing = connectionVisuals.Find(v => v.source == source && v.target == target);
        if (existing != null) return;

        GameObject lineObj = new GameObject($"LerpVisual_{source.name}_to_{target.name}");
        lineObj.transform.SetParent(this.transform); 
        
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = scrollLineMaterial;
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        
        // --- SET THE COLOR HERE ---
        // Color(Red, Green, Blue, Alpha)
        // 0.5f is medium gray (stops the bloom). 0.4f is 40% opacity (makes it see-through).
        Color tetherColor = new Color(.7f, .7f, .7f, 1f); 
        lr.startColor = tetherColor;
        lr.endColor = tetherColor;
        
        lr.textureMode = LineTextureMode.Tile; 
        lr.generateLightingData = false;

        connectionVisuals.Add(new LerpConnectionVisual 
        { 
            source = source, 
            target = target, 
            lineRenderer = lr 
        });
    }

    void Start()
    {
        connectButton.onClick.AddListener(() => EventHub.Publish(new EnterExitLerping(true)));
    }
}