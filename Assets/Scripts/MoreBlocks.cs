using System.Collections.Generic;
using UnityEngine;

// More of the world:
//
//   Grass blocks are green on top and dirt on the sides, with a bit of green
//   hanging over the edge.
//   Gravel: grey pebbles, found in pockets underground. Gravel and sand
//   both fall down if there's nothing underneath them!
//   Farmland and wheat: plant seeds on grass or dirt (it turns into farmland)
//   and wheat grows, from a little green shoot to tall golden wheat.
//   Caves have iron, gold and diamonds in clumps, pockets of gravel, and
//   pools of lava right at the bottom.
//
// The furnace is in Furnace.cs.
public partial class BlockWorld
{
    public const byte Gravel     = 150;
    public const byte Farmland   = 151;
    public const byte WheatFirst = 152;   // just planted
    public const byte WheatRipe  = 155;   // ready to pick
    public const int WheatStages = 4;

    // Not a block - an extra look used when drawing the sides of grass.
    // (It's kept past the end of the block numbers, so all 256 can be real blocks.)
    const int GrassSide = 256;

    public static bool IsWheat(byte type) { return type >= WheatFirst && type <= WheatRipe; }

    // Blocks that fall when there's nothing under them
    public static bool Falls(byte type) { return type == Sand || type == Gravel; }

    private Material wheatGreen, wheatGold;

    void CreateMoreMaterials()
    {
        materials[GrassSide] = MakePixelMaterial((x, y) =>
        {
            // Dirt, with green over the top that hangs down in places
            int hangs = 6 - (int)(Jumble(x, 0, 8) * 2.5f);
            return y >= hangs ? GrassPixel(x, y) : DirtPixel(x, y);
        }, true);

        materials[Gravel] = MakePixelMaterial((x, y) =>
        {
            float n = Jumble(x, y, 9);
            if (n < 0.25f) return new Color(0.38f, 0.36f, 0.35f);
            if (n < 0.45f) return new Color(0.60f, 0.55f, 0.52f);
            if (n > 0.85f) return new Color(0.72f, 0.70f, 0.68f);
            return new Color(0.50f, 0.48f, 0.47f);
        }, true);

        // Dark, damp soil in rows
        materials[Farmland] = MakePixelMaterial((x, y) =>
            x % 3 == 0 ? new Color(0.24f, 0.15f, 0.08f) : (Jumble(x, y, 10) > 0.8f ? new Color(0.40f, 0.27f, 0.15f) : new Color(0.33f, 0.21f, 0.11f)), true);

        wheatGreen = MakeMaterial(new Color(0.35f, 0.70f, 0.20f));
        wheatGold = MakeMaterial(new Color(0.88f, 0.75f, 0.30f));
        for (byte stage = WheatFirst; stage <= WheatRipe; stage++)
            materials[stage] = stage == WheatRipe ? wheatGold : wheatGreen;
    }

    // Grass shows a different face depending on which way it looks
    static int FaceLook(byte type, int direction)
    {
        if (type != Grass) return type;
        if (direction == 2) return Grass;   // the top
        if (direction == 3) return Dirt;    // the bottom
        return GrassSide;
    }

