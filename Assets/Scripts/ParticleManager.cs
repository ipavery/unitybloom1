using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Unity.Mathematics; // Needed for SequenceEqual

public enum ParticleSpawnMode
{
    Instant,
    Music,
    Independent
}

public class ParticleManager : MonoBehaviour
{
    [SerializeField]
    ParticleSystem ps; //Particle system to use
    ParticleSystem.ColorOverLifetimeModule colorModule;

    //move these to the spline itself so they can be set per spline. this script should be more of a manager
    [SerializeField] int symmetry;
    public ParticleSpawnMode spawnMode = ParticleSpawnMode.Music;

    public List<SplineParticleGroup> splineParticleGroup;
    [SerializeField] Transform ParticleCubeTransform;
    [SerializeField] Transform symmetryPosition;

    ParticleSystem.Particle[] particleArray;
    ParticleSystem.Particle[] combinedParticleArray; // used for setparticles
    Vector3[] posArray;

    public float catchParticlesBufferTimeRatio = 0.2f;
    public float musicModeWait;

    [Header("New Spline Event")]
    public GameObject drawSplineContainer; //where the new splines go

    private bool started = false;
    [Header("audio debug")]
    public Vector3 audioDebugPos;
    public RealtimeAudioAnalyzer audioAnalyzer;
    public ConditionManager conditionManager;
    public RuntimeLineDrawer runtimeLineDrawer;
    public GameObject UIButtonContainer;

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
        EventHub.Subscribe<ReloadParticles>(OnReloadParticles);
        EventHub.Subscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Subscribe<SplineUpdated>(OnSplineUpdated);


    }

    void OnDisable()
    {
        EventHub.Unsubscribe<ClipDragEnded>(OnClipDragEnded);
        EventHub.Unsubscribe<GizmoDragEnded>(OnGizmoDragEnded);
        EventHub.Unsubscribe<ReloadParticles>(OnReloadParticles);
        EventHub.Unsubscribe<NewSplineCreated>(OnNewSplineCreated);
        EventHub.Unsubscribe<SplineUpdated>(OnSplineUpdated);


    }

    // void HandleAudioBands(RealtimeAudioAnalyzer bands)
    // {

    //     Debug.DrawLine(audioDebugPos, audioDebugPos + 10 * bands.bass * Vector3.up, Color.green, .1f);
    //     Debug.DrawLine(audioDebugPos + 1 * Vector3.left, audioDebugPos + 1 * Vector3.left + 10 * bands.mid * Vector3.up, Color.red, .1f);
    //     Debug.DrawLine(audioDebugPos + 2 * Vector3.left, audioDebugPos + 2 * Vector3.left + 10 * bands.treble * Vector3.up, Color.blue, .1f);
    //     Debug.DrawLine(audioDebugPos + 3 * Vector3.left, audioDebugPos + 3 * Vector3.left + 10 * bands.rms * Vector3.up, Color.yellow, .1f);
    //     // example: call a function when bass is strong
    //     bool runChannel = bands.bass > 0.05f;
    // if (runChannel && !IsInvoking(nameof(InvokeBass)))
    //     InvokeRepeating(nameof(InvokeBass), 0f, musicModeWait);
    // else if (!runChannel && IsInvoking(nameof(InvokeBass)))
    //     CancelInvoke(nameof(InvokeBass));

    // runChannel = bands.treble > .01f;
    // if (runChannel && !IsInvoking(nameof(InvokeTreble)))
    //     InvokeRepeating(nameof(InvokeTreble), 0f, musicModeWait);
    // else if (!runChannel && IsInvoking(nameof(InvokeTreble)))
    //     CancelInvoke(nameof(InvokeTreble));

    // runChannel = bands.mid > .05f;
    // if (runChannel && !midRunning)
    // {
    //     midRunning = true;
    //     StartCoroutine(InvokeMid());
    // }
    // else if (!runChannel && midRunning)
    // {
    //     midRunning = false;
    //     StopCoroutine(InvokeMid());
    // }

    void InvokeBass()
    {
        SimulateSpline(splineParticleGroup[0].spline);
    }
    void InvokeTreble()
    {
        SimulateSpline(splineParticleGroup[2].spline);
    }

    void OnNewSplineCreated(NewSplineCreated e)
    {
        if (splineParticleGroup.Count <= 0)
        {
            //would be nice to save spline settings even if they are all deleted
        }
        else if (e.isDrawSpline == true)
        {
            e.spline.CopyFrom(splineParticleGroup[^1].spline); // just for splines created by drawing
        }

        if (drawSplineContainer != null) e.spline.transform.SetParent(drawSplineContainer.transform); //make sure parent is the drawspline box (all splines go there even if not draw splines)
        splineParticleGroup.Add(new SplineParticleGroup { spline = e.spline });
    }

    void OnSplineUpdated(SplineUpdated e)
    {
        RestartParticleSystem();
    }

    private void OnReloadParticles(ReloadParticles e)
    {
        RestartParticleSystem();
    }

    private void RestartParticleSystem()
    {
        //simple restart of the particle system
        if (spawnMode == ParticleSpawnMode.Instant)
        {
            started = false;
            ps.Clear();
            StartInstant();
        }
        if (spawnMode == ParticleSpawnMode.Music)
        {
            conditionManager.StopAndClearAllEntries();
            for (int i = 0; i < splineParticleGroup.Count; i++)
            {
                var group = splineParticleGroup[i];        // capture the group
                var curSpline = group.spline;              // capture the spline object
                int musicChannel = curSpline.musicChannel; // capture primitive values too, if you use them
                float threshold = curSpline.activationThreshhold;
                Debug.Log($"{curSpline.name} coroutine started");
                conditionManager.CreateEntry($"spline_{i}", () => audioAnalyzer.bands[musicChannel] > threshold, () => SimulateSpline(curSpline), musicModeWait, true, .08f);
            }
        }
        if (spawnMode == ParticleSpawnMode.Independent)
        {
            //StopAllCoroutines();
            conditionManager.StopAndClearAllEntries();
            StartIndependent();
        }

    }

    private void OnClipDragEnded(ClipDragEnded e)
    {

        e.spline.startTimeOffset = e.timePosition;
        RestartParticleSystem();
        Debug.Log($"Clip drag ended for {e.spline.name} at time {e.timePosition}");
    }

    private void OnGizmoDragEnded(GizmoDragEnded e)
    {
        RestartParticleSystem();
    }

    void Start()
    {
        if (spawnMode == ParticleSpawnMode.Instant)
        {
            Invoke(nameof(StartInstant), 0.5f);
        }
        if (spawnMode == ParticleSpawnMode.Music) //this will need to change if you want to change modes in the middle
        {
            Invoke(nameof(WaitForAudio), 2.5f);
        }
        if (spawnMode == ParticleSpawnMode.Independent)
        {
            //Invoke(nameof(StartIndependent), 1f); //commented out because saveloadsystem already auto-loads and starts system
        }
        //conditionManager.CreateEntry("test", () => Input.GetMouseButton(0), () => SimulateSpline(splineParticleGroup[0].spline), musicModeWait, true, .08f);
    }

    void StartIndependent() {
        for (int i = 0; i < splineParticleGroup.Count; i++)
        {
            var group = splineParticleGroup[i];        // capture the group
            var curSpline = group.spline;              // capture the spline object
            int musicChannel = curSpline.musicChannel; // capture primitive values too, if you use them
            float threshold = curSpline.activationThreshhold;
            conditionManager.CreateEntry($"spline_{i}", () => true, () => SimulateSpline(curSpline), curSpline.s_life, true, 0f);
        }
    }

    void StartIndependent2() {
        //Option 2 for start strategy
        //iterate thru all splines passed to ParticleManager
        for (int j = 0; j < splineParticleGroup.Count; j++)
        {
            BezierSpline currentSpline = splineParticleGroup[j].spline;
            if (currentSpline.isActive == false)
                continue;

            int splineParticleCount = currentSpline.frequency * currentSpline.lerpTimes;

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
                //here in the lerploop, it should start coroutines for each lerped spline
                float l = k / currentLerpTimes;

                if (!currentSpline.lerpSpline)
                {
                    currentSpline.lerpSpline = currentSpline;
                }
                if (currentSpline.frequency <= 0)
                {
                    currentSpline.frequency = 1; // avoid problems

                }

                StartCoroutine(SplineLoop(currentSpline, (currentSpline.startTimeOffset + currentSpline.lerpLifetimeOffset * k), currentFrequency, splineParticleCount, stepSize, l, k, currentSpline.lifetimeOffset));
            }

        }
        
    }

    void WaitForAudio()
    {
        for (int i = 0; i < splineParticleGroup.Count; i++)
        {
            var group = splineParticleGroup[i];        // capture the group
            var curSpline = group.spline;              // capture the spline object
            int musicChannel = curSpline.musicChannel; // capture primitive values too, if you use them
            float threshold = curSpline.activationThreshhold;
            conditionManager.CreateEntry($"spline_{i}", () => audioAnalyzer.bands[musicChannel] > threshold, () => SimulateSpline(curSpline), musicModeWait, true, .08f);
        }
        // int i = 0;
        // var curSpline = splineParticleGroup[i].spline;
        // conditionManager.CreateEntry($"spline_{i}", () => audioAnalyzer.bands[curSpline.musicChannel] > curSpline.activationThreshhold, () => SimulateSpline(splineParticleGroup[i].spline), musicModeWait, true, .08f);
        // i = 1;
        // curSpline = splineParticleGroup[i].spline;
        // conditionManager.CreateEntry($"spline_{i}", () => audioAnalyzer.bands[curSpline.musicChannel] > curSpline.activationThreshhold, () => SimulateSpline(splineParticleGroup[i].spline), musicModeWait, true, .08f);

    }

    void StartInstant()
    {
        if (spawnMode == ParticleSpawnMode.Instant)
        {
            int particleCount = splineParticleGroup.Sum(group => group.spline.frequency * group.spline.lerpTimes * group.spline.splineSymmetry * (group.spline.isActive ? 1 : 0));
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
                if (currentSpline.isActive == false)
                    continue;
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

                        float angleStep = 360f / currentSpline.splineSymmetry;
                        // set position and life with radial symmetry
                        for (int z = 0; z < currentSpline.splineSymmetry; z++)
                        {

                            symmetryPosition.transform.position = newParticlePosition;
                            symmetryPosition.transform.RotateAround(Vector3.zero, Vector3.back, z * angleStep);
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
        
    }

    IEnumerator SplineLoop(BezierSpline currentSpline, float startTimeOffset, int currentFrequency, int splineParticleCount, float stepSize, float l, float k, float particleTimeOffset)
    {
        //loop along spline, placing particles along the way. This loop controls particles, the z loop controls symmetry
        yield return new WaitForSeconds(startTimeOffset);
        for (int i = 0; i <= currentFrequency; i++)
        {
            if (currentSpline == null) yield break; // exit the coroutine if the spline has been deleted
            
            //the row coroutine should start here
            Vector3 newParticlePosition = currentSpline.GetLerpPoint(i * stepSize, l);

            float t = (k * (currentFrequency + 1) + i + 1) / splineParticleCount;
            Color particleColor = Color.Lerp(currentSpline.lerpColor1, currentSpline.lerpColor2, t);

            //life offset by distance along spline and lerp position

            float angleStep = 360f / currentSpline.splineSymmetry;
            // set position and life with radial symmetry
            for (int z = 0; z < currentSpline.splineSymmetry; z++)
            {

                symmetryPosition.transform.position = newParticlePosition;
                symmetryPosition.transform.RotateAround(Vector3.zero, Vector3.back, z * angleStep);

                var emitParams = new ParticleSystem.EmitParams
                {
                    position = symmetryPosition.transform.position,
                    startColor = particleColor,
                    startLifetime = currentSpline.s_life,
                };
                ps.Emit(emitParams, 1);
            }
            yield return new WaitForSeconds(particleTimeOffset);
        }
    }

    void SimulateSpline(BezierSpline spline)
    {

        BezierSpline currentSpline = spline;
        if (currentSpline.isActive == true)
        {
            int splineParticleCount = currentSpline.frequency * currentSpline.lerpTimes;

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

            float stepSize = 1f / currentFrequency;

            //initial spline, place particles along it
            //loop along spline, placing particles along the way
            //lerploop - idk why this uses <= and -1 in the condition, but it works
            for (float k = 0; k <= currentSpline.lerpTimes - 1; k++)
            {
                //here in the lerploop, it should start coroutines for each lerped spline
                float l = k / currentLerpTimes;

                if (!currentSpline.lerpSpline)
                {
                    currentSpline.lerpSpline = currentSpline;
                }
                if (currentSpline.frequency <= 0)
                {
                    currentSpline.frequency = 1; // avoid problems

                }

                StartCoroutine(SplineLoop(currentSpline, (currentSpline.startTimeOffset + currentSpline.lerpLifetimeOffset * k), currentFrequency, splineParticleCount, stepSize, l, k, currentSpline.lifetimeOffset));
            }
        }

    }

    void Update()
    {
        if (spawnMode == ParticleSpawnMode.Instant)
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
        }
        if (spawnMode == ParticleSpawnMode.Music)
        {
            //RuntimeLineDrawer.DrawLine(audioDebugPos + 1 * Vector3.right, audioDebugPos + 1 * Vector3.right + 2 * Vector3.up, Color.green, 20f);
            for (int i = 0; i < audioAnalyzer.bands.Length; i++)
            {
                if (UIButtonContainer.activeInHierarchy)
                {
                    RuntimeLineDrawer.DrawLine(audioDebugPos + i * Vector3.left, audioDebugPos + i * Vector3.left + 1 * audioAnalyzer.bands[i] * Vector3.up, Color.red, .2f);

                }
                //Debug.DrawLine(audioDebugPos + i * Vector3.left, audioDebugPos + i * Vector3.left + 10 * audioAnalyzer.bands[i] * Vector3.up, Color.red, .1f);

            }
        }
        if (spawnMode == ParticleSpawnMode.Independent)
        {

        }
    }
}