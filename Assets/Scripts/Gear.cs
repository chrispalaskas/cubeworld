using System.Collections.Generic;
using UnityEngine;

// Gear, and using the new blocks:
//
//   Bow:           left click to shoot an arrow (in survival, you need arrows)
//   Fishing rod:   press E while looking at water to cast. When the float
//                  bobs under - splash! - press E quickly to catch a fish.
//   Armour:        press E while holding it to put it on (leather, iron or
//                  diamond). Monsters do less damage - see the shields above your hearts.
//   Compass:       hold it and it points the way to your bed (or the start)
//   Treasure map:  hold it and it points the way to buried treasure
//   Boat/Minecart: put a boat on water, a minecart on rails. E to get in and out.
//   Signs:         press E on a sign to write on it
//
// This is part of BlockInteractor.
public partial class BlockInteractor
{
    // Uses one up (in survival) - for feeding pets
    public bool TakeOne(byte item)
    {
        if (item == BlockWorld.Air) return false;
        return UseUp(item, 1);
    }

    // --- Placing the new blocks ---

    // Ladders, signs and slides turn to face you, like furniture
    byte FacingMe(byte first)
    {
        Vector3 f = transform.forward;
        int facing;
        if (Mathf.Abs(f.z) >= Mathf.Abs(f.x)) facing = f.z > 0f ? 2 : 0;
        else facing = f.x > 0f ? 3 : 1;
        return (byte)(first + facing);
    }

    // Rails join up with the rails next to them - or run the way you're looking
    byte RailFor(Vector3Int at)
    {
        BlockWorld world = BlockWorld.Instance;
        foreach (int dy in new[] { 0, -1, 1 })
        {
            bool eastWest = BlockWorld.IsRail(world.GetBlock(at.x + 1, at.y + dy, at.z)) || BlockWorld.IsRail(world.GetBlock(at.x - 1, at.y + dy, at.z));
            bool northSouth = BlockWorld.IsRail(world.GetBlock(at.x, at.y + dy, at.z + 1)) || BlockWorld.IsRail(world.GetBlock(at.x, at.y + dy, at.z - 1));
            if (eastWest && !northSouth) return BlockWorld.RailEW;
            if (northSouth && !eastWest) return BlockWorld.RailNS;
        }
        Vector3 f = transform.forward;
        return Mathf.Abs(f.x) > Mathf.Abs(f.z) ? BlockWorld.RailEW : BlockWorld.RailNS;
    }

    // The block that actually goes down, for things that turn to face a way
    byte TurnedFor(byte item, Vector3Int at)
    {
        if (item == BlockWorld.LadderFirst || item == BlockWorld.SignFirst || item == BlockWorld.SlideFirst) return FacingMe(item);
        if (item == BlockWorld.RailNS) return RailFor(at);
        return item;
    }

    // Picked up some armour in the hotbar? It only works once you put it on!
    private byte lastHeld;
    void RemindAboutArmour()
    {
        if (selected == lastHeld) return;
        lastHeld = selected;
        PlayerHealth health = PlayerHealth.Instance;
        if (!IsArmour(selected) || health == null) return;
        int level = selected - BlockWorld.LeatherArmour + 1;
        if (health.Armour < level)
            PortalTraveller.Announce("Press E (or right click) to put on the " + ItemNames[selected] + " - it only protects you once you're wearing it!");
    }

    static bool IsArmour(byte item) { return item >= BlockWorld.LeatherArmour && item <= BlockWorld.DiamondArmour; }

