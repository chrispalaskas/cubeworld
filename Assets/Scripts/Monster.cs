using System.Collections.Generic;
using UnityEngine;

// What every bad creature has in common: walking (or flying) about,
// noticing you, coming after you, getting hit, and going "poof".
//
// They only come out in survival worlds. At home there are zombies,
// skeletons, spiders and slimes (see Zombie.cs and MoreMonsters.cs); in the
// Nether there are fire imps. The Magic Land is far too nice for any of them.
//
// They're all scared of the Star Crystal, they all set off pressure plates,
// and TNT gets every one of them. Lava and fire hurt them (except fire imps).
public abstract class Monster : MonoBehaviour
{
    const float Gravity = 20f;

    public static readonly List<Monster> All = new List<Monster>();

    protected BlockWorld world;
    protected int health;
    protected float fallSpeed;
    protected Vector3 knock;            // being pushed back after you hit it
    protected float walkCycle;          // for swinging legs
    protected bool chasing;
    protected float attackTimer = 1f;

    private Vector3 wanderTo;
    private float wanderTimer;
    private float flashUntil;
    private float burnTimer;
    private bool climbing;
    private Renderer[] parts;

    // --- What makes each kind different ---

    protected abstract string Called { get; }        // "zombie" - for "A zombie got you!"
    protected abstract void BuildBody();
    protected virtual int MostHealth { get { return 10; } }
    protected virtual float WalkSpeed { get { return 1.3f; } }
    protected virtual float ChaseSpeed { get { return 2.4f; } }
    protected virtual float NoticeRange { get { return 16f; } }
    protected virtual bool Flies { get { return false; } }
    protected virtual bool FireProof { get { return false; } }
    protected virtual bool ClimbsWalls { get { return false; } }

    // For a close-up bite or bash: how much, how close, and how often
    protected virtual int HitDamage { get { return 3; } }
    protected virtual float HitRange { get { return 1.3f; } }
    protected virtual float HitSeconds { get { return 1.2f; } }

    // Where to aim at it, and how big a target it is
    public virtual float AimHeight { get { return 0.9f; } }
    public virtual float HitSize { get { return 0.6f; } }

    // Where it heads for when it's after you (straight at you, usually)
    protected virtual Vector3 ChaseGoal(Vector3 player, float distance) { return player; }

    // Every frame, before moving - for hopping, flapping and so on
    protected virtual void Think() { }

    protected virtual void Animate(bool walking) { }

    // When it's after you: by default, a bite if it's close enough
    protected virtual void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        attackTimer -= Time.deltaTime;
        if (flat > HitRange || Mathf.Abs(toPlayer.y) > 1.8f || attackTimer > 0f) return;

