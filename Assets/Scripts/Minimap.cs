using System.Collections.Generic;
using UnityEngine;

// A little map in the top corner of the screen, looking down on the land
// around you (north is at the top). The white arrow is you. Higher ground is
// lighter, lower ground darker, and water is blue. Press N to hide or show it.
public class Minimap : MonoBehaviour
{
    const int Blocks = 48;        // how many blocks across it shows
    const float Size = 132f;      // how big it is on screen

    private Texture2D picture;
    private Texture2D arrow;
    private float redrawTimer;
    private bool hidden;
    private readonly Dictionary<byte, Color> colours = new Dictionary<byte, Color>();

    void Start()
    {
        picture = new Texture2D(Blocks, Blocks);
        picture.filterMode = FilterMode.Point;
        picture.wrapMode = TextureWrapMode.Clamp;
        arrow = BlockInteractor.PixelArt(new[]
        {
            "...ww...",
            "...ww...",
            "..wwww..",
            "..wwww..",
            ".wwwwww.",
            ".wwwwww.",
            "www..www",
            "ww....ww",
        });
    }

    void OnDestroy()
    {
        if (picture != null) Destroy(picture);
        if (arrow != null) Destroy(arrow);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N) && !BlockInteractor.InventoryOpen && !Mods.ScreenOpen) hidden = !hidden;

        redrawTimer -= Time.deltaTime;
        if (redrawTimer > 0f || hidden || BlockWorld.Instance == null) return;
        redrawTimer = 0.5f;
        Redraw();
    }

    // The average colour of a block's picture
    Color ColourOf(byte type)
    {
        Color colour;
        if (colours.TryGetValue(type, out colour)) return colour;

        colour = new Color(0.5f, 0.5f, 0.5f);
        Material look = BlockWorld.Instance.MaterialFor(type);
        if (look != null)
        {
            colour = look.color;
            Texture2D texture = look.mainTexture as Texture2D;
            if (texture != null && texture.isReadable)
            {
                Color sum = Color.black;
                Color[] pixels = texture.GetPixels();
                foreach (Color p in pixels) sum += p;
                colour = sum / pixels.Length;
            }
        }
        if (type == BlockWorld.Water) colour = new Color(0.2f, 0.4f, 0.85f);
        colour.a = 1f;
        colours[type] = colour;
        return colour;
    }

    void Redraw()
    {
        BlockWorld world = BlockWorld.Instance;
        int cx = Mathf.RoundToInt(transform.position.x), cz = Mathf.RoundToInt(transform.position.z);
        int myHeight = Mathf.RoundToInt(transform.position.y);

        for (int px = 0; px < Blocks; px++)
            for (int pz = 0; pz < Blocks; pz++)
            {
                int x = cx - Blocks / 2 + px, z = cz - Blocks / 2 + pz;
                if (x < 0 || z < 0 || x >= BlockWorld.SizeX || z >= BlockWorld.SizeZ)
                {
                    picture.SetPixel(px, pz, new Color(0.08f, 0.08f, 0.1f));
                    continue;
                }

                int top = world.GetSurfaceHeight(x, z);
                if (top < 0) { picture.SetPixel(px, pz, Color.black); continue; }
                Color colour = ColourOf(world.GetBlock(x, top, z));
                float shade = Mathf.Clamp(1f + (top - myHeight) * 0.04f, 0.55f, 1.35f);
                picture.SetPixel(px, pz, colour * shade);
            }
        picture.Apply();
    }

    void OnGUI()
    {
        if (hidden || picture == null || BlockInteractor.InventoryOpen) return;

        Rect map = new Rect(Screen.width - Size - 14f, 36f, Size, Size);
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(map.x - 3, map.y - 3, map.width + 6, map.height + 6), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(map, picture);
        GUI.Label(new Rect(map.center.x - 5f, map.y - 2f, 20f, 20f), "N");

        // You, in the middle, pointing the way you're facing
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(transform.eulerAngles.y, map.center);
        GUI.DrawTexture(new Rect(map.center.x - 6f, map.center.y - 6f, 12f, 12f), arrow);
        GUI.matrix = old;
        GUI.color = before;
    }
}
