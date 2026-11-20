using System.Collections.Generic;
using UnityEngine;

// Things to ride in: boats and minecarts.
//
//   Boat:      put it down on water. Press E to climb in, W to row the way
//              you're looking, S to go backwards, and E again to get out.
//   Minecart:  put it down on rails. Climb in with E, press W to push off,
//              and it rolls along the rails - round corners, up and down
//              hills - until it runs out of track or slows to a stop.
// Left click an empty boat or minecart to pick it back up.
// They stay where you left them, even after you stop playing.
public abstract class Vehicle : MonoBehaviour
{
    public static readonly List<Vehicle> All = new List<Vehicle>();

    protected BlockWorld world;
    public bool Ridden { get; set; }

    public abstract byte Item { get; }                     // what you get back when you pick it up
    public abstract void Drive(float throttle, Transform rider);
    public virtual Vector3 Seat { get { return transform.position + Vector3.down * 0.55f; } }

    protected abstract void Build();
    protected abstract string SaveAs();

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    protected static T Make<T>(BlockWorld world, Vector3 at) where T : Vehicle
    {
        T made = new GameObject(typeof(T).Name).AddComponent<T>();
        made.world = world;
        made.transform.position = at;
        made.Build();
        return made;
    }

    protected Transform Part(Vector3 at, Vector3 size, Material look)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(transform, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = world.CubeMesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = look;
        return part.transform;
    }

    // --- Remembering where they are ---

    static string Key(int slot, int place)
    {
        return "world" + (slot + 1) + "-vehicles-" + WorldSaves.PlaceName(place);
    }

    public static void SaveAll(int slot, int place)
    {
        List<string> parts = new List<string>();
        foreach (Vehicle vehicle in All) parts.Add(vehicle.SaveAs());
        PlayerPrefs.SetString(Key(slot, place), string.Join(";", parts));
    }

    public static void LoadAll(BlockWorld world)
    {
        string saved = PlayerPrefs.GetString(Key(WorldMenu.CurrentSlot, WorldMenu.CurrentPlace), "");
        foreach (string one in saved.Split(';'))
        {
            string[] bits = one.Split(',');
            if (bits.Length < 4) continue;
            float x, y, z;
            if (!float.TryParse(bits[1], out x) || !float.TryParse(bits[2], out y) || !float.TryParse(bits[3], out z)) continue;
            Vector3 at = new Vector3(x, y, z);
            if (bits[0] == "boat") BoatRide.Launch(world, at);
            else if (bits[0] == "cart") Cart.PutOn(world, Vector3Int.RoundToInt(at + Vector3.up * 0.35f));
        }
    }

    protected static string Num(float f) { return f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
}

public class BoatRide : Vehicle
{
    const float Speed = 6f;
    private static Material wood, dark;
    private float speed;

    public override byte Item { get { return BlockWorld.Boat; } }
    public override Vector3 Seat { get { return transform.position + Vector3.down * 0.65f; } }

    // Sits on the top of the water in this column
    public static BoatRide Launch(BlockWorld world, Vector3 near)
    {
        Vector3Int cell = Vector3Int.RoundToInt(near);
        for (int y = Mathf.Min(cell.y + 1, BlockWorld.SizeY - 1); y > 0; y--)
            if (world.GetBlock(cell.x, y, cell.z) == BlockWorld.Water && world.GetBlock(cell.x, y + 1, cell.z) != BlockWorld.Water)
                return Make<BoatRide>(world, new Vector3(cell.x, y + 0.35f, cell.z));
        return null;
    }

    protected override void Build()
    {
        if (wood == null)
        {
            wood = world.MaterialFor(BlockWorld.Planks);
            dark = BlockWorld.MakeMaterial(new Color(0.35f, 0.22f, 0.12f));
        }
        Part(new Vector3(0f, -0.05f, 0f), new Vector3(0.9f, 0.1f, 1.5f), wood);   // bottom
        Part(new Vector3(-0.45f, 0.1f, 0f), new Vector3(0.08f, 0.3f, 1.5f), wood);
        Part(new Vector3(0.45f, 0.1f, 0f), new Vector3(0.08f, 0.3f, 1.5f), wood);
        Part(new Vector3(0f, 0.1f, 0.75f), new Vector3(0.9f, 0.3f, 0.08f), wood);
        Part(new Vector3(0f, 0.1f, -0.75f), new Vector3(0.9f, 0.3f, 0.08f), wood);
        Part(new Vector3(0f, 0.1f, 0.1f), new Vector3(0.8f, 0.06f, 0.2f), dark);   // seat
    }

    public override void Drive(float throttle, Transform rider)
    {
        Vector3 flat = rider.forward;
        flat.y = 0f;
        speed = Mathf.MoveTowards(speed, throttle * Speed, 6f * Time.deltaTime);
        if (Mathf.Abs(speed) < 0.01f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 5f * Time.deltaTime);
        Vector3 next = transform.position + flat.normalized * speed * Time.deltaTime;

        // Only on water - bump into the shore and stop
        Vector3Int ahead = Vector3Int.RoundToInt(next + flat.normalized * Mathf.Sign(speed) * 0.7f);
        int waterY = Mathf.RoundToInt(transform.position.y - 0.35f);
        if (world.GetBlock(ahead.x, waterY, ahead.z) != BlockWorld.Water || world.GetBlock(ahead.x, waterY + 1, ahead.z) == BlockWorld.Water)
        {
            speed = 0f;
            return;
        }
        transform.position = next;
    }

