using UnityEngine;
using UnityEngine.InputSystem;

public enum ControlMode
{
    Fly,
    Fixed
}

public class MouseLook : MonoBehaviour
{
    [Header("Mouse Look")]
    public ControlMode controlMode = ControlMode.Fixed; // Default to fixed mode
    public PlayerInputActions playerControls;
    private InputAction move;
    private InputAction fire;
    private InputAction verticalupdown;
    private InputAction look;
    public float mouseSensitivity = 2f;
    [Tooltip("Smoothing factor (default ~0.05)")]
    [Range(0f, 1f)]
    public float smoothing = 0.05f;
    public Transform playerBody;


    [Header("Movement")]
    public float acceleration = 80f;
    public float maxSpeed = 300f;
    public float deceleration = 10f;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector2 _smoothVelocity;
    private Vector2 _currentLooking;
    private Vector2 _rotation;
    float precisionExponent = .01f; // Adjust this value to control the precision of the mouse movement


    void Awake()
    {
        playerControls = new PlayerInputActions();
    }

    void OnEnable()
    {
        playerControls.Enable();
        move = playerControls.Player.Move;
        fire = playerControls.Player.Fire;
        verticalupdown = playerControls.Player.VerticalUpDown;
        look = playerControls.Player.Look;

        move.Enable();
        fire.Enable();
        verticalupdown.Enable();
        look.Enable();
    }

    void OnDisable()
    {
        move.Disable();
        fire.Disable();
        verticalupdown.Disable();
        look.Disable();
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
        }

        else if (controlMode == ControlMode.Fixed)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


    }

    void MovementInput()
    {
        // Movement Input
        //left/right and forward/backward input using the new Input System
        // This assumes you have a PlayerInputActions class set up with a "Move" action
        Vector2 input2 = move.ReadValue<Vector2>();
        Vector3 input = Vector3.zero;

        input += playerBody.right * input2.x;
        input += transform.forward * input2.y;

        // Vertical up/down input using the new Input System - uses an Axis action with a 1D Axis binding
        input += transform.up * verticalupdown.ReadValue<float>();

        // Normalize input to ensure consistent speed in all directions (although right/left is already normalized, up/down is not)
        input = input.normalized;

        // Accelerate
        if (input.magnitude > 0)
        {
            currentVelocity += input * acceleration * Time.deltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // Scroll movement
        if (Input.mouseScrollDelta.y != 0)
        {
            currentVelocity += transform.forward * Input.mouseScrollDelta.y * acceleration * .3f;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed * 2f);
        }

        playerBody.position += currentVelocity * Time.deltaTime;
    }

    void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            controlMode = (controlMode == ControlMode.Fly) ? ControlMode.Fixed : ControlMode.Fly;
            UpdateCursorLock();
        }

        if (controlMode == ControlMode.Fixed)
        {
            MovementInput();
        }
        else if (controlMode == ControlMode.Fly)
        {
            // Mouse Look -- i tried to switch to the new Input System but it didn't work smoothly, so using the old Input System for mouse look
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            _rotation.x -= mouseY;
            _rotation.x = Mathf.Clamp(_rotation.x, -90f, 90f);

            // Vertical look (camera)
            transform.localRotation = Quaternion.Euler(_rotation.x, 0f, 0f);

            // Horizontal look (player body)
            playerBody.Rotate(Vector3.up * mouseX);

            MovementInput();
        }

    }
}