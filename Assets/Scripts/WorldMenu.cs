using System;
using UnityEngine;

// The screen where you pick one of three worlds, plus the pause screen that
// takes you back to it. While you play, it also saves your world every minute.
// Magic portals come through here too: travelling to the Magic Land saves
// the place you're leaving and opens the other one.
public class WorldMenu : MonoBehaviour
{
    const float AutosaveSeconds = 60f;

    private static WorldMenu instance;

    private BlockWorld world;            // null while we're on the menu
    private FirstPersonPlayer player;
    private int currentSlot = -1;
    private int currentPlace;            // home, or the Magic Land
    private string arrivalMessage = "";
    private float arrivalTime = -100f;
    private float autosaveTimer;

    private int highlighted;             // the slot the keyboard is pointing at
    private int confirmingDelete = -1;   // the slot waiting for "are you sure?"
    private int choosingMode = -1;       // the empty slot waiting for "creative or survival?"
    private string[] slotInfo = new string[WorldSaves.Slots];
    private string problem = "";
    private float showSavedUntil;

    private GUIStyle titleStyle;
    private GUIStyle bigLabelStyle;

    bool Playing { get { return world != null; } }

    // Which world (0 to 2, or -1 on the menu) and which place in it is open,
    // so magic villagers know where they are
    public static int CurrentSlot { get { return instance != null && instance.Playing ? instance.currentSlot : -1; } }
    public static int CurrentPlace { get { return instance != null ? instance.currentPlace : WorldSaves.Home; } }

