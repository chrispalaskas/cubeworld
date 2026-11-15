using System.Collections.Generic;
using UnityEngine;

// The fire of the Nether, near you (there's no need to make flames nobody
// can see):
//  - every fire vent erupts every few seconds, shooting a tall column of fire
//    into the air - and if you're standing on it, it launches you up high!
//  - fires on the ground send up sparks
//  - the lava spits little flames
//  - glowing embers drift up through the air all around
public class NetherFire : MonoBehaviour
{
    const float Range = 24f;              // how far from you things happen
    const float EruptSeconds = 1.3f;      // how long an eruption lasts
    const float LookAgainSeconds = 1.5f;  // how often we look for vents near you
    const float ColumnHeight = 7f;        // about how high the fire shoots
    const float LaunchSpeed = 18f;

    // When each vent near you next erupts, and when its eruption ends
    private readonly Dictionary<Vector3Int, float> nextEruption = new Dictionary<Vector3Int, float>();
    private readonly Dictionary<Vector3Int, float> eruptingUntil = new Dictionary<Vector3Int, float>();
    private readonly List<Vector3Int> firesNearby = new List<Vector3Int>();

    private float lookAgain;
    private float tick;
    private float lastLaunch = -100f;

    public static void Begin()
    {
        new GameObject("Nether Fire").AddComponent<NetherFire>();
    }

    void Update()
    {
        BlockWorld world = BlockWorld.Instance;
        Camera cam = Camera.main;
        if (world == null || cam == null) return;

        Vector3 me = cam.transform.position;

        lookAgain -= Time.deltaTime;
        if (lookAgain <= 0f)
        {
            lookAgain = LookAgainSeconds;
            FindVentsAndFires(world, me);
        }

        // Vents erupt in turn, each on its own timer
        foreach (Vector3Int vent in new List<Vector3Int>(nextEruption.Keys))
        {
            if (Time.time >= nextEruption[vent])
            {
                eruptingUntil[vent] = Time.time + EruptSeconds;
                nextEruption[vent] = Time.time + EruptSeconds + Random.Range(2.5f, 6f);
            }

            float until;
            if (eruptingUntil.TryGetValue(vent, out until) && Time.time < until)
                Erupt(vent);
        }

        // The smaller things, a few times a second
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.06f;

        // Sparks off a fire or two
        for (int i = 0; i < 2 && firesNearby.Count > 0; i++)
        {
            Vector3Int fire = firesNearby[Random.Range(0, firesNearby.Count)];
            Dragon.FireBurst(new Vector3(fire.x, fire.y + 0.2f, fire.z), Vector3.up, 1, 3f, 0.7f);
        }

        // The lava spits now and then
        for (int i = 0; i < 3; i++)
        {
            int x = Mathf.RoundToInt(me.x + Random.Range(-Range, Range));
            int z = Mathf.RoundToInt(me.z + Random.Range(-Range, Range));
            int y = world.GetSurfaceHeight(x, z);
            if (y >= 0 && world.GetBlock(x, y, z) == BlockWorld.Lava && Random.value < 0.4f)
                Dragon.FireBurst(new Vector3(x, y + 0.5f, z), Vector3.up, 2, 2.5f, 0.6f);
        }

        // An ember floating up somewhere near you
        Vector3 ember = me + new Vector3(Random.Range(-10f, 10f), Random.Range(-4f, 2f), Random.Range(-10f, 10f));
        Dragon.FireBurst(ember, Vector3.up, 1, 1.2f, 1.6f);
    }

    // Every fire vent and fire on the surface near you. New vents get a
    // timer, and vents that have gone (or that you've left behind) are forgotten.
    void FindVentsAndFires(BlockWorld world, Vector3 me)
    {
        HashSet<Vector3Int> vents = new HashSet<Vector3Int>();
        firesNearby.Clear();

        int range = Mathf.CeilToInt(Range);
        for (int x = Mathf.RoundToInt(me.x) - range; x <= Mathf.RoundToInt(me.x) + range; x++)
        {
            for (int z = Mathf.RoundToInt(me.z) - range; z <= Mathf.RoundToInt(me.z) + range; z++)
            {
                int y = world.GetSurfaceHeight(x, z);
                if (y < 0) continue;

                byte top = world.GetBlock(x, y, z);
                if (top == BlockWorld.FireVent) vents.Add(new Vector3Int(x, y, z));
                else if (top == BlockWorld.Fire) firesNearby.Add(new Vector3Int(x, y, z));
            }
        }

        foreach (Vector3Int vent in vents)
            if (!nextEruption.ContainsKey(vent))
                nextEruption[vent] = Time.time + Random.Range(0.2f, 4f);   // not all at once

        foreach (Vector3Int old in new List<Vector3Int>(nextEruption.Keys))
            if (!vents.Contains(old))
            {
                nextEruption.Remove(old);
                eruptingUntil.Remove(old);
            }
    }

    // A column of fire roaring up out of the vent
    void Erupt(Vector3Int vent)
    {
        Vector3 top = new Vector3(vent.x, vent.y + 0.5f, vent.z);
        float speed = ColumnHeight / 0.6f;   // fast enough to get that high in a flame's short life
        Dragon.FireBurst(top, Vector3.up, 2, speed, 0.6f);

        // Standing on it, or flying low over it? Whoosh - up you go!
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        if (player == null || Time.time - lastLaunch < 1f) return;

        Vector3 feet = player.transform.position;
        Vector2 across = new Vector2(feet.x - vent.x, feet.z - vent.z);
        if (across.magnitude < 0.8f && feet.y >= vent.y && feet.y < vent.y + ColumnHeight)
        {
            lastLaunch = Time.time;
            player.LaunchUp(LaunchSpeed);
            PortalTraveller.Announce("Whoa! The fire vent launched you into the air!");
        }
    }
}
