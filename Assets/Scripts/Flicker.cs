using UnityEngine;

// Makes the flames of a fire block flicker: each flame stretches and
// shrinks, and sways a little, all out of step with each other.
public class Flicker : MonoBehaviour
{
    private Transform[] flames;
    private Vector3[] sizes;
    private float[] bottoms;
    private float seed;

    void Start()
    {
        flames = new Transform[transform.childCount];
        sizes = new Vector3[flames.Length];
        bottoms = new float[flames.Length];
        for (int i = 0; i < flames.Length; i++)
        {
            flames[i] = transform.GetChild(i);
            sizes[i] = flames[i].localScale;
            bottoms[i] = flames[i].localPosition.y - sizes[i].y / 2f;   // the flame stays standing on the ground
        }
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time * 6f + seed;
        for (int i = 0; i < flames.Length; i++)
        {
            float stretch = 0.7f + Mathf.PerlinNoise(t, i * 3.7f) * 0.6f;
            Vector3 size = new Vector3(sizes[i].x, sizes[i].y * stretch, sizes[i].z);
            flames[i].localScale = size;

            Vector3 at = flames[i].localPosition;
            at.y = bottoms[i] + size.y / 2f;
            flames[i].localPosition = at;
            flames[i].localRotation = Quaternion.Euler(Mathf.Sin(t + i) * 6f, 0f, Mathf.Cos(t * 0.8f + i) * 6f);
        }
    }
}
