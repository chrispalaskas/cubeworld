using UnityEngine;

// More bad creatures for survival worlds (what they all share is in Monster.cs).
//
//   Skeleton:  keeps its distance and shoots arrows at you. Get up close!
//   Spider:    fast, climbs straight up walls, and pounces.
//   Slime:     a big bouncy green cube. Hit it and it splits into two
//              smaller ones - and those split again.
//   Fire Imp:  only in the Nether. Flies about and throws fireballs. Lava
//              and fire don't bother it one bit.

// --- Skeleton ---

public class Skeleton : Monster
{
    const float ArrowSpeed = 16f;

    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private static Material bone, dark, bowWood;

    protected override string Called { get { return "skeleton"; } }
    protected override void DropLoot() { Drop(BlockWorld.Arrow, Random.Range(0, 3)); }
    protected override int MostHealth { get { return 8; } }
    protected override float ChaseSpeed { get { return 2.0f; } }
    protected override float NoticeRange { get { return 20f; } }

    // Not too close, not too far - just right for shooting
    protected override Vector3 ChaseGoal(Vector3 player, float distance)
    {
        Vector3 me = transform.position;
        if (distance < 7f)
        {
            Vector3 away = me - player;
            away.y = 0f;
            return me + away.normalized * 3f;
        }
        return distance > 12f ? player : me;
    }

