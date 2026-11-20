using System.Collections.Generic;
using System.IO;
using UnityEngine;

// More blocks for building, and the numbers for all the new things you can hold.
//
//   Slabs:      half-height blocks, in stone and planks
//   Fence:      joins up with the fences and walls next to it. Too tall to jump over!
//   Ladder:     goes on the wall behind it. Walk into it and press W or Space to climb.
//   Sign:       press E on it to write your own words on it
//   Wool:       soft blocks in eight colours (from sheep, or dyed)
//   Cactus:     grows in the desert. Prickly - it hurts to touch in survival!
//   Trampoline: bounces you even higher than a bounce pad
//   Slide:      whee! Stand on it and down you go
//   Rails:      for minecarts to ride along (see Vehicles.cs)
//   Monster cage: found in dungeons. Monsters keep coming out of it until you break it.
public partial class BlockWorld
{
    // --- Blocks ---
    public const byte StoneSlab   = 172;
    public const byte PlankSlab   = 173;
    public const byte Fence       = 174;
    public const byte LadderFirst = 175;   // 175 to 178, one for each way it faces
    public const byte SignFirst   = 179;   // 179 to 182
    public const byte WoolFirst   = 183;   // 183 to 190: white, red, orange, yellow, green, blue, purple, black
    public const byte Cactus      = 191;
    public const byte Trampoline  = 192;
    public const byte SlideFirst  = 193;   // 193 to 196
    public const byte RailNS      = 197;   // rails running north to south
    public const byte RailEW      = 198;   // ...and east to west
    public const byte Spawner     = 199;   // a monster cage

    public const int WoolColours = 8;

    // --- Things you hold that aren't blocks ---
    public const byte RawBeef       = 201;
    public const byte Steak         = 202;
    public const byte RawPork       = 203;
    public const byte CookedPork    = 204;
    public const byte RawFish       = 205;
    public const byte CookedFish    = 206;
    public const byte Leather       = 207;
    public const byte Feather       = 208;
    public const byte StringItem    = 209;
    public const byte FishingRod    = 210;
    public const byte Bow           = 211;
    public const byte Arrow         = 212;
    public const byte LeatherArmour = 213;
    public const byte IronArmour    = 214;
    public const byte DiamondArmour = 215;
    public const byte TreasureMap   = 216;
    public const byte Compass       = 217;
    public const byte Minecart      = 218;
    public const byte Boat          = 219;

    public static bool IsLadder(byte type) { return type >= LadderFirst && type < LadderFirst + 4; }
    public static bool IsSign(byte type)   { return type >= SignFirst && type < SignFirst + 4; }
    public static bool IsSlide(byte type)  { return type >= SlideFirst && type < SlideFirst + 4; }
    public static bool IsWool(byte type)   { return type >= WoolFirst && type < WoolFirst + WoolColours; }
    public static bool IsRail(byte type)   { return type == RailNS || type == RailEW; }
    public static bool IsSlab(byte type)   { return type == StoneSlab || type == PlankSlab; }

    // Blocks drawn as little models (not ordinary cubes)
    public static bool IsModel(byte type)
    {
        return IsSlab(type) || type == Fence || IsLadder(type) || IsSign(type) || type == Cactus ||
               IsSlide(type) || IsRail(type) || type == Spawner;
    }

    // Which way a ladder, sign or slide faces (0 north, 1 east, 2 south, 3 west)
    public static int ModelFacing(byte type)
    {
        if (IsLadder(type)) return type - LadderFirst;
        if (IsSign(type)) return type - SignFirst;
        if (IsSlide(type)) return type - SlideFirst;
        return 0;
    }

    // The direction a slide sends you sliding
    public static Vector3 SlideDirection(byte type) { return Direction(ModelFacing(type)); }

