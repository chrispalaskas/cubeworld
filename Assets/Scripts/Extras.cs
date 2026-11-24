using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Even more blocks, and the Candy Land.
//
//   Coloured glass (red, yellow, green, blue), lanterns, flowers (red,
//   yellow and blue - they grow on grass) and flower pots.
//   Elevator:   stand on it and press Space to whizz up to the next elevator
//               above you, or Ctrl to go down to the one below.
//   Target:     shoot it with an arrow for a coin. Bullseye!
//   Start and finish pads: for the parkour course.
//   Glowing ice: lights up the ice caves.
//   Cauldron:   fill it with water, add something, and brew a potion (Magic.cs)
//   Enchanting table: makes your sword, tool or bow magic (Magic.cs)
//   Photo frame: hangs your latest photo on the wall (take one with the camera)
//   Candy Land: candy grass, chocolate ground, rivers of melted chocolate,
//   lollipop trees, candy canes and cotton candy clouds - through a candy portal.
//
// The numbers 220 to 255 are the very last ones there are, so they're
// shared out carefully here and in Magic.cs.
public partial class BlockWorld
{
    public const byte StainedFirst   = 220;   // 220 to 223: red, yellow, green, blue
    public const byte Lantern        = 224;
    public const byte FlowerFirst    = 225;   // 225 to 227: red, yellow, blue
    public const byte FlowerPot      = 228;
    public const byte Elevator       = 229;
    public const byte Target         = 230;
    public const byte StartPad       = 231;
    public const byte FinishPad      = 232;
    public const byte GlowIce        = 233;
    public const byte Cauldron       = 234;
    public const byte EnchantTable   = 235;
    public const byte PhotoFrame     = 236;
    public const byte CandyGrass     = 237;
    public const byte Chocolate      = 238;
    public const byte ChocolateRiver = 239;
    public const byte Lollipop       = 240;
    public const byte CandyCane      = 241;
    public const byte CottonCandy    = 242;
    public const byte CandyPortal    = 243;

    public const int StainedColours = 4;
    public static readonly Color[] StainedColour =
    {
        new Color(0.95f, 0.25f, 0.25f, 0.55f), new Color(1.00f, 0.90f, 0.25f, 0.55f),
        new Color(0.30f, 0.90f, 0.35f, 0.55f), new Color(0.30f, 0.50f, 1.00f, 0.55f)
    };
    static readonly Color[] FlowerColour = { new Color(0.95f, 0.15f, 0.20f), new Color(1.00f, 0.88f, 0.15f), new Color(0.35f, 0.55f, 1.00f) };

    public static bool IsStainedGlass(byte type) { return type >= StainedFirst && type < StainedFirst + StainedColours; }
    public static bool IsFlower(byte type)       { return type >= FlowerFirst && type < FlowerFirst + 3; }

    // The new blocks drawn as little models
    static bool IsExtraModel(byte type)
    {
        return type == Lantern || IsFlower(type) || type == FlowerPot || type == Cauldron || type == EnchantTable || type == PhotoFrame ||
               type == Observer || type == FireflyJar;
    }

    // Blocks you can see through or walk into
    static bool ExtraSeeThrough(byte type) { return IsStainedGlass(type) || type == ChocolateRiver; }

    private Material flowerStem, potLook, ironLook, lanternGlow, bookLook, tableTop, frameWood;

