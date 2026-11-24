using UnityEngine;

// You!
//
//   K:   change how you look - shirt, trousers, skin, hair and hat
//   V:   switch between seeing through your own eyes and seeing yourself from behind
//   F1:  wave      F2: dance      F3: jump for joy
//   B:   open your backpack (27 extra spaces, that go everywhere with you - see Backpack.cs)
//
// Your look is remembered, and is the same in every world.
public partial class PlayerLook : MonoBehaviour
{
    public static bool Dressing { get; private set; }   // the K screen is open

    static readonly Color[] Shirts = { new Color(0.25f, 0.5f, 0.95f), new Color(0.9f, 0.2f, 0.2f), new Color(0.25f, 0.75f, 0.3f), new Color(1f, 0.8f, 0.2f),
                                       new Color(0.6f, 0.3f, 0.85f), new Color(1f, 0.5f, 0.7f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.18f) };
    static readonly Color[] Trousers = { new Color(0.25f, 0.22f, 0.55f), new Color(0.3f, 0.2f, 0.1f), new Color(0.15f, 0.15f, 0.18f), new Color(0.5f, 0.5f, 0.55f),
                                         new Color(0.2f, 0.45f, 0.25f), new Color(0.75f, 0.2f, 0.2f) };
    static readonly Color[] Skins = { new Color(0.95f, 0.78f, 0.62f), new Color(0.85f, 0.65f, 0.48f), new Color(0.65f, 0.45f, 0.3f), new Color(0.42f, 0.28f, 0.18f) };
    static readonly Color[] Hairs = { new Color(0.35f, 0.22f, 0.1f), new Color(0.1f, 0.08f, 0.06f), new Color(0.95f, 0.8f, 0.35f), new Color(0.8f, 0.35f, 0.15f),
                                      new Color(0.6f, 0.6f, 0.65f), new Color(0.45f, 0.75f, 1f) };
    static readonly string[] Hats = { "No hat", "Cap", "Crown", "Wizard hat", "Top hat", "Party hat" };

    private Transform model, head, leftArm, rightArm, leftLeg, rightLeg, hat;
    private Camera behind;
    private Camera eyes;
    private bool thirdPerson;
    private string emote = "";
    private float emoteUntil;
    private Vector3 lastPosition;
    private float walkCycle;
    private GUIStyle title;

    static int Get(string what, int fallback) { return PlayerPrefs.GetInt("look-" + what, fallback); }
    static void Set(string what, int value) { PlayerPrefs.SetInt("look-" + what, value); }

    void Start()
    {
        eyes = Camera.main;
        BuildModel();
        lastPosition = transform.position;
    }

    void OnDestroy()
    {
        Dressing = false;
        if (behind != null) Destroy(behind.gameObject);
        if (eyes != null) eyes.cullingMask = ~0;
    }

    // --- Building you out of boxes (the same shape as the villagers, but taller) ---

