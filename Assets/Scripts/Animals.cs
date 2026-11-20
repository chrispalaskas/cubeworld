using System.Collections.Generic;
using UnityEngine;

// Farm animals and pets, in the home world (creative and survival).
//
//   Cow:    moo! Gives raw beef and leather (in survival).
//   Sheep:  press E to shear it for wool - white, or sometimes another
//           colour. Its wool grows back after a minute.
//   Pig:    oink! Gives raw pork.
//   Dog:    feed it meat (press E while holding raw beef, pork or chicken)
//           and it's your pet: it follows you everywhere and fights monsters.
//   Cat:    feed it fish and it's your pet too. It follows you around.
// Press E on your pet to make it sit and wait, and again to make it follow.
// Your pets are remembered, and are waiting by you next time you play.
//
// Fish swim about in the sea - catch them with a fishing rod (Gear.cs).
// (All the walking, falling and getting hit is shared with monsters - Monster.cs.)

// Something with four legs - all the animals here are built like this
public abstract class FourLegs : Monster
{
    protected Transform[] legs = new Transform[4];
    protected Transform headPivot;

    public override bool Hostile { get { return false; } }
    protected override float WalkSpeed { get { return 1.0f; } }
    protected override float ChaseSpeed { get { return 2.6f; } }
    public override float AimHeight { get { return 0.5f; } }
    public override float HitSize { get { return 0.7f; } }

    protected Transform BuildFourLegs(float scale, Vector3 bodySize, Material body, Vector3 headSize, Material head, float legLength, Material leg)
    {
        Transform model = Model(scale);
        float bodyY = legLength + bodySize.y / 2f;
        AddPart(model, new Vector3(0f, bodyY, 0f), bodySize, body);

        headPivot = Pivot(model, new Vector3(0f, bodyY + bodySize.y * 0.2f, bodySize.z / 2f));
        AddPart(headPivot, new Vector3(0f, 0f, headSize.z / 2f), headSize, head);

        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f, end = i < 2 ? 1f : -1f;
            legs[i] = Pivot(model, new Vector3(side * (bodySize.x / 2f - 0.06f), legLength, end * (bodySize.z / 2f - 0.08f)));
            AddPart(legs[i], new Vector3(0f, -legLength / 2f, 0f), new Vector3(0.1f, legLength, 0.1f), leg);
        }
        return model;
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 8f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 30f : 0f;
        for (int i = 0; i < 4; i++)
            legs[i].localRotation = Quaternion.Euler((i == 0 || i == 3) ? swing : -swing, 0f, 0f);
        if (headPivot != null && !walking)
            headPivot.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.7f + GetInstanceID()) * 15f + 10f, 0f, 0f);   // grazing
    }

    private static Material black;
    protected static Material Black { get { if (black == null) black = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.05f)); return black; } }

    // Little red hearts floating up
    protected void Hearts()
    {
        Material red = BlockWorld.MakeMaterial(new Color(0.95f, 0.2f, 0.3f));
        for (int i = 0; i < 6; i++)
        {
            GameObject heart = new GameObject("Heart");
            heart.transform.position = transform.position + Vector3.up * 1f + Random.insideUnitSphere * 0.3f;
            heart.transform.localScale = Vector3.one * 0.12f;
            heart.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            heart.AddComponent<MeshRenderer>().sharedMaterial = red;
            heart.AddComponent<FloatUp>();
            Destroy(heart, 1.5f);
        }
        Destroy(red, 1.6f);
    }
}

public class Cow : FourLegs
{
    private static Material brown, white, pink, horn;
    protected override string Called { get { return "cow"; } }
    protected override int MostHealth { get { return 10; } }

