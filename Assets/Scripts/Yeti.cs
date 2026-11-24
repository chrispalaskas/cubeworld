using UnityEngine;

// The Yeti: a big, shaggy snow monster who lives deep in the ice caves
// (under the snowy land - see MorePlaces.cs). In survival, he wakes up when
// you reach the middle of the caves. He stomps after you, swipes with his
// huge paws, and throws snowballs from further away. Beat him for his frozen
// treasure: diamonds, a golden apple and glowing ice.
public class Yeti : Monster
{
    private Transform leftArm, rightArm, leftLeg, rightLeg, head;
    private float throwTimer = 2.5f;
    private float swipe;
    private GUIStyle barStyle;
    private static Material fur, belly, face, eyes, horn;

    protected override string Called { get { return "yeti"; } }
    protected override int MostHealth { get { return 55; } }
    protected override float WalkSpeed { get { return 1f; } }
    protected override float ChaseSpeed { get { return 2.3f; } }
    protected override float NoticeRange { get { return 22f; } }
    protected override int HitDamage { get { return 5; } }
    protected override float HitRange { get { return 2f; } }
    protected override float HitSeconds { get { return 1.4f; } }
    public override float AimHeight { get { return 1.2f; } }
    public override float HitSize { get { return 1.1f; } }

    // A swipe up close, and snowballs from further off
    protected override void Attack(PlayerHealth target, Vector3 toPlayer, float flat)
    {
        if (flat < HitRange + 0.5f && attackTimer <= 0.2f) swipe = 1f;
        base.Attack(target, toPlayer, flat);

        throwTimer -= Time.deltaTime;
        if (throwTimer > 0f || flat < 3.5f || flat > 18f) return;
        Vector3 from = transform.position + Vector3.up * 2.1f + transform.forward * 0.5f;
        Vector3 to = target.transform.position + Vector3.up;
        if (!CanSee(from, to)) return;

        throwTimer = Random.Range(2.2f, 3.2f);
        swipe = 1f;
        MonsterShot.Snowball(from, MonsterShot.Aim(from, to, 12f, true));
    }

    protected override void BuildBody()
    {
        if (fur == null)
        {
            fur = BlockWorld.MakeMaterial(new Color(0.93f, 0.95f, 1.00f));
            belly = BlockWorld.MakeMaterial(new Color(0.78f, 0.84f, 0.95f));
            face = BlockWorld.MakeMaterial(new Color(0.45f, 0.55f, 0.75f));
            eyes = BlockWorld.MakeMaterial(new Color(0.20f, 0.90f, 1.00f));
            eyes.EnableKeyword("_EMISSION");
            eyes.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 1f));
            horn = BlockWorld.MakeMaterial(new Color(0.55f, 0.50f, 0.42f));
        }

        Transform model = Model(2.4f);   // big - but he still fits in the caves
        AddPart(model, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.45f, 0.36f), fur);            // a big round body
        AddPart(model, new Vector3(0f, 0.53f, 0.17f), new Vector3(0.32f, 0.3f, 0.04f), belly);
        leftLeg = Pivot(model, new Vector3(-0.13f, 0.33f, 0f));
        rightLeg = Pivot(model, new Vector3(0.13f, 0.33f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
        {
            AddPart(leg, new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.3f, 0.2f), fur);
            AddPart(leg, new Vector3(0f, -0.3f, 0.05f), new Vector3(0.2f, 0.06f, 0.28f), face);      // big flat feet
        }
        leftArm = Pivot(model, new Vector3(-0.32f, 0.72f, 0f));
        rightArm = Pivot(model, new Vector3(0.32f, 0.72f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.5f, 0.18f), fur);         // long shaggy arms
            AddPart(arm, new Vector3(0f, -0.52f, 0.02f), new Vector3(0.18f, 0.08f, 0.2f), face);     // paws
        }
        head = Pivot(model, new Vector3(0f, 0.78f, 0.02f));
        AddPart(head, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.24f, 0.28f), fur);
        AddPart(head, new Vector3(0f, 0.08f, 0.141f), new Vector3(0.2f, 0.14f, 0.01f), face);
        AddPart(head, new Vector3(-0.05f, 0.11f, 0.147f), new Vector3(0.05f, 0.03f, 0.01f), eyes);
        AddPart(head, new Vector3(0.05f, 0.11f, 0.147f), new Vector3(0.05f, 0.03f, 0.01f), eyes);
        AddPart(head, new Vector3(0f, 0.04f, 0.147f), new Vector3(0.1f, 0.025f, 0.01f), horn);        // a grumpy mouth
        AddPart(head, new Vector3(-0.17f, 0.22f, 0f), new Vector3(0.05f, 0.1f, 0.05f), horn);         // little curly horns
        AddPart(head, new Vector3(0.17f, 0.22f, 0f), new Vector3(0.05f, 0.1f, 0.05f), horn);
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 4.5f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 22f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

        swipe = Mathf.Max(0f, swipe - Time.deltaTime * 2.2f);
        float paw = Mathf.Sin(swipe * Mathf.PI) * -120f;
        leftArm.localRotation = Quaternion.Euler(-swing * 0.6f + paw, 0f, 10f);
        rightArm.localRotation = Quaternion.Euler(swing * 0.6f + paw, 0f, -10f);
        head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.3f) * 10f, 0f);
    }

    protected override void DropLoot()
    {
        Drop(BlockWorld.Diamond, 5);
        Drop(95, 1);   // a golden apple
        Drop(BlockWorld.GlowIce, 4);
    }

    protected override void OnDefeated()
    {
        Structures.YetiDefeated = true;
        Achievements.Unlock("yeti");
        Wallet.Earn(15, "You beat the YETI!");
        PortalTraveller.Announce("You defeated the YETI! His frozen treasure is yours!");
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
        GUI.color = new Color(0.4f, 0.8f, 1f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(health / (float)MostHealth), bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(bar.x, bar.y - 22f, bar.width, 20f), "THE YETI", barStyle);
        GUI.color = before;
    }
}
