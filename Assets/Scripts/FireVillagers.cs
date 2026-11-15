using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fire villagers live in the Nether. They have fiery skin, glowing eyes,
// dark clothes with a glowing ember belt, and hair made of real flickering
// flames. Fire doesn't bother them a bit: they walk right through it, and
// they can even stand on lava.
//
// As well as everything ordinary villagers do, they juggle fireballs,
// breathe fire, do a fire dance, shoot fireballs into the sky, and light
// little fires on the ash.
//
// The same ones live in each world's Nether every time you visit (the game
// remembers their names, like the magic villagers').
//
// This is part of Villager.
public partial class Villager
{
    const int FireFolkCount = 6;
    const float FlameTop = 1.4f;   // the top of their flaming hair
    const int MostFires = 3;       // each fire villager lights at most this many fires

    static readonly string[] FireNames =
        { "Ember", "Cinder", "Flint", "Sparky", "Scorch", "Pyra", "Ignis", "Torch", "Sol", "Kindle", "Smokey", "Coal" };

    static readonly string[] FireHellos =
        { "Welcome to the Nether!", "Toasty, isn't it?", "Mind the lava!", "Want to see a trick?",
          "Hot enough for you?", "Don't touch my hair!" };

    static readonly string[][] FireChats =
    {
        new[] { "Is it hot today?", "It's ALWAYS hot here!" },
        new[] { "I had a lava bath this morning.", "So relaxing!" },
        new[] { "Have you seen the dragons?", "One sneezed on me. Lovely and warm!" },
        new[] { "My hair's on fire!", "Mine too! It always is!" },
        new[] { "Want some toast?", "I'll make it with my breath!" },
    };

    static readonly Color[] FireSkin =
    {
        new Color(0.95f, 0.45f, 0.20f), new Color(0.85f, 0.30f, 0.15f), new Color(1.00f, 0.60f, 0.25f),
        new Color(0.70f, 0.22f, 0.12f)
    };
    static readonly Color[] FireClothes =
    {
        new Color(0.15f, 0.12f, 0.12f), new Color(0.30f, 0.08f, 0.06f), new Color(0.22f, 0.20f, 0.20f),
        new Color(0.35f, 0.15f, 0.05f)
    };

    private bool isFire;
    private int firesLit;

    static Material[] fireGlow;   // red, orange, yellow - for hair, eyes, belt and fireballs

