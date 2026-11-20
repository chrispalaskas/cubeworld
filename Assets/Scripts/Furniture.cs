using System.Collections.Generic;
using UnityEngine;

// Furniture, for making your house a home:
//
//   Sink      E: splash! (in survival, it fills you up with a bucket of water)
//   Toilet    E: flush!
//   Bathtub   E: bath time (and in survival, water too)
//   Bed       E: have a nap. Your hearts all come back, and from now on you
//             wake up here when you're knocked out or fall out of the world.
//   Table, Chair, Couch, Window, Rug and Painting - just for looking lovely
//   Stove     E: sizzle!
//   Fridge    E: opens up like a chest - somewhere to keep your food
//
// Each one turns to face you when you put it down (a painting goes on the
// wall behind it, and a window goes across the way you're looking).
//
// Like stairs, each piece of furniture is a little model made of boxes,
// built when it appears (see BlockWorld.CreateSpecial).
public partial class BlockWorld
{
    // Each kind of furniture takes up four numbers, one for each way it can
    // face (north, east, south, west). The north one is the item you hold.
    public const byte FurnitureFirst = 98;
    public const int FurnitureKinds = 13;
    public const byte FurnitureLast = FurnitureFirst + FurnitureKinds * 4 - 1;   // 149

    public const byte Sink     = 98;
    public const byte Toilet   = 102;
    public const byte Bathtub  = 106;
    public const byte Bed      = 110;
    public const byte Table    = 114;
    public const byte Chair    = 118;
    public const byte Couch    = 122;
    public const byte Stove    = 126;
    public const byte Fridge   = 130;
    public const byte Window   = 134;
    public const byte Rug      = 138;
    public const byte Painting = 142;
    public const byte Furnace  = 146;   // for cooking and smelting - see Furnace.cs

    public static bool IsFurniture(byte type) { return type >= FurnitureFirst && type <= FurnitureLast; }

    // The item (north-facing) version of any furniture
    public static byte FurnitureItem(byte type) { return (byte)(FurnitureFirst + (type - FurnitureFirst) / 4 * 4); }
    public static int FurnitureFacing(byte type) { return (type - FurnitureFirst) % 4; }
    public static bool IsFridge(byte type) { return IsFurniture(type) && FurnitureItem(type) == Fridge; }
    public static bool IsFurnace(byte type) { return IsFurniture(type) && FurnitureItem(type) == Furnace; }

    // The colours and textures furniture is made of
    enum Look { White, Steel, Wood, DarkWood, Water, Red, LightRed, Blue, Black, OvenGlow, FridgeWhite, Glass, RugPattern, Picture, DarkGrey, Cobble }
    private Material[] furnitureLooks;

    // One box of a piece of furniture. "solid" ones you bump into.
    struct Piece
    {
        public Vector3 centre, size;
        public Look look;
        public bool solid;
        public Piece(float x, float y, float z, float w, float h, float d, Look look, bool solid = true)
        {
            centre = new Vector3(x, y, z);
            size = new Vector3(w, h, d);
            this.look = look;
            this.solid = solid;
        }
    }

