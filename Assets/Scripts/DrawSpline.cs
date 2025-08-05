using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using System.Linq;

public class SamplePoints
{
    public Vector3 position;
    public Vector3 velocity;
}

public class DrawSpline : MonoBehaviour
{
    [Header("Actions")]
    public Button drawSplineButton;

    //input variables
    public PlayerInputActions playerControls;
    private InputAction mousePosition;
    private InputAction select;

    //Spline variables
    public BezierSpline splinePrefab;
    private List<SamplePoints> samplePoints = new();
    private bool drawingSpline;
    public float sampleInterval = .3f;
    private float _timer;
    public Plane drawPlane = new Plane(Vector3.forward, new Vector3(0, 0, 10));
    private Vector2 previousMousePos = new Vector2(0, 0);
    private bool isSplineCreated;
    private BezierSpline newSpline;

    void Awake()
    {
        playerControls = new PlayerInputActions();

        mousePosition = playerControls.Player.MousePosition;
        select = playerControls.Player.Select;
        select.started += OnSelectStart;
        select.canceled += OnSelectEnd;
    }

    void OnEnable()
    {
        playerControls.Enable();
        mousePosition.Enable();
        select.Enable();
    }

    void OnDisable()
    {
        mousePosition.Disable();
        select.Disable();
    }

    void Start()
    {
        drawSplineButton.onClick.AddListener(OnDrawSpline);
    }

    void OnDrawSpline()
    {
        if (InputBlocker.inputBlockOverride == true)
        {
            InputBlocker.inputBlockOverride = false;
        }
        else
        {
            InputBlocker.inputBlockOverride = true;
        }
    }

    void OnSelectStart(InputAction.CallbackContext ctx)
    {
        if (InputBlocker.inputBlockOverride == true)
        {
            drawingSpline = true;
            samplePoints.Clear();
        }

    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (InputBlocker.inputBlockOverride == true)
        {
            drawingSpline = false;
            isSplineCreated = false;
            samplePoints.ForEach(s => Debug.Log("point: " + s.velocity + " pos: " + s.position));
        }

    }

    void DoSamplePoints()
    {

        Vector2 mousePosition2D = Mouse.current.position.ReadValue();
        if ((mousePosition2D - previousMousePos).magnitude < 5)
            return;
        previousMousePos = mousePosition2D;
        Vector3 mousePosition3D = ProjectToPlane(mousePosition2D);
        Vector3 mouseVelocity3D = ProjectToPlane(mousePosition2D + Mouse.current.delta.ReadValue()) - mousePosition3D;
        samplePoints.Add(new SamplePoints { position = mousePosition3D, velocity = mouseVelocity3D });

        if (samplePoints.Count >= 2 && isSplineCreated == false)
        {
            //calculate control points and draw spline
            //if there is no spline created yet make one and set it's first and last points
            newSpline = Instantiate(splinePrefab, samplePoints[0].position, Quaternion.identity);
            newSpline.points[3] = samplePoints[1].position;
            isSplineCreated = true;
        }
        else if (isSplineCreated == true)
        {
            newSpline.AddCurve();
            newSpline.points[samplePoints.Count - 1] = samplePoints[samplePoints.Count - 1].position;
        }
    }

    Vector3 ProjectToPlane(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (drawPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        else
        {
            return Vector3.zero;
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (drawingSpline == true)
        {
            _timer += Time.deltaTime;
            if (_timer >= sampleInterval)
            {
                _timer -= sampleInterval;         // reset (keeps any overshoot)
                DoSamplePoints();
            }
        }
        else
        {
            _timer = 0f;                    // reset when released
        }
        if (samplePoints != null)
        {
            foreach (var s in samplePoints)
            {
                // point
                Debug.DrawLine(s.position + Vector3.up * 0.02f,
                               s.position - Vector3.up * 0.02f, Color.yellow);
                Debug.DrawLine(s.position + Vector3.left * 0.02f,
                               s.position - Vector3.left * 0.02f, Color.yellow);
                // velocity
                Debug.DrawRay(s.position, s.velocity, Color.cyan);
            }
        }


    }
}
