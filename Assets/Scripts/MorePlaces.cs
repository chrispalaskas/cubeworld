using UnityEngine;

// More places in the home world (built the same careful way as the
// village - only on untouched ground):
//
//   Underwater temple: crystal walls and glowing ice on the sea floor, with
//                      treasure (and a water-breathing potion!) inside.
//   Sky castle:        on a cloud high above the land, reached by a long
//                      rainbow bridge. Treasure in the courtyard.
//   Pyramid:           in the desert. Jump over the lava pit in the
//                      tunnel - and watch out for the mummy guarding the treasure!
//   Ice caves:         under the snowy land - glowing ice, and frozen treasure.
//   Fun park:          a minecart race track, a parkour course, and a target range.
public static partial class Structures
{
    static void BuildMorePlaces(BlockWorld world, int slot, System.Random dice)
    {
        foreach (string what in new[] { "temple", "castle", "pyramid", "icecave", "funpark" })
        {
            string key = Key(slot, what + "-built");
            bool retry = what == "pyramid" && !PlayerPrefs.HasKey(Key(slot, "pyramid"));   // every world gets a pyramid
            if (PlayerPrefs.HasKey(key) && !retry) continue;
            PlayerPrefs.SetInt(key, 1);
            if (what == "temple") BuildTemple(world, slot, dice);
            else if (what == "castle") BuildSkyCastle(world, slot, dice);
            else if (what == "pyramid") BuildPyramid(world, slot, dice);
            else if (what == "icecave") BuildIceCave(world, slot, dice);
            else BuildFunPark(world, slot, dice);
        }
    }

    public static bool Spot(string what, out Vector3 at)
    {
        Vector3Int spot = ReadSpot(Key(what));
        at = spot;
        return spot.y > 0;
    }

    static bool TopIs(BlockWorld world, int x, int z, byte type)
    {
        int y = world.GetSurfaceHeight(x, z);
        return y > 0 && world.GetBlock(x, y, z) == type;
    }

    // --- The underwater temple ---

    static void BuildTemple(BlockWorld world, int slot, System.Random dice)
    {
        for (int tries = 0; tries < 120; tries++)
        {
            int x = dice.Next(6, BlockWorld.SizeX - 18), z = dice.Next(6, BlockWorld.SizeZ - 18);
            int floor = 99;
            bool deep = true;
            for (int dx = 0; dx <= 10 && deep; dx++)
                for (int dz = 0; dz <= 10 && deep; dz++)
                {
                    int ground = -1;
                    for (int y = BlockWorld.SeaLevel; y > 0; y--)
                        if (world.GetBlock(x + dx, y, z + dz) != BlockWorld.Water) { ground = y; break; }
                    if (world.GetBlock(x + dx, BlockWorld.SeaLevel, z + dz) != BlockWorld.Water || ground > BlockWorld.SeaLevel - 6) deep = false;
                    floor = Mathf.Min(floor, ground);
                }
            if (!deep || floor < 1) continue;
            if (!AllNatural(world, x, floor, z, x + 10, BlockWorld.SeaLevel, z + 10)) continue;

            int y0 = floor + 1;
            Fill(world, x, y0 - 1, z, x + 10, y0 - 1, z + 10, BlockWorld.Crystal);   // the floor
            for (int y = y0; y <= y0 + 4; y++)
                for (int dx = 0; dx <= 10; dx++)
                    for (int dz = 0; dz <= 10; dz++)
                    {
                        bool wall = dx == 0 || dx == 10 || dz == 0 || dz == 10;
                        bool corner = (dx == 0 || dx == 10) && (dz == 0 || dz == 10);
                        if (!wall) continue;
                        bool window = y == y0 + 2 && (dx % 3 == 1 || dz % 3 == 1);
                        world.SetBlock(x + dx, y, z + dz, corner ? BlockWorld.Diamond : window ? BlockWorld.Glass : BlockWorld.Crystal);
                    }
            Fill(world, x, y0 + 5, z, x + 10, y0 + 5, z + 10, BlockWorld.Glass);
            Fill(world, x + 4, y0, z, x + 6, y0 + 2, z, BlockWorld.Water);   // the way in
            foreach (int gx in new[] { x + 2, x + 8 })
                foreach (int gz in new[] { z + 2, z + 8 })
                    world.SetBlock(gx, y0, gz, BlockWorld.GlowIce);
            Loot(world, new Vector3Int(x + 5, y0, z + 8), dice, new[] { BlockWorld.Diamond, BlockWorld.Gold, (byte)254, (byte)95, BlockWorld.Crystal, (byte)245 }, 6);
            WriteSpot(Key(slot, "temple"), new Vector3Int(x + 5, y0, z + 5));
            return;
        }
    }

