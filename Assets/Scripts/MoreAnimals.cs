using UnityEngine;

// Even more creatures:
//
//   Penguin: waddles about the snowy land - and slides on its tummy on ice!
//   Camel:   lives in the desert. Press E to climb on and ride it.
//   Parrot:  flies around the jungle, and copies the sounds it hears!
//   Ghost:   only comes out in thunderstorms (survival). Floats right through walls.
//   Whale:   a gentle giant swimming in the sea, spraying water from its blowhole.
//   Mummy:   guards the treasure in the desert pyramid (survival).

public class Penguin : Monster
{
    private Transform model;
    private static Material black, white, orange;

    public override bool Hostile { get { return false; } }
    protected override string Called { get { return "penguin"; } }
    protected override int MostHealth { get { return 6; } }
    protected override float WalkSpeed { get { return OnIce ? 2.6f : 0.8f; } }
    protected override float ChaseSpeed { get { return OnIce ? 4f : 2f; } }
    public override float AimHeight { get { return 0.4f; } }

    bool OnIce
    {
        get
        {
            Vector3Int under = Vector3Int.RoundToInt(transform.position + Vector3.down * 0.3f);
            return world.GetBlock(under.x, under.y, under.z) == BlockWorld.Ice;
        }
    }

    protected override void BuildBody()
    {
        if (black == null)
        {
            black = BlockWorld.MakeMaterial(new Color(0.1f, 0.1f, 0.13f));
            white = BlockWorld.MakeMaterial(new Color(0.95f, 0.95f, 0.95f));
            orange = BlockWorld.MakeMaterial(new Color(1f, 0.6f, 0.1f));
        }
        model = Model(1f);
        AddPart(model, new Vector3(0f, 0.32f, 0f), new Vector3(0.34f, 0.5f, 0.3f), black);
        AddPart(model, new Vector3(0f, 0.3f, 0.13f), new Vector3(0.26f, 0.42f, 0.06f), white);    // white tummy
        AddPart(model, new Vector3(0f, 0.65f, 0.02f), new Vector3(0.26f, 0.22f, 0.24f), black);   // head
        AddPart(model, new Vector3(0f, 0.63f, 0.16f), new Vector3(0.08f, 0.05f, 0.1f), orange);   // beak
        AddPart(model, new Vector3(-0.06f, 0.69f, 0.141f), new Vector3(0.04f, 0.04f, 0.01f), white);
        AddPart(model, new Vector3(0.06f, 0.69f, 0.141f), new Vector3(0.04f, 0.04f, 0.01f), white);
        AddPart(model, new Vector3(-0.2f, 0.35f, 0f), new Vector3(0.05f, 0.3f, 0.15f), black);   // flippers
        AddPart(model, new Vector3(0.2f, 0.35f, 0f), new Vector3(0.05f, 0.3f, 0.15f), black);
        AddPart(model, new Vector3(-0.08f, 0.04f, 0.05f), new Vector3(0.1f, 0.05f, 0.16f), orange);   // feet
        AddPart(model, new Vector3(0.08f, 0.04f, 0.05f), new Vector3(0.1f, 0.05f, 0.16f), orange);
    }

    protected override void Animate(bool walking)
    {
        // Waddle side to side - or flop down and slide on ice
        bool sliding = walking && OnIce;
        float waddle = walking && !sliding ? Mathf.Sin(Time.time * 10f) * 12f : 0f;
        model.localRotation = Quaternion.Lerp(model.localRotation, Quaternion.Euler(sliding ? 80f : 0f, 0f, waddle), 10f * Time.deltaTime);
        model.localPosition = Vector3.up * (sliding ? 0.15f : 0f);
    }

    public override bool Interact(byte holding)
    {
        PortalTraveller.Announce("The penguin flaps its flippers at you!");
        return true;
    }
}

// You can ride it! While you're on its back, it goes where you go.
public class Camel : FourLegs, IRideable
{
    private bool ridden;
    private static Material sandy, dark;

    protected override string Called { get { return "camel"; } }
    protected override int MostHealth { get { return 14; } }
    protected override bool Frozen { get { return ridden; } }

    public float SpeedBoost { get { return 1.9f; } }
    public float SeatHeight { get { return 1.0f; } }
    public float GlideSpeed { get { return 10f; } }

    public void StartRiding() { ridden = true; }
    public void StopRiding() { ridden = false; }