    void CreateExtraMaterials()
    {
        for (int i = 0; i < StainedColours; i++)
        {
            Color tint = StainedColour[i];
            Texture2D glass = new Texture2D(8, 8);
            glass.filterMode = FilterMode.Point;
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                {
                    bool frame = x == 0 || y == 0 || x == 7 || y == 7;
                    bool glint = x - y == 2 || x - y == 3;
                    Color c = frame ? new Color(tint.r * 0.7f, tint.g * 0.7f, tint.b * 0.7f, 0.9f) : glint ? new Color(1f, 1f, 1f, 0.6f) : tint;
                    glass.SetPixel(x, y, c);
                }
            glass.Apply();
            materials[StainedFirst + i] = MakeSeeThroughTexture(glass);
        }

        for (int i = 0; i < 3; i++) { Color c = FlowerColour[i]; materials[FlowerFirst + i] = MakeMaterial(c); }
        flowerStem = MakeMaterial(new Color(0.25f, 0.6f, 0.2f));
        potLook = MakeMaterial(new Color(0.72f, 0.38f, 0.24f));
        ironLook = MakeMaterial(new Color(0.25f, 0.25f, 0.28f));
        lanternGlow = MakeGlowingMaterial((x, y) => new Color(1f, 0.82f, 0.4f));
        bookLook = MakeGlowingMaterial((x, y) => x == 3 || x == 4 ? new Color(0.9f, 0.85f, 0.7f) : new Color(0.55f, 0.2f, 0.7f));
        tableTop = MakePixelMaterial((x, y) => (x + y) % 3 == 0 ? new Color(0.5f, 0.15f, 0.25f) : new Color(0.35f, 0.08f, 0.15f));
        frameWood = MakeMaterial(new Color(0.35f, 0.22f, 0.12f));
        materials[Lantern] = lanternGlow;
        materials[FlowerPot] = potLook;
        materials[Cauldron] = ironLook;
        materials[EnchantTable] = tableTop;
        materials[PhotoFrame] = frameWood;
        CreateObserverMaterials();
        CreateFireflyMaterials();

        materials[Elevator] = MakePixelMaterial((x, y) =>
        {
            bool arrow = (x == 3 || x == 4) && y >= 1 && y <= 6 || (y == 5 && x >= 2 && x <= 5) || (y == 6 && x >= 3 && x <= 4);
            return arrow ? new Color(1f, 0.85f, 0.2f) : (x == 0 || y == 0 || x == 7 || y == 7) ? new Color(0.4f, 0.4f, 0.45f) : new Color(0.62f, 0.64f, 0.7f);
        });
        materials[Target] = MakePixelMaterial((x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
            return d < 1.2f || (d > 2.3f && d < 3.3f) ? new Color(0.9f, 0.15f, 0.15f) : Color.white;
        });
        materials[StartPad] = MakeGlowingMaterial((x, y) => (x == 0 || y == 0 || x == 7 || y == 7) ? new Color(0.1f, 0.5f, 0.1f) : new Color(0.3f, 0.95f, 0.3f));
        materials[FinishPad] = MakePixelMaterial((x, y) => ((x / 2) + (y / 2)) % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.1f));
        materials[GlowIce] = MakeGlowingMaterial((x, y) => (x + 2 * y) % 5 == 0 ? new Color(0.9f, 1f, 1f) : new Color(0.5f, 0.85f, 1f));

