using System.Collections.Generic;
using UnityEngine;

// Even more places in the home world (built the same careful way - only on
// untouched ground, and old worlds get them too):
//
//   Haunted house:    a spooky black mansion with broken windows. Ghosts drift
//                     about inside (in survival). A wall of bookshelves upstairs
//                     hides a secret room with treasure - break a book to find it!
//   Volcano island:   a smoking mountain out in the sea, with a lava lake in its
//                     crater. Every so often it rumbles and throws out lava bombs!
//                     There's treasure on the rim, if you're brave enough to climb.
//   Sky islands:      little floating islands high in the clouds, with grass,
//                     flowers, a tree and a treasure chest. Fly there in the
//                     village's hot air balloon (see Balloon.cs).
//   The quest board:  a sign in the middle of the village with three jobs on it.
//                     New jobs every morning!
// The Yeti lives in the ice caves (see Yeti.cs).
public static partial class Structures
{
    public static bool YetiDefeated
    {
        get { return PlayerPrefs.GetInt(Key("yeti-defeated"), 0) == 1; }
        set { PlayerPrefs.SetInt(Key("yeti-defeated"), value ? 1 : 0); }
    }

    // For meteors - so they never blow up anything you built
    public static bool IsNaturalBlock(byte type) { return Natural(type); }

    static void BuildNewPlaces(BlockWorld world, int slot, System.Random dice)
    {
        if (!PlayerPrefs.HasKey(Key(slot, "haunted"))) BuildHauntedHouse(world, slot, dice);
        if (!PlayerPrefs.HasKey(Key(slot, "volcano"))) BuildVolcano(world, slot, dice);
        if (!PlayerPrefs.HasKey(Key(slot, "skyislands"))) BuildSkyIslands(world, slot, dice);
        if (!PlayerPrefs.HasKey(Key(slot, "board"))) PutUpQuestBoard(world, slot);
    }

    // --- Finding flat, dry, untouched land, away from the other places ---

    static bool FarFromPlaces(int slot, int x, int z, float apart)
    {
        foreach (string what in new[] { "village", "pyramid", "haunted", "funpark" })
        {
            Vector3Int spot = ReadSpot(Key(slot, what));
            if (spot.y > 0 && new Vector2(spot.x - x, spot.z - z).magnitude < apart) return false;
        }
        return true;
    }

    static bool FindFlatLand(BlockWorld world, int slot, System.Random dice, int half, out int bx, out int bz, out int level)
    {
        bx = bz = level = -1;
        foreach (int bumpy in new[] { 3, 5, 8 })
            for (int tries = 0; tries < 400; tries++)
            {
                int x = dice.Next(half + 4, BlockWorld.SizeX - half - 4), z = dice.Next(half + 4, BlockWorld.SizeZ - half - 4);
                if (!FarFromPlaces(slot, x, z, half + 22)) continue;

                int low = 999, high = -1, total = 0, count = 0;
                bool dry = true;
                for (int dx = -half; dx <= half && dry; dx += 2)
                    for (int dz = -half; dz <= half && dry; dz += 2)
                    {
                        int g = GroundAt(world, x + dx, z + dz);
                        if (g <= BlockWorld.SeaLevel + 1) dry = false;
                        low = Mathf.Min(low, g); high = Mathf.Max(high, g);
                        total += g; count++;
                    }
                if (!dry || high - low > bumpy) continue;
                int ground = Mathf.RoundToInt((float)total / count);
                if (ground + 14 >= BlockWorld.SizeY || ground - 5 < 1) continue;
                if (!AllNatural(world, x - half, ground - 5, z - half, x + half, BlockWorld.SizeY - 1, z + half)) continue;
                bx = x; bz = z; level = ground;
                return true;
            }
        return false;
    }

