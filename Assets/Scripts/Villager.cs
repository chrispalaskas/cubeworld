using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Little people who live in the world. They wander about near home, wave
// at you, chat to each other, chase chickens, jump for joy, and now and then
// plant a tree. They walk through open doors but not closed ones.
//
// TNT and most swords won't hurt them - they just get a fright (or a flight).
// Only a diamond sword can defeat them.
// Like the animals they have no collider, so they never get in your way.
public class Villager : MonoBehaviour, ILeashable
{
    const int HowMany = 6;
    const float WalkSpeed = 1.6f;
    const float RunSpeed = 3.5f;
    const float Gravity = 15f;
    const float Height = 1.12f;
    const float HomeRange = 12f;      // how far from home they like to wander
    const int MostTrees = 40;         // they stop planting once the world has this many

    static readonly string[] Names =
        { "Bob", "Mia", "Leo", "Zoe", "Max", "Ivy", "Sam", "Ada", "Tom", "Lily", "Ben", "Ruby", "Nico", "Elena" };

    static readonly string[] Hellos =
        { "Hi!", "Hello there!", "Nice day!", "Hey, it's you!", "Howdy!", "Love your house!", "Built anything cool?" };

    // Little conversations: the first person says one line, the second answers
    static readonly string[][] Chats =
    {
        new[] { "Seen any squirrels?", "Loads, up in the trees!" },
        new[] { "Nice weather!", "Perfect for building!" },
        new[] { "Did you hear that BOOM?", "Someone's playing with TNT again..." },
        new[] { "Want to chase chickens?", "Maybe later!" },
        new[] { "I love diamonds.", "Who doesn't?" },
        new[] { "Have you tried the bounce pads?", "Wheee! Yes!" },
        new[] { "What's for dinner?", "Not chicken, I hope!" },
    };

    static readonly Color[] Shirts =
    {
        new Color(0.85f, 0.20f, 0.20f), new Color(0.20f, 0.45f, 0.85f), new Color(0.25f, 0.65f, 0.30f),
        new Color(0.95f, 0.75f, 0.15f), new Color(0.60f, 0.30f, 0.75f), new Color(0.95f, 0.55f, 0.15f),
        new Color(0.90f, 0.45f, 0.65f), new Color(0.20f, 0.70f, 0.70f)
    };
    static readonly Color[] Trousers =
    {
        new Color(0.20f, 0.25f, 0.45f), new Color(0.35f, 0.25f, 0.15f), new Color(0.25f, 0.25f, 0.25f),
        new Color(0.45f, 0.40f, 0.30f)
    };
    static readonly Color[] Hair =
    {
        new Color(0.15f, 0.10f, 0.05f), new Color(0.45f, 0.28f, 0.12f), new Color(0.90f, 0.75f, 0.35f),
        new Color(0.70f, 0.25f, 0.10f), new Color(0.10f, 0.10f, 0.10f), new Color(0.80f, 0.80f, 0.80f)
    };
    static readonly Color[] Skin =
    {
        new Color(0.98f, 0.82f, 0.68f), new Color(0.90f, 0.70f, 0.52f), new Color(0.72f, 0.52f, 0.36f),
        new Color(0.50f, 0.34f, 0.22f), new Color(0.36f, 0.24f, 0.16f)
    };

    // One material per colour, shared by everyone wearing it
    static readonly Dictionary<Color, Material> paints = new Dictionary<Color, Material>();

    private BlockWorld world;
    private Vector3 home;
    private string villagerName;

    private Transform model, headPivot, leftArm, rightArm, leftLeg, rightLeg;
    private Quaternion facing;
    private float verticalSpeed;
    private Vector3 knock;          // being thrown sideways by a blast or a hit
    private bool walking, running, waving, armsUp;
    private float walkTime;
    private float headTurn, headNod;

    private Transform holder;       // whoever has us on a leash
    private Villager chatPartner;   // who we're talking to, if anyone
    private bool plantedTree;
    private float lastHello = -100f;

    private string saying = "";
    private float sayUntil;
    private GUIStyle bubbleStyle, nameStyle;

    // --- Making people ---

