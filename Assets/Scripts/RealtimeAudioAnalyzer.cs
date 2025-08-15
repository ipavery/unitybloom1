using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RealtimeAudioAnalyzer : MonoBehaviour
{
    [Header("Analysis")]
    public int sampleSize = 1024;
    public int numBands = 8;
    public float[] bands;

    [Header("UI")]
    public TMP_Dropdown audioDropDown; // assign in inspector

    // internal
    private float[] sampleBuffer;
    private object lockObject = new object();
    private string[] devices;
    private AudioSource audioSource;
    private string currentDevice = null;
    private Coroutine micStartCoroutine;

    void Start()
    {
        // Ensure AudioSource exists
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.clip = null;

        // Init buffers
        bands = new float[numBands];
        sampleBuffer = new float[sampleSize];

        // Populate mic list but DO NOT start recording
        devices = Microphone.devices;

        if (audioDropDown == null)
        {
            Debug.LogWarning("RealtimeAudioAnalyzer: audioDropDown not assigned in inspector.");
            return;
        }

        audioDropDown.ClearOptions();
        List<string> options = new List<string>();
        if (devices.Length == 0)
        {
            options.Add("No input devices found");
            audioDropDown.AddOptions(options);
            audioDropDown.interactable = false;
            return;
        }

        // Add placeholder option first
        options.Add("Select Input...");
        options.AddRange(devices);
        audioDropDown.AddOptions(options);
        audioDropDown.value = 0; // show placeholder
        audioDropDown.onValueChanged.AddListener(OnDeviceSelected);

        // Do NOT auto-start any mic — wait for selection
    }

    void OnDestroy()
    {
        if (audioDropDown != null)
            audioDropDown.onValueChanged.RemoveListener(OnDeviceSelected);

        StopMic();
    }

    // Dropdown callback: index 0 is placeholder, 1..N map to devices[0..]
    void OnDeviceSelected(int dropdownIndex)
    {
        if (devices == null || devices.Length == 0) return;

        if (dropdownIndex == 0)
        {
            // Placeholder chosen or reset — stop recording
            Debug.Log("RealtimeAudioAnalyzer: No device selected (placeholder).");
            StopMic();
            return;
        }

        int deviceIndex = dropdownIndex - 1;
        if (deviceIndex < 0 || deviceIndex >= devices.Length) return;

        string selectedDevice = devices[deviceIndex];
        Debug.Log("RealtimeAudioAnalyzer: Selected device -> " + selectedDevice);
        StartMic(selectedDevice);
    }

    void StartMic(string deviceName)
    {
        // If already recording the same device, do nothing
        if (currentDevice != null && currentDevice == deviceName && Microphone.IsRecording(deviceName))
        {
            Debug.Log("RealtimeAudioAnalyzer: Already recording from " + deviceName);
            return;
        }

        // Stop previous
        StopMic();

        currentDevice = deviceName;

        // Start recording (record length 1s rolling buffer), sample rate matches Unity audio output
        audioSource.clip = Microphone.Start(deviceName, true, 1, AudioSettings.outputSampleRate);

        // Start coroutine to wait until mic buffer starts writing (non-blocking)
        if (micStartCoroutine != null) StopCoroutine(micStartCoroutine);
        micStartCoroutine = StartCoroutine(WaitForMicToStart(deviceName));
    }

    IEnumerator WaitForMicToStart(string deviceName)
    {
        // Wait until Microphone.GetPosition(deviceName) > 0
        while (Microphone.GetPosition(deviceName) <= 0)
        {
            yield return null;
        }

        // Play the audio source after the mic has started filling
        audioSource.Play();

        micStartCoroutine = null;
        Debug.Log("RealtimeAudioAnalyzer: Mic started and playing: " + deviceName);
    }

    void StopMic()
    {
        // Stop coroutine if waiting
        if (micStartCoroutine != null)
        {
            StopCoroutine(micStartCoroutine);
            micStartCoroutine = null;
        }

        // Stop playback and end recording
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        try
        {
            Microphone.End(null); // stops all recordings
        }
        catch { /* ignore if none */ }

        currentDevice = null;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Copy audio data to sampleBuffer for processing on main thread
        lock (lockObject)
        {
            int copyCount = Mathf.Min(sampleSize, data.Length);
            for (int i = 0; i < copyCount; i++)
                sampleBuffer[i] = data[i];

            // If sampleSize > data.Length, zero the rest so old data isn't reused
            if (data.Length < sampleSize)
            {
                for (int i = data.Length; i < sampleSize; i++) sampleBuffer[i] = 0f;
            }
        }
    }

    void Update()
    {
        // Copy buffer for processing to avoid locking for long periods
        float[] bufferCopy = new float[sampleSize];
        lock (lockObject)
        {
            sampleBuffer.CopyTo(bufferCopy, 0);
        }

        // Run FFT (your existing FFTUtility)
        float[] spectrum = FFTUtility.ComputeNormalizedSpectrum(bufferCopy, floorDb: -80f, applyHann: true);

        // Convert spectrum to bands
        MakeFrequencyBands(spectrum);
    }

    void MakeFrequencyBands(float[] spectrum)
    {
        if (spectrum == null || spectrum.Length == 0) return;

        int count = 0;
        for (int i = 0; i < numBands; i++)
        {
            int sampleCount = (int)Mathf.Pow(2, i) * 2;
            // clamp to remaining spectrum length
            sampleCount = Mathf.Min(sampleCount, spectrum.Length - count);
            if (sampleCount <= 0)
            {
                bands[i] = 0f;
                continue;
            }

            float average = 0f;
            for (int j = 0; j < sampleCount; j++)
            {
                average += spectrum[count] * (count + 1); // optional weighting
                count++;
            }

            average /= sampleCount;
            bands[i] = average * 10f;
        }
    }
}