    // Stone underneath, the top block on top, and clear sky above
    static void Flatten(BlockWorld world, int x0, int z0, int x1, int z1, int ground, byte top)
    {
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                for (int y = ground - 5; y < ground; y++)
                {
                    byte below = world.GetBlock(x, y, z);
                    if (below == BlockWorld.Air || below == BlockWorld.Water || below == BlockWorld.Lava) world.SetBlock(x, y, z, BlockWorld.Dirt);
                }
                world.SetBlock(x, ground, z, top);
                for (int y = ground + 1; y < BlockWorld.SizeY; y++) world.SetBlock(x, y, z, BlockWorld.Air);
            }
    }

    // --- The haunted house ---

    static void BuildHauntedHouse(BlockWorld world, int slot, System.Random dice)
    {
        const int half = 8;
        int cx, cz, g;
        if (!FindFlatLand(world, slot, dice, half, out cx, out cz, out g)) return;
        PlayerPrefs.SetInt(Key(slot, "haunted-built"), 1);

        byte black = (byte)(BlockWorld.WoolFirst + 7), purple = (byte)(BlockWorld.WoolFirst + 6);
        Flatten(world, cx - half, cz - half, cx + half, cz + half, g, BlockWorld.Dirt);   // dead, bare earth all round

        // Spooky dead trees in the garden - bare trunks with no leaves
        int[,] trees = { { -7, -6 }, { 7, -5 }, { -6, 7 }, { 6, 6 } };
        for (int t = 0; t < trees.GetLength(0); t++)
        {
            int tx = cx + trees[t, 0], tz = cz + trees[t, 1];
            for (int y = 1; y <= 4; y++) world.SetBlock(tx, g + y, tz, BlockWorld.Wood);
            world.SetBlock(tx + 1, g + 4, tz, BlockWorld.Wood);
            world.SetBlock(tx - 1, g + 3, tz, BlockWorld.Wood);
        }

        // The house: 11 x 11, two floors
        int x0 = cx - 5, x1 = cx + 5, z0 = cz - 5, z1 = cz + 5;
        Fill(world, x0, g, z0, x1, g, z1, BlockWorld.Cobblestone);
        for (int y = g + 1; y <= g + 9; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    bool corner = (x == x0 || x == x1) && (z == z0 || z == z1);
                    world.SetBlock(x, y, z, corner ? BlockWorld.Obsidian : edge ? black : BlockWorld.Air);
                }
        Fill(world, x0 + 1, g + 5, z0 + 1, x1 - 1, g + 5, z1 - 1, BlockWorld.Planks);    // the upstairs floor
        Fill(world, x0, g + 10, z0, x1, g + 10, z1, BlockWorld.Obsidian);                // a pointy roof
        Fill(world, x0 + 1, g + 11, z0 + 1, x1 - 1, g + 11, z1 - 1, black);
        Fill(world, x0 + 3, g + 12, z0 + 3, x1 - 3, g + 12, z1 - 3, purple);
        Fill(world, cx, g + 13, cz, cx, g + 14, cz, BlockWorld.Obsidian);                // a spire

        // Windows - some of them broken (just holes)
        foreach (int y in new[] { g + 2, g + 7 })
            for (int i = -3; i <= 3; i += 3)
            {
                world.SetBlock(cx + i, y, z0, dice.NextDouble() < 0.5 ? BlockWorld.Glass : BlockWorld.Air);
                world.SetBlock(cx + i, y, z1, dice.NextDouble() < 0.5 ? BlockWorld.Glass : BlockWorld.Air);
                world.SetBlock(x0, y, cz + i, dice.NextDouble() < 0.5 ? BlockWorld.Glass : BlockWorld.Air);
                world.SetBlock(x1, y, cz + i, dice.NextDouble() < 0.5 ? BlockWorld.Glass : BlockWorld.Air);
            }

        // The front door, and a warning sign
        world.SetBlock(cx, g + 1, z0, BlockWorld.MakeDoor(false, false, true));
        world.SetBlock(cx, g + 2, z0, BlockWorld.MakeDoor(true, false, true));
        world.SetBlock(cx, g + 3, z0, BlockWorld.Lantern);
        Sign(world, new Vector3Int(cx + 2, g + 1, z0 - 2), "KEEP OUT! Haunted!");

        // Downstairs: a dusty old room with one flickering lantern
        world.SetBlock(cx, g + 1, cz, BlockWorld.Table);
        world.SetBlock(cx - 1, g + 1, cz, BlockWorld.Chair);
        world.SetBlock(cx + 1, g + 1, cz, BlockWorld.Chair + 2);
        world.SetBlock(cx, g + 2, cz, BlockWorld.Lantern);
        for (int z = z0 + 1; z <= z1 - 1; z += 2) world.SetBlock(x1 - 1, g + 1, z, BlockWorld.Bookshelf);
        world.SetBlock(x0 + 1, g + 3, cz + 2, BlockWorld.Painting);

        // A ladder up to the first floor
        for (int y = g + 1; y <= g + 5; y++) world.SetBlock(x0 + 1, y, z0 + 2, BlockWorld.LadderFirst + 1);
        Sign(world, new Vector3Int(x0 + 2, g + 1, z0 + 1), "Some books hide secrets...");

        // Upstairs: a wall of bookshelves... with a hidden room behind it!
        for (int x = x0 + 1; x <= x1 - 1; x++)
            for (int y = g + 6; y <= g + 8; y++)
                world.SetBlock(x, y, z1 - 3, BlockWorld.Bookshelf);
        world.SetBlock(x0 + 3, g + 6, z0 + 2, BlockWorld.Bed + 1);
        Loot(world, new Vector3Int(x1 - 2, g + 6, z0 + 1), dice, new[] { BlockWorld.Feather, BlockWorld.StringItem, (byte)94, BlockWorld.Leather }, 3);

        Vector3Int secret = new Vector3Int(cx, g + 6, z1 - 1);
        Loot(world, secret, dice, new[] { BlockWorld.Diamond, BlockWorld.Gold, (byte)95, (byte)247, (byte)253, BlockWorld.TreasureMap, BlockWorld.Crystal }, 8);
        world.SetBlock(cx - 2, g + 6, z1 - 1, BlockWorld.Lantern);
        world.SetBlock(cx + 2, g + 6, z1 - 1, BlockWorld.Lantern);

        WriteSpot(Key(slot, "haunted"), new Vector3Int(cx, g + 1, cz));
        WriteSpot(Key(slot, "haunted-secret"), secret);
    }

    // --- The volcano island ---

    static void BuildVolcano(BlockWorld world, int slot, System.Random dice)
    {
        foreach (int radius in new[] { 12, 10, 8, 6 })
            for (int tries = 0; tries < 300; tries++)
            {
                int cx = dice.Next(radius + 3, BlockWorld.SizeX - radius - 3), cz = dice.Next(radius + 3, BlockWorld.SizeZ - radius - 3);
                bool sea = true;
                for (int dx = -radius; dx <= radius && sea; dx += 2)
                    for (int dz = -radius; dz <= radius && sea; dz += 2)
                        if (world.GetBlock(cx + dx, BlockWorld.SeaLevel, cz + dz) != BlockWorld.Water) sea = false;
                if (!sea) continue;
                int peak = BlockWorld.SeaLevel + Mathf.Min(14, radius + 3);
                if (peak + 3 >= BlockWorld.SizeY) continue;
                if (!AllNatural(world, cx - radius, 1, cz - radius, cx + radius, BlockWorld.SizeY - 1, cz + radius)) continue;

                PlayerPrefs.SetInt(Key(slot, "volcano-built"), 1);
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        float d = new Vector2(dx, dz).magnitude;
                        if (d > radius) continue;
                        int x = cx + dx, z = cz + dz;
                        int top = Mathf.RoundToInt(Mathf.Lerp(peak, BlockWorld.SeaLevel + 1, d / radius));
                        int floor = 1;
                        for (int y = BlockWorld.SeaLevel; y > 1; y--)
                            if (world.GetBlock(x, y, z) != BlockWorld.Water) { floor = y; break; }

                        bool beach = d > radius - 2;
                        for (int y = floor; y <= top; y++)
                            world.SetBlock(x, y, z, beach ? BlockWorld.Sand : y == top && dice.NextDouble() < 0.25 ? BlockWorld.Obsidian : BlockWorld.EmberRock);

                        // The crater, with a lava lake in it
                        if (d < 2.6f)
                        {
                            for (int y = peak - 2; y <= peak; y++) world.SetBlock(x, y, z, BlockWorld.Air);
                            world.SetBlock(x, peak - 3, z, BlockWorld.Lava);
                        }
                        else if (d < 3.6f) world.SetBlock(x, top, z, BlockWorld.Obsidian);   // the rim
                    }

                // Treasure on the rim (hot!)
                int chestTop = Mathf.RoundToInt(Mathf.Lerp(peak, BlockWorld.SeaLevel + 1, 4f / radius));
                Loot(world, new Vector3Int(cx + 4, chestTop + 1, cz), dice,
                     new[] { BlockWorld.FireCrystal, BlockWorld.Diamond, BlockWorld.Gold, BlockWorld.Obsidian, (byte)95 }, 7);
                WriteSpot(Key(slot, "volcano"), new Vector3Int(cx, peak, cz));
                return;
            }
    }

    // --- The sky islands ---

    static void BuildSkyIslands(BlockWorld world, int slot, System.Random dice)
    {
        Vector3 start;
        if (!VillageCentre(out start)) start = world.FindSpawnPoint();
        int baseY = BlockWorld.SizeY - 12;

        for (int tries = 0; tries < 80; tries++)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f, far = 20f + (float)dice.NextDouble() * 30f;
            int cx = Mathf.RoundToInt(start.x + Mathf.Cos(angle) * far), cz = Mathf.RoundToInt(start.z + Mathf.Sin(angle) * far);

            // One big island and three little ones round it
            int[,] islands = { { 0, 0, 6, 0 }, { 12, 4, 3, 2 }, { -6, 12, 4, -2 }, { -11, -7, 3, 3 } };
            bool clear = true;
            for (int i = 0; i < islands.GetLength(0) && clear; i++)
            {
                int ix = cx + islands[i, 0], iz = cz + islands[i, 1], r = islands[i, 2] + 1, iy = baseY + islands[i, 3];
                if (ix - r < 2 || iz - r < 2 || ix + r >= BlockWorld.SizeX - 2 || iz + r >= BlockWorld.SizeZ - 2 || iy + 7 >= BlockWorld.SizeY) { clear = false; break; }
                for (int x = ix - r; x <= ix + r && clear; x++)
                    for (int z = iz - r; z <= iz + r && clear; z++)
                        for (int y = iy - 7; y < BlockWorld.SizeY; y++)
                            if (world.GetBlock(x, y, z) != BlockWorld.Air) { clear = false; break; }
            }
            if (!clear) continue;

            PlayerPrefs.SetInt(Key(slot, "skyislands-built"), 1);
            for (int i = 0; i < islands.GetLength(0); i++)
            {
                int ix = cx + islands[i, 0], iz = cz + islands[i, 1], r = islands[i, 2], iy = baseY + islands[i, 3];
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        float d = new Vector2(dx, dz).magnitude;
                        if (d > r + 0.3f) continue;
                        int x = ix + dx, z = iz + dz;
                        world.SetBlock(x, iy, z, BlockWorld.Grass);
                        world.SetBlock(x, iy - 1, z, BlockWorld.Dirt);
                        int depth = Mathf.RoundToInt((r - d) * 1.2f) + 1;   // pointy underneath, like a turnip
                        for (int y = iy - 2; y >= iy - 1 - depth; y--) world.SetBlock(x, y, z, BlockWorld.Stone);
                        if (d < r - 0.5f && dice.NextDouble() < 0.12) world.SetBlock(x, iy + 1, z, (byte)(BlockWorld.FlowerFirst + dice.Next(0, 3)));
                    }
            }
            int y0 = baseY;
            world.SetBlock(cx + 2, y0 + 1, cz + 2, BlockWorld.Air);
            world.GrowTreeAt(cx + 2, y0 + 1, cz + 2);
            Loot(world, new Vector3Int(cx - 2, y0 + 1, cz - 1), dice,
                 new[] { BlockWorld.Diamond, BlockWorld.Gold, (byte)95, BlockWorld.Rainbow, BlockWorld.Cloud, (byte)252, (byte)246 }, 7);
            world.SetBlock(cx - 2, y0 + 1, cz + 1, BlockWorld.Lantern);
            WriteSpot(Key(slot, "skyislands"), new Vector3Int(cx, y0 + 1, cz));
            return;
        }
    }

    // --- The quest board, in the middle of the village ---

    public static bool IsQuestBoard(BlockWorld world, Vector3Int cell)
    {
        return world.SignText(cell).StartsWith("QUEST BOARD");
    }

    static void PutUpQuestBoard(BlockWorld world, int slot)
    {
        Vector3Int village = ReadSpot(Key(slot, "village"));   // (that's 2 along from the well, at ground level)
        if (village.y <= 0) return;

        // Somewhere free just by the well
        int[,] tries = { { 1, -4 }, { -4, 1 }, { 1, 1 }, { 2, -1 }, { -1, 2 } };
        for (int i = 0; i < tries.GetLength(0); i++)
        {
            Vector3Int at = new Vector3Int(village.x + tries[i, 0], village.y + 1, village.z + tries[i, 1]);
            if (world.GetBlock(at.x, at.y, at.z) != BlockWorld.Air || world.GetBlock(at.x, at.y - 1, at.z) == BlockWorld.Air) continue;
            Sign(world, at, "QUEST BOARD - press E for jobs!");
            WriteSpot(Key(slot, "board"), at);
            return;
        }
    }
}
