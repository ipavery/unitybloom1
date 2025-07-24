using UnityEngine;
using System;

public class ParticleSplineManager : MonoBehaviour
{

    // public BezierSpline spline;
    // public ParticleSystem ps;

    // public int frequency;

    // ParticleSystem.Particle[] particleArray;

    // void Start()
    // {
    //     particleArray = new ParticleSystem.Particle[0];
    //     var main = ps.main;
    //     // var emitParams = new ParticleSystem.EmitParams();
    //     main.startLifetime = 5;

    //     ps.Emit(1);
    //     Array.Resize<ParticleSystem.Particle>(ref particleArray, particleArray.Length + 1);
    //     ps.GetParticles(particleArray);
    //     particleArray[particleArray.Length - 1].position = new Vector3(0,0,-16);
    //     ps.SetParticles(particleArray, particleArray.Length);

    //     Debug.Log(ps.GetParticles(particleArray));

    //     if (frequency <= 0)
    //     {
    //         return;
    //     }
    //     float stepSize = 1f / (frequency);
    //     for (int i = 0; i < frequency; i++)
    //     {

    //         Vector3 position = spline.GetPoint(i * stepSize);

    //         ps.Emit(1);
    //         Array.Resize<ParticleSystem.Particle>(ref particleArray, particleArray.Length + 1);
    //         ps.GetParticles(particleArray);
    //         particleArray[particleArray.Length - 1].position = position;
    //         ps.SetParticles(particleArray, particleArray.Length);
    //         Debug.Log(particleArray[i].position);
    //     }
    // }
    // void Start()
    // {
    //     int s_count = 5;
    //     float s_life = 5;
    //     Vector3[] posArray;
    //     particleArray = new ParticleSystem.Particle[s_count];
    //     posArray = new Vector3[s_count];

    //     for (int i = 0; i < s_count; i++)
    //     {
    //         // Debug.Log("hello");
    //         posArray[i] = UnityEngine.Random.insideUnitCircle * 5;
    //     }

    //     var main = ps.main;
    //     // var emitParams = new ParticleSystem.EmitParams();
    //     main.startLifetime = s_life;

    //     ps.Emit(s_count);
    //     ps.GetParticles(particleArray);

    //     for (int i = 0; i < posArray.Length; i++)
    //     {
    //         particleArray[i].position = posArray[i];
    //     }
    //     ps.SetParticles(particleArray, particleArray.Length);

    //     //PrintArray(posArray);
    //     //Debug.Log(particleArray[0]);
    // }
}