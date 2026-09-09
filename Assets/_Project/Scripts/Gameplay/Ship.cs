using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Where a ship is in its night.</summary>
public enum ShipState
{
    /// <summary>Sailing in from open sea to the spot it will wait at. Not yet asking for anything.</summary>
    Arriving,

    /// <summary>Waiting out at sea, flashing its sequence, steerable by nobody.</summary>
    Idle,

    /// <summary>Answering the lighthouse: follows the cursor until it berths or is dropped.</summary>
    Linked,

    /// <summary>Home. Out of the simulation for the rest of the night.</summary>
    Docked
}

/// <summary>
/// One vessel.
///
/// A ship does nothing on its own. It has no Update: <see cref="NightController"/>
/// ticks the single linked ship, and the idle ones just sit there flashing. That
/// is the whole game — only the ship you are talking to moves.
///
/// Steering is deliberately not physics. A ship turns its heading towards the
/// cursor at its own turn rate and swims forward; heavy ships turn slowly, which
/// is the entire difference between a rowboat and a freighter.
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class Ship : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The hull. Its sprite is swapped as the ship turns.")]
    [SerializeField] private SpriteRenderer body;

    [Tooltip("Foam trail. Only shown while the ship is under way.")]
    [SerializeField] private SpriteRenderer wake;

    [Tooltip("The signal lamp sprite, driven by the emitter.")]
    [SerializeField] private SpriteRenderer lamp;

    [Tooltip("Ellipse under the hull, shown while the beam is on this ship.")]
    [SerializeField] private SpriteRenderer targetRing;

    [Tooltip("Soft glow shown while this ship is the linked one.")]
    [SerializeField] private SpriteRenderer linkedHalo;

    [Tooltip("Plays this ship's sequence while it waits.")]
    [SerializeField] private ShipSignalEmitter emitter;

    [Tooltip("The floating sequence bubble above the hull.")]
    [SerializeField] private SequenceBubbleUI bubble;

    private GameConfig config;
    private float frozenUntil;
    private Vector2 waitPosition;
    private Vector2 entryFrom;
    private float entryDelay;
    private float entryElapsed;
    private int directionIndex = -1;
    private bool showingBound;
    private Coroutine bounceRoutine;

    /// <summary>True when this vessel has a full set of halo drawings for its linked state.</summary>
    private bool HasBoundArt => Type != null && Type.boundSprites != null && Type.boundSprites.Length >= 8 && Type.boundSprites[0] != null;

    /// <summary>The vessel class, and with it speed, turn rate and radius.</summary>
    public ShipType Type { get; private set; }

    /// <summary>The sequence this ship answers to.</summary>
    public Signal[] Sequence { get; private set; }

    /// <summary>Which berth this ship is bound for.</summary>
    public int DockIndex { get; private set; }

    /// <summary>The two sentences shown when this ship arrives.</summary>
    public string Note { get; private set; }

    /// <summary>Where the ship is in its night.</summary>
    public ShipState State { get; private set; } = ShipState.Idle;

    /// <summary>Which way the ship is pointing, as a unit vector.</summary>
    public Vector2 Heading { get; private set; } = Vector2.left;

    /// <summary>Position in world units.</summary>
    public Vector2 Position => transform.position;

    /// <summary>Collision circle radius, from the ship's type.</summary>
    public float Radius => Type != null ? Type.collisionRadius : 0.2f;

    /// <summary>True while the ship is recovering from a collision and ignores steering.</summary>
    public bool IsFrozen => Time.time < frozenUntil;

    /// <summary>The emitter that plays this ship's sequence.</summary>
    public ShipSignalEmitter Emitter => emitter;

    /// <summary>
    /// Puts the ship on the water: type, sequence, berth and note, then sends
    /// it in from open sea towards the spot it will wait at.
    ///
    /// Ships sail in rather than appearing. A night that opens with four boats
    /// blinking into existence is a level loading; one that opens with them
    /// coming over the horizon is a night, and the few seconds it takes are the
    /// player's chance to see how many are out there before any of them starts
    /// asking.
    /// </summary>
    public void Init(ShipSpawn spawn, GameConfig gameConfig, bool showSequenceAlways, float entryDelay)
    {
        config = gameConfig;
        Type = spawn.type;
        Sequence = spawn.sequence;
        DockIndex = spawn.dockIndex;
        Note = spawn.note;

        waitPosition = spawn.position;
        entryFrom = OffScreenApproach(waitPosition);
        this.entryDelay = entryDelay;
        entryElapsed = 0f;

        transform.position = new Vector3(entryFrom.x, entryFrom.y, 0f);

        Heading = (waitPosition - entryFrom).sqrMagnitude > Mathf.Epsilon
            ? (waitPosition - entryFrom).normalized
            : Vector2.left;

        directionIndex = -1;

        if (emitter != null)
        {
            emitter.Init(this, gameConfig, bubble, lamp, showSequenceAlways);
        }

        State = ShipState.Arriving;
        ApplyStateVisuals();

        if (wake != null)
        {
            wake.enabled = true;
        }
    }

    /// <summary>
    /// One frame of sailing in. Returns false once the ship has taken up its
    /// station and gone idle.
    ///
    /// Driven by a speed rather than a duration. With a fixed duration a ship
    /// starting further out simply moved faster to arrive at the same moment,
    /// which is exactly the thing that reads as wrong — boats on the same water
    /// should travel at the same pace.
    ///
    /// It is not the ship's own steering speed either: a freighter crossing
    /// nine units at 0.45 a second would take twenty seconds, and nobody wants
    /// to watch that before the night starts.
    /// </summary>
    public bool TickEntry(float deltaTime)
    {
        if (State != ShipState.Arriving)
        {
            return false;
        }

        entryElapsed += deltaTime;

        if (entryElapsed < entryDelay)
        {
            return true;
        }

        Vector2 toStation = waitPosition - Position;
        float remaining = toStation.magnitude;

        float speed = config != null ? config.entrySpeed : 1.1f;
        float slowRadius = config != null ? Mathf.Max(0.01f, config.entrySlowRadius) : 1.5f;

        // Steady all the way in, then easing off over the last stretch, so the
        // ship settles onto its station instead of stopping dead.
        speed *= Mathf.Clamp(remaining / slowRadius, 0.2f, 1f);

        float step = speed * deltaTime;

        if (remaining <= step || remaining <= 0.01f)
        {
            transform.position = new Vector3(waitPosition.x, waitPosition.y, 0f);
            SetIdle();
            return false;
        }

        Heading = toStation / remaining;
        transform.position += (Vector3)(Heading * step);
        UpdateDirectionSprite();
        return true;
    }

    /// <summary>
    /// Where a ship bound for <paramref name="station"/> comes onto the screen:
    /// straight out from the middle of the harbour, past the edge. Ships then
    /// arrive out of the open sea rather than over the town.
    /// </summary>
    private static Vector2 OffScreenApproach(Vector2 station)
    {
        // Just past the visible edge — 9.6 by 5.4 — and no further. Every extra
        // unit out here is another second of watching an empty sea before the
        // night starts.
        const float edgeX = 10.6f;
        const float edgeY = 6.2f;

        Vector2 direction = station.sqrMagnitude > Mathf.Epsilon ? station.normalized : Vector2.right;

        float toX = Mathf.Abs(direction.x) > 0.001f
            ? (Mathf.Sign(direction.x) * edgeX - station.x) / direction.x
            : float.MaxValue;

        float toY = Mathf.Abs(direction.y) > 0.001f
            ? (Mathf.Sign(direction.y) * edgeY - station.y) / direction.y
            : float.MaxValue;

        return station + direction * Mathf.Min(toX, toY);
    }

    /// <summary>Drops the ship back to waiting. Its sequence starts playing again from where it was.</summary>
    public void SetIdle()
    {
        State = ShipState.Idle;
        ApplyStateVisuals();

        if (emitter != null)
        {
            emitter.Play();
        }
    }

    /// <summary>Takes hold of the ship. It follows the cursor from here until it berths or is dropped.</summary>
    public void SetLinked()
    {
        State = ShipState.Linked;
        ApplyStateVisuals();

        if (emitter != null)
        {
            emitter.Stop(lampLit: true);
        }
    }

    /// <summary>Berths the ship: it slides the last stretch into the pier and leaves the simulation.</summary>
    public void SetDocked(Dock dock)
    {
        State = ShipState.Docked;
        ApplyStateVisuals();

        if (emitter != null)
        {
            emitter.Stop(lampLit: false);
        }

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
            bounceRoutine = null;
        }

        if (dock != null && isActiveAndEnabled)
        {
            StartCoroutine(SlideInto(dock.Position));
        }
    }

    /// <summary>
    /// One frame of steering, for the linked ship only. Turns towards the cursor
    /// at the ship's own rate, then swims forward — and stops short of the
    /// cursor rather than sitting under it, so the player can still see it.
    /// </summary>
    public void Tick(float deltaTime, Vector2 cursor)
    {
        if (State != ShipState.Linked || Type == null || IsFrozen)
        {
            return;
        }

        Vector2 toCursor = cursor - Position;
        float distance = toCursor.magnitude;
        float stopRadius = config != null ? config.stopRadius : 0.1f;

        if (distance > Mathf.Epsilon)
        {
            float current = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg;
            float wanted = Mathf.Atan2(toCursor.y, toCursor.x) * Mathf.Rad2Deg;
            float turned = Mathf.MoveTowardsAngle(current, wanted, Type.turnRate * deltaTime) * Mathf.Deg2Rad;
            Heading = new Vector2(Mathf.Cos(turned), Mathf.Sin(turned));
        }

        // Clamped by the remaining distance as well as by speed, so a ship
        // arriving at the cursor settles instead of orbiting it.
        float step = Mathf.Min(Type.speed * deltaTime, Mathf.Max(0f, distance - stopRadius));
        if (step > 0f)
        {
            transform.position += (Vector3)(Heading * step);
        }

        UpdateDirectionSprite();

        if (wake != null)
        {
            wake.enabled = step > 0f;
        }
    }

    /// <summary>
    /// Knocks the ship back along <paramref name="normal"/> and freezes it
    /// briefly. The freeze is what stops two ships from grinding into each
    /// other: while it lasts, the ship neither steers nor collides again.
    /// </summary>
    public void Bounce(Vector2 normal)
    {
        if (config == null)
        {
            return;
        }

        frozenUntil = Time.time + config.bounceFreeze;

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
        }

        bounceRoutine = StartCoroutine(BounceRoutine(normal.normalized * config.bounceDistance, config.bounceDuration));
    }

    /// <summary>
    /// Shoves the ship a little, without freezing it. Used on the ship that was
    /// hit, so a collision separates both hulls but only the steered one loses
    /// control of itself.
    /// </summary>
    public void Nudge(Vector2 normal, float distance)
    {
        transform.position += (Vector3)(normal.normalized * distance);
    }

    private IEnumerator BounceRoutine(Vector2 offset, float duration)
    {
        Vector3 from = transform.position;
        Vector3 to = from + (Vector3)offset;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.position = to;
        bounceRoutine = null;
    }

    private IEnumerator SlideInto(Vector2 berth)
    {
        float duration = config != null ? config.dockSlideTime : 1f;
        Vector3 from = transform.position;
        Vector3 to = new Vector3(berth.x, berth.y, 0f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(from, to, t);

            Vector2 toBerth = to - transform.position;
            if (toBerth.sqrMagnitude > Mathf.Epsilon)
            {
                Heading = toBerth.normalized;
                UpdateDirectionSprite();
            }

            yield return null;
        }

        transform.position = to;

        if (wake != null)
        {
            wake.enabled = false;
        }
    }

    /// <summary>Shows or hides the targeting ring. Driven every frame by the night controller.</summary>
    public void SetTargeted(bool targeted)
    {
        if (targetRing != null)
        {
            targetRing.enabled = targeted && State == ShipState.Idle;
        }

        // Aiming at a ship is how you ask what it wants: its code comes up
        // while the beam is on it, whatever the night's rule about bubbles.
        if (bubble != null)
        {
            bubble.SetTargeted(targeted && State == ShipState.Idle);
        }
    }

    /// <summary>
    /// Picks the heading sprite. Index 0 is East and the eight run
    /// counter-clockwise, matching the table in the technical document, §7.
    /// The sprite is only assigned when the index actually changes, so a ship
    /// turning slowly is not reassigning a sprite sixty times a second.
    /// </summary>
    private void UpdateDirectionSprite()
    {
        if (body == null || Type == null || Type.directionSprites == null || Type.directionSprites.Length < 8)
        {
            return;
        }

        float angle = Mathf.Atan2(Heading.y, Heading.x) * Mathf.Rad2Deg;
        int index = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;

        // A linked ship is drawn with the halo the artist put around the hull,
        // so the swap has to happen on a state change as well as on a turn.
        bool wantBound = State == ShipState.Linked && HasBoundArt;

        if (index == directionIndex && wantBound == showingBound)
        {
            return;
        }

        directionIndex = index;
        showingBound = wantBound;
        body.sprite = wantBound ? Type.boundSprites[index] : Type.directionSprites[index];

        if (wake != null && Type.wakeSprites != null && Type.wakeSprites.Length > index)
        {
            wake.sprite = Type.wakeSprites[index];
        }
    }

    private void ApplyStateVisuals()
    {
        // The halo is drawn into the bound hulls, so the stand-in glow is only
        // needed for vessels that have no bound art.
        if (linkedHalo != null)
        {
            linkedHalo.enabled = State == ShipState.Linked && !HasBoundArt;
        }

        UpdateDirectionSprite();

        if (targetRing != null && State != ShipState.Idle)
        {
            targetRing.enabled = false;
        }

        if (wake != null && State != ShipState.Linked)
        {
            wake.enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
