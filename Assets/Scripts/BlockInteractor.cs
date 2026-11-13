using UnityEngine;
using UnityEngine.Rendering;

// Breaking blocks, placing blocks, and choosing which block to place.
// The crafting table screen lives in CraftingTable.cs, and swords in Swords.cs.
public partial class BlockInteractor : MonoBehaviour
{
    [Tooltip("How far away you can reach, in blocks")]
    public float reach = 6f;

    // Not a block you can place - a tool that sits in your hand and mops up water
    const byte Sponge = 9;

    // Also a tool: click an animal to catch it, and click again to let it go
    const byte Leash = 10;
    const float LeashReach = 8f;

    // Not a block either - place it and a chicken hatches out
    const byte ChickenEgg = 24;

    // And this one hatches a little person
    const byte VillagerEgg = 48;

    // More things you place that aren't blocks: a sapling grows into a tree,
    // and a firework shoots up into the sky
    const byte Sapling = 28;
    const byte FireworkRocket = 29;

    // What the hotbar holds when you first play, in the same order as the
    // number keys: 1 to 9, then 0. The inventory lets you change it.
    static readonly byte[] StartingHotbar =
    {
        BlockWorld.Grass, BlockWorld.Dirt, BlockWorld.Stone, BlockWorld.Wood, BlockWorld.Sand,
        BlockWorld.Leaves, BlockWorld.Tnt, Sponge, BlockWorld.Water, Leash
    };
    static readonly string[] SlotKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

    // Everything in the inventory, in the order it's laid out
    static readonly byte[] AllItems =
    {
        BlockWorld.Grass, BlockWorld.Dirt, BlockWorld.Stone, BlockWorld.Cobblestone, BlockWorld.Wood,
        BlockWorld.Planks, BlockWorld.Sand, BlockWorld.Leaves, BlockWorld.Glass, BlockWorld.Snow,
        BlockWorld.Brick, BlockWorld.Iron, BlockWorld.Gold, BlockWorld.Diamond, BlockWorld.Obsidian,
        BlockWorld.Water, BlockWorld.Tnt, Sponge, Leash, BlockWorld.CraftingTable,
        BlockWorld.Lamp, BlockWorld.BouncePad, BlockWorld.MegaTnt, ChickenEgg, BlockWorld.Ice,
        BlockWorld.Rainbow, BlockWorld.Bookshelf, Sapling, FireworkRocket, WoodenSword,
        StoneSword, IronSword, GoldSword, DiamondSword, BlockWorld.StairsNorth, BlockWorld.DoorItem,
        VillagerEgg, BlockWorld.Portal, BlockWorld.MagicPortal, BlockWorld.MagicGrass, BlockWorld.Crystal,
        BlockWorld.Cloud
    };
    const int InventoryColumns = 10;

    // Names, looked up by item number
    static readonly string[] ItemNames =
    {
        "", "Grass", "Dirt", "Stone", "Wood", "Sand", "Leaves", "Water", "TNT", "Sponge", "Leash",
        "Glass", "Diamond", "Gold", "Iron", "Brick", "Cobblestone", "Planks", "Snow", "Obsidian",
        "Crafting Table", "Lamp", "Bounce Pad", "Mega TNT", "Chicken Egg",
        "Ice", "Rainbow Block", "Bookshelf", "Sapling", "Firework",
        "Wooden Sword", "Stone Sword", "Iron Sword", "Gold Sword", "Diamond Sword",
        "Stairs", "", "", "",   // (the other three are the same stairs facing other ways)
        "Door",
        "", "", "", "", "", "", "", "",   // (40 to 47 are the halves of a door, once it's placed)
        "Villager Egg", "Portal", "Magic Portal", "Magic Grass", "Crystal", "Cloud"
    };

    // While the inventory is open, other scripts leave the keys and mouse alone.
    // The frame it closes on counts too, so the key that closed it isn't reused.
    public static bool InventoryOpen { get; private set; }
    public static int InventoryClosedFrame { get; private set; }