        attackTimer = HitSeconds;
        target.Hurt(HitDamage, "A " + Called + " got you!");
        FirstPersonPlayer player = target.GetComponent<FirstPersonPlayer>();
        if (player != null) player.LaunchUp(4f);   // a little knock into the air
    }

    // Defeated - slimes split up, the rest just go poof
    protected virtual void OnDefeated() { }

    // What it leaves behind in survival (spiders drop string, cows drop beef...)
    protected virtual void DropLoot() { }

    // --- Friendly animals (cows, sheep, pigs, dogs and cats - see Animals.cs) ---

    // Bad creatures come after you. Animals just wander about, and run away when hit.
    public virtual bool Hostile { get { return true; } }

    // Pressed E on it - returns true if that did something (taming, shearing...)
    public virtual bool Interact(byte holding) { return false; }

    // An animal's own plans for where to go. True if it has somewhere to be.
    protected virtual bool AnimalGoal(ref Vector3 goal, ref float speed)
    {
        if (Time.time > panicUntil) return false;
        Vector3 away = transform.position - panicFrom;
        away.y = 0f;
        goal = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * 4f;
        speed = ChaseSpeed * 1.3f;
        return true;
    }

    private float panicUntil;
    private Vector3 panicFrom;

    // The player (in any mode)
    protected static Transform Player { get { return BlockInteractor.Me != null ? BlockInteractor.Me.transform : null; } }

    // Somewhere to drop loot
    protected void Drop(byte item, int count)
    {
        if (WorldMenu.Survival && count > 0) ItemDrop.Spawn(item, count, transform.position + Vector3.up * 0.5f);
    }

    // --- Making one ---

    public static T Spawn<T>(BlockWorld world, Vector3 feet) where T : Monster
    {
        T monster = new GameObject(typeof(T).Name).AddComponent<T>();
        monster.world = world;
        monster.transform.position = feet;
        monster.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        monster.wanderTo = feet;
        monster.Setup();
        return monster;
    }

    protected void Setup()
    {
        BuildBody();
        health = MostHealth;
        parts = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Starts the right monsters coming in this place
    public static void BeginSpawning(BlockWorld world)
    {
        MonsterSpawner spawner = new GameObject("Monster Spawner").AddComponent<MonsterSpawner>();
        spawner.world = world;
    }

    // --- Moving about ---

    void Update()
    {
        // Monsters wait while you're in your inventory, or paused
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        PlayerHealth target = PlayerHealth.Instance;
        Vector3 me = transform.position;
        Vector3 goal = wanderTo;
        float speed = WalkSpeed;
        Vector3 toPlayer = Vector3.zero;
        chasing = false;

        if (!Hostile)
        {
            chasing = AnimalGoal(ref goal, ref speed);
        }
        else if (target != null && !target.Dead)
        {
            toPlayer = target.transform.position - me;
            float flat = new Vector2(toPlayer.x, toPlayer.z).magnitude;
            if (flat < NoticeRange && Mathf.Abs(toPlayer.y) < 10f)
            {
                chasing = true;
                goal = ChaseGoal(target.transform.position, flat);
                speed = ChaseSpeed;
                Attack(target, toPlayer, flat);
            }
        }

        // Scared of the Star Crystal - run away from it!
        Vector3 star;
        if (Hostile && StarShrine.Near(me, StarShrine.ScareRange, out star))
        {
            Vector3 away = me - star;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            goal = me + away.normalized * 5f;
            speed = Mathf.Max(ChaseSpeed, 2f);
            chasing = true;
            wanderTo = goal;
        }

        if (!chasing)
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = Random.Range(3f, 7f);
                wanderTo = me + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
            }
        }

        Think();

        Vector3 step = goal - me;
        step.y = 0f;
        bool walking = step.magnitude > (chasing ? 0.9f : 0.5f);
        Vector3 move = walking ? step.normalized * speed : Vector3.zero;

        // Face where it's going - or, when it's stopped to fight, face you
        Vector3 facing = walking ? step : (chasing ? new Vector3(toPlayer.x, 0f, toPlayer.z) : Vector3.zero);
        if (facing.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 6f * Time.deltaTime);

        // Being knocked back after a hit
        move += knock;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 12f * Time.deltaTime);

        bool moved;
        if (Flies)
        {
            moved = Fly(move * Time.deltaTime, chasing && target != null ? target.transform.position.y + 1.5f : GroundBelow() + 3.5f);
        }
        else
        {
            climbing = false;
            moved = Walk(move * Time.deltaTime);
            if (!climbing) Fall();
        }
        if (!moved && !chasing) wanderTimer = 0f;   // bumped into something - go somewhere else

        Animate(walking && moved);
        Flash();
        Groan(target);
        if (!FireProof) Burn();

        // Fell out of the world?
        if (transform.position.y < -10f) Destroy(gameObject);
    }

    // Every so often, a groan (or a rattle, or a hiss...) so you can hear them coming
    private float groanTimer = 3f;
    void Groan(PlayerHealth target)
    {
        groanTimer -= Time.deltaTime;
        if (groanTimer > 0f) return;
        groanTimer = Random.Range(4f, 9f);
        if (target != null && Vector3.Distance(target.transform.position, transform.position) < 24f)
            Sounds.Monster(Called, transform.position);
    }

    // Is this cell something it can't go through?
    protected bool Solid(int x, int y, int z)
    {
        byte type = world.GetBlock(x, y, z);
        return type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Lava &&
               type != BlockWorld.Fire && type != BlockWorld.Cable && type != BlockWorld.PressurePlate &&
               !BlockWorld.IsWheat(type) && !BlockWorld.IsRail(type) && !(BlockWorld.IsDoor(type) && BlockWorld.DoorIsOpen(type));
    }

    protected bool OnGround { get { return fallSpeed == 0f && !climbing; } }

    // Steps along, climbing up one block if there's a step in the way.
    // Spiders climb straight up walls instead. False if it got stuck.
    bool Walk(Vector3 by)
    {
        if (by.sqrMagnitude < 0.000001f) return true;

        Vector3 next = transform.position + by;
        Vector3 ahead = next + by.normalized * 0.3f;
        int x = Mathf.RoundToInt(ahead.x), z = Mathf.RoundToInt(ahead.z);
        int feet = Mathf.FloorToInt(transform.position.y + 0.5f);

        bool wall = Solid(x, feet + 1, z);
        bool stepUp = !wall && Solid(x, feet, z);

        if (wall || (stepUp && (Solid(x, feet + 2, z) || fallSpeed != 0f)))
        {
            if (!ClimbsWalls || Solid(Mathf.RoundToInt(transform.position.x), feet + 2, Mathf.RoundToInt(transform.position.z)))
                return false;

            // Up the wall it goes
            transform.position += Vector3.up * 3f * Time.deltaTime;
            fallSpeed = 0f;
            climbing = true;
            return true;
        }

        if (stepUp) next.y = feet + 0.5f;
        transform.position = next;
        return true;
    }

    // Standing on something, or falling (feet sit at the top of a block: y + 0.5)
    void Fall()
    {
        Vector3 p = transform.position;
        int x = Mathf.RoundToInt(p.x), z = Mathf.RoundToInt(p.z);
        int below = Mathf.FloorToInt(p.y + 0.5f) - 1;

        if (fallSpeed <= 0f && Solid(x, below, z) && p.y <= below + 0.51f)
        {
            p.y = below + 0.5f;
            fallSpeed = 0f;
            transform.position = p;
            return;
        }

        fallSpeed -= Gravity * Time.deltaTime;
        float newY = p.y + fallSpeed * Time.deltaTime;
        int feetCell = Mathf.FloorToInt(newY + 0.5f);

        if (fallSpeed < 0f && Solid(x, feetCell, z))
        {
            newY = feetCell + 0.5f;   // landed on top of it
            fallSpeed = 0f;
        }
        else if (fallSpeed > 0f && Solid(x, feetCell + 2, z))
        {
            fallSpeed = 0f;           // bumped its head
        }

        p.y = newY;
        transform.position = p;
    }

    // Flying: along, and drifting up or down towards the height it wants
    bool Fly(Vector3 by, float wantHeight)
    {
        Vector3 p = transform.position;
        float climb = Mathf.Clamp(wantHeight - p.y, -1f, 1f) * 2.5f * Time.deltaTime;
        Vector3 next = p + by + Vector3.up * climb;

        Vector3Int cell = Vector3Int.RoundToInt(next + Vector3.up * 0.4f);
        if (Solid(cell.x, cell.y, cell.z))
        {
            transform.position = p + Vector3.up * 2.5f * Time.deltaTime;   // something in the way - go over it
            return false;
        }
        transform.position = next;
        return true;
    }

    // The height of the ground under it
    float GroundBelow()
    {
        Vector3 p = transform.position;
        int x = Mathf.RoundToInt(p.x), z = Mathf.RoundToInt(p.z);
        for (int y = Mathf.RoundToInt(p.y); y > 0; y--)
            if (Solid(x, y, z)) return y + 0.5f;
        return p.y - 3f;
    }

    // Can it see you from here? (No walls in between)
    protected bool CanSee(Vector3 from, Vector3 to)
    {
        float length = Vector3.Distance(from, to);
        for (float d = 0.5f; d < length - 0.5f; d += 0.25f)
        {
            Vector3Int cell = Vector3Int.RoundToInt(Vector3.Lerp(from, to, d / length));
            byte type = world.GetBlock(cell.x, cell.y, cell.z);
            if (type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Leaves && type != BlockWorld.Glass &&
                type != BlockWorld.Fire && type != BlockWorld.Lava)
                return false;
        }
        return true;
    }

    // Blinks for a moment when hit
    void Flash()
    {
        bool flash = Time.time < flashUntil;
        foreach (Renderer part in parts)
            if (part != null) part.enabled = !flash || Mathf.Repeat(Time.time * 20f, 1f) < 0.5f;
    }

    // Lava and fire hurt
    void Burn()
    {
        Vector3Int cell = Vector3Int.RoundToInt(transform.position + Vector3.up * 0.3f);
        byte type = world.GetBlock(cell.x, cell.y, cell.z);
        if (type != BlockWorld.Lava && type != BlockWorld.Fire) return;

        burnTimer -= Time.deltaTime;
        if (burnTimer <= 0f)
        {
            burnTimer = 0.5f;
            Hit(type == BlockWorld.Lava ? 4 : 2, transform.position + transform.forward);
        }
    }

    // --- Getting hit ---

    public void Hit(int damage, Vector3 from)
    {
        if (health <= 0) return;   // already on its way out
        health -= damage;
        panicUntil = Time.time + 3f;   // (animals run off)
        panicFrom = from;
        flashUntil = Time.time + 0.3f;

        Vector3 away = transform.position - from;
        away.y = 0f;
        knock = (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * 7f;
        if (OnGround && !Flies) fallSpeed = 4f;   // a little hop backwards

        if (health <= 0) Defeat();
    }

    void Defeat()
    {
        // A puff of bits the colours of its body, then gone
        for (int i = 0; i < 14 && parts.Length > 0; i++)
        {
            GameObject bit = new GameObject("Monster Bit");
            bit.transform.position = transform.position + Vector3.up * AimHeight;
            bit.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);
            bit.transform.rotation = Random.rotation;
            bit.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            bit.AddComponent<MeshRenderer>().sharedMaterial = parts[i % parts.Length].sharedMaterial;

            Rigidbody flight = bit.AddComponent<Rigidbody>();
            flight.linearVelocity = Random.insideUnitSphere * 4f + Vector3.up * 3f;
            flight.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(bit, 1.2f);
        }

        if (Hostile) Achievements.Unlock("monster");
        DropLoot();
        OnDefeated();
        Destroy(gameObject);
    }

    // --- Building bodies out of boxes ---

    protected static Transform Pivot(Transform parent, Vector3 at)
    {
        Transform pivot = new GameObject("Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = at;
        return pivot;
    }

    protected Transform AddPart(Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part.transform;
    }

    protected Transform Model(float scale)
    {
        Transform model = new GameObject("Model").transform;
        model.SetParent(transform, false);
        model.localScale = Vector3.one * scale;
        return model;
    }
}

