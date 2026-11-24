using UnityEngine;

// Mods: switches that change how the game works, for fun. Press O (or click
// "Mods" on the pause screen) to open the Mods screen, and click a switch to
// turn it on or off. Mix and match! They stay on until you turn them off,
// in every world.
public static class Mods
{
    // id, name, what it does
    static readonly string[,] All =
    {
        { "jump",     "Super Jump",        "Jump really, really high" },
        { "speed",    "Super Speed",       "Run nearly twice as fast" },
        { "fly",      "Flying",            "Hold Space to fly up, let go to glide down - no wings needed" },
        { "moon",     "Moon Gravity",      "Everything's floaty - you fall slowly, like on the moon" },
        { "bouncy",   "Bouncy Ground",     "Boing boing boing! The ground is a trampoline" },
        { "day",      "Always Daytime",    "The sun never sets" },
        { "rainbow",  "Rainbow Sky",       "The sky slowly changes through all the colours" },
        { "peaceful", "No Monsters",       "No bad creatures at all (survival)" },
        { "safe",     "Can't Get Hurt",    "Nothing hurts you, and you never get hungry (survival)" },
        { "mining",   "Instant Mining",    "Blocks break straight away (survival)" },
        { "drops",    "Double Drops",      "Broken blocks give you two of them (survival)" },
        { "boom",     "Explosive Arrows",  "Arrows go BOOM when they hit something" },
        { "giant",    "Giant Creatures",   "Every animal and monster is twice as big" },
        { "transform", "Transform",        "Press T to turn into a creature - each one has a power!" },
        { "strength", "Super Strength",    "Punches hit five times as hard, swords three times" },
        { "magnet",   "Item Magnet",       "Dropped things fly to you from far away" },
        { "fireproof", "Fireproof",        "Fire and lava can't hurt you" },
        { "icewalk",  "Ice Walker",        "Water freezes under your feet - walk on the sea!" },
        { "nightvision", "Night Vision",   "See clearly in the dark, at night and in caves" },
        { "endless",  "Never Run Out",     "Use things as much as you like - they never run out (survival)" },
        { "crops",    "Fast Crops",        "Wheat grows in a few seconds" },
        { "party",    "Party Mode",        "Fireworks go off all around you!" },
        { "chickens", "Chicken Rain",      "It's raining chickens! Bwaaak!" },
        { "bigboom",  "Mega Explosions",   "TNT blows up twice as big" },
        { "fastday",  "Speedy Days",       "Days and nights fly by, eight times as fast" },
        { "trail",    "Rainbow Trail",     "Leave a trail of sparkles wherever you go" },
        { "tiny",     "Tiny Creatures",    "Every animal and monster is half the size" },
        { "slowmo",   "Slow Motion",       "Everything happens in slow motion..." },
        { "madness",  "Monster Madness",   "Three times as many monsters - if you dare! (survival)" },
        { "powers",   "My Powers",         "Make your own super powers in the Power Maker - then press R, F or X" },
    };

    public static int Count { get { return All.GetLength(0); } }
    public static string Name(int i) { return All[i, 1]; }
    public static string About(int i) { return All[i, 2]; }

    // On or off (remembered between games)
    static bool On(string id) { return PlayerPrefs.GetInt("mod-" + id, 0) == 1; }
    public static bool IsOn(int i) { return On(All[i, 0]); }
    public static void Flip(int i) { PlayerPrefs.SetInt("mod-" + All[i, 0], IsOn(i) ? 0 : 1); PlayerPrefs.Save(); }
    public static void AllOff() { for (int i = 0; i < Count; i++) PlayerPrefs.SetInt("mod-" + All[i, 0], 0); PlayerPrefs.Save(); }

    public static int HowManyOn
    {
        get { int on = 0; for (int i = 0; i < Count; i++) if (IsOn(i)) on++; return on; }
    }

