using System.Collections;
using UnityEngine;

// Unicorns live in the Magic Land. They're white, with a rainbow mane and
// tail and a golden horn, and they trot about the islands leaving sparkles
// behind them. Now and then one rears up, or prances about.
//
// You can catch one with the leash. They're magic, so swords and TNT can't
// hurt them - a sword just makes them gallop off. Like the other animals they
// have no collider, and they're too sensible to walk off the edge of an island.
//
// Unicorns can go through portals too: they hop through linked portals if
// they step on one, and now and then one trots through a magic portal to
// visit the other place. Ride one (or lead one on the leash) through a magic
// portal and it comes with you.
public class Unicorn : MonoBehaviour, ILeashable, IRideable
{
    const int HowMany = 8;
    const float WalkSpeed = 1.8f;
    const float GallopSpeed = 5f;

    static readonly Color[] Mane =
    {
        new Color(1.00f, 0.25f, 0.25f), new Color(1.00f, 0.60f, 0.15f), new Color(1.00f, 0.92f, 0.25f),
        new Color(0.35f, 0.85f, 0.35f), new Color(0.30f, 0.60f, 1.00f), new Color(0.70f, 0.35f, 1.00f)
    };

    static Material white, hoof, gold, eye;
    static Material[] maneColours;

    private BlockWorld world;
    private Quaternion facing;
    private Transform model, headPivot, tail;
    private Transform[] legs = new Transform[4];
    private float fallSpeed;
    private float walkTime;
    private bool walking, galloping;
    private float rearUp;          // how far up on its back legs it is, in degrees
    private Transform holder;      // whoever has us on a leash
    private bool ridden;           // someone's sitting on our back
    private bool justHatched;      // rears up to say hello first
    private bool flyingWithRider;

    // --- Making unicorns ---

    const int MostHatched = 20;   // how many extra unicorns you can hatch in each place
    const int MostInOnePlace = 28; // unicorns won't wander off to a place that already has this many
    const float WalkToPortalRange = 25f;

    static bool InMagicLand { get { return WorldMenu.CurrentPlace == WorldSaves.MagicLand; } }

    // Unicorns aren't saved with the world (no animals are), so for each
    // world and place we just remember how many you've hatched from eggs
    // (Unicorns that travel through a magic portal count too: one less here, one more there.)
    static string HatchedKey(bool magicLand)
    {
        return "world" + (WorldMenu.CurrentSlot + 1) + "-unicorns" + (magicLand ? "-magic" : "-home");
    }

    static string HatchedKey()
    {
        return HatchedKey(InMagicLand);
    }

    // How many have just come through the portal, so they appear next to it
    static string ArrivingKey(bool magicLand)
    {
        return "world" + (WorldMenu.CurrentSlot + 1) + "-unicorns-arriving" + (magicLand ? "-magic" : "-home");
    }

    // How many unicorns live in a place: the Magic Land's own herd (if it's
    // the Magic Land), plus or minus the ones that have hatched or travelled
    static int HowManyIn(bool magicLand)
    {
        return Mathf.Max(0, (magicLand ? HowMany : 0) + PlayerPrefs.GetInt(HatchedKey(magicLand), 0));
    }

    public static void SpawnAll(BlockWorld world)
    {
        if (WorldMenu.CurrentPlace == WorldSaves.Nether) return;   // no unicorns in the Nether

        int count = HowManyIn(InMagicLand);

        // Any that have just come through the portal appear right beside it
        int arriving = Mathf.Min(PlayerPrefs.GetInt(ArrivingKey(InMagicLand), 0), count);
        PlayerPrefs.SetInt(ArrivingKey(InMagicLand), 0);

        Vector3Int portal;
        bool hasPortal = Villager.FindNearestMagicPortal(world, world.FindSpawnPoint(), out portal);

        for (int i = 0; i < count; i++)
        {
            Vector3 spot;
            bool found = i < arriving && hasPortal ? FindGrassNear(world, portal, out spot) : FindGrass(world, out spot);
            if (!found && !FindGrass(world, out spot)) return;
            SpawnAt(world, spot);
        }
    }