    void BuildModel()
    {
        if (model != null) Destroy(model.gameObject);
        model = new GameObject("You").transform;
        model.SetParent(transform, false);
        model.localScale = Vector3.one * 1.75f;

        Material shirt = BlockWorld.MakeMaterial(Shirts[Get("shirt", 0) % Shirts.Length]);
        Material legs = BlockWorld.MakeMaterial(Trousers[Get("trousers", 0) % Trousers.Length]);
        Material skin = BlockWorld.MakeMaterial(Skins[Get("skin", 0) % Skins.Length]);
        Material hair = BlockWorld.MakeMaterial(Hairs[Get("hair", 0) % Hairs.Length]);
        Material black = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));

        Part(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.3f, 0.44f, 0.18f), shirt);
        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg }) Part(leg, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.36f, 0.14f), legs);
        leftArm = Pivot(model, new Vector3(-0.21f, 0.78f, 0f));
        rightArm = Pivot(model, new Vector3(0.21f, 0.78f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            Part(arm, new Vector3(0f, -0.1f, 0f), new Vector3(0.11f, 0.2f, 0.11f), shirt);
            Part(arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.09f, 0.18f, 0.09f), skin);
        }
        head = Pivot(model, new Vector3(0f, 0.80f, 0f));
        Part(head, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), skin);
        Part(head, new Vector3(0f, 0.29f, -0.01f), new Vector3(0.32f, 0.06f, 0.33f), hair);
        Part(head, new Vector3(0f, 0.18f, -0.15f), new Vector3(0.32f, 0.24f, 0.04f), hair);
        Part(head, new Vector3(-0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);
        Part(head, new Vector3(0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);
        Part(head, new Vector3(0f, 0.07f, 0.151f), new Vector3(0.1f, 0.02f, 0.01f), black);

        // Armour, if you're wearing some (survival)
        wornArmour = PlayerHealth.Instance != null ? PlayerHealth.Instance.Armour : 0;
        if (wornArmour > 0)
        {
            Color metal = wornArmour == 1 ? new Color(0.55f, 0.34f, 0.18f) : wornArmour == 2 ? new Color(0.82f, 0.83f, 0.86f) : new Color(0.40f, 0.92f, 0.92f);
            Material plate = BlockWorld.MakeMaterial(metal);
            Part(model, new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.4f, 0.2f), plate);                  // chest plate
            foreach (Transform arm in new[] { leftArm, rightArm })
                Part(arm, new Vector3(0f, -0.04f, 0f), new Vector3(0.13f, 0.1f, 0.13f), plate);             // shoulder pads
            foreach (Transform leg in new[] { leftLeg, rightLeg })
                Part(leg, new Vector3(0f, -0.26f, 0f), new Vector3(0.145f, 0.16f, 0.155f), plate);          // shin guards
            Part(head, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.33f, 0.1f, 0.34f), plate);              // helmet
            Part(head, new Vector3(0f, 0.17f, -0.16f), new Vector3(0.33f, 0.26f, 0.04f), plate);
        }

        hat = Pivot(head, new Vector3(0f, 0.32f, 0f));
        switch (Get("hat", 0) % Hats.Length)
        {
            case 1:   // a cap, with a peak at the front
                Material cap = BlockWorld.MakeMaterial(Shirts[(Get("shirt", 0) + 1) % Shirts.Length]);
                Part(hat, new Vector3(0f, 0.02f, 0f), new Vector3(0.33f, 0.07f, 0.33f), cap);
                Part(hat, new Vector3(0f, -0.01f, 0.2f), new Vector3(0.3f, 0.02f, 0.12f), cap);
                break;
            case 2:   // a golden crown
                Material gold = BlockWorld.MakeMaterial(new Color(1f, 0.82f, 0.2f));
                Part(hat, new Vector3(0f, 0.02f, 0f), new Vector3(0.32f, 0.06f, 0.32f), gold);
                for (int i = 0; i < 4; i++) Part(hat, new Vector3(i % 2 == 0 ? -0.12f : 0.12f, 0.08f, i < 2 ? -0.12f : 0.12f), new Vector3(0.06f, 0.08f, 0.06f), gold);
                break;
            case 3:   // a tall pointy wizard hat
                Material blue = BlockWorld.MakeMaterial(new Color(0.25f, 0.2f, 0.7f));
                Part(hat, new Vector3(0f, 0.01f, 0f), new Vector3(0.42f, 0.03f, 0.42f), blue);
                Part(hat, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.16f, 0.24f), blue);
                Part(hat, new Vector3(0f, 0.24f, 0f), new Vector3(0.14f, 0.14f, 0.14f), blue);
                Part(hat, new Vector3(0f, 0.34f, 0f), new Vector3(0.06f, 0.08f, 0.06f), BlockWorld.MakeMaterial(new Color(1f, 0.9f, 0.3f)));
                break;
            case 4:   // a smart top hat
                Material top = BlockWorld.MakeMaterial(new Color(0.08f, 0.08f, 0.1f));
                Part(hat, new Vector3(0f, 0.01f, 0f), new Vector3(0.4f, 0.03f, 0.4f), top);
                Part(hat, new Vector3(0f, 0.13f, 0f), new Vector3(0.26f, 0.22f, 0.26f), top);
                break;
            case 5:   // a stripy party hat
                Part(hat, new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.2f), BlockWorld.MakeMaterial(new Color(1f, 0.4f, 0.7f)));
                Part(hat, new Vector3(0f, 0.17f, 0f), new Vector3(0.12f, 0.1f, 0.12f), BlockWorld.MakeMaterial(new Color(0.4f, 0.9f, 1f)));
                Part(hat, new Vector3(0f, 0.26f, 0f), new Vector3(0.06f, 0.08f, 0.06f), BlockWorld.MakeMaterial(new Color(1f, 0.9f, 0.2f)));
                break;
        }
        ShowModel(thirdPerson);
    }

    static Transform Pivot(Transform parent, Vector3 at)
    {
        Transform pivot = new GameObject("Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = at;
        return pivot;
    }

    static void Part(Transform parent, Vector3 at, Vector3 size, Material look)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = look;
    }

    void ShowModel(bool show)
    {
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>()) part.enabled = show && Current == Form.You;
        ShowDisguise(show);   // (Transform.cs)
    }

    // --- Every frame: keys, the camera behind you, and moving your arms and legs ---

    private int wornArmour;

    void Update()
    {
        if (Tutorial.IsOpen) return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.Armour != wornArmour) BuildModel();   // put some armour on
        if (Dressing)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.K)) CloseDressing();
            return;
        }
        if (BlockInteractor.InventoryOpen || Mods.ScreenOpen) return;

        if (Input.GetKeyDown(KeyCode.V)) SwitchView();
        if (Input.GetKeyDown(KeyCode.K)) OpenDressing();
        if (Input.GetKeyDown(KeyCode.F1)) Emote("wave", "*waves*");
        if (Input.GetKeyDown(KeyCode.F2)) Emote("dance", "*dances*");
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Emote("cheer", "Woo-hoo!");
            GetComponent<FirstPersonPlayer>().LaunchUp(7f);
        }
        TransformKeys();   // T: turn into a creature (Transform.cs)
    }

    void Emote(string which, string words)
    {
        emote = which;
        emoteUntil = Time.time + 2.5f;
        PortalTraveller.Announce(words);
        Sounds.Pop(transform.position);
        Achievements.Unlock("emote");
    }

    void SwitchView()
    {
        thirdPerson = !thirdPerson;
        ShowModel(thirdPerson);
        if (thirdPerson && behind == null)
        {
            behind = new GameObject("Camera Behind You").AddComponent<Camera>();
            behind.depth = eyes.depth + 1f;
            behind.fieldOfView = eyes.fieldOfView;
        }
        if (behind != null) behind.enabled = thirdPerson;
        eyes.cullingMask = thirdPerson ? 0 : ~0;   // the eyes still aim, but the camera behind does the seeing
        PortalTraveller.Announce(thirdPerson ? "Now you can see yourself! (V to go back)" : "Back to looking through your own eyes.");
    }

    void LateUpdate()
    {
        if (model == null) return;

        // Walking: legs and arms swing
        Vector3 moved = transform.position - lastPosition;
        moved.y = 0f;
        lastPosition = transform.position;
        float speed = moved.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
        walkCycle += speed * Time.deltaTime * 2.2f;
        float swing = speed > 0.3f ? Mathf.Sin(walkCycle) * 35f : 0f;
        MoveDisguise(speed);   // (Transform.cs)
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
        model.localRotation = Quaternion.identity;
        head.localRotation = Quaternion.Euler(Mathf.Clamp(GetComponent<FirstPersonPlayer>().Pitch, -40f, 40f), 0f, 0f);

        // Emotes
        if (Time.time < emoteUntil)
        {
            float t = Time.time * 10f;
            if (emote == "wave") rightArm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(t) * 25f);
            else if (emote == "dance")
            {
                model.localRotation = Quaternion.Euler(0f, Time.time * 360f, 0f);
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -120f + Mathf.Sin(t) * 30f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 120f - Mathf.Sin(t) * 30f);
            }
            else
            {
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -160f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 160f);
            }
        }

        // The camera behind you, pulled in closer if there's a wall in the way
        if (thirdPerson && behind != null && Dressing)
        {
            behind.clearFlags = eyes.clearFlags;
            behind.backgroundColor = eyes.backgroundColor;
            Vector3 face = transform.position + Vector3.up * 1.4f;
            behind.transform.position = face + transform.forward * 3f + Vector3.up * 0.3f - transform.right * 0.8f;
            behind.transform.rotation = Quaternion.LookRotation(face - behind.transform.position);
        }
        else if (thirdPerson && behind != null)
        {
            behind.clearFlags = eyes.clearFlags;
            behind.backgroundColor = eyes.backgroundColor;
            // Over your right shoulder, so you're off to one side and the
            // middle of the screen is still what you're pointing at
            Vector3 eye = eyes.transform.position;
            Vector3 back = -eyes.transform.forward * 3.2f + eyes.transform.right * 1.1f + Vector3.up * 0.4f;
            float distance = 1f;
            for (float d = 0.1f; d <= 1f; d += 0.05f)
            {
                Vector3Int cell = Vector3Int.RoundToInt(eye + back * d);
                byte type = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);
                if (type != BlockWorld.Air && type != BlockWorld.Water) { distance = Mathf.Max(0.1f, d - 0.1f); break; }
            }
            behind.transform.position = eye + back * distance;
            behind.transform.rotation = Quaternion.LookRotation(eye + eyes.transform.forward * 20f - behind.transform.position);
        }
    }

    // --- The dressing-up screen (K) ---

    void OpenDressing()
    {
        Dressing = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        switchedForDressing = !thirdPerson;
        if (!thirdPerson) SwitchView();   // so you can see yourself
    }

    void CloseDressing()
    {
        Dressing = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (switchedForDressing && thirdPerson) SwitchView();   // back to looking through your own eyes
        switchedForDressing = false;
    }

    private bool switchedForDressing;

    void OnGUI()
    {
        if (!Dressing) return;
        if (title == null) title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        Rect box = new Rect(20f, 60f, 360f, 420f);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x, box.y + 8f, box.width, 28f), "How do you look?", title);
        float y = box.y + 50f;
        y = Choice(box, y, "Shirt", "shirt", Shirts.Length, Shirts);
        y = Choice(box, y, "Trousers", "trousers", Trousers.Length, Trousers);
        y = Choice(box, y, "Skin", "skin", Skins.Length, Skins);
        y = Choice(box, y, "Hair", "hair", Hairs.Length, Hairs);

        GUI.Label(new Rect(box.x + 20f, y, 100f, 24f), "Hat");
        if (GUI.Button(new Rect(box.x + 110f, y, 230f, 26f), "< " + Hats[Get("hat", 0) % Hats.Length] + " >"))
        {
            Set("hat", (Get("hat", 0) + 1) % Hats.Length);
            BuildModel();
        }
        y += 50f;
        if (GUI.Button(new Rect(box.x + 90f, y, 180f, 34f), "Done (K)")) CloseDressing();
        GUI.Label(new Rect(box.x, box.yMax - 30f, box.width, 24f), "V: see yourself, or through your eyes", title.fontSize > 0 ? GUI.skin.label : GUI.skin.label);
    }

    float Choice(Rect box, float y, string label, string what, int count, Color[] colours)
    {
        GUI.Label(new Rect(box.x + 20f, y, 100f, 24f), label);
        float size = Mathf.Min(28f, 230f / count - 4f);
        for (int i = 0; i < count; i++)
        {
            Rect swatch = new Rect(box.x + 110f + i * (size + 4f), y, size, size);
            Color before = GUI.color;
            if (Get(what, 0) % count == i)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(swatch.x - 2f, swatch.y - 2f, swatch.width + 4f, swatch.height + 4f), Texture2D.whiteTexture);
            }
            GUI.color = colours[i];
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = before;
            if (Event.current.type == EventType.MouseDown && swatch.Contains(Event.current.mousePosition))
            {
                Set(what, i);
                BuildModel();
                Event.current.Use();
            }
        }
        return y + 50f;
    }
}
