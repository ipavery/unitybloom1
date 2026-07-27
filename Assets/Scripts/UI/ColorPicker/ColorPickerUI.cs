// UI logic
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorPickerUI : MonoBehaviour
{
    private System.Action<Color> onPicked;
    private Color selectedColor;
    private Color initialColor;
    public Button confirmButton;
    public Button cancelButton;
    public Slider valueSlider;

    [Header("UI References")]
    [SerializeField] private RectTransform wheelRect;
    [SerializeField] private ColorWheelRenderer wheelRenderer;
    [SerializeField] private RectTransform colorDarkener;

    public void Open(Color initial, System.Action<Color> onPicked)
    {
        //Debug.Log($"initial color: {initial}");
        this.onPicked = onPicked;
        this.selectedColor = initial;
        this.initialColor = initial;
        gameObject.SetActive(true);
        // Update wheel visuals to match `initial`
        // Here I need to add something that will calculate the position of the pointer...
        valueSlider.minValue = 0;
        valueSlider.maxValue = 1;
        valueSlider.wholeNumbers = false;
        Color.RGBToHSV(initial, out float h, out float s, out float v);
        valueSlider.value = v;
        wheelRenderer.value = v;
        valueSlider.onValueChanged.AddListener(val =>
        {
            wheelRenderer.value = val;
            colorDarkener.GetComponent<Image>().color = new Color(0, 0, 0, 1 - val); 
            
            // Use the new method to get the properly calculated color
            OnWheelChanged(wheelRenderer.GetCurrentIndicatorColor(val));
        });

        //Add listeners for confirm and cancel buttons
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);

        colorDarkener.GetComponent<Image>().color = new Color(0, 0, 0, 1 - v); //initialize darkening rectangle
        wheelRenderer.Initialize();
        wheelRenderer.colorIndicator.anchoredPosition = wheelRenderer.ColorToXY(initial);
        
    }

    public void OnWheelChanged(Color newColor)
    {
        selectedColor = newColor;
        onPicked(newColor);
        Debug.Log(newColor);
    }

    public void OnConfirm()
    {
        onPicked?.Invoke(selectedColor);
        gameObject.SetActive(false);
    }

    public void OnCancel()
    {
        //Debug.Log($"Cancelling...color was {selectedColor}, initial was {initialColor}");
        onPicked(initialColor);
        gameObject.SetActive(false);
    }
}

