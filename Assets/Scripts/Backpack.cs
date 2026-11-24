using UnityEngine;

// Your backpack: press B to open it. 27 extra spaces that go everywhere
// with you - to the Magic Land, the Nether, even the Candy Land. It works
// just like a chest. (Each world has its own backpack.)
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    private bool backpackOpen;
    private BlockWorld.ChestContents backpack;

    static string BackpackKey { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-backpack"; } }

    BlockWorld.ChestContents Backpack
    {
        get
        {
            if (backpack != null) return backpack;
            backpack = new BlockWorld.ChestContents();
            string[] saved = PlayerPrefs.GetString(BackpackKey, "").Split(',');
            for (int i = 0; i + 1 < saved.Length && i / 2 < BlockWorld.ChestContents.Spaces; i += 2)
            {
                byte item, count;
                if (byte.TryParse(saved[i], out item) && byte.TryParse(saved[i + 1], out count))
                {
                    backpack.items[i / 2] = item;
                    backpack.counts[i / 2] = count;
                }
            }
            return backpack;
        }
    }

    void OpenBackpack()
    {
        OpenInventory();
        chestOpen = true;
        backpackOpen = true;
        Sounds.Whoosh(transform.position);
    }

    void SaveBackpack()
    {
        if (backpack == null) return;
        string[] parts = new string[BlockWorld.ChestContents.Spaces * 2];
        for (int i = 0; i < BlockWorld.ChestContents.Spaces; i++)
        {
            parts[i * 2] = backpack.items[i].ToString();
            parts[i * 2 + 1] = backpack.counts[i].ToString();
        }
        PlayerPrefs.SetString(BackpackKey, string.Join(",", parts));
    }
}
