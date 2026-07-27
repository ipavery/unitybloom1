using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Collections;

public class SaveLoadUI : MonoBehaviour
{
    // Singleton Instance for easy access across any script
    public static SaveLoadUI Instance { get; private set; }

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
    public Button saveButton;
    public Button clearButton;              // clears all saves
    public Button closeButton;
    public SaveIconFader saveIconFader;

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

    // Add these new variables to your existing headers in SaveLoadUI.cs
    [Header("Pagination UI")]
    public Button nextPageButton;
    public Button prevPageButton;
    public TMP_Text pageText;
    private int currentPage = 0;
    private const int SAVES_PER_PAGE = 4;

    [Header("Autosave Settings")]
    public bool enableAutosave = true;
    [Tooltip("How long to wait after the last user action before saving to disk.")]
    public float debounceTime = 1f; 
    private Coroutine pendingAutosave;
    

    // Internal state
    private List<SaveMeta> currentIndex = new();
    private string activeFilename;         // currently-active save file (set when user selects a save or creates new blank)
    private string pendingRenameTarget;    // filename being renamed
    private string pendingDeleteTarget;    // filename pending deletion


    private void Awake()
    {
        // Setup Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // void OnEnable()
    // {
    //     EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
    // }

    // void OnDisable()
    // {
    //     EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
    // }

    // void OnNewSplineCreated(NewSplineCreated e)
    // {
    //     NotifyActionPerformed();
    // }

    void Start()
    {
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
        if (renamePanel) renamePanel.SetActive(false);
        if (confirmDeleteDialog) confirmDeleteDialog.SetActive(false);

        if (loadButton != null) loadButton.onClick.AddListener(OpenSaveWindow);
        if (saveButton != null) saveButton.onClick.AddListener(QuickSaveCurrentScene);
        if (clearButton != null) clearButton.onClick.AddListener(OpenClearAllSavesConfirm);
        if (closeButton != null) closeButton.onClick.AddListener(() => { if (saveWindowPanel) saveWindowPanel.SetActive(false); });
        if (newBlankFileButton != null) newBlankFileButton.onClick.AddListener(OnNewBlankFileClicked);

        if (renameCancelButton != null) renameCancelButton.onClick.AddListener(CancelRename);
        if (cancelDeleteButton != null) cancelDeleteButton.onClick.AddListener(CancelDelete);

        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);
        if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);

        // confirm buttons wired when used to avoid stale listeners, but it's safe to clear here
        if (renameConfirmButton != null) renameConfirmButton.onClick.RemoveAllListeners();
        if (confirmDeleteButton != null) confirmDeleteButton.onClick.RemoveAllListeners();

        // Auto-load the most recent save on startup
        AutoLoadMostRecentSave();

