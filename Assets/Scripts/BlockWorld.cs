using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// The world is a 3D grid of block numbers. To draw it, the world is cut into
// "chunks" 16 blocks wide (and the full height of the world), the way
// Minecraft does it. Each chunk is drawn as ONE shape made only of the block
// faces you could actually see - faces buried in the ground, or pressed
// against another block, are left out. That is the trick that keeps a big
// world fast.
//
// A few blocks aren't simple cubes - stairs, doors, and lamps (which carry a
// light) - so those each get their own little object instead.
public partial class BlockWorld : MonoBehaviour
{
    public static BlockWorld Instance { get; private set; }

    // How big brand new worlds are. A saved world keeps the size it was made
    // with, so worlds from before they got bigger still load.
    public const int NewSizeX = 128;
    public const int NewSizeY = 48;
    public const int NewSizeZ = 128;

    // The size of the world that's open right now
    public static int SizeX { get; private set; } = NewSizeX;
    public static int SizeY { get; private set; } = NewSizeY;
    public static int SizeZ { get; private set; } = NewSizeZ;

    const int ChunkSize = 16;

    // Any empty space at or below this height fills up with water
    public const int SeaLevel = 11;

    // 0 gives a brand new world every time you press Play. Put the seed
    // printed in the Console here to get a world you liked back again.
    public const int FixedSeed = 0;

    // Block types. 0 means empty space.
    public const byte Air    = 0;
    public const byte Grass  = 1;
    public const byte Dirt   = 2;
    public const byte Stone  = 3;
    public const byte Wood   = 4;
    public const byte Sand   = 5;
    public const byte Leaves = 6;
    public const byte Water  = 7;
    public const byte Tnt    = 8;

    // 9 and 10 are the sponge and leash, which are tools rather than blocks,
    // so the newer blocks start at 11
    public const byte Glass       = 11;
    public const byte Diamond     = 12;
    public const byte Gold        = 13;
    public const byte Iron        = 14;
    public const byte Brick       = 15;
    public const byte Cobblestone = 16;
    public const byte Planks      = 17;
    public const byte Snow        = 18;
    public const byte Obsidian    = 19;

    // Blocks you can only get by making them at a crafting table
    public const byte CraftingTable = 20;
    public const byte Lamp          = 21;
    public const byte BouncePad     = 22;
    public const byte MegaTnt       = 23;
    // (24 is the chicken egg, which isn't a block)
    public const byte Ice           = 25;
    public const byte Rainbow       = 26;
    public const byte Bookshelf     = 27;

    // Blocks that aren't a plain cube. (28 to 34 are the sapling, firework and swords.)
    //
    // Stairs have four numbers, one for each way they can face - the number
    // says which side the high step is on.
    public const byte StairsNorth = 35;   // high step towards +Z
    public const byte StairsEast  = 36;   // +X
    public const byte StairsSouth = 37;   // -Z
    public const byte StairsWest  = 38;   // -X

    // A door is two blocks tall. Each half is one of eight numbers from 40,
    // built from three yes/no answers added together:
    //   +1 if it's the top half, +2 if it's open, +4 if it runs along the X axis.
    // (39 is the door you hold, before it's placed.)
    public const byte DoorItem = 39;
    public const byte DoorFirst = 40;
    public const byte DoorLast = 47;

    // (48 is the villager egg.) Portals, and blocks from the Magic Land.
    public const byte Portal      = 49;   // takes you to the next portal in the same world
    public const byte MagicPortal = 50;   // takes you to the Magic Land, and back home
    public const byte MagicGrass  = 51;
    public const byte Crystal     = 52;
    public const byte Cloud       = 53;

    // (54 is the magic villager egg, and 55 the rainbow you place.)
    // Six glowing colours, which rainbows are built from.
    public const byte RainbowRed    = 56;
    public const byte RainbowOrange = 57;
    public const byte RainbowYellow = 58;
    public const byte RainbowGreen  = 59;
    public const byte RainbowBlue   = 60;
    public const byte RainbowPurple = 61;

    // A chest keeps blocks inside it (see Chests.cs for the screen)
    public const byte Chest = 62;

    // Giant mushrooms in the Magic Land. The spotty caps are bouncy!
    public const byte MushroomStem = 63;
    public const byte MushroomCap  = 64;

    // (65 to 67 are the unicorn egg, wings and dragon egg.) The Nether:
    public const byte FirePortal  = 68;   // takes you to the Nether, and back home
    public const byte EmberRock   = 69;   // dark red rock that the Nether is made of
    public const byte Ash         = 70;   // soft grey ash on top of the rock
    public const byte Lava        = 71;   // glowing and runny - far too hot to stay in!
    public const byte FireCrystal = 72;   // glowing crystals on top of the obsidian spires
    public const byte FireVent    = 73;   // a crack in the ground that shoots out flames
    public const byte Fire        = 74;   // flickering flames - hot! You walk into them, not on them

    // (75 is the fire villager egg.) Machines (see Machines.cs):
    public const byte Button = 76;        // press it with E to send a pulse of power
    public const byte Cable  = 77;        // carries power, and glows while it does
    // A dispenser shoots out what's inside it when power reaches it. It has
    // four numbers, one for each way it can face - like stairs.
    public const byte DispenserNorth = 78;   // shoots towards +Z
    public const byte DispenserEast  = 79;   // +X
    public const byte DispenserSouth = 80;   // -Z
    public const byte DispenserWest  = 81;   // -X

    public static bool IsDispenser(byte type) { return type >= DispenserNorth && type <= DispenserWest; }

    // Which way a dispenser shoots
    public static Vector3Int DispenserFacing(byte type)
    {
        switch (type)
        {
            case DispenserEast:  return Vector3Int.right;
            case DispenserSouth: return new Vector3Int(0, 0, -1);
            case DispenserWest:  return Vector3Int.left;
            default:             return new Vector3Int(0, 0, 1);
        }
    }

    // Blocks with things kept inside them, like a chest
    public static bool IsContainer(byte type) { return type == Chest || IsDispenser(type) || IsFridge(type); }

    // More machines
    public const byte PressurePlate = 82;   // step on it to send power
    public const byte Timer         = 83;   // sends power all by itself, every couple of seconds
    public const byte SignalLight   = 84;   // glows when power reaches it
    public const byte NoteBlock     = 85;   // plays a note when power reaches it (E changes the note)
    // A piston pushes the blocks in front of it when it's powered. Four
    // numbers again, one for each way it can face.
    public const byte PistonNorth   = 86;
    public const byte PistonEast    = 87;
    public const byte PistonSouth   = 88;
    public const byte PistonWest    = 89;
    // A conveyor belt carries you along the way its arrow points
    public const byte ConveyorNorth = 90;
    public const byte ConveyorEast  = 91;
    public const byte ConveyorSouth = 92;
    public const byte ConveyorWest  = 93;

    // (94 to 96 are food, which isn't a block)

    // Very rare: only found on the Star Shrine, at the top of the highest
    // mountain. It glows, shines a beam of starlight into the sky, heals you
    // in survival, and zombies won't go near it. See StarCrystal.cs.
    public const byte StarCrystal   = 97;

    public static bool IsPiston(byte type)   { return type >= PistonNorth && type <= PistonWest; }
    public static bool IsConveyor(byte type) { return type >= ConveyorNorth && type <= ConveyorWest; }

    // North, east, south or west - for anything that comes in four directions
    static Vector3Int Direction(int which)
    {
        switch (which)
        {
            case 1:  return Vector3Int.right;
            case 2:  return new Vector3Int(0, 0, -1);
            case 3:  return Vector3Int.left;
            default: return new Vector3Int(0, 0, 1);
        }
    }

    public static Vector3Int PistonFacing(byte type)       { return Direction(type - PistonNorth); }
    public static Vector3Int ConveyorDirection(byte type)  { return Direction(type - ConveyorNorth); }

    // Every timer in the world, so they can tick without searching for them
    private readonly HashSet<Vector3Int> timers = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> Timers { get { return timers; } }
    private readonly HashSet<Vector3Int> starCrystals = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> StarCrystals { get { return starCrystals; } }
    private Material starBeam;

    // Which note each note block is set to (0 is the lowest)
    private readonly Dictionary<Vector3Int, byte> noteTunes = new Dictionary<Vector3Int, byte>();

    public byte NoteAt(Vector3Int cell)
    {
        byte note;
        return noteTunes.TryGetValue(cell, out note) ? note : (byte)0;
    }

    public void SetNote(Vector3Int cell, byte note)
    {
        noteTunes[cell] = note;
    }

    // What's inside one chest: 27 spaces, each holding up to 64 of one thing
    public class ChestContents
    {
        public const int Spaces = 27;
        public const int MostInASpace = 64;
        public readonly byte[] items = new byte[Spaces];    // 0 means the space is empty
        public readonly byte[] counts = new byte[Spaces];
    }

    // Every chest in the world, by where it is
    private readonly Dictionary<Vector3Int, ChestContents> chests = new Dictionary<Vector3Int, ChestContents>();

    // The inside of the chest at this spot (a new, empty one the first time)
    public ChestContents ChestAt(Vector3Int cell)
    {
        ChestContents inside;
        if (!chests.TryGetValue(cell, out inside))
        {
            inside = new ChestContents();
            chests[cell] = inside;
        }
        return inside;
    }

    private byte[,,] blocks;
    private Material[] materials;

    // One drawn shape per chunk, and which chunks need redrawing after a change
    private Chunk[,] chunks;
    private bool[,] chunkChanged;
    private bool anyChunkChanged;

    // The stairs, doors and lamps, which are drawn one by one
    private readonly Dictionary<Vector3Int, GameObject> specials = new Dictionary<Vector3Int, GameObject>();
    private readonly Dictionary<Vector3Int, byte> specialBuiltAs = new Dictionary<Vector3Int, byte>();
    private Mesh cubeMesh;

    public Mesh CubeMesh { get { return cubeMesh; } }

    // The bottom block of every tree's trunk, so squirrels can find them
    public readonly List<Vector3Int> Trees = new List<Vector3Int>();

    // Every TNT block whose fuse is burning right now
    public readonly Dictionary<Vector3Int, Tnt> LitTnt = new Dictionary<Vector3Int, Tnt>();

    public Material TntFlashMaterial { get; private set; }
    public Material ExplosionMaterial { get; private set; }

    private int[,] groundHeight;      // top of the land in each column, before caves
    private System.Random random;

    void Awake()
    {
        Instance = this;
        CreateMaterials();
    }

    // Makes the empty grid. Called first by Generate, GenerateMagicLand and ReadFrom.
    void MakeEmptyWorld(int sizeX, int sizeY, int sizeZ)
    {
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;

        blocks = new byte[SizeX, SizeY, SizeZ];
        groundHeight = new int[SizeX, SizeZ];
        chests.Clear();
        timers.Clear();
        starCrystals.Clear();
        crops.Clear();
        furnaces.Clear();
        spawners.Clear();
        signTexts.Clear();
        noteTunes.Clear();

        int chunksX = (SizeX + ChunkSize - 1) / ChunkSize;
        int chunksZ = (SizeZ + ChunkSize - 1) / ChunkSize;
        chunks = new Chunk[chunksX, chunksZ];
        chunkChanged = new bool[chunksX, chunksZ];
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        OnDestroyChunks();
    }

