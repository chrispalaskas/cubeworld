using System.Collections.Generic;
using UnityEngine;

// Every sound in the game, made from scratch out of bits of noise and tones
// (so there are no sound files to look after).
//
// Blocks sound different depending on what they're made of: stone clicks,
// dirt thuds, wood knocks, sand and snow go "shhh", leaves rustle, glass
// tinkles. There are also footsteps, splashes, pops for picking things up,
// munching, ouches, booms, and a groan, rattle, hiss or squelch for each
// kind of monster.
public static class Sounds
{
    const int Rate = 22050;

    public enum Kind { Stone, Dirt, Wood, Sand, Glass, Leaves, Snow, Metal }

    private static readonly Dictionary<string, AudioClip[]> made = new Dictionary<string, AudioClip[]>();

    // What a block is made of, as far as your ears can tell
    public static Kind KindOf(byte block)
    {
        if (block == BlockWorld.Dirt || block == BlockWorld.Grass || block == BlockWorld.Farmland ||
            block == BlockWorld.MagicGrass || block == BlockWorld.Ash || block == BlockWorld.Gravel) return Kind.Dirt;
        if (block == BlockWorld.Sand) return Kind.Sand;
        if (block == BlockWorld.Snow || block == BlockWorld.Cloud || BlockWorld.IsWool(block) || block == BlockWorld.Trampoline) return Kind.Snow;
        if (block == BlockWorld.Cactus) return Kind.Leaves;
        if (BlockWorld.IsRail(block) || block == BlockWorld.Spawner || BlockWorld.IsSlide(block)) return Kind.Metal;
        if (block == BlockWorld.PlankSlab || block == BlockWorld.Fence || BlockWorld.IsLadder(block) || BlockWorld.IsSign(block)) return Kind.Wood;
        if (block == BlockWorld.Leaves || BlockWorld.IsWheat(block) || block == BlockWorld.Rug) return Kind.Leaves;
        if (block == BlockWorld.Glass || block == BlockWorld.Ice || block == BlockWorld.Crystal || block == BlockWorld.Lamp ||
            block == BlockWorld.FireCrystal || block == BlockWorld.StarCrystal || block == BlockWorld.Window) return Kind.Glass;
        if (block == BlockWorld.Iron || block == BlockWorld.Gold || block == BlockWorld.Diamond || block == BlockWorld.Cable ||
            BlockWorld.IsPiston(block) || block == BlockWorld.Sink || block == BlockWorld.Fridge) return Kind.Metal;
        if (block == BlockWorld.Wood || block == BlockWorld.Planks || block == BlockWorld.Bookshelf || block == BlockWorld.Chest ||
            block == BlockWorld.CraftingTable || block == BlockWorld.NoteBlock || BlockWorld.IsDoor(block) ||
            block == BlockWorld.DoorItem || (block >= BlockWorld.StairsNorth && block <= BlockWorld.StairsNorth + 3) ||
            block == BlockWorld.MushroomStem || block == BlockWorld.MushroomCap ||
            (BlockWorld.IsFurniture(block) && !BlockWorld.IsFurnace(block))) return Kind.Wood;
        return Kind.Stone;
    }

    // --- The sounds ---

    public static void Dig(byte block, Vector3 at)   { Play("dig" + KindOf(block), at, 0.35f); }
    public static void Break(byte block, Vector3 at) { Play("break" + KindOf(block), at, 0.7f); }
    public static void Place(byte block, Vector3 at) { Play("place" + KindOf(block), at, 0.6f); }
    public static void Step(byte block, Vector3 at)  { Play("step" + KindOf(block), at, 0.25f); }
    public static void Splash(Vector3 at)  { Play("splash", at, 0.6f); }
    public static void Pop(Vector3 at)     { Play("pop", at, 0.4f); }
    public static void Eat(Vector3 at)     { Play("eat", at, 0.6f); }
    public static void Ouch(Vector3 at)    { Play("ouch", at, 0.7f); }
    public static void Thwack(Vector3 at)  { Play("thwack", at, 0.6f); }
    public static void Boom(Vector3 at)    { Play("boom", at, 1f); }
    public static void Door(Vector3 at)    { Play("placeWood", at, 0.6f); }
    public static void Flush(Vector3 at)   { Play("flush", at, 0.6f); }
    public static void Twang(Vector3 at)   { Play("twang", at, 0.5f); }
    public static void Whoosh(Vector3 at)  { Play("whoosh", at, 0.5f); }
    public static void Thunder(Vector3 at) { Play("thunder", at, 0.9f); }

    // A couple of seconds of rain, made to loop smoothly
    private static AudioClip rain;
    public static AudioClip RainClip()
    {
        if (rain == null)
        {
            System.Random dice = new System.Random(99);
            rain = Build("rain", 2f, dice, t => Noise(dice, 0.5f) * 0.35f + (dice.NextDouble() < 0.002 ? 0.6f : 0f));
        }
        return rain;
    }

    // "zombie", "skeleton", "spider", "slime" or "fire imp"
    public static void Monster(string who, Vector3 at) { Play("monster " + who, at, 0.6f); }

    static void Play(string name, Vector3 at, float volume)
    {
        AudioClip[] clips;
        if (!made.TryGetValue(name, out clips) || clips[0] == null)
        {
            clips = new AudioClip[3];   // three slightly different ones, so it doesn't sound like a robot
            for (int i = 0; i < clips.Length; i++) clips[i] = Make(name, i);
            made[name] = clips;
        }
        AudioSource.PlayClipAtPoint(clips[Random.Range(0, clips.Length)], at, volume);
    }

    // --- Making them ---

