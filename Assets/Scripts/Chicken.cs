using System.Collections;
using UnityEngine;

// A chicken that wanders around pecking at the ground. Like squirrels it has
// no collider, so it won't get in the way of you or your blocks. TNT is the
// only thing that can hurt it.
public class Chicken : MonoBehaviour, ILeashable
{
    const float WalkSpeed = 1.5f;

    private BlockWorld world;
    private Quaternion facing;
    private Transform model;
    private Transform headPivot;
    private Material feathers;
    private float fallSpeed;
    private float walkTime;
    private bool walking;
    private Transform holder;   // whoever has us on a leash

    // --- Making chickens ---

    // Puts one new chicken on a random patch of grass, if there's any left
    public static void Spawn(BlockWorld world, Material white, Material red, Material orange, Material black)
    {
        Vector3 spot;
        if (!FindGrass(world, out spot)) return;

        GameObject obj = new GameObject("Chicken");
        obj.transform.position = spot;

        Chicken chicken = obj.AddComponent<Chicken>();
        chicken.world = world;
        chicken.feathers = white;
        chicken.BuildBody(white, red, orange, black);
    }

    static bool FindGrass(BlockWorld world, out Vector3 spot)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            int x = Random.Range(0, BlockWorld.SizeX);
            int z = Random.Range(0, BlockWorld.SizeZ);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == BlockWorld.Grass)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    // Built from cubes, facing along Z with its feet at the origin
    void BuildBody(Material white, Material red, Material orange, Material black)
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.30f, 0f), new Vector3(0.28f, 0.26f, 0.38f), white);       // body
        AddPart(model, new Vector3(0f, 0.38f, -0.22f), new Vector3(0.14f, 0.20f, 0.08f), white);   // tail

        for (int side = -1; side <= 1; side += 2)
        {
            AddPart(model, new Vector3(side * 0.16f, 0.32f, 0f), new Vector3(0.04f, 0.16f, 0.24f), white);   // wing
            AddPart(model, new Vector3(side * 0.07f, 0.09f, 0f), new Vector3(0.04f, 0.18f, 0.04f), orange);  // leg
            AddPart(model, new Vector3(side * 0.07f, 0.01f, 0.03f), new Vector3(0.07f, 0.02f, 0.11f), orange); // foot
        }

        // The head hangs off a pivot at the neck so it can bend down to peck
        headPivot = new GameObject("Head").transform;
        headPivot.SetParent(model, false);
        headPivot.localPosition = new Vector3(0f, 0.38f, 0.16f);

        AddPart(headPivot, new Vector3(0f, 0.14f, 0.03f), new Vector3(0.14f, 0.20f, 0.14f), white);     // head
        AddPart(headPivot, new Vector3(0f, 0.27f, 0.03f), new Vector3(0.04f, 0.06f, 0.10f), red);       // comb
        AddPart(headPivot, new Vector3(0f, 0.15f, 0.13f), new Vector3(0.07f, 0.04f, 0.07f), orange);    // beak
        AddPart(headPivot, new Vector3(0f, 0.08f, 0.11f), new Vector3(0.04f, 0.06f, 0.03f), red);       // wattle
        AddPart(headPivot, new Vector3(0.072f, 0.18f, 0.06f), new Vector3(0.01f, 0.03f, 0.03f), black); // eye
        AddPart(headPivot, new Vector3(-0.072f, 0.18f, 0.06f), new Vector3(0.01f, 0.03f, 0.03f), black);
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

    // --- Getting blown up ---

    public void BlowUp()
    {
        for (int i = 0; i < 16; i++)
        {
            GameObject feather = new GameObject("Feather");
            feather.transform.position = transform.position + Vector3.up * 0.3f;
            feather.transform.localScale = new Vector3(0.10f, 0.02f, 0.06f);
            feather.transform.rotation = Random.rotation;
            feather.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            feather.AddComponent<MeshRenderer>().sharedMaterial = feathers;

            Rigidbody flight = feather.AddComponent<Rigidbody>();
            flight.linearVelocity = Random.insideUnitSphere * 5f + Vector3.up * 3f;
            flight.angularVelocity = Random.insideUnitSphere * 10f;

            Destroy(feather, 1.5f);
        }

        Destroy(gameObject);
    }

    // --- Being on a leash ---

    public void Catch(Transform newHolder)
    {
        StopAllCoroutines();
        headPivot.localRotation = Quaternion.identity;
        holder = newHolder;
    }

    public void Release()
    {
        holder = null;
        walking = false;
        StartCoroutine(Live());
    }

    void FollowHolder()
    {
        Vector3 pos = transform.position;
        Vector3 flat = holder.position - pos;
        flat.y = 0f;
        float distance = flat.magnitude;

        walking = false;
        if (distance <= 2f) return;   // close enough - no need to crowd you

        Vector3 direction = flat / distance;
        facing = Quaternion.LookRotation(direction);

        // The further behind it gets, the faster it hurries
        Vector3 next = pos + direction * Mathf.Min(distance * 1.5f, 7f) * Time.deltaTime;

        if (CanWalkTo(next))
        {
            transform.position = next;
            walking = true;
        }
        else if (Vector3.Distance(pos, holder.position) > 10f && !HolderInWater())
        {
            transform.position = holder.position;   // stuck and left far behind - the leash yanks it along
        }
    }

    bool HolderInWater()
    {
        Vector3Int cell = Vector3Int.RoundToInt(holder.position + Vector3.up * 0.4f);
        return world.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.Water;
    }

    // --- Staying alive ---

    void Start()
    {
        facing = transform.rotation;
        StartCoroutine(Live());
    }

    void Update()
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);

        if (holder != null) FollowHolder();
        StayOnGround();

        // Waddle from side to side while walking
        if (walking) walkTime += Time.deltaTime;
        float waddle = walking ? Mathf.Sin(walkTime * 12f) : 0f;
        model.localRotation = Quaternion.Euler(0f, 0f, waddle * 6f);
    }

    // Falling is handled here, so a chicken drops whatever else it's doing
    // the moment the ground is taken away from under it
    void StayOnGround()
    {
        Vector3 pos = transform.position;
        int ground = GroundBelow(Mathf.RoundToInt(pos.x), pos.y, Mathf.RoundToInt(pos.z));
        float floor = ground >= 0 ? ground + 0.5f : -100f;

        if (pos.y > floor)
        {
            fallSpeed += 15f * Time.deltaTime;
            pos.y = Mathf.Max(pos.y - fallSpeed * Time.deltaTime, floor);
        }
        else
        {
            fallSpeed = 0f;
            pos.y = Mathf.MoveTowards(pos.y, floor, 5f * Time.deltaTime);   // stepping up onto a block
        }

        transform.position = pos;

        if (pos.y < -10f)
        {
            Destroy(gameObject);   // fell out of the world
            return;
        }

        // Chickens can't swim, so one caught in water pops back onto some grass
        int feet = Mathf.FloorToInt(pos.y + 0.5f);
        Vector3 dryLand;
        if (world.GetBlock(Mathf.RoundToInt(pos.x), feet, Mathf.RoundToInt(pos.z)) == BlockWorld.Water
            && FindGrass(world, out dryLand))
        {
            transform.position = dryLand;
        }
    }

    IEnumerator Live()
    {
        // Start at different times, so they don't all move at once
        yield return new WaitForSeconds(Random.Range(0f, 2f));

        while (true)
        {
            if (Random.value < 0.6f)
                yield return Peck();
            else
                yield return new WaitForSeconds(Random.Range(0.5f, 2f));

            yield return Walk();
        }
    }

    IEnumerator Peck()
    {
        int pecks = Random.Range(1, 4);

        for (int i = 0; i < pecks; i++)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                headPivot.localRotation = Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 70f, 0f, 0f);
                yield return null;
            }
        }

        headPivot.localRotation = Quaternion.identity;
        yield return new WaitForSeconds(Random.Range(0.2f, 1f));
    }

    // Stroll in a random direction, stopping at anything it can't walk over
    IEnumerator Walk()
    {
        Vector3 direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        float distanceLeft = Random.Range(2f, 6f);

        facing = Quaternion.LookRotation(direction);
        yield return new WaitForSeconds(0.2f);

        walking = true;

        while (distanceLeft > 0f)
        {
            float step = WalkSpeed * Time.deltaTime;
            Vector3 next = transform.position + direction * step;

            if (!CanWalkTo(next)) break;

            transform.position = next;
            distanceLeft -= step;
            yield return null;
        }

        walking = false;
    }

    // --- Looking at the world ---

    bool CanWalkTo(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x);
        int z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);

        if (ground < 0) return false;                                  // edge of the world, or a bottomless hole
        if (world.GetBlock(x, ground + 1, z) != BlockWorld.Air) return false;   // water, or a block in the way
        if (pos.y - (ground + 0.5f) > 2f) return false;                // too big a drop

        return true;
    }

    // The block a chicken at this height would stand on. It starts looking from
    // level with its feet, so it can step up one block but not onto your roof.
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
}
