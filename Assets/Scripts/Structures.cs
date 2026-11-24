using UnityEngine;

// Places to find in the home world. Each world gets them the first time you
// visit after they were added - even old worlds. They're only ever built
// where the ground is untouched, so they never land on top of anything you built.
//
//   The village:     little houses (with beds, tables and chests), a well,
//                    and gravel paths - and that's where the villagers live now.
//   Dungeons:        hidden cobblestone rooms deep underground, each with a
//                    monster cage and treasure chests. Break the cage to stop the monsters!
//   The Golem Hall:  a huge room right at the bottom of the world, guarding
//                    the best treasure of all. A shaft with a ladder leads
//                    down to it, under a warning sign. The Stone Golem lives
//                    there (in survival) - a giant who smashes and throws boulders.
//   Buried treasure: hold a treasure map (from dungeon and village chests) and
//                    it points the way to a red X. Dig down three blocks!
public static partial class Structures
{
    static string Key(string what) { return "world" + (WorldMenu.CurrentSlot + 1) + "-" + what; }
    static string Key(int slot, string what) { return "world" + (slot + 1) + "-" + what; }

    public static void Forget(int slot)
    {
        foreach (string what in new[] { "village", "village-built", "dungeons", "golem-hall", "golem-hall-built", "golem-defeated", "treasure",
                                        "temple", "castle", "pyramid", "icecave", "funpark", "mummy-defeated", "race-start", "parkour-best", "race-best",
                                        "temple-built", "castle-built", "pyramid-built", "icecave-built", "funpark-built",
                                        "haunted", "haunted-built", "haunted-secret", "volcano", "volcano-built", "skyislands", "skyislands-built",
                                        "board", "board-round", "board-done", "yeti-defeated" })
            PlayerPrefs.DeleteKey(Key(slot, what));
    }

    public static void BuildIfMissing(BlockWorld world, int slot)
    {
        System.Random dice = new System.Random();
        // Every world gets a village - old worlds that missed out on one get another go
        Vector3 village;
        if (!PlayerPrefs.HasKey(Key(slot, "village-built")) || !HasVillage(slot, out village))
        {
            PlayerPrefs.SetInt(Key(slot, "village-built"), 1);
            BuildVillage(world, slot, dice);
        }
        if (!PlayerPrefs.HasKey(Key(slot, "dungeons"))) { PlayerPrefs.SetInt(Key(slot, "dungeons"), 1); BuildDungeons(world, dice); }
        if (!PlayerPrefs.HasKey(Key(slot, "golem-hall-built"))) { PlayerPrefs.SetInt(Key(slot, "golem-hall-built"), 1); BuildGolemHall(world, slot, dice); }
        BuildMorePlaces(world, slot, dice);   // (MorePlaces.cs)
        BuildNewPlaces(world, slot, dice);    // (NewPlaces.cs)
        PlayerPrefs.Save();
    }

    // --- Helpers ---

    // Blocks that were made by the world, not by you
    static bool Natural(byte type)
    {
        return type == BlockWorld.Air || type == BlockWorld.Grass || type == BlockWorld.Dirt || type == BlockWorld.Stone ||
               type == BlockWorld.Sand || type == BlockWorld.Snow || type == BlockWorld.Leaves || type == BlockWorld.Wood ||
               type == BlockWorld.Gravel || type == BlockWorld.Iron || type == BlockWorld.Gold || type == BlockWorld.Diamond ||
               type == BlockWorld.Obsidian || type == BlockWorld.Water || type == BlockWorld.Lava || type == BlockWorld.Cactus ||
               type == BlockWorld.Ice || BlockWorld.IsFlower(type);
    }

    static bool AllNatural(BlockWorld world, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        if (!BlockWorld.InBounds(x0, y0, z0) || !BlockWorld.InBounds(x1, y1, z1)) return false;
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    if (!Natural(world.GetBlock(x, y, z))) return false;
        return true;
    }

    // The ground in a column, ignoring trees
    static int GroundAt(BlockWorld world, int x, int z)
    {
        for (int y = BlockWorld.SizeY - 1; y > 0; y--)
        {
            byte type = world.GetBlock(x, y, z);
            if (type == BlockWorld.Air || type == BlockWorld.Leaves || type == BlockWorld.Wood || type == BlockWorld.Cactus) continue;
            return type == BlockWorld.Water || type == BlockWorld.Ice ? -1 : y;
        }
        return -1;
    }