    void CreateFurnitureMaterials()
    {
        furnitureLooks = new Material[System.Enum.GetValues(typeof(Look)).Length];
        furnitureLooks[(int)Look.White]       = MakeMaterial(new Color(0.96f, 0.96f, 0.94f));
        furnitureLooks[(int)Look.Steel]       = MakeMaterial(new Color(0.70f, 0.72f, 0.76f));
        furnitureLooks[(int)Look.Wood]        = materials[Planks];
        furnitureLooks[(int)Look.DarkWood]    = MakeMaterial(new Color(0.40f, 0.26f, 0.14f));
        furnitureLooks[(int)Look.Water]       = materials[Water];
        furnitureLooks[(int)Look.Red]         = MakeMaterial(new Color(0.72f, 0.16f, 0.16f));
        furnitureLooks[(int)Look.LightRed]    = MakeMaterial(new Color(0.88f, 0.32f, 0.30f));
        furnitureLooks[(int)Look.Blue]        = MakeMaterial(new Color(0.25f, 0.42f, 0.85f));
        furnitureLooks[(int)Look.Black]       = MakeMaterial(new Color(0.10f, 0.10f, 0.11f));
        furnitureLooks[(int)Look.OvenGlow]    = MakeGlowingMaterial((x, y) => new Color(1.00f, 0.55f + y * 0.03f, 0.15f));
        furnitureLooks[(int)Look.FridgeWhite] = MakeMaterial(new Color(0.88f, 0.92f, 0.95f));
        furnitureLooks[(int)Look.Glass]       = materials[Glass];
        furnitureLooks[(int)Look.DarkGrey]    = MakeMaterial(new Color(0.30f, 0.30f, 0.32f));
        furnitureLooks[(int)Look.Cobble]      = materials[Cobblestone];

        // A red rug with a gold border
        furnitureLooks[(int)Look.RugPattern] = MakePixelMaterial((x, y) =>
        {
            if (x == 0 || y == 0 || x == 7 || y == 7) return new Color(0.90f, 0.70f, 0.20f);
            if ((x + y) % 4 == 0) return new Color(0.95f, 0.80f, 0.35f);
            return new Color(0.70f, 0.12f, 0.15f);
        });

        // A little painting: blue sky, a yellow sun, and green hills
        furnitureLooks[(int)Look.Picture] = MakePixelMaterial((x, y) =>
        {
            if (Vector2.Distance(new Vector2(x, y), new Vector2(5.5f, 5.5f)) < 1.3f) return new Color(1.00f, 0.85f, 0.20f);
            float hill = 2.2f + Mathf.Sin(x * 0.9f) * 1.2f;
            if (y < hill) return y < hill - 1.5f ? new Color(0.20f, 0.55f, 0.20f) : new Color(0.35f, 0.70f, 0.30f);
            return new Color(0.50f, 0.75f, 1.00f);
        });
    }

