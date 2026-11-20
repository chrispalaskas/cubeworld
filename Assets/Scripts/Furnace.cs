using System.Collections.Generic;
using UnityEngine;

// The furnace - for cooking food and baking blocks.
//
// Aim at it and press E (or right click):
//   holding something it can cook:  in it goes (everything you're holding)
//   holding fuel:                   on the fire it goes
//   holding anything else:          take out whatever's finished
// Each time it tells you what's cooking, how much fuel is left, and what's ready.
//
//   Raw Chicken  -> Cooked Chicken  (much more filling!)
//   Sand         -> Glass
//   Cobblestone  -> Stone
//   Ice          -> Water
//
// Fuel: wood, planks, saplings, fire crystals, or lava (which burns for ages).
// It cooks one thing every 4 seconds, and you can see the fire while it's going.
public partial class BlockWorld
{
    public const byte RawChicken = 96;      // (the same number as the chicken leg in BlockInteractor)
    public const byte CookedChicken = 171;

    const float CookSeconds = 4f;
    const float FuelSeconds = 8f;           // how long one bit of fuel burns

    // Inside a furnace (kept in the same place as a chest's things):
    //   space 0: what's waiting to be cooked
    //   space 1: how much fuel is left (the count)
    //   space 2: what's finished
    const int InputSpace = 0, FuelSpace = 1, OutputSpace = 2;

    private readonly HashSet<Vector3Int> furnaces = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, float> burning = new Dictionary<Vector3Int, float>();
    private readonly Dictionary<Vector3Int, float> cooking = new Dictionary<Vector3Int, float>();

    public static byte CookedFrom(byte item)
    {
        switch (item)
        {
            case RawChicken:  return CookedChicken;
            case RawBeef:     return Steak;
            case RawPork:     return CookedPork;
            case RawFish:     return CookedFish;
            case Sand:        return Glass;
            case Cobblestone: return Stone;
            case Ice:         return Water;
            default:          return Air;
        }
    }

    // How many bits of fuel one of these is worth (0 if it won't burn)
    public static int FuelIn(byte item)
    {
        switch (item)
        {
            case Wood:        return 4;
            case Planks:      return 2;
            case 28:          return 1;    // a sapling
            case FireCrystal: return 8;
            case Lava:        return 12;
            default:          return 0;
        }
    }

    void TrackFurnace(Vector3Int cell, byte type)
    {
        if (IsFurnace(type)) furnaces.Add(cell);
        else
        {
            furnaces.Remove(cell);
            burning.Remove(cell);
            cooking.Remove(cell);
        }
    }

    public bool FurnaceLit(Vector3Int cell)
    {
        float left;
        return burning.TryGetValue(cell, out left) && left > 0f;
    }

    public void FurnaceContents(Vector3Int cell, out byte input, out int inputCount, out int fuel, out byte output, out int outputCount)
    {
        ChestContents inside = ChestAt(cell);
        input = inside.items[InputSpace];
        inputCount = inside.counts[InputSpace];
        fuel = inside.counts[FuelSpace];
        output = inside.items[OutputSpace];
        outputCount = inside.counts[OutputSpace];
    }

    // Puts things in to cook. Returns how many went in.
    public int AddToFurnace(Vector3Int cell, byte item, int howMany)
    {
        ChestContents inside = ChestAt(cell);
        if (inside.items[InputSpace] != Air && inside.items[InputSpace] != item) return 0;
        howMany = Mathf.Min(howMany, ChestContents.MostInASpace - inside.counts[InputSpace]);
        inside.items[InputSpace] = item;
        inside.counts[InputSpace] = (byte)(inside.counts[InputSpace] + howMany);
        return howMany;
    }

    // Puts fuel on. Returns how many went on.
    public int AddFuel(Vector3Int cell, byte item, int howMany)
    {
        ChestContents inside = ChestAt(cell);
        int each = FuelIn(item);
        howMany = Mathf.Min(howMany, (255 - inside.counts[FuelSpace]) / each);
        inside.items[FuelSpace] = Planks;   // (just so the space doesn't look empty)
        inside.counts[FuelSpace] = (byte)(inside.counts[FuelSpace] + howMany * each);
        return howMany;
    }

    // Takes out everything that's finished
    public bool TakeFromFurnace(Vector3Int cell, out byte item, out int howMany)
    {
        ChestContents inside = ChestAt(cell);
        item = inside.items[OutputSpace];
        howMany = inside.counts[OutputSpace];
        inside.items[OutputSpace] = Air;
        inside.counts[OutputSpace] = 0;
        return item != Air && howMany > 0;
    }

