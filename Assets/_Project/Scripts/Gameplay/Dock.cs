using UnityEngine;

/// <summary>
/// A berth. A ship arrives by entering its circle — there is no trigger and no
/// collider, just a centre and a radius, which keeps arrival deterministic and
/// frame-rate independent.
/// </summary>
public class Dock : MonoBehaviour
{
    [Tooltip("The pier art.")]
    [SerializeField] private SpriteRenderer pier;

    [Tooltip("Coloured strip that tells two berths apart. Only shown on nights with more than one dock.")]
    [SerializeField] private SpriteRenderer colorStrip;

    /// <summary>Which dock this is; ships name their berth by this index.</summary>
    public int Index { get; private set; }

    /// <summary>How close a ship must come to count as arrived.</summary>
    public float Radius { get; private set; } = 0.6f;

    /// <summary>Centre of the berth in world units.</summary>
    public Vector2 Position => transform.position;

    /// <summary>False on nights that do not use this berth; it is then hidden and ignored.</summary>
    public bool Active { get; private set; } = true;

    /// <summary>
    /// Places the berth for a night. <paramref name="showStrip"/> is on only
    /// when the night has more than one dock — a lone berth needs no colour to
    /// tell it from anything.
    /// </summary>
    public void Setup(DockSpawn spawn, int index, bool showStrip)
    {
        Index = index;
        Radius = spawn.radius;
        transform.position = new Vector3(spawn.position.x, spawn.position.y, 0f);

        if (colorStrip != null)
        {
            colorStrip.enabled = showStrip;
            colorStrip.color = spawn.color;
        }

        SetActive(true);
    }

    /// <summary>Shows or hides the berth for this night.</summary>
    public void SetActive(bool active)
    {
        Active = active;
        gameObject.SetActive(active);
    }

    /// <summary>True when <paramref name="point"/> is inside the berth.</summary>
    public bool Contains(Vector2 point)
    {
        return ((Vector2)transform.position - point).sqrMagnitude <= Radius * Radius;
    }

    private void OnDrawGizmos()
    {
        // The berth is invisible in the art; without this it cannot be placed.
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
