using UnityEngine;

// Owls: soft brown birds with big round eyes that perch up in the trees.
// Hold some seeds, look at one and press E to tame it - then it rides on your
// shoulder wherever you go, and keeps watch. If a monster comes anywhere
// near, it hoots to warn you ("Hoo hoo! Something's coming!").
public class Owl : Monster
{
    public bool Tamed { get; set; }

    private Transform leftWing, rightWing, head;
    private float hootTimer = 3f;
    private float warnedAt = -100f;
    private static Material feathers, chest, eyeRing, eyes, beak;

    public override bool Hostile { get { return false; } }
    protected override string Called { get { return "owl"; } }
    protected override int MostHealth { get { return 6; } }
    protected override bool Flies { get { return true; } }
    protected override float WalkSpeed { get { return 2f; } }
    protected override float ChaseSpeed { get { return 3.5f; } }
    public override float AimHeight { get { return 0.2f; } }
    public override float HitSize { get { return Tamed ? 0.01f : 0.4f; } }   // (one on your shoulder doesn't get in the way)

    // On your shoulder, it doesn't fly about by itself
    protected override bool Frozen { get { return Tamed; } }

    public override bool Interact(byte holding)
    {
        if (Tamed) return false;
        const byte Seeds = 168;
        if (holding != Seeds || BlockInteractor.Me == null || !BlockInteractor.Me.TakeOne(holding))
        {
            PortalTraveller.Announce("Hoo? (Owls love seeds!)");
            return true;
        }
        Tamed = true;
        Pets.Add("owl");
        Achievements.Unlock("owl");
        Sounds.Hoot(transform.position);
        PortalTraveller.Announce("Hoo hoo! The owl is your friend now. It'll ride on your shoulder and warn you about monsters!");
        return true;
    }

    protected override void BuildBody()
    {
        if (feathers == null)
        {
            feathers = BlockWorld.MakeMaterial(new Color(0.50f, 0.36f, 0.22f));
            chest = BlockWorld.MakeMaterial(new Color(0.85f, 0.75f, 0.58f));
            eyeRing = BlockWorld.MakeMaterial(new Color(0.95f, 0.92f, 0.85f));
            eyes = BlockWorld.MakeMaterial(new Color(1.00f, 0.70f, 0.10f));
            beak = BlockWorld.MakeMaterial(new Color(0.30f, 0.25f, 0.20f));
        }
        Transform model = Model(1f);
        AddPart(model, new Vector3(0f, 0.18f, 0f), new Vector3(0.2f, 0.26f, 0.18f), feathers);
        AddPart(model, new Vector3(0f, 0.16f, 0.085f), new Vector3(0.14f, 0.18f, 0.02f), chest);
        head = Pivot(model, new Vector3(0f, 0.33f, 0f));
        AddPart(head, new Vector3(0f, 0.07f, 0f), new Vector3(0.22f, 0.15f, 0.18f), feathers);
        foreach (float x in new[] { -0.05f, 0.05f })
        {
            AddPart(head, new Vector3(x, 0.08f, 0.09f), new Vector3(0.07f, 0.07f, 0.01f), eyeRing);   // big round eyes
            AddPart(head, new Vector3(x, 0.08f, 0.096f), new Vector3(0.035f, 0.035f, 0.01f), eyes);
            AddPart(head, new Vector3(x * 1.6f, 0.17f, 0f), new Vector3(0.04f, 0.05f, 0.04f), feathers); // ear tufts
        }
        AddPart(head, new Vector3(0f, 0.04f, 0.1f), new Vector3(0.03f, 0.04f, 0.03f), beak);
        leftWing = Pivot(model, new Vector3(-0.1f, 0.24f, 0f));
        rightWing = Pivot(model, new Vector3(0.1f, 0.24f, 0f));
        AddPart(leftWing, new Vector3(-0.02f, -0.08f, 0f), new Vector3(0.04f, 0.2f, 0.15f), feathers);
        AddPart(rightWing, new Vector3(0.02f, -0.08f, 0f), new Vector3(0.04f, 0.2f, 0.15f), feathers);
    }

    protected override void Animate(bool walking)
    {
        float flap = walking ? Mathf.Sin(Time.time * 16f) * 50f : 0f;
        leftWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
        rightWing.localRotation = Quaternion.Euler(0f, 0f, flap);
        head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.7f + transform.position.x) * 60f, 0f);   // owls turn their heads right round!
    }

    protected override void OnDefeated()
    {
        if (Tamed) Pets.Remove("owl");
    }

    // Riding on your shoulder, and keeping watch
    void LateUpdate()
    {
        if (!Tamed || BlockInteractor.Me == null) return;
        Transform me = BlockInteractor.Me.transform;
        transform.position = me.position + Vector3.up * 1.45f + me.right * 0.42f - me.forward * 0.1f;
        transform.rotation = me.rotation;
        head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.5f) * 70f, 0f);

        if (BlockInteractor.InventoryOpen) return;
        hootTimer -= Time.deltaTime;
        if (hootTimer > 0f) return;
        hootTimer = 2f;
        foreach (Monster monster in All)
        {
            if (!monster.Hostile || monster is Mimic) continue;   // (even owls get fooled by mimics!)
            if (Vector3.Distance(monster.transform.position, me.position) > 16f) continue;
            if (Time.time < warnedAt + 20f) return;
            warnedAt = Time.time;
            Sounds.Hoot(transform.position);
            PortalTraveller.Announce("Hoo hoo! Your owl says: something's coming - watch out!");
            return;
        }
    }
}
