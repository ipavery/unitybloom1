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

    [Tooltip("Sensitivity for looking around in Fly mode")]
    public float mouseSensitivity = 0.1f; 

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
        // 1. Scrolling (Zoom towards mouse)
        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (scrollY != 0 && cam != null)
        {
            // Normalize the scroll value to -1 or 1
            float scrollDir = Mathf.Clamp(scrollY, -1f, 1f);
            
            // Cast a ray from the camera through the mouse pointer on the screen
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);
            
            // Move along the exact direction the mouse is pointing
            playerBody.position += ray.direction * scrollDir * scrollSpeed * Time.deltaTime;
        }

        // 2. Panning (Right Mouse Drag)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            // Record initial mouse position when drag starts
            lastMousePos = Mouse.current.position.ReadValue();
        }
        else if (Mouse.current.rightButton.isPressed)
        {
            if (lockPanToZZero && cam != null)
            {
                // Exact 1:1 mapping using the Z=0 plane
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                
                Ray currentRay = cam.ScreenPointToRay(currentMousePos);
                Ray lastRay = cam.ScreenPointToRay(lastMousePos);
                Plane zPlane = new Plane(Vector3.forward, Vector3.zero); // Z=0 plane

                // If both rays hit the mathematical plane, calculate the world delta
                if (zPlane.Raycast(currentRay, out float dist1) && zPlane.Raycast(lastRay, out float dist2))
                {
                    Vector3 currentWorld = currentRay.GetPoint(dist1);
                    Vector3 lastWorld = lastRay.GetPoint(dist2);
                    
                    // Move the camera by the inverted difference to keep the world point pinned to the cursor
                    playerBody.position += (lastWorld - currentWorld);
                }
                
                lastMousePos = currentMousePos;
            }
            else
            {
                // Manual sensitivity mapping (fallback)
                Vector2 panDelta = look.ReadValue<Vector2>(); 
                Vector3 panMovement = (-transform.right * panDelta.x * panSensitivity) + (-transform.up * panDelta.y * panSensitivity);
                playerBody.position += panMovement;
            }
        }
    }

    void Update()
    {
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