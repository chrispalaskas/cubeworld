using System.Collections.Generic;
using UnityEngine;

// Talking to villagers: press E on one.
//
//   Quests:  a villager asks you for something - "Bring me 5 apples!" -
//            and gives you coins when you bring it. One quest at a time.
//   Trading: every villager has a few things to sell for coins, and buys a
//            few things from you. What they trade depends on who they are.
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    // What villagers might ask for: item, fewest, most, coins for it
    static readonly int[,] QuestIdeas =
    {
        { 94, 3, 6, 3 },                   // apples
        { 169, 4, 8, 4 },                  // wheat
        { BlockWorld.RawFish, 2, 4, 4 },
        { BlockWorld.WoolFirst, 3, 6, 3 },
        { BlockWorld.Iron, 3, 6, 5 },
        { 170, 2, 4, 5 },                  // bread
        { BlockWorld.Planks, 10, 20, 2 },
        { BlockWorld.Diamond, 1, 2, 10 },
        { BlockWorld.FlowerFirst, 2, 4, 3 },
        { BlockWorld.CookedFish, 2, 3, 5 },
        { BlockWorld.Leather, 2, 4, 4 },
        { BlockWorld.Feather, 3, 6, 3 },
    };

    // Things to buy: item, how many, coins
    static readonly int[,] ForSale =
    {
        { BlockWorld.Diamond, 1, 8 }, { 95, 1, 6 }, { BlockWorld.TreasureMap, 1, 5 }, { BlockWorld.Bow, 1, 4 },
        { BlockWorld.Arrow, 8, 2 }, { 28, 2, 1 }, { 168, 4, 1 }, { BlockWorld.IronArmour, 1, 12 },
        { 66, 1, 20 }, { BlockWorld.Steak, 3, 3 }, { 254, 1, 4 }, { 251, 1, 4 }, { BlockWorld.Lantern, 2, 2 },
        { 245, 1, 10 }, { BlockWorld.Compass, 1, 4 }, { BlockWorld.Boat, 1, 3 },
    };

    // Things they'll buy from you: item, how many, coins
    static readonly int[,] WantToBuy =
    {
        { 169, 5, 2 }, { BlockWorld.RawFish, 3, 2 }, { BlockWorld.WoolFirst, 4, 2 }, { BlockWorld.Iron, 3, 3 },
        { 94, 4, 1 }, { BlockWorld.Gold, 2, 3 }, { BlockWorld.Leather, 3, 2 }, { BlockWorld.StringItem, 4, 2 },
    };

    private bool talking;
    private Villager talkingTo;
    private GUIStyle talkTitle;

    bool LookingAtVillager(out Villager person)
    {
        person = AnimalInSights(5f) as Villager;
        return person != null;
    }

    void StartTalking(Villager person)
    {
        OpenInventory();
        talking = true;
        talkingTo = person;
    }

    void TalkKeys()
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) || talkingTo == null)
            CloseInventory();
    }

    // --- The quest (one at a time, kept for each world) ---

    static string QuestKey { get { return "world" + (WorldMenu.CurrentSlot + 1) + "-quest"; } }

    // item, how many, coins, and who asked
    bool CurrentQuest(out byte item, out int count, out int reward, out string who)
    {
        item = 0; count = 0; reward = 0; who = "";
        string[] bits = PlayerPrefs.GetString(QuestKey, "").Split(':');
        if (bits.Length != 4) return false;
        int i, c, r;
        if (!int.TryParse(bits[0], out i) || !int.TryParse(bits[1], out c) || !int.TryParse(bits[2], out r)) return false;
        item = (byte)i; count = c; reward = r; who = bits[3];
        return true;
    }

    // What this villager would ask for (the same thing each time you ask them)
    void QuestFrom(string name, out byte item, out int count, out int reward)
    {
        int pick = Mathf.Abs((name + System.DateTime.Now.DayOfYear + Wallet.Coins).GetHashCode()) % QuestIdeas.GetLength(0);
        item = (byte)QuestIdeas[pick, 0];
        count = Random.Range(QuestIdeas[pick, 1], QuestIdeas[pick, 2] + 1);
        reward = QuestIdeas[pick, 3] + count / 3;
    }

    bool Have(byte item, int count) { return Survival ? HowMany(item) >= count : HaveGot(item); }

    void GiveItem(byte item, int count)
    {
        if (Survival) Give(item, count);
        else
        {
            if (crafted.Add(item)) SaveCrafted();   // (in creative, buying something unlocks it)
            PutInHotbar(item);
        }
    }

    // --- The screen ---

    void DrawTalking()
    {
        if (talkingTo == null) return;
        if (talkTitle == null) talkTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        const float width = 760f, height = 560f;
        float scale = Mathf.Min(1f, (Screen.width - 24f) / width, (Screen.height - 100f) / height);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f, Mathf.Max(6f, (Screen.height - 86f - height * scale) / 2f), 0f),
                                   Quaternion.identity, Vector3.one * scale);

        GUI.Box(new Rect(0, 0, width, height), "");
        string name = talkingTo.Name;
        GUI.Label(new Rect(0, 8, width, 30), "Talking to " + name, talkTitle);
        GUI.Label(new Rect(0, 38, width, 22), "You have " + Wallet.Coins + " coins", centredLabel);

        // --- The quest ---
        GUI.Box(new Rect(20, 66, width - 40, 110), "");
        byte item; int count, reward; string who;
        if (CurrentQuest(out item, out count, out reward, out who))
        {
            GUI.Label(new Rect(36, 74, width - 70, 24), "Your quest (from " + who + "): bring " + count + " " + ItemNames[item] + ".  Reward: " + reward + " coins");
            DrawItemIcon(new Rect(36, 104, 40, 40), item);
            if (Have(item, count))
            {
                if (GUI.Button(new Rect(90, 108, 260, 36), "Hand over " + count + " " + ItemNames[item]))
                {
                    if (Survival) UseUp(item, count);
                    PlayerPrefs.DeleteKey(QuestKey);
                    Wallet.Earn(reward, "Thank you! Quest done.");
                    Achievements.Unlock("quest");
                }
            }
            else GUI.Label(new Rect(90, 112, 400, 24), Survival ? "You've got " + HowMany(item) + " so far." : "Find some, then come back!");
            if (GUI.Button(new Rect(width - 190, 108, 150, 36), "Give up quest")) PlayerPrefs.DeleteKey(QuestKey);
        }
        else
        {
            QuestFrom(name, out item, out count, out reward);
            GUI.Label(new Rect(36, 74, width - 70, 24), "\"Could you bring me " + count + " " + ItemNames[item] + "? I'll give you " + reward + " coins!\"");
            DrawItemIcon(new Rect(36, 104, 40, 40), item);
            if (GUI.Button(new Rect(90, 108, 200, 36), "Yes, I'll do it!"))
                PlayerPrefs.SetString(QuestKey, item + ":" + count + ":" + reward + ":" + name);
        }

        // --- Trading ---
        int seed = Mathf.Abs(name.GetHashCode());
        GUI.Label(new Rect(20, 186, 340, 24), "Buy from " + name, talkTitle);
        GUI.Label(new Rect(400, 186, 340, 24), "Sell to " + name, talkTitle);

        for (int i = 0; i < 5; i++)
        {
            int pick = (seed + i * 7) % ForSale.GetLength(0);
            byte thing = (byte)ForSale[pick, 0];
            int howMany = ForSale[pick, 1], price = ForSale[pick, 2];
            Rect row = new Rect(20, 216 + i * 56, 340, 50);
            GUI.Box(row, "");
            DrawItemIcon(new Rect(row.x + 8, row.y + 6, 38, 38), thing);
            GUI.Label(new Rect(row.x + 54, row.y + 4, 180, 22), howMany + " " + ItemNames[thing]);
            GUI.Label(new Rect(row.x + 54, row.y + 24, 180, 22), price + " coins");
            GUI.enabled = Wallet.Coins >= price;
            if (GUI.Button(new Rect(row.xMax - 90, row.y + 10, 80, 30), "Buy") && Wallet.Spend(price))
            {
                GiveItem(thing, howMany);
                Sounds.Pop(transform.position);
                Achievements.Unlock("trade");
            }
            GUI.enabled = true;
        }

        for (int i = 0; i < 4; i++)
        {
            int pick = (seed / 3 + i * 3) % WantToBuy.GetLength(0);
            byte thing = (byte)WantToBuy[pick, 0];
            int howMany = WantToBuy[pick, 1], price = WantToBuy[pick, 2];
            Rect row = new Rect(400, 216 + i * 56, 340, 50);
            GUI.Box(row, "");
            DrawItemIcon(new Rect(row.x + 8, row.y + 6, 38, 38), thing);
            GUI.Label(new Rect(row.x + 54, row.y + 4, 180, 22), howMany + " " + ItemNames[thing]);
            GUI.Label(new Rect(row.x + 54, row.y + 24, 180, 22), "for " + price + " coins");
            GUI.enabled = Have(thing, howMany);
            if (GUI.Button(new Rect(row.xMax - 90, row.y + 10, 80, 30), "Sell"))
            {
                if (Survival) UseUp(thing, howMany);
                Wallet.Earn(price, "");
                Achievements.Unlock("trade");
            }
            GUI.enabled = true;
        }

        GUI.Label(new Rect(0, height - 30, width, 24), "Earn coins from quests, selling, and the mini-games in the fun park.   E or Esc: goodbye", centredLabel);
        GUI.matrix = old;
    }
}
