using System.Collections.Generic;
using UnityEngine;

public class SavePreviewManager : MonoBehaviour
{
    public static SavePreviewManager Instance { get; private set; }

    [Header("Preview Setup")]
    public GameObject previewBoothPrefab; 
    public GameObject splinePrefab;
    
    // Tracks active booths and dynamically created textures
    private List<GameObject> activeBooths = new List<GameObject>();
    private List<RenderTexture> dynamicTextures = new List<RenderTexture>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Generates a RenderTexture on the GPU sized perfectly for the UI cell.
    /// </summary>
    public RenderTexture CreatePreviewTexture(int width, int height)
    {
        // 16-bit depth buffer is standard for 3D camera rendering
        RenderTexture rt = new RenderTexture(width, height, 16);
        rt.Create();
        dynamicTextures.Add(rt);
        return rt;
    }

    /// <summary>
    /// Spawns a single booth for a specific save slot and links it to the generated texture.
    /// </summary>
    public void GenerateSinglePreviewBooth(SaveData data, RenderTexture targetTexture, int index)
    {
        Vector3 boothPos = new Vector3(0, (index + 1) * 2000f, 0);
        GameObject booth = Instantiate(previewBoothPrefab, boothPos, Quaternion.identity);
        activeBooths.Add(booth);

        Camera boothCam = booth.GetComponentInChildren<Camera>();
        boothCam.targetTexture = targetTexture;

        // Apply the saved perspective relative to the booth's center
        boothCam.transform.SetLocalPositionAndRotation(new Vector3(data.camPosX, data.camPosY, data.camPosZ), Quaternion.Euler(data.camRotX, data.camRotY, data.camRotZ));
        Transform container = booth.transform.Find("SplineContainer");
        ParticleManager boothPM = booth.GetComponentInChildren<ParticleManager>();
        QuietLoadPreviewData(data, container, boothPM);
    }

    public void ClearPreviews()
    {
        // Destroy 3D Booths
        foreach (var booth in activeBooths)
        {
            Destroy(booth);
        }
        activeBooths.Clear();

        // CRITICAL: Release GPU memory to prevent massive memory leaks
        foreach (var rt in dynamicTextures)
        {
            if (rt != null) rt.Release();
        }
        dynamicTextures.Clear();
    }

    private void QuietLoadPreviewData(SaveData data, Transform container, ParticleManager boothPM)
    {
        boothPM.splineParticleGroup = new List<SplineParticleGroup>();
        int previewLayer = LayerMask.NameToLayer("Preview");
        
        var lookup = new Dictionary<string, BezierSpline>();

        foreach (var sd in data.splines)
        {
            GameObject go = Instantiate(splinePrefab, container);
            go.transform.localPosition = new Vector3(sd.posX, sd.posY, sd.posZ);
            SetLayerRecursively(go, previewLayer);

            BezierSpline bs = go.GetComponent<BezierSpline>();
            if (bs != null)
            {
                if (sd.points != null && sd.points.Count % 3 == 0)
                {
                    int vecCount = sd.points.Count / 3;
                    bs.points = new Vector3[vecCount];
                    for (int j = 0; j < vecCount; j++)
                    {
                        bs.points[j] = new Vector3(sd.points[j * 3], sd.points[j * 3 + 1], sd.points[j * 3 + 2]);
                    }
                }
                
                bs.s_life = sd.s_life;
                bs.splineSymmetry = sd.splineSymmetry;
                bs.frequency = sd.frequency;
                bs.lifetimeOffset = sd.lifetimeOffset;
                bs.lerpLifetimeOffset = sd.lerpLifetimeOffset;
                bs.lerpTimes = sd.lerpTimes;
                bs.lerpColor1 = sd.lerpColor1;
                bs.lerpColor2 = sd.lerpColor2;
                bs.isActive = sd.isActive;
                
                typeof(BezierSpline).GetField("guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(bs, sd.id);
                lookup[sd.id] = bs;

                boothPM.splineParticleGroup.Add(new SplineParticleGroup { spline = bs });
            }
        }

        foreach (var sd in data.splines)
        {
            if (!string.IsNullOrEmpty(sd.lerpSplineId) && lookup.TryGetValue(sd.lerpSplineId, out var target))
            {
                lookup[sd.id].lerpSpline = target;
            }
        }

        boothPM.spawnMode = ParticleSpawnMode.Independent;
        boothPM.gameObject.SendMessage("StartIndependent");
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}