    protected override void BuildBody()
    {
        if (brown == null)
        {
            brown = BlockWorld.MakeMaterial(new Color(0.35f, 0.24f, 0.16f));
            white = BlockWorld.MakeMaterial(new Color(0.92f, 0.92f, 0.90f));
            pink = BlockWorld.MakeMaterial(new Color(0.90f, 0.62f, 0.62f));
            horn = BlockWorld.MakeMaterial(new Color(0.85f, 0.82f, 0.70f));
        }
        Transform model = BuildFourLegs(1f, new Vector3(0.55f, 0.45f, 0.9f), brown, new Vector3(0.36f, 0.34f, 0.32f), brown, 0.45f, brown);
        AddPart(model, new Vector3(0.18f, 0.80f, 0.1f), new Vector3(0.22f, 0.25f, 0.3f), white);   // spots
        AddPart(model, new Vector3(-0.2f, 0.72f, -0.2f), new Vector3(0.18f, 0.2f, 0.25f), white);
        AddPart(headPivot, new Vector3(0f, -0.08f, 0.33f), new Vector3(0.28f, 0.14f, 0.04f), pink);   // nose
        AddPart(headPivot, new Vector3(-0.2f, 0.17f, 0.12f), new Vector3(0.05f, 0.1f, 0.05f), horn);
        AddPart(headPivot, new Vector3(0.2f, 0.17f, 0.12f), new Vector3(0.05f, 0.1f, 0.05f), horn);
        AddPart(headPivot, new Vector3(-0.09f, 0.05f, 0.33f), new Vector3(0.05f, 0.05f, 0.01f), Black);
        AddPart(headPivot, new Vector3(0.09f, 0.05f, 0.33f), new Vector3(0.05f, 0.05f, 0.01f), Black);
    }

    protected override void DropLoot()
    {
        Drop(BlockWorld.RawBeef, Random.Range(1, 3));
        Drop(BlockWorld.Leather, Random.Range(0, 3));
    }

    public override bool Interact(byte holding)
    {
        PortalTraveller.Announce("Moooo!");
        return true;
    }
}

public class Pig : FourLegs
{
    private static Material pink, snout;
    protected override string Called { get { return "pig"; } }
    protected override int MostHealth { get { return 8; } }

    protected override void BuildBody()
    {
        if (pink == null)
        {
            pink = BlockWorld.MakeMaterial(new Color(0.95f, 0.66f, 0.70f));
            snout = BlockWorld.MakeMaterial(new Color(0.88f, 0.50f, 0.55f));
        }
        BuildFourLegs(1f, new Vector3(0.5f, 0.4f, 0.75f), pink, new Vector3(0.38f, 0.36f, 0.32f), pink, 0.22f, pink);
        AddPart(headPivot, new Vector3(0f, -0.05f, 0.34f), new Vector3(0.18f, 0.12f, 0.06f), snout);
        AddPart(headPivot, new Vector3(-0.1f, 0.06f, 0.325f), new Vector3(0.05f, 0.05f, 0.01f), Black);
        AddPart(headPivot, new Vector3(0.1f, 0.06f, 0.325f), new Vector3(0.05f, 0.05f, 0.01f), Black);
    }

    protected override void DropLoot() { Drop(BlockWorld.RawPork, Random.Range(1, 3)); }

    public override bool Interact(byte holding)
    {
        PortalTraveller.Announce("Oink oink!");
        return true;
    }
}

public class Sheep : FourLegs
{
    private int colour;               // which wool colour (mostly white)
    private float woolBackAt = -1f;   // when its wool grows back after shearing (-1: it has its wool)
    private readonly List<Renderer> woolParts = new List<Renderer>();
    private static Material face, skin;

    protected override string Called { get { return "sheep"; } }
    protected override int MostHealth { get { return 8; } }

    protected override void BuildBody()
    {
        if (face == null)
        {
            face = BlockWorld.MakeMaterial(new Color(0.85f, 0.78f, 0.70f));
            skin = BlockWorld.MakeMaterial(new Color(0.80f, 0.72f, 0.65f));
        }
        float roll = Random.value;
        colour = roll < 0.6f ? 0 : Random.Range(1, BlockWorld.WoolColours);

        Material wool = world.MaterialFor((byte)(BlockWorld.WoolFirst + colour));
        BuildFourLegs(1f, new Vector3(0.45f, 0.35f, 0.7f), skin, new Vector3(0.28f, 0.3f, 0.28f), face, 0.4f, face);

        // Thick fluffy wool all round its body
        Transform model = transform.GetChild(0);
        woolParts.Add(AddPart(model, new Vector3(0f, 0.6f, 0f), new Vector3(0.62f, 0.5f, 0.86f), wool).GetComponent<Renderer>());
        woolParts.Add(AddPart(headPivot, new Vector3(0f, 0.12f, 0.1f), new Vector3(0.32f, 0.12f, 0.24f), wool).GetComponent<Renderer>());
    }

    void LateUpdate()
    {
        bool hasWool = woolBackAt < 0f || Time.time > woolBackAt;
        if (woolBackAt > 0f && Time.time > woolBackAt) woolBackAt = -1f;
        foreach (Renderer part in woolParts) part.enabled = hasWool;
    }

