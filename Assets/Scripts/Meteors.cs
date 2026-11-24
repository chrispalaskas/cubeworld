using System.Collections.Generic;
using UnityEngine;

// Meteor showers: some nights the sky fills with shooting stars, streaking
// across overhead. A few of them come right down - whoosh... BOOM! - and
// land not far from you, leaving a little crater. In the middle is a glowing
// star fragment (crystal), often with some gold, and sometimes a diamond.
// Go and find them!
public class MeteorShower : MonoBehaviour
{
    const float ShowerSeconds = 55f;
    const float Chance = 0.4f;   // how often a night has one

    private BlockWorld world;
    private bool wasNight;
    private float showerEnds = -1f;
    private float nextStreak, nextFaller;
    private int fallersLeft;
    private readonly List<Vector3> landed = new List<Vector3>();
    private static Material streakLook;

    public static void Begin(BlockWorld world)
    {
        new GameObject("Meteor Showers").AddComponent<MeteorShower>().world = world;
    }

    void Update()
    {
        if (BlockInteractor.Me == null || BlockInteractor.InventoryOpen) return;
        Vector3 me = BlockInteractor.Me.transform.position;

        // A new night - is there a shower tonight?
        bool night = DayNight.IsNight;
        if (night && !wasNight && Random.value < Chance) StartShower();
        wasNight = night;

        // Found one that came down?
        for (int i = landed.Count - 1; i >= 0; i--)
            if (Vector3.Distance(landed[i], me) < 3.5f)
            {
                Achievements.Unlock("meteor");
                PortalTraveller.Announce("A fallen meteor! Dig out the glowing star fragment - and look for gold and diamonds.");
                landed.RemoveAt(i);
            }

        if (Time.time > showerEnds) return;

        if (Time.time > nextStreak)
        {
            nextStreak = Time.time + Random.Range(0.15f, 0.6f);
            Streak(me);
        }
        if (fallersLeft > 0 && Time.time > nextFaller)
        {
            nextFaller = Time.time + Random.Range(8f, 14f);
            fallersLeft--;
            SendOneDown(me);
        }
    }

    void StartShower()
    {
        showerEnds = Time.time + ShowerSeconds;
        nextFaller = Time.time + 4f;
        fallersLeft = Random.Range(3, 5);
        PortalTraveller.Announce("A METEOR SHOWER! Look up! Some shooting stars are falling - find where they land for treasure!");
    }

    static Material Glow()
    {
        if (streakLook == null)
        {
            streakLook = BlockWorld.MakeMaterial(new Color(1f, 0.95f, 0.7f));
            streakLook.EnableKeyword("_EMISSION");
            streakLook.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.6f) * 3f);
        }
        return streakLook;
    }

    // A shooting star, high up and far away, just for looking at
    void Streak(Vector3 me)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f), far = Random.Range(50f, 90f);
        Vector3 from = me + new Vector3(Mathf.Cos(angle) * far, Random.Range(45f, 70f), Mathf.Sin(angle) * far);
        Vector3 way = new Vector3(Random.Range(-1f, 1f), -0.35f, Random.Range(-1f, 1f)).normalized;
        GameObject star = new GameObject("Shooting Star");
        star.transform.position = from;
        star.transform.rotation = Quaternion.LookRotation(way);
        star.transform.localScale = new Vector3(0.25f, 0.25f, 6f);
        star.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        MeshRenderer look = star.AddComponent<MeshRenderer>();
        look.sharedMaterial = Glow();
        look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        star.AddComponent<FallingMeteor>().Fly(way * 70f, 1.3f, null);
    }

    // One that actually lands, somewhere you can find it
    void SendOneDown(Vector3 me)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f), far = Random.Range(14f, 32f);
            int x = Mathf.RoundToInt(me.x + Mathf.Cos(angle) * far), z = Mathf.RoundToInt(me.z + Mathf.Sin(angle) * far);
            if (x < 3 || z < 3 || x >= BlockWorld.SizeX - 3 || z >= BlockWorld.SizeZ - 3) continue;
            int top = world.GetSurfaceHeight(x, z);
            if (top <= BlockWorld.SeaLevel || top + 2 >= BlockWorld.SizeY) continue;
            if (!AllNatural(x, top, z)) continue;

            Vector3 target = new Vector3(x, top + 0.5f, z);
            Vector3 way = new Vector3(Random.Range(-0.5f, 0.5f), -1f, Random.Range(-0.5f, 0.5f)).normalized;
            float time = 2.2f;
            Vector3 from = target - way * 40f * time;

            GameObject rock = new GameObject("Meteor");
            rock.transform.position = from;
            rock.transform.localScale = Vector3.one * 0.9f;
            rock.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            rock.AddComponent<MeshRenderer>().sharedMaterial = Glow();
            Light light = rock.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.range = 14f;
            light.intensity = 3f;
            rock.AddComponent<FallingMeteor>().Fly(way * 40f, time, () => Land(new Vector3Int(x, top, z)));
            Sounds.Whoosh(me + (from - me).normalized * 8f);
            return;
        }
    }

    bool AllNatural(int x, int top, int z)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
                for (int y = top - 2; y <= Mathf.Min(top + 2, BlockWorld.SizeY - 1); y++)
                    if (!Structures.IsNaturalBlock(world.GetBlock(x + dx, y, z + dz))) return false;
        return true;
    }

    // BOOM - a little crater, with treasure in the bottom
    void Land(Vector3Int top)
    {
        Vector3 at = top + Vector3.up * 0.5f;
        Sounds.Boom(at);
        Villager.SparkleBurst(at + Vector3.up, 14);

        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                float d = new Vector2(dx, dz).magnitude;
                if (d > 2.3f) continue;
                int deep = d < 1.1f ? 2 : 1;
                for (int y = top.y; y > top.y - deep; y--) world.SetBlock(top.x + dx, y, top.z + dz, BlockWorld.Air);
                for (int y = top.y + 1; y <= Mathf.Min(top.y + 3, BlockWorld.SizeY - 1); y++)
                    if (BlockWorld.IsFlower(world.GetBlock(top.x + dx, y, top.z + dz)) || world.GetBlock(top.x + dx, y, top.z + dz) == BlockWorld.Leaves)
                        world.SetBlock(top.x + dx, y, top.z + dz, BlockWorld.Air);
            }

        world.SetBlock(top.x, top.y - 2, top.z, BlockWorld.Crystal);   // the star fragment
        world.SetBlock(top.x + 1, top.y - 1, top.z, BlockWorld.Gold);
        if (Random.value < 0.5f) world.SetBlock(top.x - 1, top.y - 1, top.z, BlockWorld.Gold);
        if (Random.value < 0.3f) world.SetBlock(top.x, top.y - 1, top.z + 1, BlockWorld.Diamond);
        landed.Add(at);

        PlayerHealth player = PlayerHealth.Instance;
        if (player != null && Vector3.Distance(player.transform.position, at) < 3f) player.Hurt(4, "A meteor landed on you!");
    }
}

// Something flying in a straight line for a while, then gone (calling "landed" if it hits)
public class FallingMeteor : MonoBehaviour
{
    private Vector3 velocity;
    private float endsAt;
    private System.Action landed;

    public void Fly(Vector3 velocity, float seconds, System.Action landed)
    {
        this.velocity = velocity;
        endsAt = Time.time + seconds;
        this.landed = landed;
        if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        if (landed != null) transform.Rotate(200f * Time.deltaTime, 300f * Time.deltaTime, 0f);
        if (Time.time < endsAt) return;
        if (landed != null) landed();
        Destroy(gameObject);
    }
}
