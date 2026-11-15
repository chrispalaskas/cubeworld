using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Magic villagers live in the Magic Land. They wear robes and pointy hats
// with a glowing star, they can float through the air and cast sparkles,
// and now and then one flies to a magic portal and goes through it to visit
// your home world. Later, they fly back home again. (Going through portals
// is in VillagerTravel.cs, as ordinary villagers do it too.)
//
// This is part of Villager - magic villagers do everything ordinary ones do too.
public partial class Villager
{
    const int MagicFolkCount = 5;
    const float HatTop = 1.62f;
    const float FlySpeed = 3f;

    static readonly string[] MagicNames =
        { "Zara", "Orion", "Luna", "Nova", "Merlin", "Stella", "Pip", "Juniper", "Comet", "Willow", "Sage", "Twinkle" };

    static readonly string[] MagicLandHellos =
        { "Greetings, traveller!", "Welcome to the Magic Land!", "Twinkle twinkle!", "Have you seen my wand?",
          "Mind the edge - it's a long way down!" };

    static readonly string[] VisitingHellos =
        { "So this is your world!", "Where have all the clouds gone?", "Your grass is so green!",
          "Greetings, builder!", "I love it here!" };

    static readonly string[][] MagicChats =
    {
        new[] { "Can you float yet?", "Only on Tuesdays!" },
        new[] { "I made a sparkle spell!", "Ooh, show me!" },
        new[] { "Have you been through the portal?", "Yes! There are CHICKENS there!" },
        new[] { "My hat is pointier than yours.", "Is not!" },
        new[] { "The crystals are glowing today.", "They always glow, silly!" },
    };

    static readonly Color[] Robes =
    {
        new Color(0.45f, 0.20f, 0.75f), new Color(0.15f, 0.25f, 0.70f), new Color(0.10f, 0.60f, 0.65f),
        new Color(0.85f, 0.30f, 0.65f), new Color(0.25f, 0.15f, 0.40f)
    };
    static readonly Color[] MagicSkin =
    {
        new Color(0.98f, 0.85f, 0.75f), new Color(0.75f, 0.85f, 1.00f), new Color(0.85f, 0.75f, 1.00f),
        new Color(0.70f, 0.95f, 0.85f), new Color(0.60f, 0.42f, 0.30f)
    };
    static readonly Color[] MagicHair =
    {
        new Color(0.95f, 0.95f, 1.00f), new Color(1.00f, 0.60f, 0.85f), new Color(0.55f, 0.85f, 1.00f),
        new Color(0.80f, 0.80f, 0.85f), new Color(0.30f, 0.15f, 0.50f)
    };

    private bool isMagic;
    private bool flying;
    private readonly List<MeshRenderer> robeParts = new List<MeshRenderer>();   // so a spell can change their colour
    private int crystalsGrown;

    const int MostCrystals = 3;   // each magic villager grows at most this many

    static Material[] rainbowColours;

