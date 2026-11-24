using UnityEngine;

// The Iron Golems that look after the village.
//
// They're big, slow and friendly: they wander about near the village well, and
// if a monster comes close they stomp over and bash it high into the air.
// They never hurt you (unless you hit them first - then they just stand firm).
// Two of them live in every village. If one is ever beaten, a new one turns up
// a few minutes later. Beat one and it drops some iron.
public class IronGolem : Monster
{
    public Vector3 Post;   // the village well they look after

    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private float smashSwing;
    private Monster foe;
    private float smashTimer;
    private static Material iron, rust, core, eyes;

    protected override string Called { get { return "Iron Golem"; } }
    protected override int MostHealth { get { return 60; } }
    protected override float WalkSpeed { get { return 0.9f; } }
    protected override float ChaseSpeed { get { return 2.6f; } }
    protected override bool FireProof { get { return true; } }
    protected override bool FearsStar { get { return false; } }
    public override float AimHeight { get { return 1.3f; } }
    public override float HitSize { get { return 1.0f; } }
    public override bool Hostile { get { return false; } }   // it's on your side

    protected override bool GuardPost(out Vector3 post) { post = Post; return true; }

    // Heads for the nearest monster, if there is one close to the village
    protected override bool AnimalGoal(ref Vector3 goal, ref float speed)
    {
        if (foe == null) return false;
        goal = foe.transform.position;
        speed = ChaseSpeed;
        return true;
    }

    protected override void Think()
    {
        smashTimer -= Time.deltaTime;

        // Look for a monster (every so often, or if the last one's gone)
        if (foe == null)
        {
            float best = 14f;
            foreach (Monster monster in All)
            {
                if (!monster.Hostile) continue;
                float d = Vector3.Distance(monster.transform.position, transform.position);
                bool nearHome = Vector3.Distance(new Vector3(monster.transform.position.x, Post.y, monster.transform.position.z), Post) < 22f;
                if (d < best && nearHome) { best = d; foe = monster; }
            }
        }
        if (foe == null) return;

        // Too far from home now? Give up and go back
        if (Vector3.Distance(new Vector3(transform.position.x, Post.y, transform.position.z), Post) > 26f) { foe = null; return; }

        Vector3 to = foe.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);

        if (to.magnitude < 2.6f && smashTimer <= 0f)
        {
            smashTimer = 1.4f;
            smashSwing = 1f;
            foe.Hit(14, transform.position);
            Sounds.Thwack(transform.position);
            // (if that finished it, "foe" becomes null by itself once it's destroyed)
        }
    }

    protected override void BuildBody()
    {
        if (iron == null)
        {
            iron = BlockWorld.MakeMaterial(new Color(0.70f, 0.72f, 0.76f));
            rust = BlockWorld.MakeMaterial(new Color(0.55f, 0.36f, 0.24f));
            core = BlockWorld.MakeMaterial(new Color(1.00f, 0.62f, 0.15f));
            core.EnableKeyword("_EMISSION");
            core.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f));
            eyes = BlockWorld.MakeMaterial(new Color(0.55f, 0.85f, 1.00f));
            eyes.EnableKeyword("_EMISSION");
            eyes.SetColor("_EmissionColor", new Color(0.4f, 0.75f, 1f));
        }

        Transform model = Model(2.2f);
        AddPart(model, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.46f, 0.3f), iron);               // broad chest
        AddPart(model, new Vector3(0f, 0.38f, 0f), new Vector3(0.3f, 0.12f, 0.22f), rust);             // belt
        AddPart(model, new Vector3(0f, 0.62f, 0.155f), new Vector3(0.14f, 0.14f, 0.02f), core);        // a glowing heart
        foreach (float x in new[] { -0.2f, 0.2f })                                                    // rivets
            AddPart(model, new Vector3(x, 0.8f, 0.155f), new Vector3(0.04f, 0.04f, 0.02f), rust);

        leftLeg = Pivot(model, new Vector3(-0.12f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.12f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
            AddPart(leg, new Vector3(0f, -0.18f, 0f), new Vector3(0.16f, 0.36f, 0.2f), iron);

        leftArm = Pivot(model, new Vector3(-0.33f, 0.8f, 0f));
        rightArm = Pivot(model, new Vector3(0.33f, 0.8f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.14f, 0.6f, 0.16f), iron);
            AddPart(arm, new Vector3(0f, -0.62f, 0f), new Vector3(0.18f, 0.1f, 0.2f), rust);   // heavy fists
        }

        Transform head = Pivot(model, new Vector3(0f, 0.86f, 0.02f));
        AddPart(head, new Vector3(0f, 0.1f, 0f), new Vector3(0.22f, 0.2f, 0.22f), iron);
        AddPart(head, new Vector3(-0.055f, 0.13f, 0.112f), new Vector3(0.05f, 0.04f, 0.01f), eyes);
        AddPart(head, new Vector3(0.055f, 0.13f, 0.112f), new Vector3(0.05f, 0.04f, 0.01f), eyes);
        AddPart(head, new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.06f, 0.06f), rust);    // a little knob on top
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 5f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 25f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

        smashSwing = Mathf.Max(0f, smashSwing - Time.deltaTime * 2.5f);
        float smash = Mathf.Sin(smashSwing * Mathf.PI) * -120f;
        leftArm.localRotation = Quaternion.Euler(-swing * 0.6f + smash, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(swing * 0.6f + smash, 0f, 0f);
    }

    protected override void DropLoot()
    {
        Drop(BlockWorld.Iron, Random.Range(3, 6));
    }
}
