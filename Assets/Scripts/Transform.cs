using UnityEngine;

// The Transform mod (turn it on in the Mods screen - press O). Then press T
// to turn into a creature, and T again for the next one. Each has a power:
//
//   Zombie:      monsters think you're one of them, and leave you alone
//   Spider:      walk into a wall and you climb right up it
//   Slime:       boing! You bounce everywhere, and jump super high
//   Owl:         fly! Hold Space to go up, let go to glide
//   Iron Golem:  huge and super strong - your punches hit four times as hard
//                (but you're a bit slower)
//   Penguin:     a super fast swimmer
//   Mimic:       stand still and you look just like a block - nothing can see you!
//
//   Skeleton:     with nothing in your hand (or arrows), click to shoot arrows - free!
//   Ghost:        float about, and monsters can't see you
//   Fire Imp:     fire and lava can't hurt you
//   Stone Golem:  smash any block in one go
//   Yeti:         water turns to ice under your feet - walk across the sea!
//   Cat:          always land on your feet - falling never hurts
//   Dog:          fetch! Dropped things come flying to you from far away
//   Cow:          munch, munch - you never get hungry
//   Pig:          a happy pig heals all by itself
//   Sheep:        so fluffy that hits only do half as much
//   Parrot:       fly, extra fast
//   Star Guardian: fly, and glow so you light up the dark
//
// Shift+T goes back to the one before.
// Press V to see yourself (it switches by itself the first time). Keep
// pressing T to go all the way round, back to being you.
public partial class PlayerLook
{
    public enum Form { You, Zombie, Spider, Slime, Owl, IronGolem, Penguin, Mimic,
                       Skeleton, Ghost, FireImp, StoneGolem, Yeti, Cat, Dog, Cow, Pig, Sheep, Parrot, StarGuardian }

    static readonly string[] FormNames = { "yourself", "a Zombie", "a Spider", "a Slime", "an Owl", "an Iron Golem", "a Penguin", "a Mimic",
                                           "a Skeleton", "a Ghost", "a Fire Imp", "the Stone Golem", "the Yeti", "a Cat", "a Dog", "a Cow",
                                           "a Pig", "a Sheep", "a Parrot", "a Star Guardian" };
    static readonly string[] FormPowers =
    {
        "",
        "Monsters think you're one of them - they leave you alone!",
        "Walk into a wall to climb straight up it!",
        "Boing! You bounce everywhere, and jump super high.",
        "Hold Space to fly up, let go to glide down.",
        "You're HUGE! Your punches hit four times as hard.",
        "Swim super fast!",
        "Stand still and you look just like a block - nothing can see you!",
        "With nothing in your hand, click to shoot arrows - free!",
        "Float about - and monsters can't see you!",
        "Fire and lava can't hurt you!",
        "SMASH! Any block breaks in one go.",
        "Water turns to ice under your feet - walk across the sea!",
        "You always land on your feet - falling never hurts.",
        "Fetch! Dropped things come flying to you from far away.",
        "Munch munch - you never get hungry.",
        "A happy pig heals all by itself.",
        "So fluffy that hits only do half as much!",
        "Fly - extra fast!",
        "Fly, and glow so you light up the dark!",
    };

    public static Form Current { get; private set; }
    private static bool standingStill;

    private GameObject disguise;

    // --- What each form can do (used by the player, monsters and punches) ---

    public static bool MonstersIgnoreYou { get { return Current == Form.Zombie || Current == Form.Ghost || (Current == Form.Mimic && standingStill); } }
    public static bool FormFlies { get { return Current == Form.Owl || Current == Form.Ghost || Current == Form.Parrot || Current == Form.StarGuardian; } }
    public static bool FormShoots { get { return Current == Form.Skeleton; } }
    public static bool Fireproof { get { return Current == Form.FireImp; } }
    public static bool FormSmashes { get { return Current == Form.StoneGolem; } }
    public static bool SoftLanding { get { return Current == Form.Cat; } }
    public static bool FormFetches { get { return Current == Form.Dog; } }
    public static bool NeverHungry { get { return Current == Form.Cow; } }
    public static bool SelfHealing { get { return Current == Form.Pig; } }
    public static bool Fluffy { get { return Current == Form.Sheep; } }
    public static bool FormClimbs { get { return Current == Form.Spider; } }
    public static bool FormBounces { get { return Current == Form.Slime; } }
    public static float FormJump { get { return Current == Form.Slime ? 1.7f : 1f; } }
    public static float FormSpeed
    {
        get
        {
            switch (Current)
            {
                case Form.IronGolem: case Form.StoneGolem: return 0.8f;
                case Form.Spider: case Form.Cat: case Form.Dog: return 1.25f;
                case Form.Parrot: return 1.6f;
                default: return 1f;
            }
        }
    }
    public static float FormSwimming { get { return Current == Form.Penguin ? 3f : 1f; } }
    public static int FormPunch { get { return Current == Form.IronGolem ? 4 : 1; } }

    void TransformKeys()
    {
        // The mod's been switched off? Back to being you
        if (!Mods.Transform && Current != Form.You) Become(Form.You);

        if (!Input.GetKeyDown(KeyCode.T)) return;
        if (!Mods.Transform)
        {
            PortalTraveller.Announce("Turn on the Transform mod first - press O for the Mods screen.");
            return;
        }
        bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Become((Form)(((int)Current + (back ? FormNames.Length - 1 : 1)) % FormNames.Length));
    }

