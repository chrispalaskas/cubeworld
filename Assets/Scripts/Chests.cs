using UnityEngine;

// The chest screen. Aim at a chest and press E (or right click) to open it.
// It has 27 spaces, and each one holds up to 64 of one kind of block.
//
//   Left click a space:  put in one of whatever's in your hand (hold Shift for 10)
//   Right click a space: take one out, into your hand
//
// Everything stays inside when you close it, and it's saved with the world.
// Break the chest (or blow it up) and whatever was inside is lost.
//
// This is part of BlockInteractor - it shares the hotbar.
public partial class BlockInteractor
{
    const int ChestColumns = 9;

    private bool chestOpen;
    private Vector3Int openChestAt;
    private int chestCursor;
    private string chestMessage = "";
    private float chestMessageTime = -100f;

    bool LookingAtChest(out Vector3Int chest)
    {
        Vector3Int unused;
        return LookingAtBlock(out chest, out unused)
            && BlockWorld.Instance.GetBlock(chest.x, chest.y, chest.z) == BlockWorld.Chest;
    }

    void OpenChest(Vector3Int where)
    {
        OpenInventory();
        chestOpen = true;
        openChestAt = where;
    }

    BlockWorld.ChestContents OpenChestContents()
    {
        return BlockWorld.Instance.ChestAt(openChestAt);
    }

    void ChestKeys()
    {
        // Close it - or if the chest has gone (TNT!), close it for you
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) ||
            BlockWorld.Instance.GetBlock(openChestAt.x, openChestAt.y, openChestAt.z) != BlockWorld.Chest)
        {
            CloseInventory();
            return;
        }

        // Arrows move around the spaces
        int count = BlockWorld.ChestContents.Spaces;
        if (Input.GetKeyDown(KeyCode.RightArrow)) chestCursor = (chestCursor + 1) % count;
        if (Input.GetKeyDown(KeyCode.LeftArrow))  chestCursor = (chestCursor + count - 1) % count;
        if (Input.GetKeyDown(KeyCode.DownArrow))  chestCursor = (chestCursor + ChestColumns) % count;
        if (Input.GetKeyDown(KeyCode.UpArrow))    chestCursor = (chestCursor + count - ChestColumns) % count;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (EnterPressed())
            PutInChest(chestCursor, shift ? 10 : 1);
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Q))
            TakeFromChest(chestCursor);
    }

    // Puts some of what's in your hand into a space - an empty one, or one
    // that already has the same kind of block in it
    void PutInChest(int space, int howMany)
    {
        BlockWorld.ChestContents inside = OpenChestContents();
        byte holding = selected;

        if (inside.items[space] != BlockWorld.Air && inside.items[space] != holding)
        {
            ChestSays("That space already has " + ItemNames[inside.items[space]] + " in it.");
            return;
        }
        if (inside.counts[space] >= BlockWorld.ChestContents.MostInASpace)
        {
            ChestSays("That space is full - 64 is the most it can hold.");
            return;
        }

        inside.items[space] = holding;
        inside.counts[space] = (byte)Mathf.Min(inside.counts[space] + howMany, BlockWorld.ChestContents.MostInASpace);
    }

    // Takes one out of a space, and puts that kind of block in your hand
    void TakeFromChest(int space)
    {
        BlockWorld.ChestContents inside = OpenChestContents();
        byte item = inside.items[space];
        if (item == BlockWorld.Air) return;

        inside.counts[space]--;
        if (inside.counts[space] == 0) inside.items[space] = BlockWorld.Air;

        // Treasure can hold things you haven't made yet - finding one counts!
        bool found = !HaveGot(item);
        if (found)
        {
            crafted.Add(item);
            SaveCrafted();
        }

        PutInHotbar(item);
        ChestSays((found ? "You found a " + ItemNames[item] + "! It's in your inventory now, and in hotbar slot "
                         : "Took out 1 " + ItemNames[item] + " - it's in hotbar slot ") + SlotKeys[slot] + ".");
    }

    void ChestSays(string words)
    {
        chestMessage = words;
        chestMessageTime = Time.time;
    }

    void DrawChest()
    {
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        // Laid out on a 640 x 360 panel, shrunk to fit small windows
        const float width = 640f, height = 360f;
        float scale = Mathf.Min(1f, (Screen.width - 32f) / width, (Screen.height - 120f) / height);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - width * scale) / 2f, Mathf.Max(8f, (Screen.height - 90f - height * scale) / 2f), 0f),
            Quaternion.identity, Vector3.one * scale);

        Event e = Event.current;
        BlockWorld.ChestContents inside = OpenChestContents();

        GUI.Box(new Rect(0, 0, width, height), "");
        GUI.Label(new Rect(0, 8, width, 24), "Chest", centredLabel);
        GUI.Label(new Rect(20, 32, width - 40, 24),
                  "Holding: " + ItemNames[selected] + "   (pick a different block with 1-9, 0, or by clicking the hotbar)");

        const float cell = 58f, gap = 8f;
        float left = (width - (ChestColumns * (cell + gap) - gap)) / 2f;
        const float top = 70f;

        for (int i = 0; i < BlockWorld.ChestContents.Spaces; i++)
        {
            Rect box = new Rect(left + (i % ChestColumns) * (cell + gap), top + (i / ChestColumns) * (cell + gap), cell, cell);

            if (e.type == EventType.MouseDown && box.Contains(e.mousePosition))
            {
                chestCursor = i;
                if (e.button == 1) TakeFromChest(i);
                else PutInChest(i, e.shift ? 10 : 1);
                e.Use();
            }

            DrawSlot(box, i == chestCursor ? Color.yellow : (Color?)null);
            if (inside.items[i] != BlockWorld.Air)
            {
                DrawItemIcon(Shrink(box, 0.18f), inside.items[i]);
                GUI.Label(new Rect(box.x + 4f, box.yMax - 22f, box.width - 8f, 20f), inside.counts[i].ToString());
            }
        }

        // What's in the space the cursor is on
        float under = top + 3 * (cell + gap) + 4f;
        byte pointedAt = inside.items[chestCursor];
        GUI.Label(new Rect(0, under, width, 24),
                  pointedAt == BlockWorld.Air ? "(empty)" : inside.counts[chestCursor] + " x " + ItemNames[pointedAt], centredLabel);

        if (Time.time - chestMessageTime < 3f)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(0, under + 24f, width, 24), chestMessage, centredLabel);
            GUI.color = before;
        }

        GUI.Label(new Rect(0, height - 64f, width, 24),
                  "Left click: put one in (Shift: 10)     Right click: take one out", centredLabel);
        GUI.Label(new Rect(0, height - 42f, width, 24),
                  "Keys: arrows to move, Enter to put in, Backspace to take out", centredLabel);
        GUI.Label(new Rect(0, height - 22f, width, 24), "E or Esc: close", centredLabel);

        GUI.matrix = oldMatrix;
    }
}