// Keeps a few monsters around, appearing out of sight a little way from you
// and vanishing again if you get far away from them
public class MonsterSpawner : MonoBehaviour
{
    const float SpawnSeconds = 6f;
    const float TooFar = 60f;

    public BlockWorld world;
    private float timer = 15f;   // a little peace when you first arrive

    static bool InNether { get { return WorldMenu.CurrentPlace == WorldSaves.Nether; } }

    void Update()
    {
        PlayerHealth player = PlayerHealth.Instance;
        if (player == null || world == null) return;
        Vector3 me = player.transform.position;

        int hostile = 0;
        foreach (Monster monster in Monster.All.ToArray())
        {
            if (!monster.Hostile) continue;   // animals stay put
            if (Vector3.Distance(monster.transform.position, me) > TooFar) Destroy(monster.gameObject);
            else hostile++;
        }

        RunCages(me);

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = SpawnSeconds;
        // Only a few in the daytime - lots more at night!
        int most = InNether ? 4 : DayNight.IsNight ? 10 : 3;
        if (hostile >= most) return;

        // Somewhere 20 to 40 blocks away, standing on solid ground
        for (int tries = 0; tries < 20; tries++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(20f, 40f);
            int x = Mathf.RoundToInt(me.x + Mathf.Cos(angle) * distance);
            int z = Mathf.RoundToInt(me.z + Mathf.Sin(angle) * distance);
            if (x < 0 || z < 0 || x >= BlockWorld.SizeX || z >= BlockWorld.SizeZ) continue;

            Vector3 feet;
            if (!GroundAt(x, z, out feet)) continue;

            if (InNether)
            {
                Monster.Spawn<FireImp>(world, feet + Vector3.up * 3f);
            }
            else
            {
                // Each kind is just as likely as the others
                int pick = Random.Range(0, 4);
                if (pick == 0) Monster.Spawn<Zombie>(world, feet);
                else if (pick == 1) Monster.Spawn<Skeleton>(world, feet);
                else if (pick == 2) Monster.Spawn<Spider>(world, feet);
                else Slime.Make(world, feet, Random.Range(2, 4));
            }
            return;
        }
    }

