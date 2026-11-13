using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// A firework rocket. It whooshes up into the sky leaving sparks behind,
// then bursts into a ball of coloured sparks. Pretty, and harmless.
public class Firework : MonoBehaviour
{
    const float FlightSeconds = 1.6f;
    const float RocketSpeed = 14f;
    const int SparkCount = 40;
    const float SparkSpeed = 7f;
    const float SparkSeconds = 1.4f;

    // A few colours, shared by every firework
    static Material[] colours;
    static Material rocketLook;

    private Mesh cube;

    public static void Launch(Vector3 from)
    {
        if (colours == null)
        {
            colours = new[]
            {
                Glowing(new Color(1.0f, 0.2f, 0.2f)), Glowing(new Color(1.0f, 0.8f, 0.2f)),
                Glowing(new Color(0.3f, 1.0f, 0.3f)), Glowing(new Color(0.3f, 0.6f, 1.0f)),
                Glowing(new Color(0.9f, 0.4f, 1.0f)), Glowing(Color.white)
            };
            rocketLook = BlockWorld.MakeMaterial(new Color(0.8f, 0.15f, 0.1f));
        }

        GameObject obj = new GameObject("Firework");
        obj.transform.position = from;
        obj.AddComponent<Firework>().cube = BlockWorld.Instance.CubeMesh;
    }

    // Shines by itself, so sparks are bright even against a dark sky
    static Material Glowing(Color colour)
    {
        Material mat = BlockWorld.MakeMaterial(colour);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour);
        return mat;
    }

    void Start()
    {
        StartCoroutine(FlyAndBurst());
    }

    IEnumerator FlyAndBurst()
    {
        GameObject rocket = Piece(transform.position, new Vector3(0.2f, 0.5f, 0.2f), rocketLook);

        // Up it goes, wobbling a little, dropping sparks as it goes
        Vector3 wobble = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        for (float t = 0f; t < FlightSeconds; t += Time.deltaTime)
        {
            rocket.transform.position += (Vector3.up * RocketSpeed + wobble) * Time.deltaTime;
            if (Random.value < 0.5f)
                StartCoroutine(Spark(rocket.transform.position, Random.insideUnitSphere * 0.5f, colours[1], 0.4f));
            yield return null;
        }

        Vector3 burst = rocket.transform.position;
        Destroy(rocket);

        // A flash of light...
        GameObject flashObj = new GameObject("Flash");
        flashObj.transform.SetParent(transform, false);
        flashObj.transform.position = burst;
        Light flash = flashObj.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 30f;

        // ...and sparks flying out every way, in one or two colours
        Material first = colours[Random.Range(0, colours.Length)];
        Material second = colours[Random.Range(0, colours.Length)];
        flash.color = first.color;
        for (int i = 0; i < SparkCount; i++)
            StartCoroutine(Spark(burst, Random.onUnitSphere * SparkSpeed, i % 2 == 0 ? first : second, SparkSeconds));

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            flash.intensity = 4f * (1f - t);
            yield return null;
        }
        flash.enabled = false;

        yield return new WaitForSeconds(SparkSeconds);
        Destroy(gameObject);
    }

    // One little spark that flies off, droops, and shrinks away
    IEnumerator Spark(Vector3 position, Vector3 velocity, Material look, float seconds)
    {
        GameObject spark = Piece(position, Vector3.one * 0.15f, look);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            velocity += Vector3.down * 4f * Time.deltaTime;   // a gentle droop
            velocity *= 1f - 1.5f * Time.deltaTime;           // and slowing down in the air
            spark.transform.position += velocity * Time.deltaTime;
            spark.transform.localScale = Vector3.one * 0.15f * (1f - t / seconds);
            yield return null;
        }
        Destroy(spark);
    }

    GameObject Piece(Vector3 position, Vector3 size, Material look)
    {
        GameObject piece = new GameObject("Spark");
        piece.transform.SetParent(transform, true);
        piece.transform.position = position;
        piece.transform.localScale = size;
        piece.AddComponent<MeshFilter>().sharedMesh = cube;

        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = look;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return piece;
    }
}
