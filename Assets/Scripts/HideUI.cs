using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HideUI : MonoBehaviour
{
    [Header("Either set uiRootCanvasGroup OR uiRootGameObject")]
    public CanvasGroup uiRootCanvasGroup; // Preferred: keeps objects alive

    public GameObject hideUIContainer;    // MUST be outside uiRoot
    public RectTransform hotZone;         // MUST be outside uiRoot or assigned to active object
    public Button hideUIButton;           // MUST be outside uiRoot (or inside but manager outside)

    private bool isUIHidden = false;

    void Start()
    {
        isUIHidden = false;
        if (hideUIButton != null)
            hideUIButton.onClick.AddListener(ShowHideUI);
        else
            Debug.LogWarning("HideUI: hideUIButton not assigned.");
    }

    void ShowHideUI()
    {
        if (!isUIHidden)
            HideEverything();
        else
            ShowEverything();
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

        Debug.Log("hide?");
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

        Debug.Log("show?");
    }

    void Update()
    {
        // Make sure this script and hotZone are on an always-active object
        if (hotZone == null || hideUIContainer == null) return;

        var mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        // If your Canvas is Screen Space - Camera, you may need to pass the canvas camera as the 3rd argument.
        bool inZone = RectTransformUtility.RectangleContainsScreenPoint(hotZone, mousePos, null);
        hideUIContainer.SetActive(inZone);
    }
}
