using UnityEngine;

/// <summary>
/// The stars, breathing.
///
/// Each one is given its own speed and phase, so the sky shimmers instead of
/// pulsing as a single unit — the difference between a night sky and a string
/// of fairy lights.
///
/// Alpha is scaled rather than set, so <see cref="DawnController"/> can fade
/// the whole field out at sunrise and the twinkle goes with it.
/// </summary>
public class Starfield : MonoBehaviour
{
    [Tooltip("The stars.")]
    [SerializeField] private SpriteRenderer[] stars = new SpriteRenderer[0];

    [Tooltip("How deep the twinkle goes. 0 is a steady star, 1 blinks out entirely.")]
    [SerializeField, Range(0f, 1f)] private float depth = 0.45f;

    [Tooltip("Slowest and fastest twinkle, in cycles per second.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.2f, 0.7f);

    private float[] speeds;
    private float[] phases;
    private float[] baseAlpha;

    /// <summary>
    /// How much of the field is showing, 1 down to 0. Dawn drives this rather
    /// than writing the stars' colours itself, which would fight the twinkle
    /// for the same field every frame.
    /// </summary>
    public float Visibility { get; set; } = 1f;

    private void Awake()
    {
        speeds = new float[stars.Length];
        phases = new float[stars.Length];
        baseAlpha = new float[stars.Length];

        for (int i = 0; i < stars.Length; i++)
        {
            // Seeded off the index rather than Random, so a star keeps the same
            // rhythm every run and the sky is the same sky each night.
            speeds[i] = Mathf.Lerp(speedRange.x, speedRange.y, Frac(i * 0.6180339f));
            phases[i] = Frac(i * 0.7548777f) * Mathf.PI * 2f;
            baseAlpha[i] = stars[i] != null ? stars[i].color.a : 1f;
        }
    }

    private void Update()
    {
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null)
            {
                continue;
            }

            float wave = (Mathf.Sin(Time.time * speeds[i] * Mathf.PI * 2f + phases[i]) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f - depth, 1f, wave);

            Color color = stars[i].color;
            stars[i].color = new Color(color.r, color.g, color.b, baseAlpha[i] * scale * Mathf.Clamp01(Visibility));
        }
    }

    /// <summary>The stars, so the dawn can fade them.</summary>
    public SpriteRenderer[] Stars => stars;

    /// <summary>Fills in the stars when the field is built in code.</summary>
    public void Bind(SpriteRenderer[] field)
    {
        stars = field;
    }

    private static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }
}