    // Wheat: a little clump of stalks, taller and more golden as it grows.
    // Nothing solid - you walk right through it.
    void BuildWheat(GameObject cube, byte type)
    {
        int stage = type - WheatFirst;
        float tall = 0.2f + stage * 0.23f;
        Material stalk = type == WheatRipe ? wheatGold : wheatGreen;

        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float x = -0.3f + i * 0.3f + ((i + j) % 2) * 0.04f;
                float z = -0.3f + j * 0.3f;
                float height = tall * (0.85f + ((i * 3 + j) % 3) * 0.08f);
                AddSpecialPiece(cube, new Vector3(x, -0.5f + height / 2f, z), new Vector3(0.05f, height, 0.05f), stalk);
                if (type == WheatRipe)   // the ears of grain on top
                    AddSpecialPiece(cube, new Vector3(x, -0.5f + height + 0.06f, z), new Vector3(0.09f, 0.14f, 0.09f), wheatGold);
            }
    }

    // --- Falling sand and gravel ---

    private readonly HashSet<Vector3Int> mightFall = new HashSet<Vector3Int>();
    private float fallTimer;

    // Something changed here - the block itself, or the one on top, might now fall
    void CheckForFalling(int x, int y, int z)
    {
        mightFall.Add(new Vector3Int(x, y, z));
        mightFall.Add(new Vector3Int(x, y + 1, z));
    }

    void RunFalling()
    {
        fallTimer -= Time.deltaTime;
        if (fallTimer > 0f || mightFall.Count == 0) return;
        fallTimer = 0.05f;   // one block's drop at a time, so you can watch it fall

        Vector3Int[] check = new Vector3Int[mightFall.Count];
        mightFall.CopyTo(check);
        mightFall.Clear();

        foreach (Vector3Int cell in check)
        {
            byte type = GetBlock(cell.x, cell.y, cell.z);
            if (!Falls(type) || cell.y <= 0) continue;

            byte below = GetBlock(cell.x, cell.y - 1, cell.z);
            if (below != Air && below != Water && below != Fire && !IsWheat(below)) continue;

            SetBlock(cell.x, cell.y - 1, cell.z, type);   // (this also checks again from there)
            SetBlock(cell.x, cell.y, cell.z, Air);
        }
    }

    // --- Crops ---

    private readonly HashSet<Vector3Int> crops = new HashSet<Vector3Int>();
    private float growTimer;

    void TrackCrop(Vector3Int cell, byte type)
    {
        if (IsWheat(type)) crops.Add(cell);
        else crops.Remove(cell);
    }

    // Every second, each crop has a small chance to grow a bit. It takes
    // about a minute and a half to go from seed to ripe wheat.
    void GrowCrops()
    {
        growTimer -= Time.deltaTime;
        if (growTimer > 0f || crops.Count == 0) return;
        growTimer = 1f;

        Vector3Int[] all = new Vector3Int[crops.Count];
        crops.CopyTo(all);
        foreach (Vector3Int cell in all)
        {
            byte type = GetBlock(cell.x, cell.y, cell.z);

            // Its farmland was dug up - the crop goes too
            if (GetBlock(cell.x, cell.y - 1, cell.z) != Farmland)
            {
                SetBlock(cell.x, cell.y, cell.z, Air);
                continue;
            }
            if (type < WheatRipe && Random.value < (Mods.FastCrops ? 0.6f : 1f / 30f))
                SetBlock(cell.x, cell.y, cell.z, (byte)(type + 1));
        }
    }

    // Called from Update every frame
    void TickMoreBlocks()
    {
        RunFalling();
        GrowCrops();
        RunFurnaces();
    }

    // --- Underground ---

    // Iron, gold and diamonds in clumps (veins), and pockets of gravel.
    // The deeper you dig, the better it gets. Obsidian lies right at the bottom.
    void BuryTreasure()
    {
        float scale = SizeX * SizeZ / (128f * 128f);
        Veins(Gravel, (int)(70 * scale), 6, 18, 1, SizeY - 10);
        Veins(Iron, (int)(170 * scale), 3, 8, 1, SizeY - 10);
        Veins(Gold, (int)(55 * scale), 3, 6, 1, 18);
        Veins(Diamond, (int)(22 * scale), 2, 5, 1, 10);

        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
                for (int y = 0; y <= 2; y++)
                    if (blocks[x, y, z] == Stone && random.NextDouble() < 0.04) blocks[x, y, z] = Obsidian;
    }

    // Clumps of one block: start somewhere in the stone and wander about,
    // changing stone into it as we go
    void Veins(byte ore, int howMany, int smallest, int biggest, int lowest, int highest)
    {
        for (int v = 0; v < howMany; v++)
        {
            int x = random.Next(0, SizeX), y = random.Next(lowest, Mathf.Max(lowest + 1, highest)), z = random.Next(0, SizeZ);
            int size = random.Next(smallest, biggest + 1);

            for (int i = 0; i < size; i++)
            {
                if (InBounds(x, y, z) && blocks[x, y, z] == Stone) blocks[x, y, z] = ore;
                switch (random.Next(0, 6))
                {
                    case 0: x++; break;
                    case 1: x--; break;
                    case 2: y++; break;
                    case 3: y--; break;
                    case 4: z++; break;
                    default: z--; break;
                }
            }
        }
    }

    // The very bottoms of the deepest caves fill up with lava
    void PoolLava()
    {
        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
                for (int y = 1; y <= 3; y++)
                    if (blocks[x, y, z] == Air) blocks[x, y, z] = Lava;
    }
}
