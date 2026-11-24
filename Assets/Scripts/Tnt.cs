using System.Collections;
using UnityEngine;

// A lit block of TNT. It flashes while the fuse burns, then destroys every
// block, squirrel and chicken within BlastRadius. Break the block before the fuse runs
// out and the bomb is defused. Mega TNT (made at a crafting table) works the
// same way, with a longer fuse and a much bigger bang.
public class Tnt : MonoBehaviour
{
    public const float FuseSeconds = 3f;
    public const int BlastRadius = 5;

    public const float MegaFuseSeconds = 5f;
    public const int MegaBlastRadius = 8;

    // TNT caught in a blast isn't destroyed - it goes off a moment later
    const float ChainReactionSeconds = 0.4f;

    private BlockWorld world;
    private Vector3Int cell;
    private float timeLeft;
    private byte type;     // Tnt or MegaTnt
    private int radius;

    public static bool IsExplosive(byte type)
    {
        return type == BlockWorld.Tnt || type == BlockWorld.MegaTnt;
    }

    public static void Ignite(BlockWorld world, Vector3Int cell, float seconds)
    {
        // Already burning? Then all we can do is hurry it up
        Tnt alreadyLit;
        if (world.LitTnt.TryGetValue(cell, out alreadyLit))
        {
            alreadyLit.timeLeft = Mathf.Min(alreadyLit.timeLeft, seconds);
            return;
        }

        GameObject obj = new GameObject("Lit TNT");
        obj.transform.position = cell;

        Tnt tnt = obj.AddComponent<Tnt>();
        tnt.world = world;
        tnt.cell = cell;
        tnt.timeLeft = seconds;
        tnt.type = world.GetBlock(cell.x, cell.y, cell.z);
        tnt.radius = (tnt.type == BlockWorld.MegaTnt ? MegaBlastRadius : BlastRadius) * (Mods.MegaExplosions ? 2 : 1);
        world.LitTnt[cell] = tnt;
    }

    void Start()
    {
        StartCoroutine(Burn());
    }

    IEnumerator Burn()
    {
        // A white cube just bigger than the block, switched on and off, makes it flash
        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(transform, false);
        flash.transform.localScale = Vector3.one * 1.02f;
        flash.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        MeshRenderer flashLook = flash.AddComponent<MeshRenderer>();
        flashLook.sharedMaterial = world.TntFlashMaterial;

        while (timeLeft > 0f)
        {
            if (world.GetBlock(cell.x, cell.y, cell.z) != type)
            {
                world.LitTnt.Remove(cell);   // someone broke it in time
                Destroy(gameObject);
                yield break;
            }

            // Flash faster as the end gets close
            float period = timeLeft < 1f ? 0.15f : 0.4f;
            flashLook.enabled = Mathf.Repeat(timeLeft, period) < period / 2f;

            timeLeft -= Time.deltaTime;
            yield return null;
        }

        flashLook.enabled = false;
        Explode();
        yield return Fireball();
        Destroy(gameObject);
    }

    void Explode()
    {
        world.LitTnt.Remove(cell);
        Sounds.Boom(cell);
        world.SetBlock(cell.x, cell.y, cell.z, BlockWorld.Air);

        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            if (dx * dx + dy * dy + dz * dz > radius * radius) continue;   // makes the crater round

            int x = cell.x + dx;
            int y = cell.y + dy;
            int z = cell.z + dz;
            byte there = world.GetBlock(x, y, z);

            if (IsExplosive(there))
                Ignite(world, new Vector3Int(x, y, z), ChainReactionSeconds);
            else if (there != BlockWorld.Air)
                world.SetBlock(x, y, z, BlockWorld.Air);
        }

        foreach (Squirrel squirrel in FindObjectsByType<Squirrel>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(squirrel.transform.position, cell) <= radius)
                Destroy(squirrel.gameObject);
        }

        foreach (Chicken chicken in FindObjectsByType<Chicken>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(chicken.transform.position, cell) <= radius)
                chicken.BlowUp();
        }

        // In survival, being too close to a blast hurts - a lot, right next to it
        PlayerHealth health = PlayerHealth.Instance;
        if (health != null)
        {
            float away = Vector3.Distance(health.transform.position + Vector3.up * 0.9f, cell);
            float reach = radius + 2f;
            if (away < reach)
                health.Hurt(Mathf.CeilToInt((1f - away / reach) * (radius >= MegaBlastRadius ? 30f : 18f)), "You were too close to the TNT.");
        }

        foreach (Monster monster in Monster.All.ToArray())
            if (Vector3.Distance(monster.transform.position, cell) <= radius)
                monster.Hit(100, cell);

        // People don't get hurt - they get sent flying
        foreach (Villager person in FindObjectsByType<Villager>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(person.transform.position, cell) <= radius)
                person.Blast(cell, radius);
        }
    }

    // A ball of fire that quickly swells to the size of the blast
    IEnumerator Fireball()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(ball.GetComponent<Collider>());   // nothing should bump into it
        ball.transform.SetParent(transform, false);
        ball.GetComponent<Renderer>().sharedMaterial = world.ExplosionMaterial;

        Light glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.6f, 0.2f);
        glow.range = radius * 4f;

        const float duration = 0.3f;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            ball.transform.localScale = Vector3.one * radius * 2f * t;
            glow.intensity = 6f * (1f - t);
            yield return null;
        }
    }
}
