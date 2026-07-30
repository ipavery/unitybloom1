using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("Global Toggle")]
    [Tooltip("Uncheck this to globally disable all tooltips.")]
    public bool tooltipsEnabled = true;

    [Header("References")]
    public TextMeshProUGUI tooltipText;
    public RectTransform backgroundRect; 
    
    [Header("Settings")]
    public float showDelay = 0.5f; 
    public Vector2 offset = new Vector2(15f, -15f); 
    
    [Tooltip("Extra space around the text (Width, Height)")]
    public Vector2 padding = new Vector2(20f, 10f); // <-- Added padding control

    private Canvas parentCanvas;
    private Coroutine showCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        parentCanvas = GetComponentInParent<Canvas>();
        
        if (backgroundRect != null) backgroundRect.gameObject.SetActive(false); 
    }

    private void Update()
    {
        if (backgroundRect == null || !backgroundRect.gameObject.activeSelf) return;

        if (Mouse.current != null)
        {
            UpdatePosition(Mouse.current.position.ReadValue());
        }
    }

    public void QueueTooltip(string text)
    {
        if (!tooltipsEnabled) return;

        if (showCoroutine != null) StopCoroutine(showCoroutine);
        
        showCoroutine = StartCoroutine(ShowAfterDelay(text));
    }

    private IEnumerator ShowAfterDelay(string text)
    {
        yield return new WaitForSeconds(showDelay);
        
        tooltipText.text = text;

        // Force TextMeshPro to calculate its bounds based on the new text
        tooltipText.ForceMeshUpdate();
        
        // Brute-force the background size based on the text's preferred dimensions plus padding
        Vector2 textSize = new Vector2(tooltipText.preferredWidth, tooltipText.preferredHeight);
        backgroundRect.sizeDelta = textSize + padding;

        backgroundRect.gameObject.SetActive(true);
        
        if (Mouse.current != null)
        {
            UpdatePosition(Mouse.current.position.ReadValue());
        }
    }

    public void HideTooltip()
    {
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        
        if (backgroundRect != null) backgroundRect.gameObject.SetActive(false);
    }

    private void UpdatePosition(Vector2 mousePos)
    {
        Vector2 pivot = new Vector2(0f, 1f);
        Vector2 position = mousePos + offset;

        float scaleFactor = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
        
        // Use the newly calculated sizeDelta for boundary checking
        float width = backgroundRect.sizeDelta.x * scaleFactor;
        float height = backgroundRect.sizeDelta.y * scaleFactor;

        if (position.x + width > Screen.width)
        {
            pivot.x = 1f;
            position.x = mousePos.x - offset.x; 
        }

        if (position.y - height < 0)
        {
            pivot.y = 0f;
            position.y = mousePos.y - offset.y; 
        }

        backgroundRect.pivot = pivot;
        backgroundRect.position = position; 
    }
}