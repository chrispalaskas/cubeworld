using System.Collections.Generic;
using UnityEngine;

// Jellyfish: wobbly, glowing jellies that drift about on top of the sea, with
// their tentacles trailing down into the water. Their squishy heads stick up
// out of the water - jump onto one and BOING, you're flung high into the air!
// (They're friendly. They just like bouncing people.)
public class Jellyfish : MonoBehaviour
{
    public static readonly List<Jellyfish> All = new List<Jellyfish>();

    const float BounceSpeed = 15f;

    private Vector3 drift;
    private float driftTimer;
    private float squish;            // a big wobble after a bounce
    private Transform bell;
    private Transform[] tentacles;
    private float seed;
    private BlockWorld world;
    private static Material[] colours;
    private static Material tentacleLook;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static Jellyfish Make(BlockWorld world, Vector3 at, Transform parent)
    {
        if (colours == null)
        {
            colours = new Material[3];
            Color[] tints = { new Color(1f, 0.5f, 0.85f, 0.7f), new Color(0.6f, 0.55f, 1f, 0.7f), new Color(0.45f, 0.95f, 1f, 0.7f) };
            for (int i = 0; i < tints.Length; i++)
            {
                colours[i] = BlockWorld.MakeSeeThrough(tints[i]);
                colours[i].EnableKeyword("_EMISSION");
                colours[i].SetColor("_EmissionColor", new Color(tints[i].r, tints[i].g, tints[i].b) * 0.6f);
            }
            tentacleLook = BlockWorld.MakeSeeThrough(new Color(1f, 0.85f, 0.95f, 0.5f));
        }

        Jellyfish jelly = new GameObject("Jellyfish").AddComponent<Jellyfish>();
        jelly.transform.SetParent(parent, false);
        jelly.transform.position = at;
        jelly.world = world;
        jelly.seed = Random.Range(0f, 100f);
        Material look = colours[Random.Range(0, colours.Length)];

        jelly.bell = new GameObject("Bell").transform;
        jelly.bell.SetParent(jelly.transform, false);
        Bit(jelly.bell, new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.4f, 1.1f), look);
        Bit(jelly.bell, new Vector3(0f, 0.4f, 0f), new Vector3(0.75f, 0.2f, 0.75f), look);
        jelly.tentacles = new Transform[6];
        for (int i = 0; i < jelly.tentacles.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / jelly.tentacles.Length;
            Transform t = new GameObject("Tentacle").transform;
            t.SetParent(jelly.transform, false);
            t.localPosition = new Vector3(Mathf.Cos(angle) * 0.35f, -0.05f, Mathf.Sin(angle) * 0.35f);
            Bit(t, new Vector3(0f, -0.45f, 0f), new Vector3(0.06f, 0.9f, 0.06f), tentacleLook);
            jelly.tentacles[i] = t;
        }
        Light light = jelly.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = look.color;
        light.range = 4f;
        light.intensity = 0.6f;
        return jelly;
    }

    static void Bit(Transform parent, Vector3 at, Vector3 size, Material look)
    {
        GameObject bit = new GameObject("Bit");
        bit.transform.SetParent(parent, false);
        bit.transform.localPosition = at;
        bit.transform.localScale = size;
        bit.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        bit.AddComponent<MeshRenderer>().sharedMaterial = look;
    }

    void Update()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        // Drifting slowly about - but only ever on the water
        driftTimer -= Time.deltaTime;
        if (driftTimer <= 0f)
        {
            driftTimer = Random.Range(3f, 7f);
            drift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 0.4f;
        }
        Vector3 next = transform.position + drift * Time.deltaTime;
        Vector3Int cell = Vector3Int.RoundToInt(next);
        if (world.GetBlock(cell.x, BlockWorld.SeaLevel, cell.z) == BlockWorld.Water && world.GetBlock(cell.x, BlockWorld.SeaLevel + 1, cell.z) == BlockWorld.Air)
            transform.position = new Vector3(next.x, BlockWorld.SeaLevel + 0.3f + Mathf.Sin(Time.time * 1.5f + seed) * 0.06f, next.z);
        else driftTimer = 0f;

        // Pulsing, and the tentacles swaying
        squish = Mathf.MoveTowards(squish, 0f, Time.deltaTime * 2f);
        float pulse = Mathf.Sin(Time.time * 2.5f + seed) * 0.06f + Mathf.Sin(squish * 20f) * squish * 0.35f;
        bell.localScale = new Vector3(1f + pulse, 1f - pulse * 1.5f, 1f + pulse);
        for (int i = 0; i < tentacles.Length; i++)
            tentacles[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.8f + i + seed) * 18f, 0f, Mathf.Cos(Time.time * 1.4f + i) * 18f);

        // Someone landed on top? BOING!
        FirstPersonPlayer player = BlockInteractor.Me != null ? BlockInteractor.Me.GetComponent<FirstPersonPlayer>() : null;
        if (player == null) return;
        Vector3 feet = player.transform.position;
        Vector3 top = transform.position + Vector3.up * 0.5f;
        bool onTop = new Vector2(feet.x - top.x, feet.z - top.z).magnitude < 0.75f && feet.y > top.y - 0.35f && feet.y < top.y + 0.4f;
        if (onTop && squish < 0.5f)
        {
            squish = 1f;
            player.LaunchUp(BounceSpeed);
            Sounds.Boing(top);
            Achievements.Unlock("jelly");
        }
    }
}

// Keeps a few jellyfish bobbing about in the sea near you
public class JellySwarm : MonoBehaviour
{
    private BlockWorld world;
    private float timer;

    public static void Begin(BlockWorld world)
    {
        new GameObject("Jellyfish").AddComponent<JellySwarm>().world = world;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f || BlockInteractor.Me == null) return;
        timer = 3f;

        Vector3 me = BlockInteractor.Me.transform.position;
        foreach (Jellyfish jelly in Jellyfish.All.ToArray())
            if (Vector3.Distance(jelly.transform.position, me) > 55f) Destroy(jelly.gameObject);
        if (Jellyfish.All.Count >= 8) return;

        for (int tries = 0; tries < 10; tries++)
        {
            Vector3Int spot = Vector3Int.RoundToInt(me + new Vector3(Random.Range(-35f, 35f), 0f, Random.Range(-35f, 35f)));
            if (world.GetBlock(spot.x, BlockWorld.SeaLevel, spot.z) != BlockWorld.Water || world.GetBlock(spot.x, BlockWorld.SeaLevel - 1, spot.z) != BlockWorld.Water ||
                world.GetBlock(spot.x, BlockWorld.SeaLevel + 1, spot.z) != BlockWorld.Air) continue;
            Jellyfish.Make(world, new Vector3(spot.x, BlockWorld.SeaLevel + 0.3f, spot.z), transform);
            return;
        }
    }
}
