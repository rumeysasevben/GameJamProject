using UnityEngine;

/// <summary>
/// Thin night cloud drifting over the far water, toward the top of the screen.
///
/// The wisps are faint and slow — the far distance, not weather — and wrap
/// round from one side to the other. Paired with a <see cref="ParallaxLayer"/>
/// on the same object they also lag the view's drift, which is what sets them
/// behind the rocks and ships. Their drawing is generated here.
/// </summary>
public class NightClouds : MonoBehaviour
{
    [Tooltip("How many wisps.")]
    [SerializeField] private int count = 7;

    [Tooltip("The band they drift in, bottom and top, in world units.")]
    [SerializeField] private Vector2 heightRange = new Vector2(2.2f, 5.2f);

    [Tooltip("How far past either screen edge a wisp travels before it wraps round.")]
    [SerializeField] private float halfWidth = 12f;

    [Tooltip("Slowest and fastest drift, in world units per second.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.03f, 0.07f);

    [Tooltip("Cloud colour. Mostly see-through: moonlit haze, not a ceiling.")]
    [SerializeField] private Color color = new Color(0.62f, 0.7f, 0.85f, 0.06f);

    [Tooltip("Unlit sprite material, so the haze is not swallowed by the night light.")]
    [SerializeField] private Material material;

    [Tooltip("Sorting order. Over the sea's wave marks, under the land, rocks and ships.")]
    [SerializeField] private int sortingOrder = -93;

    private Transform[] wisps;
    private float[] speeds;
    private static Sprite cloudSprite;

    private void Awake()
    {
        if (cloudSprite == null)
        {
            cloudSprite = MakeCloudSprite();
        }

        wisps = new Transform[Mathf.Max(0, count)];
        speeds = new float[wisps.Length];

        for (int i = 0; i < wisps.Length; i++)
        {
            var go = new GameObject($"Wisp_{i}", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);

            // Spread evenly across the width with a little jitter, so the sky
            // never starts with every wisp bunched in one place.
            float x = Mathf.Lerp(-halfWidth, halfWidth, (i + Random.Range(0.2f, 0.8f)) / wisps.Length);
            float y = Random.Range(heightRange.x, heightRange.y);
            go.transform.localPosition = new Vector3(x, y, 0f);

            float scale = Random.Range(1.6f, 3f);
            go.transform.localScale = new Vector3(scale, scale * Random.Range(0.45f, 0.7f), 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = cloudSprite;
            renderer.flipX = Random.value < 0.5f;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(color.r, color.g, color.b, color.a * Random.Range(0.6f, 1f));
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            wisps[i] = go.transform;
            speeds[i] = Random.Range(speedRange.x, speedRange.y);
        }
    }

    private void Update()
    {
        for (int i = 0; i < wisps.Length; i++)
        {
            Vector3 p = wisps[i].localPosition;
            p.x += speeds[i] * Time.deltaTime;

            if (p.x > halfWidth)
            {
                p.x -= halfWidth * 2f;
                p.y = Random.Range(heightRange.x, heightRange.y);
            }

            wisps[i].localPosition = p;
        }
    }

    /// <summary>A soft, lumpy streak of haze, 2.56 × 0.96 units before scaling.</summary>
    private static Sprite MakeCloudSprite()
    {
        const int w = 256;
        const int h = 96;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "NightCloud"
        };

        // A row of overlapping soft puffs along a flattened body, fixed so the
        // same cloud is drawn every run.
        var puffs = new[]
        {
            new Vector3(0.18f, 0.45f, 0.16f), new Vector3(0.32f, 0.55f, 0.22f), new Vector3(0.5f, 0.6f, 0.26f),
            new Vector3(0.66f, 0.52f, 0.2f), new Vector3(0.8f, 0.46f, 0.15f), new Vector3(0.42f, 0.4f, 0.2f),
            new Vector3(0.6f, 0.38f, 0.18f)
        };

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float density = 0f;

                foreach (Vector3 puff in puffs)
                {
                    // Puffs are round in pixels, so stretch v by the aspect.
                    float dx = u - puff.x;
                    float dy = (v - puff.y) * h / w;
                    density += Mathf.Exp(-(dx * dx + dy * dy) / (puff.z * puff.z * 0.12f));
                }

                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(density * 0.8f));
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
