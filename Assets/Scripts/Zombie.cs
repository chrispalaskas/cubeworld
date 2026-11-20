using UnityEngine;

// A zombie: shuffles about, arms out, and comes after you when you're close.
// Each bite knocks a heart and a half off. A punch does a little, a sword
// does a lot, and a diamond sword gets one in a single hit.
// (What all monsters share is in Monster.cs.)
public class Zombie : Monster
{
    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private static Material skin, shirt, trousers, black;

    protected override string Called { get { return "zombie"; } }

    protected override void BuildBody()
    {
        if (skin == null)
        {
            skin = BlockWorld.MakeMaterial(new Color(0.35f, 0.62f, 0.30f));
            shirt = BlockWorld.MakeMaterial(new Color(0.15f, 0.55f, 0.60f));
            trousers = BlockWorld.MakeMaterial(new Color(0.25f, 0.22f, 0.55f));
            black = BlockWorld.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));
        }

        Transform model = Model(1.45f);   // a bit bigger than a villager - nearly as tall as you

        AddPart(model, new Vector3(0f, 0.58f, 0f), new Vector3(0.30f, 0.44f, 0.18f), shirt);   // body

        leftLeg = Pivot(model, new Vector3(-0.08f, 0.36f, 0f));
        rightLeg = Pivot(model, new Vector3(0.08f, 0.36f, 0f));
        foreach (Transform leg in new[] { leftLeg, rightLeg })
            AddPart(leg, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.36f, 0.14f), trousers);

        // Arms stuck out in front, the zombie way
        leftArm = Pivot(model, new Vector3(-0.21f, 0.76f, 0f));
        rightArm = Pivot(model, new Vector3(0.21f, 0.76f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            AddPart(arm, new Vector3(0f, -0.10f, 0f), new Vector3(0.11f, 0.20f, 0.11f), shirt);
            AddPart(arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.09f, 0.18f, 0.09f), skin);
        }

        Transform head = Pivot(model, new Vector3(0f, 0.80f, 0f));
        AddPart(head, new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.30f, 0.30f), skin);
        AddPart(head, new Vector3(-0.07f, 0.17f, 0.151f), new Vector3(0.06f, 0.04f, 0.01f), black);   // eyes
        AddPart(head, new Vector3(0.07f, 0.17f, 0.151f), new Vector3(0.06f, 0.04f, 0.01f), black);
        AddPart(head, new Vector3(0f, 0.06f, 0.151f), new Vector3(0.14f, 0.04f, 0.01f), black);       // groaning mouth
    }

    protected override void Animate(bool walking)
    {
        walkCycle += walking ? Time.deltaTime * 7f : 0f;
        float swing = walking ? Mathf.Sin(walkCycle) * 30f : 0f;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(-85f + swing * 0.2f, 0f, 0f);
        rightArm.localRotation = Quaternion.Euler(-85f - swing * 0.2f, 0f, 0f);
    }
}
