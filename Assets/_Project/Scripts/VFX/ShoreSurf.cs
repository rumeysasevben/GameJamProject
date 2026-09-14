using UnityEngine;

/// <summary>
/// Waves breaking against the quay.
///
/// Along the shoreline sit a handful of surf points. At each one a foam crest
/// rolls in from the open water, breaks against the rock or the pier legs and
/// throws up a little spray, then the water draws back as a faint wash — and
/// after a pause of its own the next wave comes in.
///
/// The foam is drawn just beneath the land, so the terrain drawing itself cuts
/// the crest off where the water meets the shore; only the spray rises above
/// it. Every sprite is generated here, so there is no art to wire up, and every
/// renderer is made once in Awake and reused.
/// </summary>
public class ShoreSurf : MonoBehaviour
{
    /// <summary>One place where waves break.</summary>
    [System.Serializable]
    public struct SurfPoint
    {
        [Tooltip("Where the wave breaks, in world units — right on the shoreline.")]
        public Vector2 position;

        [Tooltip("Direction from the shore out to the open sea. Waves arrive from this side.")]
        public Vector2 seaward;

        [Tooltip("How wide the crest is, in world units.")]
        public float width;

        public SurfPoint(float x, float y, float seaX, float seaY, float width)
        {
            position = new Vector2(x, y);
            seaward = new Vector2(seaX, seaY);
            this.width = width;
        }
    }

    [Tooltip("Where the waves break. Placed along the terrain drawing's shore: the boulder, under the pier, the pier legs and around the lighthouse rock.")]
    [SerializeField] private SurfPoint[] points =
    {
        new SurfPoint(-8.85f, -0.40f, 0.5f, 0.85f, 0.9f),
        new SurfPoint(-8.25f, -0.95f, 1f, 0.2f, 0.7f),
        new SurfPoint(-7.02f, -1.62f, 0.5f, 0.85f, 0.6f),
        new SurfPoint(-6.70f, -2.35f, 0.6f, 0.8f, 0.7f),
        new SurfPoint(-6.58f, -1.42f, 0.5f, 0.85f, 0.6f),
        new SurfPoint(-6.05f, -1.30f, 0.5f, 0.85f, 0.6f),
        new SurfPoint(-6.15f, -2.05f, 0.3f, 0.95f, 0.8f),
        new SurfPoint(-5.60f, -1.80f, 0f, 1f, 1f),
        new SurfPoint(-4.60f, -1.72f, 0.2f, 1f, 0.9f),
        new SurfPoint(-4.00f, -2.10f, 0.7f, 0.7f, 1f),
        new SurfPoint(-3.65f, -3.20f, 1f, 0f, 1f),
        new SurfPoint(-3.60f, -4.25f, 1f, 0.1f, 1f),
        new SurfPoint(-3.00f, -4.90f, 0.7f, 0.7f, 1f)
    };

    [Tooltip("Unlit sprite material. Under the night light a lit crest is the same colour as the water.")]
    [SerializeField] private Material material;

    [Tooltip("Foam colour.")]
    [SerializeField] private Color foamColor = new Color(0.78f, 0.88f, 0.98f, 0.55f);

    [Tooltip("Spray colour. A touch brighter than the foam: water catching the lamps.")]
    [SerializeField] private Color sprayColor = new Color(0.88f, 0.94f, 1f, 0.8f);

    [Tooltip("Shortest and longest pause between two waves at the same point, in seconds.")]
    [SerializeField] private Vector2 intervalRange = new Vector2(1.2f, 3.2f);

    [Tooltip("How far out a crest starts before rolling in, in world units.")]
    [SerializeField] private float reach = 0.8f;

    [Tooltip("How long a crest takes to roll in.")]
    [SerializeField] private float rollTime = 1.3f;

    [Tooltip("How long the break and the draw-back take together.")]
    [SerializeField] private float washTime = 1.1f;

    [Tooltip("Spray droplets per wave.")]
    [SerializeField] private int dropletCount = 6;

    [Tooltip("How hard the spray is thrown.")]
    [SerializeField] private float sprayPower = 1f;

    [Tooltip("Sorting order for the foam. Under the land (-90), so the shore cuts the crest off; over the wave marks (-95).")]
    [SerializeField] private int foamOrder = -91;

    [Tooltip("Sorting order for the spray, which rises over the rocks.")]
    [SerializeField] private int sprayOrder = -89;

    [Tooltip("Scales the whole wave — how far it rolls, how tall the crest, how high the spray. 1 for the quay; far less for a rock at sea.")]
    [SerializeField] private float size = 1f;

    private const float SprayLife = 0.6f;
    private const float Gravity = 5f;

    private SpriteRenderer[] crests;
    private SpriteRenderer[] washes;
    private SpriteRenderer[][] droplets;
    private Vector2[][] dropletVelocity;