    // Things you use rather than place. True if it was one of those.
    bool UseGear()
    {
        byte item = selected;

        if (IsArmour(item))
        {
            PlayerHealth health = PlayerHealth.Instance;
            if (health == null) { PortalTraveller.Announce("Armour only matters in survival - nothing can hurt you here!"); return true; }
            int level = item - BlockWorld.LeatherArmour + 1;
            if (health.Armour >= level) { PortalTraveller.Announce("You're already wearing armour that good."); return true; }
            if (!UseUp(item)) return true;
            if (health.Armour > 0) Give((byte)(BlockWorld.LeatherArmour + health.Armour - 1));   // the old armour back
            health.Armour = level;
            Sounds.Place(BlockWorld.Iron, transform.position);
            Achievements.Unlock("armour");
            PortalTraveller.Announce("You put on the " + ItemNames[item] + "!");
            return true;
        }

        if (item == BlockWorld.FishingRod) { Fish(); return true; }

        if (item == BlockWorld.Boat)
        {
            Vector3Int water;
            if (!LookingAtWaterFar(out water)) { PortalTraveller.Announce("Boats go on water!"); return true; }
            if (BoatRide.Launch(BlockWorld.Instance, water) != null) UseUp(item);
            return true;
        }

        if (item == BlockWorld.Minecart)
        {
            Vector3Int rail;
            if (!LookingAtRail(out rail)) { PortalTraveller.Announce("Minecarts go on rails!"); return true; }
            if (Cart.PutOn(BlockWorld.Instance, rail) != null) UseUp(item);
            return true;
        }

        if (item == BlockWorld.Compass || item == BlockWorld.TreasureMap || item == BlockWorld.Bow || item == BlockWorld.Arrow ||
            item == BlockWorld.Leather || item == BlockWorld.Feather || item == BlockWorld.StringItem)
            return true;   // nothing to place

        return false;
    }

    // Creeps along the line of sight for the first water, a bit further than you can reach
    bool LookingAtWaterFar(out Vector3Int water)
    {
        for (float d = 0f; d < 14f; d += 0.1f)
        {
            water = Vector3Int.RoundToInt(cam.transform.position + cam.transform.forward * d);
            byte type = BlockWorld.Instance.GetBlock(water.x, water.y, water.z);
            if (type == BlockWorld.Water) return true;
            if (type != BlockWorld.Air && !BlockWorld.IsWheat(type)) break;
        }
        water = Vector3Int.zero;
        return false;
    }

    bool LookingAtRail(out Vector3Int rail)
    {
        for (float d = 0f; d < reach; d += 0.05f)
        {
            rail = Vector3Int.RoundToInt(cam.transform.position + cam.transform.forward * d);
            byte type = BlockWorld.Instance.GetBlock(rail.x, rail.y, rail.z);
            if (BlockWorld.IsRail(type)) return true;
            if (type != BlockWorld.Air) break;
        }
        rail = Vector3Int.zero;
        return false;
    }

    // --- Shooting ---

    // A bow in your pocket (in creative, you've always got one)
    bool HasBow()
    {
        return !Survival || HowMany(BlockWorld.Bow) > 0;
    }

    void ShootArrow()
    {
        if (Survival && !PlayerLook.FormShoots && !UseUp(BlockWorld.Arrow))
        {
            PortalTraveller.Announce("You've run out of arrows! Make some: gravel, wood and a feather.");
            return;
        }
        squeezeTime = 0.15f;
        Sounds.Twang(transform.position);
        MonsterShot.PlayerArrow(cam.transform.position + cam.transform.forward * 0.8f, cam.transform.forward * 30f, Enchanted(BlockWorld.Bow) ? 10 : 6);
    }

    // --- Fishing ---

    private GameObject bobber, fishingLine;
    private float biteAt, biteUntil;

    void Fish()
    {
        if (bobber != null)
        {
            // Reel in: did you catch it in time?
            if (Time.time >= biteAt && Time.time <= biteUntil) Caught();
            else PortalTraveller.Announce(Time.time > biteUntil ? "It got away!" : "Nothing yet... wait for the splash!");
            StopFishing();
            return;
        }

        Vector3Int water;
        if (!LookingAtWaterFar(out water)) { PortalTraveller.Announce("Look at some water to cast your line."); return; }
        int top = water.y;
        while (BlockWorld.Instance.GetBlock(water.x, top + 1, water.z) == BlockWorld.Water) top++;

        bobber = new GameObject("Float");
        bobber.transform.position = new Vector3(water.x, top + 0.45f, water.z);
        bobber.transform.localScale = Vector3.one * 0.15f;
        bobber.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        bobber.AddComponent<MeshRenderer>().sharedMaterial = BlockWorld.MakeMaterial(new Color(0.95f, 0.2f, 0.2f));

        fishingLine = AddHandPart(Vector3.zero, Vector3.one, BlockWorld.MakeMaterial(new Color(0.9f, 0.9f, 0.9f)));
        fishingLine.transform.SetParent(null);

        biteAt = Time.time + Random.Range(2.5f, 7f);
        biteUntil = biteAt + 1.3f;
        Sounds.Splash(bobber.transform.position);
    }

