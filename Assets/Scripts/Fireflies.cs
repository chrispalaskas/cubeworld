using System.Collections.Generic;
using UnityEngine;

// Fireflies: little swarms of glowing dots that come out at night over the
// grass and in the jungle, drifting and blinking. Hold a block of glass, look
// at a swarm and press E to catch them - and you get a Firefly Jar, a lamp
// you can put anywhere that glows a soft yellow-green.
public partial class BlockWorld
{
    public const byte FireflyJar = 200;

    private Material jarGlass, jarLid, fireflyGlow;

    void CreateFireflyMaterials()
    {
        jarGlass = MakeSeeThrough(new Color(0.85f, 0.95f, 0.9f, 0.3f));
        jarLid = MakeMaterial(new Color(0.45f, 0.3f, 0.15f));
        fireflyGlow = MakeGlowingMaterial((x, y) => new Color(0.85f, 1f, 0.35f));
        materials[FireflyJar] = fireflyGlow;
    }

    void BuildFireflyJar(GameObject cube)
    {
        ModelPiece(cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.4f, 0.6f, 0.4f), jarGlass, true);
        ModelPiece(cube, new Vector3(0f, 0.13f, 0f), new Vector3(0.3f, 0.07f, 0.3f), jarLid, false);
        System.Random dice = new System.Random(cube.GetInstanceID());
        for (int i = 0; i < 6; i++)
        {
            Vector3 at = new Vector3((float)dice.NextDouble() * 0.28f - 0.14f, -0.42f + (float)dice.NextDouble() * 0.45f, (float)dice.NextDouble() * 0.28f - 0.14f);
            ModelPiece(cube, at, Vector3.one * 0.04f, fireflyGlow, false);
        }
        AddLight(cube, new Color(0.8f, 1f, 0.4f), 7f, 1.2f);
        cube.AddComponent<JarTwinkle>();
    }
}

// The fireflies in a jar buzz about a little
public class JarTwinkle : MonoBehaviour
{
    private Light glow;
    void Start() { glow = GetComponent<Light>(); }
    void Update() { if (glow != null) glow.intensity = 1f + Mathf.Sin(Time.time * 3f + transform.position.x) * 0.3f; }
}

// Keeps a few swarms near you at night, and clears them away in the morning
public class FireflySwarms : MonoBehaviour
{
    const int MostSwarms = 6;

    public static readonly List<FireflySwarms> All = new List<FireflySwarms>();
    private static FireflySwarms keeper;
    private BlockWorld world;
    private float timer;
    private static Material glow;

    // (a swarm is one of these too - the keeper is the one with "world" set)
    private bool isSwarm;
    private Transform[] bugs;
    private Vector3[] homes;

    public static void Begin(BlockWorld world)
    {
        keeper = new GameObject("Fireflies").AddComponent<FireflySwarms>();
        keeper.world = world;
    }

    void OnEnable() { if (isSwarm) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        if (isSwarm) { Drift(); return; }

        timer -= Time.deltaTime;
        if (timer > 0f || BlockInteractor.Me == null) return;
        timer = 2f;
        Vector3 me = BlockInteractor.Me.transform.position;

        // Gone in the daytime, and when you're far away
        foreach (FireflySwarms swarm in All.ToArray())
            if (!DayNight.IsNight || Vector3.Distance(swarm.transform.position, me) > 45f) Destroy(swarm.gameObject);
        if (!DayNight.IsNight || All.Count >= MostSwarms) return;

        for (int tries = 0; tries < 10; tries++)
        {
            int x = Mathf.RoundToInt(me.x + Random.Range(-28f, 28f)), z = Mathf.RoundToInt(me.z + Random.Range(-28f, 28f));
            int y = world.GetSurfaceHeight(x, z);
            if (y < 0 || y + 3 >= BlockWorld.SizeY) continue;
            byte top = world.GetBlock(x, y, z);
            if (top != BlockWorld.Grass && top != BlockWorld.Leaves && !BlockWorld.IsFlower(top)) continue;
            MakeSwarm(new Vector3(x, y + 1.6f, z));
            return;
        }
    }

    void MakeSwarm(Vector3 at)
    {
        if (glow == null)
        {
            glow = BlockWorld.MakeMaterial(new Color(0.85f, 1f, 0.35f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.8f, 1f, 0.3f) * 2f);
        }
        FireflySwarms swarm = new GameObject("Firefly Swarm").AddComponent<FireflySwarms>();
        swarm.transform.position = at;
        swarm.isSwarm = true;
        All.Add(swarm);
        swarm.bugs = new Transform[10];
        swarm.homes = new Vector3[10];
        for (int i = 0; i < swarm.bugs.Length; i++)
        {
            GameObject bug = new GameObject("Firefly");
            bug.transform.SetParent(swarm.transform, false);
            bug.transform.localScale = Vector3.one * 0.06f;
            bug.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            MeshRenderer look = bug.AddComponent<MeshRenderer>();
            look.sharedMaterial = glow;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            swarm.bugs[i] = bug.transform;
            swarm.homes[i] = Random.insideUnitSphere * 1.2f;
        }
        Light light = swarm.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.8f, 1f, 0.4f);
        light.range = 5f;
        light.intensity = 0.8f;
    }

    // Wandering about their spot, blinking on and off
    void Drift()
    {
        for (int i = 0; i < bugs.Length; i++)
        {
            float t = Time.time * 0.7f + i * 1.7f;
            bugs[i].localPosition = homes[i] + new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f) * 0.5f, Mathf.Cos(t * 0.8f)) * 0.5f;
            bugs[i].gameObject.SetActive(Mathf.Repeat(t * 0.9f, 1f) < 0.75f);
        }
    }

    // A swarm close to this line of sight (for catching them in a jar)
    public static FireflySwarms InSights(Vector3 eye, Vector3 forward, float reach)
    {
        foreach (FireflySwarms swarm in All)
        {
            Vector3 to = swarm.transform.position - eye;
            float along = Vector3.Dot(to, forward);
            if (along < 0f || along > reach) continue;
            if ((to - forward * along).magnitude < 1.4f) return swarm;
        }
        return null;
    }
}

public partial class BlockInteractor
{
    // Holding glass and looking at fireflies? Catch them!
    bool CatchFireflies()
    {
        if (selected != BlockWorld.Glass) return false;
        FireflySwarms swarm = FireflySwarms.InSights(cam.transform.position, cam.transform.forward, 5f);
        if (swarm == null) return false;
        if (Survival && !UseUp(BlockWorld.Glass)) return false;

        Destroy(swarm.gameObject);
        if (Survival) Give(BlockWorld.FireflyJar);
        else PutInHotbar(BlockWorld.FireflyJar);
        Sounds.Pop(swarm.transform.position);
        Villager.SparkleBurst(swarm.transform.position, 6);
        Achievements.Unlock("firefly");
        PortalTraveller.Announce("You caught the fireflies in a jar! Put it down for a lovely glowing light.");
        return true;
    }
}
