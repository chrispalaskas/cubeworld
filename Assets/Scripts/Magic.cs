using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Magic, and some handy gadgets. Hold one and left click to use it.
//
//   Sparkle Wand:  shoots sparkles that zap monsters
//   Ice Wand:      freezes water into ice (and lava into obsidian!) where you point
//   Growing Wand:  grows a tree on grass, ripens wheat, and makes flowers pop up
//   Floating Wand: makes a creature float up into the air - or, pointed at nothing, you!
//   Paint Bucket:  changes the colour of wool and coloured glass
//   Builder's Wand (creative): click two corners to copy everything between
//                  them, then press E to paste a copy wherever you look
//   Camera:        takes a photo! Hang it on a wall with a photo frame.
//
// Potions: fill a cauldron with water (E while holding water), then press E
// with something in your hand to brew:
//   a feather -> Speed,  an apple -> Jumping,  glass -> Invisibility (monsters
//   can't see you),  a fish -> Water Breathing.
// Hold a potion and press E to drink it. Each one lasts a minute.
//
// Enchanting table: press E holding a sword, tool or bow (and 3 gold, in
// survival) and it becomes magic - stronger, faster, and sparkly.
//
// Coins: earned from quests, mini-games and selling things to villagers.
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    const byte SparkleWand = 244, IceWand = 245, GrowWand = 246, FloatWand = 247;
    const byte PaintBucket = 248, BuilderWand = 249;
    const byte SpeedPotion = 251, JumpPotion = 252, InvisibilityPotion = 253, BreathingPotion = 254;
    const byte CameraItem = 255;

    static bool IsWand(byte item) { return item >= SparkleWand && item <= FloatWand; }
    static bool IsPotion(byte item) { return item >= SpeedPotion && item <= BreathingPotion; }

    // Things you left click to use. True if it was one of those.
    bool UseMagic()
    {
        byte item = selected;
        if (IsWand(item)) { Wave(item); return true; }
        if (item == PaintBucket) { Paint(); return true; }
        if (item == BuilderWand) { MarkCorner(); return true; }
        if (item == CameraItem) { StartCoroutine(TakePhoto()); return true; }
        return false;
    }

    // Things you press E with. True if it was one of those.
    bool UseMagicE()
    {
        byte item = selected;
        if (IsPotion(item))
        {
            if (!UseUp(item)) return true;
            Effects.Start(item);
            Sounds.Eat(transform.position);
            PortalTraveller.Announce("Glug glug! " + ItemNames[item] + " - it lasts a minute.");
            Achievements.Unlock("potion");
            return true;
        }
        if (item == BuilderWand) { Paste(); return true; }
        if (IsWand(item) || item == PaintBucket || item == CameraItem) return true;
        return false;
    }

    // --- Wands ---

    void Wave(byte wand)
    {
        squeezeTime = 0.2f;
        Sounds.Whoosh(transform.position);
        BlockWorld world = BlockWorld.Instance;
        Vector3 from = cam.transform.position;
        Vector3 way = cam.transform.forward;

        if (wand == SparkleWand)
        {
            Monster target = AnimalInSights(18f, true) as Monster;
            Vector3 end = target != null ? target.transform.position + Vector3.up * target.AimHeight : from + way * 12f;
            for (float d = 0f; d < Vector3.Distance(from, end); d += 0.8f) Sparkle(from + way * d, 1, new Color(1f, 0.85f, 1f));
            Sparkle(end, 12, new Color(1f, 0.7f, 1f));
            if (target != null) { target.Hit(Enchanted(wand) ? 8 : 5, transform.position); Sounds.Thwack(end); }
            return;
        }

        if (wand == FloatWand)
        {
            Monster target = AnimalInSights(14f, true) as Monster;
            if (target != null)
            {
                target.Float(4f);
                Sparkle(target.transform.position, 10, new Color(0.7f, 0.9f, 1f));
            }
            else
            {
                GetComponent<FirstPersonPlayer>().LaunchUp(12f);   // up you go!
                Sparkle(transform.position, 10, new Color(0.7f, 0.9f, 1f));
            }
            return;
        }

        // The other wands work on the block you point at
        Vector3Int spot = Vector3Int.zero;
        bool found = false;
        for (float d = 0f; d < 16f; d += 0.1f)
        {
            spot = Vector3Int.RoundToInt(from + way * d);
            byte type = world.GetBlock(spot.x, spot.y, spot.z);
            if (type != BlockWorld.Air) { found = true; break; }
        }
        if (!found) return;

        if (wand == IceWand)
        {
            int frozen = 0;
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -2; dy <= 1; dy++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        if (dx * dx + dz * dz > 10) continue;
                        Vector3Int c = spot + new Vector3Int(dx, dy, dz);
                        byte type = world.GetBlock(c.x, c.y, c.z);
                        if (type == BlockWorld.Water) { world.SetBlock(c.x, c.y, c.z, BlockWorld.Ice); frozen++; }
                        else if (type == BlockWorld.Lava) { world.SetBlock(c.x, c.y, c.z, BlockWorld.Obsidian); frozen++; }
                        else if (type == BlockWorld.Fire) world.SetBlock(c.x, c.y, c.z, BlockWorld.Air);
                    }
            Sparkle(spot, 14, new Color(0.7f, 0.95f, 1f));
            PortalTraveller.Announce(frozen > 0 ? "Brrr! Frozen solid." : "Point the Ice Wand at water or lava.");
            return;
        }

        if (wand == GrowWand)
        {
            byte type = world.GetBlock(spot.x, spot.y, spot.z);
            Sparkle(spot + Vector3.up * 0.6f, 14, new Color(0.5f, 1f, 0.5f));
            if (BlockWorld.IsWheat(type)) { world.SetBlock(spot.x, spot.y, spot.z, BlockWorld.WheatRipe); return; }
            if (type == BlockWorld.Grass || type == BlockWorld.Dirt || type == BlockWorld.CandyGrass)
            {
                if (Random.value < 0.4f && world.GrowTreeAt(spot.x, spot.y + 1, spot.z)) return;
                // ...or a little patch of flowers
                for (int i = 0; i < 6; i++)
                {
                    int fx = spot.x + Random.Range(-2, 3), fz = spot.z + Random.Range(-2, 3);
                    int top = world.GetSurfaceHeight(fx, fz);
                    byte ground = world.GetBlock(fx, top, fz);
                    if ((ground == BlockWorld.Grass || ground == BlockWorld.CandyGrass) && world.GetBlock(fx, top + 1, fz) == BlockWorld.Air)
                        world.SetBlock(fx, top + 1, fz, (byte)(BlockWorld.FlowerFirst + Random.Range(0, 3)));
                }
            }
        }
    }

    // Glowing specks drifting up
    static void Sparkle(Vector3 at, int count, Color colour)
    {
        Material look = BlockWorld.MakeMaterial(colour);
        look.EnableKeyword("_EMISSION");
        look.SetColor("_EmissionColor", colour);
        for (int i = 0; i < count; i++)
        {
            GameObject speck = new GameObject("Sparkle");
            speck.transform.position = at + Random.insideUnitSphere * 0.5f;
            speck.transform.localScale = Vector3.one * Random.Range(0.05f, 0.11f);
            speck.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            speck.AddComponent<MeshRenderer>().sharedMaterial = look;
            speck.AddComponent<FloatUp>();
            Destroy(speck, 1.2f);
        }
        Destroy(look, 1.3f);
    }

    // --- The paint bucket ---

    void Paint()
    {
        Vector3Int block, unused;
        if (!LookingAtBlock(out block, out unused))
        {
            Vector3Int soft;
            if (!LookingAtLavaOrFire(out soft)) return;
            block = soft;
        }
        BlockWorld world = BlockWorld.Instance;
        byte type = world.GetBlock(block.x, block.y, block.z);
        byte painted;
        if (BlockWorld.IsWool(type)) painted = (byte)(BlockWorld.WoolFirst + (type - BlockWorld.WoolFirst + 1) % BlockWorld.WoolColours);
        else if (BlockWorld.IsStainedGlass(type)) painted = (byte)(BlockWorld.StainedFirst + (type - BlockWorld.StainedFirst + 1) % BlockWorld.StainedColours);
        else if (type == BlockWorld.Glass) painted = BlockWorld.StainedFirst;
        else { PortalTraveller.Announce("The paint bucket works on wool and glass."); return; }

        world.SetBlock(block.x, block.y, block.z, painted);
        Sparkle(block, 6, Color.white);
        Sounds.Splash(block);
    }

    // --- The builder's wand: copy and paste (creative) ---

    private static Vector3Int? firstCorner;
    private static byte[,,] clipboard;

    void MarkCorner()
    {
        if (Survival) { PortalTraveller.Announce("The Builder's Wand only works in creative worlds."); return; }
        Vector3Int block, unused;
        if (!LookingAtBlock(out block, out unused)) return;

        if (firstCorner == null)
        {
            firstCorner = block;
            PortalTraveller.Announce("First corner marked! Now click the opposite corner.");
            Sparkle(block, 8, Color.yellow);
            return;
        }

        Vector3Int a = firstCorner.Value, b = block;
        firstCorner = null;
        Vector3Int low = Vector3Int.Min(a, b), high = Vector3Int.Max(a, b);
        Vector3Int size = high - low + Vector3Int.one;
        if (size.x > 24 || size.y > 24 || size.z > 24)
        {
            PortalTraveller.Announce("That's too big to copy - 24 blocks each way at most.");
            return;
        }

        clipboard = new byte[size.x, size.y, size.z];
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                    clipboard[x, y, z] = BlockWorld.Instance.GetBlock(low.x + x, low.y + y, low.z + z);
        Sparkle(block, 8, Color.yellow);
        PortalTraveller.Announce("Copied " + size.x + " x " + size.y + " x " + size.z + "! Press E to paste it where you look.");
    }

    void Paste()
    {
        if (Survival || clipboard == null) { PortalTraveller.Announce("Copy something first: click two corners with the Builder's Wand."); return; }
        Vector3Int unused, at;
        if (!LookingAtBlock(out unused, out at)) return;

        BlockWorld world = BlockWorld.Instance;
        for (int x = 0; x < clipboard.GetLength(0); x++)
            for (int y = 0; y < clipboard.GetLength(1); y++)
                for (int z = 0; z < clipboard.GetLength(2); z++)
                {
                    byte type = clipboard[x, y, z];
                    if (type == BlockWorld.Air) continue;   // don't wipe out what's already there
                    world.SetBlock(at.x + x, at.y + y, at.z + z, type);
                }
        Sounds.Place(BlockWorld.Stone, at);
        PortalTraveller.Announce("Pasted!");
    }

    // --- The camera ---

    IEnumerator TakePhoto()
    {
        bool hadHand = hand.activeSelf;
        hand.SetActive(false);
        hiddenForPhoto = true;
        yield return new WaitForEndOfFrame();

        Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
        hiddenForPhoto = false;
        hand.SetActive(hadHand);

        // Shrink it down to a small picture
        const int w = 256, h = 160;
        Texture2D photo = new Texture2D(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                photo.SetPixel(x, y, screen.GetPixelBilinear((float)x / w, (float)y / h));
        photo.Apply();
        Destroy(screen);

        string key = "world" + (WorldMenu.CurrentSlot + 1) + "-photos";
        int number = PlayerPrefs.GetInt(key, 0) + 1;
        PlayerPrefs.SetInt(key, number);
        BlockWorld.SavePhoto(number, photo);

        flashUntil = Time.time + 0.2f;
        Sounds.Pop(transform.position);
        Achievements.Unlock("photo");
        PortalTraveller.Announce("Click! Photo " + number + " taken. Put down a photo frame to hang it on the wall.");
    }

    private bool hiddenForPhoto;
    private float flashUntil;

    // A new photo frame shows your latest photo
    void HangLatestPhoto(Vector3Int frame)
    {
        int latest = PlayerPrefs.GetInt("world" + (WorldMenu.CurrentSlot + 1) + "-photos", 0);
        BlockWorld.Instance.HangPhoto(frame, latest, FacingMe(0));
        if (latest == 0) PortalTraveller.Announce("It's empty - take a photo with the camera first, then put up another frame.");
    }

    // --- Cauldrons and the enchanting table ---

    bool LookingAtMagicBlock(out Vector3Int cell, out byte type)
    {
        Vector3Int unused;
        type = 0;
        if (!LookingAtBlock(out cell, out unused)) return false;
        type = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
        return type == BlockWorld.Cauldron || type == BlockWorld.EnchantTable;
    }

    void UseMagicBlock(Vector3Int cell, byte type)
    {
        BlockWorld world = BlockWorld.Instance;
        byte item = selected;

        if (type == BlockWorld.Cauldron)
        {
            if (!world.CauldronFilled(cell))
            {
                if (item != BlockWorld.Water) { PortalTraveller.Announce("The cauldron's empty - fill it with water first (E while holding water)."); return; }
                UseUp(item);
                world.FillCauldron(cell, true);
                Sounds.Splash(cell);
                PortalTraveller.Announce("Splash! Now add a feather, an apple, some glass or a fish.");
                return;
            }

            byte potion = item == BlockWorld.Feather ? SpeedPotion : item == Apple ? JumpPotion
                        : item == BlockWorld.Glass ? InvisibilityPotion : item == BlockWorld.RawFish || item == BlockWorld.CookedFish ? BreathingPotion : (byte)0;
            if (potion == 0) { PortalTraveller.Announce("Bubble bubble... add a feather, an apple, some glass or a fish."); return; }
            UseUp(item);
            world.FillCauldron(cell, false);
            if (Survival) Give(potion); else PutInHotbar(potion);
            Sparkle(cell + Vector3.up * 0.5f, 16, new Color(0.8f, 0.4f, 1f));
            Sounds.Pop(cell);
            PortalTraveller.Announce("You brewed a " + ItemNames[potion] + "!");
            return;
        }

        // The enchanting table
        if (!IsSword(item) && !IsDiggingTool(item) && item != BlockWorld.Bow && !IsWand(item))
        {
            PortalTraveller.Announce("Hold a sword, a tool, a bow or a wand to enchant it" + (Survival ? " (it costs 3 gold)." : "."));
            return;
        }
        if (Enchanted(item)) { PortalTraveller.Announce("That's already enchanted!"); return; }
        if (Survival && !UseUp(BlockWorld.Gold, 3)) { PortalTraveller.Announce("You need 3 gold to enchant something."); return; }

        Enchant(item);
        Sparkle(cell + Vector3.up * 0.6f, 24, new Color(0.75f, 0.45f, 1f));
        Sounds.Twang(cell);
        Achievements.Unlock("enchant");
        PortalTraveller.Announce("Your " + ItemNames[item] + " is enchanted! It's stronger and faster now.");
    }

    // Which things you've enchanted, in this world
    static string EnchantKey { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-enchanted"; } }

    public static bool Enchanted(byte item)
    {
        return ("," + PlayerPrefs.GetString(EnchantKey, "") + ",").Contains("," + item + ",");
    }

    static void Enchant(byte item)
    {
        string now = PlayerPrefs.GetString(EnchantKey, "");
        PlayerPrefs.SetString(EnchantKey, now == "" ? item.ToString() : now + "," + item);
    }

    // --- Things drawn on the screen: potion timers, coins, the camera flash ---

    void DrawMagicHud()
    {
        float y = 136f;
        foreach (byte potion in new[] { SpeedPotion, JumpPotion, InvisibilityPotion, BreathingPotion })
        {
            float left = Effects.Left(potion);
            if (left <= 0f) continue;
            GUI.Label(new Rect(12, y, 400, 24), ItemNames[potion] + ": " + Mathf.CeilToInt(left) + "s");
            y += 20f;
        }

        if (!InventoryOpen && BlockWorld.Instance != null)
            GUI.Label(new Rect(Screen.width - 160f, 180f, 150f, 24f), "Coins: " + Wallet.Coins);

        if (Time.time < flashUntil)
        {
            Color before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;
        }
    }

    // --- Pictures ---

    private static readonly Dictionary<byte, Texture2D> magicPictures = new Dictionary<byte, Texture2D>();

    static Texture2D MagicPicture(byte item)
    {
        Texture2D picture;
        if (magicPictures.TryGetValue(item, out picture)) return picture;

        string[] rows = null;
        Color special = Color.white;
        if (IsWand(item))
        {
            special = item == SparkleWand ? new Color(1f, 0.5f, 0.9f) : item == IceWand ? new Color(0.6f, 0.9f, 1f)
                    : item == GrowWand ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.85f, 0.85f, 1f);
            rows = new[] { "......xx", ".....xxx", "......x.", ".....b..", "....b...", "...b....", "..b.....", ".b......" };
        }
        else if (IsPotion(item))
        {
            special = item == SpeedPotion ? new Color(0.4f, 0.8f, 1f) : item == JumpPotion ? new Color(0.5f, 1f, 0.4f)
                    : item == InvisibilityPotion ? new Color(0.85f, 0.85f, 0.95f) : new Color(0.2f, 0.4f, 0.95f);
            rows = new[] { "...bb...", "...ww...", "...ww...", "..wxxw..", ".wxxxxw.", ".wxwxxw.", ".wxxxxw.", "..wwww.." };
        }
        else switch (item)
        {
            case PaintBucket:
                rows = new[] { "........", ".kkkkkk.", "kryybgk.", "kssssssk", ".ssssss.", ".ssssss.", "..ssss..", "........" };
                break;
            case BuilderWand:
                special = new Color(1f, 0.85f, 0.2f);
                rows = new[] { "x.x...xx", ".x...xxx", "x.x...x.", ".....b..", "....b...", "...b....", "..b.....", ".b......" };
                break;
            case CameraItem:
                rows = new[] { "........", "..kk....", "kkkkkkkk", "kkkwwkkk", "kkwkkwkk", "kkkwwkkk", "kkkkkkkk", "........" };
                break;
            case BlockWorld.Lantern:
                rows = new[] { "...kk...", "..kkkk..", "..kyyk..", "..yooy..", "..yooy..", "..kyyk..", "..kkkk..", "........" };
                break;
            case BlockWorld.FlowerFirst: case BlockWorld.FlowerFirst + 1: case BlockWorld.FlowerFirst + 2:
                special = BlockWorld.Instance != null ? BlockWorld.Instance.MaterialFor(item).color : Color.red;
                rows = new[] { "...x....", "..xyx...", "...x....", "...g....", "...g.g..", "..gggg..", "...g....", "...g...." };
                break;
            case BlockWorld.FlowerPot:
                rows = new[] { "...r....", "..ryr...", "...r....", "...g....", ".oooooo.", ".oooooo.", "..oooo..", "..oooo.." };
                break;
            case BlockWorld.Cauldron:
                special = new Color(0.3f, 0.3f, 0.33f);
                rows = new[] { "........", "x......x", "x......x", "xxxxxxxx", "xxxxxxxx", ".xxxxxx.", ".x....x.", "........" };
                break;
            case BlockWorld.EnchantTable:
                rows = new[] { "........", "..wwww..", "..rwwr..", "rrrrrrrr", "kkkkkkkk", "kkkkkkkk", "kkkkkkkk", "kkkkkkkk" };
                break;
            case BlockWorld.PhotoFrame:
                special = new Color(0.5f, 0.75f, 1f);
                rows = new[] { "bbbbbbbb", "bxxxxyxb", "bxxxxxxb", "bxwwxxxb", "bgggxggb", "bggggggb", "bbbbbbbb", "........" };
                break;
        }
        if (rows == null) return null;
        picture = PixelArt(rows, special);
        magicPictures[item] = picture;
        return picture;
    }
}

