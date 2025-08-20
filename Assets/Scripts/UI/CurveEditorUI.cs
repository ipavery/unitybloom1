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
    public GameObject colorPickerPanel;
    public RawImage colorPaletteImage;
    public RectTransform previousColorMarker;
    private int pickingColor = 0;

    [Header("Actions")]
    public Button selectButton;
    public Button deleteButton;
    public Button activateButton;

    [Header("Inspector Setup")]
    public List<InspectorField> fields = new List<InspectorField>();

    private BezierSpline selectedObject;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Texture2D paletteTexture;

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
        Debug.Log("Delete event triggered for " + e.spline.name);
        particleManager.splineParticleGroup.RemoveAll(splineGroup => splineGroup.spline == e.spline);
        Destroy(e.spline.gameObject);
        DeselectObject();
        Debug.Log("reloading now");
        ReloadParticles();
    }


    void ReloadParticles()
    {
        EventHub.Publish(new ReloadParticles(true));
    }

    void Start()
    {
        inspectorPanel.SetActive(false);
        colorPickerPanel.SetActive(false);

        // Setup color picker
        paletteTexture = colorPaletteImage.texture as Texture2D;
        colorPickerButton.onClick.AddListener(ColorButton1);
        colorPickerButton2.onClick.AddListener(ColorButton2);
        colorPaletteImage.GetComponent<Button>().onClick.AddListener(OnPaletteClicked);

        // Action buttons
        selectButton.onClick.AddListener(OnSelectClicked);
        deleteButton.onClick.AddListener(OnDeleteClicked);
        activateButton.onClick.AddListener(ActivateButton);
    }

    void ColorButton1()
    {
        ColorPickerManager.Instance.Show(originalColor, OnColorPicked, gameObject);
        pickingColor = 1;
    }
    void ColorButton2()
    {
        
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
    }

    void ActivateButton()
    {

        selectedObject.isActive = !selectedObject.isActive; // flips between true and false
        ReloadParticles();
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

            // Sync slider -> input -> property
            slider.onValueChanged.AddListener(val =>
            {
                input.text = val.ToString("0.##");
                f.setter(val);
                ReloadParticles();
            });
            input.onEndEdit.AddListener(str =>
            {
                if (float.TryParse(str, out float v))
                {
                    v = Mathf.Clamp(v, f.minMax.x, f.minMax.y);
                    slider.value = v;
                    f.setter(v);
                    ReloadParticles();
                }
            });
        }
    }

    bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    void SelectObject(SplineSelectionChange e)
    {
        if (e.isSelected == true)
        {
            selectedObject = e.spline;
            Vector2 uv = new Vector2(originalColor.r, originalColor.g); // simplistic example
            previousColorMarker.anchoredPosition = uv * colorPaletteImage.rectTransform.sizeDelta;

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
                minMax = new(2,60)
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
        selectedObject = null;
        inspectorPanel.SetActive(false);
        colorPickerPanel.SetActive(false);
    }

    void OnPaletteClicked()
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            colorPaletteImage.rectTransform,
            Input.mousePosition,
            uiCanvas.worldCamera,
            out localPos);

        // Convert to UV
        Rect rect = colorPaletteImage.rectTransform.rect;
        float u = (localPos.x - rect.x) / rect.width;
        float v = (localPos.y - rect.y) / rect.height;
        Color c = paletteTexture.GetPixelBilinear(u, v);

        // Apply color
        if (pickingColor == 1)
            selectedObject.lerpColor1 = c;
        if (pickingColor == 2)
            selectedObject.lerpColor2 = c;
        pickingColor = 0;
        ReloadParticles();

        colorPickerPanel.SetActive(false);
    }

    void OnSelectClicked() => EventHub.Publish(new SelectSpline(selectedObject));
    void OnDeleteClicked()
    {

        EventHub.Publish(new DeleteSpline(selectedObject));

    }
}
