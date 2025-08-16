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

    [Header("UI - main")]
    public GameObject saveWindowPanel;      // modal panel to show save list
    public Transform saveListContent;       // content parent inside ScrollRect
    public GameObject saveSlotButtonPrefab; // button prefab for each save entry (must have Button + TMP_Text)
    public Button loadButton;               // opens the save list
    public Button clearButton;              // clears all saves
    public Button closeButton;

    [Header("UI - new blank")]
    public Button newBlankFileButton;       // create/save & clear and create an empty file

    [Header("UI - rename (shared)")]
    public GameObject renamePanel;          // panel that contains the TMP input & confirm/cancel. Set inactive by default.
    public TMP_InputField renameInputField; // shared input field used to rename a selected save meta
    public Button renameConfirmButton;
    public Button renameCancelButton;

    [Header("UI - delete confirm (shared)")]
    public GameObject confirmDeleteDialog;  // panel for delete confirmation
    public TMP_Text confirmDeleteText;
    public Button confirmDeleteButton;
    public Button cancelDeleteButton;

    // Internal state
    private List<SaveMeta> currentIndex = new();
    private string activeFilename;         // currently-active save file (set when user selects a save or creates new blank)
    private string pendingRenameTarget;    // filename being renamed
    private string pendingDeleteTarget;    // filename pending deletion

    //loadbuttoncolors


    void Start()
    {
        ImportPrepopulatedSaves_FromResources();
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
        if (renamePanel) renamePanel.SetActive(false);
        if (confirmDeleteDialog) confirmDeleteDialog.SetActive(false);

        if (loadButton != null) loadButton.onClick.AddListener(OpenSaveWindow);
        if (clearButton != null) clearButton.onClick.AddListener(ClearAllSaves);
        if (closeButton != null) closeButton.onClick.AddListener(() => { if (saveWindowPanel) saveWindowPanel.SetActive(false); });
        if (newBlankFileButton != null) newBlankFileButton.onClick.AddListener(OnNewBlankFileClicked);

        if (renameCancelButton != null) renameCancelButton.onClick.AddListener(CancelRename);
        if (cancelDeleteButton != null) cancelDeleteButton.onClick.AddListener(CancelDelete);

        // confirm buttons wired when used to avoid stale listeners, but it's safe to clear here
        if (renameConfirmButton != null) renameConfirmButton.onClick.RemoveAllListeners();
        if (confirmDeleteButton != null) confirmDeleteButton.onClick.RemoveAllListeners();

        //normalLoadColors = FindChildButtonByName(saveSlotButtonPrefab, "LoadButton", "load").colors;
    }

    // --- top-level actions ---
    void ClearAllSaves()
    {
        SaveSystem.ClearAllSaves();
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();
    }

    public void OpenSaveWindow()
    {
        // Quick-save behavior: if there's an active file, overwrite it; otherwise create a quick save.
        QuickSaveCurrentScene();
        PopulateSaveList();
        if (saveWindowPanel) saveWindowPanel.SetActive(true);
    }

    private void ImportPrepopulatedSaves_FromResources()
    {
        // Path inside Resources: "PrepopulatedHistory" -> Assets/Resources/PrepopulatedHistory/*.json
        TextAsset[] items = Resources.LoadAll<TextAsset>("PrepopulatedHistory");
        if (items == null || items.Length == 0)
        {
            Debug.Log("[SaveLoadUI] No prepopulated saves found in Resources/PrepopulatedHistory.");
            return;
        }

        Debug.Log($"[SaveLoadUI] Found {items.Length} prepopulated save(s). Importing...");

        foreach (var ta in items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ta.text))
                {
                    Debug.LogWarning($"[SaveLoadUI] Resource {ta.name} is empty, skipping.");
                    continue;
                }

                // Attempt to deserialize to your SaveData type (must match the JSON layout)
                var sd = JsonUtility.FromJson<SaveData>(ta.text);
                if (sd == null)
                {
                    Debug.LogWarning($"[SaveLoadUI] Failed to deserialize {ta.name} into SaveData. Skipping.");
                    continue;
                }

                // Use the resource filename as the display name
                string displayName = ta.name;

                // Save it into persistent saves via your existing SaveSystem API
                // This should create a new file + index entry.
                try
                {
                    string createdFilename = SaveSystem.SaveSceneAs(sd, displayName);
                    Debug.Log($"[SaveLoadUI] Imported '{displayName}' as {createdFilename}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveLoadUI] SaveSystem.SaveSceneAs failed for {displayName}: {e.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadUI] Exception while importing resource {ta.name}: {ex.Message}");
            }
        }
    }

    private void QuickSaveCurrentScene()
    {
        var sd = BuildSaveDataFromRoot();

        if (!string.IsNullOrEmpty(activeFilename))
        {
            // try to keep the same displayName while replacing file on disk (SaveSystem creates a new filename).
            var meta = currentIndex?.Find(m => m.filename == activeFilename);
            string displayName = meta != null ? meta.displayName : "Saved_File";

            // delete the old file / index entry, then create a new save with same displayName
            try
            {
                SaveSystem.DeleteSave(activeFilename);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveLoadUI] Delete during quick-save failed: " + ex.Message);
            }

            string created = SaveSystem.SaveSceneAs(sd, displayName);
            activeFilename = created; // update active to the newly created filename
        }
        else
        {
            // create a quick save (no active file)
            string created = SaveSystem.SaveSceneAs(sd, "Saved_File");
            activeFilename = created;
        }

        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
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

            // ids for lerping
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
            s.musicChannel = bs.musicChannel;
            s.activationThreshhold = bs.activationThreshhold;
            s.isActive = bs.isActive;

            data.splines.Add(s);
        }

        return data;
    }

    // --- UI population ---
    private void PopulateSaveList()
    {
        if (saveListContent == null || saveSlotButtonPrefab == null) return;

        // clear existing UI entries
        foreach (Transform t in saveListContent) Destroy(t.gameObject);

        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();

        var contentRect = saveListContent.GetComponent<RectTransform>();
        float slotHeight = saveSlotButtonPrefab.GetComponent<RectTransform>().sizeDelta.y;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 1.5f * currentIndex.Count * Mathf.Max(1f, slotHeight));

        foreach (var meta in currentIndex)
        {
            var go = Instantiate(saveSlotButtonPrefab, saveListContent);
            Button rootBtn = FindChildButtonByName(go, "LoadButton", "load");


            // TEXTMESH PRO: look for TMP_Text (TextMeshProUGUI)
            var tmpText = rootBtn.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                var dt = new DateTime(meta.timestamp, DateTimeKind.Utc).ToLocalTime();
                tmpText.text = $"{meta.displayName} — {dt:g}";
            }
            else
            {
                Debug.LogWarning("Save slot prefab does not contain a TMP_Text child. Please add one.");
            }

            // capture loop variable
            string filenameForListeners = meta.filename;
            string displayForListeners = meta.displayName;

            //cant changeloadcolors without all of them changing color???!?!
            // if (!string.IsNullOrEmpty(activeFilename) && activeFilename == meta.filename)
            // {
            //     var colors = loadButton.colors;
            //     colors.normalColor = new Color(246, 185, 59);
            //     loadButton.colors = colors;
            // }
            // else
            // {
            //     loadButton.colors = normalLoadColors;
            // }


            // Root click selects / loads the save and sets it as active
            if (rootBtn != null)
            {
                rootBtn.onClick.RemoveAllListeners();
                rootBtn.onClick.AddListener(() => OnSaveSelected(filenameForListeners));
            }

            // Per-slot buttons: try to find children named "RenameButton", "CopyButton", "DeleteButton".
            // Fall back to scanning child buttons and matching name contains.
            Button renameBtn = FindChildButtonByName(go, "RenameButton", "rename");
            Button copyBtn = FindChildButtonByName(go, "CopyButton", "copy");
            Button deleteBtn = FindChildButtonByName(go, "DeleteButton", "delete");

            if (renameBtn != null)
            {
                renameBtn.onClick.RemoveAllListeners();
                renameBtn.onClick.AddListener(() => OpenRenameFor(filenameForListeners));
            }

            if (copyBtn != null)
            {
                copyBtn.onClick.RemoveAllListeners();
                copyBtn.onClick.AddListener(() => CopySaveFile(filenameForListeners));
            }

            if (deleteBtn != null)
            {
                deleteBtn.onClick.RemoveAllListeners();
                deleteBtn.onClick.AddListener(() => OpenDeleteConfirm(filenameForListeners, displayForListeners));
            }
        }
    }

    // Helper: find a child button by exact name or by substring fallback
    private Button FindChildButtonByName(GameObject root, string exactName, string containsLower)
    {
        var t = root.transform.Find(exactName);
        if (t != null)
        {
            var b = t.GetComponent<Button>();
            if (b != null) return b;
        }

        var allButtons = root.GetComponentsInChildren<Button>();
        foreach (var b in allButtons)
        {
            if (b.gameObject.name.ToLower().Contains(containsLower)) return b;
        }

        return null;
    }

    // --- selection / loading ---
    public void OnSaveSelected(string filename)
    {
        activeFilename = filename;
        LoadSaveGroup(filename);
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
    }

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
            var go = Instantiate(splinePrefab, savableRoot);
            go.name = sd.name;
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
                bs.musicChannel = sd.musicChannel;
                bs.activationThreshhold = sd.activationThreshhold;
                bs.isActive = sd.isActive;

                // set private guid via reflection (your existing approach)
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

        EventHub.Publish(new ReloadLerpUI(true));
        EventHub.Publish(new ReloadParticles(true));
    }

    private void ClearSavedObjects()
    {
        if (savableRoot == null) return;
        var children = new List<GameObject>();
        foreach (Transform t in savableRoot) children.Add(t.gameObject);
        foreach (var c in children)
        {
            var bs = c.GetComponent<BezierSpline>();
            EventHub.Publish(new DeleteSpline(bs));
        }
    }

    // --- rename flow ---
    private void OpenRenameFor(string filename)
    {
        var meta = currentIndex?.Find(m => m.filename == filename);
        if (meta == null)
        {
            Debug.LogError("Rename target meta not found: " + filename);
            return;
        }

        pendingRenameTarget = filename;
        if (renameInputField != null) renameInputField.text = meta.displayName;

        if (renameConfirmButton != null)
        {
            renameConfirmButton.onClick.RemoveAllListeners();
            renameConfirmButton.onClick.AddListener(ConfirmRename);
        }

        if (renamePanel != null) renamePanel.SetActive(true);
    }

    private void ConfirmRename()
    {
        if (string.IsNullOrEmpty(pendingRenameTarget))
        {
            Debug.LogWarning("No pending rename target.");
            if (renamePanel != null) renamePanel.SetActive(false);
            return;
        }

        var meta = currentIndex?.Find(m => m.filename == pendingRenameTarget);
        if (meta == null)
        {
            Debug.LogError("Rename target meta not found during confirm: " + pendingRenameTarget);
            if (renamePanel != null) renamePanel.SetActive(false);
            return;
        }

        string newName = renameInputField != null ? renameInputField.text : meta.displayName;
        meta.displayName = newName;

        SaveSystem.SaveIndex(currentIndex);
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();

        pendingRenameTarget = null;
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    private void CancelRename()
    {
        pendingRenameTarget = null;
        if (renamePanel != null) renamePanel.SetActive(false);
    }

    // --- delete flow ---
    private void OpenDeleteConfirm(string filename, string displayName)
    {
        pendingDeleteTarget = filename;
        if (confirmDeleteText != null) confirmDeleteText.text = $"Delete \"{displayName}\"? This cannot be undone.";

        if (confirmDeleteButton != null)
        {
            confirmDeleteButton.onClick.RemoveAllListeners();
            confirmDeleteButton.onClick.AddListener(ConfirmDelete);
        }

        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(true);
    }

    private void ConfirmDelete()
    {
        if (string.IsNullOrEmpty(pendingDeleteTarget))
        {
            Debug.LogWarning("No pending delete target.");
            if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
            return;
        }

        SaveSystem.DeleteSave(pendingDeleteTarget);
        // if deleting the active file, clear active
        if (pendingDeleteTarget == activeFilename) activeFilename = null;

        pendingDeleteTarget = null;
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();

        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
    }

    private void CancelDelete()
    {
        pendingDeleteTarget = null;
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
    }

    // --- copy ---
    private void CopySaveFile(string filename)
    {
        var meta = currentIndex?.Find(m => m.filename == filename);
        if (meta == null)
        {
            Debug.LogError("Copy target meta not found: " + filename);
            return;
        }

        var data = SaveSystem.LoadSaveFile(filename);
        if (data == null)
        {
            Debug.LogError("Copy source save data missing: " + filename);
            return;
        }

        string newDisplayName = meta.displayName + " (copy)";
        string created = SaveSystem.SaveSceneAs(data, newDisplayName);

        // Optional: set copied file as active? Not required — we leave activeFilename unchanged.
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();
    }

    private void OnNewBlankFileClicked()
    {
        // If there's an active file, persist current scene by overwriting the active entry (delete old file then save snapshot)
        if (!string.IsNullOrEmpty(activeFilename))
        {
            var meta = currentIndex?.Find(m => m.filename == activeFilename);
            string displayName = meta != null ? meta.displayName : "Saved_File";

            var snapshot = BuildSaveDataFromRoot();

            try
            {
                // remove old file/index entry so SaveSceneAs doesn't just add another file
                SaveSystem.DeleteSave(activeFilename);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveLoadUI] Failed to delete active file before overwrite: " + ex.Message);
            }

            // recreate the active save entry using the same displayName
            // we don't keep this filename as active because we're about to create the new blank and set that active
            try
            {
                SaveSystem.SaveSceneAs(snapshot, displayName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveLoadUI] Failed to save snapshot of current scene: " + ex.Message);
            }
        }

        // Clear the scene
        ClearSavedObjects();

        // Create exactly one new empty save and set it active
        var empty = new SaveData();
        string created = null;
        try
        {
            created = SaveSystem.SaveSceneAs(empty, "New File");
            activeFilename = created;
        }
        catch (Exception ex)
        {
            Debug.LogError("[SaveLoadUI] Failed to create new blank save: " + ex.Message);
        }

        // refresh index & UI
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();
    }

}