    // Every frame: each furnace burns its fuel and cooks
    void RunFurnaces()
    {
        if (furnaces.Count == 0) return;
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;   // paused

        foreach (Vector3Int cell in furnaces)
        {
            ChestContents inside = ChestAt(cell);
            byte input = inside.items[InputSpace];
            byte result = CookedFrom(input);
            byte output = inside.items[OutputSpace];
            bool canCook = input != Air && result != Air && (output == Air || output == result) &&
                           inside.counts[OutputSpace] < ChestContents.MostInASpace;

            float fire, progress;
            burning.TryGetValue(cell, out fire);
            cooking.TryGetValue(cell, out progress);

            // Need more fuel?
            if (canCook && fire <= 0f && inside.counts[FuelSpace] > 0)
            {
                inside.counts[FuelSpace]--;
                if (inside.counts[FuelSpace] == 0) inside.items[FuelSpace] = Air;
                fire = FuelSeconds;
            }

            if (fire > 0f)
            {
                fire -= Time.deltaTime;
                if (canCook)
                {
                    progress += Time.deltaTime;
                    if (progress >= CookSeconds)
                    {
                        progress = 0f;
                        inside.counts[InputSpace]--;
                        if (inside.counts[InputSpace] == 0) inside.items[InputSpace] = Air;
                        inside.items[OutputSpace] = result;
                        inside.counts[OutputSpace]++;
                    }
                }
            }
            else
            {
                progress = 0f;
            }

            burning[cell] = fire;
            cooking[cell] = progress;
        }
    }

    public Material FurnaceGlowLook { get { return furnitureLooks[(int)Look.OvenGlow]; } }
}

// Shows a furnace's fire (and a warm light) only while it's burning
public class FurnaceFire : MonoBehaviour
{
    private Renderer fire;
    private Light glow;
    private Vector3Int cell;

    void Start()
    {
        cell = Vector3Int.RoundToInt(transform.localPosition);
        foreach (Renderer part in GetComponentsInChildren<Renderer>())
            if (part.sharedMaterial == BlockWorld.Instance.FurnaceGlowLook) fire = part;

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.6f, 0.25f);
        glow.range = 5f;
        glow.intensity = 1.6f;
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        bool lit = BlockWorld.Instance != null && BlockWorld.Instance.FurnaceLit(cell);
        if (fire != null) fire.enabled = lit;
        glow.enabled = lit;
        if (lit) glow.intensity = 1.4f + Mathf.Sin(Time.time * 13f) * 0.3f;
    }
}

// Using a furnace. This is part of BlockInteractor.
public partial class BlockInteractor
{
    void UseFurnace(Vector3Int cell)
    {
        BlockWorld world = BlockWorld.Instance;
        byte holding = selected;
        string said = "";

        // How many to put in: everything in your hand (in creative, 8 at a time)
        int have = Survival ? HowMany(holding) : 8;

        if (holding != BlockWorld.Air && BlockWorld.CookedFrom(holding) != BlockWorld.Air)
        {
            int went = world.AddToFurnace(cell, holding, have);
            if (went == 0) said = "The furnace is already cooking something else. ";
            else
            {
                UseUp(holding, went);
                said = "In go " + went + " " + ItemNames[holding] + ". ";
            }
        }
        else if (holding != BlockWorld.Air && BlockWorld.FuelIn(holding) > 0)
        {
            int went = world.AddFuel(cell, holding, have);
            UseUp(holding, went);
            said = went > 0 ? "You put " + went + " " + ItemNames[holding] + " on the fire. " : "It can't hold any more fuel. ";
        }
        else
        {
            byte item;
            int count;
            if (world.TakeFromFurnace(cell, out item, out count))
            {
                if (Survival) Give(item, count);
                else PutInHotbar(item);
                Sounds.Pop(cell);
                Achievements.Unlock("cook");
                said = "You took out " + count + " " + ItemNames[item] + "! ";
            }
        }

        // And how it's getting on
        byte input, output;
        int inputCount, fuel, outputCount;
        world.FurnaceContents(cell, out input, out inputCount, out fuel, out output, out outputCount);

        string status = input != BlockWorld.Air ? "Cooking: " + inputCount + " " + ItemNames[input] : "Nothing to cook";
        status += fuel > 0 || world.FurnaceLit(cell) ? ".  Fuel: " + fuel + " left" : ".  No fuel - add wood or planks";
        if (output != BlockWorld.Air) status += ".  Ready: " + outputCount + " " + ItemNames[output] + " (E with an empty hand to take)";
        PortalTraveller.Announce(said + status + ".");
    }
}