    // Shear it!
    public override bool Interact(byte holding)
    {
        if (woolBackAt > 0f)
        {
            PortalTraveller.Announce("Baa! Its wool hasn't grown back yet.");
            return true;
        }
        woolBackAt = Time.time + 60f;
        Drop((byte)(BlockWorld.WoolFirst + colour), Random.Range(1, 4));
        Sounds.Whoosh(transform.position);
        PortalTraveller.Announce(WorldMenu.Survival ? "Snip snip! You got some " + BlockWorld.WoolNames[colour].ToLower() + " wool." : "Snip snip! Baa!");
        return true;
    }

    protected override void DropLoot()
    {
        if (woolBackAt < 0f) Drop((byte)(BlockWorld.WoolFirst + colour), 1);
    }
}

// Dogs and cats: wild until you feed them, then your pet
public abstract class Pet : FourLegs
{
    public bool Tamed { get; set; }
    private bool sitting;
    private float biteTimer;

    protected abstract bool LikesToEat(byte food);
    protected abstract string Kind { get; }         // "dog" or "cat"
    protected virtual bool Fights { get { return false; } }

    protected override int MostHealth { get { return 12; } }
    protected override float ChaseSpeed { get { return 3.6f; } }

    public override bool Interact(byte holding)
    {
        if (!Tamed)
        {
            if (!LikesToEat(holding) || BlockInteractor.Me == null || !BlockInteractor.Me.TakeOne(holding))
            {
                PortalTraveller.Announce(Kind == "dog" ? "Woof? (Dogs love meat!)" : "Meow? (Cats love fish!)");
                return true;
            }
            Tamed = true;
            Hearts();
            Pets.Add(Kind);
            Achievements.Unlock("pet");
            PortalTraveller.Announce(Kind == "dog" ? "Woof woof! The dog is your pet now!" : "Purrr... The cat is your pet now!");
            return true;
        }

        sitting = !sitting;
        PortalTraveller.Announce(sitting ? "Your " + Kind + " sits and waits." : "Your " + Kind + " follows you again.");
        return true;
    }

    protected override bool AnimalGoal(ref Vector3 goal, ref float speed)
    {
        if (!Tamed) return base.AnimalGoal(ref goal, ref speed);
        Transform player = Player;
        if (player == null) return false;

        if (sitting)
        {
            goal = transform.position;
            return true;
        }

        // Dogs go after monsters that come near you
        if (Fights)
        {
            Monster enemy = null;
            float best = 10f;
            foreach (Monster monster in Monster.All)
            {
                if (!monster.Hostile) continue;
                float d = Vector3.Distance(monster.transform.position, transform.position);
                if (d < best) { best = d; enemy = monster; }
            }
            if (enemy != null)
            {
                goal = enemy.transform.position;
                speed = ChaseSpeed;
                biteTimer -= Time.deltaTime;
                if (best < 1.5f && biteTimer <= 0f)
                {
                    biteTimer = 1f;
                    enemy.Hit(4, transform.position);
                    Sounds.Thwack(enemy.transform.position);
                }
                return true;
            }
        }

        // Follow you about - and if you get really far away, catch up
        float far = Vector3.Distance(player.position, transform.position);
        if (far > 24f) transform.position = player.position - player.forward * 2f + Vector3.up * 0.5f;
        if (far > 3f)
        {
            goal = player.position;
            speed = ChaseSpeed;
            return true;
        }
        goal = transform.position;
        return true;
    }

    protected override void OnDefeated()
    {
        if (Tamed) Pets.Remove(Kind);
    }
}

public class Dog : Pet
{
    private static Material fur, dark;
    protected override string Called { get { return "dog"; } }
    protected override string Kind { get { return "dog"; } }
    protected override bool Fights { get { return true; } }

    protected override bool LikesToEat(byte food)
    {
        return food == BlockWorld.RawBeef || food == BlockWorld.RawPork || food == BlockWorld.RawChicken ||
               food == BlockWorld.Steak || food == BlockWorld.CookedPork || food == BlockWorld.CookedChicken;
    }

