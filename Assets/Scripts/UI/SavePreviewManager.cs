using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SavePreviewManager : MonoBehaviour
{
    public static SavePreviewManager Instance { get; private set; }

    [Header("Preview Setup")]
    public GameObject previewBoothPrefab; // Contains Camera and ParticleManager
    public GameObject splinePrefab;
    public RenderTexture[] previewTextures; // Exactly 4 textures for the 2x2 grid

    private List<GameObject> activeBooths = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Spawns booths and loads spline data for a given list of save files (max 4).
    /// </summary>
    public void GeneratePreviews(List<SaveData> pageData)
    {
        ClearPreviews();

        for (int i = 0; i < pageData.Count; i++)
        {
            if (i >= 4) break; // Hard cap at 4 for the 2x2 grid

            // 1. Spawn isolated booth far away on the Y axis
            Vector3 boothPos = new Vector3(0, (i + 1) * 2000f, 0);
            GameObject booth = Instantiate(previewBoothPrefab, boothPos, Quaternion.identity);
            activeBooths.Add(booth);

            Camera boothCam = booth.GetComponentInChildren<Camera>();
            boothCam.targetTexture = previewTextures[i];
            //Debug.Log($"[Preview Mapping] Booth {i} (Y={boothPos.y}) Camera '{boothCam.name}' is rendering to Texture '{previewTextures[i].name}'");

            // 3. Load the data quietly
            Transform container = booth.transform.Find("SplineContainer");
            ParticleManager boothPM = booth.GetComponentInChildren<ParticleManager>();

            //Debug.Log($"quietloading. first spline has color {pageData[i].splines[0].lerpColor1}");
            QuietLoadPreviewData(pageData[i], container, boothPM);
            
        }
    }

    public void ClearPreviews()
    {
        foreach (var booth in activeBooths)
        {
            Destroy(booth);
        }
        activeBooths.Clear();
    }

    private void QuietLoadPreviewData(SaveData data, Transform container, ParticleManager boothPM)
    {
        boothPM.splineParticleGroup = new List<SplineParticleGroup>();
        int previewLayer = LayerMask.NameToLayer("Preview");
        
        // 1. Create a lookup dictionary (just like in SaveLoadUI)
        var lookup = new Dictionary<string, BezierSpline>();

        foreach (var sd in data.splines)
        {
            GameObject go = Instantiate(splinePrefab, container);
            go.transform.localPosition = new Vector3(sd.posX, sd.posY, sd.posZ);
            SetLayerRecursively(go, previewLayer);

            BezierSpline bs = go.GetComponent<BezierSpline>();
            if (bs != null)
            {
                // Reconstruct points
                if (sd.points != null && sd.points.Count % 3 == 0)
                {
                    int vecCount = sd.points.Count / 3;
                    bs.points = new Vector3[vecCount];
                    for (int j = 0; j < vecCount; j++)
                    {
                        bs.points[j] = new Vector3(sd.points[j * 3], sd.points[j * 3 + 1], sd.points[j * 3 + 2]);
                    }
                }
                
                // Apply visual settings (Added missing offsets)
                bs.s_life = sd.s_life;
                bs.splineSymmetry = sd.splineSymmetry;
                bs.frequency = sd.frequency;
                bs.lifetimeOffset = sd.lifetimeOffset; // FIXED
                bs.lerpLifetimeOffset = sd.lerpLifetimeOffset; // FIXED
                bs.lerpTimes = sd.lerpTimes;
                bs.lerpColor1 = sd.lerpColor1;
                bs.lerpColor2 = sd.lerpColor2;
                bs.isActive = sd.isActive;
                
                // Set GUID via reflection so the dictionary lookup works
                typeof(BezierSpline).GetField("guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(bs, sd.id);
                lookup[sd.id] = bs;

                boothPM.splineParticleGroup.Add(new SplineParticleGroup { spline = bs });
            }
        }

        // 2. Second pass to rebuild the lerp connections
        foreach (var sd in data.splines)
        {
            if (!string.IsNullOrEmpty(sd.lerpSplineId) && lookup.TryGetValue(sd.lerpSplineId, out var target))
            {
                lookup[sd.id].lerpSpline = target;
            }
        }

        // Force independent mode so particles emit automatically without audio input
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
