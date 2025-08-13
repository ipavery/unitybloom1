using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RealtimeAudioAnalyzer : MonoBehaviour
{
    // Simple struct any script can read/receive
    [Serializable]
    public struct AudioBands
    {
        public float[] spectrum;   // raw spectrum values (length = spectrumSize)
        public float bass;         // aggregated low freq
        public float mid;          // aggregated mid freq
        public float treble;       // aggregated high freq
        public float rms;          // root-mean-square volume
    }

    public static RealtimeAudioAnalyzer Instance { get; private set; }

    [Header("Source")]
    public bool useMicrophone = false;
    public string microphoneDevice = null; // null = default
    public AudioSource targetAudioSource; // optional, will be fetched/created

    [Header("FFT")]
    [Tooltip("Power-of-two sizes: 64,128,256,512,1024,...")]
    public int spectrumSize = 1024;
    public FFTWindow fftWindow = FFTWindow.BlackmanHarris;

    [Header("Frequency bands (Hz)")]
    public float bassMax = 250f;
    public float midMax = 4000f; // bass: 20-250, mid: 250-4000, treble: 4000+

    // Public event any script can subscribe to
    public event Action<AudioBands> OnBandsUpdated;

    float[] spectrum; // cached
    float[] samples;  // for RMS or waveform
    AudioBands lastBands;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (!targetAudioSource)
            targetAudioSource = GetComponent<AudioSource>();

        if (spectrumSize <= 0) spectrumSize = 1024;
        spectrum = new float[spectrumSize];
        samples = new float[spectrumSize];

        // If using mic, start it
        if (useMicrophone)
            StartMicrophone();
    }

    void OnEnable()
    {
        // ensure audio source is configured for analysis
        if (targetAudioSource)
        {
            targetAudioSource.playOnAwake = true;
            targetAudioSource.loop = true;
        }
    }

    void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("RealtimeAudioAnalyzer: No microphone devices found.");
            return;
        }
        if (microphoneDevice == null) microphoneDevice = Microphone.devices[0];
        // 1 second clip length (it will loop)
        var clip = Microphone.Start(microphoneDevice, true, 1, AudioSettings.outputSampleRate);
        // wait until ready - but we won't block; playing immediately is usually ok
        targetAudioSource.clip = clip;
        // play after device starts
        while (!(Microphone.GetPosition(microphoneDevice) > 0)) { } // quick spin - usually returns fast
        targetAudioSource.Play();
    }

    void Update()
    {
        if (targetAudioSource == null) return;

        // 1) get waveform for RMS (optional)
        targetAudioSource.GetOutputData(samples, 0); // waveform samples
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        lastBands.rms = Mathf.Sqrt(sum / samples.Length);

        // 2) get spectrum
        targetAudioSource.GetSpectrumData(spectrum, 0, fftWindow);

        lastBands.spectrum = (float[])spectrum.Clone(); // copy - consumers shouldn't modify it
        // 3) convert spectrum bins to meaningful bands
        ComputeBandsAndRaiseEvent(lastBands, spectrum);
    }

    void ComputeBandsAndRaiseEvent(AudioBands current, float[] spec)
    {
        float sr = AudioSettings.outputSampleRate; // GetSpectrumData uses output sample rate. :contentReference[oaicite:3]{index=3}
        float nyquist = sr * 0.5f;
        float binFreq = nyquist / spec.Length; // Hz per bin

        float bassSum = 0f, midSum = 0f, trebleSum = 0f;
        for (int i = 0; i < spec.Length; i++)
        {
            float freq = i * binFreq;
            float val = spec[i];

            if (freq <= bassMax) bassSum += val;
            else if (freq <= midMax) midSum += val;
            else trebleSum += val;
        }

        // simple normalization/scaling - you can change smoothing here
        current.bass = bassSum;
        current.mid = midSum;
        current.treble = trebleSum;

        lastBands = current;

        // Event - any script can subscribe
        OnBandsUpdated?.Invoke(current);
    }

    // Convenience helpers for other scripts that prefer polling
    public AudioBands GetLatestBands() => lastBands;

    // Example helper: run an Action when a band exceeds threshold (non-blocking)
    public void RunWhenBandAbove(Action callback, Band band, float threshold)
    {
        StartCoroutine(RunCheckCoroutine(callback, band, threshold));
    }

    System.Collections.IEnumerator RunCheckCoroutine(Action callback, Band band, float threshold)
    {
        while (true)
        {
            var b = GetLatestBands();
            float val = band == Band.Bass ? b.bass : band == Band.Mid ? b.mid : b.treble;
            if (val > threshold)
            {
                callback?.Invoke();
                yield break;
            }
            yield return null;
        }
    }

    public enum Band { Bass, Mid, Treble }

    void OnDisable()
    {
        // stop microphone if used
        if (useMicrophone && microphoneDevice != null)
        {
            Microphone.End(microphoneDevice);
        }
    }
}
