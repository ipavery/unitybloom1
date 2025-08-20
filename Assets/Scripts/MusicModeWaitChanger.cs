using UnityEngine;
using TMPro;

public class MusicModeWaitChanger : MonoBehaviour
{
    public TMP_InputField inputField;  // Drag your TMP Input Field here in the inspector
    public ParticleManager particleManager;

    void Start()
    {
        // Subscribe to the input field's "onValueChanged" event
        inputField.onValueChanged.AddListener(OnInputChanged);
    }

    void OnInputChanged(string input)
    {
        // Try to parse the input as an int
        if (float.TryParse(input, out float newValue))
        {
            particleManager.musicModeWait = newValue;
        }
        else
        {
        }
    }
}

