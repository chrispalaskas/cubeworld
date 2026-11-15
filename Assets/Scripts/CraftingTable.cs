using System.Collections.Generic;
using UnityEngine;

// The crafting table. Aim at one and press E (or right click) to open it.
// Put blocks in its 3x3 grid, and if they match a recipe you can make
// something new. Whatever you make stays in your inventory for good.
//
// This is part of BlockInteractor - it shares the hotbar and inventory.
public partial class BlockInteractor
{
    // A recipe is just which things, and how many of each.
    // Where you put them in the grid doesn't matter.
    class Recipe
    {
        public readonly byte Result;
        public readonly byte[] Needs;   // in pairs: item, how many

        public Recipe(byte result, params byte[] needs)
        {
            Result = result;
            Needs = needs;
        }
    }

    static readonly Recipe[] Recipes =
    {
        new Recipe(BlockWorld.Lamp,      BlockWorld.Glass, 4, BlockWorld.Gold, 1),
        new Recipe(BlockWorld.BouncePad, BlockWorld.Leaves, 4, BlockWorld.Water, 1),
        new Recipe(BlockWorld.MegaTnt,   BlockWorld.Tnt, 4, BlockWorld.Obsidian, 1),
        new Recipe(ChickenEgg,           BlockWorld.Snow, 1, BlockWorld.Grass, 2),
        new Recipe(VillagerEgg,          ChickenEgg, 1, BlockWorld.Gold, 1, BlockWorld.Diamond, 1),
        new Recipe(MagicVillagerEgg,     VillagerEgg, 1, BlockWorld.Crystal, 1, BlockWorld.MagicGrass, 1),
        new Recipe(BlockWorld.Portal,    BlockWorld.Obsidian, 4, BlockWorld.Diamond, 1),
        new Recipe(BlockWorld.MagicPortal, BlockWorld.Portal, 1, BlockWorld.Rainbow, 1, BlockWorld.Diamond, 1),
        new Recipe(RainbowArch,          BlockWorld.Rainbow, 1, BlockWorld.Lamp, 1, BlockWorld.Water, 1),
        new Recipe(BlockWorld.Chest,     BlockWorld.Planks, 8),
        new Recipe(UnicornEgg,           ChickenEgg, 1, BlockWorld.Rainbow, 1, BlockWorld.Gold, 1),
        new Recipe(Wings,                BlockWorld.Snow, 4, BlockWorld.Gold, 1, BlockWorld.Diamond, 1),
        new Recipe(DragonEgg,            ChickenEgg, 1, BlockWorld.Obsidian, 2, BlockWorld.Gold, 1),
        new Recipe(BlockWorld.FirePortal, BlockWorld.Obsidian, 4, BlockWorld.Lamp, 1, BlockWorld.Tnt, 1),
        new Recipe(FireVillagerEgg,      VillagerEgg, 1, BlockWorld.FireCrystal, 1, BlockWorld.Lava, 1),
        new Recipe(Sapling,              BlockWorld.Wood, 1, BlockWorld.Leaves, 1),
        new Recipe(BlockWorld.Ice,       BlockWorld.Snow, 2, BlockWorld.Water, 2),
        new Recipe(BlockWorld.Rainbow,   BlockWorld.Brick, 1, BlockWorld.Gold, 1, BlockWorld.Grass, 1,
                                         BlockWorld.Diamond, 1, BlockWorld.Obsidian, 1),
        new Recipe(BlockWorld.Bookshelf, BlockWorld.Planks, 6, BlockWorld.Leaves, 3),
        new Recipe(FireworkRocket,       BlockWorld.Tnt, 1, BlockWorld.Gold, 2),

        // Swords: two of what the blade is made of, and wood for the handle
        new Recipe(WoodenSword,          BlockWorld.Planks, 2, BlockWorld.Wood, 1),
        new Recipe(StoneSword,           BlockWorld.Stone, 2, BlockWorld.Wood, 1),
        new Recipe(IronSword,            BlockWorld.Iron, 2, BlockWorld.Wood, 1),
        new Recipe(GoldSword,            BlockWorld.Gold, 2, BlockWorld.Wood, 1),
        new Recipe(DiamondSword,         BlockWorld.Diamond, 2, BlockWorld.Wood, 1),
    };

