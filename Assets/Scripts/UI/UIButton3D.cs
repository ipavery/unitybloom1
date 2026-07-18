using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButton3D : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The RectTransform of the button's top face that will shift down when pressed.")]
    public RectTransform buttonFace;
    
    [Tooltip("How many pixels to shift the button face down when pressed.")]
    public float clickOffset = 4f;

    private Vector2 originalPosition;
    private bool isPressed = false;
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (buttonFace == null)
        {
            // If not assigned, look for a child named "ButtonFace"
            var faceTransform = transform.Find("ButtonFace");
            if (faceTransform != null)
                buttonFace = faceTransform.GetComponent<RectTransform>();
            else
                buttonFace = GetComponent<RectTransform>(); // fallback
        }
        
        if (buttonFace != null)
        {
            originalPosition = buttonFace.anchoredPosition;
            isInitialized = true;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Initialize();
        if (buttonFace == null) return;
        isPressed = true;
        buttonFace.anchoredPosition = originalPosition + new Vector2(0f, -clickOffset);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Initialize();
        if (buttonFace == null) return;
        isPressed = false;
        buttonFace.anchoredPosition = originalPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPressed && buttonFace != null)
        {
            Initialize();
            buttonFace.anchoredPosition = originalPosition + new Vector2(0f, -clickOffset);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonFace != null)
        {
            Initialize();
            buttonFace.anchoredPosition = originalPosition;
        }
    }

    void OnDisable()
    {
        isPressed = false;
        if (buttonFace != null && isInitialized)
        {
            buttonFace.anchoredPosition = originalPosition;
        }
    }
}

