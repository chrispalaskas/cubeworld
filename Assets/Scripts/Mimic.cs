using UnityEngine;

// The Mimic: a sneaky monster that pretends to be a block.
//
// It sits perfectly still on the ground, looking just like the dirt, grass,
// stone, sand or snow around it. Walk right up to it and - SNAP! - its top
// flips open like a lid, with a mouth full of teeth and a long tongue, and it
// hops after you, chomping. Get far enough away and it settles down and
// pretends to be a block again.
//
// Hitting a block that turns out to be a mimic wakes it up too (so if a
// block won't break... watch out!). Beat one and it coughs up whatever it
// swallowed: blocks, and sometimes gold or a diamond.
public class Mimic : Monster
{
    const float WakeRange = 3.2f;     // how close you can get before it springs
    const float SleepRange = 18f;     // run this far away and it gives up
    const float SleepAfter = 8f;      // ...for this long

    private bool awake;
    private float awayFor;
    private float hopTimer;
    private float lidOpen;            // 0 shut (a block), 1 wide open
    private byte disguise = BlockWorld.Dirt;
    private Transform lid, body, inside;
    private Renderer[] bodyLooks;
    private static Material teeth, tongue, mouth, eyes;

    protected override string Called { get { return "mimic"; } }
    protected override int MostHealth { get { return 14; } }
    protected override float WalkSpeed { get { return OnGround ? 0f : 1.2f; } }
    protected override float ChaseSpeed { get { return OnGround ? 0f : 3.6f; } }
    protected override float NoticeRange { get { return SleepRange; } }
    protected override int HitDamage { get { return 4; } }
    protected override float HitRange { get { return 1.2f; } }
    protected override float HitSeconds { get { return 1f; } }
    public override float AimHeight { get { return 0.5f; } }
    public override float HitSize { get { return 0.7f; } }

    // Asleep, it doesn't move at all - it's just a block
    protected override bool Frozen { get { return !awake; } }

    protected override void Think()
    {
        // Big chomping hops towards you
        hopTimer -= Time.deltaTime;
        if (OnGround && hopTimer <= 0f)
        {
            hopTimer = chasing ? 0.7f : 1.6f;
            fallSpeed = 6.5f;
        }
    }

    // Waking up and going back to sleep (this runs even while it's pretending)
    void LateUpdate()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;
        PlayerHealth player = PlayerHealth.Instance;
        bool canSeeYou = player != null && !player.Dead && !Effects.Invisible;
        float far = player != null ? Vector3.Distance(player.transform.position, transform.position) : 999f;

        if (!awake)
        {
            if (canSeeYou && far < WakeRange) WakeUp();
        }
        else
        {
            awayFor = !canSeeYou || far > SleepRange ? awayFor + Time.deltaTime : 0f;
            if (awayFor > SleepAfter && OnGround) GoToSleep();
        }