    // How long the "You made..." message stays up
    const float MessageSeconds = 4f;

    private bool crafting;                          // is the crafting screen open (rather than the inventory)?
    private readonly byte[] craftGrid = new byte[9];  // 0 is an empty square
    private int craftCursor;                        // which square the arrow keys are on
    private readonly HashSet<byte> crafted = new HashSet<byte>();   // everything you've ever made
    private string craftMessage = "";
    private float craftMessageTime = -100f;
    private static readonly Dictionary<byte, Texture2D> eggPictures = new Dictionary<byte, Texture2D>();
    private static Texture2D saplingPicture, fireworkPicture, stairsPicture, rainbowPicture;

    // --- What you've made so far ---

    // Anything that comes from a recipe has to be made before you can use it
    bool HaveGot(byte item)
    {
        foreach (Recipe recipe in Recipes)
            if (recipe.Result == item)
                return crafted.Contains(item);
        return true;
    }

    // Remembered between games, in every world, just like the hotbar
    void SaveCrafted()
    {
        PlayerPrefs.SetString("crafted", string.Join(",", crafted));
        PlayerPrefs.Save();
    }

    void LoadCrafted()
    {
        foreach (string part in PlayerPrefs.GetString("crafted", "").Split(','))
        {
            byte item;
            if (byte.TryParse(part, out item)) crafted.Add(item);
        }
    }

    // --- Opening and using the table ---

    bool LookingAtCraftingTable()
    {
        Vector3Int block, unused;
        return LookingAtBlock(out block, out unused)
            && BlockWorld.Instance.GetBlock(block.x, block.y, block.z) == BlockWorld.CraftingTable;
    }

    void OpenCrafting()
    {
        OpenInventory();
        crafting = true;
    }

