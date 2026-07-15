using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }

    public SaveLoadUI saveLoadUI;
    public SplinePickerData SPD;

    private Stack<string> undoStack = new Stack<string>();
    private Stack<string> redoStack = new Stack<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        if (Keyboard.current.ctrlKey.isPressed)
        {
            if (Keyboard.current.zKey.wasPressedThisFrame) Undo();
            if (Keyboard.current.yKey.wasPressedThisFrame) Redo();
        }
    }

    public void RecordState()
    {
        string jsonState = JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot());
        undoStack.Push(jsonState);
        redoStack.Clear();
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.LogWarning("UndoManager: Nothing to undo");
            return;
        }
        redoStack.Push(JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot()));
        RestoreState(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.LogWarning("UndoManager: Nothing to redo");
            return;
        }
        undoStack.Push(JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot()));
        RestoreState(redoStack.Pop());
    }

    // Call this from SaveLoadUI when loading a new file or creating a blank one
    public void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
    }

    private void RestoreState(string json)
    {
        // ==========================================
        // STEP 1: CAPTURE EXACT SELECTION STATE
        // ==========================================
        string activeSplineGuid = null;
        List<int> selectedIndices = new List<int>();
        int lastHighlightedIndex = -1;

        // A. Capture Spline UI Selection
        SelectionController selectionController = FindFirstObjectByType<SelectionController>();
        if (selectionController != null && selectionController.selectedSpline != null)
        {
            activeSplineGuid = GetSplineGuid(selectionController.selectedSpline);
        }

        // B. Capture Point Selection & Last Highlighted
        if (SPD != null)
        {
            foreach (var group in SPD.controlSphereGroup)
            {
                if (group.isSelected)
                {
                    if (activeSplineGuid == null)
                    {
                        var spline = group.sphereObject.GetComponentInParent<BezierSpline>();
                        if (spline != null) activeSplineGuid = GetSplineGuid(spline);
                    }

                    selectedIndices.Add(group.index);

                    if (SPD.lastHighlighted == group.sphereObject)
                    {
                        lastHighlightedIndex = group.index;
                    }
                }
            }
        }

        // ---> ADD THIS FIX HERE: Cache the old splines so we can ignore them later
        HashSet<BezierSpline> oldSplines = new HashSet<BezierSpline>();
        if (saveLoadUI != null && saveLoadUI.savableRoot != null)
        {
            foreach (var bs in saveLoadUI.savableRoot.GetComponentsInChildren<BezierSpline>(true))
            {
                oldSplines.Add(bs);
            }
        }

        // ==========================================
        // STEP 2: CLEAR AND LOAD SNAPSHOT
        // ==========================================
        EventHub.Publish(new ClearSelection());
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        saveLoadUI.LoadSaveDataInMemory(data);

        // ==========================================
        // STEP 3: RESTORE SELECTION EXACTLY
        // ==========================================
        if (activeSplineGuid != null)
        {
            BezierSpline targetSpline = null;

            if (saveLoadUI != null && saveLoadUI.savableRoot != null)
            {
                BezierSpline[] allSplines = saveLoadUI.savableRoot.GetComponentsInChildren<BezierSpline>(true);
                foreach (var spline in allSplines)
                {
                    // ---> ADD THIS FIX HERE: Skip the old splines waiting to be destroyed
                    if (oldSplines.Contains(spline)) continue;

                    if (GetSplineGuid(spline) == activeSplineGuid)
                    {
                        targetSpline = spline;
                        break;
                    }
                }
            }

            if (targetSpline != null)
            {
                GameObject exactLastHighlighted = null;
                GameObject fallbackHighlighted = null;

                if (SPD != null)
                {
                    foreach (var group in SPD.controlSphereGroup)
                    {
                        var spline = group.sphereObject.GetComponentInParent<BezierSpline>();

                        if (spline != null && GetSplineGuid(spline) == activeSplineGuid && selectedIndices.Contains(group.index))
                        {
                            group.isSelected = true;
                            if (fallbackHighlighted == null) fallbackHighlighted = group.sphereObject;

                            if (group.index == lastHighlightedIndex) exactLastHighlighted = group.sphereObject;

                            if (group.sphereObject.TryGetComponent<Renderer>(out var rend))
                            {
                                rend.material.color = SPD.highlightColor; //could change this to eventhub publish selectcontrolpoint to reduce duplicate code
                                rend.material.SetColor("_EmissionColor", SPD.highlightColor * SPD.gizmoEmissionIntensity);
                            }
                        }
                    }
                }

                // Restore UI Inspector connection
                // This instantly updates all the sliders to reflect the UNDONE values!
                EventHub.Publish(new SplineSelectionChange(targetSpline, true));

                // Restore Gizmo to the exact same point
                GameObject targetGizmoAnchor = exactLastHighlighted != null ? exactLastHighlighted : fallbackHighlighted;
                if (targetGizmoAnchor != null)
                {
                    SPD.lastHighlighted = targetGizmoAnchor;
                    EventHub.Publish(new ShowMoveGizmosEvent(targetGizmoAnchor.transform.position));
                }
            }
            else
            {
                Debug.LogWarning("UndoManager: Could not find target spline to reconnect UI. This shouldn't happen anymore!");
            }
        }
    }

    private string GetSplineGuid(BezierSpline spline)
    {
        if (spline == null) return null;

        // Robust fallback: Check for both the private field and public property
        var field = typeof(BezierSpline).GetField("guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) return (string)field.GetValue(spline);

        var prop = typeof(BezierSpline).GetProperty("Guid", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop != null) return (string)prop.GetValue(spline);

        return null;
    }
}