    // Each point's clock runs from the moment its current wave started rolling.
    private float[] clocks;
    private float[] pauses;
    private bool[] broken;
    private float[] sprayClocks;

    private static Sprite crestSprite;
    private static Sprite dropletSprite;

    // The quay's material, borrowed by surf that is added in code and has
    // none of its own — the rocks spawned each night.
    private static Material sharedMaterial;

    /// <summary>
    /// Sets the surf up from code. Call straight after adding the component,
    /// before its first frame. Point positions are relative to this object.
    /// </summary>
    public void Configure(SurfPoint[] surfPoints, float waveSize, Vector2 pauseRange, int drops, int foamSortingOrder, int spraySortingOrder, Color foam)
    {
        points = surfPoints;
        size = waveSize;
        intervalRange = pauseRange;
        dropletCount = drops;
        foamOrder = foamSortingOrder;
        sprayOrder = spraySortingOrder;
        foamColor = foam;
    }

    private void Awake()
    {
        if (material != null)
        {
            sharedMaterial = material;
        }
    }

    private void Start()
    {
        if (material == null)
        {
            material = sharedMaterial;
        }

        if (crestSprite == null)
        {
            crestSprite = MakeCrestSprite();
            dropletSprite = MakeDropletSprite();
        }

        int count = points.Length;
        crests = new SpriteRenderer[count];
        washes = new SpriteRenderer[count];
        droplets = new SpriteRenderer[count][];
        dropletVelocity = new Vector2[count][];
        clocks = new float[count];
        pauses = new float[count];
        broken = new bool[count];
        sprayClocks = new float[count];

        for (int i = 0; i < count; i++)
        {
            crests[i] = CreateRenderer($"Crest_{i}", crestSprite, foamOrder);
            washes[i] = CreateRenderer($"Wash_{i}", crestSprite, foamOrder);

            droplets[i] = new SpriteRenderer[Mathf.Max(0, dropletCount)];
            dropletVelocity[i] = new Vector2[droplets[i].Length];
            for (int d = 0; d < droplets[i].Length; d++)
            {
                droplets[i][d] = CreateRenderer($"Spray_{i}_{d}", dropletSprite, sprayOrder);
            }

            // Staggered so the shore never breaks all at once.
            pauses[i] = Random.Range(intervalRange.x, intervalRange.y);
            clocks[i] = -Random.Range(0f, rollTime + washTime + intervalRange.y);
            sprayClocks[i] = SprayLife;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < points.Length; i++)
        {
            clocks[i] += dt;
            float t = clocks[i];

            if (t >= rollTime + washTime + pauses[i])
            {
                t = clocks[i] = 0f;
                broken[i] = false;
                pauses[i] = Random.Range(intervalRange.x, intervalRange.y);
            }

            DrawWave(i, t);
            DrawSpray(i, dt);
        }
    }