    protected override void BuildBody()
    {
        if (fur == null)
        {
            fur = BlockWorld.MakeMaterial(new Color(0.72f, 0.52f, 0.30f));
            dark = BlockWorld.MakeMaterial(new Color(0.30f, 0.20f, 0.12f));
        }
        Transform model = BuildFourLegs(0.85f, new Vector3(0.32f, 0.3f, 0.65f), fur, new Vector3(0.3f, 0.28f, 0.3f), fur, 0.32f, fur);
        AddPart(headPivot, new Vector3(0f, -0.06f, 0.36f), new Vector3(0.16f, 0.14f, 0.14f), fur);     // nose
        AddPart(headPivot, new Vector3(0f, -0.02f, 0.44f), new Vector3(0.06f, 0.05f, 0.03f), dark);
        AddPart(headPivot, new Vector3(-0.13f, 0.17f, 0.08f), new Vector3(0.06f, 0.12f, 0.08f), dark);  // ears
        AddPart(headPivot, new Vector3(0.13f, 0.17f, 0.08f), new Vector3(0.06f, 0.12f, 0.08f), dark);
        AddPart(headPivot, new Vector3(-0.08f, 0.06f, 0.301f), new Vector3(0.05f, 0.05f, 0.01f), dark);
        AddPart(headPivot, new Vector3(0.08f, 0.06f, 0.301f), new Vector3(0.05f, 0.05f, 0.01f), dark);
        AddPart(model, new Vector3(0f, 0.6f, -0.38f), new Vector3(0.06f, 0.06f, 0.25f), fur);          // tail
    }
}

public class Cat : Pet
{
    private static Material fur, stripe, eye;
    protected override string Called { get { return "cat"; } }
    protected override string Kind { get { return "cat"; } }
    protected override int MostHealth { get { return 8; } }

    protected override bool LikesToEat(byte food) { return food == BlockWorld.RawFish || food == BlockWorld.CookedFish; }

    protected override void BuildBody()
    {
        if (fur == null)
        {
            fur = BlockWorld.MakeMaterial(new Color(0.95f, 0.60f, 0.25f));
            stripe = BlockWorld.MakeMaterial(new Color(0.75f, 0.40f, 0.12f));
            eye = BlockWorld.MakeMaterial(new Color(0.40f, 0.85f, 0.30f));
        }
        Transform model = BuildFourLegs(0.7f, new Vector3(0.28f, 0.26f, 0.6f), fur, new Vector3(0.28f, 0.26f, 0.24f), fur, 0.28f, fur);
        for (int i = 0; i < 3; i++)
            AddPart(model, new Vector3(0f, 0.55f, 0.15f - i * 0.15f), new Vector3(0.3f, 0.04f, 0.05f), stripe);
        AddPart(headPivot, new Vector3(-0.09f, 0.17f, 0.08f), new Vector3(0.07f, 0.09f, 0.04f), fur);   // pointy ears
        AddPart(headPivot, new Vector3(0.09f, 0.17f, 0.08f), new Vector3(0.07f, 0.09f, 0.04f), fur);
        AddPart(headPivot, new Vector3(-0.07f, 0.04f, 0.241f), new Vector3(0.05f, 0.04f, 0.01f), eye);
        AddPart(headPivot, new Vector3(0.07f, 0.04f, 0.241f), new Vector3(0.05f, 0.04f, 0.01f), eye);
        AddPart(model, new Vector3(0f, 0.75f, -0.4f), new Vector3(0.05f, 0.35f, 0.05f), stripe);        // tail, held up high
    }
}

// Remembers which pets you have in each world
public static class Pets
{
    static string Key { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-pets"; } }

    public static List<string> Load()
    {
        List<string> pets = new List<string>();
        foreach (string pet in PlayerPrefs.GetString(Key, "").Split(','))
            if (pet != "") pets.Add(pet);
        return pets;
    }

    public static void Add(string kind)
    {
        List<string> pets = Load();
        pets.Add(kind);
        PlayerPrefs.SetString(Key, string.Join(",", pets));
    }

    public static void Remove(string kind)
    {
        List<string> pets = Load();
        pets.Remove(kind);
        PlayerPrefs.SetString(Key, string.Join(",", pets));
    }
}

// Keeps the home world stocked with animals, and brings your pets along
public class AnimalHerd : MonoBehaviour
{
    private BlockWorld world;
    private float timer = 30f;

    public static void Begin(BlockWorld world, Vector3 player)
    {
        AnimalHerd herd = new GameObject("Animals").AddComponent<AnimalHerd>();
        herd.world = world;

        // Your pets are right there with you
        foreach (string pet in Pets.Load())
        {
            Vector3 spot = player + new Vector3(Random.Range(-2f, 2f), 0.5f, Random.Range(-2f, 2f));
            Pet made = pet == "dog" ? (Pet)Monster.Spawn<Dog>(world, spot) : Monster.Spawn<Cat>(world, spot);
            made.Tamed = true;
        }
        herd.FillUp();

        FishSchool.Begin(world);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 30f;
        FillUp();
    }

