using System.Collections.Generic;
using UnityEngine;

// Achievements: little gold pop-ups for doing something for the first time -
// "DIAMONDS!", "Survived a Night!", "Gone Fishing" and lots more.
// Each one only pops up once, ever (they're shared between all your worlds).
// The pause screen shows how many you've got.
public static class Achievements
{
    // id, title, what you did
    static readonly string[,] All =
    {
        { "wood",     "Getting Wood",       "Broke your first piece of wood" },
        { "craft",    "Crafty",             "Made something at a crafting table" },
        { "pickaxe",  "Time to Mine!",      "Made a pickaxe" },
        { "iron",     "Iron Age",           "Dug up some iron" },
        { "diamond",  "DIAMONDS!",          "Dug up a diamond" },
        { "cook",     "Hot Stuff",          "Cooked something in a furnace" },
        { "night",    "Survived a Night!",  "Made it through a whole night in survival" },
        { "monster",  "Monster Hunter",     "Defeated a monster" },
        { "golem",    "Golem Smasher",      "Defeated the Stone Golem" },
        { "treasure", "Treasure Hunter",    "Found buried treasure" },
        { "pet",      "Best Friends",       "Tamed a pet" },
        { "farm",     "Farmer",             "Picked some ripe wheat" },
        { "fish",     "Gone Fishing",       "Caught a fish" },
        { "boat",     "Ahoy!",              "Went for a ride in a boat" },
        { "cart",     "All Aboard!",        "Rode in a minecart" },
        { "nether",   "Into the Fire",      "Visited the Nether" },
        { "magic",    "Over the Rainbow",   "Visited the Magic Land" },
        { "star",     "Star Finder",        "Found the Star Crystal" },
        { "dungeon",  "Dungeon Delver",     "Broke a monster cage" },
        { "sleep",    "Sweet Dreams",       "Slept in a bed" },
        { "dragon",   "Dragon Rider",       "Rode a dragon" },
        { "armour",   "Suit Up",            "Put on some armour" },
        { "village",  "Neighbours",         "Found the village" },
        { "bow",      "Bullseye",           "Hit a monster with an arrow" },
    };

    public static int Total { get { return All.GetLength(0); } }

    public static int Got
    {
        get
        {
            int got = 0;
            for (int i = 0; i < Total; i++)
                if (PlayerPrefs.GetInt("achievement-" + All[i, 0], 0) == 1) got++;
            return got;
        }
    }

    // Done something? Pops up the first time only
    public static void Unlock(string id)
    {
        if (WorldMenu.CurrentSlot < 0) return;
        string key = "achievement-" + id;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;

        for (int i = 0; i < Total; i++)
        {
            if (All[i, 0] != id) continue;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            AchievementPopup.Show(All[i, 1], All[i, 2]);
            return;
        }
    }
}

// The gold box that slides in at the top of the screen
public class AchievementPopup : MonoBehaviour
{
    const float ShowSeconds = 4f;

    private static AchievementPopup instance;
    private readonly Queue<string[]> waiting = new Queue<string[]>();
    private string[] showing;
    private float shownAt;
    private GUIStyle titleStyle, textStyle;

    public static void Show(string title, string text)
    {
        if (instance == null)
        {
            GameObject holder = new GameObject("Achievement Popups");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<AchievementPopup>();
        }
        instance.waiting.Enqueue(new[] { title, text });
        Sounds.Pop(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }

    void OnGUI()
    {
        GUI.depth = -20;
        if (showing == null || Time.time - shownAt > ShowSeconds)
        {
            showing = waiting.Count > 0 ? waiting.Dequeue() : null;
            shownAt = Time.time;
            if (showing == null) return;
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        }

        // Slides down from the top, then back up
        float t = Time.time - shownAt;
        float down = Mathf.Clamp01(Mathf.Min(t / 0.3f, (ShowSeconds - t) / 0.3f));
        Rect box = new Rect(Screen.width / 2f - 160f, -70f + down * 80f, 320f, 62f);

        Color before = GUI.color;
        GUI.color = new Color(0.95f, 0.75f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.10f, 0.06f, 0.95f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.3f);
        GUI.Label(new Rect(box.x, box.y + 6, box.width, 22), "Achievement: " + showing[0], titleStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(box.x, box.y + 32, box.width, 22), showing[1], textStyle);
        GUI.color = before;
    }
}
