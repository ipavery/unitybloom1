using UnityEngine;
using System.Collections.Generic;

public class RuntimeLineDrawer : MonoBehaviour
{
    private static RuntimeLineDrawer _instance;

    [Header("Pool Settings")]
    [SerializeField] private int maxPoolSize = 200; // absolute cap for active+pooled LineRenderers
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private string defaultLayer = "Default";

    private Queue<LineRenderer> pool = new Queue<LineRenderer>();
    private List<LineData> activeLines = new List<LineData>(); // active + recently-created lines
    private List<LineRequest> frameRequests = new List<LineRequest>(); // requests collected during the frame

    private class LineData
    {
        public LineRenderer lr;
        public float endTime; // 0 = never expire (but we avoid that by default)
    }

    private struct LineRequest
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;
        public float width;
        public float duration;
        public string layerName;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (defaultMaterial == null)
                defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Public API: Draw a line. If duration <= 0, it will be treated as a one-frame line (auto-recycled next Update).
    /// If duration > 0, it will persist until Time.time >= Time.time + duration.
    /// </summary>
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float width = 0.02f, float duration = 0f, string layerName = null)
    {
        if (_instance == null)
        {
            var go = new GameObject("RuntimeLineDrawer");
            _instance = go.AddComponent<RuntimeLineDrawer>();
        }

        if (layerName == null) layerName = _instance.defaultLayer;

        _instance.frameRequests.Add(new LineRequest
        {
            start = start,
            end = end,
            color = color,
            width = width,
            duration = duration,
            layerName = layerName
        });
    }

    private void Update()
    {
        float time = Time.time;

        // Recycle expired lines (endTime > 0)
        for (int i = activeLines.Count - 1; i >= 0; i--)
        {
            var d = activeLines[i];
            if (d.endTime > 0f && time >= d.endTime)
            {
                RecycleLine(d.lr);
                activeLines.RemoveAt(i);
            }
        }

        // Now process frame requests after recycling expired ones (keeps pressure down)
        if (frameRequests.Count > 0)
        {
            ProcessFrameRequests();
            frameRequests.Clear();
        }
    }

    private void ProcessFrameRequests()
    {
        // For each request, get a LineRenderer and add it to activeLines with its endTime
        float now = Time.time;

        for (int i = 0; i < frameRequests.Count; i++)
        {
            var r = frameRequests[i];

            // If duration <= 0 treat as one-frame: set very short expiry (next Update will recycle)
            float endTime = r.duration > 0f ? now + r.duration : now + Time.deltaTime;

            var lr = GetLineRenderer(); // will reuse or create while respecting maxPoolSize
            SetupLineRenderer(lr, r.start, r.end, r.color, r.width, r.layerName);

            activeLines.Add(new LineData { lr = lr, endTime = endTime });
        }
    }

    private void SetupLineRenderer(LineRenderer lr, Vector3 start, Vector3 end, Color color, float width, string layerName)
    {
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) lr.gameObject.layer = layer;
        else lr.gameObject.layer = LayerMask.NameToLayer(defaultLayer);
    }

    private LineRenderer GetLineRenderer()
    {
        // If there's something in pool, reuse it
        if (pool.Count > 0)
        {
            var lr = pool.Dequeue();
            lr.gameObject.SetActive(true);
            return lr;
        }

        // If total objects would exceed cap, forcibly recycle an active one (choose one with farthest endTime)
        if (activeLines.Count + pool.Count >= maxPoolSize)
        {
            int idx = FindIndexToRecycle();
            if (idx >= 0)
            {
                var data = activeLines[idx];
                RecycleLine(data.lr);
                activeLines.RemoveAt(idx);
            }
            // After recycling, try pool again
            if (pool.Count > 0)
            {
                var lr = pool.Dequeue();
                lr.gameObject.SetActive(true);
                return lr;
            }
        }

        // Create a new LineRenderer
        GameObject go = new GameObject("RuntimeLine");
        go.transform.parent = transform;
        var lrNew = go.AddComponent<LineRenderer>();
        lrNew.material = defaultMaterial;
        lrNew.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lrNew.receiveShadows = false;
        lrNew.positionCount = 2;
        lrNew.useWorldSpace = true;
        return lrNew;
    }

    // Choose an active line to recycle when we exceed capacity.
    // Heuristic: pick the one with the largest remaining lifetime (so we free the most memory),
    // or if all are frame-only, pick the earliest added (index 0).
    private int FindIndexToRecycle()
    {
        if (activeLines.Count == 0) return -1;

        int idx = 0;
        float maxEnd = activeLines[0].endTime;
        for (int i = 1; i < activeLines.Count; i++)
        {
            if (activeLines[i].endTime > maxEnd)
            {
                maxEnd = activeLines[i].endTime;
                idx = i;
            }
        }
        return idx;
    }

    private void RecycleLine(LineRenderer lr)
    {
        // Defensive: disable and reset a few things to keep a clean pool
        lr.gameObject.SetActive(false);
        // optionally clear positions to save memory:
        lr.positionCount = 0;
        pool.Enqueue(lr);
    }

    // Optional: manual pool cleanup (call if you want to truly destroy pooled objects)
    public void ClearPoolAndActive()
    {
        for (int i = 0; i < activeLines.Count; i++)
        {
            Destroy(activeLines[i].lr.gameObject);
        }
        activeLines.Clear();

        while (pool.Count > 0)
        {
            Destroy(pool.Dequeue().gameObject);
        }
    }
}
