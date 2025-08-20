// Reusable entry point
using UnityEngine;

public class ColorPickerManager : MonoBehaviour
{
    public static ColorPickerManager Instance { get; private set; }
    [SerializeField] private GameObject colorPickerPrefab;

    private ColorPickerUI currentPicker;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Show(Color initial, System.Action<Color> onPicked, GameObject pickerParent)
    {
        if (currentPicker == null)
        {
            var pickerGO = Instantiate(colorPickerPrefab, pickerParent.transform);
            currentPicker = pickerGO.GetComponent<ColorPickerUI>();
        }
        currentPicker.Open(initial, onPicked);
    }
}
