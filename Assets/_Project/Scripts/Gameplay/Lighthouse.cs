using UnityEngine;

/// <summary>
/// The tower. Its only job is to point the beam at the cursor.
///
/// Kept separate from <see cref="Beam"/> so the beam stays a pure cone — angle
/// in, hit test out — while aiming, which is a gameplay decision, lives here
/// and is driven from <see cref="NightController"/>'s frame order.
/// </summary>
public class Lighthouse : MonoBehaviour
{
    [Tooltip("Where the light leaves the tower. The beam's pivot.")]
    [SerializeField] private Transform lampPoint;

    [Tooltip("The cone this lighthouse aims.")]
    [SerializeField] private Beam beam;

    /// <summary>The cone this lighthouse aims.</summary>
    public Beam Beam => beam;

    /// <summary>Where the light leaves the tower, in world units.</summary>
    public Vector2 LampPosition => lampPoint != null ? (Vector2)lampPoint.position : (Vector2)transform.position;

    /// <summary>Turns the beam towards <paramref name="cursorWorld"/>. Called once a frame.</summary>
    public void Aim(Vector2 cursorWorld)
    {
        if (beam == null)
        {
            return;
        }

        beam.SetDirection(cursorWorld - LampPosition);
    }
}
