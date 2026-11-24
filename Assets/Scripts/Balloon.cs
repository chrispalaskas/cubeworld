using UnityEngine;

// The hot air balloon. It lives in the village, by the well. Press E on the
// basket to climb in, then:
//   W / S:          float forwards (the way you're looking) or backwards
//   Space:          more hot air - up you go!
//   Left Ctrl:      less hot air - down you come
//   E:              climb out (only when you're near the ground, or right
//                   next to a sky island - it's a long way down otherwise!)
// Fly it anywhere: over the sea to the volcano, or up to the sky islands.
// It stays wherever you leave it (and if it ever gets lost, a new one turns
// up back at the village).
public class Balloon : Vehicle
{
    const float Speed = 5f;
    const float Climb = 3f;

    private float speed;
    private Transform flame;
    private Light burner;
    private static Material wicker, rope, red, yellow, fire;

    public override byte Item { get { return BlockWorld.Air; } }   // (you can't put it in your pocket)
    public override Vector3 Seat { get { return transform.position + Vector3.up * 0.1f; } }

    public static Balloon Make(BlockWorld world, Vector3 at)
    {
        return Make<Balloon>(world, at);
    }

    protected override void Build()
    {
        if (wicker == null)
        {
            wicker = BlockWorld.MakeMaterial(new Color(0.62f, 0.45f, 0.24f));
            rope = BlockWorld.MakeMaterial(new Color(0.30f, 0.22f, 0.12f));
            red = BlockWorld.MakeMaterial(new Color(0.90f, 0.20f, 0.20f));
            yellow = BlockWorld.MakeMaterial(new Color(1.00f, 0.82f, 0.20f));
            fire = BlockWorld.MakeMaterial(new Color(1.00f, 0.60f, 0.15f));
            fire.EnableKeyword("_EMISSION");
            fire.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 2f);
        }

        // The basket
        Part(new Vector3(0f, 0.03f, 0f), new Vector3(1.4f, 0.06f, 1.4f), wicker);
        Part(new Vector3(0.68f, 0.4f, 0f), new Vector3(0.06f, 0.75f, 1.4f), wicker);
        Part(new Vector3(-0.68f, 0.4f, 0f), new Vector3(0.06f, 0.75f, 1.4f), wicker);
        Part(new Vector3(0f, 0.4f, 0.68f), new Vector3(1.4f, 0.75f, 0.06f), wicker);
        Part(new Vector3(0f, 0.4f, -0.68f), new Vector3(1.4f, 0.75f, 0.06f), wicker);

        // Ropes up to the balloon
        foreach (Vector2 c in new[] { new Vector2(-0.65f, -0.65f), new Vector2(0.65f, -0.65f), new Vector2(-0.65f, 0.65f), new Vector2(0.65f, 0.65f) })
            Part(new Vector3(c.x, 1.9f, c.y), new Vector3(0.04f, 2.4f, 0.04f), rope);

        // The burner, and its flame
        Part(new Vector3(0f, 2.75f, 0f), new Vector3(0.3f, 0.12f, 0.3f), rope);
        flame = Part(new Vector3(0f, 2.95f, 0f), new Vector3(0.18f, 0.3f, 0.18f), fire);
        burner = flame.gameObject.AddComponent<Light>();
        burner.type = LightType.Point;
        burner.color = new Color(1f, 0.6f, 0.25f);
        burner.range = 6f;
        burner.intensity = 1.2f;

        // The big round balloon, in red and yellow stripes
        float[] widths = { 1.4f, 2.4f, 3.1f, 3.4f, 3.4f, 3.1f, 2.5f, 1.5f };
        for (int i = 0; i < widths.Length; i++)
        {
            float w = widths[i];
            Part(new Vector3(0f, 3.3f + i * 0.5f, 0f), new Vector3(w, 0.52f, w * 0.55f), i % 2 == 0 ? red : yellow);
            Part(new Vector3(0f, 3.3f + i * 0.5f, 0f), new Vector3(w * 0.55f, 0.52f, w), i % 2 == 0 ? yellow : red);
        }
    }

    protected override string SaveAs()
    {
        Vector3 p = transform.position;
        return "balloon," + Num(p.x) + "," + Num(p.y) + "," + Num(p.z);
    }

    bool Solid(Vector3 at)
    {
        Vector3Int cell = Vector3Int.RoundToInt(at);
        if (cell.y >= BlockWorld.SizeY) return false;
        byte type = world.GetBlock(cell.x, cell.y, cell.z);
        return type != BlockWorld.Air && type != BlockWorld.Water && !BlockWorld.IsFlower(type) && type != BlockWorld.Fire;
    }

    // Would something block the basket (or you standing in it) here?
    bool Blocked(Vector3 at)
    {
        return Solid(at + Vector3.up * 0.3f) || Solid(at + Vector3.up * 1.2f) || Solid(at + Vector3.up * 1.9f);
    }

    public override void Drive(float throttle, Transform rider)
    {
        Vector3 flat = rider.forward;
        flat.y = 0f;
        flat.Normalize();
        speed = Mathf.MoveTowards(speed, throttle * Speed, 4f * Time.deltaTime);

        float up = 0f;
        if (Input.GetKey(KeyCode.Space)) up = Climb;
        else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) up = -Climb;
        if (up > 0f) Achievements.Unlock("balloon");

        Vector3 p = transform.position;
        Vector3 across = p + flat * speed * Time.deltaTime;
        across.x = Mathf.Clamp(across.x, 1f, BlockWorld.SizeX - 2f);
        across.z = Mathf.Clamp(across.z, 1f, BlockWorld.SizeZ - 2f);
        if (!Blocked(across)) p = across; else speed = 0f;

        Vector3 updown = p + Vector3.up * up * Time.deltaTime;
        updown.y = Mathf.Min(updown.y, BlockWorld.SizeY + 6f);
        bool landed = up < 0f && Solid(updown + Vector3.down * 0.1f);
        if (!landed && !Blocked(updown) && !(up > 0f && Solid(updown + Vector3.up * 3.2f))) p = updown;
        transform.position = p;

        // The flame roars when it's going up
        flame.localScale = new Vector3(0.18f, up > 0f ? 0.6f : 0.3f, 0.18f);
        burner.intensity = up > 0f ? 2.5f : 1.2f;
        if (Mathf.Abs(speed) > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1.5f * Time.deltaTime);
    }

    // Is it safe to climb out here? (Something to stand on, not too far below)
    public bool SafeToLeave(Vector3 riderRight)
    {
        Vector3 off = transform.position + Vector3.up * 0.7f + riderRight * 1f;
        for (int d = 0; d <= 4; d++)
        {
            if (Solid(off + Vector3.down * d)) return true;
            if (Solid(transform.position + Vector3.down * (d + 0.1f))) return true;
        }
        return false;
    }

    void Update()
    {
        // The flame flickers, and it sways gently in the wind
        if (flame != null) flame.localScale = new Vector3(0.18f, flame.localScale.y * (0.95f + Mathf.Sin(Time.time * 20f) * 0.05f), 0.18f);
        if (!Ridden) transform.rotation *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.5f) * 2f * Time.deltaTime, 0f);
    }
}
