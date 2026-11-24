using System.Collections.Generic;
using UnityEngine;

// Swords, made at the crafting table. Hold one and left click (or Q) to swing
// it. A chicken or squirrel in reach gets hit (people just get a fright,
// unless it's a diamond sword); otherwise the swing breaks the
// block you're looking at, just like an empty hand. Better swords reach further.
//
// This is part of BlockInteractor - the sword lives in the same hand as the
// sponge and the leash.
public partial class BlockInteractor
{
    const byte WoodenSword  = 30;
    const byte StoneSword   = 31;
    const byte IronSword    = 32;
    const byte GoldSword    = 33;
    const byte DiamondSword = 34;

    const float SwingSeconds = 0.25f;

    private GameObject swordInHand;
    private MeshRenderer swordBlade;
    private float swingTime;   // counts down during a swing
    private static readonly Dictionary<byte, Texture2D> swordPictures = new Dictionary<byte, Texture2D>();

    static bool IsSword(byte item)
    {
        return item >= WoodenSword && item <= DiamondSword;
    }

    // Wooden reaches 3 blocks, and each better sword half a block more,
    // up to 5 for diamond
    static float SwordReach(byte sword)
    {
        return 3f + (sword - WoodenSword) * 0.5f;
    }

    // The block each blade is made from - it borrows that block's look
    static byte BladeBlock(byte sword)
    {
        switch (sword)
        {
            case StoneSword:   return BlockWorld.Stone;
            case IronSword:    return BlockWorld.Iron;
            case GoldSword:    return BlockWorld.Gold;
            case DiamondSword: return BlockWorld.Diamond;
            default:           return BlockWorld.Planks;
        }
    }

    // --- Swinging ---

    // True if it hit something. (Missed? In creative the block in front breaks;
    // in survival you dig it the slow way, like with your hands.)
    bool Swing()
    {
        swingTime = SwingSeconds;

        MonoBehaviour animal = AnimalInSights(SwordReach(selected), true);
        if (animal == null)
        {
            if (!Survival) Break();
            return false;
        }

        // Let go of the leash first if it's the animal we're holding
        if (animal == caughtAnimal)
        {
            ((ILeashable)caughtAnimal).Release();
            caughtAnimal = null;
        }

        // Monsters take a few hits - better swords take fewer
        Monster monster = animal as Monster;
        if (monster != null)
        {
            Sounds.Thwack(monster.transform.position);
            monster.Hit(SwordDamage(selected), transform.position);
            return true;
        }

        // Only a diamond sword is strong enough to defeat a person.
        // Any other sword and they just say "Ouch!" and run off.
        // Dragons are far too tough: no harm done, but off they fly
        Dragon dragon = animal as Dragon;
        if (dragon != null)
        {
            dragon.Startle(transform.position);
            return true;
        }

        // Unicorns are magic: no harm done, but off they gallop
        Unicorn unicorn = animal as Unicorn;
        if (unicorn != null)
        {
            unicorn.Startle(transform.position);
            return true;
        }

        Villager person = animal as Villager;
        if (person != null)
        {
            if (selected == DiamondSword)
                person.Defeat();
            else
                person.Hurt(transform.position);
            return true;
        }

        Chicken chicken = animal as Chicken;
        if (chicken != null)
        {
            if (Survival) ItemDrop.Spawn(ChickenLeg, 1, chicken.transform.position + Vector3.up * 0.3f);   // (dinner)
            if (Survival) ItemDrop.Spawn(BlockWorld.Feather, Random.Range(0, 3), chicken.transform.position + Vector3.up * 0.3f);
            chicken.BlowUp();   // a puff of feathers
        }
        else
            Destroy(animal.gameObject);
        return true;
    }

    static int SwordDamage(byte sword)
    {
        if (Enchanted(sword)) return Mathf.RoundToInt(PlainSwordDamage(sword) * 1.5f) * (Mods.SuperStrength ? 3 : 1);   // magic!
        return PlainSwordDamage(sword) * (Mods.SuperStrength ? 3 : 1);
    }

    static int PlainSwordDamage(byte sword)
    {
        if (sword == DiamondSword) return 10;
        if (sword == IronSword || sword == GoldSword) return 7;
        if (sword == StoneSword) return 5;
        return 4;
    }

    // --- The sword in your hand ---

    void BuildSword()
    {
        swordInHand = new GameObject("Sword");
        swordInHand.transform.SetParent(hand.transform, false);
        swordInHand.transform.localPosition = new Vector3(0.45f, -0.36f, 0.72f);

        Material brown = BlockWorld.MakeMaterial(new Color(0.40f, 0.26f, 0.12f));
        Material dark = BlockWorld.MakeMaterial(new Color(0.20f, 0.20f, 0.22f));

        SwordPart(Vector3.zero, new Vector3(0.05f, 0.18f, 0.05f), brown);                   // handle
        SwordPart(new Vector3(0f, 0.10f, 0f), new Vector3(0.20f, 0.04f, 0.06f), dark);     // guard
        swordBlade = SwordPart(new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.48f, 0.025f), null)
                     .GetComponent<MeshRenderer>();                                       // blade
    }

    GameObject SwordPart(Vector3 position, Vector3 size, Material material)
    {
        GameObject part = AddHandPart(position, size, material);
        part.transform.SetParent(swordInHand.transform, false);
        return part;
    }

    void MoveSword()
    {
        bool holding = IsSword(selected);
        swordInHand.SetActive(holding);
        if (!holding) return;

        swordBlade.sharedMaterial = BlockWorld.Instance.MaterialFor(BladeBlock(selected));

        // Tipped forward a little, and a quick chop forward and back when swinging
        swingTime = Mathf.Max(swingTime - Time.deltaTime, 0f);
        float chop = Mathf.Sin(swingTime / SwingSeconds * Mathf.PI) * 70f;
        swordInHand.transform.localRotation = Quaternion.Euler(25f + chop, 0f, 15f);
    }

    // --- Its picture in the inventory ---

    static Texture2D SwordPicture(byte sword)
    {
        Texture2D picture;
        if (swordPictures.TryGetValue(sword, out picture)) return picture;

        Color blade;
        switch (sword)
        {
            case StoneSword:   blade = new Color(0.60f, 0.60f, 0.62f); break;
            case IronSword:    blade = new Color(0.88f, 0.88f, 0.90f); break;
            case GoldSword:    blade = new Color(1.00f, 0.85f, 0.20f); break;
            case DiamondSword: blade = new Color(0.45f, 0.95f, 0.95f); break;
            default:           blade = new Color(0.75f, 0.58f, 0.34f); break;
        }

        // x is the blade, k the guard, b the handle
        picture = PixelArt(new[]
        {
            "......xx",
            ".....xxx",
            "....xxx.",
            ".k.xxx..",
            "..kxx...",
            "..bk....",
            ".b..k...",
            "b.......",
        }, blade);

        swordPictures[sword] = picture;
        return picture;
    }
}
