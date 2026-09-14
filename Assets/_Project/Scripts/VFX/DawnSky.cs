using UnityEngine;

/// <summary>
/// The morning over the water: the sun coming up at the far edge of the sea,
/// its light laid down the water toward the harbour, and gulls.
///
/// An ordinary dawn gets a low sun and a couple of birds. The last one is the
/// ending, so everything is given room: the sun climbs higher and burns
/// warmer, and the gulls keep coming — one, then a few, then the sky full of
/// them, calling to each other — for as long as the player stays to watch.
///
/// Every drawing but the gulls is generated here; the gulls are two frames,
/// wings up and wings down, flipped for the ones flying the other way.
/// </summary>
public class DawnSky : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] private Sprite gullWingsUp;
    [SerializeField] private Sprite gullWingsDown;

    [Tooltip("Unlit sprite material, so the morning is not dimmed by the global light it is part of.")]
    [SerializeField] private Material material;

    [Header("Sun")]
    [Tooltip("Where the sun comes up, in world units: x along the far edge, and the heights it climbs from and to.")]
    [SerializeField] private Vector3 sunPath = new Vector3(3.5f, 7.6f, 5.3f);

    [SerializeField] private Color sunColor = new Color(1f, 0.78f, 0.5f, 1f);

    [Tooltip("How strong the sun is at the end of an ordinary dawn, and of the last one.")]
    [SerializeField] private Vector2 sunStrength = new Vector2(0.45f, 0.85f);

    [Tooltip("Sorting order for the sun and its light on the water. Over the sea and cloud, under the land, rocks and ships.")]
    [SerializeField] private int sunOrder = -92;

    [Header("Gulls")]
    [Tooltip("Gulls over an ordinary dawn.")]
    [SerializeField] private int ordinaryGulls = 2;

    [Tooltip("How many are in the air at once by the end of the last dawn.")]
    [SerializeField] private int finaleGulls = 16;

    [Tooltip("Slowest and fastest flight, in world units per second.")]
    [SerializeField] private Vector2 gullSpeed = new Vector2(1.2f, 2.1f);

    [Tooltip("Sorting order for the gulls. Over everything on the water.")]
    [SerializeField] private int gullOrder = 26;

    private const float EdgeX = 11f;

    private class Gull
    {
        public SpriteRenderer renderer;
        public bool flying;
        public float speed;
        public float direction;
        public float baseY;
        public float wave;
        public float flapRate;
        public float clock;
    }

    private SpriteRenderer sun;
    private SpriteRenderer sunPathOnWater;
    private Gull[] gulls;

    private bool playing;
    private bool finale;
    private float duration;
    private float elapsed;
    private float fade = 1f;
    private float fadeSpeed;
    private int launched;
    private float nextCry;

    private void Awake()
    {
        Sprite glow = MakeGlowSprite();

        sun = CreateRenderer("Sun", glow, sunOrder);
        sunPathOnWater = CreateRenderer("SunOnWater", glow, sunOrder);

        gulls = new Gull[Mathf.Max(ordinaryGulls, finaleGulls)];
        for (int i = 0; i < gulls.Length; i++)
        {
            gulls[i] = new Gull { renderer = CreateRenderer($"Gull_{i}", gullWingsUp, gullOrder) };
        }
    }

    /// <summary>
    /// Brings the morning in over <paramref name="seconds"/> — the same span
    /// as the sunrise it goes with. <paramref name="isFinale"/> is the last
    /// night's.
    /// </summary>
    public void Play(float seconds, bool isFinale)
    {
        playing = true;
        finale = isFinale;
        duration = Mathf.Max(0.1f, seconds);
        elapsed = 0f;
        fade = 1f;
        fadeSpeed = 0f;
        launched = 0;
        nextCry = isFinale ? 1.2f : 2.5f;

        foreach (Gull gull in gulls)
        {
            gull.flying = false;
            gull.renderer.enabled = false;
        }
    }

    /// <summary>Lets the morning go over <paramref name="seconds"/>, as evening comes back.</summary>
    public void Hide(float seconds)
    {
        if (!playing)
        {
            return;
        }

        fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        float dt = Time.deltaTime;
        elapsed += dt;

        if (fadeSpeed > 0f)
        {
            fade = Mathf.MoveTowards(fade, 0f, fadeSpeed * dt);
            if (fade <= 0f)
            {
                StopAll();
                return;
            }
        }

        float t = Mathf.Clamp01(elapsed / duration);
        DrawSun(t);
        LaunchGulls(t);
        FlyGulls(dt);
        Cry(dt);
    }

    private void DrawSun(float t)
    {
        // Slow to start, as a sunrise is: most of the climb in the second half.
        float rise = Mathf.SmoothStep(0f, 1f, t);
        float strength = (finale ? sunStrength.y : sunStrength.x) * rise * fade;
        float height = Mathf.Lerp(sunPath.y, finale ? sunPath.z : sunPath.z + 0.8f, rise);

        sun.enabled = strength > 0.001f;
        sun.transform.position = new Vector3(sunPath.x, height, 0f);
        float size = Mathf.Lerp(6f, finale ? 10f : 7.5f, rise);
        sun.transform.localScale = new Vector3(size * 1.3f, size, 1f);
        sun.color = new Color(sunColor.r, sunColor.g, sunColor.b, strength);

        // Its light laid down the water toward the harbour: a long, thin wash.
        sunPathOnWater.enabled = sun.enabled;
        sunPathOnWater.transform.position = new Vector3(sunPath.x - 0.6f, height - 4.2f, 0f);
        sunPathOnWater.transform.localScale = new Vector3(size * 0.28f, size * 1.1f, 1f);
        sunPathOnWater.color = new Color(sunColor.r, sunColor.g * 0.95f, sunColor.b * 0.9f, strength * 0.35f);
    }

    /// <summary>
    /// Sends birds up. An ordinary dawn lets its couple go early; the last one
    /// launches them on a curve that starts sparse and fills the sky, and keeps
    /// every bird that leaves the screen coming back round.
    /// </summary>
    private void LaunchGulls(float t)
    {
        int wanted = finale
            ? Mathf.Clamp(1 + Mathf.FloorToInt((finaleGulls - 1) * Mathf.Pow(t, 1.4f)), 1, gulls.Length)
            : Mathf.Min(ordinaryGulls, Mathf.CeilToInt(t * 4f * ordinaryGulls));

        int inAir = 0;
        foreach (Gull gull in gulls)
        {
            if (gull.flying)
            {
                inAir++;
            }
        }

        // The last dawn keeps its sky full; an ordinary one lets birds leave.
        int target = finale ? wanted : wanted - launched + inAir;

        for (int i = 0; i < gulls.Length && inAir < target; i++)
        {
            if (!gulls[i].flying)
            {
                Launch(gulls[i]);
                inAir++;
                launched++;
            }
        }
    }

    private void Launch(Gull gull)
    {
        gull.flying = true;
        gull.direction = Random.value < 0.5f ? 1f : -1f;
        gull.speed = Random.Range(gullSpeed.x, gullSpeed.y);
        gull.baseY = Random.Range(-1.5f, 4.6f);
        gull.wave = Random.Range(0f, 10f);
        gull.flapRate = Random.Range(3.2f, 4.4f);
        gull.clock = Random.Range(0f, 3f);

        // Enter from just off either side, a little staggered so a flock
        // arriving together does not cross the edge in a line.
        float x = -gull.direction * (EdgeX + Random.Range(0f, 2f));
        gull.renderer.transform.position = new Vector3(x, gull.baseY, 0f);

        float scale = Random.Range(0.7f, 1.1f);
        gull.renderer.transform.localScale = new Vector3(scale, scale, 1f);
        gull.renderer.flipX = gull.direction < 0f;
        gull.renderer.enabled = true;
    }

    private void FlyGulls(float dt)
    {
        foreach (Gull gull in gulls)
        {
            if (!gull.flying)
            {
                continue;
            }

            gull.clock += dt;
            Vector3 p = gull.renderer.transform.position;
            p.x += gull.direction * gull.speed * dt;

            // A lazy rise and fall along the way, and a tilt to match.
            p.y = gull.baseY + Mathf.Sin(gull.clock * 0.9f + gull.wave) * 0.35f;
            float tilt = Mathf.Cos(gull.clock * 0.9f + gull.wave) * 8f * gull.direction;
            gull.renderer.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, tilt));

            // Flapping in bursts, then gliding with the wings held up.
            float cycle = Mathf.Repeat(gull.clock + gull.wave, 3f);
            bool gliding = cycle > 1.8f;
            bool down = !gliding && Mathf.Repeat(gull.clock * gull.flapRate, 1f) > 0.5f;
            gull.renderer.sprite = down ? gullWingsDown : gullWingsUp;
            gull.renderer.color = new Color(1f, 1f, 1f, fade);

            if (Mathf.Abs(p.x) > EdgeX + 2.5f && Mathf.Sign(p.x) == gull.direction)
            {
                gull.flying = false;
                gull.renderer.enabled = false;
            }
        }
    }

    /// <summary>Calls from the flock — more of them, closer together, the fuller the sky.</summary>
    private void Cry(float dt)
    {
        nextCry -= dt;
        if (nextCry > 0f || fadeSpeed > 0f)
        {
            return;
        }

        Gull caller = null;
        int inAir = 0;
        foreach (Gull gull in gulls)
        {
            if (gull.flying)
            {
                inAir++;
                if (caller == null || Random.value < 0.3f)
                {
                    caller = gull;
                }
            }
        }

        if (caller != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAt("gull_cry", caller.renderer.transform.position);
        }

        float crowd = gulls.Length > 1 ? Mathf.Clamp01(inAir / (float)(gulls.Length - 1)) : 0f;
        nextCry = Mathf.Lerp(3f, 0.7f, crowd) * Random.Range(0.7f, 1.3f);
    }

    private void StopAll()
    {
        playing = false;
        sun.enabled = false;
        sunPathOnWater.enabled = false;

        foreach (Gull gull in gulls)
        {
            gull.flying = false;
            gull.renderer.enabled = false;
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

    /// <summary>A soft, warm-cored glow, one unit across before scaling.</summary>
    private static Sprite MakeGlowSprite()
    {
        const int across = 128;
        var texture = new Texture2D(across, across, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "SunGlow"
        };

        var pixels = new Color32[across * across];
        float centre = (across - 1) * 0.5f;
        for (int y = 0; y < across; y++)
        {
            for (int x = 0; x < across; x++)
            {
                float r = new Vector2(x - centre, y - centre).magnitude / centre;
                float falloff = Mathf.Clamp01(1f - r);
                float alpha = falloff * falloff * (0.6f + 0.4f * falloff);

                // A little whiter in the middle, where the disc itself would be.
                byte core = (byte)(Mathf.Lerp(0.85f, 1f, falloff * falloff) * 255f);
                pixels[y * across + x] = new Color32(255, core, core, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, across, across), new Vector2(0.5f, 0.5f), across);
    }
}