    static void Fill(BlockWorld world, int x0, int y0, int z0, int x1, int y1, int z1, byte type)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    world.SetBlock(x, y, z, type);
    }

    static void Loot(BlockWorld world, Vector3Int at, System.Random dice, byte[] things, int howMany)
    {
        world.SetBlock(at.x, at.y, at.z, BlockWorld.Chest);
        BlockWorld.ChestContents inside = world.ChestAt(at);
        for (int i = 0; i < howMany; i++)
        {
            int space = dice.Next(0, BlockWorld.ChestContents.Spaces);
            byte item = things[dice.Next(0, things.Length)];
            inside.items[space] = item;
            bool onlyOne = item == BlockWorld.TreasureMap || item == BlockWorld.Bow || item == BlockWorld.IronArmour ||
                           item == BlockWorld.DiamondArmour || (item >= 156 && item <= 167);   // (tools)
            inside.counts[space] = (byte)(onlyOne ? 1 : dice.Next(2, 9));
        }
    }

    static Vector3Int ReadSpot(string key)
    {
        string[] bits = PlayerPrefs.GetString(key, "").Split(',');
        int x, y, z;
        if (bits.Length == 3 && int.TryParse(bits[0], out x) && int.TryParse(bits[1], out y) && int.TryParse(bits[2], out z))
            return new Vector3Int(x, y, z);
        return new Vector3Int(-1, -1, -1);
    }

    static void WriteSpot(string key, Vector3Int at) { PlayerPrefs.SetString(key, at.x + "," + at.y + "," + at.z); }

    // --- The village ---

    public static bool VillageCentre(out Vector3 centre)
    {
        Vector3Int spot = ReadSpot(Key("village"));
        centre = new Vector3(spot.x, spot.y + 1, spot.z);
        return spot.y > 0;
    }

    static bool HasVillage(int slot, out Vector3 centre)
    {
        Vector3Int spot = ReadSpot(Key(slot, "village"));
        centre = new Vector3(spot.x, spot.y + 1, spot.z);
        return spot.y > 0;
    }

    const int VillageHalf = 11;

    // Somewhere for the village. Tries nice flat ground near where you start
    // first, then anywhere in the world, then gets less and less fussy - so
    // every world ends up with one. (It still never builds over anything you made.)
    static bool FindVillageSpot(BlockWorld world, System.Random dice, out int bestX, out int bestZ, out int bestLevel)
    {
        const int half = VillageHalf;
        Vector3 start = world.FindSpawnPoint();
        bestX = -1; bestZ = -1; bestLevel = 0;

        // (how bumpy it can be, how much of it can be water, near the start?, tries)
        int[,] passes = { { 5, 0, 1, 60 }, { 6, 0, 0, 300 }, { 10, 10, 0, 400 }, { 16, 35, 0, 600 }, { 40, 60, 0, 800 } };
        for (int pass = 0; pass < passes.GetLength(0); pass++)
        {
            int bumpy = passes[pass, 0], wetPercent = passes[pass, 1], tries = passes[pass, 3];
            bool nearStart = passes[pass, 2] == 1;
            int bestRange = 999;

            for (int t = 0; t < tries; t++)
            {
                int cx, cz;
                if (nearStart)
                {
                    float angle = (float)dice.NextDouble() * Mathf.PI * 2f, far = 25f + (float)dice.NextDouble() * 25f;
                    cx = Mathf.RoundToInt(start.x + Mathf.Cos(angle) * far); cz = Mathf.RoundToInt(start.z + Mathf.Sin(angle) * far);
                }
                else
                {
                    cx = dice.Next(half + 3, BlockWorld.SizeX - half - 3); cz = dice.Next(half + 3, BlockWorld.SizeZ - half - 3);
                }
                if (cx - half < 2 || cz - half < 2 || cx + half >= BlockWorld.SizeX - 2 || cz + half >= BlockWorld.SizeZ - 2) continue;

                int low = 999, high = -1, total = 0, count = 0, wet = 0, all = 0;
                for (int x = cx - half; x <= cx + half; x += 2)
                    for (int z = cz - half; z <= cz + half; z += 2)
                    {
                        all++;
                        int g = GroundAt(world, x, z);
                        if (g <= BlockWorld.SeaLevel + 1) { wet++; continue; }
                        low = Mathf.Min(low, g); high = Mathf.Max(high, g);
                        total += g; count++;
                    }
                if (count == 0 || wet * 100 > wetPercent * all || high - low > bumpy || high - low >= bestRange) continue;
                int level = Mathf.Max(Mathf.RoundToInt((float)total / count), BlockWorld.SeaLevel + 2);
                if (level + 12 >= BlockWorld.SizeY) continue;
                if (!AllNatural(world, cx - half, Mathf.Max(1, level - 14), cz - half, cx + half, BlockWorld.SizeY - 1, cz + half)) continue;

                bestX = cx; bestZ = cz; bestLevel = level; bestRange = high - low;
            }
            if (bestX >= 0) return true;
        }
        return false;
    }

    static void BuildVillage(BlockWorld world, int slot, System.Random dice)
    {
        const int half = VillageHalf;
        int vx, vz, ground;
        if (!FindVillageSpot(world, dice, out vx, out vz, out ground)) return;   // the whole world's been built on!

        // Level the ground: fill in any dips and water, grass on top, and clear the hills and sky above
        for (int x = vx - half; x <= vx + half; x++)
            for (int z = vz - half; z <= vz + half; z++)
            {
                for (int y = Mathf.Max(1, ground - 14); y < ground; y++)
                {
                    byte below = world.GetBlock(x, y, z);
                    if (below == BlockWorld.Air || below == BlockWorld.Water || below == BlockWorld.Ice || below == BlockWorld.Lava)
                        world.SetBlock(x, y, z, BlockWorld.Dirt);
                }
                world.SetBlock(x, ground, z, BlockWorld.Grass);
                for (int y = ground + 1; y < BlockWorld.SizeY; y++) world.SetBlock(x, y, z, BlockWorld.Air);
            }

        // Gravel paths in a cross, and a well in the middle
        for (int i = -half; i <= half; i++)
        {
            world.SetBlock(vx + i, ground, vz, BlockWorld.Gravel);
            world.SetBlock(vx, ground, vz + i, BlockWorld.Gravel);
        }
        Fill(world, vx - 1, ground - 2, vz - 1, vx + 1, ground + 1, vz + 1, BlockWorld.Cobblestone);
        world.SetBlock(vx, ground + 1, vz, BlockWorld.Air);
        world.SetBlock(vx, ground, vz, BlockWorld.Water);
        world.SetBlock(vx, ground - 1, vz, BlockWorld.Water);
        for (int i = 0; i < 4; i++)
        {
            int fx = vx + (i % 2 == 0 ? -1 : 1), fz = vz + (i < 2 ? -1 : 1);
            world.SetBlock(fx, ground + 2, fz, BlockWorld.Fence);
        }
        Fill(world, vx - 1, ground + 3, vz - 1, vx + 1, ground + 3, vz + 1, BlockWorld.PlankSlab);

        // Four houses, one in each corner, with their doors facing the paths
        int[,] corners = { { -6, -6 }, { 6, -6 }, { -6, 6 }, { 6, 6 } };
        for (int h = 0; h < 4; h++)
            BuildHouse(world, vx + corners[h, 0], ground, vz + corners[h, 1], corners[h, 0] < 0 ? 1 : -1, dice);

        WriteSpot(Key(slot, "village"), new Vector3Int(vx + 2, ground, vz + 2));
    }

    // A little 5 x 5 house. "doorSide" is which way (east +1 / west -1) the door faces.
    static void BuildHouse(BlockWorld world, int hx, int ground, int hz, int doorSide, System.Random dice)
    {
        int x0 = hx - 2, x1 = hx + 2, z0 = hz - 2, z1 = hz + 2;
        Fill(world, x0, ground, z0, x1, ground, z1, BlockWorld.Cobblestone);   // floor

        for (int y = ground + 1; y <= ground + 3; y++)
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    bool corner = (x == x0 || x == x1) && (z == z0 || z == z1);
                    world.SetBlock(x, y, z, corner ? BlockWorld.Wood : edge ? BlockWorld.Planks : BlockWorld.Air);
                }
        Fill(world, x0, ground + 4, z0, x1, ground + 4, z1, BlockWorld.Planks);   // roof
        Fill(world, x0 + 1, ground + 5, z0 + 1, x1 - 1, ground + 5, z1 - 1, BlockWorld.PlankSlab);
        world.SetBlock(hx, ground + 4, hz, BlockWorld.Lamp);                       // a light in the ceiling

        // Windows on the north and south walls
        world.SetBlock(hx, ground + 2, z0, BlockWorld.Glass);
        world.SetBlock(hx, ground + 2, z1, BlockWorld.Glass);

        // The door, in the east or west wall
        int dx = doorSide > 0 ? x1 : x0;
        world.SetBlock(dx, ground + 1, hz, BlockWorld.MakeDoor(false, false, false));
        world.SetBlock(dx, ground + 2, hz, BlockWorld.MakeDoor(true, false, false));

        // Inside: a bed, a table and a chest
        world.SetBlock(hx - doorSide, ground + 1, hz - 1, (byte)(BlockWorld.Bed + (doorSide > 0 ? 1 : 3)));
        world.SetBlock(hx, ground + 1, hz + 1, BlockWorld.Table);
        Loot(world, new Vector3Int(hx - doorSide, ground + 1, hz + 1), dice,
             new[] { BlockWorld.Iron, BlockWorld.Planks, (byte)170, (byte)94, BlockWorld.TreasureMap, BlockWorld.Leather, (byte)168, BlockWorld.Feather }, 4);
    }

    // --- Dungeons ---

    static void BuildDungeons(BlockWorld world, System.Random dice)
    {
        int made = 0;
        for (int tries = 0; tries < 80 && made < 4; tries++)
        {
            int x = dice.Next(8, BlockWorld.SizeX - 16), y = dice.Next(5, 15), z = dice.Next(8, BlockWorld.SizeZ - 16);
            if (!AllNatural(world, x, y, z, x + 8, y + 5, z + 8)) continue;
            if (GroundAt(world, x + 4, z + 4) < y + 9) continue;   // must be properly underground

            Fill(world, x, y, z, x + 8, y + 5, z + 8, BlockWorld.Cobblestone);
            Fill(world, x + 1, y + 1, z + 1, x + 7, y + 4, z + 7, BlockWorld.Air);
            for (int i = 0; i < 12; i++)   // the odd patch of gravel on the floor
                world.SetBlock(x + dice.Next(1, 8), y, z + dice.Next(1, 8), BlockWorld.Gravel);

            world.SetBlock(x + 4, y + 1, z + 4, BlockWorld.Spawner);
            byte[] loot = { BlockWorld.Iron, BlockWorld.Gold, BlockWorld.Diamond, (byte)170, BlockWorld.Arrow, BlockWorld.Bow,
                            BlockWorld.TreasureMap, BlockWorld.IronArmour, (byte)95, BlockWorld.StringItem };
            Loot(world, new Vector3Int(x + 1, y + 1, z + 4), dice, loot, 5);
            Loot(world, new Vector3Int(x + 7, y + 1, z + 4), dice, loot, 5);
            made++;
        }
    }

    // --- The Golem Hall ---

    public static bool GolemHall(out Vector3 centre)
    {
        Vector3Int spot = ReadSpot(Key("golem-hall"));
        centre = spot;
        return spot.y > 0;
    }

    public static bool GolemDefeated
    {
        get { return PlayerPrefs.GetInt(Key("golem-defeated"), 0) == 1; }
        set { PlayerPrefs.SetInt(Key("golem-defeated"), value ? 1 : 0); }
    }

    static void BuildGolemHall(BlockWorld world, int slot, System.Random dice)
    {
        Vector3 start = world.FindSpawnPoint();
        for (int tries = 0; tries < 80; tries++)
        {
            int x = dice.Next(4, BlockWorld.SizeX - 20), z = dice.Next(4, BlockWorld.SizeZ - 20), y = 2;
            if (Vector2.Distance(new Vector2(x + 7, z + 7), new Vector2(start.x, start.z)) < 40f) continue;
            if (!AllNatural(world, x, y, z, x + 14, y + 8, z + 14)) continue;

            // The shaft goes down from the surface just outside one wall
            int sx = x + 7, sz = z - 2;
            int top = GroundAt(world, sx, sz);
            if (top < y + 12 || top + 4 >= BlockWorld.SizeY || sz < 2) continue;
            if (!AllNatural(world, sx - 1, y, sz - 1, sx + 1, top + 3, sz + 1)) continue;

            // The hall: obsidian walls, a brick floor, and lamps on pillars
            Fill(world, x, y, z, x + 14, y + 8, z + 14, BlockWorld.Obsidian);
            Fill(world, x + 1, y + 1, z + 1, x + 13, y + 7, z + 13, BlockWorld.Air);
            Fill(world, x + 1, y, z + 1, x + 13, y, z + 13, BlockWorld.Brick);
            foreach (int px in new[] { x + 3, x + 11 })
                foreach (int pz in new[] { z + 3, z + 11 })
                {
                    Fill(world, px, y + 1, pz, px, y + 5, pz, BlockWorld.Brick);
                    world.SetBlock(px, y + 6, pz, BlockWorld.Lamp);
                }

            // The treasure, at the back of the hall
            byte[] loot = { BlockWorld.Diamond, BlockWorld.Gold, BlockWorld.DiamondArmour, (byte)95, BlockWorld.Obsidian, (byte)159 };
            for (int i = 0; i < 3; i++) Loot(world, new Vector3Int(x + 5 + i * 2, y + 1, z + 13), dice, loot, 6);

            // A shaft with a ladder from the surface down into the hall
            Fill(world, sx - 1, y + 1, sz - 1, sx + 1, top, sz + 1, BlockWorld.Cobblestone);
            for (int sy = y + 1; sy <= top + 1; sy++)
            {
                world.SetBlock(sx, sy, sz, BlockWorld.Air);
                world.SetBlock(sx, sy, sz, BlockWorld.LadderFirst);   // against the wall on the south side
            }
            world.SetBlock(sx, y + 1, z, BlockWorld.Air);   // the way in, through the hall wall
            world.SetBlock(sx, y + 2, z, BlockWorld.Air);
            world.SetBlock(sx, y + 1, sz + 1, BlockWorld.Air);
            world.SetBlock(sx, y + 2, sz + 1, BlockWorld.Air);

            // A warning sign at the top
            Vector3Int sign = new Vector3Int(sx + 2, top + 1, sz);
            world.SetBlock(sign.x, sign.y, sign.z, (byte)(BlockWorld.SignFirst + 2));
            world.SetSignText(sign, "DANGER! The Stone Golem lives down here");

            WriteSpot(Key(slot, "golem-hall"), new Vector3Int(x + 7, y + 1, z + 7));
            return;
        }
    }

    // --- Buried treasure ---

    // Where the treasure is buried - and if there isn't any right now, it
    // buries some new treasure not too far from you
    public static Vector3Int Treasure(BlockWorld world, Vector3 near)
    {
        Vector3Int spot = ReadSpot(Key("treasure"));
        if (spot.y > 0 && world.GetBlock(spot.x, spot.y, spot.z) == BlockWorld.Chest) return spot;

        System.Random dice = new System.Random();
        for (int tries = 0; tries < 60; tries++)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f, far = 25f + (float)dice.NextDouble() * 30f;
            int x = Mathf.RoundToInt(near.x + Mathf.Cos(angle) * far), z = Mathf.RoundToInt(near.z + Mathf.Sin(angle) * far);
            if (x < 4 || z < 4 || x >= BlockWorld.SizeX - 4 || z >= BlockWorld.SizeZ - 4) continue;
            int ground = GroundAt(world, x, z);
            if (ground <= BlockWorld.SeaLevel || ground < 6) continue;
            if (!AllNatural(world, x - 2, ground - 4, z - 2, x + 2, ground + 1, z + 2)) continue;

            Vector3Int chest = new Vector3Int(x, ground - 3, z);
            Loot(world, chest, dice, new[] { BlockWorld.Diamond, BlockWorld.Gold, (byte)95, BlockWorld.Iron, BlockWorld.Crystal, BlockWorld.Obsidian }, 6);

            // A big red X on the ground above it
            for (int i = -2; i <= 2; i++)
            {
                if (GroundAt(world, x + i, z + i) == ground) world.SetBlock(x + i, ground, z + i, (byte)(BlockWorld.WoolFirst + 1));
                if (GroundAt(world, x + i, z - i) == ground) world.SetBlock(x + i, ground, z - i, (byte)(BlockWorld.WoolFirst + 1));
            }
            WriteSpot(Key("treasure"), chest);
            return chest;
        }
        return new Vector3Int(-1, -1, -1);
    }

    // Opened or broke a chest - was it the treasure?
    public static void CheckTreasure(Vector3Int chest)
    {
        if (ReadSpot(Key("treasure")) != chest) return;
        PlayerPrefs.DeleteKey(Key("treasure"));
        Achievements.Unlock("treasure");
        PortalTraveller.Announce("You found the buried treasure!");
    }
}