    private void DrawWave(int i, float t)
    {
        SurfPoint point = points[i];
        Vector2 origin = (Vector2)transform.position + point.position;
        Vector2 seaward = point.seaward.sqrMagnitude > 0.0001f ? point.seaward.normalized : Vector2.up;

        // The crest's bulge faces the shore it is running at.
        float angle = Mathf.Atan2(-seaward.y, -seaward.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        float width = Mathf.Max(0.1f, point.width) / 1.28f;

        SpriteRenderer crest = crests[i];
        SpriteRenderer wash = washes[i];

        if (t < 0f)
        {
            crest.enabled = false;
            wash.enabled = false;
            return;
        }

        if (t < rollTime)
        {
            // Rolling in: gathering speed, height and brightness as it nears
            // the shore.
            float k = t / rollTime;
            float eased = k * k;

            crest.enabled = true;
            crest.transform.SetPositionAndRotation(origin + seaward * (reach * size * (1f - eased)), rotation);
            crest.transform.localScale = new Vector3(width * Mathf.Lerp(0.55f, 1f, k), Mathf.Lerp(0.5f, 1f, eased) * size, 1f);
            crest.color = WithAlpha(foamColor, foamColor.a * Mathf.SmoothStep(0f, 1f, k * 1.6f));

            wash.enabled = false;
            return;
        }

        if (!broken[i])
        {
            broken[i] = true;
            ThrowSpray(i, seaward);
        }

        float w = Mathf.Clamp01((t - rollTime) / washTime);

        // Breaking: the crest spreads flat along the shore and fades out.
        float breakK = Mathf.Clamp01(w * 2.5f);
        crest.enabled = breakK < 1f;
        crest.transform.SetPositionAndRotation(origin - seaward * (0.08f * size * breakK), rotation);
        crest.transform.localScale = new Vector3(width * Mathf.Lerp(1f, 1.3f, breakK), Mathf.Lerp(1f, 0.35f, breakK) * size, 1f);
        crest.color = WithAlpha(foamColor, foamColor.a * (1f - breakK));

        // Drawing back: a thin, faint sheet of foam sliding out to sea.
        wash.enabled = w < 1f;
        wash.transform.SetPositionAndRotation(origin + seaward * (0.3f * size * Mathf.Sqrt(w)), rotation);
        wash.transform.localScale = new Vector3(width * 1.35f, Mathf.Lerp(0.45f, 0.2f, w) * size, 1f);
        wash.color = WithAlpha(foamColor, foamColor.a * 0.6f * Mathf.Sin(w * Mathf.PI));
    }

    private void ThrowSpray(int i, Vector2 seaward)
    {
        SurfPoint point = points[i];
        Vector2 along = new Vector2(-seaward.y, seaward.x);

        for (int d = 0; d < droplets[i].Length; d++)
        {
            // Water hitting a wall goes up and back out: a fan off the shore,
            // spread along the crest.
            float side = Random.Range(-0.5f, 0.5f) * point.width;
            Vector2 velocity = seaward * Random.Range(0.3f, 0.9f)
                             + Vector2.up * Random.Range(1.2f, 2.2f)
                             + along * (side * 0.8f);

            dropletVelocity[i][d] = velocity * (sprayPower * size);

            SpriteRenderer drop = droplets[i][d];
            drop.enabled = true;
            drop.transform.position = (Vector2)transform.position + point.position + along * side;
        }

        sprayClocks[i] = 0f;
    }

    private void DrawSpray(int i, float dt)
    {
        if (sprayClocks[i] >= SprayLife)
        {
            return;
        }

        sprayClocks[i] += dt;
        float k01 = Mathf.Clamp01(sprayClocks[i] / SprayLife);
        bool alive = k01 < 1f;

        for (int d = 0; d < droplets[i].Length; d++)
        {
            SpriteRenderer drop = droplets[i][d];
            drop.enabled = alive;
            if (!alive)
            {
                continue;
            }

            dropletVelocity[i][d] += Vector2.down * (Gravity * size * dt);
            drop.transform.position += (Vector3)(dropletVelocity[i][d] * dt);

            float scale = Mathf.Lerp(1.4f, 0.5f, k01) * Mathf.Sqrt(size);
            drop.transform.localScale = new Vector3(scale, scale, 1f);
            drop.color = WithAlpha(sprayColor, sprayColor.a * (1f - k01 * k01));
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int order)
    {
        var go = new GameObject(objectName, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        return renderer;
    }

    private static Color WithAlpha(Color c, float alpha)
    {
        c.a = alpha;
        return c;
    }

    /// <summary>
    /// A foam crest, 1.28 × 0.4 units: a bright curved lip bulging toward the
    /// top of the texture, with a softer trail of broken foam behind it.
    /// </summary>
    private static Sprite MakeCrestSprite()
    {
        const int w = 128;
        const int h = 40;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SurfCrest"
        };

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1) * 2f - 1f;
                float ends = Mathf.Pow(Mathf.Clamp01(1f - u * u), 0.8f);

                float lip = 0.45f + 0.3f * (1f - u * u);
                float distance = v - lip;

                float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance) / 0.09f);
                if (distance < 0f)
                {
                    // Broken foam trailing out to sea behind the lip.
                    float speckle = 0.55f + 0.45f * Hash(x, y);
                    alpha = Mathf.Max(alpha, Mathf.Exp(distance / 0.14f) * 0.45f * speckle);
                }

                byte a = (byte)(Mathf.Clamp01(alpha * ends) * 255f);
                pixels[y * w + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.6f), 100f);
    }

    /// <summary>A soft round droplet, 0.1 units across.</summary>
    private static Sprite MakeDropletSprite()
    {
        const int across = 10;
        var texture = new Texture2D(across, across, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SurfDroplet"
        };

        var pixels = new Color32[across * across];
        float centre = (across - 1) * 0.5f;
        for (int y = 0; y < across; y++)
        {
            for (int x = 0; x < across; x++)
            {
                float r = new Vector2(x - centre, y - centre).magnitude / (across * 0.5f);
                byte a = (byte)(Mathf.Clamp01(1f - r * r) * 255f);
                pixels[y * across + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, across, across), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float Hash(int x, int y)
    {
        uint n = (uint)(x * 374761393 + y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    private void OnDrawGizmos()
    {
        if (points == null)
        {
            return;
        }

        // Where each wave breaks, and the side it comes from.
        Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.8f);
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = transform.position + (Vector3)points[i].position;
            Vector3 sea = (Vector3)points[i].seaward.normalized * (reach * size);
            Gizmos.DrawWireSphere(p, 0.08f);
            Gizmos.DrawLine(p, p + sea);
        }
    }
}
