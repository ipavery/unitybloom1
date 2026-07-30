using UnityEngine;
using UnityEngine.InputSystem;

public enum ControlMode
{
    Fixed,  // WASDQE movement only, no rotation
    Fly,    // WASDQE movement + Mouse Look
    Pan     // Unity Editor style: Scroll to zoom to mouse, right-click drag to pan
}

public class MouseLook : MonoBehaviour
{
    [Header("Mouse Look")]
    public ControlMode controlMode = ControlMode.Fixed;
    public PlayerInputActions playerControls;
    
    private InputAction move;
    private InputAction fire;
    private InputAction verticalupdown;
    private InputAction look;
    private InputAction mousePosition;

    [Tooltip("Sensitivity for looking around in Fly mode")]
    public float mouseSensitivity = 0.1f; 

    // Mobile Touch State Tracking
    private Vector2 lastTouchCentroid;
    private float lastTouchDistance;

    public Transform playerBody;
    private Camera cam;

    [Header("Movement")]
    public float acceleration = 80f;
    public float maxSpeed = 300f;
    public float deceleration = 10f;
    
    [Header("Pan & Zoom Settings")]
    [Tooltip("Speed for zooming in and out with the scroll wheel")]
    public float scrollSpeed = 100f; 

    [Tooltip("If true, pans 1:1 exactly with the z=0 plane. If false, uses manual sensitivity.")]
    public bool lockPanToZZero = true;
    
    [Tooltip("Sensitivity for dragging the camera in Pan mode (when Lock Pan To Z Zero is false)")]
    public float panSensitivity = 0.1f;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector2 _rotation;
    private Vector2 lastMousePos;

    void Awake()
    {
        playerControls = new PlayerInputActions();
        cam = GetComponent<Camera>(); // Cache the camera attached to this object
    }

    void OnEnable()
    {
        playerControls.Enable();
        move = playerControls.Player.Move;
        fire = playerControls.Player.Fire;
        verticalupdown = playerControls.Player.VerticalUpDown;
        look = playerControls.Player.Look;
        mousePosition = playerControls.Player.MousePosition;
    }

    void OnDisable()
    {
        playerControls.Disable();
    }

    void Start()
    {
        UpdateCursorLock();
    }

    void UpdateCursorLock()
    {
        if (controlMode == ControlMode.Fly)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void MovementInput()
    {
        Vector2 input2 = move.ReadValue<Vector2>();
        Vector3 input = Vector3.zero;
        
        // Use 'transform' for all directions so it is 100% relative to the camera's view
        input += transform.right * input2.x;
        input += transform.forward * input2.y;
        input += transform.up * verticalupdown.ReadValue<float>();
        
        input = input.normalized;

        if (input.magnitude > 0)
        {
            currentVelocity += input * acceleration * Time.deltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // Apply the velocity to the parent body so the whole rig moves
        playerBody.position += currentVelocity * Time.deltaTime;
    }

    void PanInput()
    {
        // 1. Manually count ACTIVE touches and grab the first two
        int activeTouchCount = 0;
        UnityEngine.InputSystem.Controls.TouchControl touch0 = null;
        UnityEngine.InputSystem.Controls.TouchControl touch1 = null;

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                    phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                    phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    activeTouchCount++;
                    if (touch0 == null) touch0 = touch;
                    else if (touch1 == null) touch1 = touch;
                }
            }
        }

        bool isTouching = activeTouchCount > 0;