    // Monster cages (in dungeons) keep letting monsters out while you're near
    private float cageTimer = 3f;
    void RunCages(Vector3 me)
    {
        cageTimer -= Time.deltaTime;
        if (cageTimer > 0f) return;
        cageTimer = 6f;

        foreach (Vector3Int cage in world.Spawners)
        {
            if (Vector3.Distance(cage, me) > 14f) continue;

            int near = 0;
            foreach (Monster monster in Monster.All)
                if (monster.Hostile && Vector3.Distance(monster.transform.position, cage) < 9f) near++;
            if (near >= 4) continue;

            // Out it pops, next to the cage
            for (int tries = 0; tries < 8; tries++)
            {
                Vector3Int spot = cage + new Vector3Int(Random.Range(-2, 3), 0, Random.Range(-2, 3));
                if (world.GetBlock(spot.x, spot.y, spot.z) != BlockWorld.Air || world.GetBlock(spot.x, spot.y + 1, spot.z) != BlockWorld.Air) continue;
                Vector3 feet = new Vector3(spot.x, spot.y - 0.5f, spot.z);
                int pick = Random.Range(0, 3);
                if (pick == 0) Monster.Spawn<Zombie>(world, feet);
                else if (pick == 1) Monster.Spawn<Skeleton>(world, feet);
                else Monster.Spawn<Spider>(world, feet);
                break;
            }
        }
    }