    // A little village of people, close to where you start
    public static void SpawnAll(BlockWorld world)
    {
        Vector3 middle = world.FindSpawnPoint();

        for (int i = 0; i < HowMany; i++)
        {
            Vector3 spot;
            if (FindSpotNear(world, middle, 8, out spot))
                SpawnAt(world, spot);
        }
    }

    // One new person, standing exactly here. Wherever they first stand is home.
    public static Villager SpawnAt(BlockWorld world, Vector3 spot)
    {
        GameObject obj = new GameObject("Villager");
        obj.transform.position = spot;
        obj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Villager person = obj.AddComponent<Villager>();
        person.world = world;
        person.home = spot;
        person.villagerName = Names[Random.Range(0, Names.Length)];
        person.BuildBody();
        return person;
    }

    // Out of a villager egg you placed - they say hello to the world
    public static void Hatch(BlockWorld world, Vector3 spot)
    {
        Villager person = SpawnAt(world, spot);
        person.Say("Hello, world! I'm " + person.villagerName + "!", 3f);
        person.lastHello = Time.time;   // they've just said hello, so they don't say it again straight away
    }

    // A grassy spot with room to stand, within "range" blocks of "middle"
    // if possible, otherwise anywhere at all
    static bool FindSpotNear(BlockWorld world, Vector3 middle, int range, out Vector3 spot)
    {
        for (int tries = 0; tries < 300; tries++)
        {
            int r = tries < 150 ? range : BlockWorld.SizeX;   // give up on staying close after a while
            int x = Mathf.RoundToInt(middle.x) + Random.Range(-r, r + 1);
            int z = Mathf.RoundToInt(middle.z) + Random.Range(-r, r + 1);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == BlockWorld.Grass && world.GetBlock(x, y + 2, z) == BlockWorld.Air)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    static Material Paint(Color colour)
    {
        Material mat;
        if (!paints.TryGetValue(colour, out mat) || mat == null)
        {
            mat = BlockWorld.MakeMaterial(colour);
            paints[colour] = mat;
        }
        return mat;
    }

    static Color Pick(Color[] colours)
    {
        return colours[Random.Range(0, colours.Length)];
    }

    // Built from cubes, facing along Z with feet at the origin. Arms, legs and
    // head each hang from a pivot (shoulder, hip, neck) so they can swing.
    void BuildBody()
    {
        Material shirt = Paint(Pick(Shirts));
        Material trousers = Paint(Pick(Trousers));
        Material hair = Paint(Pick(Hair));
        Material skin = Paint(Pick(Skin));
        Material black = Paint(new Color(0.05f, 0.05f, 0.05f));

        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.30f, 0.44f, 0.18f), shirt);   // body

        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            AddPart(leg, new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.32f, 0.14f), trousers);
            AddPart(leg, new Vector3(0f, -0.33f, 0.02f), new Vector3(0.13f, 0.06f, 0.18f), black);   // shoe
        }

        leftArm = Pivot(model, new Vector3(-0.21f, 0.78f, 0f));
        rightArm = Pivot(model, new Vector3(0.21f, 0.78f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.10f, 0f), new Vector3(0.11f, 0.20f, 0.11f), shirt);   // sleeve
            AddPart(arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.09f, 0.18f, 0.09f), skin);    // hand
        }

        headPivot = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(headPivot, new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.30f, 0.30f), skin);
        AddPart(headPivot, new Vector3(0f, 0.29f, -0.01f), new Vector3(0.32f, 0.06f, 0.33f), hair);    // top
        AddPart(headPivot, new Vector3(0f, 0.18f, -0.15f), new Vector3(0.32f, 0.24f, 0.04f), hair);    // back
        AddPart(headPivot, new Vector3(-0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);   // eyes
        AddPart(headPivot, new Vector3(0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);
        AddPart(headPivot, new Vector3(0f, 0.07f, 0.151f), new Vector3(0.10f, 0.02f, 0.01f), black);       // smile
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
        StartCoroutine(Live());
    }

    void Update()
    {
        if (holder != null)
            FollowHolder();
        else if (chatPartner != null)
            FaceTowards(chatPartner.transform.position);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);

        BeThrown();
        StayOnGround();
        Animate();
    }

    void FaceTowards(Vector3 target)
    {
        Vector3 flat = target - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) facing = Quaternion.LookRotation(flat);
    }

    void Animate()
    {
        if (walking) walkTime += Time.deltaTime * (running ? 1.8f : 1f);
        float swing = walking ? Mathf.Sin(walkTime * 9f) * 35f : 0f;

        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(armsUp ? 180f : -swing, 0f, armsUp ? 15f : 0f);

        if (waving)
            rightArm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(Time.time * 12f) * 20f);
        else
            rightArm.localRotation = Quaternion.Euler(armsUp ? 180f : swing, 0f, armsUp ? -15f : 0f);

        headPivot.localRotation = Quaternion.Euler(headNod, headTurn, 0f);
    }

    // --- Falling, being thrown, and not getting stuck ---

    // Air and open doors can be walked through. Everything else is in the way.
    bool Passable(int x, int y, int z)
    {
        byte type = world.GetBlock(x, y, z);
        return type == BlockWorld.Air || (BlockWorld.IsDoor(type) && BlockWorld.DoorIsOpen(type));
    }

    // The block someone at this height would stand on. It starts looking from
    // level with their feet, so they can step up one block but not onto your roof.
    int GroundBelow(int x, float y, int z)
    {
        for (int cell = Mathf.FloorToInt(y + 0.5f); cell >= 0; cell--)
        {
            if (!Passable(x, cell, z) && world.GetBlock(x, cell, z) != BlockWorld.Water)
                return cell;
        }
        return -1;
    }

    bool CanWalkTo(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.x);
        int z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);

        if (ground < 0) return false;                                            // edge of the world, or a bottomless hole
        if (!Passable(x, ground + 1, z) || !Passable(x, ground + 2, z)) return false;   // water, a wall, or too low a ceiling
        if (pos.y - (ground + 0.5f) > 2f) return false;                          // too big a drop

        return true;
    }

    void BeThrown()
    {
        if (knock.sqrMagnitude < 0.01f)
        {
            knock = Vector3.zero;
            return;
        }

        // Fly sideways until we hit a wall
        Vector3 next = transform.position + knock * Time.deltaTime;
        int x = Mathf.RoundToInt(next.x), z = Mathf.RoundToInt(next.z);
        int feet = Mathf.FloorToInt(next.y + 0.5f);

        if (BlockWorld.InBounds(x, 0, z) && Passable(x, feet, z) && Passable(x, feet + 1, z))
            transform.position = next;
        else
            knock = Vector3.zero;

        // Slowing down, much faster once we're back on the ground
        bool grounded = verticalSpeed == 0f;
        knock = Vector3.MoveTowards(knock, Vector3.zero, (grounded ? 12f : 2f) * Time.deltaTime);
    }

    void StayOnGround()
    {
        Vector3 pos = transform.position;
        int x = Mathf.RoundToInt(pos.x), z = Mathf.RoundToInt(pos.z);
        int ground = GroundBelow(x, pos.y, z);
        float floor = ground >= 0 ? ground + 0.5f : -100f;

        if (verticalSpeed > 0f || pos.y > floor + 0.01f)
        {
            verticalSpeed -= Gravity * Time.deltaTime;

            // Bump our head on a ceiling and stop going up
            int top = Mathf.FloorToInt(pos.y + Height + 0.5f);
            if (verticalSpeed > 0f && !Passable(x, top, z)) verticalSpeed = 0f;

            pos.y += verticalSpeed * Time.deltaTime;
            if (pos.y <= floor)
            {
                pos.y = floor;
                verticalSpeed = 0f;
            }
        }
        else
        {
            verticalSpeed = 0f;
            pos.y = Mathf.MoveTowards(pos.y, floor, 5f * Time.deltaTime);   // stepping up onto a block
        }

        transform.position = pos;

        // Fell out of the world, or landed in water? People can't swim here,
        // so they find their way back home.
        int feet = Mathf.FloorToInt(pos.y + 0.5f);
        if (pos.y < -10f || world.GetBlock(x, feet, z) == BlockWorld.Water)
        {
            Vector3 spot;
            if (FindSpotNear(world, home, 6, out spot))
            {
                transform.position = spot;
                verticalSpeed = 0f;
                knock = Vector3.zero;
            }
        }
    }

    bool OnGround()
    {
        return verticalSpeed == 0f;
    }

    // --- Things that happen to them ---

    // Hit with a sword: "Ouch!", a little knock backwards, and running away
    public void Hurt(Vector3 from)
    {
        StopEverything();

        Vector3 away = transform.position - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;

        knock = away * 5f;
        verticalSpeed = 4f;
        Say(Random.value < 0.5f ? "Ouch!" : "Hey! That hurt!");
        StartCoroutine(RunAwayFrom(from));
    }

    // Hit with a diamond sword: they fall apart into their blocks, like a
    // toy knocked over, and the pieces tumble away and vanish
    public void Defeat()
    {
        StopEverything();
        sayUntil = 0f;

        foreach (MeshRenderer piece in model.GetComponentsInChildren<MeshRenderer>())
        {
            Transform block = piece.transform;
            block.SetParent(null, true);   // keeps its place and size in the world

            Rigidbody tumble = block.gameObject.AddComponent<Rigidbody>();
            tumble.linearVelocity = Random.insideUnitSphere * 3f + Vector3.up * 3f;
            tumble.angularVelocity = Random.insideUnitSphere * 8f;

            Destroy(block.gameObject, 1.5f);
        }

        Destroy(gameObject);
    }

    // Caught in a TNT blast: sent flying, then a bit dizzy
    public void Blast(Vector3 centre, float radius)
    {
        StopEverything();

        Vector3 away = transform.position - centre;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
        away.y = 0f;

        knock = away.normalized * radius * 1.5f;
        verticalSpeed = 9f;
        armsUp = true;
        Say("Waaaaah!");
        StartCoroutine(Dizzy());
    }

    public void Catch(Transform newHolder)
    {
        StopEverything();
        holder = newHolder;
        Say("Where are we going?");
    }

    public void Release()
    {
        holder = null;
        walking = false;
        Say("Thanks for the walk!");
        StartCoroutine(Live());
    }

    // Stop whatever we were doing, so something new can happen
    void StopEverything()
    {
        StopAllCoroutines();
        walking = running = waving = armsUp = false;
        headTurn = headNod = 0f;

        if (chatPartner != null)
        {
            Villager partner = chatPartner;
            chatPartner = null;
            partner.LeaveChat();
        }
    }

    // The person we were chatting to has gone off to do something else
    void LeaveChat()
    {
        if (chatPartner == null && holder == null) return;
        chatPartner = null;
        if (holder != null) return;   // still on the leash - stay with whoever has us

        StopAllCoroutines();
        headTurn = headNod = 0f;
        StartCoroutine(Live());
    }

    void Say(string words, float seconds = 2.5f)
    {
        saying = words;
        sayUntil = Time.time + seconds;
    }

    void FollowHolder()
    {
        Vector3 pos = transform.position;
        Vector3 flat = holder.position - pos;
        flat.y = 0f;
        float distance = flat.magnitude;

        walking = false;
        if (distance <= 2f) return;

        Vector3 direction = flat / distance;
        facing = Quaternion.LookRotation(direction);
        Vector3 next = pos + direction * Mathf.Min(distance * 1.5f, 7f) * Time.deltaTime;

        if (CanWalkTo(next))
        {
            transform.position = next;
            walking = true;
        }
        else if (distance > 10f)
        {
            transform.position = holder.position;   // stuck and left far behind - the leash yanks them along
        }
    }

    // --- Deciding what to do ---

    IEnumerator Live()
    {
        // Start at different times, so they don't all move at once
        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        while (true)
            yield return DoSomething();
    }

    IEnumerator DoSomething()
    {
        // You came to visit? Say hello! (But not every single time.)
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player != null && Time.time - lastHello > 25f && FlatDistance(player.transform.position) < 6f)
        {
            yield return SayHello(player.transform);
            yield break;
        }

        float roll = Random.value;

        if (roll < 0.15f)
        {
            Villager friend = FriendNearby();
            if (friend != null)
            {
                yield return Chat(friend);
                yield break;
            }
        }

        if (roll < 0.25f)
        {
            Chicken chicken = ChickenNearby();
            if (chicken != null)
            {
                yield return ChaseChicken(chicken);
                yield break;
            }
        }

        if (roll < 0.32f)
            yield return JumpForJoy();
        else if (roll < 0.36f && !plantedTree && world.Trees.Count < MostTrees)
            yield return PlantTree();
        else if (roll < 0.55f)
            yield return LookAround();
        else
            yield return Wander();
    }

    float FlatDistance(Vector3 to)
    {
        Vector3 flat = to - transform.position;
        flat.y = 0f;
        return flat.magnitude;
    }

    // Someone close by who isn't busy
    Villager FriendNearby()
    {
        foreach (Villager other in FindObjectsByType<Villager>(FindObjectsSortMode.None))
        {
            if (other == this || other.holder != null || other.chatPartner != null) continue;
            if (FlatDistance(other.transform.position) < 8f) return other;
        }
        return null;
    }

    Chicken ChickenNearby()
    {
        foreach (Chicken chicken in FindObjectsByType<Chicken>(FindObjectsSortMode.None))
            if (FlatDistance(chicken.transform.position) < 10f) return chicken;
        return null;
    }

    // --- Walking ---

    // Walk towards a spot until we're close enough, get stuck, or run out of
    // time. The target is asked for again every step, so it can move.
    IEnumerator WalkTowards(System.Func<Vector3> target, float speed, float closeEnough, float seconds)
    {
        walking = true;
        running = speed > WalkSpeed;

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            Vector3 goal = target();
            Vector3 flat = goal - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= closeEnough) break;

            Vector3 direction = flat.normalized;
            facing = Quaternion.LookRotation(direction);

            Vector3 next = transform.position + direction * speed * Time.deltaTime;
            if (!CanWalkTo(next)) break;

            transform.position = next;
            yield return null;
        }

        walking = running = false;
    }

    IEnumerator Wander()
    {
        // Mostly any direction - but if we've strayed far from home, back that way
        float angle = Random.Range(0f, 360f);
        if (FlatDistance(home) > HomeRange)
        {
            Vector3 toHome = home - transform.position;
            angle = Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg + Random.Range(-30f, 30f);
        }

        Vector3 goal = transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(3f, 7f);
        yield return WalkTowards(() => goal, WalkSpeed, 0.2f, 6f);
        yield return new WaitForSeconds(Random.Range(0.3f, 1.5f));
    }

    IEnumerator LookAround()
    {
        float seconds = Random.Range(1.5f, 3f);
        float side = Random.value < 0.5f ? -1f : 1f;

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            headTurn = Mathf.Sin(t / seconds * Mathf.PI * 2f) * 50f * side;
            yield return null;
        }
        headTurn = 0f;
    }

    // --- Things to do ---

    IEnumerator SayHello(Transform player)
    {
        lastHello = Time.time;
        FaceTowards(player.position);
        Say(Hellos[Random.Range(0, Hellos.Length)]);

        waving = true;
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            FaceTowards(player.position);   // keep looking at you while waving
            yield return null;
        }
        waving = false;
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator Chat(Villager friend)
    {
        // Both stop what they're doing and turn to face each other
        friend.StopEverything();
        chatPartner = friend;
        friend.chatPartner = this;

        Say("Hey, " + friend.villagerName + "!", 1.5f);
        Transform them = friend.transform;
        yield return WalkTowards(() => them.position, WalkSpeed, 1.3f, 5f);

        string[] lines = Chats[Random.Range(0, Chats.Length)];
        Say(lines[0]);
        yield return Nod(2.5f);

        if (chatPartner != friend) yield break;   // they were called away
        friend.Say(lines[1]);
        friend.StartCoroutine(friend.Nod(2.5f));
        yield return new WaitForSeconds(2.5f);

        // Off they go
        if (chatPartner == friend)
        {
            chatPartner = null;
            friend.LeaveChat();
        }
    }

    // Little nods of the head while talking
    IEnumerator Nod(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            headNod = Mathf.Sin(t * 10f) * 8f;
            yield return null;
        }
        headNod = 0f;
    }

    IEnumerator ChaseChicken(Chicken chicken)
    {
        Say("Here, chicky chicky!");
        yield return WalkTowards(() => chicken != null ? chicken.transform.position : transform.position,
                                 RunSpeed * 0.8f, 0.7f, 6f);

        Say(chicken != null && FlatDistance(chicken.transform.position) < 1f ? "Got you!" : "Come back!", 1.5f);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator JumpForJoy()
    {
        armsUp = true;
        Say(Random.value < 0.5f ? "Yippee!" : "Wheee!", 1.5f);

        for (int hop = 0; hop < 2; hop++)
        {
            if (OnGround()) verticalSpeed = 5f;
            yield return new WaitForSeconds(0.7f);
        }

        armsUp = false;
    }

    IEnumerator PlantTree()
    {
        // A spot a couple of blocks in front, on the grass
        Vector3 ahead = transform.position + transform.forward * 2.5f;
        int x = Mathf.RoundToInt(ahead.x), z = Mathf.RoundToInt(ahead.z);
        int ground = GroundBelow(x, transform.position.y + 1f, z);
        if (ground < 0 || world.GetBlock(x, ground, z) != BlockWorld.Grass) yield break;

        Say("Let's plant a tree!");
        yield return WalkTowards(() => new Vector3(x, transform.position.y, z), WalkSpeed, 1.2f, 3f);

        // Bend down to dig
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            headNod = 30f + Mathf.Sin(t * 12f) * 10f;
            yield return null;
        }
        headNod = 0f;

        if (world.GrowTreeAt(x, ground + 1, z))
        {
            plantedTree = true;
            armsUp = true;
            Say("Ta-da!", 2f);
            yield return new WaitForSeconds(1.5f);
            armsUp = false;
        }
        else
        {
            Say("Hmm, not enough room here.", 2f);
            yield return new WaitForSeconds(1f);
        }
    }

    // --- After a fright ---

    IEnumerator RunAwayFrom(Vector3 danger)
    {
        yield return new WaitForSeconds(0.4f);
        Say("Help!", 1.5f);

        Vector3 away = transform.position - danger;
        away.y = 0f;
        Vector3 goal = transform.position + away.normalized * 8f;
        yield return WalkTowards(() => goal, RunSpeed, 0.3f, 3f);

        StartCoroutine(Live());
    }

    IEnumerator Dizzy()
    {
        // Wait to land first
        yield return new WaitForSeconds(0.3f);
        while (!OnGround()) yield return null;
        armsUp = false;

        Say("Ooh, my head...", 2.5f);
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            headTurn = Mathf.Sin(t * 6f) * 25f;
            headNod = Mathf.Cos(t * 6f) * 10f;
            yield return null;
        }
        headTurn = headNod = 0f;

        StartCoroutine(Live());
    }

    // --- Speech bubbles and names ---

    void OnGUI()
    {
        if (BlockInteractor.InventoryOpen) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 top = transform.position + Vector3.up * (Height + 0.3f);
        float distance = Vector3.Distance(cam.transform.position, top);
        bool talking = Time.time < sayUntil;
        if (distance > 16f || (!talking && distance > 7f)) return;

        Vector3 screen = cam.WorldToScreenPoint(top);
        if (screen.z <= 0f) return;                                // behind us
        if (Physics.Linecast(cam.transform.position, top)) return; // a wall in the way

        if (bubbleStyle == null)
        {
            bubbleStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, fontSize = 14, alignment = TextAnchor.MiddleCenter };
            bubbleStyle.normal.background = Texture2D.whiteTexture;
            bubbleStyle.normal.textColor = Color.black;
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        float x = screen.x;
        float y = Screen.height - screen.y;   // GUI counts down from the top, the camera counts up

        GUI.Label(new Rect(x - 60f, y - 4f, 120f, 20f), villagerName, nameStyle);

        if (talking)
        {
            GUIContent words = new GUIContent(saying);
            float width = Mathf.Min(bubbleStyle.CalcSize(words).x + 12f, 220f);
            float height = bubbleStyle.CalcHeight(words, width) + 6f;
            GUI.Box(new Rect(x - width / 2f, y - height - 6f, width, height), words, bubbleStyle);
        }
    }
}
