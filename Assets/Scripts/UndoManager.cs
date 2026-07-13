using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }
    
    public SaveLoadUI saveLoadUI;
    
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
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                Debug.Log("Undoing last action from keybind");
                Undo();
            }
            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                Debug.Log("Redoing last action from keybind");
                Redo();
            }
        }
    }

    /// <summary>
    /// Call this right BEFORE any script applies a change to the scene.
    /// </summary>
    public void RecordState()
    {
        string jsonState = JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot());
        undoStack.Push(jsonState);
        redoStack.Clear(); // A new action invalidates the redo future
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("Undo stack is empty, nothing to undo.");
            return;
        }

        // Save current state to Redo stack
        string currentState = JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot());
        redoStack.Push(currentState);

        // Pop and load previous state
        string previousState = undoStack.Pop();
        RestoreState(previousState);
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("Redo stack is empty, nothing to redo.");
            return;
        }

        // Save current state to Undo stack
        string currentState = JsonUtility.ToJson(saveLoadUI.BuildSaveDataFromRoot());
        undoStack.Push(currentState);

        // Pop and load next state
        string nextState = redoStack.Pop();
        RestoreState(nextState);
    }

    private void RestoreState(string json)
    {
        // Clear selection to prevent missing reference errors after objects are destroyed and rebuilt
        EventHub.Publish(new ClearSelection()); 
        
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        saveLoadUI.LoadSaveDataInMemory(data);
    }
}