using UnityEngine;

// Builds the whole game the moment you press Play, so there is nothing to
// set up by hand in the editor - no dragging things into slots. It starts on
// the world menu, which calls StartGame and EndGame as you come and go.
public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (FindFirstObjectByType<WorldMenu>() != null) return;   // already running

        // --- Sunlight (only if the scene hasn't got a light already) ---
        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject sunObj = new GameObject("Sun");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // --- A camera, which looks at the sky behind the menu ---
        if (Camera.main == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        new GameObject("World Menu").AddComponent<WorldMenu>();
    }

    // Puts a player and animals into a world that is already built
    public static FirstPersonPlayer StartGame(BlockWorld world, Vector3 position, float yaw, float pitch)
    {
        // --- The player ---
        GameObject player = new GameObject("Player");

        CharacterController body = player.AddComponent<CharacterController>();
        body.height = 1.8f;
        body.radius = 0.35f;
        body.stepOffset = 0.55f;   // walk up half-block steps like stairs without jumping
        body.center = new Vector3(0f, 0.9f, 0f);

        player.transform.position = position;
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // --- The camera becomes the player's head ---
        Camera cam = Camera.main;
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);   // eye height
        cam.transform.localRotation = Quaternion.identity;

        FirstPersonPlayer walker = player.AddComponent<FirstPersonPlayer>();
        walker.Pitch = pitch;
        player.AddComponent<BlockInteractor>();
        player.AddComponent<PortalTraveller>();
        if (WorldMenu.Survival) player.AddComponent<PlayerHealth>();
        player.AddComponent<Minimap>();

        // --- The wildlife ---
        Squirrel.SpawnAll(world);
        ChickenRespawner.Begin(world);
        Villager.SpawnAll(world);
        Villager.SpawnMagicFolk(world);
        Villager.SpawnFireFolk(world);
        Unicorn.SpawnAll(world);
        Dragon.SpawnAll(world);
        if (WorldMenu.CurrentPlace == WorldSaves.Nether) NetherFire.Begin();
        if (WorldMenu.CurrentPlace == WorldSaves.Home)
        {
            DayNight.Begin();
            Weather.Begin(world);
            AnimalHerd.Begin(world, position);
            StructureWatcher.Begin(world);
        }
        Vehicle.LoadAll(world);
        if (WorldMenu.Survival && WorldMenu.CurrentPlace != WorldSaves.MagicLand) Monster.BeginSpawning(world);

        return walker;
    }

    // Clears away everything from the world we're leaving
    public static void EndGame()
    {
        // Rescue the camera first, or it would be destroyed along with the player
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetParent(null);
            cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        DestroyAll<Tnt>();
        DestroyAll<Squirrel>();
        DestroyAll<Chicken>();
        DestroyAll<ChickenRespawner>();
        DestroyAll<Villager>();
        DestroyAll<Unicorn>();
        DestroyAll<Dragon>();
        DestroyAll<NetherFire>();
        DestroyAll<Monster>();
        DestroyAll<MonsterSpawner>();
        DestroyAll<MonsterShot>();
        DestroyAll<ItemDrop>();
        DestroyAll<DayNight>();
        DestroyAll<Weather>();
        DestroyAll<AnimalHerd>();
        DestroyAll<FishSchool>();
        DestroyAll<StructureWatcher>();
        DestroyAll<Vehicle>();
        DestroyAll<FirstPersonPlayer>();
        DestroyAll<BlockWorld>();
    }

    static void DestroyAll<T>() where T : Component
    {
        foreach (T thing in FindObjectsByType<T>(FindObjectsSortMode.None))
            Destroy(thing.gameObject);
    }
}
