using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConditionManager : MonoBehaviour
{
    [Serializable]
    public class ConditionEntry
    {
        public string name = "Condition";
        public float repeatInterval = 0.15f;   // seconds between actions when held
        public bool immediateOnStart = true;   // do an immediate action when condition first becomes true
        public float gracePeriod = 0.08f;      // how long to wait before actually stopping after condition becomes false

        // These fields are set at runtime (not serialized)
        [NonSerialized] public Func<bool> condition; // predicate: what to check (assign in code)
        [NonSerialized] public Action action;       // what to perform each tick (assign in code)

        // internal runtime state
        [NonSerialized] public Coroutine coroutine;
        [NonSerialized] public float nextActionTime;
        [NonSerialized] public float graceTimer;
    }

    public List<ConditionEntry> entries = new List<ConditionEntry>();

    void Start()
    {
        // Example registrations for different conditions.
        // Replace / extend these with your own predicates and actions.

        // 1) Left mouse button
        var mouse = new ConditionEntry { name = "LeftMouse", repeatInterval = 0.15f, immediateOnStart = true, gracePeriod = 0.08f };
        mouse.condition = () => Input.GetMouseButton(0);
        mouse.action = () => Debug.Log("Mouse action at " + Time.time);
        entries.Add(mouse);

        // 2) Right mouse button
        var rightMouse = new ConditionEntry { name = "RightMouse", repeatInterval = 0.25f, immediateOnStart = true };
        rightMouse.condition = () => Input.GetMouseButton(1);
        rightMouse.action = () => Debug.Log("Right mouse action at " + Time.time);
        entries.Add(rightMouse);

        // 3) Example keyboard key
        var keyA = new ConditionEntry { name = "KeyA", repeatInterval = 0.2f, immediateOnStart = false };
        keyA.condition = () => Input.GetKey(KeyCode.A);
        keyA.action = () => Debug.Log("A held action");
        entries.Add(keyA);

        // 4..n) Add up to ~8 conditions by creating new ConditionEntry objects
        // You can also build entries from serialized config and assign condition/action from other scripts.
    }

    void Update()
    {
        // Iterate entries and ensure coroutines are started / stopped with grace and scheduling preserved.
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool cond = e.condition != null && e.condition();

            if (cond)
            {
                e.graceTimer = 0f; // reset grace when condition true
                if (e.coroutine == null)
                {
                    // Start the coroutine that will pace actions using e.nextActionTime
                    e.coroutine = StartCoroutine(RunCondition(e));
                }
            }
            else
            {
                if (e.coroutine != null)
                {
                    // Start counting grace; only stop once grace exceeded
                    e.graceTimer += Time.deltaTime;
                    if (e.graceTimer >= e.gracePeriod)
                    {
                        // Stop but preserve nextActionTime so re-press won't speed up schedule
                        StopCoroutine(e.coroutine);
                        e.coroutine = null;
                        // do not reset e.nextActionTime here if you want to preserve pacing
                    }
                }
            }
        }
    }

    private IEnumerator RunCondition(ConditionEntry e)
    {
        // Initialize nextActionTime if never set
        if (e.nextActionTime <= 0f)
            e.nextActionTime = Time.time;

        // If immediateOnStart and we're at/after the nextActionTime, do an immediate action
        if (e.immediateOnStart && Time.time >= e.nextActionTime)
        {
            e.action?.Invoke();
            e.nextActionTime = Time.time + e.repeatInterval;
        }
        else if (!e.immediateOnStart && e.nextActionTime < Time.time)
        {
            // schedule first tick in the future (so holding doesn't immediately fire)
            e.nextActionTime = Time.time + e.repeatInterval;
        }

        // Loop while predicate holds (we also re-check after waits)
        while (e.condition != null && e.condition())
        {
            float wait = e.nextActionTime - Time.time;
            if (wait > 0f)
            {
                // Wait exactly until next tick. WaitForSeconds is fine here.
                yield return new WaitForSeconds(wait);
            }
            else
            {
                // We're behind schedule; allow next frame to proceed but avoid tight busy loop
                yield return null;
            }

            // If condition dropped during wait, break (Update handles grace/stop)
            if (!(e.condition?.Invoke() ?? false))
                break;

            e.action?.Invoke();
            e.nextActionTime += e.repeatInterval; // keep a steady schedule and avoid drift
        }

        // Coroutine finished (either condition false or removed)
        e.coroutine = null;
    }

    // Optional: helper to add entries from other scripts at runtime
    public ConditionEntry CreateEntry(string name, Func<bool> condition, Action action, float interval = 0.15f, bool immediate = true, float grace = 0.08f)
    {
        var e = new ConditionEntry
        {
            name = name,
            repeatInterval = interval,
            immediateOnStart = immediate,
            gracePeriod = grace,
            condition = condition,
            action = action
        };
        entries.Add(e);
        return e;
    }
}
