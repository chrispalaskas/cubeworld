using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Machines: buttons, pressure plates and timers send out power; cable
// carries it; and dispensers, pistons, signal lights, note blocks, doors and
// TNT do something when it reaches them. (Conveyor belts don't need power -
// see FirstPersonPlayer.)
//
// Buttons, cables and dispensers:
//
// Press a button (E, or right click) and it sends out a pulse of power. The
// power runs along any cable that's joined to it - you can see the cable
// light up as it goes - and when it reaches something, that thing happens:
//   - a dispenser shoots out one of whatever's inside it
//   - TNT is lit
//   - a door opens (or shuts)
//   - a piston pushes the blocks in front of it one space along
//   - a signal light glows
//   - a note block plays its note
// Power can come from a pressure plate (when you, or any creature, steps on
// it) or a timer (every couple of seconds, all by itself) as well as a button.
// A dispenser, door or TNT right next to the button works too, without any cable.
//
// Fill a dispenser with E, just like a chest. What comes out depends on what
// it is: TNT comes out already lit, eggs hatch, fireworks launch, saplings
// grow into trees, and blocks (and water, lava and fire) are placed in front.
// If something's already in front, it shoots further out instead, to the
// first free space (so pressing again and again builds a line of blocks).
//
// This is part of BlockInteractor - it's you who presses the button.
public partial class BlockInteractor
{
    const int LongestCable = 300;           // power gives up after this many pieces of cable
    const float PowerStepSeconds = 0.04f;   // how fast power runs along a cable
    const float CableGlowSeconds = 0.8f;    // how long cable stays lit after the power has passed
    const int FurthestShot = 16;            // how far out a dispenser looks for a free space
    const int MostPushed = 12;              // a piston can push a row of up to this many blocks
    const float TimerSeconds = 2f;          // how often timers send out power

