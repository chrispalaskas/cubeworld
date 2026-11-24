using UnityEngine;

// Keeps an eye on the newest places: wakes the Yeti, lets the ghosts out in
// the haunted house, sets off the volcano, makes sure the village has its
// hot air balloon, and notices when you find each place.
public class NewPlacesWatcher : MonoBehaviour
{
    private BlockWorld world;
    private float timer = 2f;
    private Yeti yeti;
    private float nextGhost;
    private float nextEruption = 40f;
    private bool spookedYou;

    public static void Begin(BlockWorld world)
    {
        new GameObject("New Places Watcher").AddComponent<NewPlacesWatcher>().world = world;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || BlockInteractor.Me == null || BlockInteractor.InventoryOpen) return;
        timer = 1f;
        Vector3 me = BlockInteractor.Me.transform.position;
        Vector3 spot;

        // The hot air balloon, waiting at the village
        bool hasBalloon = false;
        foreach (Vehicle v in Vehicle.All) if (v is Balloon) hasBalloon = true;
        if (!hasBalloon && Structures.VillageCentre(out spot))
            Balloon.Make(world, spot + new Vector3(-5f, -0.5f, -2f));

        // The Yeti, in the middle of the ice caves
        if (WorldMenu.Survival && !Mods.NoMonsters && yeti == null && !Structures.YetiDefeated && Structures.Spot("icecave", out spot) && Vector3.Distance(spot, me) < 8f)
        {
            yeti = Monster.Spawn<Yeti>(world, spot + new Vector3(5f, 0f, 0f));
            Sounds.Monster("yeti", spot);
            PortalTraveller.Announce("A huge ROAR echoes through the ice caves... The YETI is here!");
        }

        // The haunted house
        if (Structures.Spot("haunted", out spot) && Vector3.Distance(spot, me) < 9f)
        {
            if (!spookedYou)
            {
                spookedYou = true;
                Sounds.Monster("ghost", spot);
                PortalTraveller.Announce("Ooooooh... You feel a chill. This house is HAUNTED!");
            }
            if (WorldMenu.Survival && !Mods.NoMonsters && Time.time > nextGhost)
            {
                nextGhost = Time.time + 10f;
                int ghosts = 0;
                foreach (Monster m in Monster.All) if (m is Ghost && Vector3.Distance(m.transform.position, spot) < 12f) ghosts++;
                if (ghosts < 3) Monster.Spawn<Ghost>(world, spot + new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 5f), Random.Range(-3f, 3f)));
            }
        }
        if (Structures.Spot("haunted-secret", out spot) && Vector3.Distance(spot, me) < 2.5f)
        {
            if (Time.time > 3f) Achievements.Unlock("haunted");
        }

        // The volcano: climb to the top, and watch out when it erupts!
        if (Structures.Spot("volcano", out spot))
        {
            if (Vector3.Distance(spot, me) < 5f) Achievements.Unlock("volcano");
            float far = Vector3.Distance(spot, me);
            if (far < 70f && Time.time > nextEruption)
            {
                nextEruption = Time.time + Random.Range(60f, 110f);
                Erupt(spot, far);
            }
        }

        // Standing on a sky island?
        Vector3Int under = Vector3Int.RoundToInt(me + Vector3.down * 0.5f);
        if (me.y > BlockWorld.SizeY - 16 && world.GetBlock(under.x, under.y, under.z) == BlockWorld.Grass) Achievements.Unlock("skyisland");
    }

    void Erupt(Vector3 crater, float far)
    {
        Sounds.Rumble(crater);
        Sounds.Boom(crater);
        if (far < 45f) PortalTraveller.Announce("RUMBLE RUMBLE... The volcano is ERUPTING! Watch out for lava bombs!");
        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f), out_ = Random.Range(2f, 7f);
            Vector3 velocity = new Vector3(Mathf.Cos(angle) * out_, Random.Range(13f, 19f), Mathf.Sin(angle) * out_);
            MonsterShot.LavaBomb(crater + Vector3.up * 1.2f, velocity);
        }
        Villager.SparkleBurst(crater + Vector3.up * 2f, 10);
    }
}
