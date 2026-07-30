using UnityEngine;
using UnityEngine.InputSystem; 
using UnityEngine.UI;

public class HideUI : MonoBehaviour
{
    [Header("Either set uiRootCanvasGroup OR uiRootGameObject")]
    public CanvasGroup uiRootCanvasGroup; 
    public GameObject hideUIContainer;    
    public RectTransform hotZone;         
    public Button hideUIButton;           

    private bool isUIHidden = false;      
    private bool isForcedHidden = false;  

    void Start()
    {
        isUIHidden = false;
        if (hideUIButton != null)
            hideUIButton.onClick.AddListener(ShowHideUI);
        else
            Debug.LogWarning("HideUI: hideUIButton not assigned.");
    }

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
                // Un-force it: turn it back on if the main UI is showing OR if we are on mobile
                if (!isUIHidden || Application.isMobilePlatform) 
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
        if (hotZone == null || hideUIContainer == null) return;
        if (isForcedHidden) return;

        // 1. If the UI is currently visible, keep the button visible always.
        if (!isUIHidden)
        {
            if (!hideUIContainer.activeSelf) hideUIContainer.SetActive(true);
            return;
        }

        // 2. MOBILE LOGIC: No hover states exist, so always keep the button visible.
        if (Application.isMobilePlatform)
        {
            if (!hideUIContainer.activeSelf) hideUIContainer.SetActive(true);
            return;
        }

        // 3. DESKTOP LOGIC: Track the mouse and show/hide based on the hot zone.
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            
            bool inZone = RectTransformUtility.RectangleContainsScreenPoint(hotZone, mousePos, null);
            
            if (hideUIContainer.activeSelf != inZone)
            {
                hideUIContainer.SetActive(inZone);
            }
        }
    }
}