    void FillUp()
    {
        int cows = 0, sheep = 0, pigs = 0, dogs = 0, cats = 0;
        foreach (Monster animal in Monster.All)
        {
            if (animal is Cow) cows++;
            else if (animal is Sheep) sheep++;
            else if (animal is Pig) pigs++;
            else if (animal is Dog && !((Pet)animal).Tamed) dogs++;
            else if (animal is Cat && !((Pet)animal).Tamed) cats++;
        }
        for (; cows < 5; cows++) Place<Cow>();
        for (; sheep < 5; sheep++) Place<Sheep>();
        for (; pigs < 4; pigs++) Place<Pig>();
        for (; dogs < 2; dogs++) Place<Dog>();
        for (; cats < 2; cats++) Place<Cat>();
    }

    // Somewhere grassy
    void Place<T>() where T : Monster
    {
        for (int tries = 0; tries < 30; tries++)
        {
            int x = Random.Range(4, BlockWorld.SizeX - 4), z = Random.Range(4, BlockWorld.SizeZ - 4);
            int y = world.GetSurfaceHeight(x, z);
            if (y < 0 || world.GetBlock(x, y, z) != BlockWorld.Grass) continue;
            Monster.Spawn<T>(world, new Vector3(x, y + 0.5f, z));
            return;
        }
    }
}

// Fish swimming about in the water near you
public class FishSchool : MonoBehaviour
{
    private BlockWorld world;
    private float timer;
    private static Material[] colours;

    public static void Begin(BlockWorld world)
    {
        new GameObject("Fish").AddComponent<FishSchool>().world = world;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || BlockInteractor.Me == null) return;
        timer = 3f;

        Vector3 me = BlockInteractor.Me.transform.position;
        foreach (Transform fish in transform)
            if (Vector3.Distance(fish.position, me) > 40f) Destroy(fish.gameObject);
        if (transform.childCount >= 12) return;

        if (colours == null)
            colours = new[] { BlockWorld.MakeMaterial(new Color(1f, 0.55f, 0.15f)), BlockWorld.MakeMaterial(new Color(0.3f, 0.6f, 0.95f)),
                              BlockWorld.MakeMaterial(new Color(0.95f, 0.9f, 0.3f)), BlockWorld.MakeMaterial(new Color(0.75f, 0.75f, 0.8f)) };

        for (int tries = 0; tries < 10; tries++)
        {
            Vector3Int spot = Vector3Int.RoundToInt(me + new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-25f, 25f)));
            spot.y = BlockWorld.SeaLevel - 1;
            if (world.GetBlock(spot.x, spot.y, spot.z) != BlockWorld.Water) continue;

            GameObject fish = new GameObject("A Fish");
            fish.transform.SetParent(transform, false);
            fish.transform.position = spot;
            Material look = colours[Random.Range(0, colours.Length)];
            AddBit(fish, Vector3.zero, new Vector3(0.12f, 0.18f, 0.3f), look);
            AddBit(fish, new Vector3(0f, 0f, -0.2f), new Vector3(0.03f, 0.16f, 0.1f), look);   // tail
            fish.AddComponent<SwimmingFish>().world = world;
            return;
        }
    }

    void AddBit(GameObject fish, Vector3 at, Vector3 size, Material look)
    {
        GameObject bit = new GameObject("Bit");
        bit.transform.SetParent(fish.transform, false);
        bit.transform.localPosition = at;
        bit.transform.localScale = size;
        bit.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        bit.AddComponent<MeshRenderer>().sharedMaterial = look;
    }
}

public class SwimmingFish : MonoBehaviour
{
    public BlockWorld world;
    private Vector3 heading;
    private float turnTimer;

    void Update()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        turnTimer -= Time.deltaTime;
        if (turnTimer <= 0f || heading == Vector3.zero)
        {
            turnTimer = Random.Range(1.5f, 4f);
            heading = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.2f, 0.2f), Random.Range(-1f, 1f)).normalized;
        }

        Vector3 next = transform.position + heading * 1.2f * Time.deltaTime;
        Vector3Int cell = Vector3Int.RoundToInt(next + heading * 0.3f);
        if (world.GetBlock(cell.x, cell.y, cell.z) != BlockWorld.Water)
        {
            heading = -heading;   // bumped the edge of the water - turn round
            return;
        }
        transform.position = next;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 4f * Time.deltaTime);
    }
}
