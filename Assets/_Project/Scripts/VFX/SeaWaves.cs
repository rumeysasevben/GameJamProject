using UnityEngine;

/// <summary>
/// The wave marks on the open sea, moving.
///
/// Each one sways a little from side to side and swells and fades on its own
/// speed and phase, so the water looks alive without anything travelling across
/// it that could be mistaken for a ship.
/// </summary>
public class SeaWaves : MonoBehaviour
{
    [Tooltip("The waves.")]
    [SerializeField] private SpriteRenderer[] waves = new SpriteRenderer[0];

    [Tooltip("Multiplied into every wave's colour: a muted sea blue, mostly see-through, so the marks read as water catching a little light rather than glowing lines.")]
    [SerializeField] private Color tint = new Color(0.55f, 0.7f, 0.85f, 0.45f);

    [Tooltip("How far a wave sways either side of where it was placed, in world units.")]
    [SerializeField] private float sway = 0.12f;

    [Tooltip("How deep the fade goes. 0 keeps a wave steady, 1 fades it out entirely.")]
    [SerializeField, Range(0f, 1f)] private float fade = 0.5f;

    [Tooltip("Slowest and fastest wave, in cycles per second.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.12f, 0.3f);

    private Vector3[] home;
    private float[] speeds;
    private float[] phases;
    private float[] baseAlpha;
    private Color[] baseColor;

    private void Awake()
    {
        home = new Vector3[waves.Length];
        speeds = new float[waves.Length];
        phases = new float[waves.Length];
        baseAlpha = new float[waves.Length];
        baseColor = new Color[waves.Length];

        for (int i = 0; i < waves.Length; i++)
        {
            // Seeded off the index, as the stars are, so the sea moves the same
            // way every night.
            speeds[i] = Mathf.Lerp(speedRange.x, speedRange.y, Frac(i * 0.6180339f));
            phases[i] = Frac(i * 0.7548777f) * Mathf.PI * 2f;

            if (waves[i] != null)
            {
                home[i] = waves[i].transform.localPosition;
                baseColor[i] = waves[i].color * tint;
                baseAlpha[i] = baseColor[i].a;
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < waves.Length; i++)
        {
            SpriteRenderer wave = waves[i];
            if (wave == null)
            {
                continue;
            }

            float angle = Time.time * speeds[i] * Mathf.PI * 2f + phases[i];
            wave.transform.localPosition = home[i] + new Vector3(Mathf.Sin(angle) * sway, 0f, 0f);

            // Brightest as it passes the middle of its sway, dimmest at the ends.
            float swell = Mathf.Abs(Mathf.Cos(angle));
            Color c = baseColor[i];
            c.a = baseAlpha[i] * (1f - fade + fade * swell);
            wave.color = c;
        }
    }

    private static float Frac(float x) => x - Mathf.Floor(x);
}