    protected override void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f || flat > 18f) return;

        Vector3 from = transform.position + Vector3.up * 1.25f + transform.forward * 0.4f;
        Vector3 to = target.transform.position + Vector3.up * 1.0f;
        if (!CanSee(from, to)) return;

        attackTimer = 2.2f;
        MonsterShot.Fire(from, MonsterShot.Aim(from, to, ArrowSpeed, true), false, 2, "A skeleton's arrow got you!");
    }

    protected override void BuildBody()
    {
        if (bone == null)
        {
            bone = BlockWorld.MakeMaterial(new Color(0.88f, 0.87f, 0.82f));
            dark = BlockWorld.MakeMaterial(new Color(0.12f, 0.12f, 0.12f));
            bowWood = BlockWorld.MakeMaterial(new Color(0.50f, 0.34f, 0.18f));
        }

        Transform model = Model(1.45f);

        // A spine, and ribs across it
        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.06f, 0.44f, 0.06f), bone);
        for (int i = 0; i < 3; i++)
            AddPart(model, new Vector3(0f, 0.68f - i * 0.10f, 0f), new Vector3(0.28f, 0.04f, 0.14f), bone);
        AddPart(model, new Vector3(0f, 0.38f, 0f), new Vector3(0.24f, 0.05f, 0.10f), bone);   // hips

        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
            AddPart(leg, new Vector3(0f, -0.18f, 0f), new Vector3(0.05f, 0.36f, 0.05f), bone);

        leftArm = Pivot(model, new Vector3(-0.17f, 0.76f, 0f));
        rightArm = Pivot(model, new Vector3(0.17f, 0.76f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
            AddPart(arm, new Vector3(0f, -0.18f, 0f), new Vector3(0.05f, 0.36f, 0.05f), bone);

        // A bow in its left hand
        AddPart(leftArm, new Vector3(0f, -0.36f, 0f), new Vector3(0.03f, 0.04f, 0.40f), bowWood);
        AddPart(leftArm, new Vector3(0f, -0.38f, 0.19f), new Vector3(0.03f, 0.10f, 0.04f), bowWood);
        AddPart(leftArm, new Vector3(0f, -0.38f, -0.19f), new Vector3(0.03f, 0.10f, 0.04f), bowWood);

        // A skull with big dark eyes
        Transform head = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(head, new Vector3(0f, 0.15f, 0f), new Vector3(0.28f, 0.28f, 0.28f), bone);
        AddPart(head, new Vector3(-0.07f, 0.17f, 0.141f), new Vector3(0.08f, 0.07f, 0.01f), dark);
        AddPart(head, new Vector3(0.07f, 0.17f, 0.141f), new Vector3(0.08f, 0.07f, 0.01f), dark);
        AddPart(head, new Vector3(0f, 0.05f, 0.141f), new Vector3(0.16f, 0.03f, 0.01f), dark);
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 7f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 30f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

        // Arms up, aiming, when it's after you
        float aim = chasing ? -85f : -swing;
        leftArm.localRotation = Quaternion.Euler(aim, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(chasing ? -80f : swing, 0f, 0f);
    }
}

// --- Spider ---

public class Spider : Monster
{
    private Transform[] legs = new Transform[8];
    private float pounceTimer = 2f;
    private static Material body, eyes;

    protected override string Called { get { return "spider"; } }
    protected override void DropLoot() { Drop(BlockWorld.StringItem, Random.Range(0, 3)); }
    protected override int MostHealth { get { return 8; } }
    protected override float WalkSpeed { get { return 2.0f; } }
    protected override float ChaseSpeed { get { return 3.8f; } }
    protected override bool ClimbsWalls { get { return true; } }
    protected override int HitDamage { get { return 2; } }
    protected override float HitRange { get { return 1.1f; } }
    protected override float HitSeconds { get { return 1.0f; } }
    public override float AimHeight { get { return 0.22f; } }
    public override float HitSize { get { return 0.5f; } }

    // Every so often, a big pounce at you
    protected override void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        pounceTimer -= Time.deltaTime;
        if (pounceTimer <= 0f && flat > 2f && flat < 5f && OnGround)
        {
            pounceTimer = 3f;
            fallSpeed = 6.5f;
            knock = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized * 6f;
        }
        base.Attack(target, toPlayer, flat);
    }

    protected override void BuildBody()
    {
        if (body == null)
        {
            body = BlockWorld.MakeMaterial(new Color(0.18f, 0.15f, 0.14f));
            eyes = BlockWorld.MakeMaterial(new Color(0.95f, 0.10f, 0.10f));
        }

        Transform model = Model(0.55f);   // about knee high
        AddPart(model, new Vector3(0f, 0.40f, -0.15f), new Vector3(0.70f, 0.40f, 0.75f), body);   // big back end
        AddPart(model, new Vector3(0f, 0.38f, 0.38f), new Vector3(0.42f, 0.32f, 0.36f), body);    // head

        // Four red eyes
        for (int i = 0; i < 4; i++)
            AddPart(model, new Vector3(-0.15f + i * 0.1f, 0.44f + (i % 2) * 0.05f, 0.561f), new Vector3(0.06f, 0.06f, 0.01f), eyes);

        // Eight long legs, four each side
        for (int i = 0; i < 8; i++)
        {
            float side = i < 4 ? -1f : 1f;
            float along = 0.25f - (i % 4) * 0.17f;
            Transform leg = Pivot(model, new Vector3(side * 0.25f, 0.42f, along));
            leg.localRotation = Quaternion.Euler(0f, side * (20f - (i % 4) * 15f), side * 35f);
            AddPart(leg, new Vector3(side * 0.35f, 0f, 0f), new Vector3(0.70f, 0.06f, 0.06f), body);
            legs[i] = leg;
        }
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 14f : 0f;
        for (int i = 0; i < 8; i++)
        {
            float side = i < 4 ? -1f : 1f;
            float wiggle = walking ? Mathf.Sin(walkCycle + i * 1.6f) * 15f : 0f;
            legs[i].localRotation = Quaternion.Euler(0f, side * (20f - (i % 4) * 15f) + wiggle, side * 35f);
        }
    }
}

// --- Slime ---

public class Slime : Monster
{
    // Set just before a slime is made, so it knows how big to be
    public static int NextSize = 3;

    private int size;
    private float hopTimer = 1f;
    private Transform model;
    private static Material green, face;

