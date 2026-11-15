using UnityEngine;

// Sits on the player and watches what you're standing on.
//
// Step onto a portal and you pop out on top of the next portal in the same
// world (with three or more, they take turns). Step onto a magic portal and
// you travel to the Magic Land - or, if you're already there, back home.
//
// You arrive standing on a portal, so nothing happens again until you've
// stepped off it. Otherwise you'd bounce back and forth forever.
public class PortalTraveller : MonoBehaviour
{
    private static PortalTraveller instance;
    private bool waitingToStepOff = true;   // true at the start too, in case a save put us on a portal
    private string message = "";
    private float messageUntil;
    private GUIStyle messageStyle;

    void Awake()
    {
        instance = this;
    }

    // Shows a message in the middle of the screen, like "Luna went through the portal!"
    public static void Announce(string words)
    {
        if (instance != null) instance.Say(words);
    }

    void Update()
    {
        BlockWorld world = BlockWorld.Instance;
        if (world == null) return;

        Vector3Int under = Vector3Int.RoundToInt(transform.position + Vector3.down * 0.3f);
        byte type = world.GetBlock(under.x, under.y, under.z);
        bool onPortal = type == BlockWorld.Portal || type == BlockWorld.MagicPortal || type == BlockWorld.FirePortal;

        if (!onPortal)
        {
            waitingToStepOff = false;
            return;
        }
        if (waitingToStepOff) return;
        waitingToStepOff = true;

        if (type == BlockWorld.MagicPortal)
            WorldMenu.TravelThroughMagicPortal();
        else if (type == BlockWorld.FirePortal)
            WorldMenu.TravelThroughFirePortal();
        else
            JumpToNextPortal(world, under);
    }

    void JumpToNextPortal(BlockWorld world, Vector3Int from)
    {
        Vector3Int to;
        if (!FindNextPortal(world, from, out to))
        {
            Say("Build another portal, and this one will take you there!");
            return;
        }

        // A CharacterController ignores teleports unless we switch it off first
        CharacterController body = GetComponent<CharacterController>();
        body.enabled = false;
        transform.position = new Vector3(to.x, to.y + 0.6f, to.z);   // standing on top of it
        body.enabled = true;

        Say("Whoosh!");
    }

    // Goes through every block in the world in order, starting just after
    // the portal we're on and wrapping round, so each portal leads to the next
    public static bool FindNextPortal(BlockWorld world, Vector3Int from, out Vector3Int found)
    {
        int sizeYZ = BlockWorld.SizeY * BlockWorld.SizeZ;
        int total = BlockWorld.SizeX * sizeYZ;
        int start = from.x * sizeYZ + from.y * BlockWorld.SizeZ + from.z;

        for (int step = 1; step < total; step++)
        {
            int i = (start + step) % total;
            int x = i / sizeYZ, y = (i / BlockWorld.SizeZ) % BlockWorld.SizeY, z = i % BlockWorld.SizeZ;

            if (world.GetBlock(x, y, z) != BlockWorld.Portal) continue;

            // Skip portals that have been built over - you'd arrive stuck inside a wall
            if (world.GetBlock(x, y + 1, z) != BlockWorld.Air || world.GetBlock(x, y + 2, z) != BlockWorld.Air) continue;

            found = new Vector3Int(x, y, z);
            return true;
        }

        found = from;
        return false;
    }

    void Say(string words)
    {
        message = words;
        messageUntil = Time.time + 2.5f;
    }

    void OnGUI()
    {
        if (Time.time > messageUntil) return;

        if (messageStyle == null)
            messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40), message, messageStyle);
    }
}
