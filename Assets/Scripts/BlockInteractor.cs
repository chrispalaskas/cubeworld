using UnityEngine;
using UnityEngine.Rendering;

// Breaking blocks, placing blocks, and choosing which block to place.
public class BlockInteractor : MonoBehaviour
{
    [Tooltip("How far away you can reach, in blocks")]
    public float reach = 6f;

    // Not a block you can place - a tool that sits in your hand and mops up water
    const byte Sponge = 9;

    // Also a tool: click an animal to catch it, and click again to let it go
    const byte Leash = 10;
    const float LeashReach = 8f;

    private byte selected = BlockWorld.Grass;
    private GameObject hand;
    private GameObject spongeInHand;
    private GameObject leashInHand;
    private GameObject rope;
    private MonoBehaviour caughtAnimal;   // destroyed if TNT gets it, which Unity treats as null
    private float squeezeTime;   // counts down while the sponge dips after a wipe
    private Camera cam;
    private CharacterController body;

    void Start()
    {
        cam = Camera.main;
        body = GetComponent<CharacterController>();
        BuildHand();
    }

    void Update()
    {
        ChooseBlock();
        MoveHand();

        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Time.frameCount == FirstPersonPlayer.MouseLockedFrame) return;   // that click was just to grab the mouse

        if (Input.GetMouseButtonDown(0))
        {
            if (selected == Sponge)
                WipeWater();
            else if (selected == Leash)
                UseLeash();
            else
                Break();
        }
        else if (Input.GetMouseButtonDown(1))
            Place();
    }

    void ChooseBlock()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) selected = BlockWorld.Grass;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selected = BlockWorld.Dirt;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selected = BlockWorld.Stone;
        if (Input.GetKeyDown(KeyCode.Alpha4)) selected = BlockWorld.Wood;
        if (Input.GetKeyDown(KeyCode.Alpha5)) selected = BlockWorld.Sand;
        if (Input.GetKeyDown(KeyCode.Alpha6)) selected = BlockWorld.Leaves;
        if (Input.GetKeyDown(KeyCode.Alpha7)) selected = BlockWorld.Tnt;
        if (Input.GetKeyDown(KeyCode.Alpha8)) selected = Sponge;
        if (Input.GetKeyDown(KeyCode.Alpha9)) selected = BlockWorld.Water;
        if (Input.GetKeyDown(KeyCode.Alpha0)) selected = Leash;
    }

    // Work out which block we are looking at, and which empty space sits
    // against the face we are pointing at (that is where a new block goes).
    bool LookingAtBlock(out Vector3Int block, out Vector3Int spaceInFront)
    {
        block = Vector3Int.zero;
        spaceInFront = Vector3Int.zero;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, reach)) return false;
        if (BlockWorld.Instance == null) return false;
        if (hit.transform.parent != BlockWorld.Instance.transform) return false;

        // Every cube sits exactly on a whole number, so its position IS its grid cell
        Vector3 p = hit.transform.position;
        block = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
        spaceInFront = block + Vector3Int.RoundToInt(hit.normal);
        return true;
    }

    void Break()
    {
        Vector3Int block, unused;
        if (!LookingAtBlock(out block, out unused)) return;

        BlockWorld.Instance.SetBlock(block.x, block.y, block.z, BlockWorld.Air);
    }

    // Water has no collider for a raycast to hit, so instead we creep along the
    // line of sight checking each block, until we find water or something solid.
    bool LookingAtWater(out Vector3Int water)
    {
        for (float distance = 0f; distance <= reach; distance += 0.05f)
        {
            Vector3 point = cam.transform.position + cam.transform.forward * distance;
            water = Vector3Int.RoundToInt(point);

            byte type = BlockWorld.Instance.GetBlock(water.x, water.y, water.z);
            if (type == BlockWorld.Water) return true;
            if (type != BlockWorld.Air) return false;
        }

        water = Vector3Int.zero;
        return false;
    }

    // Water in your hand has to stack on water, which a raycast would go straight
    // through - so creep along the line of sight, remembering the last empty
    // block we passed. That is the space in front of whatever we reach.
    bool LookingAtSpaceBeforeWater(out Vector3Int space)
    {
        space = Vector3Int.zero;
        bool passedEmptySpace = false;

        for (float distance = 0f; distance <= reach; distance += 0.05f)
        {
            Vector3 point = cam.transform.position + cam.transform.forward * distance;
            Vector3Int cell = Vector3Int.RoundToInt(point);

            if (BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z) == BlockWorld.Air)
            {
                space = cell;
                passedEmptySpace = true;
            }
            else if (passedEmptySpace)
            {
                return true;
            }
        }

        return false;
    }

    void WipeWater()
    {
        squeezeTime = 0.15f;

        Vector3Int water;
        if (LookingAtWater(out water))
            BlockWorld.Instance.SetBlock(water.x, water.y, water.z, BlockWorld.Air);
    }

    void Place()
    {
        if (selected == Sponge || selected == Leash) return;

        Vector3Int unused, target;
        if (selected == BlockWorld.Water)
        {
            if (!LookingAtSpaceBeforeWater(out target)) return;
        }
        else if (!LookingAtBlock(out unused, out target)) return;

        byte there = BlockWorld.Instance.GetBlock(target.x, target.y, target.z);
        if (there != BlockWorld.Air && there != BlockWorld.Water) return;   // you can build into water
        if (WouldHitPlayer(target)) return;

        BlockWorld.Instance.SetBlock(target.x, target.y, target.z, selected);

        if (selected == BlockWorld.Tnt)
            Tnt.Ignite(BlockWorld.Instance, target, Tnt.FuseSeconds);
    }

    // Don't let us build a block inside our own body and get stuck
    bool WouldHitPlayer(Vector3Int cell)
    {
        Bounds me = new Bounds(
            transform.position + body.center,
            new Vector3(body.radius * 2f, body.height, body.radius * 2f));

        Bounds newBlock = new Bounds(cell, Vector3.one);
        return me.Intersects(newBlock);
    }

    // --- The leash ---

    void UseLeash()
    {
        squeezeTime = 0.15f;

        if (caughtAnimal != null)
        {
            ((ILeashable)caughtAnimal).Release();
            caughtAnimal = null;
            return;
        }

        caughtAnimal = AnimalInSights();
        if (caughtAnimal != null)
            ((ILeashable)caughtAnimal).Catch(transform);
    }

    // Animals have no colliders for a raycast to hit, so we check each one
    // to see how close it is to the line we're looking along
    MonoBehaviour AnimalInSights()
    {
        MonoBehaviour closest = null;
        float closestDistance = LeashReach;

        foreach (Chicken chicken in FindObjectsByType<Chicken>(FindObjectsSortMode.None))
            CheckAnimal(chicken, ref closest, ref closestDistance);

        foreach (Squirrel squirrel in FindObjectsByType<Squirrel>(FindObjectsSortMode.None))
            CheckAnimal(squirrel, ref closest, ref closestDistance);

        return closest;
    }

    void CheckAnimal(MonoBehaviour animal, ref MonoBehaviour closest, ref float closestDistance)
    {
        Vector3 toAnimal = animal.transform.position + Vector3.up * 0.25f - cam.transform.position;
        float along = Vector3.Dot(toAnimal, cam.transform.forward);

        if (along < 0f || along > closestDistance) return;                               // behind us or too far
        if ((toAnimal - cam.transform.forward * along).magnitude > 0.6f) return;         // not lined up
        if (WallInTheWay(along)) return;

        closest = animal;
        closestDistance = along;
    }

    // You can catch something through water or leaves, but not through a wall
    bool WallInTheWay(float distance)
    {
        for (float d = 0f; d < distance; d += 0.05f)
        {
            Vector3Int cell = Vector3Int.RoundToInt(cam.transform.position + cam.transform.forward * d);
            byte type = BlockWorld.Instance.GetBlock(cell.x, cell.y, cell.z);

            if (type != BlockWorld.Air && type != BlockWorld.Water && type != BlockWorld.Leaves)
                return true;
        }
        return false;
    }

    // The rope is one long thin cube, stretched from your hand to the animal
    void LateUpdate()
    {
        rope.SetActive(caughtAnimal != null);
        if (caughtAnimal == null) return;

        Vector3 from = cam.transform.position + cam.transform.right * 0.35f
                     - cam.transform.up * 0.30f + cam.transform.forward * 0.6f;
        Vector3 to = caughtAnimal.transform.position + Vector3.up * 0.25f;

        rope.transform.position = (from + to) / 2f;
        rope.transform.rotation = Quaternion.LookRotation(to - from);
        rope.transform.localScale = new Vector3(0.03f, 0.03f, Vector3.Distance(from, to));
    }

    // --- The tools in your hand ---

    void BuildHand()
    {
        // Children of the camera, so they stay in the corner of your view
        hand = new GameObject("Hand");
        hand.transform.SetParent(cam.transform, false);

        Material ropeColour = BlockWorld.MakeMaterial(new Color(0.55f, 0.38f, 0.20f));

        spongeInHand = AddHandPart(new Vector3(0.45f, -0.30f, 0.75f), new Vector3(0.32f, 0.20f, 0.26f), MakeSpongeMaterial());
        leashInHand = AddHandPart(new Vector3(0.45f, -0.33f, 0.75f), new Vector3(0.22f, 0.14f, 0.22f), ropeColour);   // a coil of rope
        AddHandPart(new Vector3(0.50f, -0.46f, 0.70f), new Vector3(0.20f, 0.14f, 0.40f),
                    BlockWorld.MakeMaterial(new Color(0.90f, 0.70f, 0.55f)));   // your hand, gripping it from below

        hand.SetActive(false);

        rope = AddHandPart(Vector3.zero, Vector3.one, ropeColour);
        rope.name = "Rope";
        rope.transform.SetParent(null);
    }

    GameObject AddHandPart(Vector3 position, Vector3 size, Material material)
    {
        GameObject part = new GameObject("Hand Part");
        part.transform.SetParent(hand.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;

        MeshRenderer look = part.AddComponent<MeshRenderer>();
        look.sharedMaterial = material;
        look.shadowCastingMode = ShadowCastingMode.Off;
        look.receiveShadows = false;
        return part;
    }

    // Yellow with darker holes dotted around, drawn pixel by pixel
    static Material MakeSpongeMaterial()
    {
        Texture2D texture = new Texture2D(8, 8);
        texture.filterMode = FilterMode.Point;

        Color yellow = new Color(0.95f, 0.85f, 0.25f);
        Color hole = new Color(0.72f, 0.60f, 0.12f);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                texture.SetPixel(x, y, (x * 3 + y * 5) % 7 == 0 ? hole : yellow);
        texture.Apply();

        Material mat = BlockWorld.MakeMaterial(Color.white);
        mat.mainTexture = texture;
        return mat;
    }

    void MoveHand()
    {
        hand.SetActive(selected == Sponge || selected == Leash);
        spongeInHand.SetActive(selected == Sponge);
        leashInHand.SetActive(selected == Leash);

        squeezeTime = Mathf.Max(squeezeTime - Time.deltaTime, 0f);
        hand.transform.localPosition = squeezeTime > 0f ? Vector3.down * 0.06f : Vector3.zero;
    }

    // Crosshair in the middle of the screen, plus a reminder of the controls
    void OnGUI()
    {
        float dot = 4f;
        GUI.DrawTexture(
            new Rect(Screen.width / 2f - dot / 2f, Screen.height / 2f - dot / 2f, dot, dot),
            Texture2D.whiteTexture);

        string[] names = { "", "Grass", "Dirt", "Stone", "Wood", "Sand", "Leaves", "Water", "TNT", "Sponge", "Leash" };
        GUI.Label(new Rect(12, 10, 400, 24), "Holding: " + names[selected] + "   (press 1-9 or 0 to change)");
        GUI.Label(new Rect(12, 30, 800, 24), "WASD move   Space jump / swim up   Left click break / wipe water / leash   Right click place   Esc frees mouse");
    }
}