    protected override string Called { get { return "slime"; } }
    protected override int MostHealth { get { return size >= 3 ? 8 : size == 2 ? 4 : 1; } }

    // It only gets anywhere while it's in the air, mid-bounce
    protected override float WalkSpeed { get { return OnGround ? 0f : 1.5f; } }
    protected override float ChaseSpeed { get { return OnGround ? 0f : 3.2f; } }

    protected override int HitDamage { get { return size; } }
    protected override float HitRange { get { return 0.8f + size * 0.3f; } }
    public override float AimHeight { get { return size * 0.3f; } }
    public override float HitSize { get { return 0.3f + size * 0.25f; } }

    public static Slime Make(BlockWorld world, Vector3 feet, int size)
    {
        NextSize = size;
        Slime slime = Spawn<Slime>(world, feet);
        NextSize = 3;
        return slime;
    }

    // Boing... boing... boing
    protected override void Think()
    {
        hopTimer -= Time.deltaTime;
        if (OnGround && hopTimer <= 0f)
        {
            hopTimer = chasing ? 0.6f : 1.8f;
            fallSpeed = 6f + size * 0.5f;
        }
    }

    protected override void BuildBody()
    {
        size = Mathf.Clamp(NextSize, 1, 3);
        if (green == null)
        {
            green = BlockWorld.MakeMaterial(new Color(0.40f, 0.85f, 0.35f));
            face = BlockWorld.MakeMaterial(new Color(0.10f, 0.25f, 0.10f));
        }

        model = Model(size * 0.6f);
        AddPart(model, new Vector3(0f, 0.5f, 0f), Vector3.one, green);
        AddPart(model, new Vector3(-0.2f, 0.62f, 0.501f), new Vector3(0.16f, 0.16f, 0.01f), face);   // eyes
        AddPart(model, new Vector3(0.2f, 0.62f, 0.501f), new Vector3(0.16f, 0.16f, 0.01f), face);
        AddPart(model, new Vector3(0f, 0.32f, 0.501f), new Vector3(0.12f, 0.08f, 0.01f), face);      // mouth
    }

    // Squashed flat on the ground, stretched tall in the air
    protected override void Animate(bool walking)
    {
        float s = size * 0.6f;
        float squash = OnGround ? 0.8f : 1.1f;
        model.localScale = Vector3.Lerp(model.localScale, new Vector3(s / Mathf.Sqrt(squash), s * squash, s / Mathf.Sqrt(squash)), 12f * Time.deltaTime);
    }

    // Splits into two smaller slimes!
    protected override void OnDefeated()
    {
        if (size <= 1) return;
        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), 0.2f, Random.Range(-0.4f, 0.4f));
            Make(world, transform.position + offset, size - 1);
        }
    }
}

// --- Fire Imp (the Nether) ---

public class FireImp : Monster
{
    private Transform leftWing, rightWing;
    private static Material red, horn, glow, wing;

    protected override string Called { get { return "fire imp"; } }
    protected override int MostHealth { get { return 6; } }
    protected override float WalkSpeed { get { return 1.5f; } }
    protected override float ChaseSpeed { get { return 3f; } }
    protected override float NoticeRange { get { return 24f; } }
    protected override bool Flies { get { return true; } }
    protected override bool FireProof { get { return true; } }
    public override float AimHeight { get { return 0.5f; } }

    // Hangs about a little way off, throwing fireballs
    protected override Vector3 ChaseGoal(Vector3 player, float distance)
    {
        Vector3 me = transform.position;
        if (distance < 6f)
        {
            Vector3 away = me - player;
            away.y = 0f;
            return me + away.normalized * 3f;
        }
        return distance > 10f ? player : me;
    }

