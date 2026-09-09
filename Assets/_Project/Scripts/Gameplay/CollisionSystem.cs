using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the steered ship out of everything else.
///
/// Only the linked ship is ever tested. Idle ships sit still and cannot run
/// into anything, so testing them would be pure cost — this is what lets the
/// game get away with a hand-rolled circle sweep and no Physics2D at all.
///
/// A ship recovering from a bounce is skipped entirely. Without that, a ship
/// pressed into a rock re-collides every frame and burrows in instead of
/// bouncing off.
/// </summary>
public class CollisionSystem : MonoBehaviour
{
    [Tooltip("How far the ship that was hit is shoved aside. Enough to separate the hulls, not enough to look like it was steered.")]
    [SerializeField] private float nudgeDistance = 0.1f;

    /// <summary>Raised on a collision, with the point it happened at. Drives the splash, the sound and the night's tally.</summary>
    public event Action<Vector2> OnCollision;

    /// <summary>
    /// One frame of collision for the linked ship. Returns as soon as anything
    /// is hit: the ship is frozen by the bounce anyway, so a second hit in the
    /// same frame could only push it somewhere unpredictable.
    /// </summary>
    public void Tick(Ship linked, IReadOnlyList<Ship> ships, IReadOnlyList<Rock> rocks, IReadOnlyList<Dock> docks, CoastCollider coast)
    {
        if (linked == null || linked.State != ShipState.Linked || linked.IsFrozen)
        {
            return;
        }

        Vector2 position = linked.Position;
        float radius = linked.Radius;

        if (ships != null)
        {
            for (int i = 0; i < ships.Count; i++)
            {
                Ship other = ships[i];
                if (other == null || other == linked || other.State == ShipState.Docked)
                {
                    continue;
                }

                if (Overlaps(position, radius, other.Position, other.Radius, out Vector2 normal))
                {
                    linked.Bounce(normal);
                    other.Nudge(-normal, nudgeDistance);
                    OnCollision?.Invoke(Midpoint(position, other.Position));
                    return;
                }
            }
        }

        if (rocks != null)
        {
            for (int i = 0; i < rocks.Count; i++)
            {
                Rock rock = rocks[i];
                if (rock == null)
                {
                    continue;
                }

                if (Overlaps(position, radius, rock.Position, rock.Radius, out Vector2 normal))
                {
                    linked.Bounce(normal);
                    OnCollision?.Invoke(Midpoint(position, rock.Position));
                    return;
                }
            }
        }

        if (coast != null)
        {
            for (int i = 0; i < coast.Count; i++)
            {
                if (Overlaps(position, radius, coast.GetCenter(i), coast.GetRadius(i), out Vector2 normal))
                {
                    linked.Bounce(normal);
                    OnCollision?.Invoke(Midpoint(position, coast.GetCenter(i)));
                    return;
                }
            }
        }

        // The wrong berth is a soft wall, not a crash. The ship is held at its
        // edge with no bounce, no splash and no tally: steering into the wrong
        // dock is a misreading, and punishing it with a collision would say the
        // player did something clumsy rather than something mistaken.
        if (docks != null)
        {
            for (int i = 0; i < docks.Count; i++)
            {
                Dock dock = docks[i];
                if (dock == null || !dock.Active || dock.Index == linked.DockIndex)
                {
                    continue;
                }

                if (Overlaps(position, radius, dock.Position, dock.Radius, out Vector2 normal))
                {
                    float wanted = dock.Radius + radius;
                    float actual = (position - dock.Position).magnitude;
                    linked.Nudge(normal, wanted - actual);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Circle against circle. <paramref name="normal"/> comes back pointing
    /// from <paramref name="otherPosition"/> towards <paramref name="position"/>
    /// — the direction the first circle has to move to get clear.
    /// </summary>
    private static bool Overlaps(Vector2 position, float radius, Vector2 otherPosition, float otherRadius, out Vector2 normal)
    {
        Vector2 offset = position - otherPosition;
        float reach = radius + otherRadius;

        if (offset.sqrMagnitude >= reach * reach)
        {
            normal = Vector2.zero;
            return false;
        }

        // Dead centre on top of each other: there is no direction to separate
        // along, so pick one rather than normalising a zero vector.
        normal = offset.sqrMagnitude <= Mathf.Epsilon ? Vector2.up : offset.normalized;
        return true;
    }

    private static Vector2 Midpoint(Vector2 a, Vector2 b)
    {
        return (a + b) * 0.5f;
    }
}
