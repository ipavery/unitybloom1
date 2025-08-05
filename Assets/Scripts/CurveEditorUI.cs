using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

[Serializable]
public class InspectorField
{
    public string fieldName;
    public Func<float> getter;
    public Action<float> setter;
    public Vector2 minMax;
}

public class CurveEditorUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas uiCanvas;
    public GameObject inspectorPanel;
    public RectTransform fieldContainer; // VerticalLayoutGroup container
    public GameObject fieldPrefab;      // prefab with Slider + InputField

    [Header("Color Picker")]
    public Button colorPickerButton;
    public GameObject colorPickerPanel;
    public RawImage colorPaletteImage;
    public RectTransform previousColorMarker;

    [Header("Actions")]
    public Button selectButton;
    public Button deleteButton;

    [Header("Inspector Setup")]
    public List<InspectorField> fields = new List<InspectorField>();

    private BezierSpline selectedObject;
    private Renderer selectedRenderer;
    private Color originalColor;
    private Texture2D paletteTexture;

    void OnEnable()
    {
        EventHub.Subscribe<SplineSelectionChange>(SelectObject);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<SplineSelectionChange>(SelectObject);
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
        colorPickerButton.onClick.AddListener(() => colorPickerPanel.SetActive(true));
        colorPaletteImage.GetComponent<Button>().onClick.AddListener(OnPaletteClicked);

        // Action buttons
        selectButton.onClick.AddListener(OnSelectClicked);
        deleteButton.onClick.AddListener(OnDeleteClicked);
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
            var slider = go.GetComponentInChildren<Slider>();
            var input = go.GetComponentInChildren<TMP_InputField>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            
            label.text = f.fieldName;
            slider.minValue = f.minMax.x;
            slider.maxValue = f.minMax.y;
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

    void Update()
    {

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
            InitializeFields(new List<InspectorField>
        {
            new InspectorField
            {
                fieldName = "Symmetry",
                getter = () => selectedObject.splineSymmetry,
                setter = v => { var p = selectedObject.splineSymmetry; p = (int)v; selectedObject.splineSymmetry = p; },
                minMax = new(1,50)
            },
            new InspectorField
            {
                fieldName = "Quantity",
                getter = () => selectedObject.frequency,
                setter = v => { var p = selectedObject.frequency; p = (int)v; selectedObject.frequency = p; },
                minMax = new(1,60)
            },
            new InspectorField
            {
                fieldName = "Speed",
                getter = () => selectedObject.s_life,
                setter = v => { var p = selectedObject.s_life; p = v; selectedObject.s_life = p; },
                minMax = new(.4f,8)
            }
        });

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
        selectedObject.lerpColor1 = c;
        ReloadParticles();
        colorPickerPanel.SetActive(false);
    }

    void OnSelectClicked() => Debug.Log("Select event triggered for " + selectedObject.name);
    void OnDeleteClicked()
    {
        Debug.Log("Delete event triggered for " + selectedObject.name);
        Destroy(selectedObject);
        DeselectObject();
    }
}
