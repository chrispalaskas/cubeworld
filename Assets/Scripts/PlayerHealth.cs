using UnityEngine;

// Hearts, food and breath - only in survival worlds.
//
//   Hearts:  10 of them. Falling too far, lava, fire, TNT, drowning and
//            zombies all knock some off. Lose them all and you're knocked
//            out - press Enter to wake up back at the start (you keep your
//            things).
//   Food:    10 drumsticks, slowly going down. Keep them nearly full and
//            your hearts come back by themselves. Run out and you start
//            losing hearts (but hunger alone never knocks you out).
//   Breath:  bubbles appear when your head is under water. When they run
//            out, you start to drown - swim up for air!
//
// Health and food are worked in halves, like Minecraft: 20 means full.
// Both are remembered for each world.
public class PlayerHealth : MonoBehaviour
{
    public const int MostHealth = 20;
    public const int MostFood = 20;

    const float HungrySeconds = 40f;     // one half-drumstick goes this often
    const float HealSeconds = 4f;        // with a full-ish tummy, half a heart comes back this often
    const float StarveSeconds = 4f;      // with an empty tummy, half a heart goes this often
    const float BreathSeconds = 10f;     // how long you can stay under water
    const float SafeFall = 3.5f;         // blocks you can fall without getting hurt

    public static PlayerHealth Instance { get; private set; }

    private int health = MostHealth;
    private int food = MostFood;
    private float breath = BreathSeconds;
    private float hungerTimer = HungrySeconds;
    private float healTimer = HealSeconds;
    private float drownTimer;
    private float starTimer;
    private float hurtFlash = -100f;
    private string knockedOutBy = "";
    private string saveKey = "";

    private static Texture2D heart, emptyHeart, drumstick, emptyDrumstick, bubble, shield;
    private GUIStyle bigStyle;

    public bool Dead { get; private set; }
    public bool Sprinting { get; set; }