        // The lid flaps open and shut as it chomps
        float want = !awake ? 0f : OnGround ? 0.35f : 1f;
        lidOpen = Mathf.MoveTowards(lidOpen, want, Time.deltaTime * 5f);
        lid.localRotation = Quaternion.Euler(-lidOpen * 75f, 0f, 0f);
        inside.gameObject.SetActive(lidOpen > 0.02f);
    }

    // Hit it, and it's not fooling anyone any more
    protected override void OnHit()
    {
        if (!awake) WakeUp();
    }

    void WakeUp()
    {
        awake = true;
        awayFor = 0f;
        hopTimer = 0.3f;
        Sounds.Monster(Called, transform.position);
        Achievements.Unlock("mimic");
        PortalTraveller.Announce("That's no block - it's a MIMIC!");
    }

    // Settles down on the nearest whole block, and looks like the ground there again
    void GoToSleep()
    {
        awake = false;
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y + 0.5f) - 0.5f, Mathf.Round(p.z));
        transform.rotation = Quaternion.identity;
        PickDisguise();
    }

    // Whatever the ground under it is made of
    void PickDisguise()
    {
        Vector3 p = transform.position;
        byte under = world.GetBlock(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y - 0.5f), Mathf.RoundToInt(p.z));
        disguise = under == BlockWorld.Grass || under == BlockWorld.Sand || under == BlockWorld.Stone ||
                   under == BlockWorld.Snow || under == BlockWorld.Cobblestone ? under : BlockWorld.Dirt;

        // Grass is dirt with a green top
        Material sides = world.MaterialFor(disguise == BlockWorld.Grass ? BlockWorld.Dirt : disguise);
        Material top = world.MaterialFor(disguise);
        for (int i = 0; i < bodyLooks.Length; i++) bodyLooks[i].sharedMaterial = sides;
        lid.GetChild(1).GetComponent<Renderer>().sharedMaterial = top;
    }

    protected override void BuildBody()
    {
        if (teeth == null)
        {
            teeth = BlockWorld.MakeMaterial(new Color(0.96f, 0.94f, 0.86f));
            tongue = BlockWorld.MakeMaterial(new Color(0.85f, 0.25f, 0.35f));
            mouth = BlockWorld.MakeMaterial(new Color(0.25f, 0.03f, 0.06f));
            eyes = BlockWorld.MakeMaterial(new Color(1.00f, 0.90f, 0.20f));
            eyes.EnableKeyword("_EMISSION");
            eyes.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.1f));
        }

        // Exactly one block, lined up with the world's blocks
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), p.y, Mathf.Round(p.z));
        transform.rotation = Quaternion.identity;

        Transform model = Model(1f);

        // The bottom part of the block, with the mouth inside it
        body = AddPart(model, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 1f), null);
        inside = new GameObject("Inside").transform;
        inside.SetParent(model, false);
        AddPart(inside, new Vector3(0f, 0.601f, 0f), new Vector3(0.86f, 0.01f, 0.86f), mouth);
        AddPart(inside, new Vector3(0f, 0.63f, -0.05f), new Vector3(0.25f, 0.05f, 0.6f), tongue);
        for (int i = 0; i < 5; i++)   // bottom teeth along the front
            AddPart(inside, new Vector3(-0.36f + i * 0.18f, 0.66f, 0.42f), new Vector3(0.08f, 0.12f, 0.06f), teeth);

        // The lid: the top part of the block, hinged at the back
        lid = Pivot(model, new Vector3(0f, 0.6f, -0.5f));
        AddPart(lid, new Vector3(0f, 0.2f, 0.5f), new Vector3(1f, 0.4f, 1f), null);           // (child 0 - the side of the lid)
        AddPart(lid, new Vector3(0f, 0.401f, 0.5f), new Vector3(1f, 0.01f, 1f), null);        // (child 1 - its top)
        for (int i = 0; i < 5; i++)   // top teeth, hanging down
            AddPart(lid, new Vector3(-0.36f + i * 0.18f, -0.05f, 0.92f), new Vector3(0.08f, 0.12f, 0.06f), teeth);
        // Glowing eyes on the underneath of the lid - you only see them once it opens
        AddPart(lid, new Vector3(-0.2f, -0.006f, 0.72f), new Vector3(0.16f, 0.01f, 0.1f), eyes);
        AddPart(lid, new Vector3(0.2f, -0.006f, 0.72f), new Vector3(0.16f, 0.01f, 0.1f), eyes);

        bodyLooks = new[] { body.GetComponent<Renderer>(), lid.GetChild(0).GetComponent<Renderer>() };
        PickDisguise();
        inside.gameObject.SetActive(false);
    }

    protected override void Animate(bool walking)
    {
        // A wobble as it hops
        float wobble = awake && !OnGround ? Mathf.Sin(Time.time * 18f) * 6f : 0f;
        transform.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, wobble);
    }

    // Whatever it swallowed
    protected override void DropLoot()
    {
        Drop(disguise == BlockWorld.Grass ? BlockWorld.Dirt : disguise, Random.Range(2, 5));
        if (Random.value < 0.5f) Drop(BlockWorld.Gold, Random.Range(1, 3));
        if (Random.value < 0.15f) Drop(BlockWorld.Diamond, 1);
        Wallet.Earn(3, "The mimic had coins inside!");
    }
}
