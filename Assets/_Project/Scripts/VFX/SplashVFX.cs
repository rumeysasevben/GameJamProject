using System.Collections;
using UnityEngine;

/// <summary>
/// The little burst of water when a ship hits something.
///
/// A collision in this game is a mistake, not a failure — nothing is lost and
/// nothing breaks. The splash has to say so: small, quick, and gone before the
/// player has time to feel told off.
///
/// Droplets are made once and reused. Collisions come in bursts when a ship is
/// pressed against a rock, and allocating a handful of objects each time is
/// the kind of thing that stutters on WebGL.
/// </summary>
public class SplashVFX : MonoBehaviour
{
    [Tooltip("The droplet drawing. A small soft circle.")]
    [SerializeField] private Sprite droplet;

    [Tooltip("Droplets per splash.")]
    [SerializeField] private int dropletCount = 8;

    [Tooltip("How many splashes can be in the air at once before the oldest is reused.")]
    [SerializeField] private int poolSize = 4;

    [Tooltip("How long a droplet lives.")]
    [SerializeField] private float duration = 0.4f;

    [Tooltip("How far a droplet travels.")]
    [SerializeField] private float spread = 0.45f;

    [Tooltip("Droplet colour. Water picking up the beam, not white paint.")]
    [SerializeField] private Color color = new Color(0.85f, 0.93f, 1f, 0.9f);

    [Tooltip("Sorting order for the droplets. Above the ships, below the UI.")]
    [SerializeField] private int sortingOrder = 25;

    private Transform[] bursts;
    private SpriteRenderer[][] droplets;
    private int nextBurst;

    /// <summary>Throws up a splash at <paramref name="position"/>, in world units.</summary>
    public void Play(Vector2 position)
    {
        EnsurePool();

        int index = nextBurst;
        nextBurst = (nextBurst + 1) % bursts.Length;

        bursts[index].position = new Vector3(position.x, position.y, 0f);
        StartCoroutine(PlayBurst(index));
    }

    private IEnumerator PlayBurst(int index)
    {
        SpriteRenderer[] burst = droplets[index];

        // A ring of directions with a little variation per droplet, so two
        // splashes in the same spot do not look like the same picture twice.
        var directions = new Vector2[burst.Length];
        for (int i = 0; i < burst.Length; i++)
        {
            float angle = (i / (float)burst.Length) * Mathf.PI * 2f + Random.value * 0.4f;
            float reach = Mathf.Lerp(0.6f, 1f, Random.value);
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * reach;

            burst[i].enabled = true;
            burst[i].transform.localPosition = Vector3.zero;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Out fast, then slowing, while fading and shrinking — water
            // thrown up and falling back rather than an expanding ring.
            float distance = Mathf.Sqrt(t) * spread;
            float scale = Mathf.Lerp(0.5f, 0.15f, t);
            float alpha = color.a * (1f - t);

            for (int i = 0; i < burst.Length; i++)
            {
                burst[i].transform.localPosition = directions[i] * distance;
                burst[i].transform.localScale = new Vector3(scale, scale, 1f);
                burst[i].color = new Color(color.r, color.g, color.b, alpha);
            }

            yield return null;
        }

        for (int i = 0; i < burst.Length; i++)
        {
            burst[i].enabled = false;
        }
    }

    private void EnsurePool()
    {
        if (bursts != null)
        {
            return;
        }

        int count = Mathf.Max(1, poolSize);
        bursts = new Transform[count];
        droplets = new SpriteRenderer[count][];

        for (int b = 0; b < count; b++)
        {
            var burstObject = new GameObject($"Splash_{b}");
            burstObject.transform.SetParent(transform, false);
            bursts[b] = burstObject.transform;

            droplets[b] = new SpriteRenderer[Mathf.Max(1, dropletCount)];

            for (int i = 0; i < droplets[b].Length; i++)
            {
                var dropObject = new GameObject($"Droplet_{i}", typeof(SpriteRenderer));
                dropObject.transform.SetParent(burstObject.transform, false);

                var renderer = dropObject.GetComponent<SpriteRenderer>();
                renderer.sprite = droplet;
                renderer.color = color;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;

                droplets[b][i] = renderer;
            }
        }
    }

    /// <summary>Supplies the droplet drawing when the effect is built in code.</summary>
    public void Bind(Sprite dropletSprite)
    {
        droplet = dropletSprite;
    }
}
