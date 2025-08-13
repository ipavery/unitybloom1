using System.Net;
using UnityEngine;
using UnityEngine.UI;

public class SelectionBoxUI : MonoBehaviour
{
    public RectTransform selectionBox;
    private Vector2 startPos;
    private Vector2 endPos;
    private bool isSelecting = false;
    public Canvas canvas;               // The Canvas it’s on

    public void BeginSelection(Vector2 screenStart)
    {
        //ScreenToLocal(screenStart, out startPos);
        startPos = screenStart / canvas.GetComponent<RectTransform>().localScale;
        Debug.Log($"startpos:{startPos}");
        isSelecting = true;
        selectionBox.gameObject.SetActive(true);
    }

    public void UpdateSelection(Vector2 screenEnd)
    {
        if (!isSelecting) return;

        //ScreenToLocal(screenEnd, out endPos);
        endPos = screenEnd / canvas.GetComponent<RectTransform>().localScale;
        Vector2 lowerLeft = Vector2.Min(startPos, endPos);
        Vector2 upperRight = Vector2.Max(startPos, endPos);



        selectionBox.anchoredPosition = lowerLeft;
        selectionBox.sizeDelta = upperRight - lowerLeft;
    }

    public void EndSelection()
    {
        isSelecting = false;
        selectionBox.gameObject.SetActive(false);
    }

    public void Start()
    {
        selectionBox.gameObject.SetActive(false);
        selectionBox.sizeDelta = Vector2.zero;
        selectionBox.anchoredPosition = Vector2.zero;
    }

    private void ScreenToLocal(Vector2 screenPos, out Vector2 localPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            selectionBox.parent as RectTransform,
            screenPos,
            null, // overlay canvas → no camera
            out localPos
        );
    }
}

