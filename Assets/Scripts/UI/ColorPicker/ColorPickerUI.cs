// UI logic
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorPickerUI : MonoBehaviour
{
    private System.Action<Color> onPicked;
    private Color selectedColor;
    public Button confirmButton;
    public Button cancelButton;
    public Slider valueSlider;
    [SerializeField] private RectTransform wheelRect;
    [SerializeField] private ColorWheelRenderer wheelRenderer;

    void Start()
    {
        
        
    }

    public void Open(Color initial, System.Action<Color> onPicked)
    {
        this.onPicked = onPicked;
        this.selectedColor = initial;
        gameObject.SetActive(true);
        // Update wheel visuals to match `initial`
        valueSlider.minValue = 0;
        valueSlider.maxValue = 1;
        valueSlider.wholeNumbers = false;
        Color.RGBToHSV(initial, out float h, out float s, out float v);
        valueSlider.value = v;
        wheelRenderer.value = v;
        valueSlider.onValueChanged.AddListener(val =>
        {
            wheelRenderer.value = val;
            wheelRenderer.GenerateWheelTexture();
        });
    }

    public void OnWheelChanged(Color newColor)
    {
        selectedColor = newColor;
        onPicked(newColor);
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

