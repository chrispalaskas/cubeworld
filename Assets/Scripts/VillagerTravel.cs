using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Villagers going through portals.
//
// Any villager who wanders onto a linked portal pops out of the next one,
// just like you. Magic portals they use on purpose: now and then a villager
// heads for one and goes through - ordinary villagers walk there and visit
// the Magic Land, and magic villagers fly there and visit your home world.
// Later they go back home again.
//
// Only one place is open at a time, so the game remembers, for each world,
// who is away visiting. That's how they're waiting for you when you get
// there. Take any villager on the leash through a magic portal and they come
// with you.
//
// This is part of Villager.
public partial class Villager
{
    const float WalkToPortalRange = 30f;   // ordinary villagers won't walk further than this to a portal
    const int MostTravellers = 8;          // how many ordinary villagers can be off visiting the Magic Land at once

    static readonly string[] MagicLandVisitorHellos =
        { "Wow, floating islands!", "Don't look down!", "It's so purple here!", "Have you met the wizards?" };

    private bool waitingToStepOff;   // just came out of a linked portal, so don't go straight back in

    // --- Remembering who is where ---
    //
    // "magicfolk": every magic villager who lives in this world's Magic Land
    // "visitors":  the magic villagers who are visiting your home world
    // "travellers": the ordinary villagers who are visiting the Magic Land

    static bool InMagicLand { get { return WorldMenu.CurrentPlace == WorldSaves.MagicLand; } }

    static string Key(string what)
    {
        return "world" + (WorldMenu.CurrentSlot + 1) + "-" + what;
    }

    static List<string> LoadNames(string what)
    {
        List<string> names = new List<string>();
        if (WorldMenu.CurrentSlot < 0) return names;

        foreach (string name in PlayerPrefs.GetString(Key(what), "").Split(','))
            if (name != "") names.Add(name);
        return names;
    }

    static void SaveNames(string what, List<string> names)
    {
        if (WorldMenu.CurrentSlot < 0) return;
        PlayerPrefs.SetString(Key(what), string.Join(",", names));
        PlayerPrefs.Save();
    }

    // Where visitors hang around when they arrive: by the magic portal,
    // or where you started the world if there isn't one
    static Vector3 NearMagicPortal(BlockWorld world)
    {
        Vector3 start = world.FindSpawnPoint();
        Vector3Int portal;
        return FindNearestMagicPortal(world, start, out portal) ? (Vector3)portal : start;
    }

    // A little village of ordinary people, close to where you start - minus
    // any who are off visiting the Magic Land. In the Magic Land, it's just
    // those visitors, by the portal.
    public static void SpawnAll(BlockWorld world)
    {
        if (WorldMenu.CurrentPlace == WorldSaves.Nether) return;   // far too hot for villagers!

        List<string> away = LoadNames("travellers");

        if (InMagicLand)
        {
            Vector3 portal = NearMagicPortal(world);
            foreach (string name in away)
            {
                Vector3 spot;
                if (FindSpotNear(world, portal, 6, out spot, true))
                    SpawnAt(world, spot, false, name);
            }
            return;
        }

        Vector3 middle = world.FindSpawnPoint();
        for (int i = 0; i < HowMany - away.Count; i++)
        {
            Vector3 spot;
            if (!FindSpotNear(world, middle, 8, out spot)) continue;

            // Someone new - not one of the people who are away
            string name = Names[Random.Range(0, Names.Length)];
            for (int tries = 0; tries < 20 && away.Contains(name); tries++)
                name = Names[Random.Range(0, Names.Length)];

            SpawnAt(world, spot, false, name);
        }
    }

    // --- Magic portals ---

    string HelloLine()
    {
        if (isFire) return FireHello();
        if (isMagic) return MagicHello();
        if (InMagicLand) return MagicLandVisitorHellos[Random.Range(0, MagicLandVisitorHellos.Length)];
        return Hellos[Random.Range(0, Hellos.Length)];
    }