    public void Carry(Vector3 riderFeet, float yaw, float speed, bool inTheAir)
    {
        transform.position = riderFeet;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        walkCycle += speed * Time.deltaTime * 1.5f;
        float swing = speed > 0.1f ? Mathf.Sin(walkCycle) * 30f : 0f;
        for (int i = 0; i < 4; i++) legs[i].localRotation = Quaternion.Euler((i == 0 || i == 3) ? swing : -swing, 0f, 0f);
    }

    protected override void BuildBody()
    {
        if (sandy == null)
        {
            sandy = BlockWorld.MakeMaterial(new Color(0.82f, 0.66f, 0.40f));
            dark = BlockWorld.MakeMaterial(new Color(0.15f, 0.12f, 0.1f));
        }
        Transform model = BuildFourLegs(1.15f, new Vector3(0.5f, 0.45f, 1.0f), sandy, new Vector3(0.24f, 0.24f, 0.36f), sandy, 0.75f, sandy);
        AddPart(model, new Vector3(0f, 1.32f, 0f), new Vector3(0.36f, 0.25f, 0.4f), sandy);           // the hump
        AddPart(model, new Vector3(0f, 1.15f, 0.55f), new Vector3(0.16f, 0.45f, 0.16f), sandy);       // long neck
        headPivot.localPosition += new Vector3(0f, 0.35f, 0.15f);
        AddPart(headPivot, new Vector3(-0.07f, 0.06f, 0.2f), new Vector3(0.04f, 0.04f, 0.01f), dark);
        AddPart(headPivot, new Vector3(0.07f, 0.06f, 0.2f), new Vector3(0.04f, 0.04f, 0.01f), dark);
    }

    public override bool Interact(byte holding) { return false; }   // (E climbs on - see BlockInteractor)
}

public class Parrot : Monster
{
    private Transform leftWing, rightWing;
    private string copying;
    private float copyAt;
    private float heardTime;
    private static Material[] feathers;
    private static Material beak;

    public override bool Hostile { get { return false; } }
    protected override string Called { get { return "parrot"; } }
    protected override int MostHealth { get { return 4; } }
    protected override bool Flies { get { return true; } }
    protected override float WalkSpeed { get { return 2.5f; } }
    protected override float ChaseSpeed { get { return 4f; } }
    public override float AimHeight { get { return 0.2f; } }

    protected override void BuildBody()
    {
        if (feathers == null)
        {
            feathers = new[] { BlockWorld.MakeMaterial(new Color(0.9f, 0.15f, 0.15f)), BlockWorld.MakeMaterial(new Color(0.2f, 0.75f, 0.3f)),
                               BlockWorld.MakeMaterial(new Color(0.2f, 0.5f, 0.95f)), BlockWorld.MakeMaterial(new Color(1f, 0.85f, 0.2f)) };
            beak = BlockWorld.MakeMaterial(new Color(0.2f, 0.2f, 0.2f));
        }
        Material body = feathers[Random.Range(0, feathers.Length)];
        Material wing = feathers[Random.Range(0, feathers.Length)];
        Transform model = Model(1f);
        AddPart(model, new Vector3(0f, 0.2f, 0f), new Vector3(0.16f, 0.24f, 0.18f), body);
        AddPart(model, new Vector3(0f, 0.38f, 0.04f), new Vector3(0.14f, 0.14f, 0.14f), body);
        AddPart(model, new Vector3(0f, 0.36f, 0.13f), new Vector3(0.05f, 0.07f, 0.06f), beak);
        AddPart(model, new Vector3(0f, 0.08f, -0.14f), new Vector3(0.06f, 0.04f, 0.2f), wing);    // tail
        leftWing = Pivot(model, new Vector3(-0.08f, 0.25f, 0f));
        rightWing = Pivot(model, new Vector3(0.08f, 0.25f, 0f));
        AddPart(leftWing, new Vector3(-0.1f, 0f, 0f), new Vector3(0.2f, 0.03f, 0.14f), wing);
        AddPart(rightWing, new Vector3(0.1f, 0f, 0f), new Vector3(0.2f, 0.03f, 0.14f), wing);
    }