    void Become(Form form)
    {
        Current = form;
        if (disguise != null) Destroy(disguise);
        disguise = null;

        BlockWorld world = BlockWorld.Instance;
        if (form != Form.You && world != null)
        {
            Monster body;
            switch (form)
            {
                case Form.Zombie:    body = Monster.Spawn<Zombie>(world, transform.position); break;
                case Form.Spider:    body = Monster.Spawn<Spider>(world, transform.position); break;
                case Form.Slime:     body = Slime.Make(world, transform.position, 2); break;
                case Form.Owl:       body = Monster.Spawn<Owl>(world, transform.position); break;
                case Form.IronGolem: body = Monster.Spawn<IronGolem>(world, transform.position); break;
                case Form.Penguin:   body = Monster.Spawn<Penguin>(world, transform.position); break;
                case Form.Skeleton:  body = Monster.Spawn<Skeleton>(world, transform.position); break;
                case Form.Ghost:     body = Monster.Spawn<Ghost>(world, transform.position); break;
                case Form.FireImp:   body = Monster.Spawn<FireImp>(world, transform.position); break;
                case Form.StoneGolem: body = Monster.Spawn<Golem>(world, transform.position); break;
                case Form.Yeti:      body = Monster.Spawn<Yeti>(world, transform.position); break;
                case Form.Cat:       body = Monster.Spawn<Cat>(world, transform.position); break;
                case Form.Dog:       body = Monster.Spawn<Dog>(world, transform.position); break;
                case Form.Cow:       body = Monster.Spawn<Cow>(world, transform.position); break;
                case Form.Pig:       body = Monster.Spawn<Pig>(world, transform.position); break;
                case Form.Sheep:     body = Monster.Spawn<Sheep>(world, transform.position); break;
                case Form.Parrot:    body = Monster.Spawn<Parrot>(world, transform.position); break;
                case Form.StarGuardian: body = Monster.Spawn<StarGuardian>(world, transform.position); break;
                default:             body = Monster.Spawn<Mimic>(world, transform.position); break;
            }
            // Just the body - no brain! It goes wherever you go.
            body.enabled = false;
            disguise = body.gameObject;
            disguise.name = "You (in disguise)";
            disguise.transform.SetParent(transform, false);
            disguise.transform.localPosition = Vector3.zero;
            disguise.transform.localRotation = Quaternion.identity;

            // A Star Guardian glows
            if (form == Form.StarGuardian)
            {
                Light glow = disguise.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.8f, 0.92f, 1f);
                glow.range = 14f;
                glow.intensity = 2f;
            }
        }

        Villager.SparkleBurst(transform.position + Vector3.up, 12);
        Sounds.Whoosh(transform.position);
        if (form == Form.You) PortalTraveller.Announce("You're back to being yourself!");
        else
        {
            PortalTraveller.Announce("You turned into " + FormNames[(int)form] + "! " + FormPowers[(int)form] + "  (T for the next one)");
            if (!thirdPerson) SwitchView();   // so you can see what you look like
        }
        ShowModel(thirdPerson);
    }

    void ShowDisguise(bool show)
    {
        if (disguise == null) return;
        foreach (Renderer part in disguise.GetComponentsInChildren<Renderer>()) part.enabled = show;
    }

    // Every frame: a little waddle, and noticing if you're standing still
    void MoveDisguise(float speed)
    {
        standingStill = speed < 0.2f;
        if (Current == Form.Yeti || Mods.IceWalker) FreezeUnderfoot();
        if (Current == Form.Pig) PigHealing();
        if (disguise == null || disguise.transform.childCount == 0) return;
        Transform body = disguise.transform.GetChild(0);
        if (Current == Form.Mimic)
        {
            // A mimic sits square on the ground, like a block
            disguise.transform.rotation = Quaternion.identity;
            body.localRotation = Quaternion.Euler(0f, 0f, speed > 0.2f ? Mathf.Sin(Time.time * 18f) * 6f : 0f);
            return;
        }
        disguise.transform.localRotation = Quaternion.identity;
        float waddle = speed > 0.3f ? Mathf.Sin(Time.time * 12f) : 0f;
        body.localRotation = Quaternion.Euler(0f, 0f, waddle * 6f);
        body.localPosition = new Vector3(0f, Mathf.Abs(waddle) * 0.06f, 0f);
    }

    // The Yeti: the water all round your feet freezes solid
    void FreezeUnderfoot()
    {
        BlockWorld world = BlockWorld.Instance;
        if (world == null) return;
        Vector3 feet = transform.position;
        int y = Mathf.RoundToInt(feet.y - 0.6f);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int x = Mathf.RoundToInt(feet.x) + dx, z = Mathf.RoundToInt(feet.z) + dz;
                if (world.GetBlock(x, y, z) == BlockWorld.Water && world.GetBlock(x, y + 1, z) == BlockWorld.Air)
                    world.SetBlock(x, y, z, BlockWorld.Ice);
            }
    }

    // A happy pig heals a heart every few seconds
    private float pigHeal;
    void PigHealing()
    {
        if (PlayerHealth.Instance == null || BlockInteractor.InventoryOpen) return;
        pigHeal -= Time.deltaTime;
        if (pigHeal > 0f) return;
        pigHeal = 3f;
        PlayerHealth.Instance.Heal(1);
    }

    void OnDisable()
    {
        Current = Form.You;
        standingStill = false;
    }
}
