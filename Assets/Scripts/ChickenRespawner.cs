using System.Collections;
using UnityEngine;

// Keeps the world stocked with chickens. It fills up the flock at the start,
// then brings back one lost chicken every so often.
public class ChickenRespawner : MonoBehaviour
{
    // A bigger world has room for more chickens: 12 in a small world, up to 30
    static int FlockSize { get { return Mathf.Clamp(BlockWorld.SizeX * BlockWorld.SizeZ / 340, 12, 30); } }
    const float SecondsBetweenChickens = 10f;

    private static ChickenRespawner instance;
    private BlockWorld world;
    private Material white, red, orange, black;

    // A chicken egg was placed - a chicken pops out right there
    public static void Hatch(Vector3 spot)
    {
        if (instance == null) return;
        Chicken.SpawnAt(instance.world, spot, instance.white, instance.red, instance.orange, instance.black);
    }

    public static void Begin(BlockWorld world)
    {
        ChickenRespawner respawner = new GameObject("Chicken Respawner").AddComponent<ChickenRespawner>();
        respawner.world = world;
        instance = respawner;

        // One material per colour, shared by every chicken
        respawner.white = BlockWorld.MakeMaterial(new Color(0.95f, 0.95f, 0.90f));
        respawner.red = BlockWorld.MakeMaterial(new Color(0.85f, 0.10f, 0.10f));
        respawner.orange = BlockWorld.MakeMaterial(new Color(0.95f, 0.60f, 0.10f));
        respawner.black = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));

        for (int i = 0; i < FlockSize; i++)
            respawner.SpawnOne();
    }

    void Start()
    {
        StartCoroutine(KeepFlockFull());
    }

    IEnumerator KeepFlockFull()
    {
        while (true)
        {
            yield return new WaitForSeconds(SecondsBetweenChickens);

            if (FindObjectsByType<Chicken>(FindObjectsSortMode.None).Length < FlockSize)
                SpawnOne();
        }
    }

    void SpawnOne()
    {
        Chicken.Spawn(world, white, red, orange, black);
    }
}
