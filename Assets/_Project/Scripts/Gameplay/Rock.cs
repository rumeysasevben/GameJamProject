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
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
