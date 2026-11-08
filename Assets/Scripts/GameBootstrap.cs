using UnityEngine;

// Builds the whole game the moment you press Play, so there is nothing to
// set up by hand in the editor - no dragging things into slots.
public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (FindFirstObjectByType<BlockWorld>() != null) return;   // already running

        // --- The world ---
        GameObject worldObj = new GameObject("World");
        BlockWorld world = worldObj.AddComponent<BlockWorld>();

        // --- Sunlight (only if the scene hasn't got a light already) ---
        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject sunObj = new GameObject("Sun");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // --- The player ---
        GameObject player = new GameObject("Player");

        CharacterController body = player.AddComponent<CharacterController>();
        body.height = 1.8f;
        body.radius = 0.35f;
        body.center = new Vector3(0f, 0.9f, 0f);

        // Stand on dry land near the middle of the map
        player.transform.position = world.FindSpawnPoint();

        // --- The camera becomes the player's head ---
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);   // eye height
        cam.transform.localRotation = Quaternion.identity;

        player.AddComponent<FirstPersonPlayer>();
        player.AddComponent<BlockInteractor>();

        // --- The wildlife ---
        Squirrel.SpawnAll(world);
        ChickenRespawner.Begin(world);
    }
}
