using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorWheelRenderer : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public void OnPointerDown(PointerEventData eventData) => HandleInput(eventData);
    public void OnDrag(PointerEventData eventData) => HandleInput(eventData);
    private RawImage rawImage;
    private Texture2D tex;
    private RectTransform wheelRect;
    private float wheelRadius;
    public RectTransform colorIndicator;
    [SerializeField] private int textureSize = 256;
    public float value;

    public void Initialize()
    {
        rawImage = gameObject.GetComponent<RawImage>();
        wheelRect = gameObject.GetComponent<RectTransform>();
        wheelRadius = wheelRect.sizeDelta.x * 0.5f;
        tex = new Texture2D(textureSize, textureSize);
        rawImage.texture = tex;
        GenerateWheelTexture();
        // Debug.Log("I ran!");
    }

    public void GenerateWheelTexture()
    {
        if (tex == null) tex = new Texture2D(textureSize, textureSize);
        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                float radius = textureSize / 2;
                Vector2 centerToPixel = new Vector2(x, y) - new Vector2(radius, radius);
                Vector2 normalized = new(centerToPixel.x / radius, centerToPixel.y / radius);
                tex.SetPixel(x, y, XYToColor(normalized, 1));
            }
        }
        tex.Apply();

    }

    public Vector2 ColorToXY(Color initialColor)
    {
        float H, S, V;
        Color.RGBToHSV(initialColor, out H, out S, out V);
        float angle = H * Mathf.PI * 2;
        float x = (wheelRadius * S) * Mathf.Cos(angle);
        float y = (wheelRadius * S) * Mathf.Sin(angle);
        Vector2 pos = new(x, y);
        //Debug.Log($"H: {H}, S: {S}, V: {V}, pos: {pos}");
        return pos;
    }

    public Color32 XYToColor(Vector2 pos, float value)
    {

        float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        float saturation = Mathf.Clamp01(pos.magnitude);

        Color32 color = Color.HSVToRGB(angle / 360f, saturation, value);
        return color;
    }

    private void HandleInput(PointerEventData eventData)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRect, eventData.position, eventData.pressEventCamera, out local))
        {
            // Normalize: center = (0,0), radius = wheelRect.sizeDelta.x / 2
            colorIndicator.anchoredPosition = local;

            Vector2 normalized = local / wheelRadius;
            Color32 color = XYToColor(normalized, value);
            gameObject.GetComponentInParent<ColorPickerUI>().OnWheelChanged(color);
        }
    }

    public Color32 GetCurrentIndicatorColor(float currentValue)
    {
        // Convert the pixel coordinates into a 0 to 1 percentage
        Vector2 normalized = colorIndicator.anchoredPosition / wheelRadius;
        return XYToColor(normalized, currentValue);
    }
}
