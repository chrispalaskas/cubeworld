using System;
using UnityEngine;

// The "How to Play" tutorial - a little show you can sit back and watch.
//
// Each page has a moving picture showing what to do, and a few words about
// it. It turns the pages by itself; press Space to pause, the arrow keys
// (or the buttons) to go back and forward, and Esc or H to close it.
// Open it from the world menu, or press H any time while you're playing.
public class Tutorial : MonoBehaviour
{
    const float PageSeconds = 9f;

    public static bool IsOpen { get; private set; }

    private static Tutorial instance;
    private int page;
    private float pageStarted;
    private bool paused;
    private float pausedAt;
    private bool wasLocked;
    private int openedFrame;   // the key that opened it shouldn't close it straight away
    private GUIStyle titleStyle, textStyle, keyStyle, smallStyle;
    private Texture2D heart, drumstick, star;

    struct Page
    {
        public string title;
        public string[] words;
        public Action<Rect, float> picture;
    }
    private Page[] pages;

    public static void Open()
    {
        if (instance == null)
        {
            GameObject holder = new GameObject("Tutorial");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<Tutorial>();
        }
        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.page = 0;
        instance.pageStarted = Time.time;
        instance.paused = false;
        instance.wasLocked = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        IsOpen = false;
        if (wasLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    float PageTime { get { return (paused ? pausedAt : Time.time) - pageStarted; } }

    void Turn(int by)
    {
        page = Mathf.Clamp(page + by, 0, pages.Length - 1);
        pageStarted = Time.time;
        pausedAt = Time.time;
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H)) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) Turn(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Turn(-1);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (paused) pageStarted += Time.time - pausedAt;
            else pausedAt = Time.time;
            paused = !paused;
        }
        if (!paused && PageTime > PageSeconds && page < pages.Length - 1) Turn(1);
    }

    // --- Drawing ---

    void OnGUI()
    {
        if (!IsOpen) return;
        GUI.depth = -30;
        if (pages == null) MakePages();

        Color before = GUI.color;
        GUI.color = new Color(0.02f, 0.03f, 0.08f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        float width = Mathf.Min(860f, Screen.width - 32f);
        float left = (Screen.width - width) / 2f;
        float top = Mathf.Max(10f, Screen.height * 0.06f);

        GUI.Label(new Rect(left, top, width, 40f), pages[page].title, titleStyle);
        GUI.Label(new Rect(left, top + 40f, width, 20f), "How to Play - page " + (page + 1) + " of " + pages.Length, smallStyle);

        // The moving picture
        float pictureHeight = Mathf.Min(320f, Screen.height * 0.45f);
        Rect picture = new Rect(left + 40f, top + 70f, width - 80f, pictureHeight);
        GUI.color = new Color(0.12f, 0.14f, 0.2f, 1f);
        GUI.DrawTexture(picture, Texture2D.whiteTexture);
        GUI.color = before;
        pages[page].picture(picture, PageTime);

        // The words
        float y = picture.yMax + 16f;
        foreach (string line in pages[page].words)
        {
            GUI.Label(new Rect(left, y, width, 26f), line, textStyle);
            y += 26f;
        }

        // A bar showing when the page will turn, and the buttons
        float progress = Mathf.Clamp01(PageTime / PageSeconds);
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(new Rect(left + 40f, y + 8f, width - 80f, 4f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.3f);
        GUI.DrawTexture(new Rect(left + 40f, y + 8f, (width - 80f) * progress, 4f), Texture2D.whiteTexture);
        GUI.color = before;

        float buttons = y + 22f;
        if (GUI.Button(new Rect(left + width / 2f - 230f, buttons, 100f, 32f), "< Back")) Turn(-1);
        if (GUI.Button(new Rect(left + width / 2f - 115f, buttons, 110f, 32f), paused ? "Play" : "Pause"))
        {
            if (paused) pageStarted += Time.time - pausedAt;
            else pausedAt = Time.time;
            paused = !paused;
        }
        if (GUI.Button(new Rect(left + width / 2f + 10f, buttons, 100f, 32f), "Next >")) Turn(1);
        if (GUI.Button(new Rect(left + width / 2f + 125f, buttons, 100f, 32f), "Close")) Close();
        GUI.Label(new Rect(left, buttons + 38f, width, 20f), "Space: pause   Arrows: back and forward   Esc or H: close", smallStyle);
    }

    // --- Little drawing helpers for the pictures ---

    static void Box(float x, float y, float w, float h, Color colour)
    {
        Color before = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = before;
    }

    // A block, with a darker edge like the real ones
    static void Block(float x, float y, float size, Color colour)
    {
        Box(x, y, size, size, colour * 0.75f);
        Box(x + 3f, y + 3f, size - 6f, size - 6f, colour);
    }

    void Key(float x, float y, string label, bool pressed, float w = 44f)
    {
        Box(x, y, w, 44f, pressed ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.85f, 0.9f));
        Box(x + 3f, y + 3f, w - 6f, 36f, pressed ? new Color(1f, 0.95f, 0.6f) : Color.white);
        Color before = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(x, y, w, 44f), label, keyStyle);
        GUI.color = before;
    }

    void Picture(float x, float y, float size, Texture2D texture)
    {
        if (texture != null) GUI.DrawTexture(new Rect(x, y, size, size), texture);
    }

    void Words(float x, float y, float w, string text)
    {
        GUI.Label(new Rect(x, y, w, 24f), text, smallStyle);
    }

    static readonly Color GrassGreen = new Color(0.36f, 0.70f, 0.24f), Dirt = new Color(0.50f, 0.35f, 0.20f),
                          Stone = new Color(0.55f, 0.55f, 0.58f), Planks = new Color(0.75f, 0.58f, 0.34f), Wood = new Color(0.45f, 0.32f, 0.18f),
                          You = new Color(0.25f, 0.5f, 0.95f), ZombieGreen = new Color(0.35f, 0.62f, 0.30f);

    // A little person: you, or a villager, or a zombie
    static void Person(float x, float y, Color shirt, float size = 1f)
    {
        Box(x - 8f * size, y - 46f * size, 16f * size, 16f * size, new Color(0.9f, 0.72f, 0.55f));
        Box(x - 10f * size, y - 30f * size, 20f * size, 18f * size, shirt);
        Box(x - 9f * size, y - 12f * size, 8f * size, 12f * size, new Color(0.25f, 0.22f, 0.55f));
        Box(x + 1f * size, y - 12f * size, 8f * size, 12f * size, new Color(0.25f, 0.22f, 0.55f));
    }

    // --- The pages ---

    void MakePages()
    {
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };

        string[] heartShape = { ".xx..xx.", "xxxxxxxx", "xwxxxxxx", "xxxxxxxx", ".xxxxxx.", "..xxxx..", "...xx...", "........" };
        heart = BlockInteractor.PixelArt(heartShape, new Color(0.9f, 0.1f, 0.12f));
        drumstick = BlockInteractor.PixelArt(new[] { "...xxx..", "..xxxxx.", "..xxxxx.", "..xxxx..", "...xx...", "..w.....", ".w.w....", "........" }, new Color(0.75f, 0.45f, 0.2f));
        star = BlockInteractor.PixelArt(new[] { "...xx...", "...xx...", "xxxxxxxx", ".xxxxxx.", "..xxxx..", ".xx..xx.", "xx....xx", "........" }, new Color(1f, 0.85f, 0.3f));

        pages = new[]
        {
            new Page { title = "Welcome to CubeWorld!", picture = Welcome, words = new[]
            {
                "A whole world made of blocks - and you can change every single one of them.",
                "Dig, build, explore, make friends with animals, and go on adventures.",
                "This little show teaches you how. Sit back and watch!"
            }},
            new Page { title = "Walking and jumping", picture = Walking, words = new[]
            {
                "W, A, S and D walk you forwards, left, backwards and right.",
                "Space jumps (and swims up in water). Hold Shift while walking to run!"
            }},
            new Page { title = "Looking around", picture = Looking, words = new[]
            {
                "Move the mouse to look around - or use the arrow keys.",
                "The little dot in the middle of the screen is what you're pointing at."
            }},
            new Page { title = "Breaking and building", picture = Building, words = new[]
            {
                "Hold the left mouse button (or Q) on a block to break it. Cracks show how close it is.",
                "Right click (or E) to put the block in your hand down. Build anything you like!"
            }},
            new Page { title = "Your hotbar and inventory", picture = Hotbar, words = new[]
            {
                "The row at the bottom is your hotbar. Press 1 to 9 (or the mouse wheel) to pick what's in your hand.",
                "Press I or Tab to open your inventory and see everything you've got."
            }},
            new Page { title = "Crafting", picture = Crafting, words = new[]
            {
                "Press E on a crafting table. Click a recipe in the book, then MAKE.",
                "Green recipes are ones you can make right now. Use the tabs and search to find things.",
                "No table? Press C in your inventory to make small things in your hands."
            }},
            new Page { title = "Survival: hearts and food", picture = Survival, words = new[]
            {
                "In a survival world, look after your hearts and your food.",
                "Falling, monsters, lava and fire take hearts. Eat to fill your food up - full food heals you.",
                "Hold food and press E to eat it. Cooked food is best!"
            }},
            new Page { title = "Tools", picture = Tools, words = new[]
            {
                "Pickaxes break stone fast, axes break wood fast, and shovels dig dirt and sand fast.",
                "Wood, then stone, then iron, then diamond - each one is quicker than the last."
            }},
            new Page { title = "Day, night and monsters", picture = Night, words = new[]
            {
                "The sun goes down every 10 minutes, and at night lots of monsters come out.",
                "Build a house with a door, light it with lamps, and sleep in a bed to skip to morning.",
                "Fight back with a sword or a bow - or hide near the Star Crystal."
            }},
            new Page { title = "Food, farming and cooking", picture = Farming, words = new[]
            {
                "Plant seeds on grass and watch the wheat grow. Three wheat makes bread.",
                "Put raw meat or fish in a furnace with some wood, and it cooks."
            }},
            new Page { title = "Animals and pets", picture = Animals, words = new[]
            {
                "Cows, pigs and sheep live in the fields. Press E on a sheep to shear its wool.",
                "Feed a dog some meat, or a cat some fish, and it becomes your pet and follows you!"
            }},
            new Page { title = "Getting around", picture = Travelling, words = new[]
            {
                "Put a boat on water, or a minecart on rails, and press E to get in.",
                "Press W to go. Make a minimap? It's already in the top corner - press N to hide it."
            }},
            new Page { title = "Other worlds", picture = Worlds, words = new[]
            {
                "Portals take you to amazing places: the floating Magic Land, and the fiery Nether.",
                "Make them at the crafting table, put them down, and walk onto them."
            }},
            new Page { title = "Adventures", picture = Adventure, words = new[]
            {
                "Find the village, explore dungeons, and follow treasure maps to the red X.",
                "Climb the highest mountain for the Star Crystal - and dare to face the Stone Golem deep below!"
            }},
            new Page { title = "You're ready!", picture = Ready, words = new[]
            {
                "Achievements pop up when you do something for the first time - try to get them all!",
                "Press H any time to watch this again. Have fun!"
            }},
        };
    }

    // --- The moving pictures (t is the seconds since the page started) ---

    void Welcome(Rect r, float t)
    {
        float size = r.height / 7f;
        for (int i = 0; i < 9; i++)
        {
            float x = r.x + r.width / 2f - 4.5f * size + i * size;
            int tall = 1 + Mathf.RoundToInt(2f + 2f * Mathf.Sin(i * 0.9f));
            for (int j = 0; j < tall; j++)
            {
                float bounce = Mathf.Max(0f, Mathf.Sin(t * 3f - i * 0.5f)) * 6f * (j == tall - 1 ? 1f : 0f);
                Block(x, r.yMax - (j + 1) * size - bounce, size, j == tall - 1 ? GrassGreen : j < tall - 2 ? Stone : Dirt);
            }
        }
        Box(r.x + 40f + Mathf.Repeat(t * 30f, r.width - 120f), r.y + 20f, 40f, 40f, new Color(1f, 0.9f, 0.4f));   // the sun
    }

    void Walking(Rect r, float t)
    {
        int step = (int)(t / 1.5f) % 5;   // W, D, S, A, then jump
        float into = (t % 1.5f) / 1.5f;
        Vector2 at = new Vector2(r.center.x, r.yMax - 40f);
        float[] dx = { 0f, 1f, 0f, -1f, 0f };
        float jump = step == 4 ? Mathf.Sin(into * Mathf.PI) * 60f : 0f;
        float size = step == 0 ? 1f - into * 0.3f : step == 2 ? 0.7f + into * 0.3f : step == 1 || step == 3 ? 0.85f : 1f;

        for (int i = 0; i < 12; i++) Block(r.x + 20f + i * (r.width - 40f) / 12f, r.yMax - 30f, (r.width - 40f) / 12f, GrassGreen);
        Person(at.x + dx[step] * (into - 0.5f) * 140f, at.y - 0f - jump + 10f, You, size * 1.5f);

        float kx = r.x + 30f, ky = r.y + 30f;
        Key(kx + 50f, ky, "W", step == 0);
        Key(kx, ky + 50f, "A", step == 3);
        Key(kx + 50f, ky + 50f, "S", step == 2);
        Key(kx + 100f, ky + 50f, "D", step == 1);
        Key(kx, ky + 100f, "Space", step == 4, 144f);
        Key(r.xMax - 110f, ky, "Shift", false, 80f);
        Words(r.xMax - 150f, ky + 48f, 160f, "+ W to run");
    }

    void Looking(Rect r, float t)
    {
        float look = Mathf.Sin(t * 1.2f);
        // The view: hills sliding past as you turn
        for (int i = -2; i < 14; i++)
        {
            float x = r.x + i * 70f - look * 120f;
            float h = 60f + 40f * Mathf.Sin(i * 1.3f);
            if (x > r.x && x + 70f < r.xMax) Box(x, r.yMax - h, 70f, h, Color.Lerp(GrassGreen, Dirt, (i % 3) * 0.2f));
        }
        Box(r.center.x - 2f, r.center.y - 2f, 4f, 4f, Color.white);   // the dot in the middle

        // The mouse, moving left and right
        float mx = r.xMax - 120f + look * 40f, my = r.y + 30f;
        Box(mx, my, 50f, 76f, new Color(0.85f, 0.85f, 0.9f));
        Box(mx + 24f, my, 2f, 30f, new Color(0.4f, 0.4f, 0.45f));
        Words(r.xMax - 175f, my + 80f, 160f, "move the mouse");
    }

    void Building(Rect r, float t)
    {
        float size = 60f;
        float x0 = r.center.x - 2.5f * size, y0 = r.yMax - size - 30f;
        float phase = t % 6f;
        bool gone = phase > 2.2f && phase < 4f;
        bool placed = phase >= 4f;

        for (int i = 0; i < 5; i++)
        {
            if (i == 2 && gone) continue;
            Block(x0 + i * size, y0, size, i == 2 && placed ? Planks : Stone);
        }
        // Cracks spreading while you hold the button
        if (phase < 2.2f)
        {
            int cracks = (int)(phase / 2.2f * 6f);
            for (int c = 0; c < cracks; c++)
                Box(x0 + 2 * size + 10f + (c * 13) % 40, y0 + 8f + (c * 23) % 44, 14f, 3f, new Color(0.05f, 0.05f, 0.05f));
        }
        Box(x0 + 2.5f * size - 2f, y0 - 30f, 4f, 4f, Color.white);

        float mx = r.x + 40f, my = r.y + 30f;
        Box(mx, my, 56f, 84f, new Color(0.85f, 0.85f, 0.9f));
        Box(mx + 3f, my + 3f, 24f, 34f, phase < 2.2f ? new Color(1f, 0.85f, 0.3f) : Color.white);   // left button
        Box(mx + 29f, my + 3f, 24f, 34f, phase >= 4f && phase < 4.5f ? new Color(1f, 0.85f, 0.3f) : Color.white);
        Words(mx - 40f, my + 90f, 140f, phase < 4f ? "hold left: break" : "right click: place");
    }

    void Hotbar(Rect r, float t)
    {
        byte[] items = { 1, 2, 3, 4, 5, 6, 8, 17, 30 };
        Color[] colours = { GrassGreen, Dirt, Stone, Wood, new Color(0.86f, 0.8f, 0.55f), new Color(0.17f, 0.45f, 0.13f),
                            new Color(0.8f, 0.15f, 0.1f), Planks, Color.white };
        int picked = (int)(t / 0.8f) % 9;
        bool showInventory = (t % 9f) > 6f;
        float size = 54f, left = r.center.x - 4.5f * (size + 4f);

        for (int i = 0; i < 9; i++)
        {
            float x = left + i * (size + 4f), y = r.yMax - size - 20f;
            if (i == picked) Box(x - 3f, y - 3f, size + 6f, size + 6f, Color.white);
            Box(x, y, size, size, new Color(0.15f, 0.15f, 0.15f));
            Texture2D pic = BlockInteractor.PictureOf(items[i]);
            if (pic != null) Picture(x + 10f, y + 10f, size - 20f, pic);
            else Block(x + 10f, y + 10f, size - 20f, colours[i]);
            Words(x - 20f, y - 26f, size + 40f, (i + 1).ToString());
        }
        Key(r.x + 30f, r.y + 30f, (picked + 1).ToString(), true);
        Key(r.x + 90f, r.y + 30f, "Tab", showInventory, 70f);

        if (showInventory)
        {
            Rect inv = new Rect(r.center.x - 200f, r.y + 20f, 400f, r.height - 120f);
            Box(inv.x, inv.y, inv.width, inv.height, new Color(0.05f, 0.05f, 0.05f, 0.9f));
            for (int i = 0; i < 40; i++)
                Block(inv.x + 10f + (i % 10) * 38f, inv.y + 10f + (i / 10) * 38f, 34f, colours[i % colours.Length]);
        }
    }

    void Crafting(Rect r, float t)
    {
        float cell = 56f, left = r.x + 60f, top = r.y + 40f;
        int filled = Mathf.Min(4, (int)(t / 1f));
        for (int i = 0; i < 9; i++)
        {
            float x = left + (i % 3) * (cell + 6f), y = top + (i / 3) * (cell + 6f);
            Box(x, y, cell, cell, new Color(0.15f, 0.15f, 0.15f));
            bool hasPlank = (i == 0 || i == 1 || i == 3 || i == 4) && filled > (i < 2 ? i : i - 1);
            if (hasPlank) Block(x + 8f, y + 8f, cell - 16f, Planks);
        }
        Words(left + 3 * (cell + 6f), top + cell + 20f, 50f, "=>");
        Rect result = new Rect(left + 3 * (cell + 6f) + 50f, top + cell, cell + 10f, cell + 10f);
        Box(result.x, result.y, result.width, result.height, filled >= 4 ? Color.green : new Color(0.15f, 0.15f, 0.15f));
        if (filled >= 4)
        {
            Block(result.x + 8f, result.y + 8f, result.width - 16f, new Color(0.62f, 0.42f, 0.22f));
            Box(result.x + 16f, result.y + 16f, result.width - 32f, 6f, new Color(0.35f, 0.22f, 0.10f));
        }
        bool flash = t > 5f && Mathf.Repeat(t * 2f, 1f) < 0.5f;
        Box(left, top + 3 * (cell + 6f) + 10f, 3 * (cell + 6f) + 120f, 40f, flash ? new Color(1f, 0.85f, 0.3f) : new Color(0.3f, 0.3f, 0.35f));
        Words(left, top + 3 * (cell + 6f) + 18f, 3 * (cell + 6f) + 120f, "MAKE CRAFTING TABLE");

        // The recipe book
        float bx = r.xMax - 300f;
        for (int i = 0; i < 6; i++)
            Box(bx, r.y + 30f + i * 42f, 260f, 38f, i == 1 ? new Color(0.12f, 0.35f, 0.12f) : new Color(0.15f, 0.15f, 0.15f));
        Words(bx, r.y + 6f, 260f, "the recipe book");
    }

    void Survival(Rect r, float t)
    {
        float phase = t % 9f;
        int hearts = phase < 3f ? 20 - (int)(phase * 3f) : phase < 6f ? 11 : 11 + (int)((phase - 6f) * 3f);
        int food = phase < 4f ? 20 - (int)(phase * 2f) : phase < 5f ? 12 : 20;
        hearts = Mathf.Clamp(hearts, 0, 20);

        for (int i = 0; i < 10; i++)
        {
            GUI.color = i * 2 < hearts ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            Picture(r.x + 60f + i * 30f, r.y + 60f, 28f, heart);
            GUI.color = i * 2 < food ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            Picture(r.x + 60f + i * 30f, r.y + 110f, 28f, drumstick);
        }
        GUI.color = Color.white;
        Words(r.x + 360f, r.y + 62f, 120f, "hearts");
        Words(r.x + 360f, r.y + 112f, 120f, "food");

        if (phase < 3f) { Person(r.xMax - 200f, r.yMax - 30f, You, 1.6f); Person(r.xMax - 140f + Mathf.Sin(t * 8f) * 6f, r.yMax - 30f, ZombieGreen, 1.6f); Words(r.xMax - 260f, r.y + 30f, 200f, "Ouch! A zombie!"); }
        else if (phase < 6f) { Picture(r.xMax - 200f, r.yMax - 140f, 80f, BlockInteractor.PictureOf(202)); Words(r.xMax - 260f, r.y + 30f, 200f, "Eat something - press E"); }
        else Words(r.xMax - 260f, r.y + 30f, 200f, "Full food heals your hearts");
    }

    void Tools(Rect r, float t)
    {
        byte[] tools = { 156, 157, 158, 159 };
        string[] names = { "wood", "stone", "iron", "diamond" };
        for (int i = 0; i < 4; i++)
        {
            float x = r.x + 40f + i * (r.width - 80f) / 4f, y = r.y + 40f;
            float swing = Mathf.Sin(t * (6f + i * 3f)) * 0.2f;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(swing * 57f, new Vector2(x + 40f, y + 80f));
            Picture(x, y, 80f, BlockInteractor.PictureOf(tools[i]));
            GUI.matrix = old;

            // How quickly each one breaks stone
            float speed = new[] { 2f, 3.5f, 5f, 8f }[i];
            float progress = Mathf.Repeat(t * speed / 3f, 1f);
            Block(x + 10f, y + 120f, 60f, Stone);
            Box(x, y + 190f, 80f, 10f, new Color(0.2f, 0.2f, 0.2f));
            Box(x, y + 190f, 80f * progress, 10f, new Color(1f, 0.85f, 0.3f));
            Words(x - 20f, y + 204f, 120f, names[i]);
        }
    }

    void Night(Rect r, float t)
    {
        float dark = Mathf.Clamp01(Mathf.Sin(t * 0.7f) * 1.5f + 0.5f);
        Box(r.x, r.y, r.width, r.height, Color.Lerp(new Color(0.45f, 0.65f, 0.95f), new Color(0.03f, 0.03f, 0.10f), dark));
        if (dark > 0.6f)
            for (int i = 0; i < 30; i++)
                Box(r.x + (i * 97) % r.width, r.y + (i * 53) % (r.height * 0.5f), 3f, 3f, Color.white);
        Box(r.x + 60f + dark * (r.width - 200f), r.y + 30f + Mathf.Sin(dark * 3f) * 20f, 36f, 36f, dark > 0.5f ? new Color(0.9f, 0.9f, 1f) : new Color(1f, 0.9f, 0.4f));

        // A little house with a lamp - safe inside
        float hx = r.center.x - 70f, hy = r.yMax - 130f;
        Box(hx, hy, 140f, 110f, Planks);
        Box(hx - 10f, hy - 20f, 160f, 24f, Wood);
        Box(hx + 55f, hy + 50f, 30f, 60f, new Color(0.35f, 0.22f, 0.10f));
        Box(hx + 15f, hy + 25f, 28f, 28f, dark > 0.5f ? new Color(1f, 0.85f, 0.4f) : new Color(0.7f, 0.85f, 1f));
        for (int i = 0; i < 12; i++) Block(r.x + i * (r.width / 12f), r.yMax - 20f, r.width / 12f, GrassGreen);

        if (dark > 0.5f)
        {
            Person(r.x + 80f + Mathf.Sin(t * 2f) * 20f, r.yMax - 20f, ZombieGreen, 1.4f);
            Person(r.xMax - 90f + Mathf.Sin(t * 2.5f) * 20f, r.yMax - 20f, new Color(0.88f, 0.87f, 0.82f), 1.4f);
            Words(r.xMax - 260f, r.y + 20f, 240f, "Night - watch out!");
        }
        else Words(r.xMax - 260f, r.y + 20f, 240f, "Day - safe to explore");
    }

    void Farming(Rect r, float t)
    {
        for (int i = 0; i < 6; i++)
        {
            float x = r.x + 40f + i * 50f, ground = r.yMax - 60f;
            Block(x, ground, 46f, new Color(0.33f, 0.21f, 0.11f));
            float grown = Mathf.Clamp01((t - i * 0.4f) / 5f);
            Box(x + 20f, ground - grown * 80f, 6f, grown * 80f, Color.Lerp(new Color(0.35f, 0.7f, 0.2f), new Color(0.88f, 0.75f, 0.3f), grown));
            if (grown >= 1f) Box(x + 16f, ground - 90f, 14f, 16f, new Color(0.88f, 0.75f, 0.3f));
        }
        Words(r.x + 40f, r.yMax - 20f, 300f, "seeds -> wheat");

        // A furnace cooking
        float fx = r.xMax - 260f, fy = r.y + 60f;
        Block(fx, fy, 110f, Stone);
        Box(fx + 25f, fy + 50f, 60f, 40f, new Color(0.1f, 0.1f, 0.1f));
        Box(fx + 30f, fy + 70f, 50f, 15f + Mathf.Sin(t * 12f) * 4f, new Color(1f, 0.55f, 0.15f));
        bool cooked = t % 6f > 3f;
        Picture(fx + 140f, fy + 20f, 70f, BlockInteractor.PictureOf(cooked ? (byte)202 : (byte)201));
        Words(fx + 110f, fy + 100f, 140f, cooked ? "steak!" : "raw beef");
    }

    void Animals(Rect r, float t)
    {
        for (int i = 0; i < 12; i++) Block(r.x + i * (r.width / 12f), r.yMax - 30f, r.width / 12f, GrassGreen);
        // A cow and a sheep wandering
        float cow = r.x + 80f + Mathf.Sin(t * 0.5f) * 40f;
        Box(cow, r.yMax - 90f, 80f, 40f, new Color(0.35f, 0.24f, 0.16f));
        Box(cow + 15f, r.yMax - 85f, 25f, 20f, Color.white);
        Box(cow + 70f, r.yMax - 100f, 30f, 30f, new Color(0.35f, 0.24f, 0.16f));
        for (int l = 0; l < 2; l++) Box(cow + 5f + l * 60f, r.yMax - 50f, 10f, 20f, new Color(0.35f, 0.24f, 0.16f));
        float sheep = r.x + 260f + Mathf.Cos(t * 0.6f) * 30f;
        Box(sheep, r.yMax - 85f, 70f, 40f, new Color(0.95f, 0.95f, 0.93f));
        Box(sheep + 60f, r.yMax - 92f, 24f, 24f, new Color(0.85f, 0.78f, 0.7f));

        // You, with your dog following along behind
        float you = r.center.x + 60f + Mathf.Sin(t * 0.8f) * 120f;
        Person(you, r.yMax - 30f, You, 1.5f);
        float dog = you - 60f * Mathf.Sign(Mathf.Cos(t * 0.8f));
        Box(dog - 25f, r.yMax - 60f, 50f, 22f, new Color(0.72f, 0.52f, 0.3f));
        Box(dog + 18f * Mathf.Sign(Mathf.Cos(t * 0.8f)), r.yMax - 75f, 20f, 20f, new Color(0.72f, 0.52f, 0.3f));
        if (Mathf.Repeat(t, 3f) < 1f) Picture(dog - 10f, r.yMax - 110f - Mathf.Repeat(t, 1f) * 20f, 20f, heart);
        Words(you - 80f, r.y + 20f, 160f, "your pet dog");
    }

    void Travelling(Rect r, float t)
    {
        // Rails with a minecart
        float railY = r.y + r.height * 0.45f;
        for (int i = 0; i < 20; i++) Box(r.x + i * r.width / 20f + 4f, railY, 6f, 18f, new Color(0.42f, 0.28f, 0.15f));
        Box(r.x, railY + 2f, r.width, 4f, new Color(0.62f, 0.62f, 0.66f));
        Box(r.x, railY + 12f, r.width, 4f, new Color(0.62f, 0.62f, 0.66f));
        float cart = r.x + Mathf.Repeat(t * 120f, r.width - 80f);
        Box(cart, railY - 36f, 80f, 36f, new Color(0.55f, 0.56f, 0.6f));
        Person(cart + 40f, railY - 20f, You, 1.1f);

        // Water with a boat
        float waterY = r.yMax - 60f;
        Box(r.x, waterY, r.width, 60f, new Color(0.2f, 0.45f, 0.85f));
        float boat = r.xMax - 120f - Mathf.Repeat(t * 70f, r.width - 140f);
        Box(boat, waterY - 18f + Mathf.Sin(t * 3f) * 3f, 100f, 22f, Planks);
        Person(boat + 50f, waterY - 12f + Mathf.Sin(t * 3f) * 3f, You, 1.1f);
    }

    void Worlds(Rect r, float t)
    {
        string[] names = { "Portal", "Magic Land", "Nether" };
        Color[] a = { new Color(0.45f, 0.1f, 0.75f), new Color(0.95f, 0.3f, 0.7f), new Color(0.7f, 0.1f, 0.02f) };
        Color[] b = { new Color(0.85f, 0.55f, 1f), new Color(0.4f, 0.95f, 1f), new Color(1f, 0.75f, 0.15f) };
        for (int p = 0; p < 3; p++)
        {
            float x = r.x + 50f + p * (r.width - 100f) / 3f, y = r.y + 40f, size = 140f;
            for (int i = 0; i < 7; i++)
                for (int j = 0; j < 7; j++)
                {
                    float swirl = Mathf.Sin(i * 0.9f + j * 0.7f + t * 3f + p);
                    Box(x + i * 20f, y + j * 20f, 20f, 20f, Color.Lerp(a[p], b[p], swirl * 0.5f + 0.5f));
                }
            Words(x - 20f, y + size + 10f, size + 40f, names[p]);
        }
    }

    void Adventure(Rect r, float t)
    {
        // A treasure map, its needle pointing to an X
        Picture(r.x + 40f, r.y + 40f, 120f, BlockInteractor.PictureOf(216));
        float xSpot = r.x + 260f, ySpot = r.yMax - 70f;
        Box(xSpot - 30f, ySpot + 10f, 200f, 30f, GrassGreen);
        for (int i = -2; i <= 2; i++)
        {
            Box(xSpot + 60f + i * 10f, ySpot + 15f + (i + 2) * 3f, 10f, 4f, new Color(0.8f, 0.15f, 0.15f));
            Box(xSpot + 60f + i * 10f, ySpot + 27f - (i + 2) * 3f, 10f, 4f, new Color(0.8f, 0.15f, 0.15f));
        }
        if (Mathf.Repeat(t, 4f) > 2f) Words(xSpot - 20f, ySpot - 20f, 240f, "Dig here!");

        // The Stone Golem, stomping about
        float gx = r.xMax - 180f + Mathf.Sin(t * 0.8f) * 30f, gy = r.yMax - 20f;
        float stomp = Mathf.Abs(Mathf.Sin(t * 2f)) * 6f;
        Box(gx - 40f, gy - 170f - stomp, 80f, 80f, Stone);
        Box(gx - 25f, gy - 210f - stomp, 50f, 42f, Stone);
        Box(gx - 15f, gy - 196f - stomp, 10f, 6f, new Color(1f, 0.5f, 0.1f));
        Box(gx + 5f, gy - 196f - stomp, 10f, 6f, new Color(1f, 0.5f, 0.1f));
        Box(gx - 60f, gy - 165f - stomp, 20f, 90f, Stone);
        Box(gx + 40f, gy - 165f - stomp, 20f, 90f, Stone);
        Box(gx - 35f, gy - 90f, 28f, 90f, Stone);
        Box(gx + 7f, gy - 90f, 28f, 90f, Stone);
        Words(gx - 100f, r.y + 10f, 200f, "the Stone Golem!");
    }

    void Ready(Rect r, float t)
    {
        float slide = Mathf.Clamp01(t / 0.6f);
        Rect box = new Rect(r.center.x - 180f, r.y + 30f + (1f - slide) * -60f, 360f, 70f);
        Box(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f, new Color(0.95f, 0.75f, 0.15f));
        Box(box.x, box.y, box.width, box.height, new Color(0.12f, 0.10f, 0.06f));
        GUI.Label(new Rect(box.x, box.y + 8f, box.width, 26f), "Achievement: DIAMONDS!", keyStyle);
        Words(box.x, box.y + 40f, box.width, "Dug up a diamond");

        for (int i = 0; i < 5; i++)
        {
            float bob = Mathf.Sin(t * 3f + i) * 10f;
            Picture(r.x + 80f + i * (r.width - 200f) / 4f, r.yMax - 110f + bob, 60f, star);
        }
    }
}
