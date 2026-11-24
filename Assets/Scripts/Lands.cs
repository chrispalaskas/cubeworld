using UnityEngine;

// Different lands in the home world (new worlds only):
//
//   Grassland:  green hills and ordinary trees, like before
//   Desert:     sand as far as you can see, with cactuses and no trees
//   Jungle:     giant trees, very close together, with leaves everywhere
//   Snowy land: snow on the ground, a few trees, and the sea frozen over
//
// Each column of the world belongs to one land, worked out from a very
// wide, gentle noise - so the lands are big, and blend into each other.
public partial class BlockWorld
{
    enum Land { Grassland, Desert, Jungle, Snowy }

    private Land[,] lands;

    // Which land each column is, and its top blocks to match
    void MakeLands()
    {
        lands = new Land[SizeX, SizeZ];
        float warmth = random.Next(0, 10000), wetness = random.Next(0, 10000);

        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
            {
                float warm = Mathf.PerlinNoise(warmth + x * 0.012f, warmth + z * 0.012f);
                float wet = Mathf.PerlinNoise(wetness + x * 0.014f, wetness + z * 0.014f);
                Land land = warm < 0.36f ? Land.Snowy : warm > 0.62f ? (wet > 0.5f ? Land.Jungle : Land.Desert) : Land.Grassland;
                lands[x, z] = land;

                int top = groundHeight[x, z];
                if (top <= SeaLevel + 1) continue;   // beaches stay beaches

                if (land == Land.Desert)
                    for (int y = top; y >= top - 4 && y > 0; y--) blocks[x, y, z] = Sand;
                else if (land == Land.Snowy)
                    blocks[x, top, z] = Snow;
            }
    }

    // In the snowy land, the top of the sea is ice
    void FreezeSea()
    {
        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
                if (lands[x, z] == Land.Snowy && blocks[x, SeaLevel, z] == Water)
                    blocks[x, SeaLevel, z] = Ice;
    }

    // Trees in grassland and snowy land, giant trees in the jungle, cactuses in the desert
    void PlantLands()
    {
        for (int x = 3; x < SizeX - 3; x++)
            for (int z = 3; z < SizeZ - 3; z++)
            {
                Land land = lands[x, z];

                // Flowers dotted about the grass
                if (land != Land.Desert && random.NextDouble() < 0.015)
                {
                    int soil = GetSurfaceHeight(x, z);
                    if (soil > 0 && blocks[x, soil, z] == Grass && soil + 1 < SizeY)
                        blocks[x, soil + 1, z] = (byte)(FlowerFirst + random.Next(0, 3));
                    continue;
                }

                double chance = land == Land.Jungle ? 0.05 : land == Land.Desert ? 0.008 : land == Land.Snowy ? 0.01 : 0.02;
                if (random.NextDouble() > chance) continue;

                int top = GetSurfaceHeight(x, z);
                if (top < 0 || top >= SizeY - 12) continue;
                byte ground = blocks[x, top, z];

                if (land == Land.Desert)
                {
                    if (ground != Sand || top <= SeaLevel + 1) continue;
                    int tall = random.Next(1, 4);
                    for (int i = 1; i <= tall; i++) blocks[x, top + i, z] = Cactus;
                }
                else if (land == Land.Jungle)
                {
                    if (ground != Grass) continue;
                    if (JungleTreeNearby(x, z)) continue;
                    GrowJungleTree(x, top + 1, z);
                }
                else
                {
                    if (ground != Grass && ground != Snow) continue;
                    if (TreeNearby(x, z)) continue;
                    GrowTree(x, top + 1, z);
                }
            }
    }

    bool JungleTreeNearby(int x, int z)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
                for (int y = SeaLevel; y < SizeY; y++)
                    if (GetBlock(x + dx, y, z + dz) == Wood) return true;
        return false;
    }

    // A tall, thick trunk (2 x 2) with a big round crown of leaves
    void GrowJungleTree(int x, int y, int z)
    {
        int tall = random.Next(8, 12);
        if (y + tall + 4 >= SizeY) tall = SizeY - y - 5;
        if (tall < 5) return;
        Trees.Add(new Vector3Int(x, y, z));

        for (int i = 0; i < tall; i++)
            for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++)
                    if (InBounds(x + dx, y + i, z + dz)) blocks[x + dx, y + i, z + dz] = Wood;

        int top = y + tall;
        for (int ly = top - 2; ly <= top + 2; ly++)
        {
            int radius = ly >= top + 2 ? 2 : 3;
            for (int dx = -radius; dx <= radius + 1; dx++)
                for (int dz = -radius; dz <= radius + 1; dz++)
                {
                    float far = new Vector2(dx - 0.5f, dz - 0.5f).magnitude;
                    if (far > radius + 0.3f || (far > radius - 0.5f && random.NextDouble() < 0.4)) continue;
                    AddLeaf(x + dx, ly, z + dz);
                }
        }

        // Leaves dangling down the sides, like vines
        for (int v = 0; v < 6; v++)
        {
            int vx = x + random.Next(-3, 5), vz = z + random.Next(-3, 5);
            for (int ly = top - 2; ly > top - 6; ly--) AddLeaf(vx, ly, vz);
        }
    }
}
