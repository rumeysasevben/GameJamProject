using System.Collections;
using UnityEngine;

/// <summary>
/// Now and then, a dolphin.
///
/// Every minute or so it breaks the surface far out in the open water, arcs
/// over and slips back under, throwing up a little water where it leaves and
/// where it dives back in. It is a small reward for looking at the sea, so it
/// is rare, small and muted — far off, not up against the glass — it never
/// jumps next to a rock or a ship, and nothing in the game reacts to it.
/// </summary>
public class Dolphin : MonoBehaviour
{
    [Tooltip("The dolphin drawing, facing right.")]
    [SerializeField] private Sprite sprite;

    [Tooltip("Unlit sprite material, so the dolphin reads against the night sea.")]
    [SerializeField] private Material material;

    [Tooltip("Wait before the first jump, in seconds: shortest and longest.")]
    [SerializeField] private Vector2 firstDelay = new Vector2(30f, 70f);

    [Tooltip("Wait between jumps, in seconds: shortest and longest.")]
    [SerializeField] private Vector2 interval = new Vector2(45f, 115f);

    [Tooltip("The water it may jump in, in world units: the far part of the sea, toward the top of the screen, clear of the coast and the top bar.")]
    [SerializeField] private Rect openSea = new Rect(-3f, 1.8f, 11.5f, 2.3f);

    [Tooltip("How far a jump must stay from rocks and ships.")]
    [SerializeField] private float clearance = 1f;

    [Tooltip("How big the dolphin is drawn. 1 is the sprite's own size; less reads as further away.")]
    [SerializeField] private float size = 0.5f;

    [Tooltip("Multiplied into the dolphin's colour: dimmed and cooled toward the sea, as something far off at night.")]
    [SerializeField] private Color tint = new Color(0.55f, 0.64f, 0.76f, 0.8f);

    [Tooltip("How long a jump lasts, surface to surface.")]
    [SerializeField] private float jumpTime = 1.5f;

    [Tooltip("How far a jump travels.")]
    [SerializeField] private float travel = 1f;

    [Tooltip("How high the arc rises.")]
    [SerializeField] private float height = 0.28f;

    [Tooltip("Water colour for the splash and ripples.")]
    [SerializeField] private Color waterColor = new Color(0.72f, 0.82f, 0.92f, 0.75f);

    [Tooltip("Droplets thrown up each time it breaks the surface.")]
    [SerializeField] private int dropletCount = 9;

    [Tooltip("Sorting order. Over the sea and the rocks' foam, under the ships.")]
    [SerializeField] private int sortingOrder = -6;

    private const float SplashLife = 0.7f;
    private const float Gravity = 2.6f;

    private SpriteRenderer body;
    private Splash[] splashes;

    private static Sprite rippleSprite;
    private static Sprite dropletSprite;

    /// <summary>The water thrown up at one surface break: droplets, a foam puff and a ripple.</summary>
    private class Splash
    {
        public float age = SplashLife;
        public float strength;
        public SpriteRenderer ripple;
        public SpriteRenderer foam;
        public SpriteRenderer[] droplets;
        public Vector2[] velocities;
    }