    // What each kind is made of, facing north (its front is towards +z)
    static Piece[] PiecesOf(byte item)
    {
        switch (item)
        {
            case Sink: return new[]
            {
                new Piece(0f, -0.17f, 0f, 0.90f, 0.66f, 0.85f, Look.Wood),             // cupboard
                new Piece(0f, -0.17f, 0.426f, 0.02f, 0.50f, 0.01f, Look.DarkWood, false),
                new Piece(0f, 0.20f, 0f, 0.95f, 0.10f, 0.90f, Look.White),             // the top
                new Piece(0f, 0.255f, 0.05f, 0.60f, 0.02f, 0.50f, Look.Steel, false),  // the basin
                new Piece(0f, 0.36f, -0.30f, 0.06f, 0.22f, 0.06f, Look.Steel, false),  // tap
                new Piece(0f, 0.45f, -0.21f, 0.06f, 0.05f, 0.20f, Look.Steel, false),
                new Piece(-0.15f, 0.29f, -0.32f, 0.07f, 0.05f, 0.07f, Look.Steel, false),
                new Piece(0.15f, 0.29f, -0.32f, 0.07f, 0.05f, 0.07f, Look.Steel, false),
            };
            case Toilet: return new[]
            {
                new Piece(0f, -0.30f, 0.05f, 0.35f, 0.40f, 0.45f, Look.White),
                new Piece(0f, -0.08f, 0.10f, 0.50f, 0.12f, 0.60f, Look.White),           // seat
                new Piece(0f, -0.015f, 0.12f, 0.30f, 0.01f, 0.38f, Look.Water, false),
                new Piece(0f, 0.10f, -0.33f, 0.55f, 0.50f, 0.22f, Look.White),           // tank
                new Piece(-0.20f, 0.28f, -0.21f, 0.10f, 0.03f, 0.04f, Look.Steel, false), // handle
            };
            case Bathtub: return new[]
            {
                new Piece(0f, -0.42f, 0f, 0.98f, 0.16f, 0.98f, Look.White),
                new Piece(0f, -0.15f, 0.44f, 0.98f, 0.40f, 0.10f, Look.White),
                new Piece(0f, -0.15f, -0.44f, 0.98f, 0.40f, 0.10f, Look.White),
                new Piece(-0.44f, -0.15f, 0f, 0.10f, 0.40f, 0.98f, Look.White),
                new Piece(0.44f, -0.15f, 0f, 0.10f, 0.40f, 0.98f, Look.White),
                new Piece(0f, -0.22f, 0f, 0.78f, 0.22f, 0.78f, Look.Water, false),
                new Piece(0f, 0.15f, -0.44f, 0.06f, 0.20f, 0.06f, Look.Steel, false),
                new Piece(0f, 0.22f, -0.36f, 0.06f, 0.05f, 0.16f, Look.Steel, false),
            };
            case Bed: return new[]
            {
                new Piece(0f, -0.38f, 0f, 0.96f, 0.24f, 0.96f, Look.Wood),
                new Piece(0f, -0.18f, 0f, 0.92f, 0.16f, 0.92f, Look.White),             // mattress
                new Piece(0f, -0.08f, 0.12f, 0.94f, 0.06f, 0.70f, Look.Blue, false),     // blanket
                new Piece(0f, -0.06f, -0.30f, 0.60f, 0.10f, 0.25f, Look.White, false),   // pillow
                new Piece(0f, 0.00f, -0.46f, 0.96f, 0.76f, 0.08f, Look.DarkWood),        // headboard
            };
            case Table: return new[]
            {
                new Piece(0f, 0.40f, 0f, 1.00f, 0.10f, 1.00f, Look.Wood),
                new Piece(-0.42f, -0.05f, -0.42f, 0.10f, 0.80f, 0.10f, Look.Wood),
                new Piece(0.42f, -0.05f, -0.42f, 0.10f, 0.80f, 0.10f, Look.Wood),
                new Piece(-0.42f, -0.05f, 0.42f, 0.10f, 0.80f, 0.10f, Look.Wood),
                new Piece(0.42f, -0.05f, 0.42f, 0.10f, 0.80f, 0.10f, Look.Wood),
            };
            case Chair: return new[]
            {
                new Piece(0f, -0.05f, 0f, 0.70f, 0.08f, 0.70f, Look.Wood),
                new Piece(-0.30f, -0.30f, -0.30f, 0.08f, 0.42f, 0.08f, Look.Wood, false),
                new Piece(0.30f, -0.30f, -0.30f, 0.08f, 0.42f, 0.08f, Look.Wood, false),
                new Piece(-0.30f, -0.30f, 0.30f, 0.08f, 0.42f, 0.08f, Look.Wood, false),
                new Piece(0.30f, -0.30f, 0.30f, 0.08f, 0.42f, 0.08f, Look.Wood, false),
                new Piece(0f, 0.30f, -0.31f, 0.70f, 0.62f, 0.08f, Look.DarkWood),        // back
            };
            case Couch: return new[]
            {
                new Piece(0f, -0.32f, 0f, 1.00f, 0.36f, 0.92f, Look.Red),
                new Piece(0f, -0.08f, 0.06f, 0.82f, 0.12f, 0.78f, Look.LightRed),        // cushion
                new Piece(0f, 0.18f, -0.36f, 1.00f, 0.64f, 0.20f, Look.Red),             // back
                new Piece(-0.45f, -0.02f, 0.02f, 0.10f, 0.30f, 0.88f, Look.Red),         // arms
                new Piece(0.45f, -0.02f, 0.02f, 0.10f, 0.30f, 0.88f, Look.Red),
            };
            case Stove: return new[]
            {
                new Piece(0f, -0.05f, 0f, 0.95f, 0.90f, 0.95f, Look.DarkGrey),
                new Piece(0f, 0.41f, 0f, 0.95f, 0.02f, 0.95f, Look.Black, false),
                new Piece(-0.22f, 0.425f, -0.20f, 0.26f, 0.01f, 0.26f, Look.Steel, false),   // rings on top
                new Piece(0.22f, 0.425f, -0.20f, 0.26f, 0.01f, 0.26f, Look.Steel, false),
                new Piece(-0.22f, 0.425f, 0.20f, 0.26f, 0.01f, 0.26f, Look.Steel, false),
                new Piece(0.22f, 0.425f, 0.20f, 0.26f, 0.01f, 0.26f, Look.Steel, false),
                new Piece(0f, -0.15f, 0.476f, 0.60f, 0.32f, 0.01f, Look.OvenGlow, false),    // oven window
                new Piece(0f, 0.08f, 0.49f, 0.60f, 0.04f, 0.03f, Look.Steel, false),         // handle
                new Piece(-0.30f, 0.30f, 0.48f, 0.06f, 0.06f, 0.02f, Look.Black, false),     // knobs
                new Piece(-0.10f, 0.30f, 0.48f, 0.06f, 0.06f, 0.02f, Look.Black, false),
                new Piece(0.10f, 0.30f, 0.48f, 0.06f, 0.06f, 0.02f, Look.Black, false),
                new Piece(0.30f, 0.30f, 0.48f, 0.06f, 0.06f, 0.02f, Look.Black, false),
            };
            case Fridge: return new[]
            {
                new Piece(0f, 0f, 0f, 0.92f, 1.00f, 0.90f, Look.FridgeWhite),
                new Piece(0f, 0.20f, 0.451f, 0.92f, 0.02f, 0.01f, Look.DarkGrey, false),     // freezer door line
                new Piece(0.35f, 0.38f, 0.47f, 0.04f, 0.14f, 0.04f, Look.Steel, false),      // handles
                new Piece(0.35f, -0.08f, 0.47f, 0.04f, 0.30f, 0.04f, Look.Steel, false),
            };
            case Window: return new[]
            {
                new Piece(0f, 0f, 0f, 0.96f, 0.96f, 0.06f, Look.Glass),
                new Piece(0f, 0.47f, 0f, 1.00f, 0.06f, 0.10f, Look.Wood, false),
                new Piece(0f, -0.47f, 0f, 1.00f, 0.06f, 0.10f, Look.Wood, false),
                new Piece(-0.47f, 0f, 0f, 0.06f, 1.00f, 0.10f, Look.Wood, false),
                new Piece(0.47f, 0f, 0f, 0.06f, 1.00f, 0.10f, Look.Wood, false),
                new Piece(0f, 0f, 0f, 0.04f, 0.96f, 0.08f, Look.Wood, false),
                new Piece(0f, 0f, 0f, 0.96f, 0.04f, 0.08f, Look.Wood, false),
            };
            case Rug: return new[]
            {
                new Piece(0f, -0.475f, 0f, 0.96f, 0.05f, 0.96f, Look.RugPattern),
            };
            case Furnace: return new[]
            {
                new Piece(0f, 0f, 0f, 1.00f, 1.00f, 0.96f, Look.Cobble),
                new Piece(0f, -0.12f, 0.486f, 0.56f, 0.40f, 0.01f, Look.Black, false),       // the opening
                new Piece(0f, 0.30f, 0.486f, 0.70f, 0.06f, 0.01f, Look.DarkGrey, false),
                new Piece(0f, -0.22f, 0.489f, 0.46f, 0.16f, 0.01f, Look.OvenGlow, false),    // the fire (only when lit)
            };
            default: return new[]   // Painting, hanging on the wall behind it
            {
                new Piece(0f, 0.05f, -0.46f, 0.86f, 0.66f, 0.06f, Look.DarkWood),
                new Piece(0f, 0.05f, -0.425f, 0.76f, 0.56f, 0.01f, Look.Picture, false),
            };
        }
    }

