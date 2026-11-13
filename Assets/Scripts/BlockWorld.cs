using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// The world is a 3D grid of block numbers. We only keep a real cube object
// for blocks you can actually SEE - blocks buried inside the ground cost us
// nothing. That is the trick that keeps this fast.
public class BlockWorld : MonoBehaviour
{
    public static BlockWorld Instance { get; private set; }

    public const int SizeX = 64;
    public const int SizeY = 32;
    public const int SizeZ = 64;

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

    private byte[,,] blocks;
    private GameObject[,,] cubes;
    private byte[,,] cubeBuiltAs;   // which block type each cube was made for
    private Material[] materials;
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

        blocks = new byte[SizeX, SizeY, SizeZ];
        cubes = new GameObject[SizeX, SizeY, SizeZ];
        cubeBuiltAs = new byte[SizeX, SizeY, SizeZ];
        groundHeight = new int[SizeX, SizeZ];

        CreateMaterials();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Makes a brand new world. (Loading a saved one uses ReadFrom instead.)
    public void Generate()
    {
        int seed = FixedSeed != 0 ? FixedSeed : System.Environment.TickCount;
        random = new System.Random(seed);
        Debug.Log("World seed: " + seed);

        GenerateHills();
        CarveCaves();
        RegrowGrass();
        FillSea();
        PlantTrees();
        BuildAllCubes();
    }

    void CreateMaterials()
    {
        // ONE material per block type, shared by every cube of that type.
        // Giving each cube its own material would bring the game to a crawl.
        materials = new Material[54];
        materials[Grass]  = MakeMaterial(new Color(0.35f, 0.70f, 0.25f));
        materials[Dirt]   = MakeMaterial(new Color(0.50f, 0.35f, 0.20f));
        materials[Stone]  = MakeMaterial(new Color(0.55f, 0.55f, 0.58f));
        materials[Wood]   = MakeMaterial(new Color(0.45f, 0.32f, 0.18f));
        materials[Sand]   = MakeMaterial(new Color(0.86f, 0.80f, 0.55f));
        materials[Leaves] = MakeMaterial(new Color(0.20f, 0.50f, 0.15f));
        materials[Water]  = MakeSeeThroughMaterial(new Color(0.20f, 0.45f, 0.85f, 0.6f));
        materials[Tnt]    = MakeTntMaterial(new Color(0.80f, 0.15f, 0.10f), Color.white);

        materials[Glass]       = MakeGlassMaterial();
        materials[Diamond]     = MakePixelMaterial((x, y) => Speckle(x, y, 4) ? new Color(0.80f, 1.00f, 1.00f) : new Color(0.35f, 0.85f, 0.85f));
        materials[Gold]        = MakePixelMaterial((x, y) => Speckle(x, y, 5) ? new Color(1.00f, 0.95f, 0.55f) : new Color(0.95f, 0.75f, 0.15f));
        materials[Iron]        = MakePixelMaterial((x, y) => Speckle(x, y, 6) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.78f, 0.78f, 0.80f));
        materials[Brick]       = MakePixelMaterial(BrickPixel);
        materials[Cobblestone] = MakePixelMaterial((x, y) => Speckle(x, y, 3) ? new Color(0.38f, 0.38f, 0.40f) : new Color(0.58f, 0.58f, 0.60f));
        materials[Planks]      = MakePixelMaterial(PlankPixel);
        materials[Snow]        = MakePixelMaterial((x, y) => Speckle(x, y, 7) ? new Color(0.85f, 0.90f, 0.95f) : new Color(0.97f, 0.98f, 1.00f));
        materials[Obsidian]    = MakePixelMaterial((x, y) => Speckle(x, y, 5) ? new Color(0.32f, 0.20f, 0.48f) : new Color(0.10f, 0.07f, 0.16f));

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
        materials[MagicGrass]  = MakePixelMaterial((x, y) => Speckle(x, y, 4) ? new Color(1.00f, 0.70f, 0.95f) : new Color(0.75f, 0.40f, 0.85f));
        materials[Crystal]     = MakeGlowingMaterial((x, y) => (x + y) % 4 == 0 ? new Color(0.85f, 1.00f, 1.00f) : new Color(0.30f, 0.85f, 0.95f));
        materials[Cloud]       = MakePixelMaterial((x, y) => Speckle(x, y, 9) ? new Color(0.90f, 0.92f, 0.98f) : Color.white);

