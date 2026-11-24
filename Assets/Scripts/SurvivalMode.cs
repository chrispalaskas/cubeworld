using System.Collections.Generic;
using UnityEngine;

// Survival mode's side of your hands and pockets.
//
// In a survival world you start with nothing but a crafting table, your
// sponge and your leash.
// Every block you break goes into your inventory, and every block you place
// uses one up. Crafting uses up what's in the recipe (and you have to have
// it!). Some extra recipes make the blocks you can't dig up - planks,
// glass, bricks, TNT and all the machinery.
//
// You also have to eat: apples fall out of leaves now and then, and a sword
// (or your fist) turns a chicken into a chicken leg. Hold food and press E
// (or right click) to eat it. Hearts and hunger live in PlayerHealth.cs.
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    // Food - only found in survival worlds (after the last block, 93)
    const byte Apple = 94;
    const byte GoldenApple = 95;
    const byte ChickenLeg = 96;

    const int AppleChance = 6;   // one leaf in this many has an apple in it

    static readonly byte[] Foods =
    {
        Apple, ChickenLeg, CookedChicken, Bread, GoldenApple,
        BlockWorld.RawBeef, BlockWorld.Steak, BlockWorld.RawPork, BlockWorld.CookedPork, BlockWorld.RawFish, BlockWorld.CookedFish
    };

    // How much each food fills you up (the food bar holds 20)
    static int FoodValue(byte food)
    {
        if (food == GoldenApple) return 20;
        if (food == BlockWorld.Steak || food == BlockWorld.CookedPork) return 16;
        if (food == CookedChicken || food == BlockWorld.CookedFish) return 12;   // cook it in a furnace - much better than raw!
        if (food == Bread) return 10;
        return 4;                               // an apple, or raw chicken
    }

    static bool IsFood(byte item)
    {
        return System.Array.IndexOf(Foods, item) >= 0;
    }

    // The things you can only make in survival, because in creative they're
    // already in your inventory
    static readonly Recipe[] SurvivalRecipes =
    {
        new Recipe(BlockWorld.Planks, BlockWorld.Wood, 1) { Makes = 4 },
        new Recipe(BlockWorld.CraftingTable, BlockWorld.Planks, 4),
        new Recipe(BlockWorld.Cobblestone, BlockWorld.Stone, 1),
        new Recipe(BlockWorld.Furnace, BlockWorld.Cobblestone, 8),   // glass comes from cooking sand in it
        new Recipe(Bread, WheatItem, 3),

        // Building
        new Recipe(BlockWorld.StoneSlab, BlockWorld.Stone, 3) { Makes = 6 },
        new Recipe(BlockWorld.PlankSlab, BlockWorld.Planks, 2) { Makes = 4 },
        new Recipe(BlockWorld.Fence, BlockWorld.Planks, 4, BlockWorld.Wood, 2) { Makes = 4 },
        new Recipe(BlockWorld.LadderFirst, BlockWorld.Wood, 3, BlockWorld.Planks, 1) { Makes = 4 },
        new Recipe(BlockWorld.SignFirst, BlockWorld.Planks, 6, BlockWorld.Wood, 1) { Makes = 3 },
        new Recipe(BlockWorld.WoolFirst, BlockWorld.StringItem, 4),
        new Recipe(BlockWorld.WoolFirst + 1, BlockWorld.WoolFirst, 1, Apple, 1),                 // red
        new Recipe(BlockWorld.WoolFirst + 2, BlockWorld.WoolFirst, 1, BlockWorld.Brick, 1),      // orange
        new Recipe(BlockWorld.WoolFirst + 3, BlockWorld.WoolFirst, 1, WheatItem, 1),             // yellow
        new Recipe(BlockWorld.WoolFirst + 4, BlockWorld.WoolFirst, 1, BlockWorld.Leaves, 1),     // green
        new Recipe(BlockWorld.WoolFirst + 5, BlockWorld.WoolFirst, 1, BlockWorld.Water, 1),      // blue
        new Recipe(BlockWorld.WoolFirst + 6, BlockWorld.WoolFirst, 1, BlockWorld.Crystal, 1),    // purple
        new Recipe(BlockWorld.WoolFirst + 7, BlockWorld.WoolFirst, 1, BlockWorld.Ash, 1),        // black
        new Recipe(BlockWorld.Trampoline, BlockWorld.BouncePad, 1, BlockWorld.WoolFirst, 2),
        new Recipe(BlockWorld.SlideFirst, BlockWorld.Iron, 3, BlockWorld.Planks, 2) { Makes = 2 },

        // Getting about
        new Recipe(BlockWorld.RailNS, BlockWorld.Iron, 2, BlockWorld.Planks, 2) { Makes = 8 },
        new Recipe(BlockWorld.Minecart, BlockWorld.Iron, 4, BlockWorld.Planks, 1),
        new Recipe(BlockWorld.Boat, BlockWorld.Planks, 5, BlockWorld.Wood, 1),
        new Recipe(BlockWorld.Compass, BlockWorld.Iron, 4, BlockWorld.Cable, 1),

        // Gear
        new Recipe(BlockWorld.FishingRod, BlockWorld.Wood, 3, BlockWorld.StringItem, 2),
        new Recipe(BlockWorld.Bow, BlockWorld.Wood, 3, BlockWorld.StringItem, 3),
        new Recipe(BlockWorld.Arrow, BlockWorld.Gravel, 1, BlockWorld.Wood, 1, BlockWorld.Feather, 1) { Makes = 4 },
        new Recipe(BlockWorld.LeatherArmour, BlockWorld.Leather, 5),
        new Recipe(BlockWorld.IronArmour, BlockWorld.Iron, 5),
        new Recipe(BlockWorld.DiamondArmour, BlockWorld.Diamond, 5),

        // Decorating
        new Recipe(BlockWorld.StainedFirst,     BlockWorld.Glass, 1, Apple, 1),
        new Recipe(BlockWorld.StainedFirst + 1, BlockWorld.Glass, 1, WheatItem, 1),
        new Recipe(BlockWorld.StainedFirst + 2, BlockWorld.Glass, 1, BlockWorld.Leaves, 1),
        new Recipe(BlockWorld.StainedFirst + 3, BlockWorld.Glass, 1, BlockWorld.Water, 1),
        new Recipe(BlockWorld.Lantern, BlockWorld.Iron, 2, BlockWorld.Lamp, 1) { Makes = 2 },
        new Recipe(BlockWorld.FlowerPot, BlockWorld.Brick, 3),
        new Recipe(BlockWorld.GlowIce, BlockWorld.Ice, 1, BlockWorld.Lamp, 1) { Makes = 2 },
        new Recipe(BlockWorld.PhotoFrame, BlockWorld.Planks, 2, BlockWorld.Glass, 1),
        new Recipe(PaintBucket, BlockWorld.Iron, 3, BlockWorld.WoolFirst + 1, 1, BlockWorld.WoolFirst + 5, 1),
        new Recipe(CameraItem, BlockWorld.Iron, 3, BlockWorld.Glass, 1, BlockWorld.Cable, 1),

        // Fun and games
        new Recipe(BlockWorld.Elevator, BlockWorld.Iron, 4, BlockWorld.Gold, 2, BlockWorld.Cable, 1) { Makes = 2 },
        new Recipe(BlockWorld.Target, BlockWorld.WoolFirst, 2, BlockWorld.WoolFirst + 1, 2),
        new Recipe(BlockWorld.StartPad, BlockWorld.Stone, 1, BlockWorld.Leaves, 2),
        new Recipe(BlockWorld.FinishPad, BlockWorld.Stone, 1, BlockWorld.WoolFirst, 1, BlockWorld.WoolFirst + 7, 1),

        // Magic
        new Recipe(BlockWorld.Cauldron, BlockWorld.Iron, 7),
        new Recipe(BlockWorld.EnchantTable, BlockWorld.Obsidian, 4, BlockWorld.Diamond, 2, BlockWorld.Bookshelf, 1),
        new Recipe(BlockWorld.Brick, BlockWorld.Dirt, 1, BlockWorld.Stone, 1) { Makes = 2 },
        new Recipe(BlockWorld.Tnt, BlockWorld.Sand, 4, BlockWorld.Iron, 1),
        new Recipe(BlockWorld.StairsNorth, BlockWorld.Planks, 3) { Makes = 4 },
        new Recipe(BlockWorld.DoorItem, BlockWorld.Planks, 6),
        new Recipe(BlockWorld.Fire, BlockWorld.FireCrystal, 1) { Makes = 4 },
        new Recipe(BlockWorld.Button, BlockWorld.Planks, 1),
        new Recipe(BlockWorld.Cable, BlockWorld.Iron, 1, BlockWorld.Gold, 1) { Makes = 8 },
        new Recipe(BlockWorld.DispenserNorth, BlockWorld.Stone, 7, BlockWorld.Iron, 1),
        new Recipe(BlockWorld.PressurePlate, BlockWorld.Stone, 2),
        new Recipe(BlockWorld.Timer, BlockWorld.Stone, 4, BlockWorld.Cable, 1),
        new Recipe(BlockWorld.Observer, BlockWorld.Cobblestone, 6, BlockWorld.Cable, 2, BlockWorld.Glass, 1),
        new Recipe(BlockWorld.SignalLight, BlockWorld.Lamp, 1, BlockWorld.Cable, 1),
        new Recipe(BlockWorld.NoteBlock, BlockWorld.Planks, 8, BlockWorld.Cable, 1),
        new Recipe(BlockWorld.PistonNorth, BlockWorld.Planks, 3, BlockWorld.Stone, 4, BlockWorld.Iron, 1, BlockWorld.Cable, 1),
        new Recipe(BlockWorld.ConveyorNorth, BlockWorld.Iron, 3, BlockWorld.Cable, 1) { Makes = 4 },
        new Recipe(GoldenApple, Apple, 1, BlockWorld.Gold, 4),

        // Furniture
        new Recipe(BlockWorld.Sink, BlockWorld.Stone, 3, BlockWorld.Iron, 1),
        new Recipe(BlockWorld.Toilet, BlockWorld.Stone, 4, BlockWorld.Water, 1),
        new Recipe(BlockWorld.Bathtub, BlockWorld.Stone, 5, BlockWorld.Iron, 1),
        new Recipe(BlockWorld.Bed, BlockWorld.Planks, 3, BlockWorld.Snow, 3),
        new Recipe(BlockWorld.Table, BlockWorld.Planks, 5),
        new Recipe(BlockWorld.Chair, BlockWorld.Planks, 4, BlockWorld.Wood, 1),
        new Recipe(BlockWorld.Couch, BlockWorld.Planks, 2, BlockWorld.Brick, 3),
        new Recipe(BlockWorld.Stove, BlockWorld.Stone, 6, BlockWorld.Iron, 1, BlockWorld.Lamp, 1),
        new Recipe(BlockWorld.Fridge, BlockWorld.Iron, 4, BlockWorld.Snow, 2),
        new Recipe(BlockWorld.Window, BlockWorld.Glass, 2, BlockWorld.Planks, 1) { Makes = 2 },
        new Recipe(BlockWorld.Rug, BlockWorld.Leaves, 2, BlockWorld.Brick, 1) { Makes = 2 },
        new Recipe(BlockWorld.Painting, BlockWorld.Planks, 2, BlockWorld.Leaves, 1, BlockWorld.Sand, 1),
    };

    private static Recipe[] survivalBook;   // all the recipes, ordinary ones and survival ones

    // The recipe book you're using right now
    static Recipe[] ActiveRecipes
    {
        get
        {
            if (!Survival) return Recipes;
            if (survivalBook == null)
            {
                survivalBook = new Recipe[SurvivalRecipes.Length + Recipes.Length];
                SurvivalRecipes.CopyTo(survivalBook, 0);
                Recipes.CopyTo(survivalBook, SurvivalRecipes.Length);
            }
            return survivalBook;
        }
    }

    static bool Survival { get { return WorldMenu.Survival; } }

    // How many of each thing you've got (survival only)
    private readonly Dictionary<byte, int> stash = new Dictionary<byte, int>();
    private string survivalKey = "";   // "world1-", so the right world's things are saved even while leaving it

    // Your tools never get used up, and you always have them
    static bool IsTool(byte item)
    {
        return item == Sponge || item == Leash;
    }

    int HowMany(byte item)
    {
        int count;
        return stash.TryGetValue(item, out count) ? count : 0;
    }

    // Blocks that turn into something else once placed come back as what you placed
    static byte AsItem(byte block)
    {
        if (BlockWorld.IsDoor(block)) return BlockWorld.DoorItem;
        if (BlockWorld.IsFurniture(block)) return BlockWorld.FurnitureItem(block);
        if (block == BlockWorld.Farmland) return BlockWorld.Dirt;
        if (BlockWorld.IsLadder(block)) return BlockWorld.LadderFirst;
        if (BlockWorld.IsSign(block)) return BlockWorld.SignFirst;
        if (BlockWorld.IsSlide(block)) return BlockWorld.SlideFirst;
        if (BlockWorld.IsRail(block)) return BlockWorld.RailNS;
        if (BlockWorld.IsDispenser(block)) return BlockWorld.DispenserNorth;
        if (BlockWorld.IsPiston(block)) return BlockWorld.PistonNorth;
        if (BlockWorld.IsConveyor(block)) return BlockWorld.ConveyorNorth;
        if (block >= BlockWorld.StairsNorth && block <= BlockWorld.StairsNorth + 3) return BlockWorld.StairsNorth;
        return block;
    }

    // Into your pockets - and onto the hotbar, if it isn't already there and
    // there's an empty slot
    void Give(byte item, int howMany = 1)
    {
        if (!Survival || item == BlockWorld.Air || IsTool(item)) return;
        item = AsItem(item);

        stash[item] = HowMany(item) + howMany;

        if (System.Array.IndexOf(hotbar, item) < 0)
        {
            int empty = System.Array.IndexOf(hotbar, BlockWorld.Air);
            if (empty >= 0)
            {
                hotbar[empty] = item;
                SaveHotbar();
            }
        }
        SaveStash();
    }

    // Uses some up, if you've got enough. When the last one goes, its
    // hotbar slot empties.
    bool UseUp(byte item, int howMany = 1)
    {
        if (!Survival || IsTool(item)) return true;
        if (Mods.NeverRunOut) return HowMany(item) > 0;   // (Mods.cs)
        if (HowMany(item) < howMany) return false;

        int left = HowMany(item) - howMany;
        if (left > 0)
        {
            stash[item] = left;
        }
        else
        {
            stash.Remove(item);
            for (int i = 0; i < hotbar.Length; i++)
                if (hotbar[i] == item) hotbar[i] = BlockWorld.Air;
            SaveHotbar();
        }
        SaveStash();
        return true;
    }

    // What you get for breaking a block: the block itself (fire just goes
    // out), and sometimes an apple from leaves
    void CollectBroken(byte block, Vector3Int where)
    {
        if (!Survival || block == BlockWorld.Fire) return;
        Vector3 at = where;

        if (block == BlockWorld.Wood) Achievements.Unlock("wood");
        if (block == BlockWorld.Iron) Achievements.Unlock("iron");
        if (block == BlockWorld.Diamond) Achievements.Unlock("diamond");
        if (block == BlockWorld.Chest) Structures.CheckTreasure(where);

        // A monster cage just breaks - no more monsters!
        if (block == BlockWorld.Spawner)
        {
            Achievements.Unlock("dungeon");
            PortalTraveller.Announce("You broke the monster cage - no more monsters from that one!");
            return;
        }

        // Wheat gives wheat and seeds, not itself
        if (BlockWorld.IsWheat(block))
        {
            HarvestWheat(block, at);
            return;
        }

        // Break a chest and everything in it comes out too
        if (BlockWorld.IsContainer(block))
        {
            BlockWorld.ChestContents inside = BlockWorld.Instance.ChestAt(where);
            for (int i = 0; i < BlockWorld.ChestContents.Spaces; i++)
                if (inside.items[i] != BlockWorld.Air)
                    ItemDrop.Spawn(AsItem(inside.items[i]), inside.counts[i], at);
        }

        // ...and a furnace's cooking, and what it's finished
        if (BlockWorld.IsFurnace(block))
        {
            byte input, output;
            int inputCount, fuel, outputCount;
            BlockWorld.Instance.FurnaceContents(where, out input, out inputCount, out fuel, out output, out outputCount);
            ItemDrop.Spawn(input, inputCount, at);
            ItemDrop.Spawn(output, outputCount, at);
        }

        ItemDrop.Spawn(AsItem(block), Mods.DoubleDrops ? 2 : 1, at);

        if (block == BlockWorld.Leaves && Random.Range(0, AppleChance) == 0)
            ItemDrop.Spawn(Apple, 1, at);
        if (block == BlockWorld.Grass && Random.Range(0, SeedChance) == 0)
            ItemDrop.Spawn(Seeds, 1, at);
        if (block == BlockWorld.Grass && Random.Range(0, 10) == 0)
            ItemDrop.Spawn((byte)(BlockWorld.FlowerFirst + Random.Range(0, 3)), 1, at);
    }

    // Munch! Only if you're hungry (golden apples you can always eat)
    void Eat(byte food)
    {
        PlayerHealth health = PlayerHealth.Instance;
        if (health == null) return;

        if (food != GoldenApple && health.Full)
        {
            PortalTraveller.Announce("You're full - you can't eat any more!");
            return;
        }
        if (!UseUp(food)) return;

        health.Eat(FoodValue(food));
        Sounds.Eat(transform.position);
        if (food == GoldenApple) health.Heal(PlayerHealth.MostHealth);
        squeezeTime = 0.15f;
        PortalTraveller.Announce(food == GoldenApple ? "Golden apple! All your hearts are back!" : "Yum! " + ItemNames[food] + ".");
    }

    // Left click with no sword: bonk a monster, or catch a chicken for dinner
    bool Punch()
    {
        MonoBehaviour target = AnimalInSights(3.5f, true);

        Monster monster = target as Monster;
        if (monster is Pet && ((Pet)monster).Tamed) return false;   // don't bop your own pet!
        if (monster != null)
        {
            squeezeTime = 0.15f;
            swingTime = SwingSeconds;
            Sounds.Thwack(monster.transform.position);
            monster.Hit(PunchDamage(), transform.position);
            return true;
        }

        Chicken chicken = target as Chicken;
        if (chicken != null)
        {
            ItemDrop.Spawn(ChickenLeg, 1, chicken.transform.position + Vector3.up * 0.3f);
            ItemDrop.Spawn(BlockWorld.Feather, Random.Range(0, 3), chicken.transform.position + Vector3.up * 0.3f);
            chicken.BlowUp();
            return true;
        }
        return false;
    }

    // --- Saving it all ---

    void LoadSurvival()
    {
        survivalKey = "world" + (WorldMenu.CurrentSlot + 1) + "-";
        stash.Clear();

        foreach (string part in PlayerPrefs.GetString(survivalKey + "stash", "").Split(','))
        {
            string[] pair = part.Split(':');
            byte item;
            int count;
            if (pair.Length == 2 && byte.TryParse(pair[0], out item) && int.TryParse(pair[1], out count) && count > 0)
                stash[item] = count;
        }

        // The hotbar starts empty, and only holds things you've got
        for (int i = 0; i < hotbar.Length; i++) hotbar[i] = BlockWorld.Air;
        hotbar[hotbar.Length - 2] = Sponge;
        hotbar[hotbar.Length - 1] = Leash;

        // A brand new survival world: you get a crafting table to start you off
        if (!PlayerPrefs.HasKey(survivalKey + "stash"))
        {
            stash[BlockWorld.CraftingTable] = 1;
            hotbar[0] = BlockWorld.CraftingTable;
            SaveStash();
            SaveHotbar();
        }

        string[] saved = PlayerPrefs.GetString(survivalKey + "survival-hotbar", "").Split(',');
        if (saved.Length == hotbar.Length)
        {
            for (int i = 0; i < saved.Length; i++)
            {
                byte item;
                hotbar[i] = byte.TryParse(saved[i], out item) && HaveGot(item) ? item : BlockWorld.Air;
            }
        }
    }

    void SaveStash()
    {
        List<string> parts = new List<string>();
        foreach (KeyValuePair<byte, int> pair in stash)
            parts.Add(pair.Key + ":" + pair.Value);
        PlayerPrefs.SetString(survivalKey + "stash", string.Join(",", parts));
    }

    // --- The inventory screen ---

    // In survival the inventory only shows what you've actually got
    byte[] InventoryItems()
    {
        if (!Survival) return AllItems;

        List<byte> have = new List<byte>();
        foreach (byte item in AllItems)
            if (HaveGot(item)) have.Add(item);
        foreach (byte food in Foods)
            if (HaveGot(food) && !have.Contains(food)) have.Add(food);
        return have.ToArray();
    }

    // The little number in the corner of a slot
    void DrawCount(Rect box, byte item)
    {
        if (!Survival || item == BlockWorld.Air || IsTool(item)) return;

        Color before = GUI.color;
        string count = HowMany(item).ToString();
        Rect where = new Rect(box.x, box.yMax - 20f, box.width - 3f, 20f);

        GUI.color = Color.black;
        GUI.Label(new Rect(where.x + 1f, where.y + 1f, where.width, where.height), count, countStyle);
        GUI.color = Color.white;
        GUI.Label(where, count, countStyle);
        GUI.color = before;
    }

    private GUIStyle countStyle;

    // --- Pictures of the food ---

    private static Texture2D applePicture, goldenApplePicture, chickenLegPicture;

    static Texture2D FoodPicture(byte food)
    {
        if (food == Bread || food == CookedChicken) return FarmPicture(food);
        if (GearPicture(food) != null) return GearPicture(food);

        if (food == ChickenLeg)
        {
            if (chickenLegPicture == null)
                chickenLegPicture = PixelArt(new[]
                {
                    "...oo...",
                    "..oooo..",
                    "..oboo..",
                    "..oooo..",
                    "...oo...",
                    "....w...",
                    "...w.w..",
                    "........",
                });
            return chickenLegPicture;
        }

        bool golden = food == GoldenApple;
        if (golden ? goldenApplePicture == null : applePicture == null)
        {
            Texture2D picture = PixelArt(new[]
            {
                "....bg..",
                "...bgg..",
                ".xxbxx..",
                "xxxxxxx.",
                "xwxxxxx.",
                "xxxxxxx.",
                ".xxxxx..",
                "..x.x...",
            }, golden ? new Color(1.00f, 0.80f, 0.15f) : new Color(0.85f, 0.12f, 0.10f));
            if (golden) goldenApplePicture = picture;
            else applePicture = picture;
        }
        return golden ? goldenApplePicture : applePicture;
    }
}