    // Builds the little model, turned to face the right way
    void BuildFurniture(GameObject cube, byte type)
    {
        cube.transform.localRotation = Quaternion.LookRotation(Direction(FurnitureFacing(type)));

        foreach (Piece piece in PiecesOf(FurnitureItem(type)))
        {
            AddSpecialPiece(cube, piece.centre, piece.size, furnitureLooks[(int)piece.look]);
            if (!piece.solid) continue;

            BoxCollider solid = cube.AddComponent<BoxCollider>();
            solid.center = piece.centre;
            solid.size = piece.size;
        }

        // A furnace's fire only shows while it's cooking something
        if (FurnitureItem(type) == Furnace)
            cube.AddComponent<FurnaceFire>();

        // The oven glows a little
        if (FurnitureItem(type) == Stove)
        {
            Light glow = cube.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.25f);
            glow.range = 3f;
            glow.intensity = 1f;
            glow.shadows = LightShadows.None;
        }
    }
}

// Placing and using furniture. This is part of BlockInteractor.
public partial class BlockInteractor
{
    // Turned so its front faces you
    byte FurnitureFacingMe(byte item)
    {
        Vector3 f = transform.forward;
        int facing;
        if (Mathf.Abs(f.z) >= Mathf.Abs(f.x)) facing = f.z > 0f ? 2 : 0;
        else facing = f.x > 0f ? 3 : 1;
        return (byte)(item + facing);
    }