    void Caught()
    {
        Achievements.Unlock("fish");
        // Usually a fish - but now and then, something else!
        float roll = Random.value;
        byte catchOf = roll < 0.85f ? BlockWorld.RawFish : roll < 0.92f ? BlockWorld.Leather : roll < 0.97f ? BlockWorld.TreasureMap : BlockWorld.Diamond;
        if (Survival) Give(catchOf);
        Sounds.Pop(transform.position);
        PortalTraveller.Announce(catchOf == BlockWorld.RawFish ? "You caught a fish!" : "Ooh, you fished up a " + ItemNames[catchOf] + "!");
    }

    void StopFishing()
    {
        if (bobber != null) { Destroy(bobber.GetComponent<MeshRenderer>().sharedMaterial); Destroy(bobber); }
        if (fishingLine != null) { Destroy(fishingLine.GetComponent<MeshRenderer>().sharedMaterial); Destroy(fishingLine); }
        bobber = null;
        fishingLine = null;
    }

    // Every frame: the float bobs, dips when a fish bites, and the line stretches out to it
    void MoveFishing()
    {
        if (bobber == null) return;
        if (selected != BlockWorld.FishingRod || Vector3.Distance(bobber.transform.position, transform.position) > 20f) { StopFishing(); return; }

        Vector3 at = bobber.transform.position;
        bool biting = Time.time >= biteAt && Time.time <= biteUntil;
        if (biting && Time.time - biteAt < Time.deltaTime * 1.5f) Sounds.Splash(at);   // splash! quick, press E!
        if (Time.time > biteUntil + 1.5f)   // missed it - another fish will come along
        {
            biteAt = Time.time + Random.Range(2.5f, 7f);
            biteUntil = biteAt + 1.3f;
        }
        float baseY = Mathf.Floor(at.y) + 0.45f;
        at.y = baseY + (biting ? -0.25f : Mathf.Sin(Time.time * 3f) * 0.04f);
        bobber.transform.position = at;

        Vector3 from = cam.transform.position + cam.transform.right * 0.35f - cam.transform.up * 0.2f + cam.transform.forward * 0.6f;
        fishingLine.transform.position = (from + at) / 2f;
        fishingLine.transform.rotation = Quaternion.LookRotation(at - from);
        fishingLine.transform.localScale = new Vector3(0.01f, 0.01f, Vector3.Distance(from, at));
    }

    // --- Signs ---

    private bool writingSign;
    private Vector3Int signAt;
    private string signWords = "";

    bool LookingAtSign(out Vector3Int sign)
    {
        Vector3Int unused;
        return LookingAtBlock(out sign, out unused) && BlockWorld.IsSign(BlockWorld.Instance.GetBlock(sign.x, sign.y, sign.z));
    }

    void StartWritingSign(Vector3Int sign)
    {
        OpenInventory();
        writingSign = true;
        signAt = sign;
        signWords = BlockWorld.Instance.SignText(sign);
    }

    void DrawSignWriting()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
        {
            BlockWorld.Instance.SetSignText(signAt, signWords);
            writingSign = false;
            CloseInventory();
            e.Use();
            return;
        }

