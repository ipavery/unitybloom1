using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SaveFileCycler : MonoBehaviour
{
    [Header("Save Cycling")]
    public float cycleInterval = 60f;
    private List<SaveMeta> currentIndex = new();
    private int currentIndexNum;

    [Header("Fade Settings")]
    public Image fadeImage;       // fullscreen black image
    public float fadeDuration = 1.5f;
    public SaveLoadUI saveLoadUI;
    public Button fadeButton;

    void Start()
    {
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(true);
        fadeButton.onClick.AddListener(() => StartCoroutine(CycleSavesRoutine()));
        
    }

    IEnumerator CycleSavesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(cycleInterval);
            yield return StartCoroutine(FadeAndLoadNextSave());
        }
    }

    IEnumerator FadeAndLoadNextSave()
    {
        // Fade out
        yield return StartCoroutine(Fade(0f, 1f));
        currentIndex = SaveSystem.LoadIndex() ?? new List<SaveMeta>();
        // Advance index
        currentIndexNum = (currentIndexNum + 1) % currentIndex.Count;


        // Insert your "apply save to scene" logic here
        saveLoadUI.OnSaveSelected(currentIndex[currentIndexNum].filename);
        EventHub.Publish(new HideShow3DUI(true));

        // Fade in
        yield return StartCoroutine(Fade(1f, 0f));
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        Color c = fadeImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            fadeImage.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(c.r, c.g, c.b, to);
    }
}
