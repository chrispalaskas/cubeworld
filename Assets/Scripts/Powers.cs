using UnityEngine;

// My Powers: make up your own super powers! Turn on the "My Powers" mod, then
// open the Power Maker (a button on the Mods screen) and design up to three:
// give each one a name, choose what it does, how strong it is, and its colour.
// In the game, press R, F or X to use them.
//
// What a power can do:
//   Energy Blast:    shoot a glowing bolt that hits monsters
//   Super Leap:      jump sky high
//   Blink:           teleport a few blocks forwards, through the air
//   Healing:         get hearts back (survival)
//   Shockwave:       knock back and hurt every monster around you
//   Freeze:          freeze every monster nearby, solid, for a few seconds
//   Lightning:       call down a bolt of lightning where you're looking
//   Rainbow Bridge:  build a rainbow bridge out in front of you
//   Nature Power:    grow a tree where you're looking, with flowers
//   Fireworks:       a firework show all around you
//   Vanish:          go invisible - monsters can't see you
//   Speed Burst:     run super fast for a while
// The stronger a power, the bigger it is (and the longer you wait to use it again).
public static class Powers
{
    public const int Slots = 3;
    public static readonly KeyCode[] Keys = { KeyCode.R, KeyCode.F, KeyCode.X };

    public static readonly string[] Types =
    {
        "Energy Blast", "Super Leap", "Blink", "Healing", "Shockwave", "Freeze",
        "Lightning", "Rainbow Bridge", "Nature Power", "Fireworks", "Vanish", "Speed Burst"
    };
    static readonly float[] Wait = { 0.6f, 2f, 2.5f, 8f, 4f, 8f, 5f, 4f, 4f, 5f, 12f, 10f };   // seconds before you can use it again

    public static readonly Color[] Colours =
    {
        new Color(1f, 0.25f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.95f, 0.25f),
        new Color(0.3f, 1f, 0.35f), new Color(0.3f, 0.75f, 1f), new Color(0.75f, 0.35f, 1f), new Color(1f, 0.45f, 0.8f), new Color(1f, 1f, 1f)
    };
    static readonly string[] StartingNames = { "Power Blast", "Mega Jump", "Zoom" };

    // --- Remembering what you made ---

    static string Key(int slot, string what) { return "power-" + slot + "-" + what; }
    public static string Name(int slot) { return PlayerPrefs.GetString(Key(slot, "name"), StartingNames[slot]); }
    public static void SetName(int slot, string name) { PlayerPrefs.SetString(Key(slot, "name"), name); }
    public static int Type(int slot) { return Mathf.Clamp(PlayerPrefs.GetInt(Key(slot, "type"), slot), 0, Types.Length - 1); }
    public static void SetType(int slot, int type) { PlayerPrefs.SetInt(Key(slot, "type"), (type + Types.Length) % Types.Length); }
    public static int Strength(int slot) { return Mathf.Clamp(PlayerPrefs.GetInt(Key(slot, "strength"), 2), 1, 3); }
    public static void SetStrength(int slot, int s) { PlayerPrefs.SetInt(Key(slot, "strength"), s); }
    public static Color Colour(int slot) { return Colours[Mathf.Clamp(PlayerPrefs.GetInt(Key(slot, "colour"), slot * 2), 0, Colours.Length - 1)]; }
    public static int ColourNumber(int slot) { return Mathf.Clamp(PlayerPrefs.GetInt(Key(slot, "colour"), slot * 2), 0, Colours.Length - 1); }
    public static void SetColour(int slot, int c) { PlayerPrefs.SetInt(Key(slot, "colour"), c); }
    public static float WaitFor(int slot) { return Wait[Type(slot)] * (0.7f + Strength(slot) * 0.3f); }

    // --- The Power Maker screen (opened from the Mods screen) ---

    public static bool MakerOpen;
    private static GUIStyle big, small;

