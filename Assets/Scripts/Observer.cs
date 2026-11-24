using System.Collections.Generic;
using UnityEngine;

// The observer: a machine block with a face on one side. It watches the
// block in front of its face, and the moment that block changes - something
// is placed or broken there, a door opens, a piston pushes, wheat grows, sand
// falls - it sends a quick pulse of power out of its back (the side with the
// red dot). Put cable behind it, and that pulse can set off anything a button can.
//
// When you place one, its face looks the same way you're looking - so it
// watches the block beyond it, and its back faces you.
//
// Two observers watching each other make a clock that ticks all by itself!
public partial class BlockWorld
{
    public const byte Observer = 250;

    // Up, down and the four sides - the facing is kept as one of these
    static readonly Vector3Int[] ObserverWays =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    // Observers that have just seen something change, waiting to send their pulse
    private readonly HashSet<Vector3Int> observerAlerts = new HashSet<Vector3Int>();

    private Material observerFace, observerEye, observerDot;

    // Which way it's looking (kept with the chests, so it's saved with the world)
    public Vector3Int ObserverFacing(Vector3Int cell)
    {
        ChestContents inside;
        if (!chests.TryGetValue(cell, out inside)) return new Vector3Int(0, 0, 1);
        return ObserverWays[Mathf.Clamp(inside.items[0], 0, ObserverWays.Length - 1)];
    }

    public void SetObserverFacing(Vector3Int cell, Vector3Int facing)
    {
        ChestContents inside = ChestAt(cell);
        inside.items[0] = (byte)Mathf.Max(0, System.Array.IndexOf(ObserverWays, facing));
        inside.counts[0] = 1;
    }

    // A block changed - is any observer watching it?
    void TellObservers(Vector3Int changed)
    {
        foreach (Vector3Int way in ObserverWays)
        {
            Vector3Int next = changed + way;
            if (GetBlock(next.x, next.y, next.z) != Observer) continue;
            if (next + ObserverFacing(next) == changed) observerAlerts.Add(next);
        }
    }

    // The observers that need to send a pulse now (and forgets them)
    public List<Vector3Int> TakeObserverAlerts()
    {
        List<Vector3Int> alerts = new List<Vector3Int>(observerAlerts);
        observerAlerts.Clear();
        return alerts;
    }

    void CreateObserverMaterials()
    {
        // Rough dark stone, a bit like cobblestone
        materials[Observer] = MakePixelMaterial((x, y) =>
        {
            float n = Jumble(x, y, 31);
            return n > 0.7f ? new Color(0.42f, 0.42f, 0.44f) : n < 0.2f ? new Color(0.22f, 0.22f, 0.24f) : new Color(0.32f, 0.32f, 0.34f);
        }, true);
        observerFace = MakeMaterial(new Color(0.12f, 0.12f, 0.14f));
        observerEye = MakeGlowingMaterial((x, y) => new Color(0.55f, 0.9f, 1f));
        observerDot = MakeGlowingMaterial((x, y) => new Color(0.95f, 0.15f, 0.1f));
    }

    // A stone cube with a dark face and two glowing eyes on the front, and a red dot on the back
    void BuildObserver(GameObject cube, Vector3Int cell)
    {
        ModelPiece(cube, Vector3.zero, Vector3.one, materials[Observer], true);

        GameObject turned = new GameObject("Observer Face");
        turned.transform.SetParent(cube.transform, false);
        Vector3 facing = ObserverFacing(cell);
        turned.transform.localRotation = Quaternion.LookRotation(facing, facing.y != 0f ? Vector3.forward : Vector3.up);

        ModelPiece(turned, new Vector3(0f, 0f, 0.505f), new Vector3(0.8f, 0.8f, 0.01f), observerFace, false);
        ModelPiece(turned, new Vector3(-0.18f, 0.08f, 0.512f), new Vector3(0.22f, 0.09f, 0.01f), observerEye, false);
        ModelPiece(turned, new Vector3(0.18f, 0.08f, 0.512f), new Vector3(0.22f, 0.09f, 0.01f), observerEye, false);
        ModelPiece(turned, new Vector3(0f, -0.2f, 0.512f), new Vector3(0.36f, 0.06f, 0.01f), observerEye, false);   // a mouth-ish slit
        ModelPiece(turned, new Vector3(0f, 0f, -0.505f), new Vector3(0.22f, 0.22f, 0.01f), observerDot, false);    // the back
    }
}

public partial class BlockInteractor
{
    const float ObserverRestSeconds = 0.25f;   // so two watching each other tick, rather than going crazy

    private readonly Dictionary<Vector3Int, float> observerRestUntil = new Dictionary<Vector3Int, float>();

    // The way you're looking, straightened out to up, down or one of the four sides
    Vector3Int ObserverFacing()
    {
        Vector3 f = cam.transform.forward;
        float ax = Mathf.Abs(f.x), ay = Mathf.Abs(f.y), az = Mathf.Abs(f.z);
        if (ay > ax && ay > az) return f.y > 0f ? Vector3Int.up : Vector3Int.down;
        if (az >= ax) return f.z > 0f ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);
        return f.x > 0f ? Vector3Int.right : Vector3Int.left;
    }

    // Observers that saw something change send a pulse out of their back
    void RunObservers(BlockWorld world)
    {
        foreach (Vector3Int observer in world.TakeObserverAlerts())
        {
            if (world.GetBlock(observer.x, observer.y, observer.z) != BlockWorld.Observer) continue;
            float rest;
            if (observerRestUntil.TryGetValue(observer, out rest) && Time.time < rest) continue;
            observerRestUntil[observer] = Time.time + ObserverRestSeconds;

            Vector3Int back = -world.ObserverFacing(observer);
            Villager.SparkleBurst(observer + (Vector3)back * 0.55f, 2);
            StartCoroutine(SendPower(observer, back));
        }
    }
}
