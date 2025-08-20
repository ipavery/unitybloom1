// UI logic
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorPickerUI : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private System.Action<Color> onPicked;
    private Color selectedColor;
    public Button confirmButton;
    public Button cancelButton;
    public Slider valueSlider;
    [SerializeField] private RectTransform wheelRect;

    public void OnPointerDown(PointerEventData eventData) => HandleInput(eventData);
    public void OnDrag(PointerEventData eventData) => HandleInput(eventData);

    private void HandleInput(PointerEventData eventData)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRect, eventData.position, eventData.pressEventCamera, out local))
        {
            // Normalize: center = (0,0), radius = wheelRect.sizeDelta.x / 2
            float radius = wheelRect.sizeDelta.x * 0.5f;
            Vector2 normalized = local / radius;

            float angle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            float saturation = Mathf.Clamp01(normalized.magnitude);

            Color color = Color.HSVToRGB(angle / 360f, saturation, 1f);
            OnWheelChanged(color);
        }
    }

    public void Open(Color initial, System.Action<Color> onPicked)
    {
        this.onPicked = onPicked;
        this.selectedColor = initial;
        gameObject.SetActive(true);
        // Update wheel visuals to match `initial`
    }

    public void OnWheelChanged(Color newColor)
    {
        selectedColor = newColor;
        // Update preview UI here
    }

    public void OnConfirm()
    {
        onPicked?.Invoke(selectedColor);
        gameObject.SetActive(false);
    }

    public void OnCancel()
    {
        gameObject.SetActive(false);
    }
}