    // Heard something? Copy it a moment later - squawk!
    protected override void Think()
    {
        if (Sounds.LastTime > heardTime && Sounds.LastName != null && !Sounds.LastWasParrot &&
            Vector3.Distance(Sounds.LastAt, transform.position) < 16f)
        {
            heardTime = Sounds.LastTime;
            copying = Sounds.LastName;
            copyAt = Time.time + Random.Range(0.6f, 1.2f);
        }
        if (copying != null && Time.time > copyAt)
        {
            Sounds.Copy(copying, transform.position);
            copying = null;
        }
    }

    protected override void Animate(bool walking)
    {
        float flap = Mathf.Sin(Time.time * 18f) * 40f;
        leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
        rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
    }

    public override bool Interact(byte holding)
    {
        PortalTraveller.Announce("Squawk! Pretty parrot!");
        Sounds.Copy("pop", transform.position);
        return true;
    }
}

public class Ghost : Monster
{
    private static Material sheet, eyes;
    protected override string Called { get { return "ghost"; } }
    protected override int MostHealth { get { return 8; } }
    protected override bool Flies { get { return true; } }
    protected override bool FireProof { get { return true; } }
    protected override bool PassesThroughWalls { get { return true; } }
    protected override float ChaseSpeed { get { return 2.2f; } }
    protected override int HitDamage { get { return 2; } }
    public override float AimHeight { get { return 0.8f; } }

    protected override void BuildBody()
    {
        if (sheet == null)
        {
            sheet = BlockWorld.MakeSeeThrough(new Color(0.92f, 0.95f, 1f, 0.55f));
            eyes = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.1f));
        }
        Transform model = Model(1.3f);
        AddPart(model, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.7f, 0.45f), sheet);
        AddPart(model, new Vector3(0f, 1.0f, 0f), new Vector3(0.4f, 0.2f, 0.38f), sheet);
        for (int i = 0; i < 3; i++) AddPart(model, new Vector3(-0.17f + i * 0.17f, 0.2f, 0f), new Vector3(0.14f, 0.15f, 0.42f), sheet);   // wavy bottom
        AddPart(model, new Vector3(-0.1f, 0.85f, 0.23f), new Vector3(0.08f, 0.12f, 0.01f), eyes);
        AddPart(model, new Vector3(0.1f, 0.85f, 0.23f), new Vector3(0.08f, 0.12f, 0.01f), eyes);
        AddPart(model, new Vector3(0f, 0.65f, 0.23f), new Vector3(0.1f, 0.1f, 0.01f), eyes);   // "oooo!"
    }

    protected override void Animate(bool walking)
    {
        transform.GetChild(0).localPosition = Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.15f;
        if (!Weather.Stormy) Destroy(gameObject);   // the storm's over - it fades away
    }
}

public class Mummy : Monster
{
    public Vector3 Post;
    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private static Material wrap, dark, glow;

    protected override string Called { get { return "mummy"; } }
    protected override int MostHealth { get { return 20; } }
    protected override float WalkSpeed { get { return 0.9f; } }
    protected override float ChaseSpeed { get { return 1.9f; } }
    protected override int HitDamage { get { return 4; } }
    protected override bool FearsStar { get { return false; } }
    protected override bool GuardPost(out Vector3 post) { post = Post; return true; }

    protected override void BuildBody()
    {
        if (wrap == null)
        {
            wrap = BlockWorld.MakeMaterial(new Color(0.85f, 0.8f, 0.65f));
            dark = BlockWorld.MakeMaterial(new Color(0.55f, 0.5f, 0.4f));
            glow = BlockWorld.MakeMaterial(new Color(0.3f, 1f, 0.4f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.2f, 0.9f, 0.3f));
        }
        Transform model = Model(1.45f);
        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.3f, 0.44f, 0.18f), wrap);
        for (int i = 0; i < 3; i++) AddPart(model, new Vector3(0f, 0.45f + i * 0.13f, 0.091f), new Vector3(0.3f, 0.025f, 0.01f), dark);   // bandages
        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg }) AddPart(leg, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.36f, 0.14f), wrap);
        leftArm = Pivot(model, new Vector3(-0.21f, 0.76f, 0f));
        rightArm = Pivot(model, new Vector3(0.21f, 0.76f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm }) AddPart(arm, new Vector3(0f, -0.18f, 0f), new Vector3(0.11f, 0.38f, 0.11f), wrap);
        Transform head = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(head, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), wrap);
        AddPart(head, new Vector3(0f, 0.12f, 0.151f), new Vector3(0.3f, 0.03f, 0.01f), dark);
        AddPart(head, new Vector3(-0.07f, 0.18f, 0.152f), new Vector3(0.05f, 0.04f, 0.01f), glow);   // spooky glowing eyes
        AddPart(head, new Vector3(0.07f, 0.18f, 0.152f), new Vector3(0.05f, 0.04f, 0.01f), glow);
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 5f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 25f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(-85f + swing * 0.2f, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(-85f - swing * 0.2f, 0f, 0f);
    }

    protected override void DropLoot()
    {
        Drop(BlockWorld.Gold, Random.Range(3, 7));
        Drop(BlockWorld.Diamond, 1);
    }

    protected override void OnDefeated()
    {
        Structures.MummyDefeated = true;
        Achievements.Unlock("mummy");
        PortalTraveller.Announce("The mummy crumbles to dust! The pyramid's treasure is yours.");
    }
}

