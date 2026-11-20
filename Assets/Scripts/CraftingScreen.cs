using System.Collections.Generic;
using UnityEngine;

// The crafting screen.
//
// On the left: the 3x3 grid, what it makes, and a big MAKE button. Under
// that, the recipe you picked - with what you've got and what you still need.
// On the right: the recipe book. Tabs sort it into kinds of things, the
// search box finds anything by name, and (in survival) the ones you can
// make right now are green - tick "Only what I can make" to see just those.
// Click a recipe to fill the grid, then press MAKE. The mouse wheel scrolls the list.
//
// This is part of BlockInteractor (see also CraftingTable.cs).
public partial class BlockInteractor
{
    static readonly string[] Tabs = { "All", "Building", "Home", "Tools & Gear", "Food", "Machines", "Magic" };

    private int recipeTab;
    private int recipeScroll;
    private string recipeSearch = "";
    private bool onlyCanMake;
    private bool typingSearch;
    private Recipe chosenRecipe;
    private GUIStyle bigButton, rowTitle, smallLabel;

    // Which tab a recipe belongs in
    static int TabFor(byte item)
    {
        if (IsSword(item) || IsDiggingTool(item) || item == Wings || item == BlockWorld.Bow || item == BlockWorld.Arrow ||
            item == BlockWorld.FishingRod || IsArmour(item) || item == BlockWorld.Compass || item == BlockWorld.TreasureMap) return 3;
        if (IsFood(item) || item == Bread) return 4;
        if (item == BlockWorld.Button || item == BlockWorld.Cable || item == BlockWorld.DispenserNorth || item == BlockWorld.PressurePlate ||
            item == BlockWorld.Timer || item == BlockWorld.SignalLight || item == BlockWorld.NoteBlock || item == BlockWorld.PistonNorth ||
            item == BlockWorld.ConveyorNorth || item == BlockWorld.RailNS || item == BlockWorld.Minecart || item == BlockWorld.Boat ||
            item == BlockWorld.Tnt || item == BlockWorld.MegaTnt) return 5;
        if (BlockWorld.IsFurniture(item) || item == BlockWorld.Chest || item == BlockWorld.DoorItem || item == BlockWorld.Bookshelf ||
            item == BlockWorld.CraftingTable || item == BlockWorld.Lamp || item == BlockWorld.SignFirst || item == BlockWorld.Trampoline ||
            item == BlockWorld.SlideFirst) return 2;
        if (item == ChickenEgg || item == VillagerEgg || item == MagicVillagerEgg || item == UnicornEgg || item == DragonEgg ||
            item == FireVillagerEgg || item == BlockWorld.Portal || item == BlockWorld.MagicPortal || item == BlockWorld.FirePortal ||
            item == RainbowArch || item == BlockWorld.Rainbow || item == FireworkRocket || item == BlockWorld.Fire ||
            item == BlockWorld.BouncePad || item == Sapling) return 6;
        return 1;
    }

    // In survival: have you got everything it needs?
    bool CanMake(Recipe recipe)
    {
        if (!Survival) return true;
        for (int i = 0; i < recipe.Needs.Length; i += 2)
            if (HowMany(recipe.Needs[i]) < recipe.Needs[i + 1]) return false;
        return !pocketCrafting || TotalNeeded(recipe) <= PocketSize;
    }

    static int TotalNeeded(Recipe recipe)
    {
        int total = 0;
        for (int i = 0; i < recipe.Needs.Length; i += 2) total += recipe.Needs[i + 1];
        return total;
    }

    // The recipes on show, with the current tab, search and tick box
    List<Recipe> ShownRecipes()
    {
        List<Recipe> shown = new List<Recipe>();
        string search = recipeSearch.Trim().ToLower();
        foreach (Recipe recipe in ActiveRecipes)
        {
            if (recipeTab != 0 && TabFor(recipe.Result) != recipeTab) continue;
            if (search != "" && !ItemNames[recipe.Result].ToLower().Contains(search)) continue;
            if (onlyCanMake && !CanMake(recipe)) continue;
            shown.Add(recipe);
        }
        // The ones you can make go first
        if (Survival) shown.Sort((a, b) => CanMake(b).CompareTo(CanMake(a)));
        return shown;
    }

