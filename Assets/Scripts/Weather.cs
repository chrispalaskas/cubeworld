using UnityEngine;

// Weather, in the home world: clear skies most of the time, but every few
// minutes it might rain, or even storm - dark skies, thunder and lightning.
// High up in the snowy mountains (or in the snowy land) the rain falls as snow.
// It doesn't rain on you while you're indoors.
public class Weather : MonoBehaviour
{
    const int Drops = 260;
    const float Around = 14f;   // how far round you the rain falls

    enum Sky { Clear, Rain, Storm }

    public static Weather Instance { get; private set; }

    // How much the weather darkens the day (1 when clear)
    public static float Gloom { get { return Instance == null ? 1f : Instance.gloom; } }
    public static bool Stormy { get { return Instance != null && Instance.sky == Sky.Storm; } }

    private Sky sky = Sky.Clear;
    private float changeAt;
    private float gloom = 1f;
    private float nextLightning;
    private float flash;

    private Transform[] drops;
    private float[] dropSpeed;
    private Material rainLook, snowLook;
    private AudioSource rainSound;
    private BlockWorld world;

    public static void Begin(BlockWorld world)
    {
        new GameObject("Weather").AddComponent<Weather>().world = world;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        changeAt = Time.time + Random.Range(60f, 180f);
        rainLook = BlockWorld.MakeMaterial(new Color(0.55f, 0.65f, 0.95f));
        snowLook = BlockWorld.MakeMaterial(Color.white);

        drops = new Transform[Drops];
        dropSpeed = new float[Drops];
        for (int i = 0; i < Drops; i++)
        {
            GameObject drop = new GameObject("Drop");
            drop.transform.SetParent(transform, false);
            drop.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            MeshRenderer look = drop.AddComponent<MeshRenderer>();
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            drops[i] = drop.transform;
            drop.SetActive(false);
        }

        rainSound = gameObject.AddComponent<AudioSource>();
        rainSound.clip = Sounds.RainClip();
        rainSound.loop = true;
        rainSound.volume = 0f;
        rainSound.Play();
    }

    void Update()
    {
        bool paused = BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked;

        // Time for a change?
        if (!paused && Time.time > changeAt)
        {
            float roll = Random.value;
            sky = sky != Sky.Clear ? Sky.Clear : roll < 0.6f ? Sky.Rain : Sky.Storm;
            changeAt = Time.time + (sky == Sky.Clear ? Random.Range(180f, 360f) : Random.Range(60f, 150f));
            if (sky == Sky.Rain) PortalTraveller.Announce("It's starting to rain...");
            if (sky == Sky.Storm) PortalTraveller.Announce("A thunderstorm is coming!");
            nextLightning = Time.time + 5f;
        }

        float wantGloom = sky == Sky.Clear ? 1f : sky == Sky.Rain ? 0.7f : 0.45f;
        gloom = Mathf.MoveTowards(gloom, wantGloom, Time.deltaTime * 0.1f);

        // Lightning and thunder in a storm
        if (sky == Sky.Storm && Time.time > nextLightning && !paused)
        {
            nextLightning = Time.time + Random.Range(6f, 15f);
            flash = 1f;
            if (Camera.main != null) Sounds.Thunder(Camera.main.transform.position);
        }
        flash = Mathf.Max(0f, flash - Time.deltaTime * 3f);

        MoveDrops(paused);
    }

    void MoveDrops(bool paused)
    {
        Camera cam = Camera.main;
        bool raining = sky != Sky.Clear && cam != null;
        rainSound.volume = Mathf.MoveTowards(rainSound.volume, raining ? (sky == Sky.Storm ? 0.5f : 0.3f) : 0f, Time.deltaTime * 0.3f);
        if (cam == null) return;

        Vector3 me = cam.transform.position;
        bool snowing = me.y > BlockWorld.SeaLevel + 17 || UnderSnow(me);

        for (int i = 0; i < Drops; i++)
        {
            Transform drop = drops[i];
            if (!raining) { drop.gameObject.SetActive(false); continue; }

            if (!drop.gameObject.activeSelf || drop.position.y < me.y - 8f || Mathf.Abs(drop.position.x - me.x) > Around ||
                Mathf.Abs(drop.position.z - me.z) > Around || Hit(drop.position))
            {
                // Start again somewhere above you (but not under a roof)
                Vector3 spot = me + new Vector3(Random.Range(-Around, Around), Random.Range(4f, 12f), Random.Range(-Around, Around));
                drop.position = spot;
                drop.gameObject.SetActive(!Sheltered(spot));
                drop.localScale = snowing ? Vector3.one * 0.08f : new Vector3(0.025f, 0.35f, 0.025f);
                drop.GetComponent<MeshRenderer>().sharedMaterial = snowing ? snowLook : rainLook;
                dropSpeed[i] = snowing ? Random.Range(1.5f, 2.5f) : Random.Range(14f, 18f);
            }
            if (!paused)
                drop.position += new Vector3(snowing ? Mathf.Sin(Time.time + i) * 0.5f * Time.deltaTime : 0f, -dropSpeed[i] * Time.deltaTime, 0f);
        }
    }

    // Is there a roof (or anything) above this spot?
    bool Sheltered(Vector3 spot)
    {
        int x = Mathf.RoundToInt(spot.x), z = Mathf.RoundToInt(spot.z);
        return world.GetSurfaceHeight(x, z) > spot.y;
    }

    bool Hit(Vector3 spot)
    {
        Vector3Int cell = Vector3Int.RoundToInt(spot);
        byte type = world.GetBlock(cell.x, cell.y, cell.z);
        return type != BlockWorld.Air;
    }

    bool UnderSnow(Vector3 me)
    {
        int x = Mathf.RoundToInt(me.x), z = Mathf.RoundToInt(me.z);
        int top = world.GetSurfaceHeight(x, z);
        return top >= 0 && (world.GetBlock(x, top, z) == BlockWorld.Snow || world.GetBlock(x, top, z) == BlockWorld.Ice);
    }

    // The white flash of lightning
    void OnGUI()
    {
        if (flash <= 0f) return;
        Color before = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, flash * 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;
    }
}