    // Some grass within a few blocks of a spot
    static bool FindGrassNear(BlockWorld world, Vector3Int near, out Vector3 spot)
    {
        byte grass = InMagicLand ? BlockWorld.MagicGrass : BlockWorld.Grass;
        for (int tries = 0; tries < 60; tries++)
        {
            int x = near.x + Random.Range(-4, 5);
            int z = near.z + Random.Range(-4, 5);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == grass && world.GetBlock(x, y + 2, z) == BlockWorld.Air)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    static Unicorn SpawnAt(BlockWorld world, Vector3 spot)
    {
        GameObject obj = new GameObject("Unicorn");
        obj.transform.position = spot;
        obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Unicorn unicorn = obj.AddComponent<Unicorn>();
        unicorn.world = world;
        unicorn.BuildBody();
        return unicorn;
    }

    // Out of a unicorn egg, in a burst of sparkles - and remembered, so it's
    // still here next time
    public static void Hatch(BlockWorld world, Vector3 spot)
    {
        int hatched = PlayerPrefs.GetInt(HatchedKey(), 0);
        if (hatched < MostHatched)
        {
            PlayerPrefs.SetInt(HatchedKey(), hatched + 1);
            PlayerPrefs.Save();
        }

        Unicorn unicorn = SpawnAt(world, spot);
        unicorn.justHatched = true;
        Villager.SparkleBurst(spot + Vector3.up * 0.6f, 30);
    }

    // Somewhere on the grass - magic grass in the Magic Land
    static bool FindGrass(BlockWorld world, out Vector3 spot)
    {
        byte grass = InMagicLand ? BlockWorld.MagicGrass : BlockWorld.Grass;

        for (int tries = 0; tries < 300; tries++)
        {
            int x = Random.Range(0, BlockWorld.SizeX);
            int z = Random.Range(0, BlockWorld.SizeZ);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == grass && world.GetBlock(x, y + 2, z) == BlockWorld.Air)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    static Material Glowing(Color colour)
    {
        Material mat = BlockWorld.MakeMaterial(colour);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour * 0.5f);
        return mat;
    }

    // Built from cubes, facing along Z with its hooves at the origin
    void BuildBody()
    {
        if (white == null)
        {
            white = BlockWorld.MakeMaterial(new Color(0.97f, 0.96f, 1.00f));
            hoof = BlockWorld.MakeMaterial(new Color(0.75f, 0.65f, 0.85f));
            gold = Glowing(new Color(1.00f, 0.85f, 0.30f));
            eye = BlockWorld.MakeMaterial(new Color(0.15f, 0.05f, 0.30f));
            maneColours = new Material[Mane.Length];
            for (int i = 0; i < Mane.Length; i++) maneColours[i] = Glowing(Mane[i]);
        }

        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.32f, 0.75f), white);   // body

        // Four legs, each swinging from the hip
        Vector3[] hips = { new Vector3(-0.12f, 0.42f, 0.27f), new Vector3(0.12f, 0.42f, 0.27f),
                           new Vector3(-0.12f, 0.42f, -0.27f), new Vector3(0.12f, 0.42f, -0.27f) };
        for (int i = 0; i < 4; i++)
        {
            legs[i] = Pivot(model, hips[i]);
            AddPart(legs[i], new Vector3(0f, -0.19f, 0f), new Vector3(0.11f, 0.36f, 0.11f), white);
            AddPart(legs[i], new Vector3(0f, -0.39f, 0f), new Vector3(0.12f, 0.06f, 0.12f), hoof);
        }

        // Neck and head, tipped up, with the horn, ears, eyes and a rainbow mane
        headPivot = Pivot(model, new Vector3(0f, 0.68f, 0.32f));
        AddPart(headPivot, new Vector3(0f, 0.16f, 0.06f), new Vector3(0.18f, 0.34f, 0.18f), white);    // neck
        AddPart(headPivot, new Vector3(0f, 0.32f, 0.20f), new Vector3(0.20f, 0.20f, 0.34f), white);    // head
        AddPart(headPivot, new Vector3(0f, 0.50f, 0.26f), new Vector3(0.05f, 0.20f, 0.05f), gold);     // horn
        AddPart(headPivot, new Vector3(-0.07f, 0.45f, 0.10f), new Vector3(0.04f, 0.08f, 0.04f), white);   // ears
        AddPart(headPivot, new Vector3(0.07f, 0.45f, 0.10f), new Vector3(0.04f, 0.08f, 0.04f), white);
        AddPart(headPivot, new Vector3(-0.101f, 0.35f, 0.24f), new Vector3(0.01f, 0.05f, 0.05f), eye);
        AddPart(headPivot, new Vector3(0.101f, 0.35f, 0.24f), new Vector3(0.01f, 0.05f, 0.05f), eye);
        for (int i = 0; i < Mane.Length; i++)
            AddPart(headPivot, new Vector3(0f, 0.42f - i * 0.07f, -0.04f - i * 0.01f), new Vector3(0.06f, 0.07f, 0.08f), maneColours[i]);