    // --- The sky castle and the rainbow bridge ---

    static void BuildSkyCastle(BlockWorld world, int slot, System.Random dice)
    {
        Vector3 start = world.FindSpawnPoint();
        int baseY = BlockWorld.SizeY - 14;
        for (int tries = 0; tries < 60; tries++)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f, far = 35f + (float)dice.NextDouble() * 20f;
            int cx = Mathf.RoundToInt(start.x + Mathf.Cos(angle) * far), cz = Mathf.RoundToInt(start.z + Mathf.Sin(angle) * far);
            if (cx < 10 || cz < 10 || cx > BlockWorld.SizeX - 10 || cz > BlockWorld.SizeZ - 10) continue;
            if (!AllNatural(world, cx - 8, baseY - 2, cz - 8, cx + 8, BlockWorld.SizeY - 1, cz + 8)) continue;
            bool clear = true;
            for (int x = cx - 8; x <= cx + 8 && clear; x++)
                for (int z = cz - 8; z <= cz + 8 && clear; z++)
                    for (int y = baseY - 2; y < BlockWorld.SizeY; y++)
                        if (world.GetBlock(x, y, z) != BlockWorld.Air) { clear = false; break; }
            if (!clear) continue;

            // A cloud to sit on
            for (int x = cx - 8; x <= cx + 8; x++)
                for (int z = cz - 8; z <= cz + 8; z++)
                    if ((x - cx) * (x - cx) + (z - cz) * (z - cz) <= 70) world.SetBlock(x, baseY - 1, z, BlockWorld.Cloud);

            // The castle: walls with a gate, towers in the corners, flags on top
            int top = baseY + 4;
            for (int x = cx - 5; x <= cx + 5; x++)
                for (int z = cz - 5; z <= cz + 5; z++)
                {
                    bool edge = Mathf.Abs(x - cx) == 5 || Mathf.Abs(z - cz) == 5;
                    world.SetBlock(x, baseY - 1, z, BlockWorld.Cobblestone);
                    if (!edge) continue;
                    for (int y = baseY; y <= top; y++) world.SetBlock(x, y, z, BlockWorld.Cobblestone);
                    if ((x + z) % 2 == 0) world.SetBlock(x, top + 1, z, BlockWorld.Cobblestone);   // battlements
                }
            foreach (int tx in new[] { cx - 5, cx + 5 })
                foreach (int tz in new[] { cz - 5, cz + 5 })
                {
                    for (int y = baseY; y <= top + 3; y++) world.SetBlock(tx, y, tz, BlockWorld.Brick);
                    world.SetBlock(tx, top + 4, tz, BlockWorld.Fence);
                    world.SetBlock(tx, top + 5, tz, (byte)(BlockWorld.WoolFirst + 1));   // a red flag
                }
            Vector3 toStart = new Vector3(start.x - cx, 0f, start.z - cz).normalized;
            Vector3Int gate = Mathf.Abs(toStart.x) > Mathf.Abs(toStart.z) ? new Vector3Int((int)Mathf.Sign(toStart.x), 0, 0) : new Vector3Int(0, 0, (int)Mathf.Sign(toStart.z));
            for (int y = baseY; y <= baseY + 2; y++)
                for (int w = -1; w <= 1; w++)
                    world.SetBlock(cx + gate.x * 5 + (gate.z != 0 ? w : 0), y, cz + gate.z * 5 + (gate.x != 0 ? w : 0), BlockWorld.Air);
            world.SetBlock(cx, baseY, cz, BlockWorld.Lantern);
            Loot(world, new Vector3Int(cx - gate.x * 3, baseY, cz - gate.z * 3), dice,
                 new[] { (byte)66, BlockWorld.Diamond, BlockWorld.Gold, (byte)95, BlockWorld.Crystal, (byte)247 }, 6);

            // The rainbow bridge: six colours wide, stepping down towards where you started
            Vector3 along = new Vector3(gate.x, 0f, gate.z);
            Vector3 side = new Vector3(-gate.z, 0f, gate.x);
            Vector3 at = new Vector3(cx, baseY - 1, cz) + along * 6f;
            for (int step = 0; step < 90; step++)
            {
                int y = Mathf.RoundToInt(at.y);
                bool landed = false;
                for (int lane = 0; lane < 6; lane++)
                {
                    Vector3 cell = at + side * (lane - 2.5f);
                    int bx = Mathf.RoundToInt(cell.x), bz = Mathf.RoundToInt(cell.z);
                    if (!BlockWorld.InBounds(bx, y, bz)) { landed = true; continue; }
                    if (world.GetSurfaceHeight(bx, bz) >= y && world.GetBlock(bx, y, bz) != BlockWorld.Air) landed = true;
                    else if (world.GetBlock(bx, y, bz) == BlockWorld.Air) world.SetBlock(bx, y, bz, (byte)(BlockWorld.RainbowRed + lane));
                }
                if (landed) break;
                at += along;
                if (step % 2 == 1) at.y -= 1f;
            }

            WriteSpot(Key(slot, "castle"), new Vector3Int(cx, baseY, cz));
            return;
        }
    }

    // --- The pyramid ---

    public static bool MummyDefeated
    {
        get { return PlayerPrefs.GetInt(Key("mummy-defeated"), 0) == 1; }
        set { PlayerPrefs.SetInt(Key("mummy-defeated"), value ? 1 : 0); }
    }

    static void BuildPyramid(BlockWorld world, int slot, System.Random dice)
    {
        if (BuildPyramidInDesert(world, slot, dice)) return;

        // No desert here (an older world)? Then make a little desert of its own -
        // somewhere fairly flat and dry, well away from the village.
        Vector3 village;
        bool hasVillage = VillageCentre(out village);
        int[] bumpiness = { 3, 5, 8 };
        foreach (int bumpy in bumpiness)
            for (int tries = 0; tries < 400; tries++)
            {
                int x = dice.Next(16, BlockWorld.SizeX - 30), z = dice.Next(16, BlockWorld.SizeZ - 30);
                if (hasVillage && Vector3.Distance(new Vector3(x + 6, village.y, z + 6), village) < 34f) continue;

                int low = 999, high = -1, total = 0, count = 0;
                bool dry = true;
                for (int dx = -2; dx <= 14 && dry; dx += 2)
                    for (int dz = -2; dz <= 14 && dry; dz += 2)
                    {
                        int g = GroundAt(world, x + dx, z + dz);
                        if (g <= BlockWorld.SeaLevel + 1) dry = false;
                        low = Mathf.Min(low, g); high = Mathf.Max(high, g);
                        total += g; count++;
                    }
                if (!dry || high - low > bumpy) continue;
                int ground = Mathf.RoundToInt((float)total / count);
                if (ground + 14 >= BlockWorld.SizeY || ground - 5 < 1) continue;
                if (!AllNatural(world, x - 2, ground - 5, z - 2, x + 14, BlockWorld.SizeY - 1, z + 14)) continue;

                // Flatten it: stone underneath, sand on top, and clear above
                for (int px = x - 2; px <= x + 14; px++)
                    for (int pz = z - 2; pz <= z + 14; pz++)
                    {
                        for (int y = ground - 5; y < ground; y++)
                        {
                            byte below = world.GetBlock(px, y, pz);
                            if (below == BlockWorld.Air || below == BlockWorld.Water || below == BlockWorld.Lava) world.SetBlock(px, y, pz, BlockWorld.Stone);
                        }
                        world.SetBlock(px, ground, pz, BlockWorld.Sand);
                        for (int y = ground + 1; y < BlockWorld.SizeY; y++) world.SetBlock(px, y, pz, BlockWorld.Air);
                    }

                // A ring of desert around it, with a few cactuses
                for (int px = x - 12; px <= x + 24; px++)
                    for (int pz = z - 12; pz <= z + 24; pz++)
                    {
                        if (px >= x - 2 && px <= x + 14 && pz >= z - 2 && pz <= z + 14) continue;
                        float far = new Vector2(px - (x + 6), pz - (z + 6)).magnitude;
                        if (far > 18f || (far > 15f && dice.NextDouble() < 0.5)) continue;
                        int top = GroundAt(world, px, pz);
                        if (top <= 0 || top + 4 >= BlockWorld.SizeY) continue;
                        byte was = world.GetBlock(px, top, pz);
                        if (was != BlockWorld.Grass && was != BlockWorld.Dirt && was != BlockWorld.Snow) continue;
                        if (!AllNatural(world, px, top, pz, px, Mathf.Min(top + 3, BlockWorld.SizeY - 1), pz)) continue;
                        world.SetBlock(px, top, pz, BlockWorld.Sand);
                        byte above = world.GetBlock(px, top + 1, pz);
                        if (BlockWorld.IsFlower(above)) world.SetBlock(px, top + 1, pz, BlockWorld.Air);
                        if (above == BlockWorld.Air && dice.NextDouble() < 0.02)
                            for (int c = 1; c <= dice.Next(1, 4); c++) world.SetBlock(px, top + c, pz, BlockWorld.Cactus);
                    }

                MakePyramid(world, slot, dice, x, z, ground);
                return;
            }
    }

    static bool BuildPyramidInDesert(BlockWorld world, int slot, System.Random dice)
    {
        for (int tries = 0; tries < 150; tries++)
        {
            int x = dice.Next(4, BlockWorld.SizeX - 18), z = dice.Next(4, BlockWorld.SizeZ - 18);
            int low = 99, high = 0;
            bool sandy = true;
            for (int dx = 0; dx <= 12 && sandy; dx += 2)
                for (int dz = 0; dz <= 12 && sandy; dz += 2)
                {
                    int g = GroundAt(world, x + dx, z + dz);
                    if (g <= BlockWorld.SeaLevel + 1 || !TopIs(world, x + dx, z + dz, BlockWorld.Sand)) sandy = false;
                    low = Mathf.Min(low, g); high = Mathf.Max(high, g);
                }
            if (!sandy || high - low > 3 || low + 10 >= BlockWorld.SizeY) continue;
            if (!AllNatural(world, x, low - 1, z, x + 12, low + 9, z + 12)) continue;

            MakePyramid(world, slot, dice, x, z, low);
            return true;
        }
        return false;
    }

    static void MakePyramid(BlockWorld world, int slot, System.Random dice, int x, int z, int ground)
    {
        {
            for (int layer = 0; layer <= 6; layer++)
                for (int dx = layer; dx <= 12 - layer; dx++)
                    for (int dz = layer; dz <= 12 - layer; dz++)
                        world.SetBlock(x + dx, ground + layer, z + dz, layer == 6 ? BlockWorld.Gold : BlockWorld.Sand);
            // Sand falls - so give it a hidden stone core underneath (and hollow out the chamber)
            Fill(world, x + 3, ground + 1, z + 3, x + 9, ground + 3, z + 9, BlockWorld.Air);
            Fill(world, x + 3, ground, z + 3, x + 9, ground, z + 9, BlockWorld.Gold);
            Fill(world, x + 3, ground + 4, z + 3, x + 9, ground + 4, z + 9, BlockWorld.Stone);   // (so the sand above doesn't fall in)
            world.SetBlock(x + 6, ground + 3, z + 6, BlockWorld.Lantern);

            // The tunnel in - with a lava pit to jump over!
            for (int dz = 0; dz < 3; dz++) Fill(world, x + 6, ground + 1, z + dz, x + 6, ground + 2, z + dz, BlockWorld.Air);
            Fill(world, x + 5, ground + 3, z, x + 7, ground + 3, z + 2, BlockWorld.Stone);
            world.SetBlock(x + 6, ground, z + 1, BlockWorld.Air);
            world.SetBlock(x + 6, ground - 1, z + 1, BlockWorld.Lava);

            Loot(world, new Vector3Int(x + 6, ground + 1, z + 8), dice, new[] { BlockWorld.Gold, BlockWorld.Diamond, (byte)95, BlockWorld.TreasureMap, (byte)244 }, 7);
            WriteSpot(Key(slot, "pyramid"), new Vector3Int(x + 6, ground + 1, z + 6));
        }
    }

    // --- The ice caves ---

    static void BuildIceCave(BlockWorld world, int slot, System.Random dice)
    {
        for (int tries = 0; tries < 150; tries++)
        {
            int x = dice.Next(4, BlockWorld.SizeX - 24), z = dice.Next(4, BlockWorld.SizeZ - 10);
            if (!TopIs(world, x, z, BlockWorld.Snow) || !TopIs(world, x + 18, z, BlockWorld.Snow)) continue;
            int surface = GroundAt(world, x, z);
            int floor = surface - 9;
            if (floor < 4) continue;
            if (!AllNatural(world, x, floor - 1, z - 2, x + 20, surface, z + 4)) continue;

            // A long icy tunnel...
            Fill(world, x + 2, floor - 1, z - 2, x + 20, floor + 4, z + 4, BlockWorld.Ice);
            Fill(world, x + 3, floor, z - 1, x + 19, floor + 3, z + 3, BlockWorld.Air);
            for (int i = x + 4; i <= x + 18; i += 4)
            {
                world.SetBlock(i, floor + 3, z - 1, BlockWorld.GlowIce);
                world.SetBlock(i, floor + 3, z + 3, BlockWorld.GlowIce);
            }
            // ...with icy steps down to it from the snow
            for (int s = 0; s <= 9; s++)
            {
                int sx = x + 2 - s / 3, sy = surface - s;
                if (sx < 1) break;
                Fill(world, x + 1, sy, z, x + 3, sy + 2, z + 2, BlockWorld.Air);
                world.SetBlock(x + 1, sy - 1, z + 1, BlockWorld.Ice);
            }
            Fill(world, x, floor, z, x + 3, floor + 2, z + 2, BlockWorld.Air);
            Loot(world, new Vector3Int(x + 19, floor, z + 1), dice, new[] { BlockWorld.Diamond, (byte)245, BlockWorld.Ice, BlockWorld.GlowIce, (byte)95, BlockWorld.Snow }, 6);
            WriteSpot(Key(slot, "icecave"), new Vector3Int(x + 11, floor, z + 1));
            return;
        }
    }

    // --- The fun park ---

    public static bool RaceStart(out Vector3Int start)
    {
        start = ReadSpot(Key("race-start"));
        return start.y > 0;
    }

    static void BuildFunPark(BlockWorld world, int slot, System.Random dice)
    {
        Vector3 start = world.FindSpawnPoint();
        for (int tries = 0; tries < 80; tries++)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f, far = 18f + (float)dice.NextDouble() * 22f;
            int x0 = Mathf.RoundToInt(start.x + Mathf.Cos(angle) * far), z0 = Mathf.RoundToInt(start.z + Mathf.Sin(angle) * far);
            if (x0 < 3 || z0 < 3 || x0 + 26 >= BlockWorld.SizeX - 2 || z0 + 18 >= BlockWorld.SizeZ - 2) continue;

            int low = 99, high = 0;
            bool ok = true;
            for (int x = x0; x <= x0 + 26 && ok; x += 2)
                for (int z = z0; z <= z0 + 18 && ok; z += 2)
                {
                    int g = GroundAt(world, x, z);
                    if (g <= BlockWorld.SeaLevel) ok = false;
                    low = Mathf.Min(low, g); high = Mathf.Max(high, g);
                }
            if (!ok || high - low > 4) continue;
            int ground = low + 1;
            if (ground + 12 >= BlockWorld.SizeY) continue;
            if (!AllNatural(world, x0, ground - 3, z0, x0 + 26, ground + 12, z0 + 18)) continue;

            // A flat grassy park
            for (int x = x0; x <= x0 + 26; x++)
                for (int z = z0; z <= z0 + 18; z++)
                {
                    for (int y = ground - 3; y < ground; y++) world.SetBlock(x, y, z, y == ground - 1 ? BlockWorld.Grass : BlockWorld.Dirt);
                    for (int y = ground; y <= ground + 12; y++) world.SetBlock(x, y, z, BlockWorld.Air);
                }

            // The race track: a loop of rails, with a minecart waiting at the start
            int rx0 = x0 + 1, rx1 = x0 + 14, rz0 = z0 + 1, rz1 = z0 + 8;
            for (int x = rx0; x <= rx1; x++)
            {
                world.SetBlock(x, ground, rz0, BlockWorld.RailEW);
                world.SetBlock(x, ground, rz1, BlockWorld.RailEW);
            }
            for (int z = rz0 + 1; z < rz1; z++)
            {
                world.SetBlock(rx0, ground, z, BlockWorld.RailNS);
                world.SetBlock(rx1, ground, z, BlockWorld.RailNS);
            }
            Vector3Int raceStart = new Vector3Int(rx0 + 4, ground, rz0);
            world.SetBlock(raceStart.x, ground - 1, raceStart.z, BlockWorld.StartPad);
            Cart.PutOn(world, raceStart);
            Sign(world, new Vector3Int(rx0 + 4, ground, rz0 - 1 < z0 ? rz0 + 1 : rz0 - 1), "RACE TRACK - ride a lap!");

            // The parkour course: jump from block to block, up to the finish
            Vector3Int pad = new Vector3Int(x0 + 18, ground - 1, z0 + 2);
            world.SetBlock(pad.x, pad.y, pad.z, BlockWorld.StartPad);
            Vector3Int hop = pad;
            for (int i = 0; i < 9; i++)
            {
                hop += new Vector3Int(i % 3 == 2 ? -1 : 0, 1, 2);
                world.SetBlock(hop.x, hop.y, hop.z, (byte)(BlockWorld.WoolFirst + 1 + i % 6));
            }
            hop += new Vector3Int(0, 1, 2);
            world.SetBlock(hop.x, hop.y, hop.z, BlockWorld.FinishPad);
            Sign(world, pad + new Vector3Int(1, 1, 0), "PARKOUR! Start on green, finish on the flag");

            // The target range: three targets on posts, and a fence to stand behind
            for (int i = 0; i < 3; i++)
            {
                int tx = x0 + 4 + i * 4, tz = z0 + 17;
                world.SetBlock(tx, ground, tz, BlockWorld.Fence);
                world.SetBlock(tx, ground + 1, tz, BlockWorld.Target);
            }
            for (int x = x0 + 2; x <= x0 + 14; x++) world.SetBlock(x, ground, z0 + 11, BlockWorld.Fence);
            Sign(world, new Vector3Int(x0 + 8, ground, z0 + 10), "TARGET RANGE - shoot the targets for coins!");

            WriteSpot(Key(slot, "funpark"), new Vector3Int(x0 + 13, ground, z0 + 9));
            WriteSpot(Key(slot, "race-start"), raceStart);
            return;
        }
    }

    static void Sign(BlockWorld world, Vector3Int at, string words)
    {
        world.SetBlock(at.x, at.y, at.z, BlockWorld.SignFirst);
        world.SetSignText(at, words);
    }
}

