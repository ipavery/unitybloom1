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
            lookModeDropdown.SetValueWithoutNotify(0); // FIX: Target the correct dropdown
        }
        else if (mouseLook.controlMode == ControlMode.Fixed)
        {
            lookModeDropdown.SetValueWithoutNotify(1); // FIX: Target the correct dropdown
        }
        else if (mouseLook.controlMode == ControlMode.Fly)
        {
            lookModeDropdown.SetValueWithoutNotify(2); // FIX: Target the correct dropdown
        }

        // --- Float Setting Setup ---
        myFloatSlider.minValue = myFloatMin;
        myFloatSlider.maxValue = myFloatMax;

        // Initialize UI with the current value silently
        UpdateFloatUIWithoutNotify(drawSpline.errorThreshold);

        // Hook up the listeners
        myFloatSlider.onValueChanged.AddListener(OnSliderValueChanged);
        myFloatInputField.onEndEdit.AddListener(OnInputFieldValueChanged);
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
        // 0 = Classic, 1 = DirectDrag
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

    // --- Float Setting Logic ---

    private void OnSliderValueChanged(float newValue)
    {
        // 1. Update the actual data
        // CHANGE THIS TO YOUR ACTUAL TARGET FLOAT
        drawSpline.errorThreshold = newValue; 

        // 2. Update the input field silently to prevent infinite feedback loops (which causes the drop-to-0 bug)
        myFloatInputField.SetTextWithoutNotify(newValue.ToString("0.##"));
    }

    private void OnInputFieldValueChanged(string textInput)
    {
        // Safely parse the text. If they typed garbage, it fails gracefully.
        if (float.TryParse(textInput, out float parsedValue))
        {
            // Clamp the typed value to your strict limits
            float clampedValue = Mathf.Clamp(parsedValue, myFloatMin, myFloatMax);

            // 1. Update the actual data
            // CHANGE THIS TO YOUR ACTUAL TARGET FLOAT
            drawSpline.errorThreshold = clampedValue; 

            // 2. Update both UI elements silently
            UpdateFloatUIWithoutNotify(clampedValue);
        }
        else
        {
            // If parsing failed, revert the text box to the last known good value
            // CHANGE 'SPD.gizmoEmissionIntensity' TO YOUR ACTUAL TARGET FLOAT
            myFloatInputField.SetTextWithoutNotify(drawSpline.errorThreshold.ToString("0.##"));
        }
    }

    private void UpdateFloatUIWithoutNotify(float value)
    {
        myFloatSlider.SetValueWithoutNotify(value);
        myFloatInputField.SetTextWithoutNotify(value.ToString("0.##"));
    }
}