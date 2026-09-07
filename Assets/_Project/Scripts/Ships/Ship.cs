using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a ship is in its life on this night.</summary>
public enum ShipState
{
    /// <summary>Sailing in toward the lighthouse.</summary>
    Approaching,

    /// <summary>Close enough to be showing its signal, waiting for the player to echo it.</summary>
    Signaling,

    /// <summary>Matched. The beam is now its course.</summary>
    Bound,

    /// <summary>Making its final run into the harbour.</summary>
    Docking,

    /// <summary>Tied up. Done.</summary>
    Arrived
}

/// <summary>
/// A ship: approaches, signals, and — once the player echoes its signal — takes
/// the beam as its heading and docks.
/// The art is fixed-perspective, drawn once per heading angle, so this never
/// touches transform.rotation; it swaps sprites instead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Ship : MonoBehaviour
{
    /// <summary>One piece of ship art, drawn for a particular heading.</summary>
    [Serializable]
    public struct HeadingSprite
    {
        [Tooltip("Heading this sprite is drawn for, in degrees. 0 = +X, counter-clockwise.")]
        public float angle;
        public Sprite sprite;
    }

    [Header("Identity")]
    [SerializeField] private ShipType shipType = ShipType.SmallBoat;

    [Tooltip("The sequence the player must echo back to bind this ship.")]
    [SerializeField] private Signal signal = new Signal();

    [Header("Targets")]
    [Tooltip("What the ship sails toward until it starts signalling.")]
    [SerializeField] private Transform lighthouse;
    [Tooltip("Where the ship docks once bound.")]
    [SerializeField] private Transform targetHarbor;

    [Header("Speeds")]
    [SerializeField] private float approachSpeed = 1f;

    [Tooltip("Forward speed while bound. Keep this low — the player has to be able to steer it.")]
    [SerializeField] private float boundSpeed = 0.8f;

    [SerializeField] private float dockingSpeed = 1.5f;

    [Tooltip("Degrees per second while approaching and docking.")]
    [SerializeField] private float turnSpeed = 90f;

    [Tooltip("Degrees per second while bound. High, so the ship answers the beam promptly. Small boats turn fast, cargo ships slowly.")]
    [SerializeField] private float boundTurnSpeed = 270f;

    [Header("Distances")]
    [Tooltip("Distance to the lighthouse at which the ship starts signalling.")]
    [SerializeField] private float signalRange = 6f;
    [Tooltip("Distance to the harbour at which a bound ship begins its docking run.")]
    [SerializeField] private float dockingRange = 3f;
    [Tooltip("How close to the harbour counts as arrived.")]
    [SerializeField] private float arrivalDistance = 0.3f;

    [Tooltip("Seconds a ship stays bound before it loosens and can be signalled again. 0 keeps it bound forever, as the design doc intends.")]
    [SerializeField] private float boundTimeout = 15f;

    [Header("Art")]
    [SerializeField] private List<HeadingSprite> normalSprites = new List<HeadingSprite>();
    [SerializeField] private List<HeadingSprite> boundSprites = new List<HeadingSprite>();
    [Tooltip("Reuse a sprite mirrored across the vertical axis when it is the closer match.")]
    [SerializeField] private bool allowMirroring = true;

    [Header("Heading")]
    [SerializeField] private float heading;

    [Header("Debug")]
    [Tooltip("Log state changes, such as binding.")]
    [SerializeField] private bool logState = true;

    [Tooltip("Log the heading while bound. Throttled, but still chatty.")]
    [SerializeField] private bool logBoundHeading = true;

    [SerializeField] private float boundLogInterval = 0.5f;

    private SpriteRenderer spriteRenderer;
    private float? targetHeading;
    private float nextBoundLogTime;
    private float boundElapsed;

    /// <summary>Raised once, when the ship reaches its harbour.</summary>
    public event Action<Ship> OnArrived;

    /// <summary>Raised when the ship binds, for glow or flash feedback.</summary>
    public event Action<Ship> OnBound;

    /// <summary>Raised when the ship shows its signal again after a wrong answer.</summary>
    public event Action<Ship> OnSignalReplayed;

    /// <summary>
    /// The sequence the player has to echo to bind this ship. Set in the
    /// Inspector for test ships, or by the spawner from the night's config.
    /// </summary>
    public Signal Signal
    {
        get => signal;
        set => signal = value;
    }

    /// <summary>Class of vessel. Set by the spawner from the night's config.</summary>
    public ShipType Type
    {
        get => shipType;
        set => shipType = value;
    }

    /// <summary>Current state. Changes only through this component.</summary>
    public ShipState State { get; private set; } = ShipState.Approaching;

    /// <summary>Current heading in degrees, 0 = +X, counter-clockwise.</summary>
    public float Heading => heading;

    /// <summary>Speed while approaching, signalling and bound. Tuned per night.</summary>
    public float ApproachSpeed
    {
        get => approachSpeed;
        set => approachSpeed = value;
    }

    /// <summary>Forward speed while bound. Tuned per night.</summary>
    public float BoundSpeed
    {
        get => boundSpeed;
        set => boundSpeed = value;
    }

    /// <summary>Turn rate while bound, in degrees per second. The main per-type feel knob.</summary>
    public float BoundTurnSpeed
    {
        get => boundTurnSpeed;
        set => boundTurnSpeed = value;
    }

    /// <summary>Speed on the final run into the harbour. Tuned per night.</summary>
    public float DockingSpeed
    {
        get => dockingSpeed;
        set => dockingSpeed = value;
    }

    /// <summary>Turn rate in degrees per second. Tuned per night, per ship type.</summary>
    public float TurnSpeed
    {
        get => turnSpeed;
        set => turnSpeed = value;
    }

    /// <summary>The harbour this ship is bound for.</summary>
    public Transform TargetHarbor
    {
        get => targetHarbor;
        set => targetHarbor = value;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        switch (State)
        {
            case ShipState.Approaching:
                UpdateApproaching();
                break;

            case ShipState.Signaling:
                UpdateSignaling();
                break;

            case ShipState.Bound:
                UpdateBound();
                break;

            case ShipState.Docking:
                UpdateDocking();
                break;

            case ShipState.Arrived:
                break;
        }

        ApplyHeadingSprite();
    }

    /// <summary>
    /// Locks the ship to the beam. Called by MatchResolver when the player's
    /// bar matches this ship's signal. Ignored once the ship is already bound.
    /// </summary>
    public void Bind()
    {
        if (logState)
        {
            Debug.Log("Ship.Bind() called - changing state to Bound", this);
        }

        if (State == ShipState.Bound || State == ShipState.Docking || State == ShipState.Arrived)
        {
            if (logState)
            {
                Debug.Log($"Bind() ignored: already past binding, state is {State}", this);
            }

            return;
        }

        if (logState)
        {
            Debug.Log($"Current state: {State}", this);
        }

        EnterState(ShipState.Bound);

        if (logState)
        {
            Debug.Log($"New state: {State}", this);
        }

        OnBound?.Invoke(this);
    }

    /// <summary>
    /// Shows the signal again after a wrong answer. Only meaningful while the
    /// ship is still waiting to be answered.
    /// </summary>
    public void ReplaySignal()
    {
        if (State != ShipState.Signaling)
        {
            return;
        }

        OnSignalReplayed?.Invoke(this);
    }

    /// <summary>
    /// Steers the ship toward a heading in degrees. While bound, the beam calls
    /// this to hand the ship its course; the ship holds the last heading given,
    /// so it stays on course after the beam moves off it.
    /// </summary>
    public void SetTargetHeading(float degrees)
    {
        targetHeading = degrees;
    }

    /// <summary>Begins the docking run. Also entered automatically near the harbour.</summary>
    public void StartDocking()
    {
        if (State == ShipState.Bound)
        {
            EnterState(ShipState.Docking);
        }
    }

    /// <summary>Sails toward the lighthouse until close enough to signal.</summary>
    private void UpdateApproaching()
    {
        if (lighthouse != null)
        {
            SteerToward(lighthouse.position);
        }

        TurnAndMove(approachSpeed, turnSpeed);

        if (lighthouse != null && WithinDistance(lighthouse.position, signalRange))
        {
            EnterState(ShipState.Signaling);
        }
    }

    /// <summary>Holds course while waiting for the player to answer.</summary>
    private void UpdateSignaling()
    {
        TurnAndMove(approachSpeed, turnSpeed);
    }

    /// <summary>Follows whatever heading the beam last gave it.</summary>
    private void UpdateBound()
    {
        boundElapsed += Time.deltaTime;

        // Letting go after a while means the same ship can be signalled again.
        // Note this contradicts the design doc, which keeps a bound ship bound
        // for good — set boundTimeout to 0 to restore that.
        if (boundTimeout > 0f && boundElapsed >= boundTimeout)
        {
            if (logState)
            {
                Debug.Log($"Ship {name}: bound for {boundElapsed:F1}s, loosening — can be signalled again", this);
            }

            EnterState(ShipState.Signaling);
            return;
        }

        if (logBoundHeading && Time.time >= nextBoundLogTime)
        {
            nextBoundLogTime = Time.time + boundLogInterval;

            string steer = targetHeading.HasValue
                ? $"{targetHeading.Value:F1}"
                : "none — not lit by the beam, holding course";
            Debug.Log($"Bound: heading={heading:F1}°, target={steer}, turn={boundTurnSpeed}°/s, forward={boundSpeed} u/s", this);
        }

        TurnAndMove(boundSpeed, boundTurnSpeed);

        if (targetHarbor != null && WithinDistance(targetHarbor.position, dockingRange))
        {
            EnterState(ShipState.Docking);
        }
    }

    /// <summary>Runs in to the dock under its own power.</summary>
    private void UpdateDocking()
    {
        if (targetHarbor == null)
        {
            return;
        }

        SteerToward(targetHarbor.position);
        TurnAndMove(dockingSpeed, turnSpeed);

        if (WithinDistance(targetHarbor.position, arrivalDistance))
        {
            EnterState(ShipState.Arrived);
        }
    }

    private void EnterState(ShipState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;

        if (next == ShipState.Bound)
        {
            boundElapsed = 0f;
        }

        if (next == ShipState.Arrived)
        {
            OnArrived?.Invoke(this);
        }
    }

    /// <summary>Points the target heading at a world position.</summary>
    private void SteerToward(Vector3 worldPosition)
    {
        Vector2 toTarget = worldPosition - transform.position;
        if (toTarget.sqrMagnitude > Mathf.Epsilon)
        {
            targetHeading = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        }
    }

    /// <summary>Turns toward the target heading, then moves along the current one.</summary>
    private void TurnAndMove(float speed, float turnRate)
    {
        if (targetHeading.HasValue)
        {
            heading = Mathf.MoveTowardsAngle(heading, targetHeading.Value, turnRate * Time.deltaTime);
        }

        float radians = heading * Mathf.Deg2Rad;
        Vector3 forward = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        transform.Translate(forward * (speed * Time.deltaTime), Space.World);
    }

    private bool WithinDistance(Vector3 worldPosition, float distance)
    {
        return ((Vector2)(worldPosition - transform.position)).sqrMagnitude <= distance * distance;
    }

    /// <summary>
    /// Picks the entry closest to the current heading. A mirrored entry only
    /// wins when it is a strictly better match than every unmirrored one.
    /// </summary>
    private void ApplyHeadingSprite()
    {
        List<HeadingSprite> entries = UsesBoundArt ? boundSprites : normalSprites;
        if (entries == null)
        {
            return;
        }

        Sprite best = null;
        float bestError = float.MaxValue;
        bool bestFlipped = false;

        foreach (HeadingSprite entry in entries)
        {
            if (entry.sprite == null)
            {
                continue;
            }

            float error = Mathf.Abs(Mathf.DeltaAngle(entry.angle, heading));
            if (error < bestError)
            {
                best = entry.sprite;
                bestError = error;
                bestFlipped = false;
            }

            if (!allowMirroring)
            {
                continue;
            }

            // Flipped, the sprite drawn for `angle` reads as heading 180 - angle.
            float mirroredError = Mathf.Abs(Mathf.DeltaAngle(180f - entry.angle, heading));
            if (mirroredError < bestError)
            {
                best = entry.sprite;
                bestError = mirroredError;
                bestFlipped = true;
            }
        }

        if (best == null)
        {
            return;
        }

        spriteRenderer.sprite = best;
        spriteRenderer.flipX = bestFlipped;
    }

    /// <summary>True once the ship is under the beam's command.</summary>
    private bool UsesBoundArt =>
        State == ShipState.Bound || State == ShipState.Docking || State == ShipState.Arrived;
}
