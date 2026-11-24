using UnityEngine;

// The mods that make things happen all by themselves, every so often:
// Party Mode's fireworks, Chicken Rain, the Rainbow Trail and Slow Motion.
// (The other mods are switched on where they make a difference - see Mods.cs.)
public class ModEffects : MonoBehaviour
{
    private float nextFirework, nextChicken, nextSparkle;
    private Vector3 lastSparkleAt;

    public static void Begin()
    {
        new GameObject("Mod Effects").AddComponent<ModEffects>();
    }

    void Update()
    {
        // Slow motion slows everything down - and back to normal when it's off
        bool paused = BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked;
        Time.timeScale = Mods.SlowMotion && !paused ? 0.4f : 1f;

        if (paused || BlockInteractor.Me == null || BlockWorld.Instance == null) return;
        Vector3 me = BlockInteractor.Me.transform.position;

        // Party Mode: fireworks all around!
        if (Mods.PartyMode && Time.time > nextFirework)
        {
            nextFirework = Time.time + Random.Range(1.2f, 2.8f);
            Vector3 spot = me + new Vector3(Random.Range(-14f, 14f), 0f, Random.Range(-14f, 14f));
            int top = BlockWorld.Instance.GetSurfaceHeight(Mathf.RoundToInt(spot.x), Mathf.RoundToInt(spot.z));
            if (top > 0) Firework.Launch(new Vector3(Mathf.Round(spot.x), top + 1, Mathf.Round(spot.z)));
        }

        // Chicken Rain: bwaaak! (but not so many that the game gets slow)
        if (Mods.ChickenRain && Time.time > nextChicken)
        {
            nextChicken = Time.time + Random.Range(1.5f, 3f);
            if (FindObjectsByType<Chicken>(FindObjectsSortMode.None).Length < 40)
                ChickenRespawner.Hatch(me + new Vector3(Random.Range(-10f, 10f), 14f, Random.Range(-10f, 10f)));
        }

        // Rainbow Trail: sparkles wherever you've been
        if (Mods.RainbowTrail && Time.time > nextSparkle && Vector3.Distance(me, lastSparkleAt) > 0.4f)
        {
            nextSparkle = Time.time + 0.08f;
            lastSparkleAt = me;
            Villager.SparkleBurst(me + Vector3.up * 0.2f, 2);
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
