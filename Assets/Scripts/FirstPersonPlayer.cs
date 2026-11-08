using UnityEngine;

// Walking, looking around and jumping - the Minecraft way.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float jumpSpeed = 7f;
    public float gravity = 20f;

    public float swimSpeed = 4f;

    [Header("Looking")]
    public float lookSensitivity = 2.5f;
    public float maxLookAngle = 85f;
    public float arrowKeyLookSpeed = 120f;   // degrees per second

    // The frame the mouse was last grabbed on, so the click that grabs it
    // doesn't also break a block
    public static int MouseLockedFrame { get; private set; }

    private CharacterController controller;
    private Transform head;
    private float pitch;
    private float verticalSpeed;
    private bool wasSwimming;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        head = Camera.main.transform;
        LockMouse(true);
    }

    void Update()
    {
        // Escape lets go of the mouse so you can get back to the Unity editor.
        // Click the game or press Enter to carry on playing.
        if (Input.GetKeyDown(KeyCode.Escape))
            LockMouse(false);
        else if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return)) && Cursor.lockState != CursorLockMode.Locked)
            LockMouse(true);

        Look();
        Move();
    }

    void LockMouse(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        if (locked) MouseLockedFrame = Time.frameCount;
    }

    // -1 if the first key is held, +1 for the second, 0 for neither or both
    static float KeyPair(KeyCode negative, KeyCode positive)
    {
        return (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Look with the mouse, the arrow keys, or both
        float arrowStep = arrowKeyLookSpeed * Time.deltaTime;
        float mouseX = Input.GetAxisRaw("Mouse X") * lookSensitivity + KeyPair(KeyCode.LeftArrow, KeyCode.RightArrow) * arrowStep;
        float mouseY = Input.GetAxisRaw("Mouse Y") * lookSensitivity + KeyPair(KeyCode.DownArrow, KeyCode.UpArrow) * arrowStep;

        // The body spins left and right...
        transform.Rotate(0f, mouseX, 0f);

        // ...and only the head tips up and down, so we can never somersault.
        pitch = Mathf.Clamp(pitch - mouseY, -maxLookAngle, maxLookAngle);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        // WASD only - Unity's built-in "Horizontal" and "Vertical" would also
        // walk us around with the arrow keys, and those are for looking
        float sideways = KeyPair(KeyCode.A, KeyCode.D);
        float forwards = KeyPair(KeyCode.S, KeyCode.W);

        Vector3 move = (transform.right * sideways + transform.forward * forwards).normalized * walkSpeed;
        bool swimming = InWater();

        if (swimming)
        {
            move *= 0.6f;   // water slows you down

            // Hold Space to swim up, otherwise sink gently
            if (Input.GetKey(KeyCode.Space))
                verticalSpeed = swimSpeed;
            else
                verticalSpeed = Mathf.Max(verticalSpeed - gravity * 0.25f * Time.deltaTime, -2f);
        }
        else if (wasSwimming && Input.GetKey(KeyCode.Space))
        {
            verticalSpeed = jumpSpeed;   // a hop as we reach the surface, so we can climb out onto the shore
        }
        else if (controller.isGrounded)
        {
            verticalSpeed = -1f;   // a little downward push keeps us stuck to the floor

            if (Input.GetKeyDown(KeyCode.Space))
                verticalSpeed = jumpSpeed;
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        wasSwimming = swimming;
        move.y = verticalSpeed;
        controller.Move(move * Time.deltaTime);

        // Fell off the edge of the world? Back to the start.
        if (transform.position.y < -20f)
            Respawn();
    }

    bool InWater()
    {
        if (BlockWorld.Instance == null) return false;

        Vector3Int cell = Vector3Int.RoundToInt(transform.position + Vector3.up * 0.4f);
        return BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.Water;
    }

    void Respawn()
    {
        // A CharacterController ignores teleports unless we switch it off first
        controller.enabled = false;
        transform.position = BlockWorld.Instance.FindSpawnPoint();   // the old spot may have been blown up
        controller.enabled = true;
        verticalSpeed = 0f;
    }
}