    public static void DrawMaker()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        }
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        const float width = 860f, height = 520f;
        float scale = Mathf.Min(1f, (Screen.width - 24f) / width, (Screen.height - 24f) / height);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f, (Screen.height - height * scale) / 2f, 0f),
                                   Quaternion.identity, Vector3.one * scale);

        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.Label(new Rect(0, 10, width, 34), "POWER MAKER", big);
        GUI.Label(new Rect(0, 44, width, 20), "Make up your own super powers! Turn on the \"My Powers\" mod, then press R, F or X in the game.",
                  new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

        for (int slot = 0; slot < Slots; slot++)
        {
            Rect card = new Rect(16, 74 + slot * 134, width - 32, 126);
            Color colour = Colour(slot);
            GUI.color = new Color(colour.r * 0.35f, colour.g * 0.35f, colour.b * 0.35f, 0.95f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = before;

            GUI.Label(new Rect(card.x + 12, card.y + 8, 60, 40), Keys[slot].ToString(), big);

            // The name - type whatever you like
            GUI.Label(new Rect(card.x + 80, card.y + 8, 60, 22), "Name:", small);
            string name = GUI.TextField(new Rect(card.x + 140, card.y + 6, 230, 26), Name(slot), 24);
            if (name != Name(slot)) SetName(slot, name);

            // What it does
            GUI.Label(new Rect(card.x + 80, card.y + 44, 60, 22), "Power:", small);
            if (GUI.Button(new Rect(card.x + 140, card.y + 40, 34, 30), "<")) SetType(slot, Type(slot) - 1);
            GUI.Label(new Rect(card.x + 178, card.y + 44, 160, 22), Types[Type(slot)], new GUIStyle(small) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(card.x + 336, card.y + 40, 34, 30), ">")) SetType(slot, Type(slot) + 1);

            // How strong
            GUI.Label(new Rect(card.x + 400, card.y + 10, 80, 22), "Strength:", small);
            for (int s = 1; s <= 3; s++)
            {
                GUI.color = Strength(slot) == s ? Color.yellow : Color.white;
                if (GUI.Button(new Rect(card.x + 480 + (s - 1) * 52, card.y + 6, 46, 28), new string('*', s))) SetStrength(slot, s);
            }
            GUI.color = before;

            // Its colour
            GUI.Label(new Rect(card.x + 400, card.y + 46, 80, 22), "Colour:", small);
            for (int c = 0; c < Colours.Length; c++)
            {
                Rect swatch = new Rect(card.x + 480 + c * 40, card.y + 40, 34, 30);
                if (ColourNumber(slot) == c)
                {
                    GUI.DrawTexture(new Rect(swatch.x - 3, swatch.y - 3, swatch.width + 6, swatch.height + 6), Texture2D.whiteTexture);
                }
                GUI.color = Colours[c];
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                GUI.color = before;
                if (GUI.Button(swatch, "", GUIStyle.none)) SetColour(slot, c);
            }

            GUI.Label(new Rect(card.x + 80, card.y + 82, card.width - 100, 40), Describe(slot), new GUIStyle(small) { wordWrap = true });
        }

        if (GUI.Button(new Rect(16, height - 48, 220, 36), "< Back to the mods")) MakerOpen = false;
        if (GUI.Button(new Rect(width - 236, height - 48, 220, 36), "Play!")) { MakerOpen = false; Mods.CloseScreen(); }
        PlayerPrefs.Save();
        GUI.matrix = old;
    }

    public static string Describe(int slot)
    {
        int s = Strength(slot);
        switch (Type(slot))
        {
            case 0:  return "Shoots a glowing bolt that does " + (6 * s) + " damage to the first monster it hits.";
            case 1:  return "Launches you high into the sky.";
            case 2:  return "Teleports you up to " + (5 * s + 3) + " blocks the way you're looking.";
            case 3:  return "Heals " + (3 * s) + " hearts' worth (in survival).";
            case 4:  return "Knocks back every monster within " + (3 + 2 * s) + " blocks, and hurts them.";
            case 5:  return "Freezes every monster within " + (6 + 4 * s) + " blocks for " + (2 + 2 * s) + " seconds.";
            case 6:  return "Lightning strikes where you're looking, hurting monsters there.";
            case 7:  return "Builds a rainbow bridge " + (6 * s + 4) + " blocks long in front of you.";
            case 8:  return "Grows a tree where you're looking, with flowers all round.";
            case 9:  return "A show of " + (3 * s) + " fireworks all around you.";
            case 10: return "Go invisible for " + (6 * s) + " seconds - monsters can't see you.";
            default: return "Run super fast for " + (6 * s) + " seconds.";
        }
    }
}

// Uses your powers when you press their keys, and shows them on the screen
public class PowerUser : MonoBehaviour
{
    private readonly float[] readyAt = new float[Powers.Slots];
    private GUIStyle label;

    public static void Begin()
    {
        new GameObject("Your Powers").AddComponent<PowerUser>();
    }

