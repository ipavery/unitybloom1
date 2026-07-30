using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class CinematicIntro : MonoBehaviour
{
    [Header("UI & Dependencies")]
    public Image fadeScreen; 
    public SaveLoadUI saveLoadUI;
    public MouseLook mouseLook; 
    public CanvasGroup mainUIGroup; 

    [Header("Intro Settings")]
    public float fadeDuration = 1.0f;
    [Tooltip("How long each save is displayed between fades")]
    public float displayDuration = 4.0f; 
    [Tooltip("The path inside the Resources folder containing the JSON save files")]
    public string examplesFolder = "Examples"; 
    
    [Header("Camera Drift Settings")]
    [Tooltip("How far from the saved camera position the shot should start")]
    public float maxOffsetDistance = 15f; 

    private bool isIntroRunning = false;
    private bool isEnding = false;
    private TextAsset[] exampleSaves;
    private Coroutine introLoopCoroutine;

    private Vector3 currentVelocity;
    private Transform targetCamTransform;
    private ParticleManager particleManager;

    void Start()
    {
        if (saveLoadUI.playCinematicIntro)
        {
            // Load all text/json assets found in the Resources/Examples folder
            exampleSaves = Resources.LoadAll<TextAsset>(examplesFolder);
            
            if (exampleSaves.Length > 0)
            {
                introLoopCoroutine = StartCoroutine(RunIntroLoop());
            }
            else
            {
                Debug.LogWarning($"CinematicIntro: No saves found in Resources/{examplesFolder}. Skipping intro.");
                EndIntroImmediately();
            }
        }
        else
        {
            EndIntroImmediately();
        }
    }

    IEnumerator RunIntroLoop()
    {
        isIntroRunning = true;
        fadeScreen.gameObject.SetActive(true);

        particleManager = FindAnyObjectByType<ParticleManager>();

        // 1. Lock player controls and hide editor UI
        if (mouseLook != null) mouseLook.enabled = false;
        if (mainUIGroup != null) 
        {
            mainUIGroup.alpha = 0f;
            mainUIGroup.blocksRaycasts = false;
        }

        int lastSaveIndex = -1; // Keep track of the last played save

        // Loop indefinitely until the user clicks
        while (isIntroRunning && !isEnding)
        {
            // 2. Pick a random save from the folder (avoiding the last played save)
            int randomIndex = Random.Range(0, exampleSaves.Length);
            
            if (exampleSaves.Length > 1)
            {
                while (randomIndex == lastSaveIndex)
                {
                    randomIndex = Random.Range(0, exampleSaves.Length);
                }
            }
            
            lastSaveIndex = randomIndex;
            TextAsset randomSave = exampleSaves[randomIndex];
            SaveData data = JsonUtility.FromJson<SaveData>(randomSave.text);

            // 3. Load it (this automatically clears the old scene and snaps the camera to the saved position)
            saveLoadUI.LoadSaveDataInMemory(data, true); 
            
            if (particleManager != null) particleManager.ClearAllParticles();

            // 4. Hide the newly generated control points and gizmos
            EventHub.Publish(new HideShow3DUI(true));

            // 5. Setup the random camera drift
            SetupRandomDrift();

            // 6. Fade In (Black to Clear)
            yield return StartCoroutine(Fade(1f, 0f));

            // 7. Wait while drifting
            float elapsed = 0f;
            while (elapsed < displayDuration)
            {
                if (isEnding) yield break; // Abort the wait if the user clicked
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 8. Fade Out (Clear to Black) before loading the next scene
            yield return StartCoroutine(Fade(0f, 1f));
        }
    }

    void SetupRandomDrift()
    {
        // Determine whether to move the parent body or the camera itself based on your MouseLook hierarchy
        targetCamTransform = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;

        Vector3 savedPos = targetCamTransform.position;
        Vector3 offset = Vector3.zero;

        // 50/50 chance to Zoom or Pan
        bool isZooming = Random.value > 0.5f;

        if (isZooming)
        {
            // Zoom: Offset backwards or forwards along the camera's forward axis
            float sign = Random.value > 0.5f ? 1f : -1f;
            offset = Camera.main.transform.forward * (maxOffsetDistance * sign);
        }
        else
        {
            // Pan: Offset along a random 2D plane perpendicular to the camera's view (Right/Up axes)
            Vector2 randomCircle = Random.insideUnitCircle.normalized * maxOffsetDistance;
            offset = (Camera.main.transform.right * randomCircle.x) + (Camera.main.transform.up * randomCircle.y);
        }

        // Apply the randomized starting position
        targetCamTransform.position = savedPos + offset;

        // We want the camera to drift exactly *through* the original saved position. 
        // If we start at (+offset) and want to end at (-offset) over the entire lifespan of the shot:
        // Velocity = Distance / Time. Distance is (-offset - offset) = -2 * offset.
        float totalTimeInShot = displayDuration + (fadeDuration * 2);
        currentVelocity = (-offset * 2f) / totalTimeInShot;
    }

    void Update()
    {
        if (!isIntroRunning || isEnding) return;

        // Apply continuous camera drift
        if (targetCamTransform != null)
        {
            targetCamTransform.position += currentVelocity * Time.deltaTime;
        }

        // Listen for a click or tap to interrupt the cycle
        if ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || 
            (Touchscreen.current != null && Touchscreen.current.touches.Count > 0 && Touchscreen.current.touches[0].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began))
        {
            isEnding = true;
            if (introLoopCoroutine != null) StopCoroutine(introLoopCoroutine);
            StartCoroutine(EndIntroSequence());
        }
    }

    IEnumerator EndIntroSequence()
    {
        // 1. Smoothly fade out to black from wherever the current fade state is
        yield return StartCoroutine(Fade(fadeScreen.color.a, 1f));

        // 2. Load the actual recent save file the user was last working on
        saveLoadUI.AutoLoadMostRecentSave();

        // 3. Restore player controls, UI, and 3D Gizmos
        if (mouseLook != null) mouseLook.enabled = true;
        if (mainUIGroup != null) 
        {
            mainUIGroup.alpha = 1f;
            mainUIGroup.blocksRaycasts = true;
        }

        // Bring the control points and gizmos back
        EventHub.Publish(new HideShow3DUI(false));

        // 4. Fade back in to the actual game
        yield return StartCoroutine(Fade(1f, 0f));

        // 5. Turn off intro components
        EndIntroImmediately();
    }

    void EndIntroImmediately()
    {
        fadeScreen.gameObject.SetActive(false);
        isIntroRunning = false;
        gameObject.SetActive(false);
    }

    IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsed = 0f;
        Color c = fadeScreen.color;
        
        // Ensure raycast target is true so the user can click the screen during the fade
        fadeScreen.raycastTarget = true; 

        while (elapsed < fadeDuration)
        {
            // If the user clicks while it's trying to fade IN, instantly abort the fade-in
            if (isEnding && endAlpha == 0f) yield break; 

            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            fadeScreen.color = c;
            yield return null;
        }
        
        c.a = endAlpha;
        fadeScreen.color = c;

        // Disable blocking clicks once the screen is fully clear
        if (endAlpha == 0f) fadeScreen.raycastTarget = false; 
    }
}