    // Furniture that does something when you press E on it
    bool LookingAtUsableFurniture(out Vector3Int cell, out byte item)
    {
        Vector3Int unused;
        item = 0;
        if (!LookingAtBlock(out cell, out unused)) return false;

        byte type = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
        if (!BlockWorld.IsFurniture(type)) return false;
        item = BlockWorld.FurnitureItem(type);
        return item == BlockWorld.Sink || item == BlockWorld.Toilet || item == BlockWorld.Bathtub ||
               item == BlockWorld.Bed || item == BlockWorld.Stove || item == BlockWorld.Furnace;
    }

    void UseFurniture(Vector3Int cell, byte item)
    {
        Vector3 top = cell + Vector3.up * 0.35f;
        squeezeTime = 0.15f;

        switch (item)
        {
            case BlockWorld.Sink:
                Sounds.Splash(top);
                Splash(top, new Color(0.45f, 0.70f, 1.00f), 14, 2.5f);
                if (Survival)
                {
                    Give(BlockWorld.Water);
                    PortalTraveller.Announce("Splash! You filled up with water - you've got " + HowMany(BlockWorld.Water) + ".");
                }
                else PortalTraveller.Announce("Splash! Lovely clean hands.");
                break;

            case BlockWorld.Toilet:
                Sounds.Flush(top);
                Splash(cell + Vector3.up * 0.05f, new Color(0.45f, 0.70f, 1.00f), 20, 1.5f);
                PortalTraveller.Announce("FLUSH! Whooooosh!");
                break;

            case BlockWorld.Bathtub:
                Sounds.Splash(top);
                Splash(cell + Vector3.up * 0.1f, Color.white, 24, 2f);   // bubbles!
                if (Survival) Give(BlockWorld.Water);
                PortalTraveller.Announce("Splish splash, bath time!");
                break;

            case BlockWorld.Stove:
                Splash(top + Vector3.up * 0.2f, new Color(0.85f, 0.85f, 0.85f), 12, 1.2f);   // steam
                PortalTraveller.Announce("Sizzle! Something smells yummy.");
                break;

            case BlockWorld.Bed:
                Sleep(cell);
                break;

            case BlockWorld.Furnace:
                UseFurnace(cell);
                break;
        }
    }

    // A nap: hearts back, and this is where you wake up from now on
    void Sleep(Vector3Int bed)
    {
        sleepingUntil = Time.time + 2.5f;
        FirstPersonPlayer.SetBed(bed);
        Achievements.Unlock("sleep");

        // At night, you sleep right through till morning
        if (DayNight.IsNight)
        {
            DayNight.Instance.SkipToMorning();
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(PlayerHealth.MostHealth);
            PortalTraveller.Announce("Good morning! You slept all through the night. You'll wake up here from now on.");
            return;
        }
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(PlayerHealth.MostHealth);
        PortalTraveller.Announce(PlayerHealth.Instance != null
            ? "You had a lovely nap - all your hearts are back! You'll wake up here from now on."
            : "Zzz... You had a lovely nap. You'll wake up here from now on.");
    }

    private float sleepingUntil;

