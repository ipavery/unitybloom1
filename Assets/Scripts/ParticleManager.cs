using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq; // Needed for SequenceEqual

public class ParticleManager : MonoBehaviour
{
    [SerializeField]
    ParticleSystem ps; //Particle system to use
    ParticleSystem.ColorOverLifetimeModule colorModule;

    //move these to the spline itself so they can be set per spline. this script should be more of a manager
    [SerializeField] int symmetry;


    public List<SplineParticleGroup> splineParticleGroup;
    [SerializeField] Transform ParticleCubeTransform;
    [SerializeField] Transform symmetryPosition;

    ParticleSystem.Particle[] particleArray;
    ParticleSystem.Particle[] combinedParticleArray; // used for setparticles
    Vector3[] posArray;
    Vector3 worldPosition;
    Vector3 mouseWorldTemp;

    int mouseTicker = 0;
    bool firstClick = true;
    int mouseDownCount = 0;
    public float catchParticlesBufferTimeRatio = 0.2f;

    private bool started = false;

    void PrintArray(Vector3[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            Debug.Log(arr[i]);
        }
    }

    bool AreArraysEqual<T>(T[] arr1, T[] arr2)
    {
        if (arr1 == null || arr2 == null) return false;
        return arr1.SequenceEqual(arr2);
    }

    // void addParticle(Vector3[]) {

    // }

    // Event system - restart particle system when clip drag ends
    void OnEnable()
    {
        EventHub.Subscribe<ClipDragEnded>(OnClipDragEnded);
        EventHub.Subscribe<GizmoDragEnded>(OnGizmoDragEnded);
    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ClipDragEnded>(OnClipDragEnded);
        EventHub.Unsubscribe<GizmoDragEnded>(OnGizmoDragEnded);
    }

    private void OnClipDragEnded(ClipDragEnded e)
    {
        //restart the particle system - somehow we are losing particles when the clip is dragged
        started = false;
        ps.Clear();
        e.spline.startTimeOffset = e.timePosition;
        StartDelayed();
        Debug.Log($"Clip drag ended for {e.spline.name} at time {e.timePosition}");
    }

    private void OnGizmoDragEnded(GizmoDragEnded e)
    {
        //restart the particle system - somehow we are losing particles when the clip is dragged
        started = false;
        ps.Clear();
        StartDelayed();
        //Debug.Log($"Gizmo drag ended for {e.gizmo.name} at position {e.position}");
    }

    void Start()
    {
        Invoke(nameof(StartDelayed), 0.5f);
    }