    public static readonly Color[] WoolColour =
    {
        new Color(0.95f, 0.95f, 0.93f), new Color(0.80f, 0.15f, 0.15f), new Color(0.95f, 0.55f, 0.15f),
        new Color(0.97f, 0.85f, 0.20f), new Color(0.30f, 0.70f, 0.25f), new Color(0.20f, 0.35f, 0.85f),
        new Color(0.55f, 0.25f, 0.75f), new Color(0.12f, 0.12f, 0.14f)
    };
    public static readonly string[] WoolNames = { "White", "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Black" };

    private Material railLook, sleeperLook, cageLook, cageFire, signBoard;
    private Font signFont;

    void CreateBuildingMaterials()
    {
        for (int i = 0; i < WoolColours; i++)
        {
            Color colour = WoolColour[i];
            int seed = i;
            materials[WoolFirst + i] = MakePixelMaterial((x, y) =>
            {
                float n = Jumble(x, y, 20 + seed);
                return n > 0.75f ? colour * 1.08f : n < 0.2f ? colour * 0.88f : colour;   // fluffy
            }, true);
        }

        materials[Cactus] = MakePixelMaterial((x, y) =>
            x % 3 == 1 ? new Color(0.20f, 0.45f, 0.18f) : (y % 4 == 0 && x % 3 == 0 ? new Color(0.90f, 0.90f, 0.75f) : new Color(0.30f, 0.62f, 0.25f)));

        materials[Trampoline] = MakePixelMaterial((x, y) =>
        {
            if (x == 0 || y == 0 || x == 7 || y == 7) return new Color(1.00f, 0.80f, 0.15f);
            return (x + y) % 2 == 0 ? new Color(0.20f, 0.45f, 0.95f) : new Color(0.25f, 0.55f, 1.00f);
        });

        Material slide = MakePixelMaterial((x, y) => y % 4 == 0 ? new Color(1.00f, 0.85f, 0.20f) : new Color(0.95f, 0.25f, 0.25f));
        for (int i = 0; i < 4; i++) materials[SlideFirst + i] = slide;

        materials[StoneSlab] = materials[Stone];
        materials[PlankSlab] = materials[Planks];
        materials[Fence] = materials[Planks];
        for (int i = 0; i < 4; i++)
        {
            materials[LadderFirst + i] = materials[Planks];
            materials[SignFirst + i] = materials[Planks];
        }

        railLook = MakeMaterial(new Color(0.62f, 0.62f, 0.66f));
        sleeperLook = MakeMaterial(new Color(0.42f, 0.28f, 0.15f));
        materials[RailNS] = materials[RailEW] = railLook;
        cageLook = MakeMaterial(new Color(0.15f, 0.17f, 0.20f));
        cageFire = MakeGlowingMaterial((x, y) => new Color(0.6f, 0.2f + y * 0.04f, 0.9f));
        materials[Spawner] = cageLook;
        signBoard = materials[Planks];
        signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // --- The models ---

    void BuildModel(GameObject cube, int x, int y, int z, byte type)
    {
        cube.transform.localRotation = Quaternion.LookRotation(Direction(ModelFacing(type)));

        if (IsSlab(type))
        {
            ModelPiece(cube, new Vector3(0f, -0.25f, 0f), new Vector3(1f, 0.5f, 1f), materials[type], true);
        }
        else if (type == Fence)
        {
            ModelPiece(cube, new Vector3(0f, 0f, 0f), new Vector3(0.25f, 1f, 0.25f), materials[Planks], false);
            Solid(cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.25f, 1.5f, 0.25f));
            foreach (Vector3Int side in new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
            {
                byte next = GetBlock(x + side.x, y, z + side.z);
                bool joins = next == Fence || (next != Air && next != Water && next != Lava && !Special(next) && next != Fire);
                if (!joins) continue;

                Vector3 along = new Vector3(side.x, 0f, side.z);
                Vector3 size = side.x != 0 ? new Vector3(0.5f, 0.12f, 0.1f) : new Vector3(0.1f, 0.12f, 0.5f);
                ModelPiece(cube, along * 0.25f + Vector3.up * 0.25f, size, materials[Planks], false);
                ModelPiece(cube, along * 0.25f - Vector3.up * 0.15f, size, materials[Planks], false);
                Solid(cube, along * 0.25f + Vector3.up * 0.25f, new Vector3(size.x, 1.5f, size.z));
            }
        }
        else if (IsLadder(type))
        {
            // Two side rails and rungs, flat against the wall behind
            ModelPiece(cube, new Vector3(-0.35f, 0f, -0.45f), new Vector3(0.08f, 1f, 0.06f), materials[Planks], false);
            ModelPiece(cube, new Vector3(0.35f, 0f, -0.45f), new Vector3(0.08f, 1f, 0.06f), materials[Planks], false);
            for (int i = 0; i < 4; i++)
                ModelPiece(cube, new Vector3(0f, -0.375f + i * 0.25f, -0.45f), new Vector3(0.7f, 0.06f, 0.05f), materials[Planks], false);
            Solid(cube, new Vector3(0f, 0f, -0.45f), new Vector3(0.8f, 1f, 0.08f));
        }
        else if (IsSign(type))
        {
            ModelPiece(cube, new Vector3(0f, -0.25f, 0f), new Vector3(0.08f, 0.5f, 0.08f), materials[Planks], true);   // post
            ModelPiece(cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.5f, 0.07f), signBoard, true);              // board

            string words;
            if (signTexts.TryGetValue(new Vector3Int(x, y, z), out words) && words != "" && signFont != null)
            {
                GameObject text = new GameObject("Words");
                text.transform.SetParent(cube.transform, false);
                text.transform.localPosition = new Vector3(0f, 0.2f, 0.04f);
                text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // readable from the front
                TextMesh mesh = text.AddComponent<TextMesh>();
                mesh.font = signFont;
                mesh.text = WrapSign(words);
                mesh.fontSize = 48;
                mesh.characterSize = 0.012f;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = new Color(0.12f, 0.08f, 0.04f);
                text.GetComponent<MeshRenderer>().sharedMaterial = signFont.material;
            }
        }
        else if (type == Cactus)
        {
            ModelPiece(cube, Vector3.zero, new Vector3(0.8f, 1f, 0.8f), materials[Cactus], true);
            for (int i = 0; i < 6; i++)   // little prickles
            {
                Vector3 spot = new Vector3(i % 2 == 0 ? 0.42f : -0.42f, -0.3f + (i / 2) * 0.3f, (i % 3 - 1) * 0.25f);
                ModelPiece(cube, spot, new Vector3(0.06f, 0.03f, 0.03f), materials[Snow], false);
            }
        }
        else if (IsSlide(type))
        {
            // A shiny ramp: highest at the back, down to the floor at the front
            for (int i = 0; i < 4; i++)
            {
                float height = 1f - i * 0.25f;
                ModelPiece(cube, new Vector3(0f, -0.5f + height / 2f, -0.375f + i * 0.25f), new Vector3(1f, height, 0.25f), materials[type], true);
            }
        }
        else if (IsRail(type))
        {
            bool northSouth = type == RailNS;
            for (int i = -1; i <= 1; i += 2)
                ModelPiece(cube, northSouth ? new Vector3(i * 0.3f, -0.45f, 0f) : new Vector3(0f, -0.45f, i * 0.3f),
                           northSouth ? new Vector3(0.06f, 0.06f, 1f) : new Vector3(1f, 0.06f, 0.06f), railLook, false);
            for (int i = 0; i < 4; i++)
            {
                float along = -0.375f + i * 0.25f;
                ModelPiece(cube, northSouth ? new Vector3(0f, -0.48f, along) : new Vector3(along, -0.48f, 0f),
                           northSouth ? new Vector3(0.85f, 0.04f, 0.1f) : new Vector3(0.1f, 0.04f, 0.85f), sleeperLook, false);
            }
        }
        else if (type == Spawner)
        {
            // A dark cage, with a spooky purple flame inside
            for (int i = 0; i < 4; i++)
            {
                float a = i % 2 == 0 ? -0.45f : 0.45f, b = i < 2 ? -0.45f : 0.45f;
                ModelPiece(cube, new Vector3(a, 0f, b), new Vector3(0.1f, 1f, 0.1f), cageLook, false);
                ModelPiece(cube, new Vector3(0f, 0.45f, a), new Vector3(1f, 0.1f, 0.1f), cageLook, false);
                ModelPiece(cube, new Vector3(0f, -0.45f, a), new Vector3(1f, 0.1f, 0.1f), cageLook, false);
                ModelPiece(cube, new Vector3(a, 0f, 0f), new Vector3(0.06f, 1f, 0.06f), cageLook, false);
                ModelPiece(cube, new Vector3(0f, 0f, a), new Vector3(0.06f, 1f, 0.06f), cageLook, false);
            }
            ModelPiece(cube, new Vector3(0f, -0.1f, 0f), new Vector3(0.3f, 0.5f, 0.3f), cageFire, false);
            Solid(cube, Vector3.zero, Vector3.one);
            Light glow = cube.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.7f, 0.3f, 1f);
            glow.range = 5f;
            glow.intensity = 1.2f;
        }
    }

    void ModelPiece(GameObject cube, Vector3 centre, Vector3 size, Material look, bool solid)
    {
        AddSpecialPiece(cube, centre, size, look);
        if (solid) Solid(cube, centre, size);
    }

    static void Solid(GameObject cube, Vector3 centre, Vector3 size)
    {
        BoxCollider box = cube.AddComponent<BoxCollider>();
        box.center = centre;
        box.size = size;
    }

    // --- Signs ---

    private readonly Dictionary<Vector3Int, string> signTexts = new Dictionary<Vector3Int, string>();

    public string SignText(Vector3Int cell)
    {
        string words;
        return signTexts.TryGetValue(cell, out words) ? words : "";
    }

    public void SetSignText(Vector3Int cell, string words)
    {
        signTexts[cell] = words.Length > 60 ? words.Substring(0, 60) : words;
        Rebuild(cell);
    }

    // Splits the words over a few short lines so they fit on the board
    static string WrapSign(string words)
    {
        string result = "", line = "";
        foreach (string word in words.Split(' '))
        {
            if (line.Length + word.Length > 16 && line != "")
            {
                result += line + "\n";
                line = "";
            }
            line += (line == "" ? "" : " ") + word;
        }
        return result + line;
    }

    // Throws away the model here and builds it again
    void Rebuild(Vector3Int cell)
    {
        GameObject existing;
        if (specials.TryGetValue(cell, out existing))
        {
            Destroy(existing);
            specials.Remove(cell);
        }
        RefreshSpecial(cell.x, cell.y, cell.z);
    }

    void WriteSigns(BinaryWriter writer)
    {
        writer.Write(signTexts.Count);
        foreach (KeyValuePair<Vector3Int, string> sign in signTexts)
        {
            writer.Write(sign.Key.x);
            writer.Write(sign.Key.y);
            writer.Write(sign.Key.z);
            writer.Write(sign.Value);
        }
    }

    void ReadSigns(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
            signTexts[new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32())] = reader.ReadString();
    }

    // --- Monster cages ---

    private readonly HashSet<Vector3Int> spawners = new HashSet<Vector3Int>();
    public IEnumerable<Vector3Int> Spawners { get { return spawners; } }

    // Keeps track of signs and cages as they come and go
    void TrackBuilding(Vector3Int cell, byte type)
    {
        if (type == Spawner) spawners.Add(cell);
        else spawners.Remove(cell);
        if (!IsSign(type)) signTexts.Remove(cell);
    }
}