    protected override void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f || flat > 20f) return;

        Vector3 from = transform.position + Vector3.up * 0.5f + transform.forward * 0.4f;
        Vector3 to = target.transform.position + Vector3.up * 1.0f;
        if (!CanSee(from, to)) return;

        attackTimer = 2.5f;
        MonsterShot.Fire(from, MonsterShot.Aim(from, to, 11f, false), true, 3, "A fire imp's fireball got you!");
    }

    protected override void BuildBody()
    {
        if (red == null)
        {
            red = BlockWorld.MakeMaterial(new Color(0.80f, 0.15f, 0.08f));
            horn = BlockWorld.MakeMaterial(new Color(0.20f, 0.10f, 0.08f));
            glow = BlockWorld.MakeMaterial(new Color(1.00f, 0.90f, 0.20f));
            wing = BlockWorld.MakeMaterial(new Color(1.00f, 0.50f, 0.10f));
        }

        Transform model = Model(1f);
        AddPart(model, new Vector3(0f, 0.35f, 0f), new Vector3(0.30f, 0.35f, 0.22f), red);   // body
        AddPart(model, new Vector3(0f, 0.68f, 0f), new Vector3(0.30f, 0.28f, 0.28f), red);   // head
        AddPart(model, new Vector3(-0.10f, 0.88f, 0f), new Vector3(0.05f, 0.14f, 0.05f), horn);
        AddPart(model, new Vector3(0.10f, 0.88f, 0f), new Vector3(0.05f, 0.14f, 0.05f), horn);
        AddPart(model, new Vector3(-0.07f, 0.71f, 0.141f), new Vector3(0.07f, 0.05f, 0.01f), glow);   // glowing eyes
        AddPart(model, new Vector3(0.07f, 0.71f, 0.141f), new Vector3(0.07f, 0.05f, 0.01f), glow);
        AddPart(model, new Vector3(0f, 0.12f, -0.18f), new Vector3(0.04f, 0.04f, 0.25f), red);       // pointy tail

        leftWing = Pivot(model, new Vector3(-0.12f, 0.45f, -0.10f));
        rightWing = Pivot(model, new Vector3(0.12f, 0.45f, -0.10f));
        AddPart(leftWing, new Vector3(-0.25f, 0f, 0f), new Vector3(0.50f, 0.25f, 0.03f), wing);
        AddPart(rightWing, new Vector3(0.25f, 0f, 0f), new Vector3(0.50f, 0.25f, 0.03f), wing);

        Light fire = gameObject.AddComponent<Light>();
        fire.type = LightType.Point;
        fire.color = new Color(1f, 0.5f, 0.2f);
        fire.range = 5f;
        fire.intensity = 1.5f;
    }

    protected override void Animate(bool walking)
    {
        float flap = Mathf.Sin(Time.time * 14f) * 35f;
        leftWing.localRotation = Quaternion.Euler(0f, 20f, flap);
        rightWing.localRotation = Quaternion.Euler(0f, -20f, -flap);
    }
}

// --- The Stone Golem (the boss, in the Golem Hall - see Structures.cs) ---

public class Golem : Monster
{
    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private float throwTimer = 3f;
    private float smashSwing;
    private static Material rock, moss, eyes;
    private GUIStyle barStyle;

    protected override string Called { get { return "Stone Golem"; } }
    protected override int MostHealth { get { return 80; } }
    protected override float WalkSpeed { get { return 0.8f; } }
    protected override float ChaseSpeed { get { return 1.7f; } }
    protected override float NoticeRange { get { return 24f; } }
    protected override bool FireProof { get { return true; } }
    protected override int HitDamage { get { return 6; } }
    protected override float HitRange { get { return 2.3f; } }
    protected override float HitSeconds { get { return 1.6f; } }
    public override float AimHeight { get { return 1.6f; } }
    public override float HitSize { get { return 1.3f; } }

