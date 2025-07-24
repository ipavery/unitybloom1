using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("Mouse Look")]
    public PlayerInputActions playerControls;
    private InputAction move;
    private InputAction fire;
    private InputAction verticalupdown;
    public float mouseSensitivity = 2f;
    public Transform playerBody;
    float xRotation = 0f;

    [Header("Movement")]
    public float acceleration = 80f;
    public float maxSpeed = 300f;
    public float deceleration = 10f;
    private Vector3 currentVelocity = Vector3.zero;

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
    }

    void OnDisable()
    {
        move.Disable();
        fire.Disable();
        verticalupdown.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Mouse Look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Vertical look (camera)
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal look (player body)
        playerBody.Rotate(Vector3.up * mouseX);


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
            currentVelocity += transform.forward * Input.mouseScrollDelta.y * acceleration;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed * 2f);
        }

        playerBody.position += currentVelocity * Time.deltaTime;
    }
}