    void DrawCrafting()
    {
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        if (bigButton == null)
        {
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            rowTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            smallLabel = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }

        // Laid out on a 1100 x 720 panel, shrunk to fit small windows
        const float width = 1100f, height = 720f;
        float scale = Mathf.Min(1f, (Screen.width - 24f) / width, (Screen.height - 100f) / height);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f, Mathf.Max(6f, (Screen.height - 86f - height * scale) / 2f), 0f),
                                   Quaternion.identity, Vector3.one * scale);

        Event e = Event.current;
        bool clicked = e.type == EventType.MouseDown;

        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.Label(new Rect(0, 8, width, 24), pocketCrafting ? "Crafting in your hands (only small recipes - 4 things or fewer)" : "Crafting Table", centredLabel);

        DrawGridSide(e, clicked);
        DrawBook(e, clicked);

        // Messages along the bottom
        if (Time.time - craftMessageTime < MessageSeconds)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(0, height - 50f, width, 24f), craftMessage, centredLabel);
            GUI.color = before;
        }
        GUI.Label(new Rect(0, height - 28f, width, 24f),
                  "Click a recipe to fill the grid, then MAKE.   Or pick a block on the hotbar and click a square (right click empties it).   E or Esc: close", centredLabel);

        GUI.matrix = oldMatrix;
    }

    // --- The left side: grid, result, MAKE, and the chosen recipe ---

    void DrawGridSide(Event e, bool clicked)
    {
        const float cell = 72f, gap = 8f, gridLeft = 30f, gridTop = 50f;
        GUI.Label(new Rect(gridLeft, gridTop - 4f, 300f, 20f), "", smallLabel);

        for (int i = 0; i < 9; i++)
        {
            Rect box = new Rect(gridLeft + (i % 3) * (cell + gap), gridTop + 10f + (i / 3) * (cell + gap), cell, cell);
            if (clicked && box.Contains(e.mousePosition))
            {
                craftCursor = i;
                craftGrid[i] = e.button == 1 ? BlockWorld.Air : selected;
                chosenRecipe = null;
                e.Use();
            }
            DrawSlot(box, i == craftCursor ? Color.yellow : (Color?)null);
            if (craftGrid[i] != BlockWorld.Air) DrawItemIcon(Shrink(box, 0.15f), craftGrid[i]);
        }

        float gridSize = 3 * (cell + gap) - gap;
        Recipe matched = MatchingRecipe();
        byte result = matched != null ? matched.Result : (byte)0;

        // An arrow, and what it makes
        GUI.Label(new Rect(gridLeft + gridSize + 8f, gridTop + cell + 30f, 40f, 30f), "=>", rowTitle);
        Rect resultBox = new Rect(gridLeft + gridSize + 50f, gridTop + 10f + cell + gap - 6f, cell + 12f, cell + 12f);
        DrawSlot(resultBox, result != 0 ? Color.green : (Color?)null);
        if (result != 0)
        {
            DrawItemIcon(Shrink(resultBox, 0.15f), result);
            if (matched.Makes > 1) GUI.Label(new Rect(resultBox.xMax - 26f, resultBox.yMax - 22f, 26f, 20f), "x" + matched.Makes, rowTitle);
        }
        else if (pocketCrafting && MatchingRecipe(true) != null)
            GUI.Label(new Rect(resultBox.x - 30f, resultBox.yMax + 2f, 150f, 40f), "Too big for your hands!", smallLabel);

        // The big MAKE button
        Rect make = new Rect(gridLeft, gridTop + 10f + gridSize + 16f, gridSize + 50f + cell + 12f, 50f);
        GUI.enabled = result != 0;
        if (GUI.Button(make, result != 0 ? "MAKE " + ItemNames[result].ToUpper() + "  (C)" : "Put a recipe in the grid", bigButton))
            Craft();
        GUI.enabled = true;

        // The recipe you picked: what it needs, and what you've got
        Recipe show = chosenRecipe ?? matched;
        float y = make.yMax + 18f;
        if (show == null)
        {
            GUI.Label(new Rect(gridLeft, y, 340f, 60f), "Pick a recipe from the book on the right to see what it needs.");
            return;
        }

        GUI.Label(new Rect(gridLeft, y, 340f, 24f), (show.Makes > 1 ? show.Makes + " x " : "") + ItemNames[show.Result], rowTitle);
        y += 28f;
        for (int i = 0; i < show.Needs.Length; i += 2)
        {
            byte need = show.Needs[i];
            int count = show.Needs[i + 1];
            DrawItemIcon(new Rect(gridLeft, y, 28f, 28f), need);
            string line = count + " x " + ItemNames[need];
            if (Survival)
            {
                int have = HowMany(need);
                GUI.color = have >= count ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.55f, 0.5f);
                line += "   (you have " + have + ")";
            }
            GUI.Label(new Rect(gridLeft + 36f, y + 4f, 320f, 24f), line);
            GUI.color = Color.white;
            y += 32f;
        }
        if (Survival && !CanMake(show))
            GUI.Label(new Rect(gridLeft, y + 4f, 340f, 40f), pocketCrafting && TotalNeeded(show) > PocketSize
                      ? "This one needs a crafting table." : "You need more of the red things to make this.", smallLabel);
    }

    // --- The right side: the recipe book ---

    void DrawBook(Event e, bool clicked)
    {
        const float left = 420f, top = 44f, bookWidth = 660f;
        Color before = GUI.color;

        // Tabs
        float tabWidth = bookWidth / Tabs.Length;
        for (int t = 0; t < Tabs.Length; t++)
        {
            GUI.color = t == recipeTab ? new Color(1f, 0.85f, 0.3f) : Color.white;
            if (GUI.Button(new Rect(left + t * tabWidth, top, tabWidth - 4f, 28f), Tabs[t]))
            {
                recipeTab = t;
                recipeScroll = 0;
            }
        }
        GUI.color = before;

        // Search, and the tick box
        GUI.Label(new Rect(left, top + 38f, 60f, 24f), "Search:");
        GUI.SetNextControlName("RecipeSearch");
        string typed = GUI.TextField(new Rect(left + 60f, top + 38f, 260f, 24f), recipeSearch, 20);
        if (typed != recipeSearch) { recipeSearch = typed; recipeScroll = 0; }
        typingSearch = GUI.GetNameOfFocusedControl() == "RecipeSearch";
        if (clicked && !new Rect(left + 60f, top + 38f, 260f, 24f).Contains(e.mousePosition) && typingSearch)
        {
            GUI.FocusControl(null);
            typingSearch = false;
        }
        if (Survival) onlyCanMake = GUI.Toggle(new Rect(left + 340f, top + 38f, 300f, 24f), onlyCanMake, " Only what I can make right now");

        // The list
        List<Recipe> shown = ShownRecipes();
        const float rowHeight = 40f, listTop = top + 72f;
        int fits = 12;
        recipeScroll = Mathf.Clamp(recipeScroll, 0, Mathf.Max(0, shown.Count - fits));

        if (shown.Count == 0)
            GUI.Label(new Rect(left, listTop + 10f, bookWidth, 24f), "No recipes here" + (onlyCanMake ? " that you can make yet." : "."), centredLabel);

        for (int r = recipeScroll; r < Mathf.Min(shown.Count, recipeScroll + fits); r++)
        {
            Recipe recipe = shown[r];
            Rect row = new Rect(left, listTop + (r - recipeScroll) * (rowHeight + 2f), bookWidth - 16f, rowHeight);
            bool canMake = CanMake(recipe);
            bool hover = row.Contains(e.mousePosition);

            if (clicked && e.button == 0 && hover)
            {
                FillGrid(recipe);
                chosenRecipe = recipe;
                e.Use();
            }

            GUI.color = recipe == chosenRecipe ? new Color(0.45f, 0.38f, 0.10f, 0.95f)
                      : canMake && Survival ? new Color(0.12f, hover ? 0.38f : 0.30f, 0.12f, 0.95f)
                      : new Color(0.15f, 0.15f, 0.15f, hover ? 1f : 0.9f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = before;

            // What it makes, on the left...
            DrawItemIcon(new Rect(row.x + 6f, row.y + 4f, 32f, 32f), recipe.Result);
            GUI.Label(new Rect(row.x + 46f, row.y + 10f, 220f, 24f), (recipe.Makes > 1 ? recipe.Makes + " " : "") + ItemNames[recipe.Result], rowTitle);

            // ...and what it needs, on the right
            float x = row.x + 270f;
            for (int i = 0; i < recipe.Needs.Length; i += 2)
            {
                DrawItemIcon(new Rect(x, row.y + 6f, 28f, 28f), recipe.Needs[i]);
                GUI.Label(new Rect(x + 28f, row.y + 12f, 30f, 22f), "x" + recipe.Needs[i + 1], smallLabel);
                x += 62f;
            }
        }

        // A scroll bar, so you can see there's more
        if (shown.Count > fits)
        {
            Rect track = new Rect(left + bookWidth - 10f, listTop, 8f, fits * (rowHeight + 2f));
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            float size = track.height * fits / shown.Count;
            float at = track.y + (track.height - size) * recipeScroll / Mathf.Max(1, shown.Count - fits);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(track.x, at, track.width, size), Texture2D.whiteTexture);
            GUI.color = before;
            GUI.Label(new Rect(left, listTop + fits * (rowHeight + 2f) + 2f, bookWidth, 20f),
                      shown.Count + " recipes - scroll with the mouse wheel", smallLabel);
        }
    }
}