        materials[CandyGrass] = MakePixelMaterial((x, y) => Jumble(x, y, 30) > 0.8f ? new Color(1f, 1f, 1f) : Jumble(x, y, 31) > 0.85f ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.6f, 0.8f), true);
        materials[Chocolate] = MakePixelMaterial((x, y) => (x % 4 == 0 || y % 4 == 0) ? new Color(0.30f, 0.17f, 0.08f) : new Color(0.42f, 0.25f, 0.12f), true);
        materials[ChocolateRiver] = MakeSeeThroughMaterial(new Color(0.45f, 0.26f, 0.12f, 0.85f));
        materials[Lollipop] = MakePixelMaterial((x, y) =>
        {
            float a = Mathf.Atan2(y - 3.5f, x - 3.5f) + Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f)) * 0.9f;
            return Mathf.Sin(a * 2f) > 0f ? new Color(1f, 0.25f, 0.45f) : new Color(1f, 0.95f, 0.95f);
        }, true);
        materials[CandyCane] = MakePixelMaterial((x, y) => (x + y) % 4 < 2 ? new Color(0.9f, 0.1f, 0.15f) : Color.white);
        materials[CottonCandy] = MakePixelMaterial((x, y) => Jumble(x, y, 32) > 0.7f ? new Color(1f, 0.85f, 0.95f) : new Color(0.95f, 0.65f, 0.85f));
        materials[CandyPortal] = MakeSwirlMaterial(new Color(1f, 0.45f, 0.75f), new Color(0.6f, 1f, 0.95f));
    }

    // --- The models ---

    void BuildExtraModel(GameObject cube, int x, int y, int z, byte type)
    {
        if (type == Lantern)
        {
            ModelPiece(cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.36f, 0.5f, 0.36f), ironLook, true);
            ModelPiece(cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.38f, 0.38f, 0.26f), lanternGlow, false);
            ModelPiece(cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.26f, 0.38f, 0.38f), lanternGlow, false);
            ModelPiece(cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.1f, 0.12f, 0.1f), ironLook, false);
            AddLight(cube, new Color(1f, 0.8f, 0.45f), 9f, 1.4f);
        }
        else if (IsFlower(type))
        {
            Material petals = materials[type];
            ModelPiece(cube, new Vector3(0f, -0.3f, 0f), new Vector3(0.05f, 0.4f, 0.05f), flowerStem, false);
            ModelPiece(cube, new Vector3(0.08f, -0.35f, 0f), new Vector3(0.14f, 0.04f, 0.06f), flowerStem, false);   // a leaf
            ModelPiece(cube, new Vector3(0f, -0.05f, 0f), new Vector3(0.24f, 0.12f, 0.08f), petals, false);
            ModelPiece(cube, new Vector3(0f, -0.05f, 0f), new Vector3(0.08f, 0.12f, 0.24f), petals, false);
            ModelPiece(cube, new Vector3(0f, -0.04f, 0f), new Vector3(0.08f, 0.14f, 0.08f), materials[(byte)(FlowerFirst + 1)], false);
        }
        else if (type == FlowerPot)
        {
            ModelPiece(cube, new Vector3(0f, -0.35f, 0f), new Vector3(0.35f, 0.3f, 0.35f), potLook, true);
            ModelPiece(cube, new Vector3(0f, -0.21f, 0f), new Vector3(0.28f, 0.03f, 0.28f), materials[Dirt], false);
            ModelPiece(cube, new Vector3(0f, -0.05f, 0f), new Vector3(0.04f, 0.3f, 0.04f), flowerStem, false);
            ModelPiece(cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.1f, 0.2f), materials[FlowerFirst], false);
        }
        else if (type == FireflyJar)
        {
            BuildFireflyJar(cube);
        }
        else if (type == Observer)
        {
            BuildObserver(cube, new Vector3Int(x, y, z));
        }
        else if (type == Cauldron)
        {
            ModelPiece(cube, new Vector3(0f, -0.4f, 0f), new Vector3(0.9f, 0.2f, 0.9f), ironLook, true);
            ModelPiece(cube, new Vector3(0f, -0.05f, 0.42f), new Vector3(0.9f, 0.6f, 0.08f), ironLook, true);
            ModelPiece(cube, new Vector3(0f, -0.05f, -0.42f), new Vector3(0.9f, 0.6f, 0.08f), ironLook, true);
            ModelPiece(cube, new Vector3(0.42f, -0.05f, 0f), new Vector3(0.08f, 0.6f, 0.9f), ironLook, true);
            ModelPiece(cube, new Vector3(-0.42f, -0.05f, 0f), new Vector3(0.08f, 0.6f, 0.9f), ironLook, true);
            if (CauldronFilled(new Vector3Int(x, y, z)))
                ModelPiece(cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.78f, 0.04f, 0.78f), materials[Water], false);
        }
        else if (type == EnchantTable)
        {
            ModelPiece(cube, new Vector3(0f, -0.13f, 0f), new Vector3(1f, 0.75f, 1f), materials[Obsidian], true);
            ModelPiece(cube, new Vector3(0f, 0.25f, 0f), new Vector3(1f, 0.02f, 1f), tableTop, false);
            ModelPiece(cube, new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.08f, 0.32f), bookLook, false);   // a magic book
            AddLight(cube, new Color(0.7f, 0.4f, 1f), 4f, 1f);
            cube.AddComponent<StarTwinkle>();
        }
        else if (type == PhotoFrame)
        {
            int facing, photo;
            PhotoIn(new Vector3Int(x, y, z), out photo, out facing);
            cube.transform.localRotation = Quaternion.LookRotation(Direction(facing));
            ModelPiece(cube, new Vector3(0f, 0f, -0.46f), new Vector3(0.94f, 0.6f, 0.06f), frameWood, true);
            Texture2D picture = PhotoTexture(photo);
            if (picture != null)
            {
                Material look = MakeMaterial(Color.white);
                look.mainTexture = picture;
                ModelPiece(cube, new Vector3(0f, 0f, -0.425f), new Vector3(0.84f, 0.5f, 0.01f), look, false);
            }
        }
    }

    static void AddLight(GameObject cube, Color colour, float range, float brightness)
    {
        Light light = cube.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = colour;
        light.range = range;
        light.intensity = brightness;
        light.shadows = LightShadows.None;
    }

    // --- Cauldrons remember whether they're full of water (kept with the chests) ---

    public bool CauldronFilled(Vector3Int cell)
    {
        ChestContents inside;
        return chests.TryGetValue(cell, out inside) && inside.counts[0] > 0;
    }

    public void FillCauldron(Vector3Int cell, bool full)
    {
        ChestContents inside = ChestAt(cell);
        inside.items[0] = full ? Water : Air;
        inside.counts[0] = (byte)(full ? 1 : 0);
        Rebuild(cell);
    }

    // --- Photos (taken with the camera - see Magic.cs) ---

    static string PhotoFile(int index)
    {
        return Path.Combine(Application.persistentDataPath, "photos", "world" + (WorldMenu.CurrentSlot + 1) + "-" + index + ".png");
    }

    private static readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();

    public static Texture2D PhotoTexture(int index)
    {
        if (index <= 0) return null;
        string file = PhotoFile(index);
        Texture2D picture;
        if (photos.TryGetValue(file, out picture) && picture != null) return picture;
        if (!File.Exists(file)) return null;

        picture = new Texture2D(2, 2);
        picture.LoadImage(File.ReadAllBytes(file));
        photos[file] = picture;
        return picture;
    }

    public static void SavePhoto(int index, Texture2D picture)
    {
        string file = PhotoFile(index);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllBytes(file, picture.EncodeToPNG());
        photos[file] = picture;
    }

    // Which photo hangs in a frame, and which way it faces (kept with the signs' words)
    void PhotoIn(Vector3Int cell, out int photo, out int facing)
    {
        photo = 0;
        facing = 0;
        string[] bits = SignText(cell).Split(':');
        if (bits.Length == 3 && bits[0] == "#photo")
        {
            int.TryParse(bits[1], out photo);
            int.TryParse(bits[2], out facing);
        }
    }

    public void HangPhoto(Vector3Int cell, int photo, int facing)
    {
        signTexts[cell] = "#photo:" + photo + ":" + facing;
        Rebuild(cell);
    }

    // --- The Candy Land ---

    public void GenerateCandyLand()
    {
        int seed = FixedSeed != 0 ? FixedSeed : System.Environment.TickCount;
        random = new System.Random(seed);
        MakeEmptyWorld(NewSizeX, NewSizeY, NewSizeZ);

        float ox = random.Next(0, 10000), oz = random.Next(0, 10000);
        for (int x = 0; x < SizeX; x++)
            for (int z = 0; z < SizeZ; z++)
            {
                float hills = Mathf.PerlinNoise(ox + x * 0.035f, oz + z * 0.035f);
                int height = 8 + Mathf.RoundToInt(hills * 14f);
                groundHeight[x, z] = height;

                // Winding rivers of melted chocolate
                float river = Mathf.PerlinNoise(ox + 300f + x * 0.02f, oz + 300f + z * 0.02f);
                bool inRiver = Mathf.Abs(river - 0.5f) < 0.025f;

                for (int y = 0; y <= height; y++)
                    blocks[x, y, z] = y == height ? CandyGrass : Chocolate;

                if (inRiver)
                {
                    for (int y = height - 2; y <= height; y++) blocks[x, y, z] = y == height - 2 ? Chocolate : ChocolateRiver;
                    blocks[x, height, z] = Air;
                    blocks[x, height - 1, z] = ChocolateRiver;
                }
            }

        // Lollipop trees and candy canes
        for (int x = 3; x < SizeX - 3; x++)
            for (int z = 3; z < SizeZ - 3; z++)
            {
                double roll = random.NextDouble();
                int top = GetSurfaceHeight(x, z);
                if (top < 0 || blocks[x, top, z] != CandyGrass || top + 9 >= SizeY) continue;

                if (roll < 0.012)
                {
                    int tall = random.Next(3, 6);
                    for (int i = 1; i <= tall; i++) blocks[x, top + i, z] = Snow;   // a white stick
                    int c = top + tall + 2;
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dz = -2; dz <= 2; dz++)
                                if (dx * dx + dy * dy + dz * dz <= 5 && InBounds(x + dx, c + dy, z + dz) && blocks[x + dx, c + dy, z + dz] == Air)
                                    blocks[x + dx, c + dy, z + dz] = Lollipop;
                }
                else if (roll < 0.018)
                {
                    int tall = random.Next(3, 6);
                    for (int i = 1; i <= tall; i++) blocks[x, top + i, z] = CandyCane;
                    if (InBounds(x + 1, top + tall, z) && blocks[x + 1, top + tall, z] == Air) blocks[x + 1, top + tall, z] = CandyCane;   // the hook
                    if (InBounds(x + 1, top + tall - 1, z) && blocks[x + 1, top + tall - 1, z] == Air) blocks[x + 1, top + tall - 1, z] = CandyCane;
                }
                else if (roll < 0.022 && InBounds(x, top + 1, z))
                    blocks[x, top + 1, z] = (byte)(FlowerFirst + random.Next(0, 3));
            }

        // Fluffy cotton candy clouds
        for (int c = 0; c < SizeX * SizeZ / 900; c++)
        {
            int cx = random.Next(4, SizeX - 4), cz = random.Next(4, SizeZ - 4), cy = SizeY - 8 + random.Next(0, 3);
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -2; dz <= 2; dz++)
                    for (int dy = 0; dy <= 1; dy++)
                        if (random.NextDouble() < 0.8 && InBounds(cx + dx, cy + dy, cz + dz)) blocks[cx + dx, cy + dy, cz + dz] = CottonCandy;
        }

        BuildAllCubes();
    }
}
