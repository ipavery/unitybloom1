using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }
    
    public SaveLoadUI saveLoadUI;
    public SplinePickerData SPD; // Assign this in the Inspector!
    
    private Stack<string> undoStack = new Stack<string>();
    private Stack<string> redoStack = new Stack<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        // Simple InputSystem check for Ctrl+Z and Ctrl+Y
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
        if (undoStack.Count == 0) return;
        redoStack.Push(JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot()));
        RestoreState(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        undoStack.Push(JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot()));
        RestoreState(redoStack.Pop());
    }

    private void RestoreState(string json)
    {
        // ==========================================
        // STEP 1: CAPTURE SELECTION BEFORE WIPE
        // ==========================================
        string activeSplineGuid = null;
        List<int> selectedIndices = new List<int>();

        if (SPD != null) 
        {
            foreach (var group in SPD.controlSphereGroup) 
            {
                if (group.isSelected) 
                {
                    var spline = group.sphereObject.GetComponentInParent<BezierSpline>();
                    if (spline != null) 
                    {
                        activeSplineGuid = GetSplineGuid(spline);
                        selectedIndices.Add(group.index);
                    }
                }
            }
        } else
        {
            Debug.LogError("SplinePickerData (SPD) is not assigned in UndoManager. Please assign it in the Inspector.");
        }

        // ==========================================
        // STEP 2: CLEAR AND LOAD SNAPSHOT
        // ==========================================
        EventHub.Publish(new ClearSelection()); 
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        saveLoadUI.LoadSaveDataInMemory(data);
        
        // ==========================================
        // STEP 3: RESTORE SELECTION TO NEW OBJECTS
        // ==========================================
        if (activeSplineGuid != null && SPD != null) 
        {
            BezierSpline targetSpline = null;
            
            // Find the newly spawned spline with the matching GUID
            foreach (var spline in FindObjectsByType<BezierSpline>(FindObjectsSortMode.None)) 
            {
                if (GetSplineGuid(spline) == activeSplineGuid) 
                {
                    targetSpline = spline;
                    break;
                }
            }

            // If the spline still exists in this timeline, re-select it
            if (targetSpline != null) 
            {
                // Re-open the UI Inspector (CurveEditorUI)
                EventHub.Publish(new SplineSelectionChange(targetSpline, true));

                GameObject firstHighlighted = null;

                // Re-select the specific control points for the gizmos
                foreach (var group in SPD.controlSphereGroup) 
                {
                    var spline = group.sphereObject.GetComponentInParent<BezierSpline>();
                    if (spline == targetSpline && selectedIndices.Contains(group.index)) 
                    {
                        group.isSelected = true;
                        if (firstHighlighted == null) firstHighlighted = group.sphereObject;
                        
                        // Re-apply visual highlight to the sphere // IDEA make this into a method in controlpointcontroller so that it can be reused easily
                        if (group.sphereObject.TryGetComponent<Renderer>(out var rend)) 
                        {
                            rend.material.color = SPD.highlightColor;
                            rend.material.SetColor("_EmissionColor", SPD.highlightColor * SPD.gizmoEmissionIntensity);
                        }
                    }
                }

                // Re-enable Gizmos on the highlighted point
                if (firstHighlighted != null) 
                {
                    SPD.lastHighlighted = firstHighlighted;
                    EventHub.Publish(new ShowMoveGizmosEvent(firstHighlighted.transform.position));
                }
            }
        }
    }

    /// <summary>
    /// Helper method to read the private 'guid' field from a BezierSpline using Reflection.
    /// This allows us to match the destroyed spline to the newly created one.
    /// </summary>
    private string GetSplineGuid(BezierSpline spline)
    {
        var field = typeof(BezierSpline).GetField("guid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (string)field.GetValue(spline) : null;
    }
}