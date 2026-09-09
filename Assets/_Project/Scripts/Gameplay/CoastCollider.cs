using UnityEngine;

/// <summary>
/// The shoreline, approximated by hand-placed circles.
///
/// Physics2D is not used anywhere in this game, so the coast is not a collider
/// but a list of circles laid along the shore in the editor. Circles are enough
/// because everything that can hit them is also a circle, and they are drawn as
/// gizmos so placing them is a matter of dragging until the coast is covered.
/// </summary>
public class CoastCollider : MonoBehaviour
{
    /// <summary>One circle of the shoreline, positioned in this object's local space.</summary>
    [System.Serializable]
    public struct CircleShape
    {
        [Tooltip("Centre, relative to this object.")]
        public Vector2 offset;

        [Tooltip("Radius in world units.")]
        public float radius;
    }

    [Tooltip("Circles covering the shoreline. Overlap them generously; gaps let ships slip onto the land.")]
    [SerializeField] private CircleShape[] circles = new CircleShape[0];

    /// <summary>How many circles make up the coast.</summary>
    public int Count => circles == null ? 0 : circles.Length;

    /// <summary>Centre of circle <paramref name="index"/> in world units.</summary>
    public Vector2 GetCenter(int index)
    {
        return (Vector2)transform.position + circles[index].offset;
    }

    /// <summary>Radius of circle <paramref name="index"/>.</summary>
    public float GetRadius(int index)
    {
        return circles[index].radius;
    }

    private void OnDrawGizmos()
    {
        if (circles == null)
        {
            return;
        }

        Gizmos.color = new Color(0.9f, 0.8f, 0.4f, 0.5f);
        for (int i = 0; i < circles.Length; i++)
        {
            Gizmos.DrawWireSphere(GetCenter(i), circles[i].radius);
        }
    }
}
