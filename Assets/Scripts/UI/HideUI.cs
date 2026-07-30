using UnityEngine;
using UnityEngine.InputSystem; // Using New Input System
using UnityEngine.UI;

public class HideUI : MonoBehaviour
{
    [Header("Either set uiRootCanvasGroup OR uiRootGameObject")]
    public CanvasGroup uiRootCanvasGroup; // Preferred: keeps objects alive

    public GameObject hideUIContainer;    // MUST be outside uiRoot
    public RectTransform hotZone;         // MUST be outside uiRoot or assigned to active object
    public Button hideUIButton;           // MUST be outside uiRoot (or inside but manager outside)

    private bool isUIHidden = false;
    
    // Flag to prevent the Update loop from overriding external scripts
    private bool isForcedHidden = false; 

    void Start()
    {
        isUIHidden = false;
        if (hideUIButton != null)
            hideUIButton.onClick.AddListener(ShowHideUI);
        else
            Debug.LogWarning("HideUI: hideUIButton not assigned.");
    }

    /// <summary>
    /// Allows external scripts to forcefully hide or unhide the button, 
    /// overriding the normal mouse-tracking logic.
    /// </summary>
    public void SetButtonForcedHidden(bool forceHide)
    {
        isForcedHidden = forceHide;
        
        if (hideUIContainer != null)
        {
            if (isForcedHidden)
            {
                hideUIContainer.SetActive(false);
            }
            else
            {
                // If we un-force it, turn it back on immediately if the main UI is showing.
                // Otherwise, let the Update loop handle turning it on based on mouse position.
                if (!isUIHidden) 
                {
                    hideUIContainer.SetActive(true);
                }
            }
        }
    }

    void ShowHideUI()
    {
        if (!isUIHidden)
        {
            HideEverything();
            EventHub.Publish(new HideShow3DUI(true));
        }
        else
        {
            EventHub.Publish(new HideShow3DUI(false));
            ShowEverything();
        }
    }

    void HideEverything()
    {
        isUIHidden = true;

        if (uiRootCanvasGroup != null)
        {
            uiRootCanvasGroup.alpha = 0f;
            uiRootCanvasGroup.interactable = false;
            uiRootCanvasGroup.blocksRaycasts = false;
        }
    }

    void ShowEverything()
    {
        isUIHidden = false;

        if (uiRootCanvasGroup != null)
        {
            uiRootCanvasGroup.alpha = 1f;
            uiRootCanvasGroup.interactable = true;
            uiRootCanvasGroup.blocksRaycasts = true;
        }
    }

    void Update()
    {
        // Make sure this script and hotZone are on an always-active object
        if (hotZone == null || hideUIContainer == null) return;

        // Skip all automatic logic if an external script has forced the button to hide
        if (isForcedHidden) return;

        // 1. If the UI is currently visible (toggle not active), keep the button visible always.
        if (!isUIHidden)
        {
            if (!hideUIContainer.activeSelf) 
            {
                hideUIContainer.SetActive(true);
            }
            return; // Skip the mouse tracking entirely while the UI is showing
        }

        // 2. If the UI is hidden, check the mouse position using ONLY the New Input System.
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            
            // If your Canvas is Screen Space - Camera, you may need to pass the canvas camera as the 3rd argument.
            bool inZone = RectTransformUtility.RectangleContainsScreenPoint(hotZone, mousePos, null);
            
            // Only update the active state if it has changed to prevent doing it every frame
            if (hideUIContainer.activeSelf != inZone)
            {
                hideUIContainer.SetActive(inZone);
            }
        }
        else 
        {
            // Fallback if no mouse is connected (e.g., touch or gamepad without virtual mouse)
            if (hideUIContainer.activeSelf)
            {
                hideUIContainer.SetActive(false);
            }
        }
    }
}