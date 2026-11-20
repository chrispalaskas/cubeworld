using UnityEngine;

// Farming!
//
// Seeds sometimes pop out when you break grass. Hold some and press E (or
// right click) on the top of a grass or dirt block: it turns into farmland,
// and a little wheat shoot appears. Give it a minute or two and it grows up
// tall and golden. Break it when it's golden for wheat (and more seeds);
// break it too early and you just get your seed back.
//
// Three wheat at the crafting table makes bread - very filling!
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    const byte Seeds = 168;
    const byte WheatItem = 169;
    const byte Bread = 170;
    const byte CookedChicken = BlockWorld.CookedChicken;   // 171

    const int SeedChance = 4;   // one grass block in this many has seeds in it

    // Planting: only on top of grass, dirt or farmland
    bool PlantSeeds()
    {
        Vector3Int ground, above;
        if (!LookingAtBlock(out ground, out above)) return false;
        if (above != ground + Vector3Int.up) return false;   // has to be the top

        BlockWorld world = BlockWorld.Instance;
        byte soil = world.GetBlock(ground.x, ground.y, ground.z);
        if (soil != BlockWorld.Grass && soil != BlockWorld.Dirt && soil != BlockWorld.Farmland)
        {
            PortalTraveller.Announce("Seeds only grow on grass or dirt.");
            return false;
        }
        if (world.GetBlock(above.x, above.y, above.z) != BlockWorld.Air) return false;

        world.SetBlock(ground.x, ground.y, ground.z, BlockWorld.Farmland);
        world.SetBlock(above.x, above.y, above.z, BlockWorld.WheatFirst);
        Sounds.Place(BlockWorld.Grass, above);
        return true;
    }

    // What you get from wheat when you break it
    void HarvestWheat(byte crop, Vector3 at)
    {
        if (crop == BlockWorld.WheatRipe)
        {
            Achievements.Unlock("farm");
            ItemDrop.Spawn(WheatItem, 1, at);
            ItemDrop.Spawn(Seeds, Random.Range(1, 3), at);
        }
        else
        {
            ItemDrop.Spawn(Seeds, 1, at);
        }
    }

    // --- Pictures ---

    private static Texture2D seedsPicture, wheatPicture, breadPicture, cookedChickenPicture;

    static Texture2D FarmPicture(byte item)
    {
        switch (item)
        {
            case Seeds:
                if (seedsPicture == null) seedsPicture = PixelArt(new[]
                {
                    "........",
                    "..g.....",
                    ".....G..",
                    "...g....",
                    ".G....g.",
                    "....G...",
                    "..g.....",
                    "........",
                });
                return seedsPicture;

            case WheatItem:
                if (wheatPicture == null) wheatPicture = PixelArt(new[]
                {
                    ".y..y..y",
                    "yy.yy.yy",
                    ".yy.yy.y",
                    "..y.y.y.",
                    "...yyy..",
                    "....y...",
                    "...yyy..",
                    "..y.y.y.",
                });
                return wheatPicture;

            case Bread:
                if (breadPicture == null) breadPicture = PixelArt(new[]
                {
                    "........",
                    "........",
                    "..oooo..",
                    ".oyoyoo.",
                    "oooooooo",
                    "obbbbbbo",
                    ".bbbbbb.",
                    "........",
                });
                return breadPicture;

            default:   // cooked chicken
                if (cookedChickenPicture == null) cookedChickenPicture = PixelArt(new[]
                {
                    "...bb...",
                    "..bbbb..",
                    "..bobb..",
                    "..bbbb..",
                    "...bb...",
                    "....w...",
                    "...w.w..",
                    "........",
                });
                return cookedChickenPicture;
        }
    }
}