    // The screen goes dark for a moment while you nap
    void DrawSleep()
    {
        float left = sleepingUntil - Time.time;
        if (left <= 0f) return;

        Color before = GUI.color;
        GUI.color = new Color(0.02f, 0.02f, 0.08f, Mathf.Clamp01(left / 0.6f) * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 30), "Z z z . . .", centredLabel);
        GUI.color = before;
    }

    // Little drops (or bubbles, or steam) flying up and out
    static void Splash(Vector3 at, Color colour, int count, float speed)
    {
        Material look = BlockWorld.MakeMaterial(colour);
        for (int i = 0; i < count; i++)
        {
            GameObject drop = new GameObject("Splash");
            drop.transform.position = at + Random.insideUnitSphere * 0.2f;
            drop.transform.localScale = Vector3.one * Random.Range(0.04f, 0.09f);
            drop.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            drop.AddComponent<MeshRenderer>().sharedMaterial = look;

            Rigidbody flight = drop.AddComponent<Rigidbody>();
            flight.linearVelocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.6f, 1.4f) * speed, Random.Range(-1f, 1f));
            Destroy(drop, 1f);
        }
        Destroy(look, 1.1f);
    }

    // --- Pictures for the inventory ---

    private static readonly Dictionary<byte, Texture2D> furniturePictures = new Dictionary<byte, Texture2D>();

    static Texture2D FurniturePicture(byte item)
    {
        Texture2D picture;
        if (furniturePictures.TryGetValue(item, out picture)) return picture;

        Color special = new Color(0.25f, 0.42f, 0.85f);   // blue, for water, blankets and glass
        string[] rows;
        switch (item)
        {
            case BlockWorld.Sink: rows = new[]
            {
                "........",
                "...ss...",
                "....ss..",
                "wwwwwwww",
                "wxxxxxxw",
                "pppppppp",
                "pppbpppp",
                "pppppppp",
            }; break;
            case BlockWorld.Toilet: rows = new[]
            {
                ".wwww...",
                ".wwww...",
                ".wwww...",
                "wwwwwww.",
                "wxxxxxw.",
                ".wwwww..",
                "..www...",
                "..www...",
            }; break;
            case BlockWorld.Bathtub: rows = new[]
            {
                "......s.",
                "......ss",
                "........",
                "w......w",
                "wxxxxxxw",
                "wxxxxxxw",
                "wwwwwwww",
                ".w....w.",
            }; break;
            case BlockWorld.Bed: rows = new[]
            {
                "........",
                "b.......",
                "b.......",
                "bww.....",
                "bwwxxxxx",
                "bppppppp",
                "bppppppp",
                "b......b",
            }; break;
            case BlockWorld.Table: rows = new[]
            {
                "........",
                "........",
                "pppppppp",
                "pppppppp",
                ".p....p.",
                ".p....p.",
                ".p....p.",
                ".p....p.",
            }; break;
            case BlockWorld.Chair: rows = new[]
            {
                "..b.....",
                "..b.....",
                "..b.....",
                "..b.....",
                "..pppppp",
                "..p....p",
                "..p....p",
                "..p....p",
            }; break;
            case BlockWorld.Couch: rows = new[]
            {
                "........",
                ".rrrrrr.",
                ".rrrrrr.",
                "rrrrrrrr",
                "roooooor",
                "rrrrrrrr",
                "rrrrrrrr",
                ".k....k.",
            }; break;
            case BlockWorld.Stove: rows = new[]
            {
                "kkkkkkkk",
                "skkskkks",
                "kkkkkkkk",
                "ssssssss",
                "skoooosk",
                "skoooosk",
                "ssssssss",
                "ssssssss",
            }; break;
            case BlockWorld.Fridge: rows = new[]
            {
                "wwwwwwww",
                "wwwwwwsw",
                "wwwwwwww",
                "ssssssss",
                "wwwwwwsw",
                "wwwwwwsw",
                "wwwwwwsw",
                "wwwwwwww",
            }; break;
            case BlockWorld.Window: rows = new[]
            {
                "pppppppp",
                "pxxxpxxp",
                "pxwxpxxp",
                "pxxxpxxp",
                "pppppppp",
                "pxxxpxxp",
                "pxxxpxwp",
                "pppppppp",
            }; break;
            case BlockWorld.Rug: rows = new[]
            {
                "........",
                "........",
                "........",
                "yyyyyyyy",
                "yryryryy",
                "yrrrrrry",
                "yyyyyyyy",
                "........",
            }; break;
            case BlockWorld.Furnace: rows = new[]
            {
                "ssssssss",
                "sskkkkss",
                "skkkkkks",
                "skkkkkks",
                "skooooks",
                "skyoyoks",
                "ssssssss",
                "ssssssss",
            }; break;
            default: rows = new[]   // painting
            {
                "bbbbbbbb",
                "bxxxxyyb",
                "bxxxxyyb",
                "bxxxxxxb",
                "bgxxxxgb",
                "bggxgggb",
                "bggggggb",
                "bbbbbbbb",
            };
            special = new Color(0.50f, 0.75f, 1.00f);
            break;
        }

        picture = PixelArt(rows, special);
        furniturePictures[item] = picture;
        return picture;
    }
}
