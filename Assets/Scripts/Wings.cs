using UnityEngine;

// Wings, made at the crafting table. While they're the thing you're holding
// on the hotbar, hold Space to fly up, and let go to glide gently down
// (FirstPersonPlayer does the flying). You can see them flapping at the
// sides of your view.
//
// This is part of BlockInteractor, like the other things you hold.
public partial class BlockInteractor
{
    const byte Wings = 66;

    private GameObject wingsView;
    private Transform leftWing, rightWing;
    private float flapTime;

    public bool HoldingWings { get { return selected == Wings; } }

    // Two white feathery wings with gold tips, one at each side of the view.
    // They hang straight off the camera, not off the hand.
    void BuildWings()
    {
        wingsView = new GameObject("Wings");
        wingsView.transform.SetParent(cam.transform, false);

        Material feathers = BlockWorld.MakeMaterial(new Color(0.97f, 0.97f, 1.00f));
        Material shadow = BlockWorld.MakeMaterial(new Color(0.82f, 0.84f, 0.92f));
        Material gold = BlockWorld.MakeMaterial(new Color(1.00f, 0.82f, 0.25f));

        leftWing = MakeWing(-1f, feathers, shadow, gold);
        rightWing = MakeWing(1f, feathers, shadow, gold);
        wingsView.SetActive(false);
    }

    Transform MakeWing(float side, Material feathers, Material shadow, Material gold)
    {
        Transform pivot = new GameObject(side < 0 ? "Left Wing" : "Right Wing").transform;
        pivot.SetParent(wingsView.transform, false);
        pivot.localPosition = new Vector3(side * 0.42f, -0.20f, 0.55f);

        // Rows of feathers, longest at the top, fanning out to the side
        WingPart(pivot, new Vector3(side * 0.05f, 0f, 0f), new Vector3(0.10f, 0.10f, 0.08f), gold);
        WingPart(pivot, new Vector3(side * 0.20f, 0.04f, 0f), new Vector3(0.26f, 0.07f, 0.05f), feathers);
        WingPart(pivot, new Vector3(side * 0.18f, -0.03f, 0f), new Vector3(0.22f, 0.06f, 0.05f), shadow);
        WingPart(pivot, new Vector3(side * 0.15f, -0.09f, 0f), new Vector3(0.16f, 0.06f, 0.05f), feathers);
        WingPart(pivot, new Vector3(side * 0.34f, 0.06f, 0f), new Vector3(0.05f, 0.05f, 0.05f), gold);   // a golden tip
        return pivot;
    }

    void WingPart(Transform wing, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = AddHandPart(position, size, material);
        part.transform.SetParent(wing, false);
    }

    // Flap hard while flying, and just stir a little while standing
    void MoveWings()
    {
        wingsView.SetActive(HoldingWings);
        if (!HoldingWings) return;

        FirstPersonPlayer me = GetComponent<FirstPersonPlayer>();
        bool flying = me != null && me.Airborne;

        flapTime += Time.deltaTime * (flying ? 12f : 2f);
        float flap = Mathf.Sin(flapTime) * (flying ? 30f : 5f);

        leftWing.localRotation = Quaternion.Euler(0f, 20f, -flap);
        rightWing.localRotation = Quaternion.Euler(0f, -20f, flap);
    }

    // A little flame, for the inventory (it lives here with the other pictures of things you hold)
    static Texture2D firePicture;

    static Texture2D FirePicture()
    {
        if (firePicture == null)
            firePicture = PixelArt(new[]
            {
                "...y....",
                "...yy...",
                "..yoy...",
                "..yooy.y",
                ".yoroy.y",
                ".yorroyo",
                "yorrrroy",
                ".orrrro.",
            });
        return firePicture;
    }

    // Two little white wings with a gold middle, for the inventory
    static Texture2D wingsPicture;

    static Texture2D WingsPicture()
    {
        if (wingsPicture == null)
            wingsPicture = PixelArt(new[]
            {
                "........",
                "ww....ww",
                "www..www",
                "wwwyywww",
                ".wwyyww.",
                "..w..w..",
                "........",
                "........",
            });
        return wingsPicture;
    }
}
