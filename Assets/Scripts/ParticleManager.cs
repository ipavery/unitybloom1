using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ParticleManager : MonoBehaviour
{
    [SerializeField]
    ParticleSystem ps; //Particle system to use
    ParticleSystem.ColorOverLifetimeModule colorModule;

    //move these to the spline itself so they can be set per spline. this script should be more of a manager
    [SerializeField] float s_life; //Lifetime of the particle in seconds
    [SerializeField] int symmetry;
    public int frequency;
    public float lifetimeOffset;
    public float lerpLifetimeOffset;
    public int lerpTimes;


    public List<SplineParticleGroup> splineParticleGroup;
    [SerializeField] Transform ParticleCubeTransform;
    [SerializeField] Transform symmetryPosition;

    ParticleSystem.Particle[] particleArray;
    Vector3[] posArray;
    Vector3 worldPosition;
    Vector3 mouseWorldTemp;

    int mouseTicker = 0;
    bool firstClick = true;
    int mouseDownCount = 0;
    float catchParticlesBufferTime = 0.5f;

    private bool started = false;

    void PrintArray(Vector3[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            Debug.Log(arr[i]);
        }
    }

    // void addParticle(Vector3[]) {

    // }

    // Event system - restart particle system when clip drag ends
    void OnEnable()
    {
        EventHub.Subscribe<ClipDragEnded>(OnClipDragEnded);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ClipDragEnded>(OnClipDragEnded);
    }

    private void OnClipDragEnded(ClipDragEnded e)
    {
        //restart the particle system
        Debug.Log($"Clip drag ended for {e.clip.name} at time {e.timePosition}");
    }

    void Start()
    {
        Invoke(nameof(StartDelayed), 0.5f);
    }

    void StartDelayed()
    {
        particleArray = new ParticleSystem.Particle[0];
        posArray = new Vector3[0];

        var main = ps.main;


        //these two lines change all the particles to RED
        //colorModule = ps.colorOverLifetime;
        //colorModule.color = Color.red;
        // var emitParams = new ParticleSystem.EmitParams();
        main.startLifetime = s_life;





        //iterate thru all splines passed to ParticleManager
        for (int j = 0; j < splineParticleGroup.Count; j++)
        {
            BezierSpline currentSpline = splineParticleGroup[j].spline;
            currentSpline.frequency -= 1;
            currentSpline.lerpTimes -= 1;
            float stepSize = 1f / currentSpline.frequency;
            
            //initial spline, place particles along it
            //loop along spline, placing particles along the way

            //lerploop
            for (float k = 0; k <= currentSpline.lerpTimes; k++)
            {
                float l = k / currentSpline.lerpTimes;
                // Debug.Log(lerpTimes);
                // Debug.Log(k);
                // Debug.Log(l);
                if (!currentSpline.lerpSpline)
                {
                    Debug.Log("add stuff here later");
                    return;
                }
                if (frequency <= 0)
                {
                    return;
                }


                //loop along spline, placing particles along the way
                for (int i = 0; i <= currentSpline.frequency; i++)
                {
                    Vector3 newParticlePosition = currentSpline.GetLerpPoint(i * stepSize, l);

                    ps.Emit(currentSpline.splineSymmetry);
                    Array.Resize<ParticleSystem.Particle>(ref particleArray, particleArray.Length + currentSpline.splineSymmetry);
                    ps.GetParticles(particleArray);

                    //life offset by distance along spline and lerp position
                    float life = currentSpline.s_life - (currentSpline.lifetimeOffset * i) - (currentSpline.lerpLifetimeOffset * k);
                    
                    while (life <= catchParticlesBufferTime)
                    {
                        life += s_life-catchParticlesBufferTime;
                    }

                    //IDEA to solve problems!!! change size over lifetime curve to end early so you have time to "catch" the particles

                    life = (float)Math.Round(life, 3);

                    // if(life <= catchParticlesBufferTime) {
                    //     life = s_life;
                    // }
                    



                    //initial symmetry and size offset
                    particleArray[particleArray.Length - currentSpline.splineSymmetry].position = newParticlePosition;
                    particleArray[particleArray.Length - currentSpline.splineSymmetry].remainingLifetime = life;
                    particleArray[particleArray.Length - currentSpline.splineSymmetry].startColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);

                    // set position and life with radial symmetry
                    for (int z = 1; z < currentSpline.splineSymmetry; z++)
                    {
                        symmetryPosition.transform.position = newParticlePosition;
                        symmetryPosition.transform.RotateAround(Vector3.zero, Vector3.back, z * (360 / currentSpline.splineSymmetry));
                        particleArray[particleArray.Length - currentSpline.splineSymmetry + z].position = symmetryPosition.transform.position;

                        particleArray[particleArray.Length - currentSpline.splineSymmetry + z].remainingLifetime = life;

                        particleArray[particleArray.Length - currentSpline.splineSymmetry + z].startColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
                        // Debug.Log("im i"+i);
                        
                    }

                    // Debug.Log("first i = "+i+"    "+life);
                    
                    ps.SetParticles(particleArray, particleArray.Length);
                    // Debug.Log("second i = "+i+"    "+life);
                }
            }

        }

        //PrintArray(posArray);
        //Debug.Log(particleArray[0]);
        started = true;
    }

    void Update()
    {

        if (started == false)
        {
            return;
        }
        ps.GetParticles(particleArray);
        for (int i = 0; i < particleArray.Length; i++)
        {
            //CATCH particles, dobr let them escaoe
            // Debug.Log(particleArray[i].remainingLifetime);
            if (particleArray[i].remainingLifetime < catchParticlesBufferTime)
            {
                particleArray[i].remainingLifetime = s_life - (catchParticlesBufferTime - particleArray[i].remainingLifetime);
            }
        }
        ps.SetParticles(particleArray, particleArray.Length);


        //input
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -1 * Camera.main.transform.position.z;
        worldPosition = Camera.main.ScreenToWorldPoint(mousePos);

        //this was for using mouse to move cube and place particles
        //ParticleCubeTransform.position = worldPosition;

        int mouseTickerTarg = 0;

        if (Input.GetMouseButton(0) && Input.GetKeyDown("t"))
        {
            // mouseTicker = mouseTickerTarg;
            if (mouseWorldTemp != worldPosition)
            {
                if (++mouseTicker >= mouseTickerTarg || firstClick == true)
                {
                    if (firstClick == true)
                    {
                        mouseDownCount = 0;
                    }
                    mouseDownCount++;
                    mouseTicker = 0;

                    //add particles below
                    ps.Emit(symmetry);
                    Array.Resize(ref posArray, posArray.Length + symmetry);
                    Array.Resize(ref particleArray, particleArray.Length + symmetry);
                    ps.GetParticles(particleArray);
                    particleArray[particleArray.Length - symmetry].position = worldPosition;
                    for (int i = 1; i < symmetry; i++)
                    {
                        symmetryPosition.transform.position = worldPosition;
                        symmetryPosition.transform.RotateAround(Vector3.zero, Vector3.back, i * (360 / symmetry));
                        particleArray[particleArray.Length - symmetry + i].position = symmetryPosition.transform.position;
                    }
                    ps.SetParticles(particleArray, particleArray.Length);

                    firstClick = false;


                    // Debug.Log($"X: {mousePos.x}    Y: {mousePos.y}");
                }
            }
            mouseWorldTemp = worldPosition;
        }
        else
        {
            firstClick = true;
        }

                
        if (Input.GetKeyDown("r"))
        {
            particleArray = new ParticleSystem.Particle[0];
            // Array.Resize<ParticleSystem.Particle>(ref particleArray, particleArray.Length-mouseDownCount);
        }
    }
}