    private void Awake()
    {
        body = CreateRenderer("Body", sprite, sortingOrder);

        if (rippleSprite == null)
        {
            rippleSprite = MakeRippleSprite();
            dropletSprite = MakeDropletSprite();
        }

        // One for leaving the water, one for diving back in.
        splashes = new Splash[2];
        for (int s = 0; s < splashes.Length; s++)
        {
            var splash = new Splash
            {
                ripple = CreateRenderer($"Ripple_{s}", rippleSprite, sortingOrder - 1),
                foam = CreateRenderer($"Foam_{s}", dropletSprite, sortingOrder - 1),
                droplets = new SpriteRenderer[Mathf.Max(0, dropletCount)],
                velocities = new Vector2[Mathf.Max(0, dropletCount)]
            };

            for (int d = 0; d < splash.droplets.Length; d++)
            {
                // Over the body, so the water seems to fall around it.
                splash.droplets[d] = CreateRenderer($"Drop_{s}_{d}", dropletSprite, sortingOrder + 1);
            }

            splashes[s] = splash;
        }
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private void Update()
    {
        for (int s = 0; s < splashes.Length; s++)
        {
            DrawSplash(splashes[s], Time.deltaTime);
        }
    }

    private IEnumerator Run()
    {
        yield return Wait(Random.Range(firstDelay.x, firstDelay.y));

        while (true)
        {
            if (sprite != null && TryPickSpot(out Vector2 start, out Vector2 direction))
            {
                yield return Jump(start, direction);
            }

            yield return Wait(Random.Range(interval.x, interval.y));
        }
    }

    /// <summary>Jumps once, right away. For checking the look in Play mode.</summary>
    [ContextMenu("Jump Now")]
    private void JumpNow()
    {
        if (Application.isPlaying && TryPickSpot(out Vector2 start, out Vector2 direction))
        {
            StartCoroutine(Jump(start, direction));
        }
    }

    private IEnumerator Jump(Vector2 start, Vector2 direction)
    {
        Vector2 end = start + direction * travel;
        bool facingLeft = direction.x < 0f;

        // Coming out is the smaller of the two; the dive back in hits harder.
        Throw(splashes[0], start, direction, 0.7f);
        body.enabled = true;
        body.flipX = facingLeft;

        float elapsed = 0f;
        while (elapsed < jumpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpTime);

            // A parabola over the straight run, with the body pointing along
            // it: nose up coming out, nose down going back in.
            Vector2 position = Vector2.Lerp(start, end, t) + Vector2.up * (height * 4f * t * (1f - t));
            Vector2 velocity = direction * travel + Vector2.up * (height * 4f * (1f - 2f * t));

            float angle = Mathf.Atan2(velocity.y, Mathf.Abs(velocity.x)) * Mathf.Rad2Deg;
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, facingLeft ? -angle : angle));