        TntFlashMaterial  = MakeMaterial(Color.white);
        ExplosionMaterial = MakeSeeThroughMaterial(new Color(1f, 0.55f, 0.1f, 0.7f));

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
    static Material MakePixelMaterial(System.Func<int, int, Color> pixel)
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;   // keep the pixels crisp and blocky

        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                texture.SetPixel(x, y, pixel(x, y));
        texture.Apply();

        Material mat = MakeMaterial(Color.white);
        mat.mainTexture = texture;
        return mat;
    }

    // A scattering of pixels that looks random but is the same every time.
    // Bigger "rarity" means fewer of them.
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
        Vector2 drift = new Vector2(Time.time * 0.25f, Time.time * 0.15f);
        materials[Portal].mainTextureOffset = drift;
        materials[MagicPortal].mainTextureOffset = -drift;
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
                // Big gentle hills, with small bumps on top of them
                float hills = Mathf.PerlinNoise(offsetX + x * 0.03f, offsetZ + z * 0.03f);
                float bumps = Mathf.PerlinNoise(offsetX + 500f + x * 0.12f, offsetZ + 500f + z * 0.12f);

                int height = 4 + Mathf.RoundToInt(hills * 20f + bumps * 3f);
                height = Mathf.Min(height, SizeY - 8);   // leave room for a tree on top
                groundHeight[x, z] = height;

                // Land that only just pokes out of the sea becomes beach
                bool beach = height <= SeaLevel + 1;

                for (int y = 0; y <= height; y++)
                {
                    if (y == height)          blocks[x, y, z] = beach ? Sand : Grass;
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
                RandomRange(3, SeaLevel + 3),
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

        // GrowTree only fills in the grid, so now draw everything around it
        for (int cx = x - 3; cx <= x + 3; cx++)
            for (int cy = y - 1; cy <= y + 8; cy++)
                for (int cz = z - 3; cz <= z + 3; cz++)
                    RefreshCube(cx, cy, cz);
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

        // The island you arrive on, in the middle, then others scattered around it
        MakeIsland(SizeX / 2, SizeZ / 2, 18, 7);
        for (int i = 0; i < 10; i++)
            MakeIsland(random.Next(6, SizeX - 6), random.Next(6, SizeZ - 6), random.Next(12, 24), random.Next(3, 8));

        for (int i = 0; i < 7; i++)
        {
            int x = random.Next(4, SizeX - 4), z = random.Next(4, SizeZ - 4);
            if (Mathf.Abs(x - SizeX / 2) < 9 && Mathf.Abs(z - SizeZ / 2) < 9) continue;   // keep the sky over the arrival island clear
            MakeCloud(x, random.Next(26, 29), z);
        }

        BuildAllCubes();
    }

    // An island is like an upside-down hill: a grassy top, and rock below
    // that narrows to a point
    void MakeIsland(int cx, int cz, int top, int radius)
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
    public void PlaceReturnPortal(Vector3 near)
    {
        int nx = Mathf.RoundToInt(near.x), nz = Mathf.RoundToInt(near.z);
        int[,] tries = { { 2, 0 }, { 0, 2 }, { -2, 0 }, { 0, -2 }, { 1, 0 }, { 0, 1 }, { 2, 2 }, { -2, -2 } };

        for (int i = 0; i < tries.GetLength(0); i++)
        {
            int x = nx + tries[i, 0], z = nz + tries[i, 1];
            int y = GetSurfaceHeight(x, z);
            if (y >= 0 && GetBlock(x, y, z) == MagicGrass)
            {
                SetBlock(x, y, z, MagicPortal);
                return;
            }
        }

        // Nowhere better? Then right where you'd stand.
        SetBlock(nx, Mathf.RoundToInt(near.y - 0.6f), nz, MagicPortal);
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
    }

    public bool ReadFrom(BinaryReader reader)
    {
        // A save from a different-sized world won't fit
        if (reader.ReadInt32() != SizeX || reader.ReadInt32() != SizeY || reader.ReadInt32() != SizeZ)
            return false;

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

                    if (y >= 0 && (GetBlock(x, y, z) == Grass || GetBlock(x, y, z) == MagicGrass))
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

        blocks[x, y, z] = type;

        // This block and its six neighbours may have just become visible
        // or hidden, so every one of them needs rechecking.
        RefreshCube(x, y, z);
        RefreshCube(x + 1, y, z);
        RefreshCube(x - 1, y, z);
        RefreshCube(x, y + 1, z);
        RefreshCube(x, y - 1, z);
        RefreshCube(x, y, z + 1);
        RefreshCube(x, y, z - 1);
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
        return type == Air || type == Water || type == Glass || Shaped(type);
    }

    // Water is drawn where it touches air, glass (so you can build a fish
    // tank), or the gaps around stairs and doors
    static bool ShowsWater(byte type)
    {
        return type == Air || type == Glass || Shaped(type);
    }

    // A block needs a cube only if at least one of its six sides is exposed
    bool IsVisible(int x, int y, int z)
    {
        byte type = blocks[x, y, z];
        if (type == Air) return false;

        // Water is only drawn where you could see it - the surface of the sea,
        // the sides of a pool or tower you built yourself, or behind glass
        if (type == Water)
            return ShowsWater(GetBlock(x + 1, y, z))
                || ShowsWater(GetBlock(x - 1, y, z))
                || ShowsWater(GetBlock(x, y + 1, z))
                || ShowsWater(GetBlock(x, y - 1, z))
                || ShowsWater(GetBlock(x, y, z + 1))
                || ShowsWater(GetBlock(x, y, z - 1));

        // The bottom of the world counts as solid, so we don't waste cubes
        // drawing the underside of the ground where nobody can ever see it.
        bool openBelow = (y > 0) && SeeThrough(blocks[x, y - 1, z]);

        return openBelow
            || SeeThrough(GetBlock(x + 1, y, z))
            || SeeThrough(GetBlock(x - 1, y, z))
            || SeeThrough(GetBlock(x, y + 1, z))
            || SeeThrough(GetBlock(x, y, z + 1))
            || SeeThrough(GetBlock(x, y, z - 1));
    }

    void BuildAllCubes()
    {
        for (int x = 0; x < SizeX; x++)
            for (int y = 0; y < SizeY; y++)
                for (int z = 0; z < SizeZ; z++)
                    RefreshCube(x, y, z);
    }

    void RefreshCube(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return;

        bool wanted = IsVisible(x, y, z);
        GameObject cube = cubes[x, y, z];

        if (wanted && cube == null)
        {
            cubes[x, y, z] = CreateCube(x, y, z);
        }
        else if (!wanted && cube != null)
        {
            Destroy(cube);
            cubes[x, y, z] = null;
        }
        else if (wanted && cubeBuiltAs[x, y, z] != blocks[x, y, z])
        {
            // Still visible, but it's a different type now. Two plain solid
            // cubes only need a change of look; anything else - water, lamps,
            // stairs, doors - is built differently, so it gets a new cube.
            if (PlainCube(cubeBuiltAs[x, y, z]) && PlainCube(blocks[x, y, z]))
            {
                cube.GetComponent<Renderer>().sharedMaterial = materials[blocks[x, y, z]];
                cubeBuiltAs[x, y, z] = blocks[x, y, z];
            }
            else
            {
                Destroy(cube);
                cubes[x, y, z] = CreateCube(x, y, z);
            }
        }
    }

    static bool PlainCube(byte type)
    {
        return type != Water && type != Lamp && !Shaped(type);
    }

    GameObject CreateCube(int x, int y, int z)
    {
        byte type = blocks[x, y, z];

        GameObject cube = new GameObject("Block " + x + "," + y + "," + z);
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = new Vector3(x, y, z);
        cubeBuiltAs[x, y, z] = type;

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

        cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        MeshRenderer look = cube.AddComponent<MeshRenderer>();
        look.sharedMaterial = materials[type];

        if (type == Water || type == Glass)
            look.shadowCastingMode = ShadowCastingMode.Off;   // light shines through these

        if (type != Water)
            cube.AddComponent<BoxCollider>();                 // everything but water is solid

        if (type == Lamp)
        {
            Light glow = cube.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.55f);
            glow.range = 8f;
            glow.intensity = 1.5f;
            glow.shadows = LightShadows.None;
        }

        return cube;
    }
}
