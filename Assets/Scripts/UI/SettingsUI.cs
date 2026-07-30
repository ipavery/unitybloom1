using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public Button settingsButton;
    public RectTransform settingsPanel;

    [Header("Selection Mode")]
    public TMP_Dropdown selectionModeDropdown;
    public SplinePickerData SPD;

    [Header("Spline Draw Freq input/slider")]
    public Slider myFloatSlider;
    public TMP_InputField myFloatInputField;
    public float myFloatMin = 0f;
    public float myFloatMax = 2f;
    public DrawSpline drawSpline;

    [Header("Control Point Size Slider")]
    public Slider controlPointSlider;
    public TMP_InputField controlPointInput;
    public float controlPointMin = .1f;
    public float controlPointMax = 10f;
    public ControlPointController controlPointController;

    [Header("Mouse Look Settings")]
    public MouseLook mouseLook;
    public TMP_Dropdown lookModeDropdown;

    void Start()
    {
        settingsPanel.gameObject.SetActive(false);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        
        // --- 1. Selection Mode Dropdown Setup (0 = DirectDrag, 1 = Classic) ---
        selectionModeDropdown.onValueChanged.AddListener(OnSelectionModeChanged);
        
        if (SPD.currentInteractionMode == SplinePickerData.InteractionMode.DirectDrag)
        {
            selectionModeDropdown.SetValueWithoutNotify(0); 
        }
        else if (SPD.currentInteractionMode == SplinePickerData.InteractionMode.Classic)
        {
            selectionModeDropdown.SetValueWithoutNotify(1); 
        }

        // --- 2. Look Mode Dropdown Setup ---
        lookModeDropdown.onValueChanged.AddListener(OnLookModeChanged);
        
        if (mouseLook.controlMode == ControlMode.Pan)
        {
            lookModeDropdown.SetValueWithoutNotify(0); 
        }
        else if (mouseLook.controlMode == ControlMode.Fixed)
        {
            lookModeDropdown.SetValueWithoutNotify(1); 
        }
        else if (mouseLook.controlMode == ControlMode.Fly)
        {
            lookModeDropdown.SetValueWithoutNotify(2); 
        }

        // --- Spline Draw Freq Setting Setup ---
        myFloatSlider.minValue = myFloatMin;
        myFloatSlider.maxValue = myFloatMax;

        UpdateFloatUIWithoutNotify(drawSpline.errorThreshold);

        myFloatSlider.onValueChanged.AddListener(OnSliderValueChanged);
        myFloatInputField.onEndEdit.AddListener(OnInputFieldValueChanged);

        // --- Control Point Size Setting Setup ---
        controlPointSlider.minValue = controlPointMin;
        controlPointSlider.maxValue = controlPointMax;

        // Initialize UI with the current value silently
        // CHANGE 'SPD.controlPointScale' TO YOUR ACTUAL TARGET FLOAT
        UpdateControlPointUIWithoutNotify(SPD.controlPointScale);

        // Hook up the listeners
        controlPointSlider.onValueChanged.AddListener(OnControlPointSliderValueChanged);
        controlPointInput.onEndEdit.AddListener(OnControlPointInputFieldValueChanged);
    }

    void OnSettingsClicked()
    {
        settingsPanel.gameObject.SetActive(!settingsPanel.gameObject.activeSelf);
    }

    void OnSelectionModeChanged(int dropdownIndex)
    {
        // 0 = DirectDrag, 1 = Classic
        if (dropdownIndex == 0)
        {
            SPD.currentInteractionMode = SplinePickerData.InteractionMode.DirectDrag;
        }
        else if (dropdownIndex == 1)
        {
            SPD.currentInteractionMode = SplinePickerData.InteractionMode.Classic;
        }
    }
    
    void OnLookModeChanged(int dropdownIndex)
    {
        // 0 = Classic, 1 = DirectDrag (Wait, comment says 0 = Classic, 1 = DirectDrag, but code assigns differently. Kept your original logic)
        if (dropdownIndex == 0)
        {
            mouseLook.controlMode = ControlMode.Pan;
        }
        else if (dropdownIndex == 1)
        {
            mouseLook.controlMode = ControlMode.Fixed;
        }
        else if (dropdownIndex == 2)
        {
            mouseLook.controlMode = ControlMode.Fly;
        }
    }

    // --- Float Setting Logic (Spline Draw Freq) ---

    private void OnSliderValueChanged(float newValue)
    {
        drawSpline.errorThreshold = newValue; 
        myFloatInputField.SetTextWithoutNotify(newValue.ToString("0.##"));
    }

    private void OnInputFieldValueChanged(string textInput)
    {
        if (float.TryParse(textInput, out float parsedValue))
        {
            float clampedValue = Mathf.Clamp(parsedValue, myFloatMin, myFloatMax);
            drawSpline.errorThreshold = clampedValue; 
            UpdateFloatUIWithoutNotify(clampedValue);
        }
        else
        {
            myFloatInputField.SetTextWithoutNotify(drawSpline.errorThreshold.ToString("0.##"));
        }
    }

    private void UpdateFloatUIWithoutNotify(float value)
    {
        myFloatSlider.SetValueWithoutNotify(value);
        myFloatInputField.SetTextWithoutNotify(value.ToString("0.##"));
    }

    // --- Control Point Size Logic ---

    private void OnControlPointSliderValueChanged(float newValue)
    {
        // 1. Update the actual data
        // CHANGE 'SPD.controlPointScale' TO YOUR ACTUAL TARGET FLOAT
        SPD.controlPointScale = newValue;
        controlPointController.RefreshControlPointScales();
        // 2. Update the input field silently to prevent infinite feedback loops (which causes the drop-to-0 bug)
        controlPointInput.SetTextWithoutNotify(newValue.ToString("0.##"));
    }

    private void OnControlPointInputFieldValueChanged(string textInput)
    {
        // Safely parse the text. If they typed garbage, it fails gracefully.
        if (float.TryParse(textInput, out float parsedValue))
        {
            // Clamp the typed value to your strict limits
            float clampedValue = Mathf.Clamp(parsedValue, controlPointMin, controlPointMax);

            // 1. Update the actual data
            // CHANGE 'SPD.controlPointScale' TO YOUR ACTUAL TARGET FLOAT
            SPD.controlPointScale = clampedValue; 
            controlPointController.RefreshControlPointScales();
            // 2. Update both UI elements silently
            UpdateControlPointUIWithoutNotify(clampedValue);
        }
        else
        {
            // If parsing failed, revert the text box to the last known good value
            // CHANGE 'SPD.controlPointScale' TO YOUR ACTUAL TARGET FLOAT
            controlPointInput.SetTextWithoutNotify(SPD.controlPointScale.ToString("0.##"));
        }
    }

    private void UpdateControlPointUIWithoutNotify(float value)
    {
        controlPointSlider.SetValueWithoutNotify(value);
        controlPointInput.SetTextWithoutNotify(value.ToString("0.##"));
    }
}