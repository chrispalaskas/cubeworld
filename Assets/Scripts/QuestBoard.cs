using UnityEngine;

// The quest board in the village: three jobs at a time, for bigger rewards
// than the villagers give. Bring what a job asks for and press "Hand over".
// The jobs change every morning - and the third one always has a diamond
// as a bonus prize!
public static class QuestBoardDays
{
    public static string Key(string what) { return "world" + (WorldMenu.CurrentSlot + 1) + "-" + what; }

    public static int Round { get { return PlayerPrefs.GetInt(Key("board-round"), 0); } }
    public static int Done { get { return PlayerPrefs.GetInt(Key("board-done"), 0); } set { PlayerPrefs.SetInt(Key("board-done"), value); } }

    // A new morning: new jobs on the board
    public static void NewDay()
    {
        if (WorldMenu.CurrentSlot < 0) return;
        PlayerPrefs.SetInt(Key("board-round"), Round + 1);
        Done = 0;
    }
}

public partial class BlockInteractor
{
    private bool readingBoard;

    void OpenBoard()
    {
        OpenInventory();
        readingBoard = true;
    }

    void BoardKeys()
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
            CloseInventory();
    }

    // Job number "which" today: what, how many, and how many coins
    static void BoardJob(int which, out byte item, out int count, out int reward)
    {
        int round = QuestBoardDays.Round;
        int pick = (((WorldMenu.CurrentSlot * 31 + round * 7 + which * 13) * 2654435) & 0x7fffffff) % QuestIdeas.GetLength(0);
        // (no two jobs the same)
        for (int j = 0; j < which; j++)
        {
            byte other; int c, r;
            BoardJob(j, out other, out c, out r);
            if (other == QuestIdeas[pick, 0]) pick = (pick + 1 + which) % QuestIdeas.GetLength(0);
        }
        item = (byte)QuestIdeas[pick, 0];
        int span = QuestIdeas[pick, 2] - QuestIdeas[pick, 1] + 1;
        count = QuestIdeas[pick, 1] + ((round * 5 + which * 3) & 0x7fffffff) % span;
        reward = (QuestIdeas[pick, 3] + count / 3) * 2 + 2;
    }

    void DrawBoard()
    {
        if (talkTitle == null) talkTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = before;

        const float width = 640f, height = 380f;
        float scale = Mathf.Min(1f, (Screen.width - 24f) / width, (Screen.height - 100f) / height);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2f, Mathf.Max(6f, (Screen.height - 86f - height * scale) / 2f), 0f),
                                   Quaternion.identity, Vector3.one * scale);

        // A wooden board, with the jobs pinned to it
        GUI.color = new Color(0.55f, 0.36f, 0.18f, 0.97f);
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
        GUI.color = before;
        GUI.Label(new Rect(0, 10, width, 30), "QUEST BOARD", talkTitle);
        GUI.Label(new Rect(0, 40, width, 22), "You have " + Wallet.Coins + " coins.   New jobs every morning!", centredLabel);

        int done = QuestBoardDays.Done;
        for (int i = 0; i < 3; i++)
        {
            byte item; int count, reward;
            BoardJob(i, out item, out count, out reward);
            Rect note = new Rect(24, 72 + i * 92, width - 48, 82);
            GUI.color = (done & (1 << i)) != 0 ? new Color(0.75f, 0.9f, 0.7f) : new Color(0.97f, 0.93f, 0.8f);
            GUI.DrawTexture(note, Texture2D.whiteTexture);
            GUI.color = Color.black;
            DrawItemIcon(new Rect(note.x + 12, note.y + 18, 46, 46), item);
            string bonus = i == 2 ? "  + a Diamond!" : "";
            GUI.Label(new Rect(note.x + 70, note.y + 12, 360, 24), "Bring " + count + " " + ItemNames[item]);
            GUI.Label(new Rect(note.x + 70, note.y + 38, 360, 24), "Reward: " + reward + " coins" + bonus);
            GUI.color = before;

            if ((done & (1 << i)) != 0)
            {
                GUI.color = new Color(0.1f, 0.45f, 0.1f);
                GUI.Label(new Rect(note.xMax - 150, note.y + 28, 140, 24), "DONE!", talkTitle);
                GUI.color = before;
            }
            else if (Have(item, count))
            {
                if (GUI.Button(new Rect(note.xMax - 160, note.y + 22, 146, 38), "Hand over"))
                {
                    if (Survival) UseUp(item, count);
                    QuestBoardDays.Done = done | (1 << i);
                    Wallet.Earn(reward, "Job done! Thank you!");
                    if (i == 2) GiveItem(BlockWorld.Diamond, 1);
                    Achievements.Unlock("board");
                    Achievements.Unlock("quest");
                }
            }
            else
            {
                GUI.color = Color.black;
                GUI.Label(new Rect(note.xMax - 160, note.y + 30, 150, 24), Survival ? "You've got " + HowMany(item) : "Find some first!");
                GUI.color = before;
            }
        }

        GUI.Label(new Rect(0, height - 28, width, 24), "E or Esc: close", centredLabel);
        GUI.matrix = old;
    }
}
