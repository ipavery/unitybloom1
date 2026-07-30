// UI logic
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections; // <-- Added to allow Coroutines

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

    [Header("Optimization")]
    [Tooltip("How often the color picker updates the particle system (in seconds)")]
    public float updateThrottle = 0.1f; 
    private bool isThrottling = false;
    private Coroutine throttleCoroutine;

    public void Open(Color initial, System.Action<Color> onPicked)
    {
        this.onPicked = onPicked;
        this.selectedColor = initial;
        this.initialColor = initial;
        gameObject.SetActive(true);

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
        // Always store the absolute newest color immediately
        selectedColor = newColor;

        // If we aren't currently waiting on a cooldown, start one
        if (!isThrottling)
        {
            throttleCoroutine = StartCoroutine(ThrottleUpdate());
        }
    }

    private IEnumerator ThrottleUpdate()
    {
        isThrottling = true;
        
        // Wait for the cooldown duration
        yield return new WaitForSeconds(updateThrottle);
        
        // Broadcast the absolute latest color selected during the wait time
        onPicked?.Invoke(selectedColor);
        
        isThrottling = false;
    }

    public void OnConfirm()
    {
        if (throttleCoroutine != null) StopCoroutine(throttleCoroutine);
        onPicked?.Invoke(selectedColor);
        gameObject.SetActive(false);
    }

    public void OnCancel()
    {
        if (throttleCoroutine != null) StopCoroutine(throttleCoroutine);
        onPicked?.Invoke(initialColor);
        gameObject.SetActive(false);
    }
}