    private byte[] hotbar = (byte[])StartingHotbar.Clone();
    private static int slot;      // which hotbar slot is picked (kept when you go through a magic portal)
    private int inventoryCursor;  // which inventory item the keyboard is pointing at
    private GUIStyle centredLabel;
    private Vector2 lastMouse;    // to tell when the mouse moves over the inventory

    byte selected { get { return hotbar[slot]; } }
    private GameObject hand;
    private GameObject spongeInHand;
    private GameObject leashInHand;
    private GameObject rope;
    private MonoBehaviour caughtAnimal;   // destroyed if TNT gets it, which Unity treats as null
    private float squeezeTime;   // counts down while the sponge dips after a wipe
    private Camera cam;
    private CharacterController body;

    void Start()
    {
        cam = Camera.main;
        body = GetComponent<CharacterController>();
        BuildHand();
        LoadCrafted();
        LoadHotbar();
    }

    void Update()
    {
        ChooseBlock();
        MoveHand();

        if (InventoryOpen)
        {
            if (crafting) CraftingKeys();
            else InventoryKeys();
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Time.frameCount == FirstPersonPlayer.MouseLockedFrame) return;   // that click was just to grab the mouse

        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            OpenInventory();
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q))
        {
            if (selected == Sponge)
                WipeWater();
            else if (selected == Leash)
                UseLeash();
            else if (IsSword(selected))
                Swing();
            else
                Break();
        }
        else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E))
        {
            Vector3Int door;
            if (LookingAtCraftingTable())
                OpenCrafting();
            else if (LookingAtDoor(out door))
                OpenOrCloseDoor(door);
            else
                Place();
        }
    }

    void ChooseBlock()
    {
        // Number keys jump straight to a slot...
        for (int i = 0; i < hotbar.Length; i++)
        {
            KeyCode key = i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0;
            if (Input.GetKeyDown(key)) slot = i;
        }

        // ...and the mouse wheel steps along, wrapping round at the ends
        float scroll = Input.mouseScrollDelta.y;
        if (scroll < 0f) slot = (slot + 1) % hotbar.Length;
        if (scroll > 0f) slot = (slot + hotbar.Length - 1) % hotbar.Length;
    }

    // Work out which block we are looking at, and which empty space sits
    // against the face we are pointing at (that is where a new block goes).
    bool LookingAtBlock(out Vector3Int block, out Vector3Int spaceInFront)
    {
        block = Vector3Int.zero;
        spaceInFront = Vector3Int.zero;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, reach)) return false;
        if (BlockWorld.Instance == null) return false;
        if (hit.transform.parent != BlockWorld.Instance.transform) return false;

        // Every cube sits exactly on a whole number, so its position IS its grid cell
        Vector3 p = hit.transform.position;
        block = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
        spaceInFront = block + Vector3Int.RoundToInt(hit.normal);
        return true;
    }

    void Break()
    {
        Vector3Int block, unused;
        if (!LookingAtBlock(out block, out unused)) return;

        // Break one half of a door and the other half goes too
        Vector3Int otherHalf;
        if (OtherHalfOfDoor(block, out otherHalf))
            BlockWorld.Instance.SetBlock(otherHalf.x, otherHalf.y, otherHalf.z, BlockWorld.Air);

        BlockWorld.Instance.SetBlock(block.x, block.y, block.z, BlockWorld.Air);
    }

    // Water has no collider for a raycast to hit, so instead we creep along the
    // line of sight checking each block, until we find water or something solid.
    bool LookingAtWater(out Vector3Int water)
    {
        for (float distance = 0f; distance <= reach; distance += 0.05f)
        {
            Vector3 point = cam.transform.position + cam.transform.forward * distance;
            water = Vector3Int.RoundToInt(point);

            byte type = BlockWorld.Instance.GetBlock(water.x, water.y, water.z);
            if (type == BlockWorld.Water) return true;
            if (type != BlockWorld.Air) return false;
        }

        water = Vector3Int.zero;
        return false;
    }

    // Water in your hand has to stack on water, which a raycast would go straight
    // through - so creep along the line of sight, remembering the last empty
    // block we passed. That is the space in front of whatever we reach.
    bool LookingAtSpaceBeforeWater(out Vector3Int space)
    {
        space = Vector3Int.zero;
        bool passedEmptySpace = false;

        for (float distance = 0f; distance <= reach; distance += 0.05f)
        {
            Vector3 point = cam.transform.position + cam.transform.forward * distance;
            Vector3Int cell = Vector3Int.RoundToInt(point);

            if (BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.Air)
            {
                space = cell;
                passedEmptySpace = true;
            }
            else if (passedEmptySpace)
            {
                return true;
            }
        }

        return false;
    }

    void WipeWater()
    {
        squeezeTime = 0.15f;

        Vector3Int water;
        if (LookingAtWater(out water))
            BlockWorld.Instance.SetBlock(water.x, water.y, water.z, BlockWorld.Air);
    }

    void Place()
    {
        if (selected == Sponge || selected == Leash || IsSword(selected)) return;

        Vector3Int unused, target;
        if (selected == BlockWorld.Water)
        {
            if (!LookingAtSpaceBeforeWater(out target)) return;
        }
        else if (!LookingAtBlock(out unused, out target)) return;

        byte there = BlockWorld.Instance.GetBlock(target.x, target.y, target.z);
        if (there != BlockWorld.Air && there != BlockWorld.Water) return;   // you can build into water

        // An egg isn't a block - a chicken hatches out, standing in that space
        if (selected == ChickenEgg)
        {
            ChickenRespawner.Hatch(new Vector3(target.x, target.y - 0.5f, target.z));
            return;
        }

        if (selected == VillagerEgg)
        {
            Villager.Hatch(BlockWorld.Instance, new Vector3(target.x, target.y - 0.5f, target.z));
            return;
        }

        // A sapling shoots straight up into a full-grown tree, if there's room
        if (selected == Sapling)
        {
            BlockWorld.Instance.GrowTreeAt(target.x, target.y, target.z);
            return;
        }

        if (selected == FireworkRocket)
        {
            Firework.Launch(target);
            return;
        }

        if (WouldHitPlayer(target)) return;

        if (selected == BlockWorld.DoorItem)
        {
            PlaceDoor(target);
            return;
        }

        // Stairs go up the way you're facing, so you can walk straight up them
        byte block = selected == BlockWorld.StairsNorth ? StairsFacingAway() : selected;
        BlockWorld.Instance.SetBlock(target.x, target.y, target.z, block);

        if (selected == BlockWorld.Tnt)
            Tnt.Ignite(BlockWorld.Instance, target, Tnt.FuseSeconds);
        else if (selected == BlockWorld.MegaTnt)
            Tnt.Ignite(BlockWorld.Instance, target, Tnt.MegaFuseSeconds);
    }

    // --- Stairs and doors ---

    byte StairsFacingAway()
    {
        Vector3 f = transform.forward;
        if (Mathf.Abs(f.z) >= Mathf.Abs(f.x))
            return f.z > 0f ? BlockWorld.StairsNorth : BlockWorld.StairsSouth;
        return f.x > 0f ? BlockWorld.StairsEast : BlockWorld.StairsWest;
    }

    // A door needs two empty blocks, one on top of the other
    void PlaceDoor(Vector3Int bottom)
    {
        Vector3Int top = bottom + Vector3Int.up;
        byte above = BlockWorld.Instance.GetBlock(top.x, top.y, top.z);
        if (above != BlockWorld.Air && above != BlockWorld.Water) return;
        if (top.y >= BlockWorld.SizeY || WouldHitPlayer(top)) return;

        // It goes across the way you're looking, so you walk straight through it
        Vector3 f = transform.forward;
        bool alongX = Mathf.Abs(f.z) >= Mathf.Abs(f.x);

        BlockWorld.Instance.SetBlock(bottom.x, bottom.y, bottom.z, BlockWorld.MakeDoor(false, false, alongX));
        BlockWorld.Instance.SetBlock(top.x, top.y, top.z, BlockWorld.MakeDoor(true, false, alongX));
    }

    bool LookingAtDoor(out Vector3Int door)
    {
        Vector3Int unused;
        return LookingAtBlock(out door, out unused)
            && BlockWorld.IsDoor(BlockWorld.Instance.GetBlock(door.x, door.y, door.z));
    }

    // The half of a door above or below this one, if this is a door
    bool OtherHalfOfDoor(Vector3Int half, out Vector3Int other)
    {
        byte type = BlockWorld.Instance.GetBlock(half.x, half.y, half.z);
        other = half + (BlockWorld.DoorIsTop(type) ? Vector3Int.down : Vector3Int.up);
        return BlockWorld.IsDoor(type)
            && BlockWorld.IsDoor(BlockWorld.Instance.GetBlock(other.x, other.y, other.z));
    }

    void OpenOrCloseDoor(Vector3Int half)
    {
        Vector3Int other;
        bool hasOther = OtherHalfOfDoor(half, out other);

        foreach (Vector3Int cell in hasOther ? new[] { half, other } : new[] { half })
        {
            byte door = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
            byte flipped = BlockWorld.MakeDoor(BlockWorld.DoorIsTop(door), !BlockWorld.DoorIsOpen(door), BlockWorld.DoorAlongX(door));
            BlockWorld.Instance.SetBlock(cell.x, cell.y, cell.z, flipped);
        }
    }

    // Don't let us build a block inside our own body and get stuck
    bool WouldHitPlayer(Vector3Int cell)
    {
        Bounds me = new Bounds(
            transform.position + body.center,
            new Vector3(body.radius * 2f, body.height, body.radius * 2f));

        Bounds newBlock = new Bounds(cell, Vector3.one);
        return me.Intersects(newBlock);
    }

    // --- The inventory ---

    void OpenInventory()
    {
        InventoryOpen = true;
        Cursor.lockState = CursorLockMode.None;   // free the mouse so you can click items
        Cursor.visible = true;
    }

    void CloseInventory()
    {
        InventoryOpen = false;
        crafting = false;
        InventoryClosedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void InventoryKeys()
    {
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
        {
            CloseInventory();
            return;
        }

        // Arrows move around the grid; Enter or Space puts that item in your hand
        int count = AllItems.Length;
        if (Input.GetKeyDown(KeyCode.RightArrow)) inventoryCursor = (inventoryCursor + 1) % count;
        if (Input.GetKeyDown(KeyCode.LeftArrow))  inventoryCursor = (inventoryCursor + count - 1) % count;
        if (Input.GetKeyDown(KeyCode.DownArrow) && inventoryCursor + InventoryColumns < count) inventoryCursor += InventoryColumns;
        if (Input.GetKeyDown(KeyCode.UpArrow) && inventoryCursor - InventoryColumns >= 0) inventoryCursor -= InventoryColumns;

        if (EnterPressed())
            PutInHotbar(AllItems[inventoryCursor]);
    }

    // Enter or Space means "yes, this one"
    static bool EnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
    }

    // Puts an item into the picked hotbar slot. If it was already somewhere
    // else on the hotbar, the two slots just swap - so nothing goes missing.
    void PutInHotbar(byte item)
    {
        if (!HaveGot(item)) return;   // not made at a crafting table yet

        int alreadyAt = System.Array.IndexOf(hotbar, item);
        if (alreadyAt >= 0) hotbar[alreadyAt] = hotbar[slot];
        hotbar[slot] = item;
        SaveHotbar();
    }

    // The hotbar is remembered between games, in every world
    void SaveHotbar()
    {
        PlayerPrefs.SetString("hotbar", string.Join(",", hotbar));
        PlayerPrefs.Save();
    }

    void LoadHotbar()
    {
        string[] saved = PlayerPrefs.GetString("hotbar", "").Split(',');
        if (saved.Length != hotbar.Length) return;

        byte[] loaded = new byte[hotbar.Length];
        for (int i = 0; i < saved.Length; i++)
        {
            // Anything unexpected, and we keep the starting hotbar instead
            if (!byte.TryParse(saved[i], out loaded[i])) return;
            if (System.Array.IndexOf(AllItems, loaded[i]) < 0 || !HaveGot(loaded[i])) return;
        }
        hotbar = loaded;
    }

    // --- The leash ---

    void UseLeash()
    {
        squeezeTime = 0.15f;

        if (caughtAnimal != null)
        {
            ((ILeashable)caughtAnimal).Release();
            caughtAnimal = null;
            return;
        }

        caughtAnimal = AnimalInSights(LeashReach);
        if (caughtAnimal != null)
            ((ILeashable)caughtAnimal).Catch(transform);
    }

    // Animals have no colliders for a raycast to hit, so we check each one
    // to see how close it is to the line we're looking along
    MonoBehaviour AnimalInSights(float reach)
    {
        MonoBehaviour closest = null;
        float closestDistance = reach;

        foreach (Chicken chicken in FindObjectsByType<Chicken>(FindObjectsSortMode.None))
            CheckAnimal(chicken, ref closest, ref closestDistance);

        foreach (Squirrel squirrel in FindObjectsByType<Squirrel>(FindObjectsSortMode.None))
            CheckAnimal(squirrel, ref closest, ref closestDistance);

        foreach (Villager person in FindObjectsByType<Villager>(FindObjectsSortMode.None))
            CheckAnimal(person, ref closest, ref closestDistance);

        return closest;
    }

    // How high above its feet to aim at - people are taller than animals
    static float MiddleHeight(MonoBehaviour animal)
    {
        return animal is Villager ? 0.6f : 0.25f;
    }

    void CheckAnimal(MonoBehaviour animal, ref MonoBehaviour closest, ref float closestDistance)
    {
        Vector3 toAnimal = animal.transform.position + Vector3.up * MiddleHeight(animal) - cam.transform.position;
        float along = Vector3.Dot(toAnimal, cam.transform.forward);

        if (along < 0f || along > closestDistance) return;                               // behind us or too far
        if ((toAnimal - cam.transform.forward * along).magnitude > 0.6f) return;         // not lined up
        if (WallInTheWay(along)) return;

        closest = animal;
        closestDistance = along;
    }

    // You can catch something through water or leaves, but not through a wall
    bool WallInTheWay(float distance)
    {
        for (float d = 0f; d < distance; d += 0.05f)
        {
            Vector3Int cell = Vector3Int.RoundToInt(cam.transform.position + cam.transform.forward * d);
            byte type = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);

            if (type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Leaves)
                return true;
        }
        return false;
    }

    // The rope is one long thin cube, stretched from your hand to the animal
    void LateUpdate()
    {
        rope.SetActive(caughtAnimal != null);
        if (caughtAnimal == null) return;

        Vector3 from = cam.transform.position + cam.transform.right * 0.35f
                     - cam.transform.up * 0.30f + cam.transform.forward * 0.6f;
        Vector3 to = caughtAnimal.transform.position + Vector3.up * MiddleHeight(caughtAnimal);

        rope.transform.position = (from + to) / 2f;
        rope.transform.rotation = Quaternion.LookRotation(to - from);
        rope.transform.localScale = new Vector3(0.03f, 0.03f, Vector3.Distance(from, to));
    }

    // The hand lives on the camera and the rope on its own, so neither goes
    // away with the player unless we clear them up when leaving a world
    void OnDestroy()
    {
        InventoryOpen = false;
        if (hand != null) Destroy(hand);
        if (rope != null) Destroy(rope);
    }

    // --- The tools in your hand ---

    void BuildHand()
    {
        // Children of the camera, so they stay in the corner of your view
        hand = new GameObject("Hand");
        hand.transform.SetParent(cam.transform, false);

        Material ropeColour = BlockWorld.MakeMaterial(new Color(0.55f, 0.38f, 0.20f));

        spongeInHand = AddHandPart(new Vector3(0.45f, -0.30f, 0.75f), new Vector3(0.32f, 0.20f, 0.26f), MakeSpongeMaterial());
        leashInHand = AddHandPart(new Vector3(0.45f, -0.33f, 0.75f), new Vector3(0.22f, 0.14f, 0.22f), ropeColour);   // a coil of rope
        AddHandPart(new Vector3(0.50f, -0.46f, 0.70f), new Vector3(0.20f, 0.14f, 0.40f),
                    BlockWorld.MakeMaterial(new Color(0.90f, 0.70f, 0.55f)));   // your hand, gripping it from below

        BuildSword();
        hand.SetActive(false);

        rope = AddHandPart(Vector3.zero, Vector3.one, ropeColour);
        rope.name = "Rope";
        rope.transform.SetParent(null);
    }

    GameObject AddHandPart(Vector3 position, Vector3 size, Material material)
    {
        GameObject part = new GameObject("Hand Part");
        part.transform.SetParent(hand.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;

        MeshRenderer look = part.AddComponent<MeshRenderer>();
        look.sharedMaterial = material;
        look.shadowCastingMode = ShadowCastingMode.Off;
        look.receiveShadows = false;
        return part;
    }

    // Yellow with darker holes dotted around, drawn pixel by pixel
    static Material MakeSpongeMaterial()
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;

        Color yellow = new Color(0.95f, 0.85f, 0.25f);
        Color hole = new Color(0.72f, 0.60f, 0.12f);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                texture.SetPixel(x, y, (x * 3 + y * 5) % 7 == 0 ? hole : yellow);
        texture.Apply();

        Material mat = BlockWorld.MakeMaterial(Color.white);
        mat.mainTexture = texture;
        return mat;
    }

    void MoveHand()
    {
        hand.SetActive(selected == Sponge || selected == Leash || IsSword(selected));
        spongeInHand.SetActive(selected == Sponge);
        leashInHand.SetActive(selected == Leash);
        MoveSword();

        squeezeTime = Mathf.Max(squeezeTime - Time.deltaTime, 0f);
        hand.transform.localPosition = squeezeTime > 0f ? Vector3.down * 0.06f : Vector3.zero;
    }

    // Crosshair in the middle of the screen, plus a reminder of the controls
    void OnGUI()
    {
        float dot = 4f;
        GUI.DrawTexture(
            new Rect(Screen.width / 2f - dot / 2f, Screen.height / 2f - dot / 2f, dot, dot),
            Texture2D.whiteTexture);

        GUI.Label(new Rect(12, 10, 800, 24), "WASD move   Arrows or mouse look   Space jump / swim up");
        GUI.Label(new Rect(12, 30, 800, 24), "Q or left click: break / wipe water / leash / swing sword   E or right click: place / open door   Esc frees mouse, Enter returns");
        GUI.Label(new Rect(12, 50, 800, 24), "1-9, 0 or mouse wheel: choose from the hotbar");

        GUI.Label(new Rect(12, 70, 800, 24), "I or Tab: inventory   E or right click on a crafting table: craft");

        if (InventoryOpen && crafting) DrawCrafting();
        else if (InventoryOpen) DrawInventory();
        DrawHotbar();
    }

    // Draws an item's picture: the block's own texture if it has one,
    // otherwise a square of its colour
    void DrawItemIcon(Rect icon, byte item)
    {
        Color before = GUI.color;
        Material look = BlockWorld.Instance.MaterialFor(item);   // tools and other things that aren't blocks have none

        if (item == Sponge)
        {
            GUI.color = new Color(0.95f, 0.85f, 0.25f);
            GUI.DrawTexture(icon, Texture2D.whiteTexture);
        }
        else if (item == Leash)
        {
            GUI.color = new Color(0.55f, 0.38f, 0.20f);
            GUI.DrawTexture(icon, Texture2D.whiteTexture);
        }
        else if (item == ChickenEgg || item == VillagerEgg)
        {
            GUI.DrawTexture(icon, EggPicture(item == VillagerEgg));
        }
        else if (item == Sapling || item == FireworkRocket)
        {
            GUI.DrawTexture(icon, item == Sapling ? SaplingPicture() : FireworkPicture());
        }
        else if (IsSword(item))
        {
            GUI.DrawTexture(icon, SwordPicture(item));
        }
        else if (item == BlockWorld.StairsNorth)
        {
            GUI.DrawTexture(icon, StairsPicture());
        }
        else if (look != null)
        {
            Color colour = look.color;
            colour.a = Mathf.Max(colour.a, 0.8f);   // keep water easy to see on the dark background
            GUI.color = colour;
            GUI.DrawTexture(icon, look.mainTexture != null ? look.mainTexture : Texture2D.whiteTexture);
        }

        GUI.color = before;
    }

    // A grid of every item. Clicking one (or Enter on the highlighted one)
    // puts it into the picked hotbar slot.
    void DrawInventory()
    {
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        const float gap = 6f;
        float size = Mathf.Min(56f, (Screen.width - 48f) / InventoryColumns - gap);
        int rows = (AllItems.Length + InventoryColumns - 1) / InventoryColumns;
        float width = InventoryColumns * (size + gap) - gap;
        float left = (Screen.width - width) / 2f;
        float top = Screen.height * 0.25f;

        GUI.Box(new Rect(left - 16, top - 60, width + 32, rows * (size + gap) + 110), "");
        GUI.Label(new Rect(left, top - 52, width, 24), "Inventory", centredLabel);
        GUI.Label(new Rect(left, top - 30, width, 24),
                  "Click an item, or use the arrows and Enter, to put it in hotbar slot " + SlotKeys[slot] + ".");

        Event e = Event.current;

        // When the mouse moves, the highlight follows it (until the arrows move it again)
        bool mouseMoved = e.type == EventType.Repaint && e.mousePosition != lastMouse;
        if (e.type == EventType.Repaint) lastMouse = e.mousePosition;

        for (int i = 0; i < AllItems.Length; i++)
        {
            Rect box = new Rect(left + (i % InventoryColumns) * (size + gap), top + (i / InventoryColumns) * (size + gap), size, size);

            if (mouseMoved && box.Contains(e.mousePosition)) inventoryCursor = i;

            if (e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
            {
                inventoryCursor = i;
                PutInHotbar(AllItems[i]);
                e.Use();
            }

            if (i == inventoryCursor)
            {
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), Texture2D.whiteTexture);
            }
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = before;

            // Things you haven't made yet are a mystery
            if (HaveGot(AllItems[i]))
                DrawItemIcon(new Rect(box.x + size * 0.15f, box.y + size * 0.15f, size * 0.7f, size * 0.7f), AllItems[i]);
            else
                GUI.Label(box, "?", centredLabel);
        }

        float under = top + rows * (size + gap) + 4;
        byte pointedAt = AllItems[inventoryCursor];
        GUI.Label(new Rect(left, under, width, 24),
                  HaveGot(pointedAt) ? ItemNames[pointedAt] : "??? - make it at a crafting table to find out", centredLabel);
        GUI.Label(new Rect(left, under + 22, width, 24), "1-9, 0 or click the hotbar: choose a slot     I, Tab or Esc: close");
    }

    // A row of slots along the bottom of the screen, with the picked one outlined
    void DrawHotbar()
    {
        if (centredLabel == null)
            centredLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        const float gap = 4f;
        float size = Mathf.Min(52f, (Screen.width - 32f) / hotbar.Length - gap);   // shrink to fit narrow windows
        float total = hotbar.Length * (size + gap) - gap;
        float left = (Screen.width - total) / 2f;
        float top = Screen.height - size - 12f;

        Color before = GUI.color;

        for (int i = 0; i < hotbar.Length; i++)
        {
            Rect box = new Rect(left + i * (size + gap), top, size, size);

            // With the inventory open, clicking a hotbar slot picks it
            Event e = Event.current;
            if (InventoryOpen && e.type == EventType.MouseDown && e.button == 0 && box.Contains(e.mousePosition))
            {
                slot = i;
                e.Use();
            }

            // A white frame behind the picked slot, a dark square behind the rest
            if (i == slot)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), Texture2D.whiteTexture);
            }
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);

            GUI.color = before;
            DrawItemIcon(new Rect(box.x + size * 0.2f, box.y + size * 0.2f, size * 0.6f, size * 0.6f), hotbar[i]);

            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 3, box.y, 20, 20), SlotKeys[i]);
        }

        GUI.color = before;
        GUI.Label(new Rect(0, top - 30, Screen.width, 24), ItemNames[selected], centredLabel);
    }
}
