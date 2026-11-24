using UnityEngine;

// The Star Shrine and its Star Crystal - something rare and special at the
// top of the highest mountain in your home world.
//
// Climb all the way up and you'll find a little snowy platform with ice
// pillars and lamps at its corners. In the middle sits the Star Crystal,
// shining a beam of starlight high into the sky (so you can spot the
// mountain from far away). Next to it is a treasure chest.
//
// In survival it's guarded! Three Star Guardians - glowing crystal knights -
// wake up when you get close, and fight to protect it (see MoreMonsters.cs).
// Beat them all and you've got 3 minutes to grab it before they come back.
//
// There's only one Star Crystal in each world. Break it to take it with you,
// and put it wherever you like - your house, say:
//   - it glows, twinkles, and shines its beam wherever it goes
//   - in survival, standing near it slowly heals your hearts
//   - monsters are scared of it and won't come close
//
// Every world gets its shrine the first time you visit after this was
// added - even old worlds.
public static class StarShrine
{
    public const float HealRange = 6f;     // how close you need to be to get healed
    public const float ScareRange = 10f;   // how close zombies dare to come

    static string Key(int slot)
    {
        return "world" + (slot + 1) + "-star-shrine";
    }

    // Builds the shrine in this world, if it hasn't had one yet
    private static int buildingFor;
    static string SpotKey(int slot) { return "world" + (slot + 1) + "-star-shrine-spot"; }

    // Where the Star Crystal sits on its shrine - if it's still there.
    // (Worlds from before this was remembered: a Star Crystal on a gold stand is the one.)
    public static bool ShrineCrystal(BlockWorld world, out Vector3Int spot)
    {
        spot = new Vector3Int(-1, -1, -1);
        int slot = WorldMenu.CurrentSlot;
        string[] bits = PlayerPrefs.GetString(SpotKey(slot), "").Split(',');
        int x, y, z;
        if (bits.Length == 3 && int.TryParse(bits[0], out x) && int.TryParse(bits[1], out y) && int.TryParse(bits[2], out z))
            spot = new Vector3Int(x, y, z);
        else
        {
            foreach (Vector3Int cell in world.StarCrystals)
                if (world.GetBlock(cell.x, cell.y - 1, cell.z) == BlockWorld.Gold)
                {
                    spot = cell;
                    PlayerPrefs.SetString(SpotKey(slot), cell.x + "," + cell.y + "," + cell.z);
                    break;
                }
        }
        return spot.y > 0 && world.GetBlock(spot.x, spot.y, spot.z) == BlockWorld.StarCrystal;
    }

    public static void BuildIfMissing(BlockWorld world, int slot)
    {
        buildingFor = slot;
        if (PlayerPrefs.HasKey(Key(slot))) return;
        PlayerPrefs.SetInt(Key(slot), 1);
        PlayerPrefs.Save();

        int peakX, peakY, peakZ;
        if (FindHighestPeak(world, out peakX, out peakY, out peakZ))
            Build(world, peakX, peakY, peakZ);
    }

    public static void Forget(int slot)
    {
        PlayerPrefs.DeleteKey(Key(slot));
        PlayerPrefs.DeleteKey(SpotKey(slot));
    }

    // The highest bit of natural ground (not trees), away from the edges
    static bool FindHighestPeak(BlockWorld world, out int bestX, out int bestY, out int bestZ)
    {
        bestX = bestY = bestZ = -1;
        for (int x = 3; x < BlockWorld.SizeX - 3; x++)
            for (int z = 3; z < BlockWorld.SizeZ - 3; z++)
                for (int y = BlockWorld.SizeY - 7; y > bestY; y--)
                {
                    byte type = world.GetBlock(x, y, z);
                    if (type == BlockWorld.Air || type == BlockWorld.Leaves || type == BlockWorld.Wood) continue;

                    if (type == BlockWorld.Snow || type == BlockWorld.Grass || type == BlockWorld.Stone || type == BlockWorld.Dirt)
                    {
                        bestX = x;
                        bestY = y;
                        bestZ = z;
                    }
                    break;
                }
        return bestY > 0;
    }

    static void Build(BlockWorld world, int px, int h, int pz)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                int x = px + dx, z = pz + dz;

