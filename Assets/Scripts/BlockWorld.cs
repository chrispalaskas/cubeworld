using System.Collections.Generic;
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

    private byte[,,] blocks;
    private GameObject[,,] cubes;
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
        groundHeight = new int[SizeX, SizeZ];

        int seed = FixedSeed != 0 ? FixedSeed : System.Environment.TickCount;
        random = new System.Random(seed);
        Debug.Log("World seed: " + seed);

        CreateMaterials();
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
        materials = new Material[9];
        materials[Grass]  = MakeMaterial(new Color(0.35f, 0.70f, 0.25f));
        materials[Dirt]   = MakeMaterial(new Color(0.50f, 0.35f, 0.20f));
        materials[Stone]  = MakeMaterial(new Color(0.55f, 0.55f, 0.58f));
        materials[Wood]   = MakeMaterial(new Color(0.45f, 0.32f, 0.18f));
        materials[Sand]   = MakeMaterial(new Color(0.86f, 0.80f, 0.55f));
        materials[Leaves] = MakeMaterial(new Color(0.20f, 0.50f, 0.15f));
        materials[Water]  = MakeSeeThroughMaterial(new Color(0.20f, 0.45f, 0.85f, 0.6f));
        materials[Tnt]    = MakeTntMaterial();

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

    // Red with a white band around the middle, drawn pixel by pixel
    static Material MakeTntMaterial()
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;   // keep the pixels crisp and blocky

        Color red = new Color(0.80f, 0.15f, 0.10f);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                texture.SetPixel(x, y, y >= 3 && y <= 4 ? Color.white : red);
        texture.Apply();

        Material mat = MakeMaterial(Color.white);
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

    void AddLeaf(int x, int y, int z)
    {
        if (InBounds(x, y, z) && blocks[x, y, z] == Air)
            blocks[x, y, z] = Leaves;
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

    // A spot standing on dry grass, as close to the middle of the map as possible
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

                    if (y >= 0 && GetBlock(x, y, z) == Grass)
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

    // You can see through air and water, so a block next to either is on show
    static bool SeeThrough(byte type)
    {
        return type == Air || type == Water;
    }

    // A block needs a cube only if at least one of its six sides is exposed
    bool IsVisible(int x, int y, int z)
    {
        byte type = blocks[x, y, z];
        if (type == Air) return false;

        // Water is only drawn where it touches air - the surface of the sea,
        // or the sides of a pool or tower you built yourself
        if (type == Water)
            return GetBlock(x + 1, y, z) == Air
                || GetBlock(x - 1, y, z) == Air
                || GetBlock(x, y + 1, z) == Air
                || GetBlock(x, y - 1, z) == Air
                || GetBlock(x, y, z + 1) == Air
                || GetBlock(x, y, z - 1) == Air;

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
        else if (wanted)
        {
            // Still visible, but it might be a different type now. Water has
            // no collider, so swapping water for a solid block means a new cube.
            bool solid = blocks[x, y, z] != Water;
            if (solid != (cube.GetComponent<Collider>() != null))
            {
                Destroy(cube);
                cubes[x, y, z] = CreateCube(x, y, z);
            }
            else
            {
                cube.GetComponent<Renderer>().sharedMaterial = materials[blocks[x, y, z]];
            }
        }
    }

    GameObject CreateCube(int x, int y, int z)
    {
        byte type = blocks[x, y, z];

        GameObject cube = new GameObject("Block " + x + "," + y + "," + z);
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = new Vector3(x, y, z);

        cube.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
        MeshRenderer look = cube.AddComponent<MeshRenderer>();
        look.sharedMaterial = materials[type];

        if (type == Water)
            look.shadowCastingMode = ShadowCastingMode.Off;   // you can walk and see through water
        else
            cube.AddComponent<BoxCollider>();                 // everything else is solid

        return cube;
    }
}
