using System.Collections;
using UnityEngine;

// Dragons live in your home world - and lots of them in the Nether. They soar about high in the sky, land
// now and then to rest or stroll about, and breathe fire into the air.
//
// Press E on one to climb on and ride it: hold Space to fly up, let go to
// glide, and left click to breathe fire. Dragon fire burns leaves and ash,
// melts ice, lights TNT and makes fire vents flare up - but nothing else,
// so your builds are safe. You can leash them too, and
// swords and TNT can't hurt them (a sword just makes them fly off).
//
// Like the other animals, dragons aren't saved with the world: home always
// has its own few, and the game remembers how many you've hatched from eggs.
public class Dragon : MonoBehaviour, ILeashable, IRideable
{
    const int HowManyAtHome = 3;
    const int HowManyInNether = 8;
    const int MostHatched = 10;
    const float FlySpeed = 6f;
    const float WalkSpeed = 1.5f;
    const float FireReach = 18f;   // how many blocks the fire reaches

    static readonly Color[] Scales =
    {
        new Color(0.75f, 0.15f, 0.12f), new Color(0.20f, 0.55f, 0.25f), new Color(0.45f, 0.20f, 0.65f),
        new Color(0.15f, 0.35f, 0.70f), new Color(0.85f, 0.65f, 0.15f)
    };

    static Material dark, horn, eyeGlow;
    static Material[] fireColours;

    private BlockWorld world;
    private Vector3 home;
    private Quaternion facing;
    private Transform model, neck, head, leftWing, rightWing;
    private Transform[] tail = new Transform[3];
    private Transform[] legs = new Transform[4];

    private bool flying;            // up in the air (on its own, or with you on its back)
    private bool walking;
    private float fallSpeed;
    private float lastFire = -100f;
    private Transform holder;       // whoever has us on a leash
    private bool ridden;

    // --- Making dragons ---

    static bool InMagicLand { get { return WorldMenu.CurrentPlace == WorldSaves.MagicLand; } }

    static string HatchedKey()
    {
        return "world" + (WorldMenu.CurrentSlot + 1) + "-dragons-" + WorldSaves.PlaceName(WorldMenu.CurrentPlace);
    }

    // Home's own few dragons, plus any you've hatched (in the Magic Land, just
    // the ones you've hatched there)
    public static void SpawnAll(BlockWorld world)
    {
        int place = WorldMenu.CurrentPlace;
        int count = (place == WorldSaves.Home ? HowManyAtHome : place == WorldSaves.Nether ? HowManyInNether : 0)
                  + PlayerPrefs.GetInt(HatchedKey(), 0);
        for (int i = 0; i < count; i++)
        {
            Vector3 spot;
            if (!FindLandingSpot(world, new Vector3(BlockWorld.SizeX / 2f, 0f, BlockWorld.SizeZ / 2f), BlockWorld.SizeX, out spot)) return;
            SpawnAt(world, spot);
        }
    }

    static Dragon SpawnAt(BlockWorld world, Vector3 spot)
    {
        GameObject obj = new GameObject("Dragon");
        obj.transform.position = spot;
        obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Dragon dragon = obj.AddComponent<Dragon>();
        dragon.world = world;
        dragon.home = spot;
        dragon.BuildBody();
        return dragon;
    }

    // Out of a dragon egg - with a puff of fire to say hello
    public static void Hatch(BlockWorld world, Vector3 spot)
    {
        int hatched = PlayerPrefs.GetInt(HatchedKey(), 0);
        if (hatched < MostHatched)
        {
            PlayerPrefs.SetInt(HatchedKey(), hatched + 1);
            PlayerPrefs.Save();
        }

        SpawnAt(world, spot);
        Villager.SparkleBurst(spot + Vector3.up * 0.8f, 20);
        FireBurst(spot + Vector3.up * 1.5f, Vector3.up, 20);
    }

    // Somewhere solid and dry to land: grass, sand, stone or snow, with the
    // sky open above it. (Magic grass too, in the Magic Land.)
    static bool FindLandingSpot(BlockWorld world, Vector3 near, int range, out Vector3 spot)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            int x = Mathf.RoundToInt(near.x) + Random.Range(-range, range + 1);
            int z = Mathf.RoundToInt(near.z) + Random.Range(-range, range + 1);
            if (x < 2 || z < 2 || x >= BlockWorld.SizeX - 2 || z >= BlockWorld.SizeZ - 2) continue;

            int y = world.GetSurfaceHeight(x, z);
            if (y < 0) continue;