    public static bool SuperJump      { get { return On("jump"); } }
    public static bool SuperSpeed     { get { return On("speed"); } }
    public static bool Flying         { get { return On("fly"); } }
    public static bool MoonGravity    { get { return On("moon"); } }
    public static bool BouncyGround   { get { return On("bouncy"); } }
    public static bool AlwaysDay      { get { return On("day"); } }
    public static bool RainbowSky     { get { return On("rainbow"); } }
    public static bool NoMonsters     { get { return On("peaceful"); } }
    public static bool CantGetHurt    { get { return On("safe"); } }
    public static bool InstantMining  { get { return On("mining"); } }
    public static bool DoubleDrops    { get { return On("drops"); } }
    public static bool ExplosiveArrows { get { return On("boom"); } }
    public static bool GiantCreatures { get { return On("giant"); } }
    public static bool Transform      { get { return On("transform"); } }
    public static bool SuperStrength  { get { return On("strength"); } }
    public static bool ItemMagnet     { get { return On("magnet"); } }
    public static bool Fireproof      { get { return On("fireproof"); } }
    public static bool IceWalker      { get { return On("icewalk"); } }
    public static bool NightVision    { get { return On("nightvision"); } }
    public static bool NeverRunOut    { get { return On("endless"); } }
    public static bool FastCrops      { get { return On("crops"); } }
    public static bool PartyMode      { get { return On("party"); } }
    public static bool ChickenRain    { get { return On("chickens"); } }
    public static bool MegaExplosions { get { return On("bigboom"); } }
    public static bool SpeedyDays     { get { return On("fastday"); } }
    public static bool RainbowTrail   { get { return On("trail"); } }
    public static bool TinyCreatures  { get { return On("tiny"); } }
    public static bool SlowMotion     { get { return On("slowmo"); } }
    public static bool MonsterMadness { get { return On("madness"); } }
    public static bool MyPowers       { get { return On("powers"); } }

    // --- The Mods screen ---

    public static bool ScreenOpen { get; private set; }
    private static GUIStyle title, nameStyle, aboutStyle;

    public static void OpenScreen()
    {
        ScreenOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void CloseScreen()
    {
        ScreenOpen = false;
        Powers.MakerOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void Draw()
    {
        if (Powers.MakerOpen) { Powers.DrawMaker(); return; }
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            aboutStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }

        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        const float width = 1056f;
        float height = 120f + ((Count + 2) / 3) * 62f;
        float scale = Mathf.Min(1f, (Screen.width - 24f) / width, (Screen.height - 24f) / height);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f, (Screen.height - height * scale) / 2f, 0f),
                                   Quaternion.identity, Vector3.one * scale);

        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.Label(new Rect(0, 10, width, 34), "MODS", title);
        GUI.Label(new Rect(0, 44, width, 20), "Click a switch to turn it on or off. Mix them up!", new GUIStyle(aboutStyle) { alignment = TextAnchor.MiddleCenter });

        for (int i = 0; i < Count; i++)
        {
            Rect card = new Rect(16 + (i % 3) * 344, 72 + (i / 3) * 62, 336, 56);
            bool on = IsOn(i);
            GUI.color = on ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.35f, 0.35f, 0.4f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = before;
            GUI.Label(new Rect(card.x + 10, card.y + 6, 240, 22), Name(i), nameStyle);
            GUI.Label(new Rect(card.x + 10, card.y + 26, 252, 30), About(i), new GUIStyle(aboutStyle) { wordWrap = true });
            if (GUI.Button(new Rect(card.xMax - 72, card.y + 12, 62, 32), on ? "ON" : "OFF"))
            {
                Flip(i);
                Sounds.Pop(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }

        float bottom = height - 44;
        if (GUI.Button(new Rect(16, bottom, 200, 34), "Turn them all off")) AllOff();
        if (GUI.Button(new Rect(232, bottom, 240, 34), "Power Maker - make your powers!")) Powers.MakerOpen = true;
        GUI.Label(new Rect(490, bottom + 6, 260, 24), "O or Esc: back to the game", aboutStyle);
        if (GUI.Button(new Rect(width - 216, bottom, 200, 34), "Play!")) CloseScreen();
        GUI.matrix = old;
    }
}