        Rect box = new Rect(Screen.width / 2f - 220f, Screen.height / 2f - 60f, 440f, 120f);
        GUI.Box(box, "");
        GUI.Label(new Rect(box.x, box.y + 10f, box.width, 24f), "Write on the sign (Enter when you're done)", centredLabel);
        GUI.SetNextControlName("SignWords");
        signWords = GUI.TextField(new Rect(box.x + 20f, box.y + 44f, box.width - 40f, 28f), signWords, 60);
        GUI.FocusControl("SignWords");
    }

    // --- Boats, carts and animals you press E on ---

    bool LookingAtVehicle(out Vehicle found)
    {
        found = null;
        float best = 4f;
        foreach (Vehicle vehicle in Vehicle.All)
        {
            if (vehicle.Ridden) continue;
            Vector3 to = vehicle.transform.position - cam.transform.position;
            float along = Vector3.Dot(to, cam.transform.forward);
            if (along < 0f || along > best) continue;
            if ((to - cam.transform.forward * along).magnitude > 0.8f) continue;
            best = along;
            found = vehicle;
        }
        return found != null;
    }

    void PickUpVehicle(Vehicle vehicle)
    {
        if (vehicle is Balloon) { PortalTraveller.Announce("That's the village's hot air balloon! Press E to climb in."); return; }
        if (Survival) Give(vehicle.Item);
        Sounds.Pop(vehicle.transform.position);
        Destroy(vehicle.gameObject);
    }

    // A friendly animal close enough to press E on
    bool LookingAtCritter(out Monster critter)
    {
        critter = AnimalInSights(5f, true) as Monster;
        if (critter != null && critter.Hostile) critter = null;
        return critter != null;
    }

    // --- Compass and treasure map ---

    void DrawGearHud()
    {
        if (selected != BlockWorld.Compass && selected != BlockWorld.TreasureMap) return;
        if (InventoryOpen || BlockWorld.Instance == null) return;

        Vector3 target;
        string what;
        if (selected == BlockWorld.Compass)
        {
            target = FirstPersonPlayer.HomeSpot();
            what = "Home";
        }
        else
        {
            if (WorldMenu.CurrentPlace != WorldSaves.Home) { GUI.Label(new Rect(0, Screen.height / 2f + 40f, Screen.width, 24), "This map is for your home world.", centredLabel); return; }
            Vector3Int spot = Structures.Treasure(BlockWorld.Instance, transform.position);
            if (spot.y < 0) return;
            target = spot;
            what = "Treasure";
        }

        Vector3 to = target - transform.position;
        float far = new Vector2(to.x, to.z).magnitude;
        float angle = Vector3.SignedAngle(new Vector3(transform.forward.x, 0f, transform.forward.z), new Vector3(to.x, 0f, to.z), Vector3.up);

        Rect dial = new Rect(Screen.width / 2f - 40f, Screen.height - 190f, 80f, 80f);
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(dial, Texture2D.whiteTexture);
        GUI.color = Color.white;
        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, dial.center);
        GUI.color = selected == BlockWorld.Compass ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.3f);
        GUI.DrawTexture(new Rect(dial.center.x - 3f, dial.y + 8f, 6f, 34f), Texture2D.whiteTexture);   // the needle
        GUI.DrawTexture(new Rect(dial.center.x - 7f, dial.y + 6f, 14f, 8f), Texture2D.whiteTexture);
        GUI.matrix = old;
        GUI.color = before;

        string words = what + ": " + Mathf.RoundToInt(far) + " blocks";
        if (selected == BlockWorld.TreasureMap && far < 2f) words = "X marks the spot - dig down 3 blocks!";
        GUI.Label(new Rect(0, dial.y - 24f, Screen.width, 24f), words, centredLabel);
    }

    // --- Pictures ---

    private static readonly Dictionary<byte, Texture2D> gearPictures = new Dictionary<byte, Texture2D>();

    static Texture2D GearPicture(byte item)
    {
        Texture2D picture;
        if (gearPictures.TryGetValue(item, out picture)) return picture;

        string[] rows = null;
        Color special = Color.white;
        switch (item)
        {
            case BlockWorld.RawBeef: case BlockWorld.Steak: case BlockWorld.RawPork: case BlockWorld.CookedPork:
                special = item == BlockWorld.RawBeef ? new Color(0.85f, 0.25f, 0.25f) : item == BlockWorld.RawPork ? new Color(0.95f, 0.6f, 0.65f)
                        : item == BlockWorld.Steak ? new Color(0.50f, 0.28f, 0.15f) : new Color(0.75f, 0.50f, 0.30f);
                rows = new[] { "........", "..xxxx..", ".xxxxxx.", "xxxwxxxx", "xxxxxxxx", ".xxxxxx.", "..xxxx..", "........" };
                break;
            case BlockWorld.RawFish: case BlockWorld.CookedFish:
                special = item == BlockWorld.RawFish ? new Color(0.45f, 0.65f, 0.85f) : new Color(0.75f, 0.55f, 0.30f);
                rows = new[] { "........", "........", ".xxxx..x", "xkxxxxxx", "xxxxxxxx", ".xxxx..x", "........", "........" };
                break;
            case BlockWorld.Leather:
                special = new Color(0.60f, 0.38f, 0.20f);
                rows = new[] { "........", ".xxxxxx.", ".xxxxxx.", "xxxxxxxx", "xxxxxxxx", ".xxxxxx.", ".xx..xx.", "........" };
                break;
            case BlockWorld.Feather:
                rows = new[] { "......ww", ".....www", "....www.", "...www..", "..www...", ".ww.....", "b.......", "........" };
                break;
            case BlockWorld.StringItem:
                rows = new[] { "........", ".w......", "..w..w..", "...ww.w.", "..w....w", ".w...ww.", "......w.", "........" };
                break;
            case BlockWorld.FishingRod:
                special = new Color(0.9f, 0.9f, 0.9f);
                rows = new[] { "......bx", ".....b.x", "....b..x", "...b...x", "..b....x", ".b.....r", "b.......", "........" };
                break;
            case BlockWorld.Bow:
                special = new Color(0.9f, 0.9f, 0.9f);
                rows = new[] { ".bbx....", "b...x...", "b....x..", "b.....x.", "b.....x.", "b....x..", "b...x...", ".bbx...." };
                break;
            case BlockWorld.Arrow:
                rows = new[] { "......ss", ".....sss", "....bs..", "...b....", "..b.....", "wb......", "ww......", "........" };
                break;
            case BlockWorld.LeatherArmour: case BlockWorld.IronArmour: case BlockWorld.DiamondArmour:
                special = item == BlockWorld.LeatherArmour ? new Color(0.60f, 0.38f, 0.20f) : item == BlockWorld.IronArmour ? new Color(0.85f, 0.85f, 0.88f) : new Color(0.45f, 0.95f, 0.95f);
                rows = new[] { "xx....xx", "xxx..xxx", "xxxxxxxx", ".xxxxxx.", ".xxxxxx.", ".xxxxxx.", ".xxxxxx.", ".xxxxxx." };
                break;
            case BlockWorld.TreasureMap:
                special = new Color(0.90f, 0.82f, 0.60f);
                rows = new[] { "xxxxxxxx", "xgxxxxxx", "xggxxrxr", "xxgxxxrx", "xxxgxrxr", "xxxxgxxx", "xxxxxggx", "xxxxxxxx" };
                break;
            case BlockWorld.Compass:
                rows = new[] { "..ssss..", ".swwwws.", "swwrwwws", "swwrwwws", "swwkwwws", "swwkwwws", ".swwwws.", "..ssss.." };
                break;
            case BlockWorld.Minecart:
                rows = new[] { "........", "........", "s......s", "s......s", "ssssssss", "ssssssss", ".kk..kk.", ".kk..kk." };
                break;
            case BlockWorld.Boat:
                rows = new[] { "........", "........", "........", "p......p", "pp....pp", "pppppppp", ".pppppp.", "........" };
                break;
            case BlockWorld.LadderFirst:
                rows = new[] { "p......p", "pppppppp", "p......p", "pppppppp", "p......p", "pppppppp", "p......p", "pppppppp" };
                break;
            case BlockWorld.SignFirst:
                rows = new[] { "pppppppp", "pkkkkkkp", "pppppppp", "pkkkkkpp", "pppppppp", "...pp...", "...pp...", "...pp..." };
                break;
            case BlockWorld.Fence:
                rows = new[] { "........", ".p..p..p", "pppppppp", ".p..p..p", "pppppppp", ".p..p..p", ".p..p..p", "........" };
                break;
            case BlockWorld.RailNS:
                rows = new[] { ".s....s.", "bsbbbbsb", ".s....s.", "bsbbbbsb", ".s....s.", "bsbbbbsb", ".s....s.", "bsbbbbsb" };
                break;
            case BlockWorld.SlideFirst:
                rows = new[] { "r.......", "rr......", "yyy.....", "rrrr....", "rrrrr...", "yyyyyy..", "rrrrrrr.", "rrrrrrrr" };
                break;
            case BlockWorld.Cactus:
                rows = new[] { "..gGgg..", ".wgGgg..", "..gGggw.", "..gGgg..", ".wgGgg..", "..gGggw.", "..gGgg..", "..gGgg.." };
                break;
            case BlockWorld.Spawner:
                special = new Color(0.65f, 0.30f, 0.90f);
                rows = new[] { "kkkkkkkk", "k.k..k.k", "k.kxxk.k", "kkxxxxkk", "k.xxxx.k", "k.kxxk.k", "k.k..k.k", "kkkkkkkk" };
                break;
            case BlockWorld.StoneSlab: case BlockWorld.PlankSlab:
                char c = item == BlockWorld.StoneSlab ? 's' : 'p';
                string full = new string(c, 8);
                rows = new[] { "........", "........", "........", "........", full, full, full, full };
                break;
        }
        if (rows == null) return null;

        picture = PixelArt(rows, special);
        gearPictures[item] = picture;
        return picture;
    }
}
