using UnityEngine;

// In survival, broken blocks (and other things that come loose) pop out as
// little spinning items. Walk close and they whizz over to you - pop! - and
// into your inventory. Anything left lying about vanishes after 5 minutes.
public class ItemDrop : MonoBehaviour
{
    const float PickUpRange = 1.3f;
    const float PullRange = 3f;
    const float Lifetime = 300f;
    const float Gravity = 16f;

    public byte Item { get; private set; }
    public int Count { get; private set; }

    private Vector3 velocity;
    private float born;
    private float spin;
    private Transform look;

    private static Material pictureShader;

    public static void Spawn(byte item, int count, Vector3 at)
    {
        if (item == BlockWorld.Air || count <= 0 || BlockWorld.Instance == null) return;

        ItemDrop drop = new GameObject("Dropped " + item).AddComponent<ItemDrop>();
        drop.Item = item;
        drop.Count = count;
        drop.transform.position = at;
        drop.velocity = new Vector3(Random.Range(-1.5f, 1.5f), 3f, Random.Range(-1.5f, 1.5f));
        drop.born = Time.time;
        drop.spin = Random.Range(0f, 360f);
        drop.BuildLook();
    }

    void BuildLook()
    {
        look = new GameObject("Look").transform;
        look.SetParent(transform, false);
        look.gameObject.AddComponent<MeshFilter>().sharedMesh = BlockWorld.Instance.CubeMesh;
        MeshRenderer renderer = look.gameObject.AddComponent<MeshRenderer>();

        // A little block - or, for things that aren't blocks, a flat picture
        Texture2D picture = BlockInteractor.PictureOf(Item);
        Material material = BlockWorld.Instance.MaterialFor(Item);
        if (picture == null && material != null && !BlockWorld.IsWheat(Item))
        {
            renderer.sharedMaterial = material;
            look.localScale = Vector3.one * 0.28f;
        }
        else
        {
            if (pictureShader == null)
            {
                Shader cutout = Shader.Find("Unlit/Transparent Cutout");
                pictureShader = cutout != null ? new Material(cutout) : BlockWorld.MakeMaterial(Color.white);
            }
            Material mat = new Material(pictureShader);
            mat.mainTexture = picture;
            renderer.sharedMaterial = mat;
            look.localScale = new Vector3(0.42f, 0.42f, 0.02f);
        }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void OnDestroy()
    {
        // The picture materials are made just for this drop
        if (look != null)
        {
            Material mat = look.GetComponent<MeshRenderer>().sharedMaterial;
            if (mat != null && (BlockWorld.Instance == null || mat != BlockWorld.Instance.MaterialFor(Item))) Destroy(mat);
        }
    }

    void Update()
    {
        BlockWorld world = BlockWorld.Instance;
        if (world == null) return;
        if (Time.time - born > Lifetime) { Destroy(gameObject); return; }

        // Bob and spin
        spin += 90f * Time.deltaTime;
        look.localRotation = Quaternion.Euler(0f, spin, 0f);
        look.localPosition = Vector3.up * (0.15f + Mathf.Sin((Time.time - born) * 3f) * 0.05f);

        // Close to the player? Whizz over and get picked up
        PlayerHealth player = PlayerHealth.Instance;
        if (player != null && !player.Dead && Time.time - born > 0.5f)
        {
            Vector3 target = player.transform.position + Vector3.up * 0.8f;
            float distance = Vector3.Distance(transform.position, target);
            if (distance < PickUpRange)
            {
                if (BlockInteractor.PickUp(Item, Count))
                {
                    Sounds.Pop(transform.position);
                    Destroy(gameObject);
                }
                return;
            }
            if (distance < PullRange)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, 6f * Time.deltaTime);
                velocity = Vector3.zero;
                return;
            }
        }

        // Fall until it lands on something solid
        velocity.y -= Gravity * Time.deltaTime;
        Vector3 next = transform.position + velocity * Time.deltaTime;
        Vector3Int below = Vector3Int.RoundToInt(next + Vector3.down * 0.1f);
        byte under = world.GetBlock(below.x, below.y, below.z);
        bool solid = under != BlockWorld.Air && under != BlockWorld.Water && under != BlockWorld.Fire &&
                     under != BlockWorld.Cable && !BlockWorld.IsWheat(under) && !BlockWorld.IsRail(under);

        if (solid && velocity.y < 0f)
        {
            next.y = below.y + 0.5f + 0.1f;
            velocity = Vector3.zero;
        }
        else if (under == BlockWorld.Lava)
        {
            Destroy(gameObject);   // burnt up!
            return;
        }

        // Don't slide into walls
        Vector3Int inside = Vector3Int.RoundToInt(next);
        byte here = world.GetBlock(inside.x, inside.y, inside.z);
        if (here != BlockWorld.Air && here != BlockWorld.Water && here != BlockWorld.Fire && !BlockWorld.IsWheat(here) &&
            here != BlockWorld.Cable && here != BlockWorld.PressurePlate)
        {
            next.x = transform.position.x;
            next.z = transform.position.z;
            velocity.x = velocity.z = 0f;
        }

        transform.position = next;
        if (transform.position.y < -10f) Destroy(gameObject);
    }
}