    void StartDelayed()
    {
        int particleCount = splineParticleGroup.Sum(group => group.spline.frequency * group.spline.lerpTimes * group.spline.splineSymmetry);
        particleArray = new ParticleSystem.Particle[particleCount];
        combinedParticleArray = new ParticleSystem.Particle[particleCount];
        //Debug.Log(combinedParticleArray.Length);

        //Copy the settings from a single particle (with settings set in the particle system interface) to the array
        ps.Emit(1);
        ParticleSystem.Particle[] singleParticle = new ParticleSystem.Particle[1];
        ps.GetParticles(singleParticle); // Should return 1 if it worked
        ParticleSystem.Particle referenceParticle = singleParticle[0];
        for (int i = 0; i < particleCount; i++)
        {
            particleArray[i] = referenceParticle;
        }
        ps.Clear();

        posArray = new Vector3[0];


        var main = ps.main;


        //these two lines change all the particles to RED
        //colorModule = ps.colorOverLifetime;
        //colorModule.color = Color.red;
        // var emitParams = new ParticleSystem.EmitParams();
        int currentParticleIndex = 0;

        //iterate thru all splines passed to ParticleManager
        for (int j = 0; j < splineParticleGroup.Count; j++)
        {
            BezierSpline currentSpline = splineParticleGroup[j].spline;
            splineParticleGroup[j].particles = new ParticleSystem.Particle[0];

            int splineParticleCount = currentSpline.frequency * currentSpline.lerpTimes;
            //Debug.Log($"splineParticleCount: {splineParticleCount}");

            int currentFrequency = currentSpline.frequency - 1; //with offset to match the true number
            int currentLerpTimes = currentSpline.lerpTimes - 1;
            if (currentFrequency <= 0)
            {
                currentFrequency = 1; // avoid division by zero
            }
            if (currentLerpTimes <= 0)
            {
                currentLerpTimes = 1; // avoid division by zero
            }
            float catchParticlesBufferTime = currentSpline.s_life * catchParticlesBufferTimeRatio;

            float stepSize = 1f / currentFrequency;

            //initial spline, place particles along it
            //loop along spline, placing particles along the way
            //lerploop - idk why this uses <= and -1 in the condition, but it works
            for (float k = 0; k <= currentSpline.lerpTimes - 1; k++)
            {
                float l = k / currentLerpTimes;

                if (!currentSpline.lerpSpline)
                {
                    currentSpline.lerpSpline = currentSpline;
                }
                if (currentSpline.frequency <= 0)
                {
                    currentSpline.frequency = 1; // avoid problems

                }

                //loop along spline, placing particles along the way. This loop controls particles, the z loop controls symmetry
                for (int i = 0; i <= currentFrequency; i++)
                {
                    Vector3 newParticlePosition = currentSpline.GetLerpPoint(i * stepSize, l);
                    //Debug.Log($"particle index within spline:  {k*(currentFrequency+1) + i + 1}");
                    //Debug.Log($"sample t value: {(k*(currentFrequency+1) + i + 1)/splineParticleCount}");
                    //ps.Emit(currentSpline.splineSymmetry);
                    //Array.Resize<ParticleSystem.Particle>(ref particleArray, particleArray.Length + currentSpline.splineSymmetry);
                    //Array.Resize(ref splineParticleGroup[j].particles, splineParticleGroup[j].particles.Length + currentSpline.splineSymmetry);
                    //ps.GetParticles(splineParticleGroup[j].particles);
                    //ps.GetParticles(particleArray);
                    float t = (k * (currentFrequency + 1) + i + 1) / splineParticleCount;
                    Color particleColor = Color.Lerp(currentSpline.lerpColor1, currentSpline.lerpColor2, t);

                    //life offset by distance along spline and lerp position
                    float life;
                    if (currentSpline.s_life < .2f)
                    {
                        currentSpline.s_life = .2f;
                        Debug.Log("minimum life set to .2f so particles don't yeet themselves");
                    }

                    life = currentSpline.s_life - (currentSpline.lifetimeOffset * i) - (currentSpline.lerpLifetimeOffset * k) - currentSpline.startTimeOffset;

                    while (life <= catchParticlesBufferTime)
                    {
                        life += currentSpline.s_life - catchParticlesBufferTime;
                    }

                    life = (float)Math.Round(life, 3);

                    // set position and life with radial symmetry
                    for (int z = 0; z < currentSpline.splineSymmetry; z++)
                    {

                        symmetryPosition.transform.position = newParticlePosition;
                        symmetryPosition.transform.RotateAround(Vector3.zero, Vector3.back, z * (360 / currentSpline.splineSymmetry));
                        var p = particleArray[currentParticleIndex];
                        p.position = symmetryPosition.transform.position;
                        p.remainingLifetime = life;
                        p.startColor = particleColor;
                        p.startLifetime = currentSpline.s_life;
                        particleArray[currentParticleIndex] = p;
                        currentParticleIndex++;
                    }

                }
            }

        }

        ps.Emit(particleCount);
        ps.SetParticles(particleArray, particleArray.Length);

        started = true;
    }

    void Update()
    {

        if (started == false)
        {
            return;
        }

        //here getparticles resets the particleArray to the current particles in the system, so they are not in the same order as the original particleArray
        ps.GetParticles(particleArray);
        for (int i = 0; i < particleArray.Length; i++)
        {
            //CATCH particles, dobr let them escaoe
            // Debug.Log(particleArray[i].remainingLifetime);
            //particleArray[i].remainingLifetime -= Time.deltaTime * .01f;
            //fix bug here where the expression results in lifetime below the buffer.
            float catchParticlesBufferTime = particleArray[i].startLifetime * catchParticlesBufferTimeRatio;
            if (particleArray[i].remainingLifetime < catchParticlesBufferTime)
            {
                particleArray[i].remainingLifetime = particleArray[i].startLifetime - (catchParticlesBufferTime - particleArray[i].remainingLifetime);
            }
            
        }
        ps.SetParticles(particleArray, particleArray.Length);
        List<Vector2> lifeList = new();
        for (int i = 0; i < particleArray.Length; i++)
        {
            lifeList.Add(new Vector2(particleArray[i].remainingLifetime, particleArray[i].GetCurrentSize(ps)));
        }
        //Debug.Log($"Particle life list: {string.Join(", ", lifeList)}");
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