// Keeps an eye on where you are: wakes the Stone Golem when you go into its
// hall, wakes the Star Guardians when you come for the Star Crystal, and
// notices when you find the village
public class StructureWatcher : MonoBehaviour
{
    private BlockWorld world;
    private float timer;
    private Golem golem;
    private bool guardiansOut;
    private float golemsAt;   // when the village is next allowed a new Iron Golem
    private float guardiansBackAt;

    const int Guardians = 3;
    const float GuardiansComeBack = 180f;   // seconds after you beat them, if the crystal is still there

    // In survival, three Star Guardians wake up when you get close to the
    // Star Crystal on its shrine. Beat them all, and you've got 3 minutes
    // to take it before they come back!
    void WatchStarCrystal(Vector3 me)
    {
        if (!WorldMenu.Survival || Mods.NoMonsters) return;
        Vector3Int crystal;
        if (!StarShrine.ShrineCrystal(world, out crystal)) return;   // already taken - it's yours now

        // Walked away? They go back to sleep (and wake up again when you come back)
        if (guardiansOut && Vector3.Distance(me, crystal) > 40f)
        {
            foreach (Monster monster in Monster.All.ToArray())
                if (monster is StarGuardian) Destroy(monster.gameObject);
            guardiansOut = false;
            return;
        }

        int alive = 0;
        foreach (Monster monster in Monster.All)
            if (monster is StarGuardian) alive++;

        if (guardiansOut && alive == 0)
        {
            guardiansOut = false;
            guardiansBackAt = Time.time + GuardiansComeBack;
            PortalTraveller.Announce("You beat the Star Guardians! Quick - take the Star Crystal before they come back!");
            return;
        }

        if (alive > 0 || Time.time < guardiansBackAt) return;
        if (Vector3.Distance(me, crystal) > 22f) return;

        for (int i = 0; i < Guardians; i++)
        {
            float angle = i * Mathf.PI * 2f / Guardians;
            Vector3 spot = crystal + new Vector3(Mathf.Cos(angle) * 3f, 1f, Mathf.Sin(angle) * 3f);
            Monster.Spawn<StarGuardian>(world, spot).Post = crystal;
        }
        guardiansOut = true;
        Sounds.Thunder(crystal);
        PortalTraveller.Announce("The STAR GUARDIANS wake up to protect the Star Crystal!");
    }

