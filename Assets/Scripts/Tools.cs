using System.Collections.Generic;
using UnityEngine;

// Digging, and the tools that make it quicker.
//
// In survival, blocks take time to break: hold the left mouse button (or Q)
// and cracks spread across the block until it breaks. Leaves and glass go
// in a moment, dirt and sand quite quickly, wood takes longer, and stone
// and metal take longest of all. (In creative, everything still breaks
// with a single click.)
//
// Tools make it much quicker - if it's the right tool:
//   Pickaxe:  stone, cobblestone, bricks, ores, metal and machines
//   Axe:      wood, planks and wooden things
//   Shovel:   dirt, grass, sand, gravel, snow and ash
// Each comes in wood, stone, iron and diamond - the better it is, the faster.
// Tools are also handy in a fight (not as good as a sword, though).
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    // Pickaxes, then axes, then shovels - wood, stone, iron, diamond
    const byte ToolFirst = 156;
    const byte ToolLast = 167;
    const byte WoodenPickaxe = 156, StonePickaxe = 157, IronPickaxe = 158, DiamondPickaxe = 159;
    const byte WoodenAxe = 160, StoneAxe = 161, IronAxe = 162, DiamondAxe = 163;
    const byte WoodenShovel = 164, StoneShovel = 165, IronShovel = 166, DiamondShovel = 167;

    enum ToolKind { Pickaxe, Axe, Shovel }

    static bool IsDiggingTool(byte item) { return item >= ToolFirst && item <= ToolLast; }
    static ToolKind KindOfTool(byte tool) { return (ToolKind)((tool - ToolFirst) / 4); }
    static int TierOf(byte tool) { return (tool - ToolFirst) % 4; }   // 0 wood, 1 stone, 2 iron, 3 diamond

    // How many times faster the right tool digs
    static readonly float[] TierSpeed = { 2f, 3.5f, 5f, 8f };

    // What each one is made of (it borrows that block's look)
    static byte HeadBlock(byte tool)
    {
        switch (TierOf(tool))
        {
            case 1: return BlockWorld.Stone;
            case 2: return BlockWorld.Iron;
            case 3: return BlockWorld.Diamond;
            default: return BlockWorld.Planks;
        }
    }

    // --- How long things take to break ---

    // Which tool a block wants
    static ToolKind? BestToolFor(byte block)
    {
        switch (Sounds.KindOf(block))
        {
            case Sounds.Kind.Dirt:
            case Sounds.Kind.Sand:
            case Sounds.Kind.Snow:  return ToolKind.Shovel;
            case Sounds.Kind.Wood:  return ToolKind.Axe;
            case Sounds.Kind.Stone:
            case Sounds.Kind.Metal: return ToolKind.Pickaxe;
            default:                return null;   // leaves and glass - anything will do
        }
    }

    // Seconds to break it with your bare hands
    static float Hardness(byte block)
    {
        if (block == BlockWorld.Obsidian) return 12f;
        if (block == BlockWorld.Diamond || block == BlockWorld.Gold || block == BlockWorld.Iron) return 4f;
        if (block == BlockWorld.Leaves || block == BlockWorld.Glass || block == BlockWorld.Window || block == BlockWorld.Lamp ||
            block == BlockWorld.Rug || block == BlockWorld.Painting || block == BlockWorld.Cloud) return 0.3f;
        if (block == BlockWorld.Snow || block == BlockWorld.MushroomCap || block == BlockWorld.Ice) return 0.4f;

        switch (Sounds.KindOf(block))
        {
            case Sounds.Kind.Dirt:
            case Sounds.Kind.Sand:  return 0.7f;
            case Sounds.Kind.Wood:  return 1.6f;
            case Sounds.Kind.Metal: return 3.5f;
            case Sounds.Kind.Glass: return 0.6f;
            case Sounds.Kind.Leaves: return 0.2f;
            default:                return 2.8f;   // stone and the like
        }
    }

    float BreakTime(byte block, byte holding)
    {
        if (Mods.InstantMining || PlayerLook.FormSmashes) return 0.01f;   // (Mods.cs, and the Stone Golem in Transform.cs)
        float time = Hardness(block);
        if (IsDiggingTool(holding) && BestToolFor(block) == KindOfTool(holding))
            time /= TierSpeed[TierOf(holding)] * (Enchanted(holding) ? 1.6f : 1f);
        return time;
    }

    // --- Digging ---

    private Vector3Int diggingAt;
    private float dug;                  // 0 to 1 - how far through breaking it we are
    private float nextDigSound;
    private bool digging;
    private bool waitForLetGo;          // the click hit a monster, so don't start digging until it's let go
    private GameObject cracks;
    private Material[] crackLooks;

    // Called every frame while the button's held down (survival only)
    void KeepDigging()
    {
        if (waitForLetGo) return;

        // Fire, lava, cable, pressure plates and wheat go straight away
        Vector3Int soft;
        if (LookingAtLavaOrFire(out soft))
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q)) Break();
            StopDigging();
            return;
        }

        Vector3Int block, unused;
        if (!LookingAtBlock(out block, out unused))
        {
            StopDigging();
            return;
        }

        byte type = BlockWorld.Instance.GetBlock(block.x, block.y, block.z);
        if (!digging || block != diggingAt)
        {
            digging = true;
            diggingAt = block;
            dug = 0f;
        }

        dug += Time.deltaTime / Mathf.Max(0.05f, BreakTime(type, selected));
        squeezeTime = 0.1f;   // the hand chops away

        if (Time.time > nextDigSound)
        {
            nextDigSound = Time.time + 0.22f;
            Sounds.Dig(type, block);
        }

        if (dug >= 1f)
        {
            Break();
            StopDigging();
            return;
        }
        ShowCracks(block, dug);
    }

    void StopDigging()
    {
        digging = false;
        dug = 0f;
        if (cracks != null) cracks.SetActive(false);
    }

    // A see-through box just bigger than the block, with cracks drawn on it
    // that spread as you dig
    void ShowCracks(Vector3Int block, float amount)
    {
        if (cracks == null)
        {
            crackLooks = new Material[5];
            for (int stage = 0; stage < crackLooks.Length; stage++)
                crackLooks[stage] = BlockWorld.MakeSeeThroughTexture(CrackPicture(stage));

            cracks = new GameObject("Cracks");
            cracks.transform.localScale = Vector3.one * 1.004f;
            cracks.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            MeshRenderer look = cracks.AddComponent<MeshRenderer>();
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        cracks.SetActive(true);
        cracks.transform.position = block;
        int which = Mathf.Clamp((int)(amount * crackLooks.Length), 0, crackLooks.Length - 1);
        cracks.GetComponent<MeshRenderer>().sharedMaterial = crackLooks[which];
    }

    // More and more dark cracks, spreading out from the middle
    static Texture2D CrackPicture(int stage)
    {
        Texture2D picture = new Texture2D(16, 16);
        picture.filterMode = FilterMode.Point;
        Color clear = new Color(0f, 0f, 0f, 0f), crack = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                picture.SetPixel(x, y, clear);

        // Wiggly lines, starting in the middle - more of them, longer, each stage
        System.Random dice = new System.Random(7);
        int lines = 2 + stage * 2;
        for (int l = 0; l < lines; l++)
        {
            float angle = (float)dice.NextDouble() * Mathf.PI * 2f;
            float x = 7.5f, y = 7.5f;
            int length = 3 + stage * 2;
            for (int step = 0; step < length; step++)
            {
                angle += ((float)dice.NextDouble() - 0.5f) * 1.2f;
                x += Mathf.Cos(angle);
                y += Mathf.Sin(angle);
                int px = Mathf.Clamp(Mathf.RoundToInt(x), 0, 15), py = Mathf.Clamp(Mathf.RoundToInt(y), 0, 15);
                picture.SetPixel(px, py, crack);
            }
        }
        picture.Apply();
        return picture;
    }

    // How hard a punch is, with whatever's in your hand
    int PunchDamage()
    {
        return (IsDiggingTool(selected) ? 3 + TierOf(selected) : 2) * PlayerLook.FormPunch * (Mods.SuperStrength ? 5 : 1);   // an iron golem punches hard! (Transform.cs)
    }

    // --- The tool in your hand ---

    private GameObject toolInHand;
    private Transform toolHead;
    private MeshRenderer toolHeadLook;

    void BuildTool()
    {
        toolInHand = new GameObject("Tool");
        toolInHand.transform.SetParent(hand.transform, false);
        toolInHand.transform.localPosition = new Vector3(0.45f, -0.36f, 0.72f);

        Material brown = BlockWorld.MakeMaterial(new Color(0.40f, 0.26f, 0.12f));
        GameObject handle = AddHandPart(new Vector3(0f, 0.12f, 0f), new Vector3(0.05f, 0.42f, 0.05f), brown);
        handle.transform.SetParent(toolInHand.transform, false);

        GameObject head = AddHandPart(Vector3.zero, Vector3.one, null);
        head.transform.SetParent(toolInHand.transform, false);
        toolHead = head.transform;
        toolHeadLook = head.GetComponent<MeshRenderer>();
    }

    void MoveTool()
    {
        bool holding = IsDiggingTool(selected);
        toolInHand.SetActive(holding);
        if (!holding) return;

        toolHeadLook.sharedMaterial = BlockWorld.Instance.MaterialFor(HeadBlock(selected));
        switch (KindOfTool(selected))
        {
            case ToolKind.Pickaxe:   // a bar across the top
                toolHead.localPosition = new Vector3(0f, 0.32f, 0f);
                toolHead.localScale = new Vector3(0.36f, 0.06f, 0.06f);
                break;
            case ToolKind.Axe:       // a chunky blade on one side
                toolHead.localPosition = new Vector3(0.07f, 0.27f, 0f);
                toolHead.localScale = new Vector3(0.13f, 0.16f, 0.04f);
                break;
            default:                 // a flat scoop on the end
                toolHead.localPosition = new Vector3(0f, 0.38f, 0f);
                toolHead.localScale = new Vector3(0.12f, 0.15f, 0.025f);
                break;
        }

        // Chopping away while digging, a swing when hitting
        swingTime = Mathf.Max(swingTime - Time.deltaTime, 0f);
        float chop = digging ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 50f : Mathf.Sin(swingTime / SwingSeconds * Mathf.PI) * 70f;
        toolInHand.transform.localRotation = Quaternion.Euler(25f + chop, 0f, 15f);
    }

    // --- Pictures for the inventory ---

    private static readonly Dictionary<byte, Texture2D> toolPictures = new Dictionary<byte, Texture2D>();

    static Texture2D ToolPicture(byte tool)
    {
        Texture2D picture;
        if (toolPictures.TryGetValue(tool, out picture)) return picture;

        Color head;
        switch (TierOf(tool))
        {
            case 1:  head = new Color(0.60f, 0.60f, 0.62f); break;
            case 2:  head = new Color(0.88f, 0.88f, 0.90f); break;
            case 3:  head = new Color(0.45f, 0.95f, 0.95f); break;
            default: head = new Color(0.75f, 0.58f, 0.34f); break;
        }

        string[] rows;
        switch (KindOfTool(tool))
        {
            case ToolKind.Pickaxe: rows = new[]
            {
                ".xxxxx..",
                "x..b..x.",
                "x..b..x.",
                "...b....",
                "...b....",
                "...b....",
                "...b....",
                "...b....",
            }; break;
            case ToolKind.Axe: rows = new[]
            {
                "..xxb...",
                ".xxxb...",
                ".xxxb...",
                "..xxb...",
                "....b...",
                "....b...",
                "....b...",
                "....b...",
            }; break;
            default: rows = new[]
            {
                "...xx...",
                "..xxxx..",
                "..xxxx..",
                "...xx...",
                "...b....",
                "...b....",
                "...b....",
                "...b....",
            }; break;
        }

        picture = PixelArt(rows, head);
        toolPictures[tool] = picture;
        return picture;
    }
}
