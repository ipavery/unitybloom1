using UnityEngine;
using System.Collections;

public class SaveIconFader : MonoBehaviour
{
    [Header("UI Reference")]
    public CanvasGroup saveCanvasGroup;

    [Header("Timing Settings")]
    public float displayTime = 1.5f; // How long it stays fully visible
    public float fadeDuration = 1.5f; // How long the fade-out takes

    private Coroutine activeFade;

    void Start()
    {
        saveCanvasGroup.gameObject.SetActive(false); //set false to start
    }

    public void ShowSaveIcon()
    {
        if (activeFade != null)
        {
            StopCoroutine(activeFade);
        }
        saveCanvasGroup.gameObject.SetActive(true);
        activeFade = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // 1. Instantly appear and ensure the GameObject is active
        saveCanvasGroup.alpha = 1f;

        // 2. Wait for the display period
        yield return new WaitForSeconds(displayTime);

        // 3. Smoothly fade out the alpha channel on the Canvas Group
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            saveCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null; // Wait for the next frame
        }

        // 4. Lock alpha to exactly 0 and disable to save rendering power
        saveCanvasGroup.alpha = 0f;
        saveCanvasGroup.gameObject.SetActive(false);
    }
}