// A whale, swimming along at the top of the sea and spraying water now and then
public class Whale : MonoBehaviour
{
    public BlockWorld world;
    private Vector3 heading;
    private float turnTimer, sprayTimer = 4f;
    private static Material skin, belly, spray;

    public static void Make(BlockWorld world, Vector3 at, Transform parent)
    {
        if (skin == null)
        {
            skin = BlockWorld.MakeMaterial(new Color(0.25f, 0.35f, 0.5f));
            belly = BlockWorld.MakeMaterial(new Color(0.8f, 0.82f, 0.85f));
            spray = BlockWorld.MakeMaterial(new Color(0.75f, 0.88f, 1f));
        }
        GameObject whale = new GameObject("Whale");
        whale.transform.SetParent(parent, false);
        whale.transform.position = at;
        Whale w = whale.AddComponent<Whale>();
        w.world = world;
        w.Part(Vector3.zero, new Vector3(1.4f, 1.1f, 3.2f), skin);
        w.Part(new Vector3(0f, -0.35f, 0.2f), new Vector3(1.2f, 0.4f, 2.6f), belly);
        w.Part(new Vector3(0f, 0.1f, -2f), new Vector3(0.6f, 0.5f, 1f), skin);
        w.Part(new Vector3(0f, 0.15f, -2.6f), new Vector3(1.8f, 0.15f, 0.6f), skin);   // tail flukes
        w.Part(new Vector3(-0.71f, 0.1f, 1.1f), new Vector3(0.02f, 0.12f, 0.12f), belly);   // eyes
        w.Part(new Vector3(0.71f, 0.1f, 1.1f), new Vector3(0.02f, 0.12f, 0.12f), belly);
    }

    void Part(Vector3 at, Vector3 size, Material look)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(transform, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = look;
    }

    void Update()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        turnTimer -= Time.deltaTime;
        if (turnTimer <= 0f || heading == Vector3.zero)
        {
            turnTimer = Random.Range(4f, 9f);
            heading = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
        }

        Vector3 next = transform.position + heading * 1.5f * Time.deltaTime;
        Vector3Int ahead = Vector3Int.RoundToInt(next + heading * 2.5f);
        if (world.GetBlock(ahead.x, ahead.y, ahead.z) != BlockWorld.Water || world.GetBlock(ahead.x, ahead.y - 1, ahead.z) != BlockWorld.Water)
        {
            heading = -heading;   // shallow water ahead - turn round
            return;
        }
        transform.position = next;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime);

        // Pffffff!
        sprayTimer -= Time.deltaTime;
        if (sprayTimer <= 0f)
        {
            sprayTimer = Random.Range(5f, 10f);
            for (int i = 0; i < 18; i++)
            {
                GameObject drop = new GameObject("Spray");
                drop.transform.position = transform.position + Vector3.up * 0.7f + transform.forward * 0.6f;
                drop.transform.localScale = Vector3.one * 0.12f;
                drop.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
                drop.AddComponent<MeshRenderer>().sharedMaterial = spray;
                Rigidbody body = drop.AddComponent<Rigidbody>();
                body.linearVelocity = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(5f, 8f), Random.Range(-0.8f, 0.8f));
                Destroy(drop, 1.5f);
            }
            if (BlockInteractor.Me != null && Vector3.Distance(BlockInteractor.Me.transform.position, transform.position) < 25f)
                Sounds.Splash(transform.position);
        }
    }
}