        //uncomment this if you want to import prepopulated saves from Resources/PrepopulatedHistory on start (make sure to add .json files there first, and that they match your SaveData structure)
        ImportPrepopulatedSaves_FromResources();
    }

    /// <summary>
    /// Call this from any script whenever a user finishes an action (e.g. added spline, edited point, executed command).
    /// </summary>
    public void NotifyActionPerformed()
    {
        if (!enableAutosave || string.IsNullOrEmpty(activeFilename)) return;

        // If the user performs another action while a save is pending, reset the timer.
        // This prevents multiple file writes while actively editing/drawing continuously.
        if (pendingAutosave != null)
        {
            StopCoroutine(pendingAutosave);
        }

        pendingAutosave = StartCoroutine(DebouncedAutosaveRoutine());
    }

    private IEnumerator DebouncedAutosaveRoutine()
    {
        yield return new WaitForSeconds(debounceTime);

        // Perform the quiet background save
        QuickSaveCurrentScene();
        pendingAutosave = null;
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

    /// <summary>
    /// Automatically loads the most recent save on startup.
    /// If no saves exist, creates and loads a blank save.
    /// </summary>
    private void AutoLoadMostRecentSave()
    {
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        
        if (currentIndex.Count == 0)
        {
            Debug.Log("[SaveLoadUI] No saves found. Creating and loading a blank save...");
            
            // Create a new blank save and load it
            var empty = new SaveData();
            try
            {
                string created = SaveSystem.SaveSceneAs(empty, "Auto-Created Save");
                activeFilename = created;
                LoadSaveGroup(created);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveLoadUI] Failed to auto-create blank save: " + ex.Message);
            }
            
            return;
        }

        // Find the most recent save (highest timestamp)
        SaveMeta mostRecent = currentIndex[0];
        foreach (var meta in currentIndex)
        {
            if (meta.timestamp > mostRecent.timestamp)
            {
                mostRecent = meta;
            }
        }

        Debug.Log($"[SaveLoadUI] Auto-loading most recent save: '{mostRecent.displayName}'");
        activeFilename = mostRecent.filename;
        LoadSaveGroup(mostRecent.filename);
    }

    // --- top-level actions ---
    void ClearAllSaves()
    {
        if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
        SaveSystem.ClearAllSaves();
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        PopulateSaveList();
    }

    public void OpenSaveWindow()
    {
        // Quick-save behavior: if there's an active file, overwrite it; otherwise create a quick save.
        //QuickSaveCurrentScene();
        PopulateSaveList();
        if (saveWindowPanel) saveWindowPanel.SetActive(true);
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

        if (saveIconFader != null)
        {
            saveIconFader.ShowSaveIcon();
        }
    }

    // Build SaveData from savableRoot by scanning for BezierSpline components
    public SaveData BuildSaveDataFromRoot()
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

        // Clean up UI and 3D Previews
        foreach (Transform t in saveListContent) 
        {
            // Detach it from the layout group instantly so it doesn't take up a grid cell
            t.SetParent(null); 
            Destroy(t.gameObject);
        }

        // Clean up UI and 3D Previews
        foreach (Transform t in saveListContent) Destroy(t.gameObject);
        if (SavePreviewManager.Instance != null) SavePreviewManager.Instance.ClearPreviews();

        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();

        // Pagination math
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)currentIndex.Count / SAVES_PER_PAGE));
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
        int startIndex = currentPage * SAVES_PER_PAGE;
        
        UpdatePaginationUI(totalPages);

        List<SaveData> pageSaveData = new List<SaveData>();

        for (int i = 0; i < SAVES_PER_PAGE; i++)
        {
            int dataIndex = startIndex + i;
            if (dataIndex >= currentIndex.Count) break;

            var meta = currentIndex[dataIndex];
            var go = Instantiate(saveSlotButtonPrefab, saveListContent);
            
            // Standard Button Setup (Your existing FindChildButtonByName logic goes here)
            Button rootBtn = go.GetComponent<Button>();
            var tmpText = go.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                var dt = new DateTime(meta.timestamp, DateTimeKind.Utc).ToLocalTime();
                tmpText.text = $"{meta.displayName}\n{dt:g}";
            }

            string filenameForListeners = meta.filename;
            if (rootBtn != null)
            {
                rootBtn.onClick.RemoveAllListeners();
                rootBtn.onClick.AddListener(() => OnSaveSelected(filenameForListeners));
            }

            var rawImage = go.GetComponentInChildren<RawImage>();
            if (rawImage != null)
            {
                // Assign the texture
                if (SavePreviewManager.Instance != null)
                {
                    rawImage.texture = SavePreviewManager.Instance.previewTextures[i];
                }

                // Ensure the image catches mouse clicks
                rawImage.raycastTarget = true;

                // Attach or grab a Button component directly on the RawImage
                if (!rawImage.TryGetComponent<Button>(out var previewButton)) 
                {
                    previewButton = rawImage.gameObject.AddComponent<Button>();
                }

                // Bind the load action
                string targetFilename = meta.filename;
                previewButton.onClick.RemoveAllListeners();
                previewButton.onClick.AddListener(() => OnSaveSelected(targetFilename));
            }

            // Load the actual save data from disk to pass to the preview manager
            SaveData data = SaveSystem.LoadSaveFile(meta.filename);
            pageSaveData.Add(data ?? new SaveData()); // Add empty if corrupted so indices match
        }

        // Send the loaded page data to the Preview Manager to spawn the booths
        if (SavePreviewManager.Instance != null)
        {
            SavePreviewManager.Instance.GeneratePreviews(pageSaveData);
        }
    }

    private void UpdatePaginationUI(int totalPages)
    {
        if (pageText != null) pageText.text = $"Page {currentPage + 1} of {totalPages}";
        if (prevPageButton != null) prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = currentPage < totalPages - 1;
    }

    private void NextPage()
    {
        currentPage++;
        PopulateSaveList();
    }

    private void PrevPage()
    {
        currentPage--;
        PopulateSaveList();
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
        UndoManager.Instance.ClearHistory(); // clear undo history so that you don't load different saves
        activeFilename = filename;
        LoadSaveGroup(filename);
        if (saveWindowPanel) saveWindowPanel.SetActive(false);
    }

    public void LoadSaveDataInMemory(SaveData data)
    {
        if (savableRoot == null || splinePrefab == null) return;

        ClearSavedObjects();
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
                        bs.points[i] = new Vector3(sd.points[i * 3], sd.points[i * 3 + 1], sd.points[i * 3 + 2]);
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
                bs.musicChannel = sd.musicChannel;
                bs.activationThreshhold = sd.activationThreshhold;
                bs.isActive = sd.isActive;

                typeof(BezierSpline).GetField("guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(bs, sd.id);
                lookup[sd.id] = bs;

                go.SetActive(true);
                EventHub.Publish(new NewSplineCreated(bs, false));
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

    private void LoadSaveGroup(string filename)
    {
        var data = SaveSystem.LoadSaveFile(filename);
        if (data == null)
        {
            Debug.LogError("Save file missing or invalid: " + filename);
            return;
        }
        LoadSaveDataInMemory(data);
    }

    private void ClearSavedObjects()
    {
        if (savableRoot == null) return;

        var children = new List<GameObject>(); //Creates a temporary list that will store the spline objects that need to be removed.
        foreach (Transform t in savableRoot)
        {
            if (t.TryGetComponent<BezierSpline>(out var bs))
            {
                EventHub.Publish(new DeleteSpline(bs));
                children.Add(t.gameObject); //Adds the actual child GameObject to the temporary list so it can be destroyed later.
            }
        }

        foreach (var c in children)
        {
            Destroy(c);
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

    private void OpenClearAllSavesConfirm()
    {
        if (confirmDeleteText != null) confirmDeleteText.text = $"Clear all saves? This cannot be undone.";

        if (confirmDeleteButton != null)
        {
            confirmDeleteButton.onClick.RemoveAllListeners();
            confirmDeleteButton.onClick.AddListener(ClearAllSaves);
        }

        if (cancelDeleteButton != null)
        {
            cancelDeleteButton.onClick.RemoveAllListeners();
            cancelDeleteButton.onClick.AddListener(() =>
            {
                if (confirmDeleteDialog != null) confirmDeleteDialog.SetActive(false);
            });
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

                if (saveIconFader != null)
                {
                    saveIconFader.ShowSaveIcon(); //show the saved text so user knows the file was saved.
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveLoadUI] Failed to save snapshot of current scene: " + ex.Message);
            }
        }

        UndoManager.Instance.ClearHistory(); // clear undo history so that you don't load different saves

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
