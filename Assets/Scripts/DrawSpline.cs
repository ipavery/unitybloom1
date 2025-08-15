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
    public Plane drawPlane = new Plane(Vector3.forward, new Vector3(0, 0, 0));
    private Vector2 previousMousePos = new Vector2(0, 0);
    private bool isSplineCreated;
    private BezierSpline newSpline;
    public float velocityFactor = 2;
    private float distanceFactor;
    public GameObject drawSplineContainer;
    public RectTransform canvasrt;

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
            _timer = sampleInterval - Time.deltaTime;
        }

    }

    void OnSelectEnd(InputAction.CallbackContext ctx)
    {
        if (InputBlocker.inputBlockOverride == true)
        {
            drawingSpline = false;
            isSplineCreated = false;
            //samplePoints.ForEach(s => Debug.Log("point: " + s.velocity + " pos: " + s.position));
        }

    }

    void DoSamplePoints()
    {

        Vector2 mousePosition2D = Mouse.current.position.ReadValue(); /// canvasrt.localScale;

        if ((mousePosition2D - previousMousePos).magnitude < 5)
            return;
        previousMousePos = mousePosition2D;
        Vector3 mousePosition3D = ProjectToPlane(mousePosition2D);
        Vector3 mouseVelocity3D = ProjectToPlane(mousePosition2D + Mouse.current.delta.ReadValue()) - mousePosition3D;
        if (mouseVelocity3D.magnitude == 0)
        {
            return;
        }
        samplePoints.Add(new SamplePoints { position = mousePosition3D, velocity = mouseVelocity3D });


        if (samplePoints.Count >= 2 && isSplineCreated == false)
        {
            //calculate control points and draw spline
            //if there is no spline created yet make one and set its first and last points
            newSpline = Instantiate(splinePrefab, samplePoints[0].position, Quaternion.identity);
            newSpline.transform.SetParent(drawSplineContainer.transform);


            newSpline.points[3] = samplePoints[1].position - newSpline.transform.position;

            newSpline.points[1] = samplePoints[0].position + velocityFactor * samplePoints[0].velocity - newSpline.transform.position;
            newSpline.points[2] = samplePoints[1].position - velocityFactor * samplePoints[1].velocity - newSpline.transform.position;
            isSplineCreated = true;
            EventHub.Publish(new NewSplineCreated(newSpline));
        }
        else if (isSplineCreated == true)
        {
            distanceFactor = (samplePoints[^1].position - samplePoints[^2].position).magnitude;
            //create the new curve and set the last point on the curve
            newSpline.AddCurve();
            newSpline.points[^1] = samplePoints[^1].position - newSpline.transform.position;

            //compute curve control points to match the velocity of the sample point
            newSpline.points[^3] = samplePoints[^2].position + velocityFactor * samplePoints[^2].velocity - newSpline.transform.position;
            newSpline.points[^2] = samplePoints[^1].position - velocityFactor * samplePoints[^1].velocity - newSpline.transform.position;
            int pointCount = newSpline.points.Length;
            EventHub.Publish(new SplineUpdated(newSpline, new List<int>{pointCount-3,pointCount-2,pointCount-1}));
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