        // A rainbow tail
        tail = Pivot(model, new Vector3(0f, 0.65f, -0.38f));
        for (int i = 0; i < Mane.Length; i++)
            AddPart(tail, new Vector3(0f, -i * 0.06f, -0.04f - i * 0.02f), new Vector3(0.07f, 0.07f, 0.06f), maneColours[i]);
    }

    Transform Pivot(Transform parent, Vector3 position)
    {
        Transform pivot = new GameObject("Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = position;
        return pivot;
    }

    void AddPart(Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    // --- Every frame ---

    void Start()
    {
        facing = transform.rotation;
        if (!ridden) StartCoroutine(Live());   // someone may have climbed on before we even got going
    }

    void Update()
    {
        // While ridden, the rider moves us (see Carry) - we just move our legs
        if (!ridden)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 300f * Time.deltaTime);
            if (holder != null) FollowHolder();
            StayOnGround();
            HopThroughLinkedPortal();
        }

        // Legs trot in pairs, faster at a gallop; the tail swishes
        if (walking) walkTime += Time.deltaTime * (galloping ? 2f : 1f);
        float swing = walking ? Mathf.Sin(walkTime * 10f) * 30f : 0f;
        if (flyingWithRider) swing = 40f;   // legs stretched out, flying like a horse leaping
        legs[0].localRotation = legs[3].localRotation = Quaternion.Euler(flyingWithRider ? -swing : swing, 0f, 0f);
        legs[1].localRotation = legs[2].localRotation = Quaternion.Euler(flyingWithRider ? swing : -swing, 0f, 0f);
        tail.localRotation = Quaternion.Euler(20f, Mathf.Sin(Time.time * 3f) * 20f, 0f);

        model.localRotation = Quaternion.Euler(-rearUp, 0f, 0f);

        // A trail of sparkles while it moves, and lots of them while flying
        if ((walking || flyingWithRider) && Random.value < (galloping || flyingWithRider ? 0.3f : 0.08f))
            Villager.SparkleBurst(transform.position + Vector3.up * 0.4f - transform.forward * 0.4f, 1);
    }

    // --- Things that happen to it ---

    // Hit with a sword: no harm done (it's magic!), but it gallops away
    public void Startle(Vector3 from)
    {
        if (holder != null || ridden) return;   // on the leash, or being ridden, it trusts you

        StopAllCoroutines();
        rearUp = 0f;
        Villager.SparkleBurst(transform.position + Vector3.up * 0.6f, 12);
        StartCoroutine(GallopAway(from));
    }

    // --- Portals ---

    public bool OnLeashOrRidden { get { return holder != null || ridden; } }

    private bool waitingToStepOff;   // just came out of a linked portal, so don't go straight back in

    // Stepped onto a linked portal? Out of the next one we pop
    void HopThroughLinkedPortal()
    {
        if (holder != null) return;

        Vector3Int under = Vector3Int.RoundToInt(transform.position + Vector3.down * 0.3f);
        if (world.GetBlock(under.x, under.y, under.z) != BlockWorld.Portal)
        {
            waitingToStepOff = false;
            return;
        }
        if (waitingToStepOff) return;
        waitingToStepOff = true;

        Vector3Int to;
        if (!PortalTraveller.FindNextPortal(world, under, out to)) return;

        Villager.SparkleBurst(transform.position + Vector3.up * 0.6f, 10);
        transform.position = new Vector3(to.x, to.y + 0.5f, to.z);
        fallSpeed = 0f;
    }

    // Vanishes through the magic portal, and is counted in the other place,
    // where it'll appear right by the portal
    public void GoThroughPortal()
    {
        bool from = InMagicLand, to = !InMagicLand;

        PlayerPrefs.SetInt(HatchedKey(from), PlayerPrefs.GetInt(HatchedKey(from), 0) - 1);
        PlayerPrefs.SetInt(HatchedKey(to), PlayerPrefs.GetInt(HatchedKey(to), 0) + 1);
        PlayerPrefs.SetInt(ArrivingKey(to), PlayerPrefs.GetInt(ArrivingKey(to), 0) + 1);
        PlayerPrefs.Save();

        Villager.SparkleBurst(transform.position + Vector3.up * 0.6f, 25);
        Destroy(gameObject);
    }

    // Now and then a unicorn trots off through a nearby magic portal by itself
    IEnumerator VisitOtherPlace()
    {
        Vector3Int portal;
        if (!Villager.FindNearestMagicPortal(world, transform.position, out portal)) yield break;
        if (HowManyIn(!InMagicLand) >= MostInOnePlace) yield break;   // crowded over there

        Vector3 onTop = new Vector3(portal.x, portal.y + 0.5f, portal.z);
        Vector3 flat = onTop - transform.position;
        flat.y = 0f;
        if (flat.magnitude > WalkToPortalRange) yield break;

        walking = true;
        for (float t = 0f; t < 20f; t += Time.deltaTime)
        {
            flat = onTop - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.3f) break;

            Vector3 direction = flat.normalized;
            facing = Quaternion.LookRotation(direction);
            Vector3 next = transform.position + direction * WalkSpeed * Time.deltaTime;
            if (!CanWalkTo(next)) break;   // something in the way - never mind

            transform.position = next;
            yield return null;
        }
        walking = false;

        flat = onTop - transform.position;
        flat.y = 0f;
        if (flat.magnitude < 0.8f && world.GetBlock(portal.x, portal.y, portal.z) == BlockWorld.MagicPortal)
        {
            PortalTraveller.Announce(InMagicLand ? "A unicorn went through the magic portal to your home world!"
                                                 : "A unicorn went through the magic portal to the Magic Land!");
            GoThroughPortal();
        }
    }

    // --- Being ridden ---

    public float SpeedBoost { get { return 1.6f; } }
    public float SeatHeight { get { return 0.6f; } }
    public float GlideSpeed { get { return 2.5f; } }

    public void StartRiding()
    {
        StopAllCoroutines();
        holder = null;
        ridden = true;
        rearUp = 0f;
        headPivot.localRotation = Quaternion.identity;
        Villager.SparkleBurst(transform.position + Vector3.up * 0.8f, 15);
    }

    public void StopRiding()
    {
        ridden = false;
        flyingWithRider = false;
        walking = galloping = false;
        fallSpeed = 0f;
        facing = transform.rotation;
        StartCoroutine(Live());
    }

    // The rider moves; we go with them, underneath, facing their way
    public void Carry(Vector3 riderFeet, float yaw, float speed, bool inTheAir)
    {
        transform.position = riderFeet;
        transform.rotation = facing = Quaternion.Euler(0f, yaw, 0f);
        flyingWithRider = inTheAir;
        walking = speed > 0.1f && !inTheAir;
        galloping = speed > 4f;
    }

    public void Catch(Transform newHolder)
    {
        if (ridden) return;   // already with you

        StopAllCoroutines();
        rearUp = 0f;
        holder = newHolder;
        Villager.SparkleBurst(transform.position + Vector3.up * 0.6f, 8);
    }

    public void Release()
    {
        holder = null;
        walking = galloping = false;
        StartCoroutine(Live());
    }

    void FollowHolder()
    {
        Vector3 pos = transform.position;
        Vector3 flat = holder.position - pos;
        flat.y = 0f;
        float distance = flat.magnitude;

        walking = galloping = false;
        if (distance <= 2.2f) return;

        Vector3 direction = flat / distance;
        facing = Quaternion.LookRotation(direction);
        Vector3 next = pos + direction * Mathf.Min(distance * 1.5f, 8f) * Time.deltaTime;

        if (CanWalkTo(next))
        {
            transform.position = next;
            walking = true;
            galloping = distance > 4f;
        }
        else if (distance > 10f)
        {
            transform.position = holder.position;   // left far behind - the leash yanks it along
        }
    }

    // --- Staying on the island ---

    void StayOnGround()
    {
        Vector3 pos = transform.position;
        int ground = GroundBelow(Mathf.RoundToInt(pos.x), pos.y, Mathf.RoundToInt(pos.z));
        float floor = ground >= 0 ? ground + 0.5f : -100f;

        if (pos.y > floor)
        {
            fallSpeed += 12f * Time.deltaTime;
            pos.y = Mathf.Max(pos.y - fallSpeed * Time.deltaTime, floor);
        }
        else
        {
            fallSpeed = 0f;
            pos.y = Mathf.MoveTowards(pos.y, floor, 5f * Time.deltaTime);   // stepping up onto a block
        }
        transform.position = pos;

        // Fell off the bottom of the sky, or into water? A little magic puts it back on the grass.
        int feet = Mathf.FloorToInt(pos.y + 0.5f);
        Vector3 safe;
        if ((pos.y < -10f || world.GetBlock(Mathf.RoundToInt(pos.x), feet, Mathf.RoundToInt(pos.z)) == BlockWorld.Water)
            && FindGrass(world, out safe))
        {
            transform.position = safe;
            fallSpeed = 0f;
        }
    }

    bool CanWalkTo(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x);
        int z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);

        if (ground < 0) return false;                                            // the edge of the island
        if (world.GetBlock(x, ground + 1, z) != BlockWorld.Air) return false;    // water, or a block in the way
        if (world.GetBlock(x, ground + 2, z) != BlockWorld.Air) return false;    // too low for a unicorn
        if (pos.y - (ground + 0.5f) > 1.5f) return false;                        // too big a drop

        return true;
    }

    int GroundBelow(int x, float y, int z)
    {
        for (int cell = Mathf.FloorToInt(y + 0.5f); cell >= 0; cell--)
        {
            byte type = world.GetBlock(x, cell, z);
            if (type != BlockWorld.Air && type != BlockWorld.Water)
                return cell;
        }
        return -1;
    }

    // --- What it gets up to ---

    IEnumerator Live()
    {
        if (justHatched)
        {
            justHatched = false;
            yield return RearUp();
        }

        yield return new WaitForSeconds(Random.Range(0f, 2f));

        while (true)
        {
            float roll = Random.value;
            if (roll < 0.02f)
                yield return VisitOtherPlace();
            else if (roll < 0.15f)
                yield return RearUp();
            else if (roll < 0.30f)
                yield return Prance();
            else if (roll < 0.50f)
                yield return Graze();
            else
                yield return Trot(Random.Range(0f, 360f), WalkSpeed, Random.Range(3f, 8f));
        }
    }

    IEnumerator Trot(float angle, float speed, float distance)
    {
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        facing = Quaternion.LookRotation(direction);
        yield return new WaitForSeconds(0.3f);

        walking = true;
        galloping = speed > WalkSpeed;

        while (distance > 0f)
        {
            float step = speed * Time.deltaTime;
            Vector3 next = transform.position + direction * step;
            if (!CanWalkTo(next)) break;   // edge of the island - stop

            transform.position = next;
            distance -= step;
            yield return null;
        }

        walking = galloping = false;
        yield return new WaitForSeconds(Random.Range(0.3f, 1.5f));
    }

    // Up on its back legs, with a burst of sparkles from the horn
    IEnumerator RearUp()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            rearUp = Mathf.Lerp(0f, 35f, t);
            yield return null;
        }

        Villager.SparkleBurst(headPivot.position + Vector3.up * 0.6f + transform.forward * 0.3f, 15);
        yield return new WaitForSeconds(0.8f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            rearUp = Mathf.Lerp(35f, 0f, t);
            yield return null;
        }
        rearUp = 0f;
        yield return new WaitForSeconds(0.5f);
    }

    // A few happy little hops in a circle
    IEnumerator Prance()
    {
        float angle = transform.eulerAngles.y;
        for (int hop = 0; hop < 4; hop++)
        {
            angle += 90f;
            facing = Quaternion.Euler(0f, angle, 0f);
            fallSpeed = -4f;   // a little spring upwards
            transform.position += Vector3.up * 0.05f;
            Villager.SparkleBurst(transform.position + Vector3.up * 0.3f, 3);
            yield return new WaitForSeconds(0.45f);
        }
    }

    // Head down, nibbling the magic grass
    IEnumerator Graze()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            headPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 60f, t), 0f, 0f);
            yield return null;
        }
        yield return new WaitForSeconds(Random.Range(1f, 3f));
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            headPivot.localRotation = Quaternion.Euler(Mathf.Lerp(60f, 0f, t), 0f, 0f);
            yield return null;
        }
        headPivot.localRotation = Quaternion.identity;
    }

    IEnumerator GallopAway(Vector3 from)
    {
        Vector3 away = transform.position - from;
        float angle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
        yield return Trot(angle, GallopSpeed, 8f);
        StartCoroutine(Live());
    }
}