        // ----------------------------------------
        // 1. MOBILE TOUCH LOGIC (Pinch & 2-Finger Drag)
        // ----------------------------------------
        if (activeTouchCount >= 2 && touch0 != null && touch1 != null)
        {
            if (touch0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                touch1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                lastTouchCentroid = (touch0.position.ReadValue() + touch1.position.ReadValue()) / 2f;
                lastTouchDistance = Vector2.Distance(touch0.position.ReadValue(), touch1.position.ReadValue());
            }
            else if (touch0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved ||
                    touch1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 currentCentroid = (touch0.position.ReadValue() + touch1.position.ReadValue()) / 2f;
                float currentDistance = Vector2.Distance(touch0.position.ReadValue(), touch1.position.ReadValue());

                // --- PINCH TO ZOOM ---
                float distanceDelta = currentDistance - lastTouchDistance;
                if (Mathf.Abs(distanceDelta) > 0f && cam != null)
                {
                    float zoomDir = distanceDelta * 0.05f; 
                    Ray ray = cam.ScreenPointToRay(currentCentroid);
                    playerBody.position += ray.direction * zoomDir * scrollSpeed * Time.deltaTime;
                }

                // --- TWO-FINGER PAN ---
                if (lockPanToZZero && cam != null)
                {
                    Ray currentRay = cam.ScreenPointToRay(currentCentroid);
                    Ray lastRay = cam.ScreenPointToRay(lastTouchCentroid);
                    Plane zPlane = new Plane(Vector3.forward, Vector3.zero);

                    if (zPlane.Raycast(currentRay, out float dist1) && zPlane.Raycast(lastRay, out float dist2))
                    {
                        Vector3 currentWorld = currentRay.GetPoint(dist1);
                        Vector3 lastWorld = lastRay.GetPoint(dist2);
                        playerBody.position += (lastWorld - currentWorld);
                    }
                }
                else
                {
                    Vector2 panDelta = currentCentroid - lastTouchCentroid;
                    Vector3 panMovement = (-transform.right * panDelta.x * panSensitivity * 0.5f) +
                                        (-transform.up * panDelta.y * panSensitivity * 0.5f);
                    playerBody.position += panMovement;
                }

                lastTouchCentroid = currentCentroid;
                lastTouchDistance = currentDistance;
            }
        }
        // ----------------------------------------
        // 2. DESKTOP MOUSE LOGIC 
        // ----------------------------------------
        else if (!isTouching && Mouse.current != null)
        {
            // Scrolling (Zoom towards mouse)
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (scrollY != 0 && cam != null)
            {
                float scrollDir = Mathf.Clamp(scrollY, -1f, 1f);
                Vector2 mousePos = mousePosition.ReadValue<Vector2>();
                Ray ray = cam.ScreenPointToRay(mousePos);
                playerBody.position += ray.direction * scrollDir * scrollSpeed * Time.deltaTime;
            }

            // Panning (Right Mouse Drag)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                lastMousePos = mousePosition.ReadValue<Vector2>();
            }
            else if (Mouse.current.rightButton.isPressed)
            {
                if (lockPanToZZero && cam != null)
                {
                    Vector2 currentMousePos = mousePosition.ReadValue<Vector2>();
                    Ray currentRay = cam.ScreenPointToRay(currentMousePos);
                    Ray lastRay = cam.ScreenPointToRay(lastMousePos);
                    Plane zPlane = new Plane(Vector3.forward, Vector3.zero); 

                    if (zPlane.Raycast(currentRay, out float dist1) && zPlane.Raycast(lastRay, out float dist2))
                    {
                        Vector3 currentWorld = currentRay.GetPoint(dist1);
                        Vector3 lastWorld = lastRay.GetPoint(dist2);
                        playerBody.position += (lastWorld - currentWorld);
                    }

                    lastMousePos = currentMousePos;
                }
                else
                {
                    Vector2 panDelta = look.ReadValue<Vector2>();
                    Vector3 panMovement = (-transform.right * panDelta.x * panSensitivity) + (-transform.up * panDelta.y * panSensitivity);
                    playerBody.position += panMovement;
                }
            }
        }
    }

    void Update()
    {
        // Hard-lock the camera to the parent's exact position to prevent any drifting or swinging offsets.
        transform.localPosition = Vector3.zero;

        // Cycle through the 3 modes using Tab
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            controlMode = (ControlMode)(((int)controlMode + 1) % 3);
            UpdateCursorLock();
        }

        if (controlMode == ControlMode.Fixed)
        {
            // WASDQE only, no rotation
            MovementInput();
        }
        else if (controlMode == ControlMode.Fly)
        {
            // WASDQE + Rotation
            Vector2 lookInput = look.ReadValue<Vector2>();
            float mouseX = lookInput.x * mouseSensitivity;
            float mouseY = lookInput.y * mouseSensitivity;

            _rotation.x -= mouseY;
            _rotation.x = Mathf.Clamp(_rotation.x, -90f, 90f);

            // Vertical look (camera)
            transform.localRotation = Quaternion.Euler(_rotation.x, 0f, 0f);
            
            // Horizontal look (player body)
            playerBody.Rotate(Vector3.up * mouseX);

            MovementInput();
        }
        else if (controlMode == ControlMode.Pan)
        {
            // Scroll to zoom to mouse, Right-Click Drag to pan, no rotation
            PanInput();
        }
    }
}