    // Makes a brand new world. (Loading a saved one uses ReadFrom instead.)
    public void Generate()
    {
        int seed = FixedSeed != 0 ? FixedSeed : System.Environment.TickCount;
        random = new System.Random(seed);
        Debug.Log("World seed: " + seed);

        MakeEmptyWorld(NewSizeX, NewSizeY, NewSizeZ);

        GenerateHills();
        MakeLands();        // deserts, jungles and snowy lands (Lands.cs)
        CarveCaves();
        PoolLava();
        BuryTreasure();
        RegrowGrass();
        FillSea();
        FreezeSea();
        PlantLands();
        BuildAllCubes();
    }

    void CreateMaterials()
    {
        // ONE material per block type, shared by every cube of that type.
        // Giving each cube its own material would bring the game to a crawl.
        materials = new Material[256];
        // Natural blocks each get their own pattern, plus a faint darker edge
        // so you can see where one block stops and the next one starts
        materials[Grass]  = MakePixelMaterial(GrassPixel, true);
        materials[Dirt]   = MakePixelMaterial(DirtPixel, true);
        materials[Stone]  = MakePixelMaterial(StonePixel, true);
        materials[Wood]   = MakePixelMaterial(BarkPixel, true);
        materials[Sand]   = MakePixelMaterial(SandPixel, true);
        materials[Leaves] = MakePixelMaterial(LeafPixel, true);
        materials[Water]  = MakeSeeThroughMaterial(new Color(0.20f, 0.45f, 0.85f, 0.6f));
        materials[Tnt]    = MakeTntMaterial(new Color(0.80f, 0.15f, 0.10f), Color.white);

        materials[Glass]       = MakeGlassMaterial();
        materials[Diamond]     = MakePixelMaterial((x, y) => Speckle(x, y, 4) ? new Color(0.80f, 1.00f, 1.00f) : new Color(0.35f, 0.85f, 0.85f), true);
        materials[Gold]        = MakePixelMaterial((x, y) => Speckle(x, y, 5) ? new Color(1.00f, 0.95f, 0.55f) : new Color(0.95f, 0.75f, 0.15f), true);
        materials[Iron]        = MakePixelMaterial((x, y) => Speckle(x, y, 6) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.78f, 0.78f, 0.80f), true);
        materials[Brick]       = MakePixelMaterial(BrickPixel);
        materials[Cobblestone] = MakePixelMaterial(CobblePixel, true);
        materials[Planks]      = MakePixelMaterial(PlankPixel);
        materials[Snow]        = MakePixelMaterial((x, y) => Speckle(x, y, 7) ? new Color(0.85f, 0.90f, 0.95f) : new Color(0.97f, 0.98f, 1.00f), true);
        materials[Obsidian]    = MakePixelMaterial((x, y) => Speckle(x, y, 5) ? new Color(0.32f, 0.20f, 0.48f) : new Color(0.10f, 0.07f, 0.16f), true);

        materials[CraftingTable] = MakePixelMaterial(CraftingTablePixel);
        materials[Lamp]          = MakeLampMaterial();
        materials[BouncePad]     = MakePixelMaterial(BouncePadPixel);
        materials[MegaTnt]       = MakeTntMaterial(new Color(0.25f, 0.05f, 0.05f), new Color(1.00f, 0.85f, 0.10f));
        materials[Ice]           = MakePixelMaterial((x, y) => x - y == 3 || x - y == -2 ? new Color(0.90f, 0.97f, 1.00f) : new Color(0.62f, 0.82f, 0.98f));
        materials[Rainbow]       = MakePixelMaterial(RainbowPixel);
        materials[Bookshelf]     = MakePixelMaterial(BookshelfPixel);

        for (byte stairs = StairsNorth; stairs <= StairsWest; stairs++)
            materials[stairs] = materials[Planks];

        Material doorBottom = MakePixelMaterial((x, y) => DoorPixel(x, y, false));
        Material doorTop = MakePixelMaterial((x, y) => DoorPixel(x, y, true));
        materials[DoorItem] = doorBottom;
        for (byte door = DoorFirst; door <= DoorLast; door++)
            materials[door] = DoorIsTop(door) ? doorTop : doorBottom;

        materials[Portal]      = MakeSwirlMaterial(new Color(0.45f, 0.10f, 0.75f), new Color(0.85f, 0.55f, 1.00f));
        materials[MagicPortal] = MakeSwirlMaterial(new Color(0.95f, 0.30f, 0.70f), new Color(0.40f, 0.95f, 1.00f));
        materials[MagicGrass]  = MakePixelMaterial((x, y) => Speckle(x, y, 4) ? new Color(1.00f, 0.70f, 0.95f) : new Color(0.75f, 0.40f, 0.85f), true);
        materials[Crystal]     = MakeGlowingMaterial((x, y) => (x + y) % 4 == 0 ? new Color(0.85f, 1.00f, 1.00f) : new Color(0.30f, 0.85f, 0.95f));
        materials[Cloud]       = MakePixelMaterial((x, y) => Speckle(x, y, 9) ? new Color(0.90f, 0.92f, 0.98f) : Color.white, true);
        materials[Chest]       = MakePixelMaterial(ChestPixel);
        materials[MushroomStem] = MakePixelMaterial((x, y) => x % 3 == 0 ? new Color(0.88f, 0.84f, 0.76f) : new Color(0.96f, 0.93f, 0.86f));
        materials[MushroomCap]  = MakePixelMaterial((x, y) =>
            (x == 1 || x == 2) && (y == 1 || y == 2) || (x == 5 || x == 6) && (y == 4 || y == 5) || x == 3 && y == 6
                ? Color.white : new Color(0.88f, 0.15f, 0.20f));   // red with white spots

