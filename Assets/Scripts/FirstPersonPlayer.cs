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
    public float sprintBoost = 1.6f;   // hold Shift to run this much faster
    public float bounceSpeed = 16f;   // how hard a bounce pad throws you up
    public float conveyorSpeed = 4f;   // how fast a conveyor belt carries you
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
    private float lastBurnt = -100f;
    private float stepTravel;          // how far we've walked since the last footstep
    private float normalView = -1f;    // the camera's field of view when walking (it widens a bit when you run)   // how fast we're really going sideways, which on ice lags behind the keys

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
        if (Tutorial.IsOpen) return;   // the game waits while you watch
        // While the inventory is open, keys and clicks belong to it. We still
        // fall and sink, but can't walk, look around, or pause.
        bool inInventory = BlockInteractor.InventoryOpen || Time.frameCount == BlockInteractor.InventoryClosedFrame;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.Dead) inInventory = true;   // knocked out: no walking about

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

        // Hold Shift to sprint (in survival, not when you're really hungry)
        PlayerHealth tummy = PlayerHealth.Instance;
        bool sprinting = !ignoreKeys && forwards > 0f && riding == null && !swimming &&
                         (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                         (tummy == null || tummy.Food > 6);
        if (sprinting) move *= sprintBoost;
        if (tummy != null) tummy.Sprinting = sprinting;

        Camera eye = head.GetComponent<Camera>();
        if (eye != null)
        {
            if (normalView < 0f) normalView = eye.fieldOfView;
            eye.fieldOfView = Mathf.Lerp(eye.fieldOfView, normalView * (sprinting ? 1.12f : 1f), 8f * Time.deltaTime);
        }

        // Splash! as you fall in the water
        if (swimming && !wasSwimming) Sounds.Splash(transform.position);

        // On ice you keep sliding, and it takes a while to get going or stop
        if (controller.isGrounded && BlockUnderFeet() == BlockWorld.Ice)
            slide = Vector3.MoveTowards(slide, move, iceGrip * walkSpeed * Time.deltaTime);
        else
            slide = move;
        move = slide;

        // Standing on a conveyor belt? It carries you along
        if (controller.isGrounded)
        {
            byte belt = BlockUnderFeet();
            if (BlockWorld.IsConveyor(belt))
                move += (Vector3)BlockWorld.ConveyorDirection(belt) * conveyorSpeed;
        }

        // In a boat or a minecart? It does the moving, and we sit in it
        if (inVehicle)
        {
            if (vehicle == null) { inVehicle = false; controller.enabled = true; }   // it got picked up or blown up
            else
            {
                vehicle.Drive(forwards, transform);
                transform.position = vehicle.Seat;
                return;
            }
        }

        // Standing on a slide? Wheee!
        byte inside = BlockAt(transform.position + Vector3.up * 0.15f);
        byte slideBlock = BlockWorld.IsSlide(inside) ? inside : BlockUnderFeet();
        if (BlockWorld.IsSlide(slideBlock)) move += BlockWorld.SlideDirection(slideBlock) * 7f;

        // Cactus prickles (survival)
        if (PlayerHealth.Instance != null && TouchingCactus() && Time.time - lastPricked > 0.6f)
        {
            lastPricked = Time.time;
            PlayerHealth.Instance.Hurt(1, "You got pricked by a cactus.");
        }

        bool onLadder = riding == null && (BlockWorld.IsLadder(inside) || BlockWorld.IsLadder(BlockAt(transform.position + Vector3.up * 1.1f)));
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
        else if (onLadder)
        {
            // Climb up with W or Space, down with S - and hold on otherwise
            verticalSpeed = forwards > 0f || spaceHeld ? 3.5f : forwards < 0f ? -3.5f : 0f;
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
            if (under == BlockWorld.Trampoline)
                verticalSpeed = bounceSpeed * 1.45f;   // BOING!
            else if (under == BlockWorld.BouncePad || under == BlockWorld.MushroomCap)
                verticalSpeed = bounceSpeed;   // boing!
            else if (spacePressed)
                verticalSpeed = jumpSpeed;
        }
        else
        {
            verticalSpeed -= Gravity * Time.deltaTime;
        }

        // Walked into fire? Ouch! A hop up out of it
        PlayerHealth health = PlayerHealth.Instance;   // only in survival worlds
        if (InFire() && Time.time - lastBurnt > 1f)
        {
            lastBurnt = Time.time;
            launch = Mathf.Max(launch, 7f);
            PortalTraveller.Announce("Ouch! Hot hot hot!");
            if (health != null) health.Hurt(2, "You got burnt by fire.");
        }

        // In survival, lava hurts a lot - and throws you up so you can get out
        if (health != null && InLava() && Time.time - lastBurnt > 0.5f)
        {
            lastBurnt = Time.time;
            launch = Mathf.Max(launch, 9f);
            PortalTraveller.Announce("Ouch, lava! Get out quick!");
            health.Hurt(3, "You fell in the lava.");
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
        bool wasGrounded = controller.isGrounded;
        Vector3 before = transform.position;
        controller.Move(move * Time.deltaTime);

        // Footsteps, which sound like whatever you're walking on
        if (controller.isGrounded && riding == null && !swimming)
        {
            Vector3 walked = transform.position - before;
            walked.y = 0f;
            stepTravel += walked.magnitude;
            if (stepTravel > 1.9f)
            {
                stepTravel = 0f;
                byte under = BlockUnderFeet();
                if (under != BlockWorld.Air) Sounds.Step(under, transform.position);
            }
        }
        if (controller.isGrounded && !wasGrounded && riding == null && !swimming)
            Sounds.Step(BlockUnderFeet(), transform.position);   // landing

        // Hit the ground hard? In survival that hurts (not in water, on a
        // bounce pad, on a mushroom, or riding something)
        if (health != null && controller.isGrounded && !wasGrounded && riding == null && !swimming && !InWater() && !onLadder)
        {
            byte landedOn = BlockUnderFeet();
            if (landedOn != BlockWorld.BouncePad && landedOn != BlockWorld.MushroomCap && landedOn != BlockWorld.Trampoline)
                health.Landed(-verticalSpeed, gravity);
        }

        // The unicorn goes wherever we go, right underneath us
        if (riding != null)
            Ride.Carry(transform.position, transform.eulerAngles.y, flat.magnitude, !controller.isGrounded);
        else if (head.localPosition != eyeHeight)
            head.localPosition = eyeHeight;   // the unicorn vanished from under us (through a portal, say)

        // Fell off the edge of the world? Back to the start (and in survival, it hurts).
        if (transform.position.y < -20f)
        {
            Respawn();
            if (health != null) health.Hurt(6, "You fell out of the world.");
        }

        // Fell in the lava? Far too hot! Whoosh - back to safety.
        // (In survival you have to get out yourself - see above.)
        if (health == null && InLava())
        {
            Respawn();
            PortalTraveller.Announce("Ouch, too hot! The lava sent you back to safety.");
        }
    }

    byte BlockAt(Vector3 point)
    {
        if (BlockWorld.Instance == null) return BlockWorld.Air;
        Vector3Int cell = Vector3Int.RoundToInt(point);
        return BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
    }

    private float lastPricked = -100f;
    bool TouchingCactus()
    {
        foreach (float dx in new[] { -0.45f, 0.45f })
            foreach (float dz in new[] { -0.45f, 0.45f })
                foreach (float dy in new[] { 0.2f, 1.2f })
                    if (BlockAt(transform.position + new Vector3(dx, dy, dz)) == BlockWorld.Cactus) return true;
        return false;
    }

    // --- Boats and minecarts ---

    private Vehicle vehicle;
    private bool inVehicle;
    public Vehicle InVehicle { get { return inVehicle ? vehicle : null; } }

    public void Board(Vehicle ride)
    {
        Dismount();
        vehicle = ride;
        inVehicle = true;
        ride.Ridden = true;
        controller.enabled = false;
        transform.position = ride.Seat;
        verticalSpeed = 0f;
        Achievements.Unlock(ride is Cart ? "cart" : "boat");
    }

    public void LeaveVehicle()
    {
        if (!inVehicle) return;
        inVehicle = false;
        Vector3 off = transform.position + Vector3.up * 1f;
        if (vehicle != null)
        {
            vehicle.Ridden = false;
            off = vehicle.transform.position + Vector3.up * 0.7f + transform.right * 1f;
        }
        vehicle = null;
        transform.position = off;
        controller.enabled = true;
    }

    // Where the compass points: your bed, or where the world began
    public static Vector3 HomeSpot()
    {
        Vector3 bed;
        return FindBed(out bed) ? bed : BlockWorld.Instance.FindSpawnPoint();
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
        if (creature is Dragon) Achievements.Unlock("dragon");

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

    // --- Beds ---
    //
    // Sleep in a bed and you wake up there from now on, instead of where the
    // world began (as long as the bed is still there, in the same place).

    static string BedKey { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-bed"; } }

    public static void SetBed(Vector3Int bed)
    {
        PlayerPrefs.SetString(BedKey, WorldMenu.CurrentPlace + "," + bed.x + "," + bed.y + "," + bed.z);
        PlayerPrefs.Save();
    }

    static bool FindBed(out Vector3 spot)
    {
        spot = Vector3.zero;
        string[] parts = PlayerPrefs.GetString(BedKey, "").Split(',');
        int place, x, y, z;
        if (parts.Length != 4 || !int.TryParse(parts[0], out place) || !int.TryParse(parts[1], out x) ||
            !int.TryParse(parts[2], out y) || !int.TryParse(parts[3], out z)) return false;
        if (place != WorldMenu.CurrentPlace) return false;

        byte there = BlockWorld.Instance.GetBlock(x, y, z);
        if (!BlockWorld.IsFurniture(there) || BlockWorld.FurnitureItem(there) != BlockWorld.Bed) return false;

        spot = new Vector3(x, y + 0.6f, z);   // lying on top of it
        return true;
    }

    public void Respawn()
    {
        LeaveVehicle();
        Dismount();

        // Back to your bed - or where the world began, if you haven't got one.
        // (A CharacterController ignores teleports unless we switch it off first.)
        Vector3 bed;
        controller.enabled = false;
        transform.position = FindBed(out bed) ? bed : BlockWorld.Instance.FindSpawnPoint();   // the old spot may have been blown up
        controller.enabled = true;
        verticalSpeed = 0f;
    }
}