    void CraftingKeys()
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            CloseInventory();
            return;
        }

        // Arrows move around the 3x3 grid
        int column = craftCursor % 3, row = craftCursor / 3;
        if (Input.GetKeyDown(KeyCode.RightArrow)) column = (column + 1) % 3;
        if (Input.GetKeyDown(KeyCode.LeftArrow))  column = (column + 2) % 3;
        if (Input.GetKeyDown(KeyCode.DownArrow))  row = (row + 1) % 3;
        if (Input.GetKeyDown(KeyCode.UpArrow))    row = (row + 2) % 3;
        craftCursor = row * 3 + column;

        if (EnterPressed())
            craftGrid[craftCursor] = selected;
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Q))
            craftGrid[craftCursor] = BlockWorld.Air;
        if (Input.GetKeyDown(KeyCode.C))
            Craft();
    }

    int CountInGrid(byte item)
    {
        int count = 0;
        foreach (byte square in craftGrid)
            if (square == item) count++;
        return count;
    }

    // The thing the grid would make right now, or 0 if it doesn't match anything
    byte MatchRecipe()
    {
        int filled = 9 - CountInGrid(BlockWorld.Air);

        foreach (Recipe recipe in Recipes)
        {
            int needed = 0;
            bool matches = true;

            for (int i = 0; i < recipe.Needs.Length; i += 2)
            {
                needed += recipe.Needs[i + 1];
                if (CountInGrid(recipe.Needs[i]) != recipe.Needs[i + 1]) matches = false;
            }

            // Exactly the right things, and nothing extra
            if (matches && needed == filled) return recipe.Result;
        }
        return 0;
    }

    void Craft()
    {
        byte made = MatchRecipe();
        if (made == 0) return;

        bool firstTime = crafted.Add(made);
        SaveCrafted();
        PutInHotbar(made);
        System.Array.Clear(craftGrid, 0, craftGrid.Length);   // the blocks get used up

        craftMessage = "You made " + ItemNames[made] + "! It's in hotbar slot " + SlotKeys[slot]
                     + (firstTime ? ", and in your inventory from now on." : ".");
        craftMessageTime = Time.time;
    }

    // Clicking a recipe lays its blocks out in the grid for you
    void FillGrid(Recipe recipe)
    {
        System.Array.Clear(craftGrid, 0, craftGrid.Length);

        int square = 0;
        for (int i = 0; i < recipe.Needs.Length; i += 2)
            for (int n = 0; n < recipe.Needs[i + 1]; n++)
                craftGrid[square++] = recipe.Needs[i];
    }

    // --- Drawing it ---

    void DrawCrafting()
    {
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        // Everything is laid out on an 860 x 980 panel, which is shrunk to fit
        // small windows and placed in the middle of the screen
        const float width = 860f, height = 980f;
        float scale = Mathf.Min(1f, (Screen.width - 32f) / width, (Screen.height - 120f) / height);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - width * scale) / 2f, Mathf.Max(8f, (Screen.height - 90f - height * scale) / 2f), 0f),
            Quaternion.identity, Vector3.one * scale);

        Event e = Event.current;
        bool clicked = e.type == EventType.MouseDown;

        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.Label(new Rect(0, 8, width, 24), "Crafting Table", centredLabel);
        GUI.Label(new Rect(20, 32, width - 40, 24),
                  "Pick a block on the hotbar (1-9, 0, or click it), then click a square to put it there.");

        // --- The 3x3 grid ---
        const float cell = 60f, gap = 6f, gridLeft = 30f, gridTop = 70f;
        for (int i = 0; i < 9; i++)
        {
            Rect box = new Rect(gridLeft + (i % 3) * (cell + gap), gridTop + (i / 3) * (cell + gap), cell, cell);

            if (clicked && box.Contains(e.mousePosition))
            {
                craftCursor = i;
                craftGrid[i] = e.button == 1 ? BlockWorld.Air : selected;   // right click empties it
                e.Use();
            }

            DrawSlot(box, i == craftCursor ? Color.yellow : (Color?)null);
            if (craftGrid[i] != BlockWorld.Air)
                DrawItemIcon(Shrink(box, 0.15f), craftGrid[i]);
        }

        // --- What it makes ---
        byte result = MatchRecipe();
        float gridSize = 3 * (cell + gap) - gap;
        Rect resultBox = new Rect(gridLeft + gridSize + 70f, gridTop + cell + gap, cell, cell);

        GUI.Label(new Rect(gridLeft + gridSize + 8f, resultBox.y + 18f, 60f, 24f), "makes", centredLabel);
        DrawSlot(resultBox, result != 0 ? Color.green : (Color?)null);

        if (result != 0)
        {
            DrawItemIcon(Shrink(resultBox, 0.15f), result);
            GUI.Label(new Rect(resultBox.x - 50f, resultBox.yMax + 4f, cell + 100f, 24f), ItemNames[result], centredLabel);
            GUI.Label(new Rect(resultBox.x - 50f, resultBox.yMax + 24f, cell + 100f, 24f), "Click it or press C", centredLabel);

            if (clicked && e.button == 0 && resultBox.Contains(e.mousePosition))
            {
                Craft();
                e.Use();
            }
        }

        // --- The recipe book ---
        const float bookLeft = 410f;
        GUI.Label(new Rect(bookLeft, gridTop - 30f, 420f, 24f), "Recipes - click one to fill the grid");

        for (int r = 0; r < Recipes.Length; r++)
        {
            Recipe recipe = Recipes[r];
            Rect row = new Rect(bookLeft, gridTop - 6f + r * 30f, 430f, 27f);

            if (clicked && e.button == 0 && row.Contains(e.mousePosition))
            {
                FillGrid(recipe);
                e.Use();
            }

            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = before;

            float x = row.x + 6f;
            for (int i = 0; i < recipe.Needs.Length; i += 2)
            {
                GUI.Label(new Rect(x, row.y + 3f, 28f, 24f), recipe.Needs[i + 1] + "x");
                DrawItemIcon(new Rect(x + 22f, row.y + 2f, 24f, 24f), recipe.Needs[i]);
                x += 52f;
            }

            GUI.Label(new Rect(x, row.y + 3f, 20f, 24f), "=");
            DrawItemIcon(new Rect(x + 16f, row.y + 2f, 24f, 24f), recipe.Result);
            GUI.Label(new Rect(x + 46f, row.y + 3f, row.xMax - x - 46f, 24f), ItemNames[recipe.Result]);
        }

        // --- Messages and help along the bottom ---
        if (Time.time - craftMessageTime < MessageSeconds)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(0, height - 66f, width, 24f), craftMessage, centredLabel);
            GUI.color = before;
        }
        GUI.Label(new Rect(0, height - 44f, width, 24f),
                  "Right click a square to empty it.   Keys: arrows + Enter put a block, Backspace empties, C makes it", centredLabel);
        GUI.Label(new Rect(0, height - 24f, width, 24f), "E or Esc: close", centredLabel);

        GUI.matrix = oldMatrix;
    }

    // A dark square, with a coloured frame round it if asked
    void DrawSlot(Rect box, Color? frame)
    {
        Color before = GUI.color;
        if (frame.HasValue)
        {
            GUI.color = frame.Value;
            GUI.DrawTexture(new Rect(box.x - 3, box.y - 3, box.width + 6, box.height + 6), Texture2D.whiteTexture);
        }
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = before;
    }

    static Rect Shrink(Rect box, float border)
    {
        return new Rect(box.x + box.width * border, box.y + box.height * border,
                        box.width * (1f - 2f * border), box.height * (1f - 2f * border));
    }

    // Little pictures drawn as text: each letter is one pixel, top row first
    // (x is drawn in whatever colour "special" is - swords use it for the blade)
    static Texture2D PixelArt(string[] rows, Color special = default(Color))
    {
        Texture2D picture = new Texture2D(8, 8);
        picture.filterMode = FilterMode.Point;

        for (int row = 0; row < 8; row++)
        {
            for (int x = 0; x < 8; x++)
            {
                Color colour;
                switch (rows[row][x])
                {
                    case 'g': colour = new Color(0.30f, 0.65f, 0.20f); break;   // green
                    case 'G': colour = new Color(0.50f, 0.85f, 0.35f); break;   // light green
                    case 'b': colour = new Color(0.45f, 0.30f, 0.15f); break;   // brown
                    case 'd': colour = new Color(0.40f, 0.28f, 0.18f); break;   // dirt
                    case 'r': colour = new Color(0.85f, 0.15f, 0.10f); break;   // red
                    case 'w': colour = Color.white; break;
                    case 'k': colour = new Color(0.20f, 0.20f, 0.22f); break;   // nearly black
                    case 'x': colour = special; break;
                    case 'p': colour = new Color(0.75f, 0.58f, 0.34f); break;   // planks
                    case 'o': colour = new Color(1.00f, 0.55f, 0.10f); break;   // orange
                    case 'y': colour = new Color(1.00f, 0.85f, 0.20f); break;   // yellow
                    default:  colour = Color.clear; break;                      // see-through
                }
                picture.SetPixel(x, 7 - row, colour);   // textures count rows from the bottom
            }
        }
        picture.Apply();
        return picture;
    }

    static Texture2D SaplingPicture()
    {
        if (saplingPicture == null)
            saplingPicture = PixelArt(new[]
            {
                "...gg...",
                "..gGGg..",
                ".gGggGg.",
                "..gGGg..",
                "...bb...",
                "...b....",
                "...b....",
                "..dddd..",
            });
        return saplingPicture;
    }

    // Six coloured bands in a half circle, red on the outside
    static Texture2D RainbowPicture()
    {
        if (rainbowPicture != null) return rainbowPicture;

        Color[] bands =
        {
            new Color(1.00f, 0.20f, 0.20f), new Color(1.00f, 0.55f, 0.10f), new Color(1.00f, 0.92f, 0.20f),
            new Color(0.30f, 0.85f, 0.30f), new Color(0.25f, 0.55f, 1.00f), new Color(0.65f, 0.30f, 0.95f)
        };

        const int size = 16;
        rainbowPicture = new Texture2D(size, size);
        rainbowPicture.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 3f));
                int band = Mathf.FloorToInt((7.5f - distance) / 1.0f);
                bool onArch = y >= 3 && band >= 0 && band < bands.Length;
                rainbowPicture.SetPixel(x, y, onArch ? bands[band] : Color.clear);
            }
        }
        rainbowPicture.Apply();
        return rainbowPicture;
    }

    static Texture2D StairsPicture()
    {
        if (stairsPicture == null)
            stairsPicture = PixelArt(new[]
            {
                "......pp",
                "......pp",
                "....pppp",
                "....pppp",
                "..pppppp",
                "..pppppp",
                "pppppppp",
                "pppppppp",
            });
        return stairsPicture;
    }

    static Texture2D FireworkPicture()
    {
        if (fireworkPicture == null)
            fireworkPicture = PixelArt(new[]
            {
                "...rr...",
                "..rrrr..",
                "..rwwr..",
                "..rrrr..",
                "..rwwr..",
                "..rrrr..",
                "...yy...",
                "..y..y..",
            });
        return fireworkPicture;
    }

    // A speckled egg on a see-through background, for the inventory: cream
    // for a chicken egg, sky blue with gold spots for a villager egg, and
    // purple with glowing pink spots for a magic villager egg
    static readonly Color[] RainbowSpots =
    {
        new Color(1.00f, 0.25f, 0.25f), new Color(1.00f, 0.60f, 0.15f), new Color(1.00f, 0.90f, 0.20f),
        new Color(0.35f, 0.85f, 0.35f), new Color(0.30f, 0.60f, 1.00f), new Color(0.70f, 0.35f, 1.00f)
    };

    static Texture2D EggPicture(byte egg)
    {
        Texture2D picture;
        if (eggPictures.TryGetValue(egg, out picture) && picture != null) return picture;

        Color shell, spots;
        if (egg == VillagerEgg)
        {
            shell = new Color(0.55f, 0.80f, 1.00f);
            spots = new Color(1.00f, 0.80f, 0.20f);
        }
        else if (egg == FireVillagerEgg)
        {
            shell = new Color(1.00f, 0.50f, 0.12f);   // bright orange, with yellow flame spots
            spots = new Color(1.00f, 0.90f, 0.30f);
        }
        else if (egg == DragonEgg)
        {
            shell = new Color(0.20f, 0.12f, 0.25f);   // dark and scaly, with fiery spots
            spots = new Color(1.00f, 0.45f, 0.10f);
        }
        else if (egg == UnicornEgg)
        {
            shell = new Color(0.98f, 0.97f, 1.00f);   // white - the spots are every colour of the rainbow
            spots = Color.white;
        }
        else if (egg == MagicVillagerEgg)
        {
            shell = new Color(0.55f, 0.30f, 0.85f);
            spots = new Color(1.00f, 0.60f, 0.95f);
        }
        else
        {
            shell = new Color(0.98f, 0.93f, 0.82f);
            spots = new Color(0.80f, 0.68f, 0.50f);
        }

        const int size = 12;
        picture = new Texture2D(size, size);
        picture.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // An oval, a little fatter at the bottom like a real egg
                float dx = (x - 5.5f) / (y < 5 ? 4.6f : 3.8f);
                float dy = (y - 5f) / 5.5f;
                bool inside = dx * dx + dy * dy <= 1f;
                bool speckle = (x * 5 + y * 3) % 7 == 0;

                Color spot = egg == UnicornEgg ? RainbowSpots[(x + y) % RainbowSpots.Length] : spots;
                picture.SetPixel(x, y, !inside ? Color.clear : speckle ? spot : shell);
            }
        }
        picture.Apply();

        eggPictures[egg] = picture;
        return picture;
    }
}