    static AudioClip Make(string name, int variant)
    {
        System.Random dice = new System.Random(name.GetHashCode() + variant * 7919);
        float wobble = 1f + (variant - 1) * 0.08f;   // each variant a little higher or lower

        switch (name)
        {
            case "splash": return Build(name, 0.5f, dice, t => Noise(dice, 0.25f) * Fade(t, 6f) * 0.8f + Tone(300f * wobble - t * 300f, t) * Fade(t, 10f) * 0.2f);
            case "pop":    return Build(name, 0.09f, dice, t => Tone(Mathf.Lerp(500f, 1100f, t / 0.09f) * wobble, t) * Fade(t, 30f));
            case "eat":    return Build(name, 0.45f, dice, t => Noise(dice, 0.3f) * Bursts(t, 3, 0.15f, 25f));
            case "ouch":   return Build(name, 0.22f, dice, t => Square(Mathf.Lerp(330f, 200f, t / 0.22f) * wobble, t) * Fade(t, 12f) * 0.5f);
            case "thwack": return Build(name, 0.12f, dice, t => (Noise(dice, 0.5f) * 0.6f + Tone(160f, t) * 0.6f) * Fade(t, 35f));
            case "boom":   return Build(name, 1.4f, dice, t => Noise(dice, 0.04f) * Fade(t, 3f) * 2.5f);
            case "flush":  return Build(name, 1.2f, dice, t => Noise(dice, 0.12f + 0.1f * Mathf.Sin(t * 20f)) * Mathf.Sin(Mathf.PI * t / 1.2f) * 1.2f);
            case "twang":  return Build(name, 0.25f, dice, t => Tone(420f * wobble, t) * Fade(t, 14f) * 0.5f);
            case "thunder": return Build(name, 2.5f, dice, t => Noise(dice, 0.03f) * (Fade(t, 1.5f) + Fade(Mathf.Abs(t - 0.4f), 6f)) * 3f);
            case "whoosh": return Build(name, 0.4f, dice, t => Noise(dice, 0.2f) * Mathf.Sin(Mathf.PI * t / 0.4f));

            case "monster zombie":   return Build(name, 0.9f, dice, t => Square(95f * wobble + Mathf.Sin(t * 9f) * 8f, t) * Mathf.Sin(Mathf.PI * t / 0.9f) * 0.35f);
            case "monster skeleton": return Build(name, 0.5f, dice, t => Noise(dice, 0.6f) * Bursts(t, 6, 0.08f, 60f) * 0.8f);
            case "monster spider":   return Build(name, 0.5f, dice, t => Noise(dice, 0.75f) * Mathf.Sin(Mathf.PI * t / 0.5f) * 0.6f);
            case "monster slime":    return Build(name, 0.3f, dice, t => Tone(Mathf.Lerp(260f, 110f, t / 0.3f) * wobble, t) * Fade(t, 8f) * 0.7f);
            case "monster fire imp": return Build(name, 0.6f, dice, t => Noise(dice, 0.45f) * Bursts(t, 8, 0.07f, 40f) + Square(600f * wobble + Mathf.Sin(t * 30f) * 60f, t) * Fade(t, 6f) * 0.15f);
        }

        // Block sounds: "dig", "break", "place" or "step", then the kind
        Kind kind = Kind.Stone;
        foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
            if (name.EndsWith(k.ToString())) kind = k;

        float length = name.StartsWith("break") ? 0.22f : name.StartsWith("step") ? 0.1f : 0.14f;
        float smooth = kind == Kind.Dirt ? 0.12f : kind == Kind.Sand || kind == Kind.Snow ? 0.3f : kind == Kind.Leaves ? 0.55f : 0.45f;
        float decay = kind == Kind.Sand || kind == Kind.Snow || kind == Kind.Leaves ? 14f : 28f;

        return Build(name, length, dice, t =>
        {
            float s = Noise(dice, smooth) * Fade(t, decay);
            if (kind == Kind.Wood) s = s * 0.5f + Tone(190f * wobble, t) * Fade(t, 30f) * 0.7f;
            if (kind == Kind.Metal) s = s * 0.4f + Tone(880f * wobble, t) * Fade(t, 16f) * 0.4f;
            if (kind == Kind.Glass) s = s * 0.3f + (Tone(1900f * wobble, t) + Tone(2600f * wobble, t)) * Fade(t, 12f) * 0.25f;
            if (kind == Kind.Stone) s += Tone(320f * wobble, t) * Fade(t, 40f) * 0.25f;
            return s;
        });
    }

    static AudioClip Build(string name, float seconds, System.Random dice, System.Func<float, float> sample)
    {
        lastNoise = 0f;
        float[] samples = new float[Mathf.Max(1, (int)(Rate * seconds))];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Clamp(sample((float)i / Rate), -1f, 1f) * 0.6f;

        AudioClip clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Hiss - smoothed out to make it duller (small "smooth" = deeper rumble)
    private static float lastNoise;
    static float Noise(System.Random dice, float smooth)
    {
        float raw = (float)dice.NextDouble() * 2f - 1f;
        lastNoise += (raw - lastNoise) * smooth;
        return lastNoise * (1.5f / Mathf.Sqrt(smooth + 0.05f)) * 0.5f;
    }

    static float Tone(float frequency, float t) { return Mathf.Sin(2f * Mathf.PI * frequency * t); }
    static float Square(float frequency, float t) { return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t)); }
    static float Fade(float t, float speed) { return Mathf.Exp(-t * speed); }

    // A few quick bursts in a row (munch munch munch, rattle rattle)
    static float Bursts(float t, int count, float gap, float speed)
    {
        float into = t % gap;
        return t / gap < count ? Mathf.Exp(-into * speed) : 0f;
    }
}