    // Two Iron Golems look after the village. If you beat one, a new one
    // turns up after five minutes.
    void WatchVillage(Vector3 village, Vector3 me)
    {
        if (Vector3.Distance(village, me) > 45f || Time.time < golemsAt) return;

        int alive = 0;
        foreach (Monster monster in Monster.All)
            if (monster is IronGolem) alive++;
        if (alive >= 2) return;

        Vector3[] spots = { new Vector3(2f, 0.1f, 0f), new Vector3(0f, 0.1f, 2f) };
        for (int i = alive; i < 2; i++)
            Monster.Spawn<IronGolem>(world, village + spots[i]).Post = village;
        golemsAt = Time.time + 300f;
    }

    public static void Begin(BlockWorld world)
    {
        new GameObject("Structure Watcher").AddComponent<StructureWatcher>().world = world;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || BlockInteractor.Me == null) return;
        timer = 1f;
        Vector3 me = BlockInteractor.Me.transform.position;

        Vector3 village;
        if (Structures.VillageCentre(out village) && Vector3.Distance(village, me) < 14f) Achievements.Unlock("village");

        WatchStarCrystal(me);
        if (Structures.VillageCentre(out village)) WatchVillage(village, me);

        Vector3 hall;
        if (WorldMenu.Survival && !Mods.NoMonsters && golem == null && !Structures.GolemDefeated && Structures.GolemHall(out hall) &&
            Vector3.Distance(hall, me) < 11f)
        {
            golem = Monster.Spawn<Golem>(world, hall + Vector3.down * 0.5f);
            PortalTraveller.Announce("The ground shakes... The STONE GOLEM wakes up!");
            Sounds.Boom(hall);
        }
    }
}