    // The top of the ground here, if it's dry land with room to stand
    bool GroundAt(int x, int z, out Vector3 feet)
    {
        feet = Vector3.zero;
        for (int y = BlockWorld.SizeY - 3; y >= 1; y--)
        {
            byte type = world.GetBlock(x, y, z);
            if (type == BlockWorld.Air) continue;

            bool ground = InNether
                ? type == BlockWorld.Ash || type == BlockWorld.EmberRock
                : type == BlockWorld.Grass || type == BlockWorld.Dirt || type == BlockWorld.Sand ||
                  type == BlockWorld.Stone || type == BlockWorld.Snow;
            if (!ground) return false;   // water, leaves, lava...
            if (world.GetBlock(x, y + 1, z) != BlockWorld.Air || world.GetBlock(x, y + 2, z) != BlockWorld.Air) return false;

            feet = new Vector3(x, y + 0.5f, z);
            return true;
        }
        return false;
    }
}

// An arrow from a skeleton, or a fireball from a fire imp
public class MonsterShot : MonoBehaviour
{
    enum Kind { Arrow, Fireball, Boulder, PlayerArrow }

    private Vector3 velocity;
    private float gravity;
    private int damage;
    private string how;
    private float dies;
    private Kind kind;

    private static Material arrowLook, fireLook, rockLook;