// Potion effects - each lasts a minute
public static class Effects
{
    const float Seconds = 60f;
    private static readonly Dictionary<byte, float> endsAt = new Dictionary<byte, float>();

    public static void Start(byte potion) { endsAt[potion] = Time.time + Seconds; }
    public static void StartFor(byte potion, float seconds) { endsAt[potion] = Mathf.Max(Time.time + seconds, Time.time + Left(potion)); }

    public static float Left(byte potion)
    {
        float end;
        return endsAt.TryGetValue(potion, out end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }

    public static bool Fast { get { return Left(251) > 0f; } }
    public static bool Springy { get { return Left(252) > 0f; } }
    public static bool Invisible { get { return Left(253) > 0f; } }
    public static bool Gills { get { return Left(254) > 0f; } }

    public static void Clear() { endsAt.Clear(); }
}

// Coins, for each world
public static class Wallet
{
    static string Key { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-coins"; } }

    public static int Coins { get { return PlayerPrefs.GetInt(Key, 0); } }

    public static void Earn(int how, string why)
    {
        PlayerPrefs.SetInt(Key, Coins + how);
        if (Camera.main != null) Sounds.Pop(Camera.main.transform.position);
        if (why != "") PortalTraveller.Announce(why + " +" + how + " coin" + (how == 1 ? "" : "s") + "!");
        if (Coins >= 50) Achievements.Unlock("rich");
    }

    public static bool Spend(int how)
    {
        if (Coins < how) return false;
        PlayerPrefs.SetInt(Key, Coins - how);
        return true;
    }
}
