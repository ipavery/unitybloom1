using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class SaveLoadUI : MonoBehaviour
{
    [Header("Scene Roots")]
    public Transform savableRoot;        // parent containing current splines (to save from)
    public Transform savedObjectsRoot;   // parent where loaded splines are instantiated

    [Header("Prefab (single)")]
    public GameObject splinePrefab;      // single prefab used for all splines (must have BezierSpline component)

    [Header("UI")]
    public GameObject saveWindowPanel;      // modal panel to show save list
    public Transform saveListContent;       // content parent inside ScrollRect
    public GameObject saveSlotButtonPrefab; // button prefab for each save entry (must have Button + TMP_Text)
    public Button loadButton;
    public Button clearButton;

    private List<SaveMeta> currentIndex;

    void Start()
    {
        currentIndex = SaveSystem.LoadIndex();
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
        loadButton.onClick.AddListener(OpenSaveWindow);
        clearButton.onClick.AddListener(ClearAllSaves);
    }

    void ClearAllSaves()
    {
        SaveSystem.ClearAllSaves();
        PopulateSaveList();
    }

    // Called by your "Load" UI button
    public void OpenSaveWindow()
    {
        QuickSaveCurrentScene();
        PopulateSaveList();
        if (saveWindowPanel) saveWindowPanel.SetActive(true);
    }

    // 1) Quick save current scene so the user can return to it
    private void QuickSaveCurrentScene()
    {
        var sd = BuildSaveDataFromRoot();
        string name = $"Saved_File";
        SaveSystem.SaveSceneAs(sd, name);
        currentIndex = SaveSystem.LoadIndex();
    }

    // Build SaveData from savableRoot by scanning for BezierSpline components
    private SaveData BuildSaveDataFromRoot()
    {
        var data = new SaveData();
        if (savableRoot == null) return data;

        foreach (Transform child in savableRoot)
        {
            var bs = child.GetComponent<BezierSpline>();
            if (bs == null) continue; // skip non-spline objects

            var s = new SplineData();
            s.name = child.name;
            s.posX = child.position.x;
            s.posY = child.position.y;
            s.posZ = child.position.z;

            // Flatten points (Vector3[] -> List<float> as x,y,z,x,y,z,...)
            s.points = new List<float>();
            if (bs.points != null)
            {
                foreach (var p in bs.points)
                {
                    s.points.Add(p.x);
                    s.points.Add(p.y);
                    s.points.Add(p.z);
                }
            }

            //ids for lerping
            s.id = bs.Guid;
            if (bs.lerpSpline != null)
                s.lerpSplineId = bs.lerpSpline.Guid;

            s.s_life = bs.s_life;
            s.splineSymmetry = bs.splineSymmetry;
            s.frequency = bs.frequency;
            s.lifetimeOffset = bs.lifetimeOffset;
            s.lerpLifetimeOffset = bs.lerpLifetimeOffset;
            s.lerpTimes = bs.lerpTimes;
            s.lerpColor1 = bs.lerpColor1;
            s.lerpColor2 = bs.lerpColor2;
            s.isActive = bs.isActive;

            data.splines.Add(s);
        }

        var tmp = new SaveData();

        return data;
    }

    // Populate the UI list with available saves
    private void PopulateSaveList()
    {
        if (saveListContent == null || saveSlotButtonPrefab == null) return;

        // clear existing UI entries
        foreach (Transform t in saveListContent) Destroy(t.gameObject);

        currentIndex = SaveSystem.LoadIndex();

        var contentRect = saveListContent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 1.5f * currentIndex.Count * saveSlotButtonPrefab.GetComponent<RectTransform>().sizeDelta.y);

        foreach (var meta in currentIndex)
        {
            var go = Instantiate(saveSlotButtonPrefab, saveListContent);
            var btn = go.GetComponent<Button>();

            // TEXTMESH PRO: look for TMP_Text (TextMeshProUGUI)
            var tmpText = go.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                var dt = new DateTime(meta.timestamp, DateTimeKind.Utc).ToLocalTime();
                tmpText.text = $"{meta.displayName} — {dt:g}";
            }
            else
            {
                Debug.LogWarning("Save slot prefab does not contain a TMP_Text child. Please add one.");
            }

            if (btn != null)
            {
                string filenameCapture = meta.filename;
                btn.onClick.AddListener(() => OnSaveSelected(filenameCapture));
            }
            else
            {
                Debug.LogWarning("Save slot prefab does not have a Button component on its root.");
            }
        }
    }

    // Called when user clicks a save entry
    private void OnSaveSelected(string filename)
    {
        LoadSaveGroup(filename);
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
    }

    // 4) Clear the savedObjectsRoot and instantiate the splines from the chosen save
    private void LoadSaveGroup(string filename)
    {
        if (savableRoot == null)
        {
            Debug.LogError("savableroot not assigned in SaveLoadUI.");
            return;
        }

        if (splinePrefab == null)
        {
            Debug.LogError("splinePrefab not assigned in SaveLoadUI.");
            return;
        }

        ClearSavedObjects();

        var data = SaveSystem.LoadSaveFile(filename);
        if (data == null)
        {
            Debug.LogError("Save file missing or invalid: " + filename);
            return;
        }

        var lookup = new Dictionary<string, BezierSpline>();

        foreach (var sd in data.splines)
        {
            // Instantiate the single spline prefab for every saved spline
            var go = Instantiate(splinePrefab, savableRoot);
            go.name = sd.name;

            // set world position
            go.transform.position = new Vector3(sd.posX, sd.posY, sd.posZ);

            var bs = go.GetComponent<BezierSpline>();
            if (bs != null)
            {
                // reconstruct points
                if (sd.points != null && sd.points.Count % 3 == 0)
                {
                    int vecCount = sd.points.Count / 3;
                    bs.points = new Vector3[vecCount];
                    for (int i = 0; i < vecCount; i++)
                    {
                        float x = sd.points[i * 3 + 0];
                        float y = sd.points[i * 3 + 1];
                        float z = sd.points[i * 3 + 2];
                        bs.points[i] = new Vector3(x, y, z);
                    }
                }
                else
                {
                    Debug.LogWarning($"Spline '{sd.name}' has invalid points list (count {sd.points?.Count ?? 0}). Using prefab defaults.");
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

                typeof(BezierSpline).GetField("guid", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(bs, sd.id);

                lookup[sd.id] = bs;

                go.SetActive(true);

                EventHub.Publish(new NewSplineCreated(bs, false));
            }
            else
            {
                Debug.LogWarning("Instantiated prefab does not contain a BezierSpline component.");
            }
        }
        foreach (var sd in data.splines)
        {
            if (!string.IsNullOrEmpty(sd.lerpSplineId) && lookup.TryGetValue(sd.lerpSplineId, out var target))
            {
                lookup[sd.id].lerpSpline = target;
            }
        }

        EventHub.Publish(new ReloadParticles(true));
    }

    private void ClearSavedObjects()
    {
        if (savableRoot == null) return;
        var children = new List<GameObject>();
        foreach (Transform t in savableRoot) children.Add(t.gameObject);
        foreach (var c in children)
        {
            EventHub.Publish(new DeleteSpline(c.GetComponent<BezierSpline>()));
        }
    }
}