                // A snowy floor with an ice ring round the edge, propped up
                // with stone so it isn't floating
                bool edge = Mathf.Abs(dx) == 2 || Mathf.Abs(dz) == 2;
                world.SetBlock(x, h, z, edge ? BlockWorld.Ice : BlockWorld.Snow);
                for (int y = h - 1; y > h - 8 && world.GetBlock(x, y, z) == BlockWorld.Air; y--)
                    world.SetBlock(x, y, z, BlockWorld.Stone);

                // Clear the sky above it (bits of tree, the very tip of the peak)
                for (int y = h + 1; y <= h + 5; y++)
                    world.SetBlock(x, y, z, BlockWorld.Air);

                // Ice pillars at the corners, each with a lamp on top
                if (Mathf.Abs(dx) == 2 && Mathf.Abs(dz) == 2)
                {
                    for (int y = h + 1; y <= h + 3; y++)
                        world.SetBlock(x, y, z, BlockWorld.Ice);
                    world.SetBlock(x, h + 4, z, BlockWorld.Lamp);
                }
            }

        // The Star Crystal, on a little gold stand
        world.SetBlock(px, h + 1, pz, BlockWorld.Gold);
        world.SetBlock(px, h + 2, pz, BlockWorld.StarCrystal);
        PlayerPrefs.SetString(SpotKey(WorldMenu.CurrentSlot < 0 ? buildingFor : WorldMenu.CurrentSlot), px + "," + (h + 2) + "," + pz);

        // And a treasure chest beside it
        Vector3Int chest = new Vector3Int(px, h + 1, pz + 1);
        world.SetBlock(chest.x, chest.y, chest.z, BlockWorld.Chest);
        BlockWorld.ChestContents inside = world.ChestAt(chest);
        Put(inside, 0, BlockWorld.Diamond, 12);
        Put(inside, 1, BlockWorld.Gold, 12);
        Put(inside, 2, BlockWorld.Obsidian, 8);
        Put(inside, 3, 66, 1);   // wings
        Put(inside, 4, 67, 1);   // a dragon egg
        Put(inside, 5, 65, 1);   // a unicorn egg
        Put(inside, 6, 95, 3);   // golden apples (food, for survival)
        Put(inside, 7, BlockWorld.Crystal, 6);

        Debug.Log("Star Shrine built at " + px + ", " + h + ", " + pz);
    }

    static void Put(BlockWorld.ChestContents inside, int space, byte item, int count)
    {
        inside.items[space] = item;
        inside.counts[space] = (byte)count;
    }

    // The nearest Star Crystal within range, if there is one
    public static bool Near(Vector3 position, float range, out Vector3 star)
    {
        star = Vector3.zero;
        if (BlockWorld.Instance == null) return false;

        float best = range * range;
        bool found = false;
        foreach (Vector3Int cell in BlockWorld.Instance.StarCrystals)
        {
            float distance = (cell - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                star = cell;
                found = true;
            }
        }
        return found;
    }
}

// Makes a Star Crystal's light shimmer, with little sparkles drifting up
public class StarTwinkle : MonoBehaviour
{
    private Light glow;
    private float nextSparkle;
    private static Material sparkleLook;

    void Start()
    {
        glow = GetComponent<Light>();
        if (sparkleLook == null)
            sparkleLook = BlockWorld.MakeMaterial(new Color(0.92f, 0.96f, 1f));
    }

    void Update()
    {
        if (glow != null)
            glow.intensity = 2.5f + Mathf.Sin(Time.time * 2.5f) * 0.8f;

        if (Time.time < nextSparkle || BlockWorld.Instance == null) return;
        nextSparkle = Time.time + 0.25f;

        GameObject sparkle = new GameObject("Star Sparkle");
        sparkle.transform.position = transform.position + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.3f, 0.6f), Random.Range(-0.7f, 0.7f));
        sparkle.transform.localScale = Vector3.one * Random.Range(0.05f, 0.1f);
        sparkle.transform.rotation = Random.rotation;
        sparkle.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        sparkle.AddComponent<MeshRenderer>().sharedMaterial = sparkleLook;
        sparkle.AddComponent<FloatUp>();
        Destroy(sparkle, 1.6f);
    }
}

// A sparkle drifting gently upwards
public class FloatUp : MonoBehaviour
{
    void Update()
    {
        transform.position += Vector3.up * 0.8f * Time.deltaTime;
        transform.Rotate(90f * Time.deltaTime, 120f * Time.deltaTime, 0f);
    }
}
