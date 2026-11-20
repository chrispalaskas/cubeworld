using UnityEngine;

// Day and night, in your home world.
//
// A whole day lasts 10 minutes: the sun rises, crosses the sky and sets,
// then it's night for a while - dark, with stars and a dim blue moonlight.
// Monsters love the dark, so many more come out at night (see Monster.cs).
// Lamps, fire and the Star Crystal really matter when it's dark!
//
// Sleep in a bed at night and you skip straight to the morning.
// (The Magic Land and the Nether have their own skies, so no night there.)
public class DayNight : MonoBehaviour
{
    const float DaySeconds = 600f;   // a whole day and night
    const int StarCount = 300;

    public static DayNight Instance { get; private set; }

    // 0 is sunrise, 0.25 midday, 0.5 sunset, 0.75 midnight
    private float time;
    private string saveKey = "";

    private Light sun;
    private Quaternion sunWas;
    private float sunBrightnessWas;
    private Light moon;
    private GameObject stars;
    private Material skyWas, sky;
    private float ambientWas;
    private bool wasNight;

    // Night is from a little after sunset until just before sunrise
    public static bool IsNight { get { return Instance != null && Instance.time > 0.53f && Instance.time < 0.97f; } }

    public static void Begin()
    {
        new GameObject("Day And Night").AddComponent<DayNight>();
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        saveKey = "world" + (WorldMenu.CurrentSlot + 1) + "-time";
        time = PlayerPrefs.GetFloat(saveKey, 0.05f);   // a new world starts just after sunrise

        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && light.gameObject.name != "Moon") { sun = light; break; }
        if (sun != null)
        {
            sunWas = sun.transform.rotation;
            sunBrightnessWas = sun.intensity;
            RenderSettings.sun = sun;
        }

        // A dim blue moonlight, so the night isn't completely black
        moon = new GameObject("Moon").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.65f, 1.00f);
        moon.shadows = LightShadows.None;

        // Our own copy of the sky, so we can dim it without changing the real one
        skyWas = RenderSettings.skybox;
        if (skyWas != null)
        {
            sky = new Material(skyWas);
            RenderSettings.skybox = sky;
        }
        ambientWas = RenderSettings.ambientIntensity;

        MakeStars();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayerPrefs.SetFloat(saveKey, time);

        // Put the sky back how it was, for the menu and the other places
        if (sun != null)
        {
            sun.transform.rotation = sunWas;
            sun.intensity = sunBrightnessWas;
            sun.color = Color.white;
        }
        if (skyWas != null) RenderSettings.skybox = skyWas;
        if (sky != null) Destroy(sky);
        RenderSettings.ambientIntensity = ambientWas;
        if (moon != null) Destroy(moon.gameObject);
        if (stars != null) Destroy(stars);
    }

    // Wake up at sunrise
    public void SkipToMorning()
    {
        time = 0.01f;
    }

    void Update()
    {
        bool paused = BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked;
        if (!paused) time = Mathf.Repeat(time + Time.deltaTime / DaySeconds, 1f);

        // How high the sun is: 1 at midday, 0 at the horizon, below 0 at night
        float height = Mathf.Sin(time * Mathf.PI * 2f);
        float daylight = Mathf.Clamp01(height * 3f + 0.25f) * Weather.Gloom;   // rain and storms make it darker

        // Made it through a whole night in survival?
        bool night = IsNight;
        if (wasNight && !night && PlayerHealth.Instance != null && !PlayerHealth.Instance.Dead) Achievements.Unlock("night");
        wasNight = night;

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(time * 360f, -30f, 0f);
            sun.intensity = Mathf.Lerp(0f, 1.1f, daylight);
            // Orange at sunrise and sunset
            sun.color = Color.Lerp(new Color(1f, 0.55f, 0.30f), Color.white, Mathf.Clamp01(height * 4f));
        }

        moon.transform.rotation = Quaternion.Euler(time * 360f + 180f, -30f, 0f);
        moon.intensity = (1f - daylight) * 0.35f;

        RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, ambientWas, daylight);
        if (sky != null && sky.HasProperty("_Exposure"))
            sky.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.3f, daylight));

        if (stars != null) stars.SetActive(daylight < 0.35f);
    }

    // The stars follow you about, so they always look far away
    void LateUpdate()
    {
        if (stars != null && Camera.main != null)
            stars.transform.position = Camera.main.transform.position;
    }

    void MakeStars()
    {
        stars = new GameObject("Stars");
        Shader plain = Shader.Find("Unlit/Color");
        Material twinkle = plain != null ? new Material(plain) : BlockWorld.MakeMaterial(Color.white);
        twinkle.color = new Color(0.95f, 0.95f, 1f);

        System.Random dice = new System.Random(12345);
        for (int i = 0; i < StarCount; i++)
        {
            // Somewhere on the upper half of a huge ball around you
            Vector3 direction = new Vector3((float)dice.NextDouble() * 2f - 1f, (float)dice.NextDouble() * 0.9f + 0.1f,
                                            (float)dice.NextDouble() * 2f - 1f).normalized;
            GameObject star = new GameObject("Star");
            star.transform.SetParent(stars.transform, false);
            star.transform.localPosition = direction * 400f;
            star.transform.localScale = Vector3.one * (1f + (float)dice.NextDouble() * 1.5f);
            star.transform.rotation = Quaternion.LookRotation(direction);
            star.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            MeshRenderer look = star.AddComponent<MeshRenderer>();
            look.sharedMaterial = twinkle;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
        }
        stars.SetActive(false);
    }
}