    // The notes a note block can play: two octaves of a major scale, as
    // steps up from middle C. E on a note block moves it up to the next one.
    static readonly int[] Scale = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23, 24 };
    static readonly Dictionary<int, AudioClip> noteSounds = new Dictionary<int, AudioClip>();

    private float timerClock;
    private float plateCheck;
    private HashSet<Vector3Int> platesPressed = new HashSet<Vector3Int>();

    static readonly Vector3Int[] SixWays =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    bool LookingAtButton(out Vector3Int button)
    {
        Vector3Int unused;
        return LookingAtBlock(out button, out unused)
            && BlockWorld.Instance.GetBlock(button.x, button.y, button.z) == BlockWorld.Button;
    }

    // Runs every frame: timers tick, and pressure plates notice who's standing on them
    void RunMachines()
    {
        BlockWorld world = BlockWorld.Instance;
        if (world == null) return;

        timerClock += Time.deltaTime;
        if (timerClock >= TimerSeconds)
        {
            timerClock = 0f;
            foreach (Vector3Int timer in new List<Vector3Int>(world.Timers))
            {
                Villager.SparkleBurst(timer + Vector3.up * 0.6f, 2);
                StartCoroutine(SendPower(timer));
            }
        }

        plateCheck -= Time.deltaTime;
        if (plateCheck > 0f) return;
        plateCheck = 0.1f;

        // Where everyone's standing: you, and every creature
        HashSet<Vector3Int> standing = new HashSet<Vector3Int>();
        StandingOnPlate(transform.position, world, standing);
        foreach (Villager person in FindObjectsByType<Villager>(FindObjectsSortMode.None)) StandingOnPlate(person.transform.position, world, standing);
        foreach (Chicken chicken in FindObjectsByType<Chicken>(FindObjectsSortMode.None)) StandingOnPlate(chicken.transform.position, world, standing);
        foreach (Squirrel squirrel in FindObjectsByType<Squirrel>(FindObjectsSortMode.None)) StandingOnPlate(squirrel.transform.position, world, standing);
        foreach (Unicorn unicorn in FindObjectsByType<Unicorn>(FindObjectsSortMode.None)) StandingOnPlate(unicorn.transform.position, world, standing);
        foreach (Dragon dragon in FindObjectsByType<Dragon>(FindObjectsSortMode.None)) StandingOnPlate(dragon.transform.position, world, standing);
        foreach (Monster monster in Monster.All) StandingOnPlate(monster.transform.position, world, standing);   // monster traps!

        // Someone just stepped on: power! Someone stepped off: the plate pops back up.
        foreach (Vector3Int plate in standing)
            if (!platesPressed.Contains(plate))
            {
                world.SetPlatePressed(plate, true);
                StartCoroutine(SendPower(plate));
            }
        foreach (Vector3Int plate in platesPressed)
            if (!standing.Contains(plate))
                world.SetPlatePressed(plate, false);
        platesPressed = standing;
    }

    static void StandingOnPlate(Vector3 feet, BlockWorld world, HashSet<Vector3Int> plates)
    {
        Vector3Int cell = Vector3Int.RoundToInt(feet + Vector3.up * 0.2f);
        if (world.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.PressurePlate) plates.Add(cell);
    }

    void PressButton(Vector3Int button)
    {
        Villager.SparkleBurst(button + Vector3.up * 0.6f, 4);
        StartCoroutine(SendPower(button));
    }

    // Pistons push away from you, and conveyor belts carry things away from
    // you, when you place them - like stairs going up away from you
    byte FacingAway(byte north)
    {
        Vector3 f = transform.forward;
        if (Mathf.Abs(f.z) >= Mathf.Abs(f.x))
            return (byte)(north + (f.z > 0f ? 0 : 2));
        return (byte)(north + (f.x > 0f ? 1 : 3));
    }

    bool LookingAtNoteBlock(out Vector3Int note)
    {
        Vector3Int unused;
        return LookingAtBlock(out note, out unused)
            && BlockWorld.Instance.GetBlock(note.x, note.y, note.z) == BlockWorld.NoteBlock;
    }

    // E on a note block moves it up to the next note (and back down to the
    // bottom after the top one), and plays it so you can hear
    void TuneNote(Vector3Int cell)
    {
        BlockWorld world = BlockWorld.Instance;
        byte note = (byte)((world.NoteAt(cell) + 1) % Scale.Length);
        world.SetNote(cell, note);
        PlayNote(cell);
    }

    // A dispenser faces you when you place it (so you can see its front)
    byte DispenserFacingMe()
    {
        Vector3 f = transform.forward;
        if (Mathf.Abs(f.z) >= Mathf.Abs(f.x))
            return f.z > 0f ? BlockWorld.DispenserSouth : BlockWorld.DispenserNorth;
        return f.x > 0f ? BlockWorld.DispenserWest : BlockWorld.DispenserEast;
    }

    // --- Power ---

    // Spreads out from the button one step at a time, through cable, so you
    // can see it travel. Everything it reaches gets powered once.
    IEnumerator SendPower(Vector3Int button)
    {
        BlockWorld world = BlockWorld.Instance;
        Queue<Vector3Int> reached = new Queue<Vector3Int>();
        HashSet<Vector3Int> done = new HashSet<Vector3Int>();
        List<Vector3Int> litCable = new List<Vector3Int>();
        List<Vector3Int> litLights = new List<Vector3Int>();

        reached.Enqueue(button);
        done.Add(button);

        while (reached.Count > 0 && litCable.Count < LongestCable)
        {
            // Everything one step further along, all at once
            int thisStep = reached.Count;
            for (int i = 0; i < thisStep; i++)
            {
                Vector3Int from = reached.Dequeue();
                foreach (Vector3Int way in SixWays)
                {
                    Vector3Int next = from + way;
                    if (done.Contains(next)) continue;
                    done.Add(next);

                    byte type = world.GetBlock(next.x, next.y, next.z);
                    if (type == BlockWorld.Cable)
                    {
                        world.SetCablePowered(next, true);
                        litCable.Add(next);
                        reached.Enqueue(next);
                    }
                    else if (BlockWorld.IsDispenser(type))
                        Dispense(next, type);
                    else if (Tnt.IsExplosive(type))
                        Tnt.Ignite(world, next, type == BlockWorld.MegaTnt ? Tnt.MegaFuseSeconds : Tnt.FuseSeconds);
                    else if (type == BlockWorld.SignalLight)
                    {
                        world.SetSignalLight(next, true);
                        litLights.Add(next);
                    }
                    else if (type == BlockWorld.NoteBlock)
                        PlayNote(next);
                    else if (BlockWorld.IsPiston(type))
                        Push(next, type);
                    else if (BlockWorld.IsDoor(type))
                    {
                        // Both halves of a door might be reached - only open it once!
                        Vector3Int otherHalf;
                        if (OtherHalfOfDoor(next, out otherHalf)) done.Add(otherHalf);
                        OpenOrCloseDoor(next);
                    }
                }
            }

            yield return new WaitForSeconds(PowerStepSeconds);
        }

        yield return new WaitForSeconds(CableGlowSeconds);
        foreach (Vector3Int cable in litCable)
            world.SetCablePowered(cable, false);
        foreach (Vector3Int light in litLights)
            world.SetSignalLight(light, false);
    }

    // --- Note blocks ---

    void PlayNote(Vector3Int cell)
    {
        int step = Scale[BlockWorld.Instance.NoteAt(cell) % Scale.Length];
        AudioSource.PlayClipAtPoint(NoteSound(step), cell, 0.8f);
        Villager.SparkleBurst(cell + Vector3.up * 0.7f, 3);   // a few sparkles, so you can see which one played
    }

    // A bell-like "ding": a note that starts loud and fades away. Each one is
    // made once, the first time it's needed.
    static AudioClip NoteSound(int step)
    {
        AudioClip clip;
        if (noteSounds.TryGetValue(step, out clip) && clip != null) return clip;

        const int rate = 44100;
        const float seconds = 0.9f;
        float frequency = 261.63f * Mathf.Pow(2f, step / 12f);   // middle C, and up from there

        float[] samples = new float[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float fade = Mathf.Exp(-t * 5f);
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t);
            samples[i] = wave * fade * 0.4f;
        }

        clip = AudioClip.Create("Note " + step, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        noteSounds[step] = clip;
        return clip;
    }

    // --- Pistons ---

    // Pushes the row of blocks in front of the piston one space along - if
    // there's room at the far end, and nothing in the row refuses to budge
    void Push(Vector3Int at, byte type)
    {
        BlockWorld world = BlockWorld.Instance;
        Vector3Int way = BlockWorld.PistonFacing(type);

        List<Vector3Int> row = new List<Vector3Int>();
        Vector3Int cell = at + way;
        while (!HasRoom(cell))
        {
            byte there = world.GetBlock(cell.x, cell.y, cell.z);
            if (!BlockWorld.InBounds(cell.x, cell.y, cell.z) || !CanBePushed(there)) return;
            row.Add(cell);
            if (row.Count > MostPushed) return;   // too heavy!
            cell += way;
        }

        // Move them along, starting from the far end so nothing gets squashed
        for (int i = row.Count - 1; i >= 0; i--)
        {
            Vector3Int from = row[i];
            world.SetBlock(from.x + way.x, from.y + way.y, from.z + way.z, world.GetBlock(from.x, from.y, from.z));
        }
        if (row.Count > 0)
        {
            world.SetBlock(row[0].x, row[0].y, row[0].z, BlockWorld.Air);
            Villager.SparkleBurst(at + (Vector3)way * 0.6f, 4);
        }
    }

    // Chests and dispensers (with things inside), doors (in two halves) and
    // note blocks (with their tune) stay where they are
    static bool CanBePushed(byte type)
    {
        return !BlockWorld.IsContainer(type) && !BlockWorld.IsDoor(type) && type != BlockWorld.NoteBlock;
    }

    // --- Dispensers ---

    // Shoots out one thing from the first space that has something in it
    void Dispense(Vector3Int at, byte type)
    {
        BlockWorld.ChestContents inside = BlockWorld.Instance.ChestAt(at);
        Vector3Int facing = BlockWorld.DispenserFacing(type);
        // The first free space in front - right in front if it's free, or
        // further out past whatever's in the way
        Vector3Int front = at + facing;
        for (int step = 1; step <= FurthestShot && !HasRoom(front); step++)
            front = at + facing * (step + 1);
        if (!HasRoom(front)) return;   // solid all the way - nowhere for anything to go

        for (int space = 0; space < BlockWorld.ChestContents.Spaces; space++)
        {
            byte item = inside.items[space];
            if (item == BlockWorld.Air) continue;

            if (ShootOut(item, front, facing))
            {
                inside.counts[space]--;
                if (inside.counts[space] == 0) inside.items[space] = BlockWorld.Air;
                Villager.SparkleBurst(front, 6);   // a little puff as it comes out
            }
            return;   // only the first thing - one per press
        }
    }

    // Does whatever this item does, coming out of a dispenser into "front".
    // Returns false if it couldn't come out (so it stays in the dispenser).
    bool ShootOut(byte item, Vector3Int front, Vector3Int facing)
    {
        BlockWorld world = BlockWorld.Instance;
        bool room = HasRoom(front);
        Vector3 feet = new Vector3(front.x, front.y - 0.5f, front.z);   // where a hatched creature stands

        // Things you hold, rather than place, can't be dispensed
        if (item == Sponge || item == Leash || item == Wings || IsSword(item) || item == BlockWorld.DoorItem)
            return false;

        if (!room) return false;   // something's in the way

        switch (item)
        {
            case ChickenEgg:       ChickenRespawner.Hatch(feet); return true;
            case VillagerEgg:      Villager.Hatch(world, feet); return true;
            case MagicVillagerEgg: Villager.HatchMagic(world, feet); return true;
            case FireVillagerEgg:  Villager.HatchFire(world, feet); return true;
            case UnicornEgg:       Unicorn.Hatch(world, feet); return true;
            case DragonEgg:        Dragon.Hatch(world, feet); return true;
            case FireworkRocket:   Firework.Launch(front); return true;
            case Sapling:          return world.GrowTreeAt(front.x, front.y, front.z);
            case RainbowArch:      world.BuildRainbow(front, facing); return true;
        }

        // Anything else is a block: it's placed in front. TNT comes out lit!
        world.SetBlock(front.x, front.y, front.z, item);
        if (item == BlockWorld.Tnt)
            Tnt.Ignite(world, front, Tnt.FuseSeconds);
        else if (item == BlockWorld.MegaTnt)
            Tnt.Ignite(world, front, Tnt.MegaFuseSeconds);
        return true;
    }

    // Empty space (or water, lava or fire, which things can go into)
    static bool HasRoom(Vector3Int cell)
    {
        byte there = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
        return BlockWorld.InBounds(cell.x, cell.y, cell.z) &&
               (there == BlockWorld.Air || there == BlockWorld.Water || there == BlockWorld.Lava || there == BlockWorld.Fire);
    }

    // --- Pictures for the inventory ---

    static Texture2D cablePicture, dispenserPicture;

    static Texture2D CablePicture()
    {
        if (cablePicture == null)
            cablePicture = PixelArt(new[]
            {
                "........",
                "........",
                "...r....",
                "rrrrrrrr",
                "rrrrrrrr",
                "...r....",
                "...r....",
                "........",
            });
        return cablePicture;
    }

    static Texture2D DispenserPicture()
    {
        if (dispenserPicture == null)
            dispenserPicture = PixelArt(new[]
            {
                "ssssssss",
                "ssssssss",
                "sskkkkss",
                "sskkkkss",
                "sskkkkss",
                "sskkkkss",
                "ssssssss",
                "ssssssss",
            });
        return dispenserPicture;
    }
}
