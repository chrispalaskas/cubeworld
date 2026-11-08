using System.Collections;
using UnityEngine;

// A little squirrel that lives in the trees. It scurries up the trunk, hops
// around on the leaves, leaps across to nearby trees, and when none are close
// enough it climbs down and runs along the ground to another one.
//
// Squirrels have no colliders, so they never get in the way of the player
// or of breaking and placing blocks.
public class Squirrel : MonoBehaviour, ILeashable
{
    const float ClimbSpeed = 2.5f;
    const float RunSpeed = 4f;
    const float HopSpeed = 5f;
    const float LeapRange = 8f;      // how far it can jump between trees
    const float RunRange = 25f;      // how far it will run to find another tree

    private BlockWorld world;
    private Vector3Int tree;         // the bottom block of the trunk it is on
    private Quaternion facing;       // the way it wants to point
    private Transform model;
    private Transform tailPivot;
    private bool running;
    private bool climbWorked;
    private float tailPhase;
    private Transform holder;        // whoever has us on a leash

    // --- Making squirrels ---

    public static void SpawnAll(BlockWorld world)
    {
        if (world.Trees.Count == 0) return;

        // One material per colour, shared by every squirrel
        Material[] furs =
        {
            BlockWorld.MakeMaterial(new Color(0.60f, 0.30f, 0.12f)),   // red
            BlockWorld.MakeMaterial(new Color(0.48f, 0.46f, 0.44f)),   // grey
        };
        Material eyes = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));

        int count = Mathf.Min(world.Trees.Count / 3 + 1, 15);

        for (int i = 0; i < count; i++)
        {
            Vector3Int home = world.Trees[Random.Range(0, world.Trees.Count)];

            GameObject obj = new GameObject("Squirrel");
            obj.transform.position = (Vector3)home + new Vector3(0.55f, -0.5f, 0f);

            Squirrel squirrel = obj.AddComponent<Squirrel>();
            squirrel.world = world;
            squirrel.tree = home;
            squirrel.BuildBody(furs[Random.Range(0, furs.Length)], eyes);
        }
    }

    // A squirrel is just a few small cubes. It faces along Z with its feet at
    // the origin, which makes it easy to stand on things.
    void BuildBody(Material fur, Material eyes)
    {
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.16f, 0f), new Vector3(0.18f, 0.16f, 0.34f), fur);        // body
        AddPart(model, new Vector3(0f, 0.24f, 0.20f), new Vector3(0.16f, 0.15f, 0.16f), fur);     // head

        for (int side = -1; side <= 1; side += 2)
        {
            AddPart(model, new Vector3(side * 0.05f, 0.34f, 0.18f), new Vector3(0.04f, 0.06f, 0.03f), fur);    // ear
            AddPart(model, new Vector3(side * 0.082f, 0.26f, 0.24f), new Vector3(0.015f, 0.04f, 0.04f), eyes); // eye
            AddPart(model, new Vector3(side * 0.08f, 0.04f, 0.12f), new Vector3(0.05f, 0.08f, 0.07f), fur);    // front leg
            AddPart(model, new Vector3(side * 0.08f, 0.04f, -0.12f), new Vector3(0.05f, 0.08f, 0.07f), fur);   // back leg
        }

        // The tail hangs off a pivot so the whole thing can swish
        tailPivot = new GameObject("Tail").transform;
        tailPivot.SetParent(model, false);
        tailPivot.localPosition = new Vector3(0f, 0.20f, -0.17f);

        AddPart(tailPivot, new Vector3(0f, 0.08f, -0.06f), new Vector3(0.10f, 0.20f, 0.10f), fur);
        AddPart(tailPivot, new Vector3(0f, 0.24f, -0.03f), new Vector3(0.13f, 0.18f, 0.13f), fur);
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

    // --- Being on a leash ---

    public void Catch(Transform newHolder)
    {
        StopAllCoroutines();
        holder = newHolder;
    }

    public void Release()
    {
        holder = null;
        running = false;
        StartCoroutine(BackToTheTrees());
    }

    IEnumerator BackToTheTrees()
    {
        Vector3Int nearest;
        if (FindNearestTree(out nearest))
        {
            tree = nearest;
            if (DryPath(transform.position, tree))
                yield return RunTo(TrunkSide(tree));
        }

        yield return Live();
    }

    void FollowHolder()
    {
        Vector3 pos = transform.position;
        Vector3 flat = holder.position - pos;
        flat.y = 0f;
        float distance = flat.magnitude;

        running = false;

        if (distance > 2f)
        {
            Vector3 direction = flat / distance;
            facing = Quaternion.LookRotation(direction);

            // The further behind it gets, the faster it hurries
            Vector3 next = pos + direction * Mathf.Min(distance * 1.5f, 7f) * Time.deltaTime;
            int x = Mathf.RoundToInt(next.x);
            int z = Mathf.RoundToInt(next.z);
            int ground = world.GetGroundHeight(x, z);

            if (ground >= 0 && world.GetBlock(x, ground + 1, z) != BlockWorld.Water)
            {
                pos = next;
                running = true;
            }
            else if (Vector3.Distance(pos, holder.position) > 10f && !HolderInWater())
            {
                transform.position = holder.position;   // stuck and left far behind - the leash yanks it along
                return;
            }
        }
        else
        {
            facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        // Come down out of the tree, and follow the ups and downs of the ground
        int below = world.GetGroundHeight(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
        if (below >= 0)
            pos.y = Mathf.MoveTowards(pos.y, below + 0.5f, 6f * Time.deltaTime);

        transform.position = pos;
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
        tailPhase = Random.Range(0f, 10f);
        StartCoroutine(Live());
    }

    void Update()
    {
        if (holder != null) FollowHolder();

        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 540f * Time.deltaTime);

        // Swish the tail, faster when in a hurry, and bounce along when running
        float time = Time.time + tailPhase;
        tailPivot.localRotation = Quaternion.Euler(Mathf.Sin(time * (running ? 14f : 5f)) * 15f, 0f, 0f);
        model.localPosition = running ? Vector3.up * Mathf.Abs(Mathf.Sin(time * 14f)) * 0.08f : Vector3.zero;
    }

    IEnumerator Live()
    {
        bool upTree = false;

        while (true)
        {
            yield return null;   // always wait a frame, so this loop can never freeze the game

            // Someone chopped our tree down!
            if (world.TrunkTop(tree) < 0)
            {
                yield return Fall();
                upTree = false;

                Vector3Int other;
                if (FindTreeToRunTo(out other))
                {
                    tree = other;
                    yield return RunTo(TrunkSide(tree));
                }
                else
                {
                    yield return Wander();
                }
                continue;
            }

            if (!upTree)
            {
                yield return ClimbUp();
                if (!climbWorked) continue;
                upTree = true;
            }

            // Hop about on the leaves for a bit
            int hops = Random.Range(2, 6);
            for (int i = 0; i < hops && upTree; i++)
            {
                Vector3 spot;
                if (!FindLeafSpot(tree, out spot)) break;

                yield return Hop(spot, 0.5f);
                yield return RestOnLeaves(Random.Range(0.2f, 1.2f));

                if (!StandingOnBlock())
                {
                    yield return Fall();
                    upTree = false;
                }
            }
            if (!upTree) continue;

            // Usually jump to a tree next door, if there is one in range...
            Vector3Int next;
            Vector3 landing;
            if (Random.value < 0.7f && FindTreeToLeapTo(out next, out landing))
            {
                yield return Hop(landing, 1.5f);
                tree = next;
                continue;
            }

            // ...otherwise climb down, and run over to one further away
            yield return ClimbDown();
            upTree = false;

            Vector3Int distant;
            if (world.TrunkTop(tree) >= 0 && FindTreeToRunTo(out distant))
            {
                tree = distant;
                yield return RunTo(TrunkSide(tree));
            }
        }
    }

    // --- Ways of moving ---

    // Up the side of the trunk, nose to the sky and belly against the bark
    IEnumerator ClimbUp()
    {
        climbWorked = false;

        int top = world.TrunkTop(tree);
        if (top < 0) yield break;

        Vector3 side = NearestTrunkFace(tree);
        Vector3 pos = (Vector3)tree + side * 0.55f;
        pos.y = Mathf.Max(transform.position.y, tree.y - 0.5f);
        transform.position = pos;
        facing = Quaternion.LookRotation(Vector3.up, side);

        // Squirrels climb in little bursts
        float stopAt = Mathf.Lerp(pos.y, top, Random.Range(0.3f, 0.7f));
        bool stopped = false;

        while (transform.position.y < top)
        {
            if (!TrunkAt(transform.position.y))
            {
                yield return Fall();
                yield break;
            }

            if (!stopped && transform.position.y >= stopAt)
            {
                stopped = true;
                yield return new WaitForSeconds(Random.Range(0.1f, 0.6f));
            }

            transform.position += Vector3.up * ClimbSpeed * Time.deltaTime;
            yield return null;
        }

        climbWorked = true;
    }

    IEnumerator ClimbDown()
    {
        int top = world.TrunkTop(tree);
        if (top < 0) yield break;

        Vector3 side = NearestTrunkFace(tree);
        Vector3 start = (Vector3)tree + side * 0.55f;
        start.y = top;
        yield return Hop(start, 0.3f);

        facing = Quaternion.LookRotation(Vector3.down, side);

        float bottom = tree.y - 0.5f;
        while (transform.position.y > bottom)
        {
            if (!TrunkAt(transform.position.y))
            {
                yield return Fall();
                yield break;
            }

            transform.position += Vector3.down * ClimbSpeed * Time.deltaTime;
            yield return null;
        }

        Vector3 pos = transform.position;
        pos.y = bottom;
        transform.position = pos;
    }

    // A jump in a smooth curve from here to there
    IEnumerator Hop(Vector3 target, float arcHeight)
    {
        Vector3 start = transform.position;
        FaceTowards(target);

        float duration = Mathf.Max(Vector3.Distance(start, target) / HopSpeed, 0.25f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += arcHeight * 4f * t * (1f - t);
            transform.position = pos;
            yield return null;
        }

        transform.position = target;
    }

    // Scamper along the ground, following its ups and downs
    IEnumerator RunTo(Vector3 target)
    {
        running = true;

        // A time limit, just in case the world changes and it can't get there
        float timeLeft = 20f;
        target.y = transform.position.y;

        while (timeLeft > 0f)
        {
            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            if (flat.magnitude < 0.1f) break;

            FaceTowards(new Vector3(target.x, pos.y, target.z));
            pos += flat.normalized * RunSpeed * Time.deltaTime;

            int ground = world.GetGroundHeight(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
            if (ground >= 0)
                pos.y = Mathf.MoveTowards(pos.y, ground + 0.5f, 6f * Time.deltaTime);

            transform.position = pos;
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        running = false;
    }

    // Nothing to hold on to any more - drop to the ground
    IEnumerator Fall()
    {
        running = false;
        facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float speed = 0f;

        while (true)
        {
            Vector3 pos = transform.position;
            int ground = world.GetGroundHeight(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
            float floor = ground >= 0 ? ground + 0.5f : -10f;

            speed += 15f * Time.deltaTime;
            pos.y -= speed * Time.deltaTime;

            if (pos.y <= floor)
            {
                pos.y = floor;
                transform.position = pos;
                break;
            }

            transform.position = pos;
            yield return null;
        }

        // Landed in water or off the map? Squirrels can't swim, so pop back
        // to a tree - or leave the world if there are none left.
        Vector3 here = transform.position;
        bool wet = world.GetBlock(Mathf.RoundToInt(here.x), Mathf.RoundToInt(here.y + 0.1f), Mathf.RoundToInt(here.z)) == BlockWorld.Water;

        if (wet || here.y < 0f)
        {
            Vector3Int other;
            if (FindAnyStandingTree(out other))
            {
                tree = other;
                transform.position = TrunkSide(tree);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    IEnumerator Wander()
    {
        for (int tries = 0; tries < 8; tries++)
        {
            Vector3 spot = transform.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            if (DryPath(transform.position, spot))
            {
                yield return RunTo(spot);
                break;
            }
        }

        yield return new WaitForSeconds(Random.Range(1f, 3f));
    }

    IEnumerator RestOnLeaves(float seconds)
    {
        while (seconds > 0f && StandingOnBlock())
        {
            seconds -= Time.deltaTime;
            yield return null;
        }
    }

    // --- Looking at the world ---

    void FaceTowards(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            facing = Quaternion.LookRotation(direction);
    }

    bool TrunkAt(float y)
    {
        return world.GetBlock(tree.x, Mathf.RoundToInt(y + 0.1f), tree.z) == BlockWorld.Wood;
    }

    bool StandingOnBlock()
    {
        Vector3 pos = transform.position;
        byte below = world.GetBlock(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y - 0.1f), Mathf.RoundToInt(pos.z));
        return below != BlockWorld.Air && below != BlockWorld.Water;
    }

    // Which of the trunk's four sides is closest to us?
    Vector3 NearestTrunkFace(Vector3Int trunk)
    {
        Vector3 away = transform.position - trunk;

        if (Mathf.Abs(away.x) > Mathf.Abs(away.z))
            return away.x > 0f ? Vector3.right : Vector3.left;
        return away.z > 0f ? Vector3.forward : Vector3.back;
    }

    // The spot on the ground right beside a trunk
    Vector3 TrunkSide(Vector3Int trunk)
    {
        Vector3 pos = (Vector3)trunk + NearestTrunkFace(trunk) * 0.55f;
        pos.y = trunk.y - 0.5f;
        return pos;
    }

    bool FindLeafSpot(Vector3Int trunk, out Vector3 spot)
    {
        for (int tries = 0; tries < 8; tries++)
        {
            int x = trunk.x + Random.Range(-2, 3);
            int z = trunk.z + Random.Range(-2, 3);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == BlockWorld.Leaves)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    bool FindTreeToLeapTo(out Vector3Int next, out Vector3 landing)
    {
        next = Vector3Int.zero;
        landing = Vector3.zero;
        int found = 0;

        foreach (Vector3Int other in world.Trees)
        {
            if (other == tree || FlatDistance(other) > LeapRange) continue;
            if (world.TrunkTop(other) < 0) continue;

            Vector3 spot;
            if (!FindLeafSpot(other, out spot)) continue;

            // Each tree in range gets an equal chance of being the one picked
            found++;
            if (Random.Range(0, found) == 0)
            {
                next = other;
                landing = spot;
            }
        }

        return found > 0;
    }

    bool FindTreeToRunTo(out Vector3Int next)
    {
        next = Vector3Int.zero;
        int found = 0;

        foreach (Vector3Int other in world.Trees)
        {
            if (other == tree || FlatDistance(other) > RunRange) continue;
            if (world.TrunkTop(other) < 0) continue;
            if (!DryPath(transform.position, other)) continue;

            found++;
            if (Random.Range(0, found) == 0)
                next = other;
        }

        return found > 0;
    }

    bool FindAnyStandingTree(out Vector3Int next)
    {
        next = Vector3Int.zero;
        int found = 0;

        foreach (Vector3Int other in world.Trees)
        {
            if (world.TrunkTop(other) < 0) continue;

            found++;
            if (Random.Range(0, found) == 0)
                next = other;
        }

        return found > 0;
    }

    bool FindNearestTree(out Vector3Int nearest)
    {
        nearest = Vector3Int.zero;
        float best = float.MaxValue;

        foreach (Vector3Int other in world.Trees)
        {
            if (world.TrunkTop(other) < 0 || FlatDistance(other) >= best) continue;

            best = FlatDistance(other);
            nearest = other;
        }

        return best < float.MaxValue;
    }

    float FlatDistance(Vector3Int other)
    {
        Vector3 here = transform.position;
        return new Vector2(other.x - here.x, other.z - here.z).magnitude;
    }

    // Can we run in a straight line from a to b, without getting wet or
    // having to climb up or down anything too steep?
    bool DryPath(Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(Vector3.Distance(from, to) * 2f);
        int previous = world.GetGroundHeight(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.z));

        for (int i = 1; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, (float)i / steps);
            int x = Mathf.RoundToInt(p.x);
            int z = Mathf.RoundToInt(p.z);
            int ground = world.GetGroundHeight(x, z);

            if (ground < 0) return false;
            if (world.GetBlock(x, ground + 1, z) == BlockWorld.Water) return false;
            if (Mathf.Abs(ground - previous) > 2) return false;

            previous = ground;
        }

        return true;
    }
}