    // 0 none, 1 leather, 2 iron, 3 diamond - each takes more off every hit
    static readonly float[] Protection = { 0f, 0.25f, 0.45f, 0.65f };
    private int armour;
    public int Armour
    {
        get { return armour; }
        set { armour = Mathf.Clamp(value, 0, 3); PlayerPrefs.SetInt(saveKey + "armour", armour); }
    }   // running makes you hungry three times as fast
    public int Food { get { return food; } }
    public bool Full { get { return food >= MostFood; } }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        saveKey = "world" + (WorldMenu.CurrentSlot + 1) + "-";
        health = Mathf.Clamp(PlayerPrefs.GetInt(saveKey + "health", MostHealth), 1, MostHealth);
        food = Mathf.Clamp(PlayerPrefs.GetInt(saveKey + "food", MostFood), 0, MostFood);
        armour = Mathf.Clamp(PlayerPrefs.GetInt(saveKey + "armour", 0), 0, 3);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Remember();
    }

    void Remember()
    {
        PlayerPrefs.SetInt(saveKey + "health", Dead ? MostHealth : health);
        PlayerPrefs.SetInt(saveKey + "food", Dead ? MostFood : food);
    }

    void Update()
    {
        if (Dead)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                WakeUp();
            return;
        }
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;   // paused

        // Getting hungry...
        hungerTimer -= Time.deltaTime * (Sprinting ? 3f : 1f);
        if (hungerTimer <= 0f)
        {
            hungerTimer = HungrySeconds;
            if (food > 0) food--;
            Remember();
        }

        // ...a full tummy heals you, an empty one hurts
        healTimer -= Time.deltaTime;
        if (healTimer <= 0f)
        {
            healTimer = food >= 16 ? HealSeconds : StarveSeconds;
            if (food >= 16 && health < MostHealth)
            {
                health++;
                if (food > 0 && Random.value < 0.5f) food--;   // healing makes you hungry
            }
            else if (food == 0 && health > 1)
            {
                Hurt(1, "");
                PortalTraveller.Announce("You're starving! Eat something - apples come from leaves.");
            }
        }

        // The Star Crystal's starlight heals you
        Vector3 star;
        starTimer -= Time.deltaTime;
        if (starTimer <= 0f)
        {
            starTimer = 1f;
            if (health < MostHealth && StarShrine.Near(transform.position, StarShrine.HealRange, out star))
                Heal(1);
        }

        // Holding your breath under water
        if (HeadUnderWater())
        {
            breath -= Time.deltaTime;
            if (breath <= 0f)
            {
                breath = 0f;
                drownTimer -= Time.deltaTime;
                if (drownTimer <= 0f)
                {
                    drownTimer = 1f;
                    Hurt(2, "You ran out of air under water.");
                }
            }
        }
        else
        {
            breath = Mathf.Min(BreathSeconds, breath + Time.deltaTime * 4f);
            drownTimer = 0f;
        }
    }

    bool HeadUnderWater()
    {
        if (BlockWorld.Instance == null || Camera.main == null) return false;
        Vector3Int head = Vector3Int.RoundToInt(Camera.main.transform.position);
        return BlockWorld.Instance.GetBlock(head.x, head.y, head.z) == BlockWorld.Water;
    }

    // --- Getting hurt and getting better ---

    // Takes off some health (in halves of a heart). "how" is what's said if it knocks you out.
    public void Hurt(int amount, string how)
    {
        if (Dead || amount <= 0) return;
        if (how != "" && armour > 0) amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Protection[armour])));   // armour helps

        health -= amount;
        hurtFlash = Time.time;
        Sounds.Ouch(transform.position);
        if (health > 0)
        {
            Remember();
            return;
        }

        health = 0;
        Dead = true;
        knockedOutBy = how;
        Remember();

        FirstPersonPlayer me = GetComponent<FirstPersonPlayer>();
        if (me != null) me.Dismount();
    }

    // Landed on the ground after falling at this speed
    public void Landed(float speed, float gravity)
    {
        float fell = speed * speed / (2f * gravity);   // how many blocks that was
        int damage = Mathf.FloorToInt(fell - SafeFall);
        if (damage > 0) Hurt(damage, "You fell from too high up.");
    }

    public void Heal(int amount)
    {
        health = Mathf.Min(MostHealth, health + amount);
        Remember();
    }

    public void Eat(int amount)
    {
        food = Mathf.Min(MostFood, food + amount);
        Remember();
    }

    void WakeUp()
    {
        Dead = false;
        health = MostHealth;
        food = MostFood;
        breath = BreathSeconds;
        Remember();

        FirstPersonPlayer me = GetComponent<FirstPersonPlayer>();
        if (me != null) me.Respawn();
    }

    // --- Drawing the hearts, food and bubbles ---

    void OnGUI()
    {
        GUI.depth = -5;
        MakePictures();

        // Same size and place as the hotbar (see BlockInteractor.DrawHotbar)
        const float gap = 4f;
        float slot = Mathf.Min(52f, (Screen.width - 32f) / 10 - gap);
        float total = 10 * (slot + gap) - gap;
        float left = (Screen.width - total) / 2f;
        float top = Screen.height - slot - 12f - 22f;
        const float icon = 18f;

        // Hearts on the left, food on the right (filling from the right, like Minecraft)
        for (int i = 0; i < 10; i++)
        {
            Rect h = new Rect(left + i * (icon + 2f), top, icon, icon);
            DrawHalves(h, health - i * 2, heart, emptyHeart, false);

            Rect f = new Rect(left + total - (i + 1) * (icon + 2f) + 2f, top, icon, icon);
            DrawHalves(f, food - i * 2, drumstick, emptyDrumstick, true);
        }

        // Shields above the hearts, for armour
        for (int i = 0; i < armour * 3; i++)
            GUI.DrawTexture(new Rect(left + i * (icon + 2f), top - icon - 2f, icon, icon), shield);

        // Bubbles, above the food, while you're under water
        if (breath < BreathSeconds)
        {
            int bubbles = Mathf.CeilToInt(breath / BreathSeconds * 10f);
            for (int i = 0; i < bubbles; i++)
                GUI.DrawTexture(new Rect(left + total - (i + 1) * (icon + 2f) + 2f, top - icon - 2f, icon, icon), bubble);
        }

        Color before = GUI.color;

        // A red flash when you get hurt
        float since = Time.time - hurtFlash;
        if (since < 0.4f)
        {
            GUI.color = new Color(0.9f, 0f, 0f, 0.35f * (1f - since / 0.4f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        if (Dead)
        {
            if (bigStyle == null)
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUIStyle middle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };

            GUI.color = new Color(0.45f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), "You were knocked out!", bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.3f + 64, Screen.width, 26), knockedOutBy, middle);
            GUI.Label(new Rect(0, Screen.height * 0.3f + 96, Screen.width, 26),
                      "Press Enter to wake up back at the start. Don't worry - you keep all your things.", middle);
        }

        GUI.color = before;
    }

    // A full, half or empty heart (or drumstick), depending on how many halves are left
    static void DrawHalves(Rect where, int halves, Texture2D full, Texture2D empty, bool fromRight)
    {
        GUI.DrawTexture(where, empty);
        if (halves >= 2)
        {
            GUI.DrawTexture(where, full);
        }
        else if (halves == 1)
        {
            // Just the left half (or right half, for food)
            Rect half = new Rect(where.x + (fromRight ? where.width / 2f : 0f), where.y, where.width / 2f, where.height);
            GUI.DrawTextureWithTexCoords(half, full, new Rect(fromRight ? 0.5f : 0f, 0f, 0.5f, 1f));
        }
    }

    static void MakePictures()
    {
        if (heart != null) return;

        string[] heartShape =
        {
            ".xx..xx.",
            "xxxxxxxx",
            "xwxxxxxx",
            "xxxxxxxx",
            ".xxxxxx.",
            "..xxxx..",
            "...xx...",
            "........",
        };
        heart = BlockInteractor.PixelArt(heartShape, new Color(0.90f, 0.10f, 0.12f));
        emptyHeart = BlockInteractor.PixelArt(heartShape, new Color(0.18f, 0.08f, 0.08f, 0.85f));

        string[] drumstickShape =
        {
            "...xxx..",
            "..xxxxx.",
            "..xxxxx.",
            "..xxxx..",
            "...xx...",
            "..w.....",
            ".w.w....",
            "........",
        };
        drumstick = BlockInteractor.PixelArt(drumstickShape, new Color(0.75f, 0.45f, 0.20f));
        emptyDrumstick = BlockInteractor.PixelArt(drumstickShape, new Color(0.15f, 0.10f, 0.06f, 0.85f));

        shield = BlockInteractor.PixelArt(new[]
        {
            "xxxxxxxx",
            "xwxxxxxx",
            "xxxxxxxx",
            "xxxxxxxx",
            ".xxxxxx.",
            ".xxxxxx.",
            "..xxxx..",
            "...xx...",
        }, new Color(0.75f, 0.78f, 0.85f));

        bubble = BlockInteractor.PixelArt(new[]
        {
            "..xxxx..",
            ".x....x.",
            "x..w...x",
            "x.w....x",
            "x......x",
            "x......x",
            ".x....x.",
            "..xxxx..",
        }, new Color(0.55f, 0.80f, 1.00f));
    }
}