    static Material[] FireGlow()
    {
        if (fireGlow == null || fireGlow[0] == null)
        {
            Color[] colours = { new Color(1.00f, 0.25f, 0.05f), new Color(1.00f, 0.55f, 0.10f), new Color(1.00f, 0.85f, 0.25f) };
            fireGlow = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                Material mat = BlockWorld.MakeMaterial(colours[i]);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", colours[i]);
                fireGlow[i] = mat;
            }
        }
        return fireGlow;
    }

    // --- Who lives in the Nether ---

    static List<string> FireFolk()
    {
        List<string> folk = LoadNames("firefolk");
        if (folk.Count == 0)
        {
            List<string> pool = new List<string>(FireNames);
            for (int i = 0; i < FireFolkCount && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                folk.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            SaveNames("firefolk", folk);
        }
        return folk;
    }

    // The fire villagers, gathered around where you arrive in the Nether
    public static void SpawnFireFolk(BlockWorld world)
    {
        if (WorldMenu.CurrentSlot < 0 || WorldMenu.CurrentPlace != WorldSaves.Nether) return;

        Vector3 middle = world.FindSpawnPoint();
        foreach (string name in FireFolk())
        {
            Vector3 spot;
            if (FindAshSpot(world, middle, 10, out spot))
                SpawnAt(world, spot, false, name, true);
        }
    }

    // Somewhere on the ash with room to stand
    static bool FindAshSpot(BlockWorld world, Vector3 middle, int range, out Vector3 spot)
    {
        for (int tries = 0; tries < 300; tries++)
        {
            int r = tries < 150 ? range : BlockWorld.SizeX;
            int x = Mathf.RoundToInt(middle.x) + Random.Range(-r, r + 1);
            int z = Mathf.RoundToInt(middle.z) + Random.Range(-r, r + 1);
            int y = world.GetSurfaceHeight(x, z);

            if (y >= 0 && world.GetBlock(x, y, z) == BlockWorld.Ash && world.GetBlock(x, y + 2, z) == BlockWorld.Air)
            {
                spot = new Vector3(x, y + 0.5f, z);
                return true;
            }
        }

        spot = Vector3.zero;
        return false;
    }

    // Out of a fire villager egg, in a burst of flames. They move into the
    // Nether for good (and if they hatch somewhere else, they'll be in the
    // Nether next time you go).
    public static void HatchFire(BlockWorld world, Vector3 spot)
    {
        List<string> folk = FireFolk();
        string name = FireNames[Random.Range(0, FireNames.Length)];
        for (int tries = 0; tries < 20 && folk.Contains(name); tries++)
            name = FireNames[Random.Range(0, FireNames.Length)];   // try for a name nobody has yet

        folk.Add(name);
        SaveNames("firefolk", folk);

        Villager person = SpawnAt(world, spot, false, name, true);
        Dragon.FireBurst(spot + Vector3.up * 0.5f, Vector3.up, 25, 6f, 0.8f);
        person.Say("*FWOOSH* Hi! I'm " + name + "!", 3f);
        person.lastHello = Time.time;
    }

    string FireHello()
    {
        return FireHellos[Random.Range(0, FireHellos.Length)];
    }

    // --- Their look ---

    void BuildFireBody()
    {
        Material clothes = Paint(Pick(FireClothes));
        Material skin = Paint(Pick(FireSkin));
        Material black = Paint(new Color(0.05f, 0.05f, 0.05f));
        Material[] glow = FireGlow();

        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.30f, 0.44f, 0.18f), clothes);   // body
        AddPart(model, new Vector3(0f, 0.42f, 0f), new Vector3(0.31f, 0.05f, 0.19f), glow[1]);   // a glowing ember belt

        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            AddPart(leg, new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.32f, 0.14f), clothes);
            AddPart(leg, new Vector3(0f, -0.33f, 0.02f), new Vector3(0.13f, 0.06f, 0.18f), black);
        }

        leftArm = Pivot(model, new Vector3(-0.21f, 0.78f, 0f));
        rightArm = Pivot(model, new Vector3(0.21f, 0.78f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.10f, 0f), new Vector3(0.11f, 0.20f, 0.11f), clothes);
            AddPart(arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.09f, 0.18f, 0.09f), skin);
        }

        headPivot = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(headPivot, new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.30f, 0.30f), skin);
        AddPart(headPivot, new Vector3(-0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), glow[2]);   // glowing eyes
        AddPart(headPivot, new Vector3(0.07f, 0.17f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), glow[2]);
        AddPart(headPivot, new Vector3(0f, 0.07f, 0.151f), new Vector3(0.10f, 0.02f, 0.01f), black);         // smile

        // Hair made of flames, which flicker (Flicker.cs, like a fire block)
        Transform hair = Pivot(headPivot, new Vector3(0f, 0.30f, 0f));
        float[,] flames = { { -0.09f, -0.06f, 0.22f }, { 0.08f, -0.04f, 0.28f }, { 0f, 0.06f, 0.34f },
                            { -0.04f, -0.10f, 0.18f }, { 0.10f, 0.08f, 0.20f }, { -0.10f, 0.08f, 0.24f } };
        for (int i = 0; i < flames.GetLength(0); i++)
        {
            float tall = flames[i, 2];
            AddPart(hair, new Vector3(flames[i, 0], tall / 2f, flames[i, 1]), new Vector3(0.11f, tall, 0.11f), glow[i % glow.Length]);
        }
        hair.gameObject.AddComponent<Flicker>();
    }

    // --- Fire tricks ---

    IEnumerator PickFireTrick()
    {
        // Mostly tricks when you're watching, and fairly often when you're not
        FirstPersonPlayer player = FindFirstObjectByType<FirstPersonPlayer>();
        bool watched = player != null && FlatDistance(player.transform.position) < 10f;
        if (Random.value > (watched ? 0.6f : 0.35f)) return null;

        switch (Random.Range(0, 5))
        {
            case 0:  return Juggle();
            case 1:  return BreatheFirePuff();
            case 2:  return FireDance();
            case 3:  return Fireball();
            default: return firesLit < MostFires ? LightAFire() : Juggle();
        }
    }

    // Three fireballs looping round and round above their hands
    IEnumerator Juggle()
    {
        Say("Watch me juggle!", 2f);
        armsUp = true;

        Material[] glow = FireGlow();
        GameObject[] balls = new GameObject[3];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i] = new GameObject("Fireball");
            balls[i].transform.localScale = Vector3.one * 0.14f;
            balls[i].AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
            balls[i].AddComponent<MeshRenderer>().sharedMaterial = glow[i % glow.Length];
            Destroy(balls[i], 3.5f);   // gone by themselves, even if the juggling gets interrupted
        }

        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] == null) continue;
                float angle = t * 6f + i * Mathf.PI * 2f / 3f;
                balls[i].transform.position = transform.position + Vector3.up * (1.35f + Mathf.Sin(angle) * 0.35f)
                                            + transform.right * Mathf.Cos(angle) * 0.3f + transform.forward * 0.15f;
                balls[i].transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
            }
            yield return null;
        }

        foreach (GameObject ball in balls)
            if (ball != null) Destroy(ball);
        armsUp = false;
        Say("Ta-da!", 1.5f);
        yield return new WaitForSeconds(1f);
    }

    // A big puff of fire, straight out in front
    IEnumerator BreatheFirePuff()
    {
        Say("Hah!", 1.2f);
        headNod = -10f;
        yield return new WaitForSeconds(0.4f);

        Vector3 mouth = transform.position + Vector3.up * 0.9f + transform.forward * 0.2f;
        for (int puff = 0; puff < 5; puff++)
        {
            Dragon.FireBurst(mouth, (transform.forward + Vector3.up * 0.2f).normalized, 5, 6f, 0.5f);
            yield return new WaitForSeconds(0.08f);
        }

        headNod = 0f;
        yield return new WaitForSeconds(1f);
    }

    // Spinning round, with flames bursting up around their feet
    IEnumerator FireDance()
    {
        Say("Fire dance!", 2f);
        armsUp = true;
        float angle = transform.eulerAngles.y;

        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            angle += 400f * Time.deltaTime;
            facing = transform.rotation = Quaternion.Euler(0f, angle, 0f);
            if (Random.value < 0.25f)
                Dragon.FireBurst(transform.position + Random.insideUnitSphere * 0.4f, Vector3.up, 2, 3f, 0.6f);
            yield return null;
        }

        armsUp = false;
        yield return new WaitForSeconds(1f);
    }

    // A fireball shot high into the sky, which bursts at the top
    IEnumerator Fireball()
    {
        Say("Fireball!", 1.5f);
        armsUp = true;
        yield return new WaitForSeconds(0.6f);

        Vector3 hands = transform.position + Vector3.up * 1.3f;
        Dragon.FireBurst(hands, Vector3.up, 12, 14f, 0.8f);
        yield return new WaitForSeconds(0.8f);
        Dragon.FireBurst(hands + Vector3.up * 10f, Random.onUnitSphere, 10, 4f, 0.6f);

        armsUp = false;
        yield return new WaitForSeconds(1f);
    }

    // Lights a little fire on the ash in front of them
    IEnumerator LightAFire()
    {
        Vector3 ahead = transform.position + transform.forward * 1.5f;
        int x = Mathf.RoundToInt(ahead.x), z = Mathf.RoundToInt(ahead.z);
        int ground = GroundBelow(x, transform.position.y + 1f, z);
        if (ground < 0 || world.GetBlock(x, ground, z) != BlockWorld.Ash) yield break;
        if (world.GetBlock(x, ground + 1, z) != BlockWorld.Air) yield break;

        Say("Let's have a campfire!", 2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            headNod = 25f;
            yield return null;
        }
        headNod = 0f;

        world.SetBlock(x, ground + 1, z, BlockWorld.Fire);
        Dragon.FireBurst(new Vector3(x, ground + 1f, z), Vector3.up, 10, 4f, 0.6f);
        firesLit++;
        yield return new WaitForSeconds(1.5f);
    }
}