    // Everyone who lives in this world's Magic Land, chosen the first time
    // they're needed and the same ever after
    static List<string> MagicFolk()
    {
        List<string> folk = LoadNames("magicfolk");
        if (folk.Count == 0)
        {
            List<string> pool = new List<string>(MagicNames);
            for (int i = 0; i < MagicFolkCount && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                folk.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            SaveNames("magicfolk", folk);
        }
        return folk;
    }

    // Puts this world's magic villagers wherever they are right now: in the
    // Magic Land, or visiting your home world near its magic portal
    public static void SpawnMagicFolk(BlockWorld world)
    {
        if (WorldMenu.CurrentSlot < 0 || WorldMenu.CurrentPlace == WorldSaves.Nether) return;

        List<string> visitors = LoadNames("visitors");
        Vector3 middle = InMagicLand ? world.FindSpawnPoint() : NearMagicPortal(world);

        foreach (string name in MagicFolk())
        {
            bool visiting = visitors.Contains(name);
            if (visiting == InMagicLand) continue;   // they're in the other place

            Vector3 spot;
            if (FindSpotNear(world, middle, 6, out spot, true))
                SpawnAt(world, spot, true, name);
        }
    }

    // Out of a magic villager egg. They move in for good: into the Magic
    // Land, or - if they hatched in your home world - as a visitor there.
    public static void HatchMagic(BlockWorld world, Vector3 spot)
    {
        List<string> folk = MagicFolk();
        string name = MagicNames[Random.Range(0, MagicNames.Length)];
        for (int tries = 0; tries < 20 && folk.Contains(name); tries++)
            name = MagicNames[Random.Range(0, MagicNames.Length)];   // try for a name nobody has yet

        folk.Add(name);
        SaveNames("magicfolk", folk);

        if (!InMagicLand)
        {
            List<string> visitors = LoadNames("visitors");
            visitors.Add(name);
            SaveNames("visitors", visitors);
        }

        Villager person = SpawnAt(world, spot, true, name);
        SparkleBurst(spot + Vector3.up * 0.5f, 20);
        person.Say("*poof* Hello! I'm " + name + "!", 3f);
        person.lastHello = Time.time;
    }

    static Material[] sparkleColours;

    // --- Their look ---

    // Like an ordinary villager, but in a long robe, with a pointy hat and a star on top
    void BuildMagicBody()
    {
        Material robe = Paint(Pick(Robes));
        Material hair = Paint(Pick(MagicHair));
        Material skin = Paint(Pick(MagicSkin));
        Material black = Paint(new Color(0.05f, 0.05f, 0.05f));
        Material star = SparkleColours()[1];

        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.32f, 0.46f, 0.20f), robe);   // body

        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            AddPart(leg, new Vector3(0f, -0.16f, 0f), new Vector3(0.15f, 0.32f, 0.17f), robe);   // the robe comes right down
            AddPart(leg, new Vector3(0f, -0.33f, 0.03f), new Vector3(0.12f, 0.06f, 0.18f), black);
        }

        leftArm = Pivot(model, new Vector3(-0.22f, 0.78f, 0f));
        rightArm = Pivot(model, new Vector3(0.22f, 0.78f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.12f, 0f), new Vector3(0.13f, 0.24f, 0.13f), robe);   // wide sleeves
            AddPart(arm, new Vector3(0f, -0.30f, 0f), new Vector3(0.09f, 0.14f, 0.09f), skin);
        }

        headPivot = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(headPivot, new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.30f, 0.30f), skin);
        AddPart(headPivot, new Vector3(0f, 0.18f, -0.15f), new Vector3(0.32f, 0.26f, 0.04f), hair);   // long hair at the back
        AddPart(headPivot, new Vector3(-0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);
        AddPart(headPivot, new Vector3(0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), black);
        AddPart(headPivot, new Vector3(0f, 0.07f, 0.151f), new Vector3(0.10f, 0.02f, 0.01f), black);

        // The hat: a wide brim, then smaller and smaller layers up to a point
        AddPart(headPivot, new Vector3(0f, 0.31f, 0f), new Vector3(0.44f, 0.04f, 0.44f), robe);
        AddPart(headPivot, new Vector3(0f, 0.40f, 0f), new Vector3(0.26f, 0.14f, 0.26f), robe);
        AddPart(headPivot, new Vector3(0f, 0.53f, -0.02f), new Vector3(0.18f, 0.12f, 0.18f), robe);
        AddPart(headPivot, new Vector3(0f, 0.63f, -0.05f), new Vector3(0.10f, 0.10f, 0.10f), robe);
        AddPart(headPivot, new Vector3(0f, 0.72f, -0.07f), new Vector3(0.08f, 0.08f, 0.08f), star);

        foreach (MeshRenderer part in model.GetComponentsInChildren<MeshRenderer>())
            if (part.sharedMaterial == robe) robeParts.Add(part);
    }

    // Glowing colours for sparkles (and the star on the hat)
    static Material[] SparkleColours()
    {
        if (sparkleColours == null || sparkleColours[0] == null)
        {
            Color[] colours =
            {
                new Color(0.85f, 0.55f, 1.00f), new Color(1.00f, 0.90f, 0.30f), new Color(0.50f, 0.95f, 1.00f),
                new Color(1.00f, 0.55f, 0.85f), Color.white
            };
            sparkleColours = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                Material mat = BlockWorld.MakeMaterial(colours[i]);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", colours[i]);
                sparkleColours[i] = mat;
            }
        }
        return sparkleColours;
    }

    string MagicHello()
    {
        string[] hellos = InMagicLand ? MagicLandHellos : VisitingHellos;
        return hellos[Random.Range(0, hellos.Length)];
    }

    // --- Tricks ---

    // Picks a magic trick to do, or null to do something ordinary instead
    IEnumerator PickMagicTrick()
    {
        if (Random.value < 0.05f) return VisitOtherWorld(1000f);   // they can fly, so no portal is too far

        // Magic most of the time when you're close by to watch, and quite
        // often even when you're not
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        bool watched = player != null && FlatDistance(player.transform.position) < 10f;
        if (Random.value > (watched ? 0.65f : 0.4f)) return null;

        switch (Random.Range(0, 8))
        {
            case 0:  return CastSparkles();
            case 1:  return FloatUp();
            case 2:  return Vanish();
            case 3:  return FireworkShow();
            case 4:  return MakeRainbow();
            case 5:  return ChangeColour();
            case 6:  return ChickenNearby() != null ? FloatChicken(ChickenNearby()) : CastSparkles();
            default: return InMagicLand && crystalsGrown < MostCrystals ? GrowCrystal() : Vanish();
        }
    }

    // Disappears in a puff of sparkles and pops up a few blocks away
    IEnumerator Vanish()
    {
        Say("Now you see me...", 1.5f);
        armsUp = true;
        yield return new WaitForSeconds(1.2f);

        for (int tries = 0; tries < 15; tries++)
        {
            Vector3 jump = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(4f, 7f);
            int x = Mathf.RoundToInt(transform.position.x + jump.x);
            int z = Mathf.RoundToInt(transform.position.z + jump.z);
            int ground = GroundBelow(x, transform.position.y + 3f, z);

            if (ground < 0 || world.GetBlock(x, ground, z) == BlockWorld.Water) continue;
            if (!Passable(x, ground + 1, z) || !Passable(x, ground + 2, z)) continue;

            SparkleBurst(transform.position + Vector3.up * 0.6f, 15);
            transform.position = new Vector3(x, ground + 0.5f, z);
            SparkleBurst(transform.position + Vector3.up * 0.6f, 15);
            Say("...now you don't!", 1.5f);
            break;
        }

        armsUp = false;
        yield return new WaitForSeconds(1.5f);
    }

    // A few fireworks, straight out of their hands
    IEnumerator FireworkShow()
    {
        Say("Watch this!", 1.5f);
        armsUp = true;
        yield return new WaitForSeconds(0.8f);

        int rockets = Random.Range(1, 4);
        for (int i = 0; i < rockets; i++)
        {
            Firework.Launch(transform.position + Vector3.up * 1.4f);
            SparkleBurst(transform.position + Vector3.up * 1.4f, 5);
            yield return new WaitForSeconds(0.6f);
        }

        armsUp = false;
        yield return new WaitForSeconds(2f);
    }

    // A glowing rainbow arching over in front of them, for a few seconds
    IEnumerator MakeRainbow()
    {
        Say("Rainbow, appear!", 2f);
        armsUp = true;
        yield return new WaitForSeconds(0.8f);

        Material[] colours = RainbowColours();
        GameObject rainbow = new GameObject("Rainbow");
        Vector3 middle = transform.position + transform.forward * 2f + Vector3.up * 0.5f;

        // Six bands, red on the outside, each a half circle of little glowing cubes
        for (int band = 0; band < colours.Length; band++)
        {
            float radius = 3f - band * 0.22f;
            for (int step = 0; step <= 24; step++)
            {
                float angle = step / 24f * Mathf.PI;
                GameObject bit = new GameObject("Rainbow Bit");
                bit.transform.SetParent(rainbow.transform, false);
                bit.transform.position = middle + transform.right * Mathf.Cos(angle) * radius + Vector3.up * Mathf.Sin(angle) * radius;
                bit.transform.localScale = Vector3.one * 0.24f;
                bit.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
                bit.AddComponent<MeshRenderer>().sharedMaterial = colours[band];
            }
        }

        Destroy(rainbow, 4f);   // gone by itself, even if this villager wanders off
        yield return new WaitForSeconds(1.5f);
        armsUp = false;
        yield return new WaitForSeconds(2.5f);
    }

    static Material[] RainbowColours()
    {
        if (rainbowColours == null || rainbowColours[0] == null)
        {
            Color[] colours =
            {
                new Color(1.0f, 0.2f, 0.2f), new Color(1.0f, 0.6f, 0.1f), new Color(1.0f, 0.95f, 0.2f),
                new Color(0.3f, 0.9f, 0.3f), new Color(0.3f, 0.6f, 1.0f), new Color(0.7f, 0.35f, 1.0f)
            };
            rainbowColours = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                Material mat = BlockWorld.MakeMaterial(colours[i]);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", colours[i]);
                rainbowColours[i] = mat;
            }
        }
        return rainbowColours;
    }

    // Their robe and hat switch to a new colour
    IEnumerator ChangeColour()
    {
        string[] spells = { "Ooh, new robes!", "Colour change!", "How do I look?" };
        Say(spells[Random.Range(0, spells.Length)], 2f);
        SparkleBurst(transform.position + Vector3.up * 0.8f, 20);

        Material robe = Paint(Pick(Robes));
        foreach (MeshRenderer part in robeParts)
            if (part != null) part.sharedMaterial = robe;

        yield return new WaitForSeconds(2f);
    }

    // Makes a chicken float up into the air - then lets go
    IEnumerator FloatChicken(Chicken chicken)
    {
        Say("Fly, chicken, fly!", 2f);
        FaceTowards(chicken.transform.position);
        armsUp = true;

        Vector3 start = chicken.transform.position;
        for (float t = 0f; t < 2.5f && chicken != null; t += Time.deltaTime)
        {
            chicken.transform.position = start + Vector3.up * Mathf.Min(t * 1.2f, 2.5f);
            if (Random.value < 0.15f) SparkleBurst(chicken.transform.position + Vector3.up * 0.3f, 1);
            yield return null;
        }

        armsUp = false;
        Say("Oops! Sorry, chicken!", 1.5f);
        yield return new WaitForSeconds(1.5f);
    }

    // Grows a glowing crystal out of the magic grass. Only in the Magic
    // Land, and only a few each, so they don't fill it up.
    IEnumerator GrowCrystal()
    {
        Vector3 ahead = transform.position + transform.forward * 1.5f;
        int x = Mathf.RoundToInt(ahead.x), z = Mathf.RoundToInt(ahead.z);
        int ground = GroundBelow(x, transform.position.y + 1f, z);
        if (ground < 0 || world.GetBlock(x, ground, z) != BlockWorld.MagicGrass) yield break;
        if (world.GetBlock(x, ground + 1, z) != BlockWorld.Air) yield break;

        Say("Grow, little crystal!", 2f);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            headNod = 25f;
            if (Random.value < 0.2f) SparkleBurst(new Vector3(x, ground + 0.8f, z), 1);
            yield return null;
        }
        headNod = 0f;

        world.SetBlock(x, ground + 1, z, BlockWorld.Crystal);
        SparkleBurst(new Vector3(x, ground + 1f, z), 15);
        crystalsGrown++;
        yield return new WaitForSeconds(1f);
    }

    IEnumerator CastSparkles()
    {
        armsUp = true;
        string[] spells = { "Abracadabra!", "Sparkle sparkle!", "Hocus pocus!", "Shazam!" };
        Say(spells[Random.Range(0, spells.Length)], 2f);

        for (int burst = 0; burst < 3; burst++)
        {
            SparkleBurst(transform.position + Vector3.up * 1.4f, 8);
            yield return new WaitForSeconds(0.4f);
        }

        yield return new WaitForSeconds(0.5f);
        armsUp = false;
    }

    IEnumerator FloatUp()
    {
        Say("Look, I can fly!", 2f);
        Vector3 start = transform.position;
        flying = true;
        armsUp = true;

        // Up a couple of blocks, bob about a bit, then gently back down
        float up = Mathf.Min(start.y + 2.5f, BlockWorld.SizeY - 2f);
        yield return Glide(new Vector3(start.x, up, start.z), 1.5f);
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            transform.position = new Vector3(start.x, up + Mathf.Sin(t * 4f) * 0.15f, start.z);
            if (Random.value < 0.1f) SparkleBurst(transform.position, 1);
            yield return null;
        }

        armsUp = false;
        flying = false;   // gravity takes over, which brings us back down
        yield return new WaitForSeconds(1f);
    }

    // Floats in a straight line to a spot, ignoring the ground
    IEnumerator Glide(Vector3 to, float speed)
    {
        while ((transform.position - to).sqrMagnitude > 0.0025f)
        {
            Vector3 flat = to - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) facing = Quaternion.LookRotation(flat);

            transform.position = Vector3.MoveTowards(transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }

    // Up into the sky, across, and down onto the spot - so magic villagers
    // can cross the gaps between floating islands
    IEnumerator FlyTo(Vector3 target)
    {
        flying = true;
        armsUp = true;

        float cruise = Mathf.Min(Mathf.Max(transform.position.y, target.y) + 3f, BlockWorld.SizeY - 2f);
        yield return Glide(new Vector3(transform.position.x, cruise, transform.position.z), FlySpeed);
        yield return Glide(new Vector3(target.x, cruise, target.z), FlySpeed);
        yield return Glide(target, FlySpeed);

        armsUp = false;
        flying = false;
    }

    // --- Sparkles ---

    // Little glowing bits that drift up and away. They move by themselves
    // (with physics, but no gravity), so they keep going even if whoever
    // made them has gone through a portal.
    public static void SparkleBurst(Vector3 where, int count)
    {
        Material[] colours = SparkleColours();

        for (int i = 0; i < count; i++)
        {
            GameObject sparkle = new GameObject("Sparkle");
            sparkle.transform.position = where + Random.insideUnitSphere * 0.3f;
            sparkle.transform.localScale = Vector3.one * Random.Range(0.05f, 0.1f);
            sparkle.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
            sparkle.AddComponent<MeshRenderer>().sharedMaterial = colours[Random.Range(0, colours.Length)];

            Rigidbody drift = sparkle.AddComponent<Rigidbody>();
            drift.useGravity = false;
            drift.linearVelocity = Random.insideUnitSphere * 1.5f + Vector3.up * 1f;
            drift.angularVelocity = Random.insideUnitSphere * 6f;

            Destroy(sparkle, Random.Range(0.6f, 1.2f));
        }
    }
}
