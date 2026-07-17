using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using System.Runtime.InteropServices;

[Serializable]
public class InspectorField
{
    public string fieldName;
    public Func<float> getter;
    public Action<float> setter;
    public Vector2 minMax;
    public bool wholeNumbers = true;
}

public class CurveEditorUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas uiCanvas;
    public GameObject inspectorPanel;
    public RectTransform fieldContainer; // VerticalLayoutGroup container
    public GameObject fieldPrefab;      // prefab with Slider + InputField
    public ParticleManager particleManager;

    [Header("Color Picker")]
    public Button colorPickerButton;
    public Button colorPickerButton2;
    private int pickingColor = 0;

    [Header("Actions")]
    public Button selectButton;
    public Button deleteButton;
    public Button activateButton;
    public Button copyButton;

    [Header("Inspector Setup")]
    public List<InspectorField> fields = new List<InspectorField>();
    public BezierSpline selectedObject;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Texture2D paletteTexture;

    [Header("Copy Setup")]
    public BezierSpline splinePrefab; // Prefab for copying splines
    public GameObject drawSplineContainer; //where the new splines go

    void OnEnable()
    {
        EventHub.Subscribe<SplineSelectionChange>(SelectObject);
        EventHub.Subscribe<DeleteSpline>(OnDeleteSpline);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<SplineSelectionChange>(SelectObject);
        EventHub.Unsubscribe<DeleteSpline>(OnDeleteSpline);
    }

    void OnDeleteSpline(DeleteSpline e)
    {
        particleManager.splineParticleGroup.RemoveAll(splineGroup => splineGroup.spline == e.spline);
        Destroy(e.spline.gameObject);
        DeselectObject();
        ReloadParticles();
    }


    void ReloadParticles()
    {
        EventHub.Publish(new ReloadParticles(true));
    }

    void Start()
    {
        inspectorPanel.SetActive(false);

        colorPickerButton.onClick.AddListener(ColorButton1);
        colorPickerButton2.onClick.AddListener(ColorButton2);

        // Action buttons
        selectButton.onClick.AddListener(OnSelectClicked);
        deleteButton.onClick.AddListener(OnDeleteClicked);
        activateButton.onClick.AddListener(ActivateButton);
        copyButton.onClick.AddListener(OnCopyClicked);
    }

    void Update()
    {
        if(selectedObject == null && inspectorPanel.activeSelf)
        {
            Debug.Log("selectedObject is null in CurveEditorUI Update and the panel is open");
        }
    }

    void ColorButton1()
    {
        ColorPickerManager.Instance.Show(selectedObject.lerpColor1, OnColorPicked, inspectorPanel);
        pickingColor = 1;
    }
    void ColorButton2()
    {
        ColorPickerManager.Instance.Show(selectedObject.lerpColor2, OnColorPicked, inspectorPanel);
        pickingColor = 2;
    }

    void OnColorPicked(Color pickedColor)
    {
        if (pickingColor == 1)
        {
            selectedObject.lerpColor1 = pickedColor;
        }
        else if (pickingColor == 2)
        {
            selectedObject.lerpColor2 = pickedColor;
        }
        ReloadParticles();
    }

    void ActivateButton()
    {
        if (selectedObject == null)
        {
            Debug.LogWarning("No spline selected to activate/deactivate.");
            return;
        }
        selectedObject.isActive = !selectedObject.isActive; // flips between true and false
        ReloadParticles();
    }

    void OnCopyClicked()
    {
        if (selectedObject == null)
        {
            Debug.LogWarning("No spline selected to copy.");
            return;
        }
        UndoManager.Instance.RecordState(); // Record the state before copying for undo functionality

        BezierSpline newSpline = Instantiate(splinePrefab, selectedObject.transform.position + Vector3.down * 2, Quaternion.identity); //blank prefab
        newSpline.CopyFrom(selectedObject); //copy spline settings
        newSpline.points = (Vector3[])selectedObject.points.Clone(); // Copy the points array (make a new one so the points aren't linked)
        newSpline.name += "_Copy";
        EventHub.Publish(new NewSplineCreated(newSpline));
    }

    public void InitializeFields(List<InspectorField> inspectorFields)
    {
        // Clear existing
        foreach (Transform child in fieldContainer) Destroy(child.gameObject);
        fields = inspectorFields;

        // Generate UI
        foreach (var f in fields)
        {
            var go = Instantiate(fieldPrefab, fieldContainer);
            go.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            go.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0);
            var slider = go.GetComponentInChildren<Slider>();
            var input = go.GetComponentInChildren<TMP_InputField>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();

            label.text = f.fieldName;
            slider.minValue = f.minMax.x;
            slider.maxValue = f.minMax.y;
            slider.wholeNumbers = f.wholeNumbers;
            slider.value = f.getter();
            input.contentType = TMP_InputField.ContentType.EmailAddress;
            input.text = f.getter().ToString("0.##");

            // --- UNDO SYSTEM: Record state on Slider click (PointerDown) ---
            EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slider.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
            pointerDownEntry.eventID = EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => 
            {
                UndoManager.Instance.RecordState();
            });
            trigger.triggers.Add(pointerDownEntry);
            // ---------------------------------------------------------------

            // Sync slider -> input -> property
            slider.onValueChanged.AddListener(val =>
            {
                input.text = val.ToString("0.##");
                f.setter(val);
                ReloadParticles();
            });

            // Input field handles its own undo record state when the user finishes typing
            input.onEndEdit.AddListener(str =>
            {
                if (float.TryParse(str, out float v))
                {
                    UndoManager.Instance.RecordState(); // Record the state before changing the value for undo functionality
                    v = Mathf.Clamp(v, f.minMax.x, f.minMax.y);
                    slider.value = v;
                    f.setter(v);
                    ReloadParticles();
                }
            });
        }
    }

    void SelectObject(SplineSelectionChange e)
    {
        if (e.isSelected == true)
        {
            selectedObject = e.spline;
            Vector2 uv = new Vector2(originalColor.r, originalColor.g); // simplistic example

            // Example fields: position X/Y/Z

            var fields = new List<InspectorField>
            {
                new InspectorField
                {
                    fieldName = "Symmetry",
                    getter = () => selectedObject.splineSymmetry,
                    setter = v => { var p = selectedObject.splineSymmetry; p = (int)v; selectedObject.splineSymmetry = p; },
                    minMax = new(1,70)
                },
                new InspectorField
                {
                    fieldName = "Quantity",
                    getter = () => selectedObject.frequency,
                    setter = v => { var p = selectedObject.frequency; p = (int)v; selectedObject.frequency = p; },
                    minMax = new(2,100)
                },
                new InspectorField
                {
                    fieldName = "Lifetime",
                    getter = () => selectedObject.s_life,
                    setter = v => { var p = selectedObject.s_life; p = v; selectedObject.s_life = p; },
                    minMax = new(.2f,4),
                    wholeNumbers = false
                },
                new InspectorField
                {
                    fieldName = "Offset",
                    getter = () => selectedObject.lifetimeOffset,
                    setter = v => { var p = selectedObject.lifetimeOffset; p = v; selectedObject.lifetimeOffset = p; },
                    minMax = new(0,.5f),
                    wholeNumbers = false
                },
                new InspectorField
                {
                    fieldName = "Repeats",
                    getter = () => selectedObject.lerpTimes,
                    setter = v => { var p = selectedObject.lerpTimes; p = (int)v; selectedObject.lerpTimes = p; },
                    minMax = new(1,30)
                },
                new InspectorField
                {
                    fieldName = "Repeat Offset",
                    getter = () => selectedObject.lerpLifetimeOffset,
                    setter = v => { var p = selectedObject.lerpLifetimeOffset; p = v; selectedObject.lerpLifetimeOffset = p; },
                    minMax = new(0,.5f),
                    wholeNumbers = false
                }
            };
            if (particleManager.spawnMode == ParticleSpawnMode.Music)
            {
                fields.Add(new InspectorField
                {
                    fieldName = "Music Activation Intensity",
                    getter = () => selectedObject.activationThreshhold,
                    setter = v => { var p = selectedObject.activationThreshhold; p = v; selectedObject.activationThreshhold = p; },
                    minMax = new(0, 200),
                    wholeNumbers = false
                });
                fields.Add(new InspectorField
                {
                    fieldName = "Music Channel",
                    getter = () => selectedObject.musicChannel,
                    setter = v => { var p = selectedObject.musicChannel; p = (int)v; selectedObject.musicChannel = p; },
                    minMax = new(0, 7),
                });
            }
            InitializeFields(fields);
            inspectorPanel.SetActive(true);
        }
        else if (e.isSelected == false)
        {
            DeselectObject();
        }
    }

    void DeselectObject()
    {
        // Deselect object and hide color picker inspector
        selectedObject = null;
        ColorPickerManager.Instance.Close();
        inspectorPanel.SetActive(false);
    }

    void OnSelectClicked() => EventHub.Publish(new SelectSpline(selectedObject));
    void OnDeleteClicked()
    {
        UndoManager.Instance.RecordState(); // Record the state before deletion for undo functionality
        EventHub.Publish(new DeleteSpline(selectedObject));
    }
}