        materials[FirePortal]  = MakeSwirlMaterial(new Color(0.70f, 0.10f, 0.02f), new Color(1.00f, 0.75f, 0.15f));
        materials[EmberRock]   = MakePixelMaterial((x, y) => Speckle(x, y, 4) ? new Color(0.30f, 0.08f, 0.06f) : new Color(0.48f, 0.14f, 0.10f), true);
        materials[Ash]         = MakePixelMaterial((x, y) => Speckle(x, y, 3) ? new Color(0.35f, 0.33f, 0.33f) : new Color(0.50f, 0.48f, 0.47f), true);
        materials[Lava]        = MakeSwirlMaterial(new Color(0.95f, 0.30f, 0.02f), new Color(1.00f, 0.85f, 0.20f));
        materials[FireCrystal] = MakeGlowingMaterial((x, y) => (x + y) % 3 == 0 ? new Color(1.00f, 0.95f, 0.60f) : new Color(1.00f, 0.65f, 0.15f));
        materials[FireVent]    = MakeGlowingMaterial((x, y) =>
            Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) < 1.6f || x == y ? new Color(1.00f, 0.50f, 0.05f) : new Color(0.15f, 0.06f, 0.05f));

        // The flames that fire is made of: red at the bottom, orange, and yellow at the tips
        flameMaterials = new[]
        {
            MakeGlowingMaterial((x, y) => new Color(1.00f, 0.25f, 0.05f)),
            MakeGlowingMaterial((x, y) => new Color(1.00f, 0.55f, 0.10f)),
            MakeGlowingMaterial((x, y) => new Color(1.00f, 0.85f, 0.25f)),
        };
        materials[Fire] = flameMaterials[1];

        materials[Button] = MakePixelMaterial((x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
            if (d < 2.2f) return d < 1.2f ? new Color(1.00f, 0.35f, 0.30f) : new Color(0.80f, 0.12f, 0.10f);   // a big red button
            return Speckle(x, y, 5) ? new Color(0.45f, 0.45f, 0.48f) : new Color(0.60f, 0.60f, 0.62f);        // on a stone block
        });
        cableOff = MakeMaterial(new Color(0.45f, 0.08f, 0.06f));
        cableOn = MakeGlowingMaterial((x, y) => new Color(1.00f, 0.15f, 0.10f));
        materials[Cable] = cableOn;
        Material dispenser = MakePixelMaterial((x, y) => Speckle(x, y, 3) ? new Color(0.38f, 0.38f, 0.40f) : new Color(0.55f, 0.55f, 0.57f));
        for (byte d = DispenserNorth; d <= DispenserWest; d++)
            materials[d] = dispenser;
        dispenserMouth = MakeMaterial(new Color(0.08f, 0.08f, 0.08f));

        materials[PressurePlate] = MakePixelMaterial((x, y) => Speckle(x, y, 6) ? new Color(0.50f, 0.50f, 0.52f) : new Color(0.68f, 0.68f, 0.70f));
        materials[Timer] = MakePixelMaterial(TimerPixel);
        signalOff = MakePixelMaterial((x, y) => x == 0 || y == 0 || x == 7 || y == 7 ? new Color(0.35f, 0.30f, 0.20f) : new Color(0.55f, 0.50f, 0.30f));
        signalOn = MakeGlowingMaterial((x, y) => x == 0 || y == 0 || x == 7 || y == 7 ? new Color(0.80f, 0.70f, 0.30f) : new Color(1.00f, 0.95f, 0.55f));
        materials[SignalLight] = signalOff;
        materials[NoteBlock] = MakePixelMaterial(NotePixel);
        Material pistonBody = MakePixelMaterial((x, y) => Speckle(x, y, 3) ? new Color(0.36f, 0.36f, 0.38f) : new Color(0.52f, 0.52f, 0.54f));
        for (byte p = PistonNorth; p <= PistonWest; p++) materials[p] = pistonBody;
        pistonHead = materials[Planks];
        for (int d = 0; d < 4; d++)
        {
            int direction = d;
            materials[ConveyorNorth + d] = MakePixelMaterial((x, y) => ConveyorPixel(x, y, direction));
        }

        // Pale gold and sky blue, with twinkling white points
        materials[StarCrystal] = MakeGlowingMaterial((x, y) =>
        {
            bool sparkle = (x == 2 && y == 5) || (x == 5 && y == 2) || (x == 4 && y == 6) || (x == 1 && y == 1);
            if (sparkle) return Color.white;
            bool middle = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) < 3f;
            return middle ? new Color(1.00f, 0.92f, 0.55f) : new Color(0.55f, 0.80f, 1.00f);
        });
        starBeam = MakeSeeThroughMaterial(new Color(0.75f, 0.90f, 1.00f, 0.28f));

        Color[] rainbow =
        {
            new Color(1.00f, 0.20f, 0.20f), new Color(1.00f, 0.55f, 0.10f), new Color(1.00f, 0.92f, 0.20f),
            new Color(0.30f, 0.85f, 0.30f), new Color(0.25f, 0.55f, 1.00f), new Color(0.65f, 0.30f, 0.95f)
        };
        for (int band = 0; band < rainbow.Length; band++)
        {
            Color colour = rainbow[band];
            materials[RainbowRed + band] = MakeGlowingMaterial((x, y) =>
                x == 0 || y == 0 || x == 7 || y == 7 ? colour * 0.85f : colour);   // a slightly darker edge, so you can see each block
        }

        TntFlashMaterial  = MakeMaterial(Color.white);
        ExplosionMaterial = MakeSeeThroughMaterial(new Color(1f, 0.55f, 0.1f, 0.7f));
        CreateFurnitureMaterials();   // (Furniture.cs)
        CreateMoreMaterials();        // (MoreBlocks.cs)
        CreateBuildingMaterials();    // (Building.cs)

        // Borrow Unity's built-in cube shape so every block can share it
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
    }

    public static Material MakeMaterial(Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Diffuse");

        Material mat = new Material(sh);
        mat.color = color;
        mat.enableInstancing = true;   // lets Unity draw lots of cubes in one go
        return mat;
    }

    // A see-through material with a picture on it (for the cracks that
    // appear on blocks as you dig them)
    public static Material MakeSeeThroughTexture(Texture2D picture)
    {
        Material mat = MakeSeeThroughMaterial(Color.white);
        mat.mainTexture = picture;
        return mat;
    }

    static Material MakeSeeThroughMaterial(Color color)
    {
        Material mat = MakeMaterial(color);

        // Switch the Standard shader to its "Fade" mode...
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");

        // ...or, if the project uses URP, its "Transparent" surface
        mat.SetFloat("_Surface", 1f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }

    // One colour with a band of another around the middle - red and white for
    // TNT, dark red and yellow for Mega TNT
    static Material MakeTntMaterial(Color body, Color band)
    {
        return MakePixelMaterial((x, y) => y >= 3 && y <= 4 ? band : body);
    }

    // The material a block is drawn with, so the inventory can show it too
    public Material MaterialFor(byte type)
    {
        return type < materials.Length ? materials[type] : null;
    }

    // An 8x8 texture where we choose the colour of every pixel
    // ("edged" darkens the outside ring of pixels a little, so a wall of the
    // same block shows a faint grid)
    static Material MakePixelMaterial(System.Func<int, int, Color> pixel, bool edged = false)
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;   // keep the pixels crisp and blocky

        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Color colour = pixel(x, y);
                if (edged && (x == 0 || y == 0 || x == 7 || y == 7))
                    colour = new Color(colour.r * 0.82f, colour.g * 0.82f, colour.b * 0.82f, colour.a);
                texture.SetPixel(x, y, colour);
            }
        texture.Apply();

        Material mat = MakeMaterial(Color.white);
        mat.mainTexture = texture;
        return mat;
    }

    // A scattering of pixels that looks random but is the same every time.
    // Bigger "rarity" means fewer of them.
    // A jumbled-up number from 0 to 1 for each pixel - the same every time,
    // so patterns look random but never change
    static float Jumble(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    // Grass: green, with lighter and darker blades
    static Color GrassPixel(int x, int y)
    {
        float n = Jumble(x, y, 1);
        if (n < 0.18f) return new Color(0.24f, 0.55f, 0.17f);
        if (n > 0.80f) return new Color(0.50f, 0.84f, 0.32f);
        return new Color(0.36f, 0.70f, 0.24f);
    }

    // Dirt: brown, with little pebbles
    static Color DirtPixel(int x, int y)
    {
        float n = Jumble(x, y, 2);
        if (n < 0.15f) return new Color(0.36f, 0.24f, 0.13f);
        if (n > 0.88f) return new Color(0.62f, 0.48f, 0.32f);
        return new Color(0.50f, 0.35f, 0.20f);
    }

    // Stone: smooth grey, with a couple of dark cracks
    static Color StonePixel(int x, int y)
    {
        bool crack = (y == 5 && x >= 1 && x <= 3) || (y == 4 && x == 4) || (y == 2 && x >= 5 && x <= 6) || (y == 1 && x == 3);
        if (crack) return new Color(0.40f, 0.40f, 0.43f);
        return Jumble(x, y, 3) > 0.85f ? new Color(0.62f, 0.62f, 0.65f) : new Color(0.55f, 0.55f, 0.58f);
    }

    // Cobblestone: lumpy stones with dark gaps between them
    static Color CobblePixel(int x, int y)
    {
        int shift = (y / 3) % 2 == 0 ? 0 : 2;
        bool gap = y % 3 == 0 || (x + shift) % 4 == 0;
        if (gap) return new Color(0.30f, 0.30f, 0.32f);
        return Jumble(x, y, 4) > 0.6f ? new Color(0.66f, 0.66f, 0.68f) : new Color(0.52f, 0.52f, 0.55f);
    }

    // Tree bark: up-and-down stripes of dark and light brown
    static Color BarkPixel(int x, int y)
    {
        bool groove = x == 2 || x == 5 || (x == 6 && y % 3 == 0);
        if (groove) return new Color(0.30f, 0.20f, 0.10f);
        return Jumble(x, y, 5) > 0.7f ? new Color(0.55f, 0.40f, 0.23f) : new Color(0.45f, 0.32f, 0.18f);
    }

    // Sand: pale yellow, with tiny grains
    static Color SandPixel(int x, int y)
    {
        float n = Jumble(x, y, 6);
        if (n < 0.12f) return new Color(0.76f, 0.69f, 0.45f);
        if (n > 0.90f) return new Color(0.95f, 0.90f, 0.70f);
        return new Color(0.86f, 0.80f, 0.55f);
    }

    // Leaves: dark green and bunched up, with gaps and bright leaf tips
    static Color LeafPixel(int x, int y)
    {
        float n = Jumble(x, y, 7);
        if (n < 0.20f) return new Color(0.08f, 0.28f, 0.07f);   // a gap between leaves
        if (n > 0.78f) return new Color(0.30f, 0.62f, 0.20f);
        return new Color(0.17f, 0.45f, 0.13f);
    }

    static bool Speckle(int x, int y, int rarity)
    {
        return ((x * 7 + y * 13 + x * y) % rarity) == 0;
    }

    // Rows of bricks, each row shifted half a brick from the one below
    static Color BrickPixel(int x, int y)
    {
        Color mortar = new Color(0.78f, 0.75f, 0.70f);
        if (y % 4 == 3) return mortar;
        if (x == ((y / 4) % 2 == 0 ? 0 : 4)) return mortar;
        return new Color(0.65f, 0.25f, 0.18f);
    }

    // Long boards with dark lines between them, and the odd join
    static Color PlankPixel(int x, int y)
    {
        Color line = new Color(0.50f, 0.36f, 0.20f);
        if (y % 4 == 3) return line;
        if (x == ((y / 4) % 2 == 0 ? 2 : 6)) return line;
        return new Color(0.75f, 0.58f, 0.34f);
    }

    // Wooden boards with a dark frame and a cross, like a worktop marked into squares
    static Color CraftingTablePixel(int x, int y)
    {
        if (x == 0 || y == 0 || x == 7 || y == 7 || x == 4 || y == 4)
            return new Color(0.38f, 0.25f, 0.13f);
        return PlankPixel(x, y);
    }

    // Stripes of every colour of the rainbow
    static Color RainbowPixel(int x, int y)
    {
        Color[] stripes =
        {
            new Color(0.90f, 0.15f, 0.15f), new Color(1.00f, 0.55f, 0.10f), new Color(1.00f, 0.90f, 0.15f),
            new Color(0.25f, 0.80f, 0.25f), new Color(0.20f, 0.55f, 0.95f), new Color(0.55f, 0.25f, 0.85f)
        };
        return stripes[Mathf.Min(y * 6 / 8, 5)];
    }

    // Wooden shelves top and bottom, with rows of coloured books in between
    static Color BookshelfPixel(int x, int y)
    {
        if (y == 0 || y == 7 || y == 4) return PlankPixel(x, 0);
        Color[] books =
        {
            new Color(0.70f, 0.15f, 0.15f), new Color(0.20f, 0.35f, 0.70f), new Color(0.20f, 0.55f, 0.25f),
            new Color(0.85f, 0.75f, 0.30f), new Color(0.45f, 0.20f, 0.50f)
        };
        return books[(x * 3 + (y > 4 ? 2 : 0)) % books.Length];
    }

    // A wooden box with dark edges, a line where the lid opens, and a gold latch
    static Color ChestPixel(int x, int y)
    {
        Color dark = new Color(0.32f, 0.20f, 0.10f);
        if ((x == 3 || x == 4) && (y == 4 || y == 5)) return new Color(1.00f, 0.82f, 0.25f);   // the latch
        if (x == 0 || y == 0 || x == 7 || y == 7 || y == 5) return dark;
        return y % 2 == 0 ? new Color(0.62f, 0.42f, 0.20f) : new Color(0.68f, 0.47f, 0.24f);
    }

    // Wooden boards in a dark frame. The top half has a little window.
    static Color DoorPixel(int x, int y, bool top)
    {
        Color frame = new Color(0.38f, 0.25f, 0.13f);
        if (x == 0 || x == 7 || (top ? y == 7 : y == 0)) return frame;
        if (top && x >= 2 && x <= 5 && y >= 2 && y <= 5)
            return x == 2 || x == 5 || y == 2 || y == 5 ? frame : new Color(0.70f, 0.85f, 0.95f);
        if (!top && x == 6 && y == 6) return new Color(0.20f, 0.20f, 0.22f);   // the handle
        return new Color(0.62f, 0.45f, 0.25f);
    }

    // Springy green slime, lighter in the middle
    static Color BouncePadPixel(int x, int y)
    {
        bool edge = x == 0 || y == 0 || x == 7 || y == 7;
        bool middle = x >= 2 && x <= 5 && y >= 2 && y <= 5;
        if (edge) return new Color(0.25f, 0.60f, 0.20f);
        return middle ? new Color(0.65f, 1.00f, 0.55f) : new Color(0.45f, 0.85f, 0.35f);
    }

    // A warm yellow light in a dark frame. "Emission" makes it glow even in a
    // dark cave, and each lamp also gets a real light (see CreateCube).
    static Material MakeLampMaterial()
    {
        return MakeGlowingMaterial((x, y) =>
        {
            if (x == 0 || y == 0 || x == 7 || y == 7) return new Color(0.30f, 0.22f, 0.12f);
            bool middle = x >= 2 && x <= 5 && y >= 2 && y <= 5;
            return middle ? new Color(1.00f, 0.97f, 0.75f) : new Color(1.00f, 0.80f, 0.35f);
        });
    }

    private Material[] flameMaterials;
    private Material cableOff, cableOn, dispenserMouth, signalOff, signalOn, pistonHead;

    // A clock face on a stone block
    static Color TimerPixel(int x, int y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
        if (d < 3.2f)
        {
            bool hand = (x == 3 || x == 4) && y >= 3 && y <= 6 || (y == 3 || y == 4) && x >= 4 && x <= 6;
            if (hand) return new Color(0.10f, 0.10f, 0.10f);
            return d > 2.4f ? new Color(0.85f, 0.65f, 0.20f) : new Color(0.95f, 0.95f, 0.90f);
        }
        return new Color(0.55f, 0.55f, 0.57f);
    }

    // A musical note on dark wood
    static Color NotePixel(int x, int y)
    {
        bool note = (x == 5 && y >= 2 && y <= 6) || (y == 6 && x >= 5 && x <= 6) ||
                    (x >= 3 && x <= 5 && y >= 1 && y <= 2);
        if (note) return new Color(0.95f, 0.90f, 0.40f);
        return y % 3 == 0 ? new Color(0.30f, 0.18f, 0.10f) : new Color(0.38f, 0.24f, 0.13f);
    }

    // A dark belt with a yellow arrow, pointing whichever way (0 north, 1 east, 2 south, 3 west)
    static Color ConveyorPixel(int x, int y, int direction)
    {
        // Turn the pixel round so we can always draw an arrow pointing "up"
        int ax = x, ay = y;
        if (direction == 1) { ax = y; ay = x; }
        if (direction == 2) { ax = 7 - x; ay = 7 - y; }
        if (direction == 3) { ax = y; ay = 7 - x; }

        bool stem = ax >= 3 && ax <= 4 && ay <= 3;
        bool head = ay >= 3 && Mathf.Abs(ax - 3.5f) <= 7 - ay;
        if (stem || head) return new Color(0.95f, 0.80f, 0.15f);
        return ay % 2 == 0 ? new Color(0.18f, 0.18f, 0.20f) : new Color(0.24f, 0.24f, 0.26f);
    }

    // A pixel texture that shines by itself
    static Material MakeGlowingMaterial(System.Func<int, int, Color> pixel)
    {
        Material mat = MakePixelMaterial(pixel);
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", mat.mainTexture);
        mat.SetColor("_EmissionColor", Color.white);
        return mat;
    }

    // A glowing swirl of two colours. Update slides it along, so it seems to churn.
    static Material MakeSwirlMaterial(Color dark, Color light)
    {
        return MakeGlowingMaterial((x, y) =>
        {
            float swirl = Mathf.Sin((x + y) * 0.8f) + Mathf.Sin((x - y) * 1.3f);
            return Color.Lerp(dark, light, (swirl + 2f) / 4f);
        });
    }

    void Update()
    {
        TickMoreBlocks();   // falling sand, growing wheat, furnaces (MoreBlocks.cs)

        Vector2 drift = new Vector2(Time.time * 0.25f, Time.time * 0.15f);
        materials[Portal].mainTextureOffset = drift;
        materials[MagicPortal].mainTextureOffset = -drift;
        materials[FirePortal].mainTextureOffset = new Vector2(drift.y, -drift.x);
        materials[Lava].mainTextureOffset = drift * 0.2f;   // slowly churning

        // Conveyor belts roll along the way their arrows point
        float roll = Time.time * 0.8f;
        materials[ConveyorNorth].mainTextureOffset = new Vector2(0f, -roll);
        materials[ConveyorEast].mainTextureOffset = new Vector2(-roll, 0f);
        materials[ConveyorSouth].mainTextureOffset = new Vector2(0f, roll);
        materials[ConveyorWest].mainTextureOffset = new Vector2(roll, 0f);
    }

    // Nearly clear in the middle, with a pale frame and a glint across it
    static Material MakeGlassMaterial()
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                bool frame = x == 0 || y == 0 || x == 7 || y == 7;
                bool glint = x - y == 2 && x < 6;
                float alpha = frame ? 0.9f : glint ? 0.6f : 0.12f;
                texture.SetPixel(x, y, new Color(0.85f, 0.95f, 1f, alpha));
            }
        }
        texture.Apply();

        Material mat = MakeSeeThroughMaterial(Color.white);
        mat.mainTexture = texture;
        return mat;
    }

    // --- Making the world ---

    float RandomRange(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    void GenerateHills()
    {
        // Perlin noise gives smooth random hills. Shifting where we read it
        // from by a random amount is what makes each seed look different.
        float offsetX = random.Next(0, 10000);
        float offsetZ = random.Next(0, 10000);

        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                // Big gentle hills, with small bumps on top of them...
                float hills = Mathf.PerlinNoise(offsetX + x * 0.03f, offsetZ + z * 0.03f);
                float bumps = Mathf.PerlinNoise(offsetX + 500f + x * 0.12f, offsetZ + 500f + z * 0.12f);

                // ...and here and there, where this much wider noise is high, mountains
                float mountains = Mathf.PerlinNoise(offsetX + 900f + x * 0.015f, offsetZ + 900f + z * 0.015f);
                float peaks = Mathf.Max(0f, mountains - 0.55f) * 45f;

                int height = 4 + Mathf.RoundToInt(hills * 20f + bumps * 3f + peaks);
                height = Mathf.Min(height, SizeY - 8);   // leave room for a tree on top
                groundHeight[x, z] = height;

                // Land that only just pokes out of the sea becomes beach,
                // and the tops of the mountains have snow on them
                bool beach = height <= SeaLevel + 1;
                bool snowy = height >= SeaLevel + 19;

                for (int y = 0; y <= height; y++)
                {
                    if (y == height)          blocks[x, y, z] = beach ? Sand : snowy ? Snow : Grass;
                    else if (y >= height - 3) blocks[x, y, z] = beach ? Sand : Dirt;
                    else                      blocks[x, y, z] = Stone;
                }
            }
        }
    }

    // Caves are dug by "worms": each one starts somewhere underground and
    // wriggles along, hollowing out a ball of blocks at every step.
    void CarveCaves()
    {
        int worms = SizeX * SizeZ / 700;

        for (int i = 0; i < worms; i++)
        {
            Vector3 pos = new Vector3(
                RandomRange(0, SizeX),
                RandomRange(3, SeaLevel + 8),
                RandomRange(0, SizeZ));

            float yaw = RandomRange(0, 360);
            float pitch = RandomRange(-20, 20);
            int length = random.Next(60, 140);

            for (int step = 0; step < length; step++)
            {
                // The tunnel swells and narrows as it goes
                float radius = 1.6f + 0.6f * Mathf.Sin(step * 0.15f + i);
                CarveBall(pos, radius);

                yaw += RandomRange(-25, 25);
                pitch = Mathf.Clamp(pitch + RandomRange(-10, 10), -35, 35);
                if (pos.y < 4) pitch = -Mathf.Abs(pitch);   // too deep - head back up

                pos += Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
            }
        }
    }

    void CarveBall(Vector3 centre, float radius)
    {
        int r = Mathf.CeilToInt(radius);
        int cx = Mathf.RoundToInt(centre.x);
        int cy = Mathf.RoundToInt(centre.y);
        int cz = Mathf.RoundToInt(centre.z);

        for (int x = cx - r; x <= cx + r; x++)
        for (int y = cy - r; y <= cy + r; y++)
        for (int z = cz - r; z <= cz + r; z++)
        {
            if (!InBounds(x, y, z)) continue;
            if (y < 1) continue;   // keep a floor under the world

            // Don't dig near the top of land that the sea will cover,
            // or the water would end up floating over a hole
            if (groundHeight[x, z] <= SeaLevel + 1 && y > groundHeight[x, z] - 3) continue;

            if (Vector3.Distance(new Vector3(x, y, z), centre) <= radius)
                blocks[x, y, z] = Air;
        }
    }

    // Where a cave cut through the top of a hill, the dirt it uncovered
    // would look bare - so give it a fresh layer of grass
    void RegrowGrass()
    {
        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                int top = GetSurfaceHeight(x, z);
                if (top >= 0 && blocks[x, top, z] == Dirt)
                    blocks[x, top, z] = Grass;
            }
        }
    }

    void FillSea()
    {
        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
                for (int y = groundHeight[x, z] + 1; y <= SeaLevel; y++)
                    blocks[x, y, z] = Water;
    }

    void PlantTrees()
    {
        // Stay a couple of blocks away from the edge so leaves don't get cut off
        for (int x = 2; x < SizeX - 2; x++)
        {
            for (int z = 2; z < SizeZ - 2; z++)
            {
                if (random.NextDouble() > 0.02) continue;   // about 1 spot in 50

                int top = GetSurfaceHeight(x, z);
                if (top < 0 || blocks[x, top, z] != Grass) continue;
                if (TreeNearby(x, z)) continue;

                GrowTree(x, top + 1, z);
            }
        }
    }

    bool TreeNearby(int x, int z)
    {
        for (int dx = -3; dx <= 3; dx++)
            for (int dz = -3; dz <= 3; dz++)
                for (int y = 0; y < SizeY; y++)
                    if (GetBlock(x + dx, y, z + dz) == Wood)
                        return true;
        return false;
    }

    void GrowTree(int x, int y, int z)
    {
        Trees.Add(new Vector3Int(x, y, z));

        int trunkHeight = random.Next(4, 6);
        for (int i = 0; i < trunkHeight; i++)
            blocks[x, y + i, z] = Wood;

        int top = y + trunkHeight - 1;

        // Two wide layers of leaves around the top of the trunk...
        for (int ly = top - 1; ly <= top; ly++)
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    bool corner = Mathf.Abs(dx) == 2 && Mathf.Abs(dz) == 2;
                    if (corner && random.NextDouble() < 0.5) continue;   // ragged edges look more natural
                    AddLeaf(x + dx, ly, z + dz);
                }

        // ...then a smaller cap, ending in a plus shape at the very top
        for (int ly = top + 1; ly <= top + 2; ly++)
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (ly == top + 2 && dx != 0 && dz != 0) continue;
                    AddLeaf(x + dx, ly, z + dz);
                }
    }

    // Grows a whole tree right now, from a sapling you placed. Returns false
    // if there isn't room for the trunk.
    public bool GrowTreeAt(int x, int y, int z)
    {
        if (x < 2 || x >= SizeX - 2 || z < 2 || z >= SizeZ - 2 || y + 8 >= SizeY) return false;

        for (int i = 0; i < 6; i++)
        {
            byte there = GetBlock(x, y + i, z);
            if (there != Air && there != Water) return false;
        }

        if (random == null) random = new System.Random();   // a loaded world never made one
        GrowTree(x, y, z);

        // GrowTree only fills in the grid, so now redraw everything around it
        for (int cx = x - 3; cx <= x + 3; cx++)
            for (int cz = z - 3; cz <= z + 3; cz++)
            {
                MarkChanged(cx, cz);
                for (int cy = y - 1; cy <= y + 8; cy++)
                    RefreshSpecial(cx, cy, cz);
            }
        return true;
    }

    void AddLeaf(int x, int y, int z)
    {
        if (InBounds(x, y, z) && blocks[x, y, z] == Air)
            blocks[x, y, z] = Leaves;
    }

    // --- The Magic Land ---

    // Floating islands in the sky, with glowing crystals and clouds, and
    // diamonds and gold to find inside the rock
    public void GenerateMagicLand()
    {
        int seed = System.Environment.TickCount;
        random = new System.Random(seed);
        Debug.Log("Magic land seed: " + seed);

        MakeEmptyWorld(NewSizeX, NewSizeY, NewSizeZ);
        int area = SizeX * SizeZ;

        // The island you arrive on, in the middle, then others scattered
        // around it - more of them in a bigger world
        List<Island> islands = new List<Island>();
        islands.Add(MakeIsland(SizeX / 2, SizeZ / 2, SizeY / 2, 8));
        for (int i = 0; i < area / 400; i++)
            islands.Add(MakeIsland(random.Next(6, SizeX - 6), random.Next(6, SizeZ - 6),
                                   random.Next(SizeY / 4, SizeY * 2 / 3), random.Next(3, 9)));

        // Fun things to find on the islands. The arrival island gets a
        // mushroom to bounce on straight away; the others take their chances.
        for (int i = 0; i < islands.Count; i++)
        {
            Island island = islands[i];
            if (i == 0 || random.NextDouble() < 0.35) MakeGiantMushroom(island);
            if (i != 0 && random.NextDouble() < 0.30) HideTreasure(island);
            if (random.NextDouble() < 0.30) MakeWaterfall(island);
            if (island.radius >= 6 && random.NextDouble() < 0.25)
                BuildRainbow(new Vector3Int(island.x - 8, island.top + 1, island.z), Vector3.right);
        }

        for (int i = 0; i < area / 600; i++)
        {
            int x = random.Next(4, SizeX - 4), z = random.Next(4, SizeZ - 4);
            if (Mathf.Abs(x - SizeX / 2) < 10 && Mathf.Abs(z - SizeZ / 2) < 10) continue;   // keep the sky over the arrival island clear
            MakeCloud(x, random.Next(SizeY - 10, SizeY - 6), z);
        }

        BuildAllCubes();
    }

    // Where an island is, so we can put things on it afterwards
    struct Island
    {
        public int x, z, top, radius;
    }

    // An island is like an upside-down hill: a grassy top, and rock below
    // that narrows to a point
    Island MakeIsland(int cx, int cz, int top, int radius)
    {
        float offset = random.Next(0, 10000);

        for (int x = cx - radius - 2; x <= cx + radius + 2; x++)
        {
            for (int z = cz - radius - 2; z <= cz + radius + 2; z++)
            {
                if (!InBounds(x, 0, z)) continue;

                // A wobbly edge, so islands aren't perfect circles
                float wobble = (Mathf.PerlinNoise(offset + x * 0.3f, offset + z * 0.3f) - 0.5f) * 0.5f;
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)) / radius + wobble;
                if (d >= 1f) continue;

                int depth = 1 + Mathf.RoundToInt((1f - d) * radius * 1.2f);
                for (int y = Mathf.Max(top - depth, 1); y <= top; y++)
                {
                    byte type = y == top ? MagicGrass : y >= top - 1 ? Dirt : Stone;
                    if (type == Stone)
                    {
                        double treasure = random.NextDouble();
                        if (treasure < 0.03) type = Diamond;
                        else if (treasure < 0.07) type = Gold;
                        else if (treasure < 0.10) type = Crystal;
                    }
                    blocks[x, y, z] = type;
                }
            }
        }

        // Bigger islands get a crystal tree
        if (radius >= 5)
        {
            int tx = cx + random.Next(-2, 3), tz = cz + random.Next(-2, 3);
            int ground = GetSurfaceHeight(tx, tz);
            if (ground >= 0 && blocks[tx, ground, tz] == MagicGrass && ground + 7 < SizeY)
                MakeCrystalTree(tx, ground + 1, tz);
        }

        return new Island { x = cx, z = cz, top = top, radius = radius };
    }

    // A spot on top of an island's grass, with room above it
    bool GrassSpotOn(Island island, out Vector3Int spot)
    {
        for (int tries = 0; tries < 25; tries++)
        {
            int x = island.x + random.Next(-island.radius + 1, island.radius);
            int z = island.z + random.Next(-island.radius + 1, island.radius);
            int y = GetSurfaceHeight(x, z);

            if (y >= 0 && y + 8 < SizeY && blocks[x, y, z] == MagicGrass && blocks[x, y + 1, z] == Air)
            {
                spot = new Vector3Int(x, y, z);
                return true;
            }
        }

        spot = Vector3Int.zero;
        return false;
    }

    // A tall pale stalk with a big spotty red cap that you can bounce on
    void MakeGiantMushroom(Island island)
    {
        Vector3Int ground;
        if (!GrassSpotOn(island, out ground)) return;

        int stem = random.Next(3, 6);
        for (int i = 1; i <= stem; i++)
            SetIfEmpty(ground.x, ground.y + i, ground.z, MushroomStem);

        // A wide round cap, and a smaller dome on top of it
        int capY = ground.y + stem + 1;
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                if (Mathf.Abs(dx) == 2 && Mathf.Abs(dz) == 2) continue;   // round off the corners
                SetIfEmpty(ground.x + dx, capY, ground.z + dz, MushroomCap);
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1)
                    SetIfEmpty(ground.x + dx, capY + 1, ground.z + dz, MushroomCap);
            }
    }

    // A chest full of treasure, sitting out on the grass
    void HideTreasure(Island island)
    {
        Vector3Int ground;
        if (!GrassSpotOn(island, out ground)) return;

        Vector3Int cell = ground + Vector3Int.up;
        blocks[cell.x, cell.y, cell.z] = Chest;

        byte[] loot =
        {
            Diamond, Diamond, Gold, Gold, Crystal, Rainbow, Lamp, BouncePad, MegaTnt, Portal,
            RainbowRed, RainbowYellow, RainbowBlue, RainbowPurple, MushroomCap, Obsidian
        };

        ChestContents inside = ChestAt(cell);
        int things = random.Next(3, 7);
        for (int i = 0; i < things; i++)
        {
            int space = random.Next(0, ChestContents.Spaces);
            inside.items[space] = loot[random.Next(0, loot.Length)];
            inside.counts[space] = (byte)random.Next(1, 16);
        }
    }

    // Water pouring off the edge of an island, down into the sky below.
    // You can swim up and down it!
    void MakeWaterfall(Island island)
    {
        // Walk out from the middle until we step off the edge of the island
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        for (int step = 1; step <= island.radius + 3; step++)
        {
            int x = island.x + Mathf.RoundToInt(Mathf.Cos(angle) * step);
            int z = island.z + Mathf.RoundToInt(Mathf.Sin(angle) * step);
            if (!InBounds(x, island.top, z)) return;
            if (blocks[x, island.top, z] != Air) continue;   // still on the island

            for (int y = island.top; y >= Mathf.Max(1, island.top - 18); y--)
            {
                if (blocks[x, y, z] != Air) break;   // landed on something
                blocks[x, y, z] = Water;
            }
            return;
        }
    }

    // A short trunk with a glowing cluster of crystals on top
    void MakeCrystalTree(int x, int y, int z)
    {
        int trunk = random.Next(3, 5);
        for (int i = 0; i < trunk; i++)
            blocks[x, y + i, z] = Wood;

        int top = y + trunk;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                SetIfEmpty(x + dx, top, z + dz, Crystal);                       // a 3x3 layer...
                if (dx == 0 || dz == 0) SetIfEmpty(x + dx, top + 1, z + dz, Crystal);   // ...a plus shape...
            }
        SetIfEmpty(x, top + 2, z, Crystal);                                     // ...and a point
    }

    // A flat, fluffy blob you can walk on
    void MakeCloud(int cx, int y, int cz)
    {
        int length = random.Next(3, 7), wide = random.Next(2, 4);
        for (int x = cx - length; x <= cx + length; x++)
            for (int z = cz - wide; z <= cz + wide; z++)
            {
                float dx = (x - cx) / (float)length, dz = (z - cz) / (float)wide;
                if (dx * dx + dz * dz <= 1f) SetIfEmpty(x, y, z, Cloud);
                if (dx * dx + dz * dz <= 0.4f) SetIfEmpty(x, y + 1, z, Cloud);   // puffier in the middle
            }
    }

    void SetIfEmpty(int x, int y, int z, byte type)
    {
        if (InBounds(x, y, z) && blocks[x, y, z] == Air) blocks[x, y, z] = type;
    }

    // Sets a magic portal into the ground a couple of steps from "near",
    // so there's always a way home from the Magic Land
    // Builds a big glowing rainbow arch out of blocks, starting at "foot" and
    // arching over in the direction you're facing. Red is on the outside,
    // purple on the inside, and it's three blocks wide so you can walk over
    // it. It only fills empty space, so it never knocks down anything you built.
    public void BuildRainbow(Vector3Int foot, Vector3 facing)
    {
        const int radius = 8;
        const int bands = 6;
        const int width = 3;

        // Arch along whichever way (X or Z) you're mostly facing
        bool alongX = Mathf.Abs(facing.x) > Mathf.Abs(facing.z);
        Vector3Int ahead = alongX ? new Vector3Int(facing.x > 0f ? 1 : -1, 0, 0) : new Vector3Int(0, 0, facing.z > 0f ? 1 : -1);
        Vector3Int across = alongX ? new Vector3Int(0, 0, 1) : new Vector3Int(1, 0, 0);

        for (int step = 0; step <= radius * 2; step++)
        {
            for (int up = 0; up <= radius; up++)
            {
                // How far this spot is from the middle of the arch, at ground level
                float distance = Mathf.Sqrt((step - radius) * (step - radius) + up * up);
                int band = radius - Mathf.RoundToInt(distance);   // 0 is the outside edge
                if (band < 0 || band >= bands) continue;

                for (int side = -(width / 2); side <= width / 2; side++)
                {
                    Vector3Int cell = foot + ahead * step + Vector3Int.up * up + across * side;
                    if (GetBlock(cell.x, cell.y, cell.z) == Air && InBounds(cell.x, cell.y, cell.z))
                        SetBlock(cell.x, cell.y, cell.z, (byte)(RainbowRed + band));
                }
            }
        }
    }

    // (It sets a fire portal into the ash in the Nether, the same way.)
    public void PlaceReturnPortal(Vector3 near, byte portal, byte ground)
    {
        int nx = Mathf.RoundToInt(near.x), nz = Mathf.RoundToInt(near.z);
        int[,] tries = { { 2, 0 }, { 0, 2 }, { -2, 0 }, { 0, -2 }, { 1, 0 }, { 0, 1 }, { 2, 2 }, { -2, -2 } };

        for (int i = 0; i < tries.GetLength(0); i++)
        {
            int x = nx + tries[i, 0], z = nz + tries[i, 1];
            int y = GetSurfaceHeight(x, z);
            if (y >= 0 && GetBlock(x, y, z) == ground)
            {
                SetBlock(x, y, z, portal);
                return;
            }
        }

        // Nowhere better? Then right where you'd stand.
        SetBlock(nx, Mathf.RoundToInt(near.y - 0.6f), nz, portal);
    }

    // --- The Nether ---

    // Rocky red hills dusted with ash, around a glowing sea of lava, with
    // tall obsidian spires topped with fire crystals, and fire vents that
    // shoot out flames (NetherFire.cs makes the flames)
    public void GenerateNether()
    {
        int seed = System.Environment.TickCount;
        random = new System.Random(seed);
        Debug.Log("Nether seed: " + seed);

        MakeEmptyWorld(NewSizeX, NewSizeY, NewSizeZ);
        const int lavaLevel = 9;
        float offsetX = random.Next(0, 10000), offsetZ = random.Next(0, 10000);

        for (int x = 0; x < SizeX; x++)
        {
            for (int z = 0; z < SizeZ; z++)
            {
                // Lumpy hills, with jagged ridges here and there
                float hills = Mathf.PerlinNoise(offsetX + x * 0.04f, offsetZ + z * 0.04f);
                float bumps = Mathf.PerlinNoise(offsetX + 300f + x * 0.15f, offsetZ + 300f + z * 0.15f);
                float ridges = Mathf.PerlinNoise(offsetX + 700f + x * 0.02f, offsetZ + 700f + z * 0.02f);

                int height = 3 + Mathf.RoundToInt(hills * 16f + bumps * 4f + Mathf.Max(0f, ridges - 0.6f) * 50f);
                height = Mathf.Min(height, SizeY - 10);

                for (int y = 0; y <= height; y++)
                {
                    byte type = EmberRock;
                    if (y == height && height > lavaLevel) type = Ash;
                    else if (y < height - 2)
                    {
                        // Treasure in the rock: gold, and the odd fire crystal
                        double treasure = random.NextDouble();
                        if (treasure < 0.015) type = Gold;
                        else if (treasure < 0.025) type = FireCrystal;
                    }
                    blocks[x, y, z] = type;
                }

                // Low ground becomes a lava lake
                for (int y = height + 1; y <= lavaLevel; y++)
                    blocks[x, y, z] = Lava;
            }
        }

        // Tall obsidian spires, each topped with glowing fire crystals
        int area = SizeX * SizeZ;
        for (int i = 0; i < area / 600; i++)
        {
            int x = random.Next(3, SizeX - 3), z = random.Next(3, SizeZ - 3);
            if (Mathf.Abs(x - SizeX / 2) < 6 && Mathf.Abs(z - SizeZ / 2) < 6) continue;   // keep the arrival spot clear
            int ground = GetSurfaceHeight(x, z);
            int tall = random.Next(6, 16);
            int top = Mathf.Min(ground + tall, SizeY - 3);

            for (int y = ground + 1; y <= top; y++)
            {
                blocks[x, y, z] = Obsidian;
                if (y < ground + tall / 2) SetIfEmpty(x + 1, y, z, Obsidian);   // thicker at the bottom
            }
            SetIfEmpty(x, top + 1, z, FireCrystal);
            SetIfEmpty(x + 1, top, z, FireCrystal);
            SetIfEmpty(x - 1, top, z, FireCrystal);
            SetIfEmpty(x, top, z + 1, FireCrystal);
            SetIfEmpty(x, top, z - 1, FireCrystal);
        }

        // Fires burning here and there on the ash
        for (int i = 0; i < area / 150; i++)
        {
            int x = random.Next(2, SizeX - 2), z = random.Next(2, SizeZ - 2);
            int y = GetSurfaceHeight(x, z);
            if (y >= 0 && y + 1 < SizeY && blocks[x, y, z] == Ash && (Mathf.Abs(x - SizeX / 2) > 5 || Mathf.Abs(z - SizeZ / 2) > 5))
                blocks[x, y + 1, z] = Fire;
        }

        // Fire vents, set into the ash
        for (int i = 0; i < area / 250; i++)
        {
            int x = random.Next(2, SizeX - 2), z = random.Next(2, SizeZ - 2);
            int y = GetSurfaceHeight(x, z);
            if (y >= 0 && blocks[x, y, z] == Ash && (Mathf.Abs(x - SizeX / 2) > 3 || Mathf.Abs(z - SizeZ / 2) > 3))
                blocks[x, y, z] = FireVent;
        }

        // Make sure there's dry ground to arrive on, in the middle
        int middleX = SizeX / 2, middleZ = SizeZ / 2;
        int floor = Mathf.Max(GetSurfaceHeight(middleX, middleZ), lavaLevel + 1);
        for (int x = middleX - 4; x <= middleX + 4; x++)
            for (int z = middleZ - 4; z <= middleZ + 4; z++)
            {
                for (int y = 0; y <= floor; y++)
                    if (blocks[x, y, z] == Lava || blocks[x, y, z] == Air || y == floor)
                        blocks[x, y, z] = y == floor ? Ash : EmberRock;
                for (int y = floor + 1; y < SizeY; y++)
                    if (blocks[x, y, z] != Obsidian && blocks[x, y, z] != FireCrystal) blocks[x, y, z] = Air;
            }

        BuildAllCubes();
    }

    // --- Saving and loading ---

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(SizeX);
        writer.Write(SizeY);
        writer.Write(SizeZ);

        // Every block, one byte each, in one long line
        byte[] all = new byte[SizeX * SizeY * SizeZ];
        int i = 0;
        for (int x = 0; x < SizeX; x++)
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                    all[i++] = blocks[x, y, z];
        writer.Write(all);

        // Where the trees are, so squirrels can find them again
        writer.Write(Trees.Count);
        foreach (Vector3Int tree in Trees)
        {
            writer.Write(tree.x);
            writer.Write(tree.y);
            writer.Write(tree.z);
        }

        // What's in every chest
        writer.Write(chests.Count);
        foreach (KeyValuePair<Vector3Int, ChestContents> chest in chests)
        {
            writer.Write(chest.Key.x);
            writer.Write(chest.Key.y);
            writer.Write(chest.Key.z);
            writer.Write(chest.Value.items);
            writer.Write(chest.Value.counts);
        }

        // Which note each note block plays
        writer.Write(noteTunes.Count);
        foreach (KeyValuePair<Vector3Int, byte> tune in noteTunes)
        {
            writer.Write(tune.Key.x);
            writer.Write(tune.Key.y);
            writer.Write(tune.Key.z);
            writer.Write(tune.Value);
        }

        WriteSigns(writer);   // (Building.cs)
    }

    public bool ReadFrom(BinaryReader reader)
    {
        // Worlds are the size they were made at - small ones from before
        // worlds got bigger, or big new ones
        int sizeX = reader.ReadInt32(), sizeY = reader.ReadInt32(), sizeZ = reader.ReadInt32();
        if (sizeX < 16 || sizeY < 16 || sizeZ < 16 || sizeX > 512 || sizeY > 256 || sizeZ > 512)
            return false;   // nonsense - the file must be damaged
        MakeEmptyWorld(sizeX, sizeY, sizeZ);

        byte[] all = reader.ReadBytes(SizeX * SizeY * SizeZ);
        if (all.Length != SizeX * SizeY * SizeZ) return false;   // file cut short

        int i = 0;
        for (int x = 0; x < SizeX; x++)
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                    blocks[x, y, z] = all[i++];

        Trees.Clear();
        int treeCount = reader.ReadInt32();
        for (int t = 0; t < treeCount; t++)
            Trees.Add(new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));

        // Saves from before chests existed stop here
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            int chestCount = reader.ReadInt32();
            for (int c = 0; c < chestCount; c++)
            {
                Vector3Int cell = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                ChestContents inside = ChestAt(cell);
                reader.Read(inside.items, 0, ChestContents.Spaces);
                reader.Read(inside.counts, 0, ChestContents.Spaces);
            }
        }

        // ...and saves from before note blocks stop here
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            int tuneCount = reader.ReadInt32();
            for (int t = 0; t < tuneCount; t++)
                noteTunes[new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())] = reader.ReadByte();
        }

        // Saves from before signs existed stop here
        if (reader.BaseStream.Position < reader.BaseStream.Length)
            ReadSigns(reader);

        BuildAllCubes();
        return true;
    }

    // --- Reading the world ---

    public static bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < SizeX
            && y >= 0 && y < SizeY
            && z >= 0 && z < SizeZ;
    }

    public byte GetBlock(int x, int y, int z)
    {
        return InBounds(x, y, z) ? blocks[x, y, z] : Air;
    }

    // Y of the highest block in a column (water and leaves count), or -1 if the column is empty
    public int GetSurfaceHeight(int x, int z)
    {
        for (int y = SizeY - 1; y >= 0; y--)
        {
            if (GetBlock(x, y, z) != Air)
                return y;
        }
        return -1;
    }

    // Y of the highest block in a column you could walk on, ignoring trees and
    // water, or -1 if there is none
    public int GetGroundHeight(int x, int z)
    {
        for (int y = SizeY - 1; y >= 0; y--)
        {
            byte type = GetBlock(x, y, z);
            if (type != Air && type != Water && type != Leaves && type != Wood)
                return y;
        }
        return -1;
    }

    // Y of the top of an unbroken trunk, or -1 if the bottom of it has been chopped away
    public int TrunkTop(Vector3Int tree)
    {
        if (GetBlock(tree.x, tree.y, tree.z) != Wood) return -1;

        int y = tree.y;
        while (GetBlock(tree.x, y + 1, tree.z) == Wood) y++;
        return y;
    }

    // A spot standing on dry grass (or magic grass), as close to the middle of the map as possible
    public Vector3 FindSpawnPoint()
    {
        int middleX = SizeX / 2;
        int middleZ = SizeZ / 2;

        // Check in bigger and bigger squares around the middle
        for (int ring = 0; ring < SizeX / 2; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;   // only the edge of the square

                    int x = middleX + dx;
                    int z = middleZ + dz;
                    int y = GetSurfaceHeight(x, z);

                    byte top = GetBlock(x, y, z);
                    if (y >= 0 && (top == Grass || top == MagicGrass || top == Ash))
                        return new Vector3(x, y + 0.6f, z);   // +0.5 is the top face of the block
                }
            }
        }

        return new Vector3(middleX, SizeY, middleZ);   // no grass anywhere? drop in from the sky
    }

    // --- Changing the world ---

    public void SetBlock(int x, int y, int z, byte type)
    {
        if (!InBounds(x, y, z)) return;

        // A chest or dispenser that's broken or blown up loses everything inside it
        Vector3Int here = new Vector3Int(x, y, z);
        if ((IsContainer(blocks[x, y, z]) || IsFurnace(blocks[x, y, z])) && type != blocks[x, y, z])
            chests.Remove(here);   // (furnaces keep what they're cooking in the same place)
        if (blocks[x, y, z] == NoteBlock && type != NoteBlock) noteTunes.Remove(here);
        if (type == Timer) timers.Add(here);
        else timers.Remove(here);
        if (type == StarCrystal) starCrystals.Add(here);
        else starCrystals.Remove(here);
        TrackCrop(here, type);
        TrackFurnace(here, type);
        TrackBuilding(here, type);

        blocks[x, y, z] = type;
        CheckForFalling(x, y, z);

        // Its chunk needs redrawing - and if it's on the edge of the chunk,
        // so does the one next door, whose faces it might hide or uncover
        MarkChanged(x, z);
        MarkChanged(x + 1, z);
        MarkChanged(x - 1, z);
        MarkChanged(x, z + 1);
        MarkChanged(x, z - 1);

        // This block and its six neighbours may be stairs, doors or lamps
        // that just appeared, went away, or were uncovered
        RefreshSpecial(x, y, z);
        RefreshSpecial(x + 1, y, z);
        RefreshSpecial(x - 1, y, z);
        RefreshSpecial(x, y + 1, z);
        RefreshSpecial(x, y - 1, z);
        RefreshSpecial(x, y, z + 1);
        RefreshSpecial(x, y, z - 1);
    }

    void MarkChanged(int x, int z)
    {
        if (!InBounds(x, 0, z)) return;
        chunkChanged[x / ChunkSize, z / ChunkSize] = true;
        anyChunkChanged = true;
    }

    // All the changes made this frame get drawn together, so a TNT blast
    // that changes hundreds of blocks only redraws each chunk once
    void LateUpdate()
    {
        if (!anyChunkChanged) return;
        anyChunkChanged = false;

        for (int cx = 0; cx < chunks.GetLength(0); cx++)
            for (int cz = 0; cz < chunks.GetLength(1); cz++)
                if (chunkChanged[cx, cz])
                {
                    chunkChanged[cx, cz] = false;
                    BuildChunk(cx, cz);
                }
    }

    // --- Deciding what to draw ---

    // --- Stairs and doors ---

    public static bool IsStairs(byte type) { return type >= StairsNorth && type <= StairsWest; }
    public static bool IsDoor(byte type)   { return type >= DoorFirst && type <= DoorLast; }

    public static bool DoorIsTop(byte door)    { return ((door - DoorFirst) & 1) != 0; }
    public static bool DoorIsOpen(byte door)   { return ((door - DoorFirst) & 2) != 0; }
    public static bool DoorAlongX(byte door)   { return ((door - DoorFirst) & 4) != 0; }

    public static byte MakeDoor(bool top, bool open, bool alongX)
    {
        return (byte)(DoorFirst + (top ? 1 : 0) + (open ? 2 : 0) + (alongX ? 4 : 0));
    }

    // Blocks you can partly see past, because they don't fill their whole space
    static bool Shaped(byte type)
    {
        return IsStairs(type) || IsDoor(type);
    }

    // The pieces a stairs or door block is made of, as boxes inside its space
    // (the middle of the space is 0,0,0 and it goes from -0.5 to +0.5)
    static Bounds[] ShapeOf(byte type)
    {
        if (IsStairs(type))
        {
            Vector3 high = type == StairsNorth ? Vector3.forward
                         : type == StairsEast  ? Vector3.right
                         : type == StairsSouth ? Vector3.back
                         : Vector3.left;
            Vector3 stepSize = high.x != 0f ? new Vector3(0.5f, 0.5f, 1f) : new Vector3(1f, 0.5f, 0.5f);

            return new[]
            {
                new Bounds(new Vector3(0f, -0.25f, 0f), new Vector3(1f, 0.5f, 1f)),   // the bottom step, the whole width
                new Bounds(high * 0.25f + Vector3.up * 0.25f, stepSize),             // the top step, at the back
            };
        }

        // A door is a thin panel. "Along X" means that when it's closed it
        // stretches across X, so you walk through it along Z. Open, it has
        // swung round to lie flat against one side of the doorway.
        const float thick = 0.15f;
        bool acrossX = DoorAlongX(type) != DoorIsOpen(type);   // which way the panel lies right now
        Vector3 size = acrossX ? new Vector3(1f, 1f, thick) : new Vector3(thick, 1f, 1f);
        Vector3 centre = Vector3.zero;
        if (DoorIsOpen(type))
            centre = acrossX ? new Vector3(0f, 0f, -0.42f) : new Vector3(-0.42f, 0f, 0f);

        return new[] { new Bounds(centre, size) };
    }

    // You can see through air, water and glass, and past stairs and doors,
    // so a block next to any of them is on show
    static bool SeeThrough(byte type)
    {
        return type == Air || type == Water || type == Glass || type == Fire || type == Cable || type == PressurePlate || Shaped(type) || IsFurniture(type) || IsWheat(type) || IsModel(type);
    }

    // Water is drawn where it touches air, glass (so you can build a fish
    // tank), or the gaps around stairs and doors
    static bool ShowsWater(byte type)
    {
        return type == Air || type == Glass || type == Fire || Shaped(type) || IsFurniture(type) || IsWheat(type) || IsModel(type);
    }

    // Stairs, doors and lamps are drawn one by one, not as part of a chunk
    static bool Special(byte type)
    {
        return Shaped(type) || type == Lamp || type == Fire || type == Cable || IsDispenser(type)
            || type == PressurePlate || type == SignalLight || IsPiston(type) || type == StarCrystal || IsFurniture(type) || IsWheat(type) || IsModel(type);
    }

    // A stairs, door or lamp only needs drawing if at least one side is exposed
    bool IsVisible(int x, int y, int z)
    {
        // The bottom of the world counts as solid, so we never draw
        // the underside of the ground where nobody can ever see it.
        bool openBelow = (y > 0) && SeeThrough(blocks[x, y - 1, z]);

        return openBelow
            || SeeThrough(GetBlock(x + 1, y, z))
            || SeeThrough(GetBlock(x - 1, y, z))
            || SeeThrough(GetBlock(x, y + 1, z))
            || SeeThrough(GetBlock(x, y, z + 1))
            || SeeThrough(GetBlock(x, y, z - 1));
    }

    // Draws the whole world from scratch, once it's been made or loaded
    void BuildAllCubes()
    {
        for (int cx = 0; cx < chunks.GetLength(0); cx++)
            for (int cz = 0; cz < chunks.GetLength(1); cz++)
                BuildChunk(cx, cz);

        for (int x = 0; x < SizeX; x++)
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                {
                    if (Special(blocks[x, y, z]))
                        RefreshSpecial(x, y, z);
                    if (blocks[x, y, z] == Timer)
                        timers.Add(new Vector3Int(x, y, z));
                    if (blocks[x, y, z] == StarCrystal)
                        starCrystals.Add(new Vector3Int(x, y, z));
                    TrackCrop(new Vector3Int(x, y, z), blocks[x, y, z]);
                    TrackFurnace(new Vector3Int(x, y, z), blocks[x, y, z]);
                    if (blocks[x, y, z] == Spawner) spawners.Add(new Vector3Int(x, y, z));
                }
    }

    // --- Stairs, doors and lamps ---

    void RefreshSpecial(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return;

        Vector3Int cell = new Vector3Int(x, y, z);
        byte type = blocks[x, y, z];
        bool wanted = Special(type) && IsVisible(x, y, z);

        GameObject existing;
        bool have = specials.TryGetValue(cell, out existing);

        // Gone, hidden, or turned into something else (like a door opening)?
        // (Cable is always rebuilt, because the things next to it may have
        // changed, and it reaches out to join up with them)
        if (have && (!wanted || specialBuiltAs[cell] != type || type == Cable || type == Fence))
        {
            Destroy(existing);
            specials.Remove(cell);
            have = false;
        }

        if (wanted && !have)
        {
            specials[cell] = CreateSpecial(x, y, z);
            specialBuiltAs[cell] = type;
        }
    }

    GameObject CreateSpecial(int x, int y, int z)
    {
        byte type = blocks[x, y, z];

        GameObject cube = new GameObject("Block " + x + "," + y + "," + z);
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = new Vector3(x, y, z);

        if (IsFurniture(type))
        {
            BuildFurniture(cube, type);
            return cube;
        }

        if (IsWheat(type))
        {
            BuildWheat(cube, type);
            return cube;
        }

        if (IsModel(type))
        {
            BuildModel(cube, x, y, z, type);
            return cube;
        }

        if (Shaped(type))
        {
            // One small box for each piece, with something solid to bump
            // into matching each one
            foreach (Bounds piece in ShapeOf(type))
            {
                GameObject part = new GameObject("Piece");
                part.transform.SetParent(cube.transform, false);
                part.transform.localPosition = piece.center;
                part.transform.localScale = piece.size;
                part.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = materials[type];

                BoxCollider solid = cube.AddComponent<BoxCollider>();
                solid.center = piece.center;
                solid.size = piece.size;
            }
            return cube;
        }

        // Cable: a thin strip lying on the ground, with arms reaching out to
        // join up with any cable, button or machine next to it. Nothing solid -
        // you walk right over it.
        if (type == Cable)
        {
            Material look = cableOff;
            AddSpecialPiece(cube, new Vector3(0f, -0.45f, 0f), new Vector3(0.22f, 0.08f, 0.22f), look);
            foreach (Vector3Int side in new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
            {
                if (!JoinsCable(GetBlock(x + side.x, y, z + side.z))) continue;
                Vector3 arm = new Vector3(side.x, 0f, side.z);
                AddSpecialPiece(cube, arm * 0.27f + Vector3.up * -0.45f,
                                side.x != 0 ? new Vector3(0.32f, 0.07f, 0.12f) : new Vector3(0.12f, 0.07f, 0.32f), look);
            }
            if (JoinsCable(GetBlock(x, y + 1, z)))   // a post going up, if there's more cable above
                AddSpecialPiece(cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 1.0f, 0.12f), look);
            return cube;
        }

        // A pressure plate: a thin stone slab on the floor. Nothing solid - you
        // walk onto it, and it sinks down a little while something's standing on it.
        if (type == PressurePlate)
        {
            AddSpecialPiece(cube, new Vector3(0f, -0.47f, 0f), new Vector3(0.8f, 0.06f, 0.8f), materials[PressurePlate]);
            return cube;
        }

        // A signal light: a glass-and-gold block that glows when powered
        if (type == SignalLight)
        {
            cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = signalOff;
            cube.AddComponent<BoxCollider>();
            return cube;
        }

        // The Star Crystal: a glowing block, a bright light, and a beam of
        // starlight going right up into the sky (so you can always find it)
        if (type == StarCrystal)
        {
            cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = materials[StarCrystal];
            cube.AddComponent<BoxCollider>();

            AddSpecialPiece(cube, new Vector3(0f, 0.5f + 100f, 0f), new Vector3(0.4f, 200f, 0.4f), starBeam);

            Light starlight = cube.AddComponent<Light>();
            starlight.type = LightType.Point;
            starlight.color = new Color(0.75f, 0.90f, 1.00f);
            starlight.range = 12f;
            starlight.intensity = 2.5f;
            starlight.shadows = LightShadows.None;

            cube.AddComponent<StarTwinkle>();
            return cube;
        }

        // A piston: a stone block with a wooden pushing plate on the front
        if (IsPiston(type))
        {
            cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = materials[type];
            cube.AddComponent<BoxCollider>();

            Vector3 front = PistonFacing(type);
            Vector3 plate = front.x != 0 ? new Vector3(0.06f, 0.96f, 0.96f) : new Vector3(0.96f, 0.96f, 0.06f);
            AddSpecialPiece(cube, front * 0.5f, plate, pistonHead);
            return cube;
        }

        // A dispenser: a stone block with a dark hole on the front, which is
        // where things come shooting out
        if (IsDispenser(type))
        {
            cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            cube.AddComponent<MeshRenderer>().sharedMaterial = materials[type];
            cube.AddComponent<BoxCollider>();

            Vector3 front = DispenserFacing(type);
            Vector3 mouthSize = front.x != 0 ? new Vector3(0.02f, 0.45f, 0.45f) : new Vector3(0.45f, 0.45f, 0.02f);
            AddSpecialPiece(cube, front * 0.505f, mouthSize, dispenserMouth);
            return cube;
        }

        // Fire: a few glowing flames of different sizes, which flicker. Nothing
        // solid - you can walk right into it (and you'll wish you hadn't!)
        if (type == Fire)
        {
            // The same spot always gets the same flames, so fire doesn't
            // change shape when it's redrawn
            System.Random shape = new System.Random(x * 73856093 ^ y * 19349663 ^ z * 83492791);
            for (int i = 0; i < 5; i++)
            {
                GameObject flame = new GameObject("Flame");
                flame.transform.SetParent(cube.transform, false);
                float tall = 0.3f + (float)shape.NextDouble() * 0.5f;
                flame.transform.localPosition = new Vector3((float)shape.NextDouble() * 0.6f - 0.3f, -0.5f + tall / 2f,
                                                            (float)shape.NextDouble() * 0.6f - 0.3f);
                flame.transform.localScale = new Vector3(0.18f + (float)shape.NextDouble() * 0.15f, tall, 0.18f + (float)shape.NextDouble() * 0.15f);
                flame.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
                MeshRenderer look = flame.AddComponent<MeshRenderer>();
                look.sharedMaterial = flameMaterials[i % flameMaterials.Length];
                look.shadowCastingMode = ShadowCastingMode.Off;
            }
            cube.AddComponent<Flicker>();
            return cube;
        }

        // A lamp: a glowing cube with a real light inside
        cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        cube.AddComponent<MeshRenderer>().sharedMaterial = materials[type];
        cube.AddComponent<BoxCollider>();

        Light glow = cube.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.55f);
        glow.range = 8f;
        glow.intensity = 1.5f;
        glow.shadows = LightShadows.None;

        return cube;
    }

    void AddSpecialPiece(GameObject owner, Vector3 position, Vector3 size, Material look)
    {
        GameObject piece = new GameObject("Piece");
        piece.transform.SetParent(owner.transform, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = size;
        piece.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = look;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // What a cable reaches out to join up with
    public static bool JoinsCable(byte type)
    {
        return type == Cable || type == Button || IsDispenser(type) || type == Tnt || type == MegaTnt || IsDoor(type)
            || type == PressurePlate || type == Timer || type == SignalLight || type == NoteBlock || IsPiston(type);
    }

    // Lights a signal light up, or back down
    public void SetSignalLight(Vector3Int cell, bool on)
    {
        GameObject light;
        if (!specials.TryGetValue(cell, out light) || light == null) return;
        light.GetComponent<MeshRenderer>().sharedMaterial = on ? signalOn : signalOff;
    }

    // A pressure plate sinks down while something's standing on it
    public void SetPlatePressed(Vector3Int cell, bool pressed)
    {
        GameObject plate;
        if (!specials.TryGetValue(cell, out plate) || plate == null || plate.transform.childCount == 0) return;
        Transform slab = plate.transform.GetChild(0);
        slab.localPosition = new Vector3(0f, pressed ? -0.485f : -0.47f, 0f);
        slab.localScale = new Vector3(0.8f, pressed ? 0.03f : 0.06f, 0.8f);
    }

    // Lights a stretch of cable up (or back down) as power runs along it
    public void SetCablePowered(Vector3Int cell, bool on)
    {
        GameObject cable;
        if (!specials.TryGetValue(cell, out cable) || cable == null) return;
        foreach (MeshRenderer piece in cable.GetComponentsInChildren<MeshRenderer>())
            piece.sharedMaterial = on ? cableOn : cableOff;
    }

    // --- Chunks ---

    // The things a chunk is drawn with. Water and glass go in their own
    // shape, because light shines through them, so they cast no shadow.
    class Chunk
    {
        public Mesh solid, seeThrough, bumps;
        public MeshRenderer solidLook, seeThroughLook;
        public MeshCollider collider;
    }

    // Is this face of a block on show, given what's next to it?
    static bool FaceShows(byte type, byte neighbour)
    {
        if (type == Water) return ShowsWater(neighbour);
        if (type == Glass) return neighbour == Air || neighbour == Water || Shaped(neighbour) || IsFurniture(neighbour) || IsModel(neighbour);   // no faces between panes
        return SeeThrough(neighbour);
    }

    // The six directions a face can point, and the corners of each face
    static readonly Vector3Int[] FaceDirections =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };
    static readonly Vector3[,] FaceCorners = MakeFaceCorners();

    static Vector3[,] MakeFaceCorners()
    {
        Vector3[,] corners = new Vector3[6, 4];
        for (int d = 0; d < 6; d++)
        {
            // Two directions along the face: "up" (so textures stand the right
            // way up on the sides) and "across". Going round the corners in
            // this order makes the face visible from the outside.
            Vector3 normal = FaceDirections[d];
            Vector3 up = d == 2 || d == 3 ? Vector3.forward : Vector3.up;
            Vector3 across = Vector3.Cross(normal, up);

            Vector3 middle = normal * 0.5f;
            corners[d, 0] = middle + (-across - up) * 0.5f;
            corners[d, 1] = middle + (-across + up) * 0.5f;
            corners[d, 2] = middle + (across + up) * 0.5f;
            corners[d, 3] = middle + (across - up) * 0.5f;
        }
        return corners;
    }

    static readonly Vector2[] CornerUVs = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };

    // Collects faces, sorted by block type, to turn into one shape
    class ShapeBuilder
    {
        public readonly List<Vector3> corners = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly SortedDictionary<byte, List<int>> trianglesByType = new SortedDictionary<byte, List<int>>();

        public void Clear()
        {
            corners.Clear();
            normals.Clear();
            uvs.Clear();
            foreach (List<int> list in trianglesByType.Values) list.Clear();
        }

        public void AddFace(Vector3 centre, int direction, byte type)
        {
            int first = corners.Count;
            for (int c = 0; c < 4; c++)
            {
                corners.Add(centre + FaceCorners[direction, c]);
                normals.Add(FaceDirections[direction]);
                uvs.Add(CornerUVs[c]);
            }

            List<int> triangles;
            if (!trianglesByType.TryGetValue(type, out triangles))
            {
                triangles = new List<int>();
                trianglesByType[type] = triangles;
            }
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        // Fills in the mesh, and returns the material for each part of it
        public Material[] CopyTo(Mesh mesh, Material[] materials)
        {
            mesh.Clear();
            mesh.SetVertices(corners);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            List<Material> used = new List<Material>();
            List<List<int>> parts = new List<List<int>>();
            foreach (KeyValuePair<byte, List<int>> entry in trianglesByType)
            {
                if (entry.Value.Count == 0) continue;
                used.Add(materials[entry.Key]);
                parts.Add(entry.Value);
            }

            mesh.subMeshCount = parts.Count;
            for (int i = 0; i < parts.Count; i++)
                mesh.SetTriangles(parts[i], i, false);
            mesh.RecalculateBounds();
            return used.ToArray();
        }
    }

    // Shared by every chunk, so building one doesn't make lots of garbage
    static readonly ShapeBuilder solidFaces = new ShapeBuilder();
    static readonly ShapeBuilder seeThroughFaces = new ShapeBuilder();
    static readonly ShapeBuilder bumpFaces = new ShapeBuilder();   // everything solid, for bumping into

    void BuildChunk(int cx, int cz)
    {
        Chunk chunk = chunks[cx, cz];
        if (chunk == null)
        {
            chunk = MakeChunk(cx, cz);
            chunks[cx, cz] = chunk;
        }

        solidFaces.Clear();
        seeThroughFaces.Clear();
        bumpFaces.Clear();

        int endX = Mathf.Min((cx + 1) * ChunkSize, SizeX);
        int endZ = Mathf.Min((cz + 1) * ChunkSize, SizeZ);

        for (int x = cx * ChunkSize; x < endX; x++)
        for (int y = 0; y < SizeY; y++)
        for (int z = cz * ChunkSize; z < endZ; z++)
        {
            byte type = blocks[x, y, z];
            if (type == Air || Special(type)) continue;

            bool clear = type == Water || type == Glass;
            Vector3 centre = new Vector3(x, y, z);

            for (int d = 0; d < 6; d++)
            {
                if (d == 3 && y == 0) continue;   // the underside of the world

                Vector3Int n = FaceDirections[d];
                if (!FaceShows(type, GetBlock(x + n.x, y + n.y, z + n.z))) continue;

                (clear ? seeThroughFaces : solidFaces).AddFace(centre, d, FaceLook(type, d));
                if (type != Water && type != Lava) bumpFaces.AddFace(centre, d, Stone);   // you sink into water and lava
            }
        }

        chunk.solidLook.sharedMaterials = solidFaces.CopyTo(chunk.solid, materials);
        chunk.seeThroughLook.sharedMaterials = seeThroughFaces.CopyTo(chunk.seeThrough, materials);
        bumpFaces.CopyTo(chunk.bumps, materials);

        // The collider only notices a changed shape if it's handed the shape again
        chunk.collider.sharedMesh = null;
        if (bumpFaces.corners.Count > 0) chunk.collider.sharedMesh = chunk.bumps;
    }

    Chunk MakeChunk(int cx, int cz)
    {
        Chunk chunk = new Chunk();

        GameObject obj = new GameObject("Chunk " + cx + "," + cz);
        obj.transform.SetParent(transform, false);

        chunk.solid = NewMesh();
        chunk.seeThrough = NewMesh();
        chunk.bumps = NewMesh();

        obj.AddComponent<MeshFilter>().sharedMesh = chunk.solid;
        chunk.solidLook = obj.AddComponent<MeshRenderer>();
        chunk.collider = obj.AddComponent<MeshCollider>();

        GameObject clear = new GameObject("Water and glass");
        clear.transform.SetParent(obj.transform, false);
        clear.AddComponent<MeshFilter>().sharedMesh = chunk.seeThrough;
        chunk.seeThroughLook = clear.AddComponent<MeshRenderer>();
        chunk.seeThroughLook.shadowCastingMode = ShadowCastingMode.Off;   // light shines through these

        return chunk;
    }

    static Mesh NewMesh()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;   // a chunk can have more corners than the usual limit of 65,535
        return mesh;
    }

    // The chunk shapes aren't cleaned up automatically like objects are
    void OnDestroyChunks()
    {
        if (chunks == null) return;
        foreach (Chunk chunk in chunks)
        {
            if (chunk == null) continue;
            Destroy(chunk.solid);
            Destroy(chunk.seeThrough);
            Destroy(chunk.bumps);
        }
    }
}
