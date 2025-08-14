using UnityEngine;

public class RealtimeAudioAnalyzer : MonoBehaviour
{
    public int sampleSize = 1024;
    public int numBands = 8;
    public float[] bands;

    private float[] sampleBuffer;
    private object lockObject = new object();

    void Start()
    {
        bands = new float[numBands];
        sampleBuffer = new float[sampleSize];
        GetComponent<AudioSource>().clip = Microphone.Start(null, true, 1, AudioSettings.outputSampleRate);
        GetComponent<AudioSource>().loop = true;
        GetComponent<AudioSource>().Play();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Copy audio data for processing later on main thread
        lock (lockObject)
        {
            for (int i = 0; i < sampleSize && i < data.Length; i++)
                sampleBuffer[i] = data[i];
        }
    }

    void Update()
    {
        float[] bufferCopy = new float[sampleSize];
        lock (lockObject)
        {
            sampleBuffer.CopyTo(bufferCopy, 0);
        }

        // Run FFT here — replace with your own FFT function
        float[] spectrum = FFTUtility.ComputeNormalizedSpectrum(bufferCopy, floorDb: -80f, applyHann: true); // you'd implement this

        // Convert spectrum to bands
        MakeFrequencyBands(spectrum);
    }

    void MakeFrequencyBands(float[] spectrum)
    {
        int count = 0;
        for (int i = 0; i < numBands; i++)
        {
            int sampleCount = (int)Mathf.Pow(2, i) * 2;
            float average = 0;

            for (int j = 0; j < sampleCount; j++)
            {
                average += spectrum[count] * (count + 1);
                count++;
            }
            average /= count;
            bands[i] = average * 10;
        }
    }
}