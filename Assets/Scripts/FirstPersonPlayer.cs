using UnityEngine;

// Walking, looking around and jumping - the Minecraft way.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float jumpSpeed = 7f;
    public float gravity = 20f;
    public float magicLandGravity = 0.45f;   // in the Magic Land gravity is this much weaker, so you jump higher

    [Header("Flying (on a unicorn or dragon, or with wings)")]
    public float flySpeed = 6f;           // how fast you rise while holding Space
    public float glideSpeed = 2.5f;       // with wings, the fastest you drift down when you let go

    [Header("Flying with wings")]
    public float wingSpeedBoost = 1.4f;   // how much faster than walking, up in the air

    public float swimSpeed = 4f;
    public float bounceSpeed = 16f;   // how hard a bounce pad throws you up
    public float iceGrip = 1f;        // on ice, how many times walk speed you can speed up or slow down by each second - lower is slippier

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
    private MonoBehaviour riding;  // the unicorn or dragon we're sitting on, if any
    private BlockInteractor hands; // to see whether we're holding wings
    private Vector3 eyeHeight;     // where the camera sits when we're on our own feet
    private Vector3 slide;
    private float launch;              // a push upwards waiting to happen (from a fire vent, or fire)
    private float lastBurnt = -100f;   // how fast we're really going sideways, which on ice lags behind the keys

    // Set when you came through a magic portal on a unicorn, so you're still riding it
    public bool MountNearestUnicornOnStart { get; set; }

    // How far up or down we're looking, so a saved world can put it back
    public float Pitch
    {
        get { return pitch; }
        set { pitch = value; }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        head = Camera.main.transform;
        eyeHeight = head.localPosition;
        hands = GetComponent<BlockInteractor>();
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        LockMouse(true);

        if (MountNearestUnicornOnStart)
        {
            Unicorn closest = null;
            float best = 8f;
            foreach (Unicorn unicorn in FindObjectsByType<Unicorn>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(unicorn.transform.position, transform.position);
                if (distance < best)
                {
                    best = distance;
                    closest = unicorn;
                }
            }
            if (closest != null) Mount(closest);
        }
    }

    void Update()
    {
        // While the inventory is open, keys and clicks belong to it. We still
        // fall and sink, but can't walk, look around, or pause.
        bool inInventory = BlockInteractor.InventoryOpen || Time.frameCount == BlockInteractor.InventoryClosedFrame;

        if (!inInventory)
        {
            // Escape lets go of the mouse so you can get back to the Unity editor.
            // Click the game or press Enter to carry on playing.
            if (Input.GetKeyDown(KeyCode.Escape))
                LockMouse(false);
            else if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return)) && Cursor.lockState != CursorLockMode.Locked)
                LockMouse(true);

            Look();
        }

        Move(inInventory);
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

    void Move(bool ignoreKeys)
    {
        // WASD only - Unity's built-in "Horizontal" and "Vertical" would also
        // walk us around with the arrow keys, and those are for looking
        float sideways = ignoreKeys ? 0f : KeyPair(KeyCode.A, KeyCode.D);
        float forwards = ignoreKeys ? 0f : KeyPair(KeyCode.S, KeyCode.W);
        bool spaceHeld = !ignoreKeys && Input.GetKey(KeyCode.Space);
        bool spacePressed = !ignoreKeys && Input.GetKeyDown(KeyCode.Space);

        Vector3 move = (transform.right * sideways + transform.forward * forwards).normalized * walkSpeed;
        bool swimming = InWater();

        // On ice you keep sliding, and it takes a while to get going or stop
        if (controller.isGrounded && BlockUnderFeet() == BlockWorld.Ice)
            slide = Vector3.MoveTowards(slide, move, iceGrip * walkSpeed * Time.deltaTime);
        else
            slide = move;
        move = slide;

        bool winged = hands != null && hands.HoldingWings;

        if (riding != null || winged)
        {
            // On a unicorn, or holding wings, you can fly! Hold Space to go
            // up, and let go to glide gently back down.
            if (riding != null) move *= Ride.SpeedBoost;
            else if (!controller.isGrounded) move *= wingSpeedBoost;
            if (spaceHeld)
                verticalSpeed = flySpeed;
            else if (controller.isGrounded)
                verticalSpeed = -1f;
            else
                verticalSpeed = Mathf.Max(verticalSpeed - Gravity * Time.deltaTime, -(riding != null ? Ride.GlideSpeed : glideSpeed));
        }
        else if (swimming)
        {
            move *= 0.6f;   // water slows you down

            // Hold Space to swim up, otherwise sink gently
            if (spaceHeld)
                verticalSpeed = swimSpeed;
            else
                verticalSpeed = Mathf.Max(verticalSpeed - gravity * 0.25f * Time.deltaTime, -2f);
        }
        else if (wasSwimming && spaceHeld)
        {
            verticalSpeed = jumpSpeed;   // a hop as we reach the surface, so we can climb out onto the shore
        }
        else if (controller.isGrounded)
        {
            verticalSpeed = -1f;   // a little downward push keeps us stuck to the floor

            byte under = BlockUnderFeet();
            if (under == BlockWorld.BouncePad || under == BlockWorld.MushroomCap)
                verticalSpeed = bounceSpeed;   // boing!
            else if (spacePressed)
                verticalSpeed = jumpSpeed;
        }
        else
        {
            verticalSpeed -= Gravity * Time.deltaTime;
        }

        // Walked into fire? Ouch! A hop up out of it
        if (InFire() && Time.time - lastBurnt > 1f)
        {
            lastBurnt = Time.time;
            launch = Mathf.Max(launch, 7f);
            PortalTraveller.Announce("Ouch! Hot hot hot!");
        }

        // Launched by a fire vent (or a hop out of fire)
        if (launch > 0f)
        {
            verticalSpeed = launch;
            launch = 0f;
        }

        wasSwimming = swimming;
        Vector3 flat = new Vector3(move.x, 0f, move.z);
        move.y = verticalSpeed;
        controller.Move(move * Time.deltaTime);

        // The unicorn goes wherever we go, right underneath us
        if (riding != null)
            Ride.Carry(transform.position, transform.eulerAngles.y, flat.magnitude, !controller.isGrounded);
        else if (head.localPosition != eyeHeight)
            head.localPosition = eyeHeight;   // the unicorn vanished from under us (through a portal, say)

        // Fell off the edge of the world? Back to the start.
        if (transform.position.y < -20f)
            Respawn();

        // Fell in the lava? Far too hot! Whoosh - back to safety.
        if (InLava())
        {
            Respawn();
            PortalTraveller.Announce("Ouch, too hot! The lava sent you back to safety.");
        }
    }

    // A sudden push upwards, like being shot up by a fire vent
    public void LaunchUp(float speed)
    {
        launch = speed;
    }

    bool InFire()
    {
        if (BlockWorld.Instance == null) return false;

        Vector3Int feet = Vector3Int.RoundToInt(transform.position + Vector3.up * 0.3f);
        return BlockWorld.Instance.GetBlock(feet.x, feet.y, feet.z) == BlockWorld.Fire;
    }

    bool InLava()
    {
        if (BlockWorld.Instance == null) return false;

        Vector3Int feet = Vector3Int.RoundToInt(transform.position + Vector3.up * 0.2f);
        return BlockWorld.Instance.GetBlock(feet.x, feet.y, feet.z) == BlockWorld.Lava;
    }

    // Moon jumping in the Magic Land!
    float Gravity
    {
        get { return WorldMenu.CurrentPlace == WorldSaves.MagicLand ? gravity * magicLandGravity : gravity; }
    }

    byte BlockUnderFeet()
    {
        if (BlockWorld.Instance == null) return BlockWorld.Air;

        Vector3Int under = Vector3Int.RoundToInt(transform.position + Vector3.down * 0.3f);
        return BlockWorld.Instance.GetBlock(under.x, under.y, under.z);
    }

    bool InWater()
    {
        if (BlockWorld.Instance == null) return false;

        Vector3Int cell = Vector3Int.RoundToInt(transform.position + Vector3.up * 0.4f);
        return BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.Water;
    }

    // --- Riding ---

    public bool IsRiding { get { return riding != null; } }
    public MonoBehaviour RidingOn { get { return riding; } }

    IRideable Ride { get { return (IRideable)riding; } }

    // Up in the air (flying, gliding, jumping or falling) - wings flap while this is true
    public bool Airborne { get { return controller != null && !controller.isGrounded; } }

    // Climb onto a unicorn's or dragon's back
    public void Mount(MonoBehaviour creature)
    {
        riding = creature;
        Ride.StartRiding();

        // Sit right where it's standing, facing the way it faces
        controller.enabled = false;
        transform.position = creature.transform.position;
        transform.rotation = Quaternion.Euler(0f, creature.transform.eulerAngles.y, 0f);
        controller.enabled = true;

        head.localPosition = eyeHeight + Vector3.up * Ride.SeatHeight;
        verticalSpeed = 0f;
    }

    // Hop off - it stays right where it is (and floats down if you were flying)
    public void Dismount()
    {
        if (riding != null) Ride.StopRiding();
        riding = null;
        head.localPosition = eyeHeight;
    }

    void Respawn()
    {
        Dismount();

        // A CharacterController ignores teleports unless we switch it off first
        controller.enabled = false;
        transform.position = BlockWorld.Instance.FindSpawnPoint();   // the old spot may have been blown up
        controller.enabled = true;
        verticalSpeed = 0f;
    }
}