// The mini-games, and noticing when you find the new places
public class MiniGames : MonoBehaviour
{
    private BlockWorld world;
    private float parkourStarted = -1f;
    private float lapStarted = -1f;
    private float leftStartAt;
    private bool onStart;
    private float checkTimer;
    private Mummy mummy;

    public static void Begin(BlockWorld world)
    {
        new GameObject("Mini Games").AddComponent<MiniGames>().world = world;
    }

    string Key(string what) { return "world" + (WorldMenu.CurrentSlot + 1) + "-" + what; }

    void Update()
    {
        BlockInteractor hands = BlockInteractor.Me;
        if (hands == null || BlockInteractor.InventoryOpen) return;
        Vector3 me = hands.transform.position;
        FirstPersonPlayer player = hands.GetComponent<FirstPersonPlayer>();

        // Parkour: start on the green pad, finish on the chequered flag
        Vector3Int under = Vector3Int.RoundToInt(me + Vector3.down * 0.3f);
        byte below = world.GetBlock(under.x, under.y, under.z);
        if (below == BlockWorld.StartPad && player.InVehicle == null && parkourStarted < 0f)
        {
            parkourStarted = Time.time;
            PortalTraveller.Announce("Parkour! Get to the finish as fast as you can - GO!");
        }
        if (below == BlockWorld.FinishPad && parkourStarted > 0f)
        {
            float time = Time.time - parkourStarted;
            parkourStarted = -1f;
            float best = PlayerPrefs.GetFloat(Key("parkour-best"), 999f);
            if (time < best) PlayerPrefs.SetFloat(Key("parkour-best"), time);
            Wallet.Earn(3, "Parkour done in " + time.ToString("0.0") + " seconds" + (time < best ? " - a new best!" : "!"));
            Achievements.Unlock("parkour");
        }
        if (parkourStarted > 0f && Time.time - parkourStarted > 90f) parkourStarted = -1f;   // gave up

        // The race: a lap of the track in a minecart
        Vector3Int start;
        if (player.InVehicle is Cart && Structures.RaceStart(out start))
        {
            bool atStart = Vector3.Distance(new Vector3(me.x, start.y, me.z), start) < 1.2f;
            if (atStart && !onStart && Time.time - leftStartAt > 3f)
            {
                if (lapStarted > 0f)
                {
                    float lap = Time.time - lapStarted;
                    float best = PlayerPrefs.GetFloat(Key("race-best"), 999f);
                    if (lap < best) PlayerPrefs.SetFloat(Key("race-best"), lap);
                    Wallet.Earn(2, "Lap time " + lap.ToString("0.0") + " seconds" + (lap < best ? " - a new record!" : "!"));
                    Achievements.Unlock("race");
                }
                else PortalTraveller.Announce("Race on! Ride all the way round the track.");
                lapStarted = Time.time;
            }
            if (!atStart && onStart) leftStartAt = Time.time;
            onStart = atStart;
        }
        else lapStarted = -1f;

        // Every second: found any of the special places? And the mummy wakes up...
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 1f;
        Vector3 spot;
        if (Structures.Spot("temple", out spot) && Vector3.Distance(spot, me) < 7f) Achievements.Unlock("temple");
        if (Structures.Spot("castle", out spot) && Vector3.Distance(spot, me) < 8f) Achievements.Unlock("castle");
        if (Structures.Spot("icecave", out spot) && Vector3.Distance(spot, me) < 9f) Achievements.Unlock("icecave");
        if (WorldMenu.Survival && !Mods.NoMonsters && mummy == null && !Structures.MummyDefeated && Structures.Spot("pyramid", out spot) && Vector3.Distance(spot, me) < 7f)
        {
            mummy = Monster.Spawn<Mummy>(world, spot + Vector3.down * 0.5f);
            mummy.Post = spot;
            PortalTraveller.Announce("Something stirs in the dark... THE MUMMY!");
        }
    }
}