    void Update()
    {
        if (!Mods.MyPowers || BlockInteractor.Me == null || BlockInteractor.InventoryOpen || Mods.ScreenOpen) return;
        if (Cursor.lockState != CursorLockMode.Locked || Tutorial.IsOpen || PlayerLook.Dressing) return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.Dead) return;

        for (int slot = 0; slot < Powers.Slots; slot++)
        {
            if (!Input.GetKeyDown(Powers.Keys[slot])) continue;
            if (Time.time < readyAt[slot])
            {
                PortalTraveller.Announce(Powers.Name(slot) + " isn't ready yet...");
                continue;
            }
            readyAt[slot] = Time.time + Powers.WaitFor(slot);
            Use(slot);
        }
    }

    void Use(int slot)
    {
        Transform me = BlockInteractor.Me.transform;
        Camera eyes = Camera.main;
        Vector3 eye = eyes.transform.position, look = eyes.transform.forward;
        int s = Powers.Strength(slot);
        Color colour = Powers.Colour(slot);
        BlockWorld world = BlockWorld.Instance;
        FirstPersonPlayer walker = me.GetComponent<FirstPersonPlayer>();
        Burst(me.position + Vector3.up, colour, 10);
        Sounds.Whoosh(me.position);

        switch (Powers.Type(slot))
        {
            case 0:   // Energy Blast
                MonsterShot.PowerBolt(eye + look * 0.8f, look * 32f, 6 * s, colour);
                Sounds.Twang(me.position);
                break;

            case 1:   // Super Leap
                walker.LaunchUp(10f + 5f * s);
                break;

            case 2:   // Blink
            {
                Vector3 to = me.position;
                Vector3 flat = new Vector3(look.x, 0f, look.z).normalized;
                for (float d = 0.5f; d <= 5 * s + 3; d += 0.5f)
                {
                    Vector3 next = me.position + flat * d;
                    if (Solid(world, next + Vector3.up * 0.3f) || Solid(world, next + Vector3.up * 1.4f)) break;
                    to = next;
                }
                walker.Teleport(to);
                Burst(to + Vector3.up, colour, 10);
                break;
            }

            case 3:   // Healing
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(6 * s);
                PortalTraveller.Announce(PlayerHealth.Instance != null ? Powers.Name(slot) + "! You feel much better." : "Nothing can hurt you in creative - but that was very sparkly!");
                break;

            case 4:   // Shockwave
                Sounds.Boom(me.position);
                foreach (Monster monster in Monster.All.ToArray())
                    if (monster.Hostile && Vector3.Distance(monster.transform.position, me.position) < 3 + 2 * s) monster.Hit(4 * s, me.position);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    Burst(me.position + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * (2 + s), colour, 2);
                }
                break;

            case 5:   // Freeze
                foreach (Monster monster in Monster.All)
                    if (monster.Hostile && Vector3.Distance(monster.transform.position, me.position) < 6 + 4 * s)
                    {
                        monster.FreezeFor(2 + 2 * s);
                        Burst(monster.transform.position + Vector3.up, new Color(0.7f, 0.9f, 1f), 6);
                    }
                break;

            case 6:   // Lightning
            {
                Vector3 hit = LookingAt(world, eye, look, 40f);
                Sounds.Thunder(hit);
                GameObject flash = new GameObject("Lightning");
                flash.transform.position = hit + Vector3.up * 2f;
                Light light = flash.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = colour;
                light.range = 25f;
                light.intensity = 8f;
                Destroy(flash, 0.25f);
                for (int y = 0; y < 18; y += 2) Burst(hit + Vector3.up * y, colour, 2);
                foreach (Monster monster in Monster.All.ToArray())
                    if (monster.Hostile && Vector3.Distance(monster.transform.position, hit) < 2 + s) monster.Hit(8 * s, hit + Vector3.up * 5f);
                break;
            }

            case 7:   // Rainbow Bridge
            {
                Vector3 flat = new Vector3(look.x, 0f, look.z).normalized;
                int y = Mathf.RoundToInt(me.position.y - 0.5f);
                for (int d = 1; d <= 6 * s + 4; d++)
                {
                    Vector3Int cell = Vector3Int.RoundToInt(me.position + flat * d);
                    if (world.GetBlock(cell.x, y, cell.z) == BlockWorld.Air || world.GetBlock(cell.x, y, cell.z) == BlockWorld.Water)
                        world.SetBlock(cell.x, y, cell.z, BlockWorld.Rainbow);
                }
                break;
            }

            case 8:   // Nature Power
            {
                Vector3 hit = LookingAt(world, eye, look, 20f);
                Vector3Int ground = Vector3Int.RoundToInt(hit);
                world.GrowTreeAt(ground.x, ground.y + 1, ground.z);
                for (int i = 0; i < 4 + 3 * s; i++)
                {
                    int x = ground.x + Random.Range(-2 - s, 3 + s), z = ground.z + Random.Range(-2 - s, 3 + s);
                    int top = world.GetSurfaceHeight(x, z);
                    if (top > 0 && world.GetBlock(x, top, z) == BlockWorld.Grass && world.GetBlock(x, top + 1, z) == BlockWorld.Air)
                        world.SetBlock(x, top + 1, z, (byte)(BlockWorld.FlowerFirst + Random.Range(0, 3)));
                }
                Burst(hit + Vector3.up, colour, 14);
                break;
            }

            case 9:   // Fireworks
                for (int i = 0; i < 3 * s; i++)
                {
                    Vector3 spot = me.position + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                    int top = world.GetSurfaceHeight(Mathf.RoundToInt(spot.x), Mathf.RoundToInt(spot.z));
                    if (top > 0) Firework.Launch(new Vector3(Mathf.Round(spot.x), top + 1, Mathf.Round(spot.z)));
                }
                break;

            case 10:  // Vanish
                Effects.StartFor(253, 6 * s);
                PortalTraveller.Announce(Powers.Name(slot) + "! You vanish... monsters can't see you.");
                break;

            default:  // Speed Burst
                Effects.StartFor(251, 6 * s);
                PortalTraveller.Announce(Powers.Name(slot) + "! ZOOOOM!");
                break;
        }
    }

    static bool Solid(BlockWorld world, Vector3 at)
    {
        Vector3Int c = Vector3Int.RoundToInt(at);
        byte t = world.GetBlock(c.x, c.y, c.z);
        return t != BlockWorld.Air && t != BlockWorld.Water && !BlockWorld.IsFlower(t) && t != BlockWorld.Fire;
    }

    // The first solid block along a line (or as far as it reaches)
    static Vector3 LookingAt(BlockWorld world, Vector3 from, Vector3 way, float reach)
    {
        for (float d = 1f; d < reach; d += 0.25f)
            if (Solid(world, from + way * d)) return Vector3Int.RoundToInt(from + way * d);
        return Vector3Int.RoundToInt(from + way * reach);
    }

    // Sparkles in your power's colour
    static void Burst(Vector3 at, Color colour, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject spark = new GameObject("Power Spark");
            spark.transform.position = at + Random.insideUnitSphere * 0.3f;
            spark.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
            spark.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            spark.AddComponent<MeshRenderer>().sharedMaterial = SparkLook(colour);
            spark.AddComponent<PowerSpark>().velocity = Random.insideUnitSphere * 3f + Vector3.up;
        }
    }

    private static readonly System.Collections.Generic.Dictionary<Color, Material> sparkLooks = new System.Collections.Generic.Dictionary<Color, Material>();
    static Material SparkLook(Color colour)
    {
        Material look;
        if (sparkLooks.TryGetValue(colour, out look)) return look;
        look = BlockWorld.MakeMaterial(colour);
        look.EnableKeyword("_EMISSION");
        look.SetColor("_EmissionColor", colour * 1.5f);
        sparkLooks[colour] = look;
        return look;
    }

    // Your powers, along the bottom left, and whether they're ready
    void OnGUI()
    {
        if (!Mods.MyPowers || BlockInteractor.Me == null || Mods.ScreenOpen) return;
        if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        Color before = GUI.color;
        for (int slot = 0; slot < Powers.Slots; slot++)
        {
            float wait = readyAt[slot] - Time.time;
            Rect row = new Rect(12, Screen.height - 150 + slot * 24, 330, 22);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = wait > 0f ? new Color(0.6f, 0.6f, 0.6f) : Powers.Colour(slot);
            GUI.Label(new Rect(row.x + 6, row.y + 1, row.width, 22),
                      Powers.Keys[slot] + ": " + Powers.Name(slot) + (wait > 0f ? "   (" + Mathf.CeilToInt(wait) + ")" : "   READY"), label);
        }
        GUI.color = before;
    }
}

// A spark that flies off and fades away
public class PowerSpark : MonoBehaviour
{
    public Vector3 velocity;
    private float age;

    void Update()
    {
        age += Time.deltaTime;
        velocity += Vector3.down * 4f * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.localScale *= 1f - Time.deltaTime * 1.5f;
        if (age > 0.8f) Destroy(gameObject);
    }
}