    string TripLine()
    {
        if (isMagic) return InMagicLand ? "I'm off to visit the other world!" : "Time to go home to the Magic Land!";
        return InMagicLand ? "Time to go home!" : "I'm going to see the Magic Land!";
    }

    // Heads for the nearest magic portal (if it's within reach) and goes through
    IEnumerator VisitOtherWorld(float maxDistance)
    {
        Vector3Int portal;
        if (!FindNearestMagicPortal(world, transform.position, out portal)) yield break;
        if (FlatDistance(portal) > maxDistance) yield break;
        if (!isMagic && !InMagicLand && LoadNames("travellers").Count >= MostTravellers) yield break;   // it's crowded over there

        Say(TripLine(), 2.5f);
        yield return new WaitForSeconds(1.5f);

        Vector3 onTop = new Vector3(portal.x, portal.y + 0.5f, portal.z);
        if (isMagic)
        {
            yield return FlyTo(onTop);
        }
        else
        {
            yield return WalkTowards(() => onTop, WalkSpeed, 0.3f, 25f);
            if (FlatDistance(onTop) > 0.8f)
            {
                Say("Hmm, I can't find a way to the portal.", 2f);
                yield break;
            }
        }

        if (world.GetBlock(portal.x, portal.y, portal.z) != BlockWorld.MagicPortal)
        {
            Say("Huh? Where did the portal go?", 2f);
            yield break;
        }

        GoThroughPortal();
    }

    // Vanishes in a burst of sparkles, and is remembered as being in the other place
    public void GoThroughPortal()
    {
        // Magic villagers are away when they're at your home; ordinary ones
        // when they're in the Magic Land
        string list = isMagic ? "visitors" : "travellers";
        bool leavingHome = isMagic ? !InMagicLand : InMagicLand;

        List<string> names = LoadNames(list);
        if (leavingHome) names.Remove(villagerName);
        else if (!isMagic || !names.Contains(villagerName)) names.Add(villagerName);
        SaveNames(list, names);

        PortalTraveller.Announce(villagerName + (InMagicLand
            ? " went through the magic portal to your home world!"
            : " went through the magic portal to the Magic Land!"));

        SparkleBurst(transform.position + Vector3.up * 0.6f, 25);
        Destroy(gameObject);
    }

    public bool IsOnLeash { get { return holder != null; } }

    // The closest magic portal in this world that you could stand on
    public static bool FindNearestMagicPortal(BlockWorld world, Vector3 near, out Vector3Int found)
    {
        found = Vector3Int.zero;
        float best = float.MaxValue;

        for (int x = 0; x < BlockWorld.SizeX; x++)
            for (int y = 0; y < BlockWorld.SizeY - 1; y++)
                for (int z = 0; z < BlockWorld.SizeZ; z++)
                {
                    if (world.GetBlock(x, y, z) != BlockWorld.MagicPortal) continue;
                    if (world.GetBlock(x, y + 1, z) != BlockWorld.Air) continue;   // built over

                    float distance = (new Vector3(x, y, z) - near).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        found = new Vector3Int(x, y, z);
                    }
                }

        return best < float.MaxValue;
    }

    // --- Linked portals ---

    // Stepped onto a linked portal? Out of the next one we pop - and that
    // becomes the new neighbourhood we wander around
    void HopThroughLinkedPortal()
    {
        if (holder != null || !OnGround()) return;

        Vector3Int under = Vector3Int.RoundToInt(transform.position + Vector3.down * 0.3f);
        if (world.GetBlock(under.x, under.y, under.z) != BlockWorld.Portal)
        {
            waitingToStepOff = false;
            return;
        }
        if (waitingToStepOff) return;
        waitingToStepOff = true;

        Vector3Int to;
        if (!PortalTraveller.FindNextPortal(world, under, out to)) return;

        SparkleBurst(transform.position + Vector3.up * 0.6f, 10);
        transform.position = new Vector3(to.x, to.y + 0.5f, to.z);
        home = transform.position;
        Say("Whoosh!", 1.5f);
    }
}
