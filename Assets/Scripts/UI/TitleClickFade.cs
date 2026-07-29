using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class TitleClickFade : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public float fadeDuration = 1.5f;

    private bool clicked = false;

    private void Update()
    {
        // Detect a click or tap
        if (!clicked && (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (Touchscreen.current != null && Touchscreen.current.touches.Count > 0))
        {
            clicked = true;
            StartCoroutine(FadeOut());
        }
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color color = titleText.color;

        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            titleText.color = new Color(color.r, color.g, color.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure invisible
        titleText.color = new Color(color.r, color.g, color.b, 0f);
        gameObject.SetActive(false); // Deactivate after fade
    }
}

