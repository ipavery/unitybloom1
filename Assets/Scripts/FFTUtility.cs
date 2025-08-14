using System;
using UnityEngine;

/// <summary>
/// Small FFT helper: computes magnitude spectrum from time-domain samples (power-of-two length).
/// Usage: float[] spectrum = FFTUtility.ComputeMagnitudeSpectrum(samples, applyHann:true, toDecibels:true);
/// Returns spectrum length = samples.Length / 2 (bins from 0 to Nyquist).
/// </summary>
public static class FFTUtility
{
    const float EPS = 1e-12f;

    // Public helper - returns magnitude spectrum (N/2 bins). Optionally converts to dB.
    public static float[] ComputeMagnitudeSpectrum(float[] samples, bool applyHann = true, bool toDecibels = true)
    {
        int n = samples.Length;
        if (n == 0) return new float[0];
        if ((n & (n - 1)) != 0)
            throw new ArgumentException("FFTUtility: sample length must be a power of two.");

        // copy samples to real/imag arrays (we won't modify original)
        float[] real = new float[n];
        float[] imag = new float[n];
        Array.Copy(samples, real, n);
        // apply window
        if (applyHann)
            ApplyHannWindowInPlace(real);

        // perform in-place complex FFT (real in 'real', imag in 'imag')
        FFT(real, imag);

        int half = n / 2;
        float[] mags = new float[half];
        float scale = 1f / n; // normalize by N (makes amplitude independent of FFT size)
        for (int i = 0; i < half; i++)
        {
            float r = real[i];
            float im = imag[i];
            float mag = Mathf.Sqrt(r * r + im * im) * scale;
            if (toDecibels)
            {
                // map magnitude to dB (negative..0), add eps to avoid log(0)
                float db = 20f * Mathf.Log10(mag + EPS);
                // remap from [floorDb..0] to [0..1] optionally — but here we just return dB values
                mags[i] = db;
            }
            else
            {
                mags[i] = mag;
            }
        }

        return mags;
    }

    // Optional: a convenience that returns 0..1 normalized amplitudes from dB
    // floorDb e.g. -80 maps to 0, 0 maps to 1.
    public static float[] ComputeNormalizedSpectrum(float[] samples, float floorDb = -80f, bool applyHann = true)
    {
        var db = ComputeMagnitudeSpectrum(samples, applyHann, toDecibels: true);
        int len = db.Length;
        float[] outArr = new float[len];
        for (int i = 0; i < len; i++)
            outArr[i] = Mathf.Clamp01((db[i] - floorDb) / -floorDb);
        return outArr;
    }

    // Hann window
    static void ApplyHannWindowInPlace(float[] data)
    {
        int n = data.Length;
        for (int i = 0; i < n; i++)
        {
            float w = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (n - 1)));
            data[i] *= w;
        }
    }

    // Iterative in-place complex FFT (Cooley-Tukey). real[] and imag[] are modified.
    // This implementation assumes forward FFT (time -> frequency).
    static void FFT(float[] real, float[] imag)
    {
        int n = real.Length;
        int j = 0;
        // bit-reversal permutation
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                float tmpR = real[i];
                float tmpI = imag[i];
                real[i] = real[j];
                imag[i] = imag[j];
                real[j] = tmpR;
                imag[j] = tmpI;
            }
            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        // Danielson-Lanczos
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2.0 * Math.PI / len; // forward transform (negative)
            double wlen_r = Math.Cos(ang);
            double wlen_i = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double wr = 1.0;
                double wi = 0.0;
                for (int m = 0; m < (len >> 1); m++)
                {
                    int u = i + m;
                    int v = i + m + (len >> 1);
                    double tr = wr * real[v] - wi * imag[v];
                    double ti = wr * imag[v] + wi * real[v];

                    real[v] = (float)(real[u] - tr);
                    imag[v] = (float)(imag[u] - ti);
                    real[u] = (float)(real[u] + tr);
                    imag[u] = (float)(imag[u] + ti);

                    // update wr,wi
                    double tmpWr = wr * wlen_r - wi * wlen_i;
                    double tmpWi = wr * wlen_i + wi * wlen_r;
                    wr = tmpWr;
                    wi = tmpWi;
                }
            }
        }
    }
}