    // Smashes you up close, and throws boulders from further away
    protected override void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        if (flat < HitRange + 0.5f && attackTimer <= 0.2f) smashSwing = 1f;
        base.Attack(target, toPlayer, flat);

        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f || flat < 4f || flat > 20f) return;
        Vector3 from = transform.position + Vector3.up * 3.2f + transform.forward * 0.6f;
        Vector3 to = target.transform.position + Vector3.up;
        if (!CanSee(from, to)) return;

        throwTimer = 3.5f;
        smashSwing = 1f;
        MonsterShot.Boulder(from, MonsterShot.Aim(from, to, 13f, true));
    }

    protected override void BuildBody()
    {
        if (rock == null)
        {
            rock = world.MaterialFor(BlockWorld.Cobblestone);
            moss = BlockWorld.MakeMaterial(new Color(0.30f, 0.50f, 0.22f));
            eyes = BlockWorld.MakeMaterial(new Color(1.00f, 0.50f, 0.10f));
            eyes.EnableKeyword("_EMISSION");
            eyes.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f));
        }

        Transform model = Model(2.6f);   // a giant!
        AddPart(model, new Vector3(0f, 0.62f, 0f), new Vector3(0.55f, 0.5f, 0.32f), rock);
        AddPart(model, new Vector3(0.12f, 0.8f, 0.17f), new Vector3(0.2f, 0.12f, 0.02f), moss);
        leftLeg = Pivot(model, new Vector3(-0.15f, 0.38f, 0f));
        rightLeg = Pivot(model, new Vector3(0.15f, 0.38f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
            AddPart(leg, new Vector3(0f, -0.19f, 0f), new Vector3(0.2f, 0.38f, 0.22f), rock);
        leftArm = Pivot(model, new Vector3(-0.36f, 0.82f, 0f));
        rightArm = Pivot(model, new Vector3(0.36f, 0.82f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.17f, 0.58f, 0.2f), rock);
            AddPart(arm, new Vector3(0f, -0.6f, 0f), new Vector3(0.22f, 0.12f, 0.24f), rock);   // big fists
        }
        Transform head = Pivot(model, new Vector3(0f, 0.87f, 0.02f));
        AddPart(head, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.26f, 0.28f), rock);
        AddPart(head, new Vector3(-0.07f, 0.14f, 0.141f), new Vector3(0.07f, 0.04f, 0.01f), eyes);
        AddPart(head, new Vector3(0.07f, 0.14f, 0.141f), new Vector3(0.07f, 0.04f, 0.01f), eyes);
        AddPart(head, new Vector3(-0.1f, 0.26f, 0f), new Vector3(0.1f, 0.03f, 0.2f), moss);
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 4f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 20f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

        smashSwing = Mathf.Max(0f, smashSwing - Time.deltaTime * 2f);
        float smash = Mathf.Sin(smashSwing * Mathf.PI) * -110f;
        leftArm.localRotation = Quaternion.Euler(-swing * 0.5f + smash, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(swing * 0.5f + smash, 0f, 0f);
    }

    protected override void DropLoot()
    {
        Drop(BlockWorld.Diamond, 8);
        Drop(95, 3);   // golden apples
    }

    protected override void OnDefeated()
    {
        Structures.GolemDefeated = true;
        Achievements.Unlock("golem");
        PortalTraveller.Announce("You defeated the STONE GOLEM! Its treasure is yours!");
        Sounds.Boom(transform.position);
    }

    // A health bar across the top of the screen
    void OnGUI()
    {
        if (BlockInteractor.Me == null || Vector3.Distance(BlockInteractor.Me.transform.position, transform.position) > 30f) return;
        if (barStyle == null) barStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        float width = Mathf.Min(500f, Screen.width - 40f);
        Rect bar = new Rect((Screen.width - width) / 2f, 96f, width, 16f);
        Color before = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.DrawTexture(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), Texture2D.whiteTexture);
        GUI.color = new Color(0.8f, 0.15f, 0.1f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(health / (float)MostHealth), bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(bar.x, bar.y - 22f, bar.width, 20f), "STONE GOLEM", barStyle);
        GUI.color = before;
    }
}