    // An arrow from a skeleton (fireball false) or a fireball from a fire imp
    public static void Fire(Vector3 from, Vector3 velocity, bool fireball, int damage, string how)
    {
        Launch(fireball ? Kind.Fireball : Kind.Arrow, from, velocity, damage, how);
    }

    // A big rock thrown by the Stone Golem
    public static void Boulder(Vector3 from, Vector3 velocity)
    {
        Launch(Kind.Boulder, from, velocity, 5, "The Stone Golem's boulder got you!");
    }

    // An arrow you shot from your bow - it hits monsters and animals, not you
    public static void PlayerArrow(Vector3 from, Vector3 velocity)
    {
        Launch(Kind.PlayerArrow, from, velocity, 6, "");
    }

    static void Launch(Kind kind, Vector3 from, Vector3 velocity, int damage, string how)
    {
        if (arrowLook == null)
        {
            arrowLook = BlockWorld.MakeMaterial(new Color(0.55f, 0.40f, 0.22f));
            fireLook = BlockWorld.MakeMaterial(new Color(1.00f, 0.55f, 0.10f));
            rockLook = BlockWorld.Instance.MaterialFor(BlockWorld.Cobblestone);
        }
        bool fireball = kind == Kind.Fireball, rock = kind == Kind.Boulder;

        GameObject shot = new GameObject(kind.ToString());
        shot.transform.position = from;
        shot.transform.localScale = fireball ? Vector3.one * 0.35f : rock ? Vector3.one * 0.7f : new Vector3(0.05f, 0.05f, 0.6f);
        shot.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        shot.AddComponent<MeshRenderer>().sharedMaterial = fireball ? fireLook : rock ? rockLook : arrowLook;

        if (fireball)
        {
            Light glow = shot.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.2f);
            glow.range = 5f;
            glow.intensity = 2f;
        }

        MonsterShot flying = shot.AddComponent<MonsterShot>();
        flying.kind = kind;
        flying.velocity = velocity;
        flying.gravity = fireball ? 0f : 9f;
        flying.damage = damage;
        flying.how = how;
        flying.dies = Time.time + 5f;
    }

    void Update()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        velocity.y -= gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        if (kind == Kind.Fireball || kind == Kind.Boulder) transform.Rotate(300f * Time.deltaTime, 0f, 400f * Time.deltaTime);   // they tumble

        // Your arrows hit monsters and animals
        if (kind == Kind.PlayerArrow)
        {
            foreach (Monster target in Monster.All)
            {
                if (Vector3.Distance(transform.position, target.transform.position + Vector3.up * target.AimHeight) > target.HitSize + 0.3f) continue;
                target.Hit(damage, transform.position - velocity);
                Sounds.Thwack(transform.position);
                if (target.Hostile) Achievements.Unlock("bow");
                Destroy(gameObject);
                return;
            }
        }

        // Hit you?
        PlayerHealth player = PlayerHealth.Instance;
        if (kind != Kind.PlayerArrow && player != null &&
            Vector3.Distance(transform.position, player.transform.position + Vector3.up * 0.9f) < (kind == Kind.Boulder ? 1.1f : 0.8f))
        {
            player.Hurt(damage, how);
            Destroy(gameObject);
            return;
        }

        // Hit a wall, or flown off too far?
        Vector3Int cell = Vector3Int.RoundToInt(transform.position);
        byte type = BlockWorld.Instance != null ? BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z) : BlockWorld.Air;
        bool solid = type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Fire && type != BlockWorld.Lava;
        if (solid || Time.time > dies) Destroy(gameObject);
    }

    // The speed and direction to throw something so it lands on a target
    // (aiming a bit high for arrows, which drop as they fly)
    public static Vector3 Aim(Vector3 from, Vector3 to, float speed, bool drops)
    {
        Vector3 line = to - from;
        Vector3 v = line.normalized * speed;
        if (drops) v.y += 0.5f * 9f * (line.magnitude / speed);
        return v;
    }
}
