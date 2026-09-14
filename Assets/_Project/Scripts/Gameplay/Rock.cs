using UnityEngine;

/// <summary>An obstacle: a circle and a sprite, nothing more.</summary>
public class Rock : MonoBehaviour
{
    [Tooltip("The rock art. Swapped per spawn so one prefab covers every variant.")]
    [SerializeField] private SpriteRenderer body;

    /// <summary>Collision radius in world units.</summary>
    public float Radius { get; private set; } = 0.5f;

    /// <summary>Centre in world units.</summary>
    public Vector2 Position => transform.position;

    /// <summary>Places and dresses the rock for a night.</summary>
    public void Setup(RockSpawn spawn)
    {
        Radius = spawn.radius;
        transform.position = new Vector3(spawn.position.x, spawn.position.y, 0f);

        if (body != null && spawn.sprite != null)
        {
            body.sprite = spawn.sprite;
        }

        if (body != null)
        {
            float scale = spawn.scale > 0f ? spawn.scale : 1f;
            body.transform.localScale = new Vector3(scale, scale, 1f);
        }

        AddSurf();
    }

    /// <summary>
    /// Small waves lapping at the rock from every side. Kept well under the
    /// quay's: a rock at sea is a hazard to steer round, and big surf around
    /// it would crowd the water and pull the eye from the ships.
    /// </summary>
    private void AddSurf()
    {
        if (TryGetComponent(out ShoreSurf _))
        {
            return;
        }

        const int sides = 5;
        float turn = Random.value * Mathf.PI * 2f;
        var points = new ShoreSurf.SurfPoint[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = turn + i * Mathf.PI * 2f / sides;
            var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 edge = outward * (Radius * 0.85f);
            points[i] = new ShoreSurf.SurfPoint(edge.x, edge.y, outward.x, outward.y, Radius * 0.9f);
        }

        // Under the rock and the ships' halos; the spray just over the rock.
        gameObject.AddComponent<ShoreSurf>().Configure(
            points,
            waveSize: 0.4f,
            pauseRange: new Vector2(2f, 5f),
            drops: 3,
            foamSortingOrder: -4,
            spraySortingOrder: 1,
            foam: new Color(0.78f, 0.88f, 0.98f, 0.4f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