            byte ground = world.GetBlock(x, y, z);
            if (ground == BlockWorld.Grass || ground == BlockWorld.Sand || ground == BlockWorld.Stone ||
                ground == BlockWorld.Snow || ground == BlockWorld.MagicGrass || ground == BlockWorld.Ash ||
                ground == BlockWorld.EmberRock)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    // --- Its body ---

    static Material Glowing(Color colour)
    {
        Material mat = BlockWorld.MakeMaterial(colour);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour);
        return mat;
    }

    // Built from cubes, facing along Z with its feet at the origin
    void BuildBody()
    {
        if (dark == null)
        {
            dark = BlockWorld.MakeMaterial(new Color(0.12f, 0.10f, 0.10f));
            horn = BlockWorld.MakeMaterial(new Color(0.90f, 0.85f, 0.70f));
            eyeGlow = Glowing(new Color(1.00f, 0.85f, 0.10f));
        }

        Color colour = Scales[Random.Range(0, Scales.Length)];
        Material scales = BlockWorld.MakeMaterial(colour);
        Material belly = BlockWorld.MakeMaterial(Color.Lerp(colour, new Color(1f, 0.9f, 0.6f), 0.55f));
        Material wingSkin = BlockWorld.MakeMaterial(colour * 0.7f);

        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.75f, 0f), new Vector3(0.80f, 0.60f, 1.40f), scales);   // body
        AddPart(model, new Vector3(0f, 0.47f, 0.05f), new Vector3(0.62f, 0.10f, 1.10f), belly);
        for (int i = 0; i < 4; i++)
            AddPart(model, new Vector3(0f, 1.10f, 0.45f - i * 0.32f), new Vector3(0.08f, 0.16f, 0.12f), dark);   // spikes

        // Four short, strong legs
        Vector3[] hips = { new Vector3(-0.30f, 0.50f, 0.45f), new Vector3(0.30f, 0.50f, 0.45f),
                           new Vector3(-0.30f, 0.50f, -0.45f), new Vector3(0.30f, 0.50f, -0.45f) };
        for (int i = 0; i < 4; i++)
        {
            legs[i] = Pivot(model, hips[i]);
            AddPart(legs[i], new Vector3(0f, -0.22f, 0f), new Vector3(0.18f, 0.44f, 0.20f), scales);
            AddPart(legs[i], new Vector3(0f, -0.47f, 0.05f), new Vector3(0.20f, 0.06f, 0.26f), dark);   // claws
        }

        // A long neck, and a head with horns, glowing eyes and a big snout
        neck = Pivot(model, new Vector3(0f, 0.90f, 0.65f));
        AddPart(neck, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.32f, 0.32f, 0.45f), scales);
        head = Pivot(neck, new Vector3(0f, 0.35f, 0.42f));
        AddPart(head, new Vector3(0f, 0f, 0.15f), new Vector3(0.42f, 0.32f, 0.50f), scales);
        AddPart(head, new Vector3(0f, -0.06f, 0.48f), new Vector3(0.30f, 0.20f, 0.30f), scales);    // snout
        AddPart(head, new Vector3(-0.13f, 0.22f, -0.05f), new Vector3(0.06f, 0.22f, 0.06f), horn);
        AddPart(head, new Vector3(0.13f, 0.22f, -0.05f), new Vector3(0.06f, 0.22f, 0.06f), horn);
        AddPart(head, new Vector3(-0.211f, 0.06f, 0.22f), new Vector3(0.01f, 0.07f, 0.09f), eyeGlow);
        AddPart(head, new Vector3(0.211f, 0.06f, 0.22f), new Vector3(0.01f, 0.07f, 0.09f), eyeGlow);

        // A tail in three pieces, each smaller than the last, so it can swish
        tail[0] = Pivot(model, new Vector3(0f, 0.75f, -0.70f));
        AddPart(tail[0], new Vector3(0f, 0f, -0.25f), new Vector3(0.36f, 0.30f, 0.50f), scales);
        tail[1] = Pivot(tail[0], new Vector3(0f, -0.02f, -0.50f));
        AddPart(tail[1], new Vector3(0f, 0f, -0.20f), new Vector3(0.25f, 0.22f, 0.42f), scales);
        tail[2] = Pivot(tail[1], new Vector3(0f, -0.02f, -0.40f));
        AddPart(tail[2], new Vector3(0f, 0f, -0.18f), new Vector3(0.16f, 0.15f, 0.36f), scales);
        AddPart(tail[2], new Vector3(0f, 0f, -0.40f), new Vector3(0.30f, 0.06f, 0.16f), dark);   // a spade on the end

        // Two big wings
        leftWing = MakeWing(-1f, wingSkin);
        rightWing = MakeWing(1f, wingSkin);
    }

    Transform MakeWing(float side, Material skin)
    {
        Transform wing = Pivot(model, new Vector3(side * 0.38f, 1.00f, 0.15f));
        AddPart(wing, new Vector3(side * 0.65f, 0f, 0f), new Vector3(1.30f, 0.07f, 0.10f), dark);          // the bone along the top
        AddPart(wing, new Vector3(side * 0.60f, -0.01f, -0.32f), new Vector3(1.20f, 0.03f, 0.60f), skin);  // the skin underneath
        AddPart(wing, new Vector3(side * 1.25f, 0f, -0.05f), new Vector3(0.10f, 0.10f, 0.10f), horn);     // a claw at the tip
        return wing;
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
        if (!ridden) StartCoroutine(Live());
    }

    void Update()
    {
        if (!ridden)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 120f * Time.deltaTime);
            if (holder != null) FollowHolder();
            else if (!flying) StayOnGround();
        }

        Animate();
    }

    void Animate()
    {
        float t = Time.time;

        // Big slow flaps in the air; folded down along its sides on the ground
        if (flying)
        {
            float flap = Mathf.Sin(t * 5f) * 35f;
            leftWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, flap);
        }
        else
        {
            leftWing.localRotation = Quaternion.Euler(0f, 0f, 60f);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, -60f);
        }

        // Legs walk on the ground, and tuck back in the air
        float swing = walking ? Mathf.Sin(t * 8f) * 25f : 0f;
        float tuck = flying ? 50f : 0f;
        legs[0].localRotation = legs[3].localRotation = Quaternion.Euler(swing + tuck, 0f, 0f);
        legs[1].localRotation = legs[2].localRotation = Quaternion.Euler(-swing + tuck, 0f, 0f);

        // The tail swishes, each piece a little behind the one before
        for (int i = 0; i < tail.Length; i++)
            tail[i].localRotation = Quaternion.Euler(flying ? -5f : 10f, Mathf.Sin(t * 2f - i * 0.8f) * 15f, 0f);
    }

    // --- Walking and flying ---

    void StayOnGround()
    {
        Vector3 pos = transform.position;
        int x = Mathf.RoundToInt(pos.x), z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);
        float floor = ground >= 0 ? ground + 0.5f : -100f;

        if (pos.y > floor)
        {
            fallSpeed += 12f * Time.deltaTime;
            pos.y = Mathf.Max(pos.y - fallSpeed * Time.deltaTime, floor);
        }
        else
        {
            fallSpeed = 0f;
            pos.y = Mathf.MoveTowards(pos.y, floor, 5f * Time.deltaTime);
        }
        transform.position = pos;

        // Nothing below at all, or standing in water? Up and away!
        byte feet = world.GetBlock(x, Mathf.FloorToInt(pos.y + 0.5f), z);
        if (pos.y < -10f || feet == BlockWorld.Water || feet == BlockWorld.Lava)
        {
            transform.position = new Vector3(pos.x, Mathf.Max(pos.y, 0f) + 2f, pos.z);
            flying = true;
        }
    }

    int GroundBelow(int x, float y, int z)
    {
        for (int cell = Mathf.FloorToInt(y + 0.5f); cell >= 0; cell--)
        {
            byte type = world.GetBlock(x, cell, z);
            if (type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Lava)
                return cell;
        }
        return -1;
    }

    bool CanWalkTo(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x);
        int z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);

        if (ground < 0) return false;
        for (int up = 1; up <= 2; up++)
            if (world.GetBlock(x, ground + up, z) != BlockWorld.Air) return false;   // something in the way
        if (pos.y - (ground + 0.5f) > 1.5f) return false;                            // too big a drop

        return true;
    }

    // Glides in a straight line to a spot, turning to face the way it's going
    IEnumerator FlyTo(Vector3 to, float speed)
    {
        flying = true;
        while ((transform.position - to).sqrMagnitude > 0.04f)
        {
            Vector3 flat = to - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.25f) facing = Quaternion.LookRotation(flat);

            transform.position = Vector3.MoveTowards(transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }

    // High above everything in the world - even the tallest trees on the mountains
    float CruiseHeight()
    {
        return BlockWorld.SizeY + Random.Range(0f, 8f);
    }

    // --- What it gets up to ---

    IEnumerator Live()
    {
        if (flying) yield return Land();   // let go of a leash in mid-air? Come down first
        yield return new WaitForSeconds(Random.Range(0.5f, 3f));

        while (true)
        {
            float roll = Random.value;
            if (roll < 0.40f)
                yield return FlyAround();
            else if (roll < 0.70f)
                yield return Rest();
            else if (roll < 0.85f)
                yield return RoarFire();
            else
                yield return Stroll();
        }
    }

    // Up into the sky, a few big loops around, then back down near home
    IEnumerator FlyAround()
    {
        float height = CruiseHeight();
        yield return FlyTo(new Vector3(transform.position.x, height, transform.position.z), FlySpeed * 0.6f);

        int loops = Random.Range(3, 6);
        for (int i = 0; i < loops; i++)
        {
            Vector3 next = new Vector3(Random.Range(8f, BlockWorld.SizeX - 8f), height + Random.Range(-3f, 3f),
                                       Random.Range(8f, BlockWorld.SizeZ - 8f));
            yield return FlyTo(next, FlySpeed);

            if (Random.value < 0.3f) yield return RoarFire();
        }

        yield return Land();
    }

    IEnumerator Land()
    {
        Vector3 spot;
        if (!FindLandingSpot(world, home, 20, out spot) && !FindLandingSpot(world, home, BlockWorld.SizeX, out spot))
            yield break;

        yield return FlyTo(new Vector3(spot.x, Mathf.Max(transform.position.y, CruiseHeight() - 4f), spot.z), FlySpeed);
        yield return FlyTo(spot, FlySpeed * 0.5f);   // gently down
        flying = false;
        fallSpeed = 0f;
    }

    IEnumerator Rest()
    {
        float seconds = Random.Range(3f, 6f);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            neck.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.8f) * 30f, 0f);   // looking around
            yield return null;
        }
        neck.localRotation = Quaternion.identity;
    }

    // Head up, and a great big gout of fire into the sky
    IEnumerator RoarFire()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            neck.localRotation = Quaternion.Euler(-30f * t, 0f, 0f);
            yield return null;
        }

        Vector3 up = (transform.forward + Vector3.up * 1.2f).normalized;
        for (int puff = 0; puff < 6; puff++)
        {
            FireBurst(Mouth(), up, 6);
            yield return new WaitForSeconds(0.08f);
        }

        yield return new WaitForSeconds(0.6f);
        neck.localRotation = Quaternion.identity;
    }

    IEnumerator Stroll()
    {
        Vector3 direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        facing = Quaternion.LookRotation(direction);
        yield return new WaitForSeconds(0.5f);

        walking = true;
        for (float distance = Random.Range(2f, 5f); distance > 0f; distance -= WalkSpeed * Time.deltaTime)
        {
            Vector3 next = transform.position + direction * WalkSpeed * Time.deltaTime;
            if (!CanWalkTo(next)) break;
            transform.position = next;
            yield return null;
        }
        walking = false;
    }

    // --- Fire ---

    Vector3 Mouth()
    {
        return head.TransformPoint(new Vector3(0f, -0.06f, 0.68f));
    }

    // Fire, from the rider's left click. It burns leaves and the Nether's ash,
    // melts ice, lights TNT, makes fire vents flare up, and stops at anything else.
    public void BreatheFire(Vector3 direction)
    {
        if (Time.time - lastFire < 0.35f) return;   // catching its breath
        lastFire = Time.time;

        FireBurst(Mouth(), direction, 35);

        Vector3 start = Mouth();
        Vector3Int lastEmpty = new Vector3Int(-1, -1, -1);
        for (float d = 0.5f; d < FireReach; d += 0.25f)
        {
            Vector3Int cell = Vector3Int.RoundToInt(start + direction * d);
            byte type = world.GetBlock(cell.x, cell.y, cell.z);
            if (type == BlockWorld.Air) lastEmpty = cell;

            if (type == BlockWorld.Leaves || type == BlockWorld.Ash)
                world.SetBlock(cell.x, cell.y, cell.z, BlockWorld.Air);     // burnt away
            else if (type == BlockWorld.Ice)
                world.SetBlock(cell.x, cell.y, cell.z, BlockWorld.Water);   // melted
            else if (Tnt.IsExplosive(type))
            {
                Tnt.Ignite(world, cell, type == BlockWorld.MegaTnt ? Tnt.MegaFuseSeconds : Tnt.FuseSeconds);
                break;
            }
            else if (type == BlockWorld.FireVent)
            {
                FireBurst(new Vector3(cell.x, cell.y + 0.5f, cell.z), Vector3.up, 25, 9f, 1f);   // whoosh!
                break;
            }
            else if (type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Lava && type != BlockWorld.Fire)
            {
                // In the Nether, where the fire lands it sets the ground alight
                if (WorldMenu.CurrentPlace == WorldSaves.Nether && lastEmpty.y >= 0 &&
                    world.GetBlock(lastEmpty.x, lastEmpty.y - 1, lastEmpty.z) != BlockWorld.Air)
                    world.SetBlock(lastEmpty.x, lastEmpty.y, lastEmpty.z, BlockWorld.Fire);
                break;   // fire doesn't go through walls (but it goes through water and lava)
            }
        }
    }

    // Little glowing flames that shoot out and fade. They move by themselves
    // (with physics, but no gravity), so they keep going wherever the dragon goes.
    // ("speed" and "life" are for gentler flames, like the ones from fire vents)
    public static void FireBurst(Vector3 from, Vector3 direction, int count, float speed = 23f, float life = 0.72f)
    {
        if (fireColours == null || fireColours[0] == null)
            fireColours = new[]
            {
                Glowing(new Color(1.00f, 0.25f, 0.05f)), Glowing(new Color(1.00f, 0.55f, 0.10f)),
                Glowing(new Color(1.00f, 0.85f, 0.25f))
            };

        for (int i = 0; i < count; i++)
        {
            GameObject flame = new GameObject("Flame");
            flame.transform.position = from + Random.insideUnitSphere * 0.1f;
            flame.transform.localScale = Vector3.one * Random.Range(0.10f, 0.24f);
            flame.transform.rotation = Random.rotation;
            flame.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            flame.AddComponent<MeshRenderer>().sharedMaterial = fireColours[Random.Range(0, fireColours.Length)];

            Rigidbody drift = flame.AddComponent<Rigidbody>();
            drift.useGravity = false;
            drift.linearVelocity = direction * speed * Random.Range(0.87f, 1.13f) + Random.insideUnitSphere * 2f;   // fast enough to reach FireReach
            drift.angularVelocity = Random.insideUnitSphere * 8f;

            Destroy(flame, life * Random.Range(0.85f, 1.15f));
        }
    }

    // --- Things that happen to it ---

    // Hit with a sword: no harm done, but it takes off and flies away
    public void Startle(Vector3 from)
    {
        if (holder != null || ridden) return;

        StopAllCoroutines();
        walking = false;
        neck.localRotation = Quaternion.identity;
        FireBurst(Mouth(), (transform.forward + Vector3.up).normalized, 15);
        StartCoroutine(FlyOff(from));
    }

    IEnumerator FlyOff(Vector3 from)
    {
        Vector3 away = transform.position - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;

        Vector3 to = transform.position + away * 20f;
        to.x = Mathf.Clamp(to.x, 8f, BlockWorld.SizeX - 8f);
        to.z = Mathf.Clamp(to.z, 8f, BlockWorld.SizeZ - 8f);
        to.y = CruiseHeight();

        yield return FlyTo(to, FlySpeed * 1.3f);
        StartCoroutine(Live());
    }

    public void Catch(Transform newHolder)
    {
        if (ridden) return;
        StopAllCoroutines();
        walking = false;
        holder = newHolder;
    }

    public void Release()
    {
        holder = null;
        StartCoroutine(Live());
    }

    // On the leash it flies along behind you, a little above your head
    void FollowHolder()
    {
        flying = true;
        Vector3 spot = holder.position - holder.forward * 3f + Vector3.up * 1.5f;
        Vector3 toSpot = spot - transform.position;
        if (toSpot.sqrMagnitude < 0.25f) return;

        Vector3 flat = toSpot;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.25f) facing = Quaternion.LookRotation(flat);

        float speed = Mathf.Min(toSpot.magnitude * 1.5f, 12f);
        transform.position = Vector3.MoveTowards(transform.position, spot, speed * Time.deltaTime);
    }

    // --- Being ridden ---

    public float SpeedBoost { get { return 2.2f; } }
    public float SeatHeight { get { return 1.1f; } }
    public float GlideSpeed { get { return 1.5f; } }

    public void StartRiding()
    {
        StopAllCoroutines();
        holder = null;
        ridden = true;
        walking = false;
        neck.localRotation = Quaternion.identity;
    }

    public void StopRiding()
    {
        ridden = false;
        facing = transform.rotation;
        StartCoroutine(Live());   // it lands by itself if you hop off in the air
    }

    public void Carry(Vector3 riderFeet, float yaw, float speed, bool inTheAir)
    {
        transform.position = riderFeet;
        transform.rotation = facing = Quaternion.Euler(0f, yaw, 0f);
        flying = inTheAir;
        walking = speed > 0.1f && !inTheAir;
    }
}