    void Update()
    {
        // Bobbing gently on the waves
        if (!Ridden) transform.rotation *= Quaternion.Euler(0f, Mathf.Sin(Time.time) * 3f * Time.deltaTime, 0f);
    }

    protected override string SaveAs()
    {
        return "boat," + Num(transform.position.x) + "," + Num(transform.position.y) + "," + Num(transform.position.z);
    }
}

public class Cart : Vehicle
{
    const float MostSpeed = 10f;

    private static Material iron, wheels;
    private Vector3Int cell;              // the rail it's coming from
    private Vector3Int towards;           // the rail it's going to
    private Vector3Int heading = new Vector3Int(0, 0, 1);
    private float along;                  // 0 to 1, from cell to towards
    private float speed;

    public override byte Item { get { return BlockWorld.Minecart; } }
    public override Vector3 Seat { get { return transform.position + Vector3.down * 0.45f; } }

    public static Cart PutOn(BlockWorld world, Vector3Int rail)
    {
        if (!BlockWorld.IsRail(world.GetBlock(rail.x, rail.y, rail.z))) return null;
        Cart cart = Make<Cart>(world, RestingOn(rail));
        cart.cell = cart.towards = rail;
        cart.heading = world.GetBlock(rail.x, rail.y, rail.z) == BlockWorld.RailEW ? Vector3Int.right : new Vector3Int(0, 0, 1);
        return cart;
    }

    static Vector3 RestingOn(Vector3Int rail) { return new Vector3(rail.x, rail.y - 0.2f, rail.z); }

    protected override void Build()
    {
        if (iron == null)
        {
            iron = BlockWorld.MakeMaterial(new Color(0.55f, 0.56f, 0.6f));
            wheels = BlockWorld.MakeMaterial(new Color(0.15f, 0.15f, 0.17f));
        }
        Part(new Vector3(0f, -0.15f, 0f), new Vector3(0.8f, 0.08f, 0.9f), iron);
        Part(new Vector3(-0.38f, 0.05f, 0f), new Vector3(0.06f, 0.36f, 0.9f), iron);
        Part(new Vector3(0.38f, 0.05f, 0f), new Vector3(0.06f, 0.36f, 0.9f), iron);
        Part(new Vector3(0f, 0.05f, 0.42f), new Vector3(0.8f, 0.36f, 0.06f), iron);
        Part(new Vector3(0f, 0.05f, -0.42f), new Vector3(0.8f, 0.36f, 0.06f), iron);
        for (int i = 0; i < 4; i++)
            Part(new Vector3(i % 2 == 0 ? -0.3f : 0.3f, -0.24f, i < 2 ? 0.3f : -0.3f), new Vector3(0.12f, 0.12f, 0.12f), wheels);
    }

    bool RailAt(Vector3Int at) { return BlockWorld.IsRail(world.GetBlock(at.x, at.y, at.z)); }

    // Where to go next from this rail, going this way: straight on, up or
    // down a hill, or round a corner. False if the track ends.
    bool NextRail(Vector3Int from, ref Vector3Int way, out Vector3Int next)
    {
        foreach (Vector3Int option in new[] { from + way, from + way + Vector3Int.up, from + way + Vector3Int.down })
            if (RailAt(option)) { next = option; return true; }

        // A corner: try left and right
        Vector3Int left = new Vector3Int(-way.z, 0, way.x);
        foreach (Vector3Int turn in new[] { left, -left })
            if (RailAt(from + turn))
            {
                way = turn;
                next = from + turn;
                return true;
            }
        next = from;
        return false;
    }

    public override void Drive(float throttle, Transform rider)
    {
        // Push the way you're facing (and turn round if you face backwards while stopped)
        if (throttle > 0f)
        {
            Vector3 facing = rider.forward;
            if (Mathf.Abs(speed) < 0.5f && Vector3.Dot(facing, heading) < -0.3f && cell == towards) heading = -heading;
            speed = Mathf.Min(MostSpeed, speed + 7f * Time.deltaTime);
        }
        else if (throttle < 0f) speed = Mathf.Max(0f, speed - 8f * Time.deltaTime);   // brakes
    }

    void Update()
    {
        if (BlockInteractor.InventoryOpen || Cursor.lockState != CursorLockMode.Locked) return;

        // Not on a rail any more (it was broken)? It just sits there
        if (!RailAt(cell) && !RailAt(towards)) { speed = 0f; return; }

        speed *= 1f - 0.15f * Time.deltaTime;   // rolling slows it down, slowly
        if (speed < 0.05f && cell == towards) { speed = 0f; return; }

        if (cell == towards)
        {
            Vector3Int next;
            if (!NextRail(cell, ref heading, out next)) { speed = 0f; return; }   // end of the line
            towards = next;
            along = 0f;
        }

        // Downhill speeds up, uphill slows down
        if (towards.y < cell.y) speed = Mathf.Min(MostSpeed, speed + 6f * Time.deltaTime);
        if (towards.y > cell.y) speed -= 5f * Time.deltaTime;
        if (speed <= 0f)
        {
            speed = 0f;
            heading = -heading;   // rolls back down next push
            towards = cell;
            along = 0f;
        }

        along += speed * Time.deltaTime;
        if (along >= 1f)
        {
            cell = towards;
            along = 0f;
        }

        transform.position = Vector3.Lerp(RestingOn(cell), RestingOn(towards), along);
        Vector3 travel = towards - cell;
        travel.y = 0f;
        if (travel.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(travel), 10f * Time.deltaTime);
    }

    protected override string SaveAs()
    {
        return "cart," + cell.x + "," + Num(cell.y - 0.2f) + "," + cell.z;
    }
}
