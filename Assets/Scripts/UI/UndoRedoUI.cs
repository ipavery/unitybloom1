using UnityEngine;
using UnityEngine.UI;

public class UndoRedoUI : MonoBehaviour
{
    [Header("Button References")]
    public Button undoButton;
    public Button redoButton;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        undoButton.onClick.AddListener(UndoButton);
        redoButton.onClick.AddListener(RedoButton);
    }

    void UndoButton()
    {
        // Debug.Log("Undo button clicked");
        UndoManager.Instance.Undo();
    }

    void RedoButton()
    {
        // Debug.Log("Redo button clicked");
        UndoManager.Instance.Redo();
    }
}