    // Is the world you're playing a survival world?
    public static bool Survival { get; private set; }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        RefreshSlotInfo();
    }

    void Update()
    {
        if (Tutorial.IsOpen) return;   // watching the tutorial

        if (!Playing)
        {
            // The mouse should always be free on the menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuKeys();
            return;
        }

        // H: watch the tutorial (and it opens by itself the very first time you play)
        if (!BlockInteractor.InventoryOpen && (Input.GetKeyDown(KeyCode.H) || PlayerPrefs.GetInt("tutorial-seen", 0) == 0))
        {
            PlayerPrefs.SetInt("tutorial-seen", 1);
            Tutorial.Open();
            return;
        }

        autosaveTimer -= Time.deltaTime;
        if (autosaveTimer <= 0f)
            Save();

        // M on the pause screen (after pressing Esc) goes back to the menu
        if (Cursor.lockState != CursorLockMode.Locked && !BlockInteractor.InventoryOpen && Input.GetKeyDown(KeyCode.M))
            SaveAndLeave();
    }

    // Stopping Play in the editor, or closing the game, saves too
    void OnApplicationQuit()
    {
        if (Playing) Save();
    }

    // --- Opening, saving and leaving worlds ---

    // Back into a world, in whichever place you left it
    void Open(int slot)
    {
        problem = "";
        confirmingDelete = -1;

        // A brand new world? First choose what kind
        if (!WorldSaves.Exists(slot))
        {
            highlighted = slot;
            choosingMode = slot;
            return;
        }
        Enter(slot, WorldSaves.Exists(slot) ? WorldSaves.LastPlace(slot) : WorldSaves.Home);
    }

    void StartNew(int slot, bool survival)
    {
        choosingMode = -1;
        WorldSaves.SetSurvival(slot, survival);
        Enter(slot, WorldSaves.Home);
    }

    // Builds or loads one place of a world, and puts you in it
    bool Enter(int slot, int place)
    {
        bool magic = place == WorldSaves.MagicLand;
        bool nether = place == WorldSaves.Nether;
        world = new GameObject(magic ? "Magic Land" : nether ? "Nether" : "World").AddComponent<BlockWorld>();
        Vector3 position;
        float yaw, pitch;
        bool brandNew = !WorldSaves.Exists(slot, place);

        if (brandNew)
        {
            if (magic) world.GenerateMagicLand();
            else if (nether) world.GenerateNether();
            else world.Generate();

            position = world.FindSpawnPoint();
            yaw = 0f;
            pitch = 0f;

            // A way back home, right next to you
            if (magic) world.PlaceReturnPortal(position, BlockWorld.MagicPortal, BlockWorld.MagicGrass);
            if (nether) world.PlaceReturnPortal(position, BlockWorld.FirePortal, BlockWorld.Ash);
        }
        else if (!WorldSaves.Load(slot, place, world, out position, out yaw, out pitch))
        {
            Destroy(world.gameObject);
            world = null;
            problem = "World " + (slot + 1) + (magic ? "'s Magic Land" : nether ? "'s Nether" : "") +
                      " couldn't be loaded - the save may be damaged. You can delete it and start a new one.";
            return false;
        }

        // Something rare and special at the top of the highest mountain
        if (place == WorldSaves.Home)
        {
            StarShrine.BuildIfMissing(world, slot);
            Structures.BuildIfMissing(world, slot);   // the village, dungeons and the Golem Hall
        }

        currentSlot = slot;
        currentPlace = place;
        Survival = WorldSaves.IsSurvival(slot);
        WorldSaves.SetLastPlace(slot, place);
        SetSky(place);

        player = GameBootstrap.StartGame(world, position, yaw, pitch);
        autosaveTimer = AutosaveSeconds;

        if (brandNew) Save();   // so the place isn't missing even if you leave straight away
        return true;
    }

    // A magic portal was stepped on: to the Magic Land - or, from there, back home
    public static void TravelThroughMagicPortal()
    {
        if (instance != null && instance.Playing)
            instance.Travel(instance.currentPlace == WorldSaves.MagicLand ? WorldSaves.Home : WorldSaves.MagicLand);
    }

    // A fire portal: to the Nether - or, from there, back home
    public static void TravelThroughFirePortal()
    {
        if (instance != null && instance.Playing)
            instance.Travel(instance.currentPlace == WorldSaves.Nether ? WorldSaves.Home : WorldSaves.Nether);
    }

    // Saves the place you're leaving, and opens the one you're going to
    void Travel(int destination)
    {
        // Between home and the Magic Land, a villager or unicorn on your leash
        // (or the unicorn you're riding) comes with you
        bool magicTrip = destination + currentPlace == WorldSaves.Home + WorldSaves.MagicLand;
        bool wasRiding = false;
        if (magicTrip)
        {
            foreach (Villager person in FindObjectsByType<Villager>(FindObjectsSortMode.None))
                if (person.IsOnLeash)
                    person.GoThroughPortal();

            wasRiding = player != null && player.RidingOn is Unicorn;
            foreach (Unicorn unicorn in FindObjectsByType<Unicorn>(FindObjectsSortMode.None))
                if (unicorn.OnLeashOrRidden)
                    unicorn.GoThroughPortal();
        }

        Save();
        GameBootstrap.EndGame();
        world = null;
        player = null;

        if (Enter(currentSlot, destination))
        {
            if (wasRiding) player.MountNearestUnicornOnStart = true;   // still on its back when you arrive
            if (destination == WorldSaves.Nether) Achievements.Unlock("nether");
            if (destination == WorldSaves.MagicLand) Achievements.Unlock("magic");
            arrivalMessage = destination == WorldSaves.MagicLand ? "Welcome to the Magic Land!"
                           : destination == WorldSaves.Nether ? "Welcome to the Nether!"
                           : "Welcome home!";
            arrivalTime = Time.time;
        }
        else
        {
            currentSlot = -1;   // couldn't get there - back to the menu, which explains why
            SetSky(WorldSaves.Home);
            RefreshSlotInfo();
        }
    }

    // The Magic Land has a purple sky with a purple haze in the distance.
    // The Nether has a dark red sky, thick red smoke, and fiery orange sunlight.
    static void SetSky(int place)
    {
        Camera cam = Camera.main;
        bool home = place == WorldSaves.Home;
        bool nether = place == WorldSaves.Nether;
        Color sky = nether ? new Color(0.30f, 0.07f, 0.04f) : new Color(0.38f, 0.22f, 0.55f);

        if (cam != null)
        {
            cam.clearFlags = home ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
        }

        RenderSettings.fog = !home;
        RenderSettings.fogColor = sky;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = nether ? 15f : 25f;
        RenderSettings.fogEndDistance = nether ? 65f : 80f;

        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional)
                light.color = nether ? new Color(1.00f, 0.55f, 0.35f) : Color.white;
    }

    void Save()
    {
        autosaveTimer = AutosaveSeconds;

        try
        {
            WorldSaves.Save(currentSlot, currentPlace, world, player);
            Vehicle.SaveAll(currentSlot, currentPlace);
            showSavedUntil = Time.time + 2f;
        }
        catch (Exception e)
        {
            Debug.LogError("Couldn't save world " + (currentSlot + 1) + ": " + e.Message);
        }
    }

    void SaveAndLeave()
    {
        Save();
        GameBootstrap.EndGame();

        world = null;
        player = null;
        currentSlot = -1;
        SetSky(WorldSaves.Home);
        RefreshSlotInfo();
    }

    void Delete(int slot)
    {
        WorldSaves.Delete(slot);
        confirmingDelete = -1;
        RefreshSlotInfo();
    }

    void RefreshSlotInfo()
    {
        for (int i = 0; i < WorldSaves.Slots; i++)
        {
            if (!WorldSaves.Exists(i))
            {
                slotInfo[i] = "Empty";
                continue;
            }

            DateTime? saved = WorldSaves.WhenSaved(i);
            slotInfo[i] = saved.HasValue
                ? (WorldSaves.IsSurvival(i) ? "Survival - " : "Creative - ") + "last played " + saved.Value.ToString("MMM d, h:mm tt")
                : "Damaged save";
        }
    }

    // --- Keyboard controls for the menu ---

    void MenuKeys()
    {
        if (choosingMode >= 0)
        {
            if (Input.GetKeyDown(KeyCode.C))
                StartNew(choosingMode, false);
            else if (Input.GetKeyDown(KeyCode.S))
                StartNew(choosingMode, true);
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.N))
                choosingMode = -1;
            return;
        }

        if (confirmingDelete >= 0)
        {
            if (Input.GetKeyDown(KeyCode.Y))
                Delete(confirmingDelete);
            else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape))
                confirmingDelete = -1;
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))   highlighted = (highlighted + WorldSaves.Slots - 1) % WorldSaves.Slots;
        if (Input.GetKeyDown(KeyCode.DownArrow)) highlighted = (highlighted + 1) % WorldSaves.Slots;

        if (Input.GetKeyDown(KeyCode.Alpha1)) Open(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) Open(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) Open(2);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Open(highlighted);
        else if (Input.GetKeyDown(KeyCode.H)) Tutorial.Open();
        else if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) && WorldSaves.Exists(highlighted))
            confirmingDelete = highlighted;
    }

    // --- Drawing ---

    void OnGUI()
    {
        GUI.depth = -10;   // draw over the in-game controls text

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        if (Tutorial.IsOpen) return;   // it draws itself, over everything

        if (!Playing)
            DrawMenu();
        else if (Cursor.lockState != CursorLockMode.Locked && !BlockInteractor.InventoryOpen)
            DrawPauseScreen();

        if (Playing && Time.time < showSavedUntil)
            GUI.Label(new Rect(Screen.width - 130, 10, 120, 24), "World saved");

        if (Playing) DrawArrival();
    }

    // Just came through a magic portal: a purple flash, and a welcome
    void DrawArrival()
    {
        float since = Time.time - arrivalTime;
        if (since > 3f) return;

        Color before = GUI.color;
        GUI.color = new Color(0.6f, 0.3f, 0.9f, Mathf.Clamp01(1f - since / 0.8f) * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), arrivalMessage, titleStyle);
    }

    void DrawMenu()
    {
        DarkenScreen(0.6f);

        float width = Mathf.Min(520f, Screen.width - 32f);
        float x = (Screen.width - width) / 2f;
        float y = Screen.height * 0.12f;

        GUI.Label(new Rect(x, y, width, 60), "CubeWorld", titleStyle);
        y += 70;
        GUI.Label(new Rect(x, y, width, 24), "Choose a world");
        y += 34;

        for (int i = 0; i < WorldSaves.Slots; i++)
        {
            Rect row = new Rect(x, y, width, 72);

            // Two boxes on top of each other look brighter - that's the highlight
            GUI.Box(row, "");
            if (i == highlighted) GUI.Box(row, "");

            GUI.Label(new Rect(x + 14, y + 10, 200, 26), "World " + (i + 1), bigLabelStyle);
            if (choosingMode != i) GUI.Label(new Rect(x + 14, y + 38, 300, 24), slotInfo[i]);

            Rect firstButton = new Rect(x + width - 210, y + 20, 100, 32);
            Rect secondButton = new Rect(x + width - 104, y + 20, 90, 32);

            if (choosingMode == i)
            {
                GUI.Label(new Rect(x + 14, y + 38, 300, 24), "What kind of world?  (Esc: never mind)");
                if (GUI.Button(new Rect(x + width - 250, y + 20, 116, 32), "Creative (C)")) StartNew(i, false);
                if (GUI.Button(new Rect(x + width - 128, y + 20, 114, 32), "Survival (S)")) StartNew(i, true);
            }
            else if (confirmingDelete == i)
            {
                GUI.Label(new Rect(x + width - 340, y + 26, 130, 24), "Delete for good?");
                if (GUI.Button(firstButton, "Yes (Y)")) Delete(i);
                if (GUI.Button(secondButton, "No (N)")) confirmingDelete = -1;
            }
            else
            {
                bool saved = WorldSaves.Exists(i);
                if (GUI.Button(firstButton, saved ? "Play (" + (i + 1) + ")" : "New (" + (i + 1) + ")")) Open(i);
                if (saved && GUI.Button(secondButton, "Delete")) { highlighted = i; confirmingDelete = i; }
            }

            y += 82;
        }

        y += 10;
        if (GUI.Button(new Rect(x + width / 2f - 110f, y, 220f, 36f), "How to Play  (H)")) Tutorial.Open();
        y += 46;
        GUI.Label(new Rect(x, y, width, 24), "Up / Down to choose,  Enter to play,  1-3 to jump straight in,  Delete to remove");

        if (choosingMode >= 0)
        {
            GUI.Label(new Rect(x, y + 30, width, 24), "Creative: every block, as many as you like - just build!");
            GUI.Label(new Rect(x, y + 52, width, 48), "Survival: start with nothing. Collect the blocks you break, craft the rest, and look after your hearts and food. Watch out for zombies, skeletons, spiders and slimes!");
        }
        else if (problem != "")
            GUI.Label(new Rect(x, y + 30, width, 48), problem);
    }

    void DrawPauseScreen()
    {
        DarkenScreen(0.4f);

        float width = Mathf.Min(420f, Screen.width - 32f);
        Rect box = new Rect((Screen.width - width) / 2f, Screen.height / 2f - 80f, width, 190);
        GUI.Box(box, "");

        string where = currentPlace == WorldSaves.MagicLand ? " (Magic Land)" : currentPlace == WorldSaves.Nether ? " (Nether)" : "";
        GUI.Label(new Rect(box.x + 20, box.y + 14, width - 40, 30), "Paused - World " + (currentSlot + 1) + where, bigLabelStyle);
        GUI.Label(new Rect(box.x + 20, box.y + 54, width - 40, 24), "Enter or click: keep playing");
        GUI.Label(new Rect(box.x + 20, box.y + 80, width - 40, 24), "M: save and go back to the world menu");
        GUI.Label(new Rect(box.x + 20, box.y + 116, width - 40, 24), "Your world also saves itself every minute.");
        GUI.Label(new Rect(box.x + 20, box.y + 146, width - 40, 24), "Achievements: " + Achievements.Got + " of " + Achievements.Total + "     H: how to play");
    }

    static void DarkenScreen(float amount)
    {
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, amount);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;
    }
}