            // Rising out of the water and sinking back into it: faded and a
            // touch smaller at either end, where most of it is still under.
            float emerged = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.2f);
            body.color = new Color(tint.r, tint.g, tint.b, tint.a * emerged);
            float scale = size * Mathf.Lerp(0.8f, 1f, emerged);
            body.transform.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }

        body.enabled = false;
        Throw(splashes[1], end, direction, 1f);
    }

    private void Throw(Splash splash, Vector2 origin, Vector2 direction, float strength)
    {
        splash.age = 0f;
        splash.strength = strength;

        for (int d = 0; d < splash.droplets.Length; d++)
        {
            // A crown of water, leaning the way the dolphin is going.
            float spread = Random.Range(-1f, 1f);
            splash.velocities[d] = new Vector2(spread * 0.45f + direction.x * 0.15f, Random.Range(0.45f, 0.95f)) * strength;
            splash.droplets[d].transform.position = origin + new Vector2(spread * 0.06f, 0f);
            splash.droplets[d].enabled = true;
        }

        splash.ripple.transform.position = origin;
        splash.foam.transform.position = origin;
        splash.ripple.enabled = true;
        splash.foam.enabled = true;
    }

    private void DrawSplash(Splash splash, float dt)
    {
        if (splash.age >= SplashLife)
        {
            return;
        }

        splash.age += dt;
        float k = Mathf.Clamp01(splash.age / SplashLife);
        bool alive = k < 1f;

        for (int d = 0; d < splash.droplets.Length; d++)
        {
            SpriteRenderer drop = splash.droplets[d];
            drop.enabled = alive;
            if (!alive)
            {
                continue;
            }

            splash.velocities[d] += Vector2.down * (Gravity * dt);
            drop.transform.position += (Vector3)(splash.velocities[d] * dt);

            float scale = Mathf.Lerp(0.9f, 0.4f, k);
            drop.transform.localScale = new Vector3(scale, scale, 1f);
            drop.color = WithAlpha(waterColor, waterColor.a * (1f - k * k));
        }

        // The white water where it broke the surface, spreading and settling.
        splash.foam.enabled = alive;
        float foamScale = Mathf.Lerp(1.5f, 3.2f, Mathf.Sqrt(k)) * splash.strength;
        splash.foam.transform.localScale = new Vector3(foamScale, foamScale * 0.45f, 1f);
        splash.foam.color = WithAlpha(waterColor, waterColor.a * 0.55f * (1f - k));

        // A ring running outward, squashed because the sea is seen at a slant.
        splash.ripple.enabled = alive;
        float ringScale = Mathf.Lerp(0.15f, 0.6f, Mathf.Sqrt(k)) * splash.strength;
        splash.ripple.transform.localScale = new Vector3(ringScale, ringScale * 0.45f, 1f);
        splash.ripple.color = WithAlpha(waterColor, waterColor.a * 0.7f * (1f - k));
    }

    /// <summary>
    /// A spot in the far water with room for the whole jump — nothing within
    /// <see cref="clearance"/> of its start, middle or end.
    /// </summary>
    private bool TryPickSpot(out Vector2 start, out Vector2 direction)
    {
        Rock[] rocks = FindObjectsByType<Rock>(FindObjectsSortMode.None);
        Ship[] ships = FindObjectsByType<Ship>(FindObjectsSortMode.None);

        for (int attempt = 0; attempt < 16; attempt++)
        {
            direction = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.Range(-0.15f, 0.15f)).normalized;
            start = new Vector2(Random.Range(openSea.xMin, openSea.xMax), Random.Range(openSea.yMin, openSea.yMax));
            Vector2 end = start + direction * travel;

            if (!openSea.Contains(end))
            {
                continue;
            }

            Vector2 middle = (start + end) * 0.5f;
            if (IsClear(start, rocks, ships) && IsClear(middle, rocks, ships) && IsClear(end, rocks, ships))
            {
                return true;
            }
        }

        start = default;
        direction = default;
        return false;
    }

    private bool IsClear(Vector2 point, Rock[] rocks, Ship[] ships)
    {
        foreach (Rock rock in rocks)
        {
            if (Vector2.Distance(point, rock.Position) < clearance + rock.Radius)
            {
                return false;
            }
        }

        foreach (Ship ship in ships)
        {
            if (Vector2.Distance(point, ship.transform.position) < clearance)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator Wait(float seconds)
    {
        // Scaled time, so the dolphin waits out the pause menu too.
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite rendererSprite, int order)
    {
        var go = new GameObject(objectName, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = rendererSprite;
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

    /// <summary>A thin soft ring, 0.64 units across before scaling.</summary>
    private static Sprite MakeRippleSprite()
    {
        return MakeRoundSprite("DolphinRipple", 64, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.1f));
    }

    /// <summary>A soft round blob, 0.06 units across before scaling.</summary>
    private static Sprite MakeDropletSprite()
    {
        return MakeRoundSprite("DolphinDroplet", 6, r => Mathf.Clamp01(1f - r * r));
    }

    private static Sprite MakeRoundSprite(string spriteName, int across, System.Func<float, float> alphaAtRadius)
    {
        var texture = new Texture2D(across, across, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = spriteName
        };

        var pixels = new Color32[across * across];
        float centre = (across - 1) * 0.5f;
        for (int y = 0; y < across; y++)
        {
            for (int x = 0; x < across; x++)
            {
                float r = new Vector2(x - centre, y - centre).magnitude / (across * 0.5f);
                pixels[y * across + x] = new Color32(255, 255, 255, (byte)(alphaAtRadius(r) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, across, across), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(openSea.center, openSea.size);
    }
}
