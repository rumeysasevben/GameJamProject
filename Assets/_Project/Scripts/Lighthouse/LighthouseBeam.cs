using UnityEngine;

/// <summary>
/// Rotates the beam to follow the mouse and reports which ship is lit.
/// Attached to the Beam GameObject.
/// </summary>
public class LighthouseBeam : MonoBehaviour
{
    [SerializeField] private float turnSpeed = 360f;

    [Tooltip("Full width of the beam cone in degrees. Keep this generous — this is a memory game, not an aiming game.")]
    [SerializeField] private float coneAngle = 45f;

    [Tooltip("Cursor distance from the pivot below which the aim is held. Near the pivot, tiny mouse moves swing the angle wildly.")]
    [SerializeField] private float minAimDistance = 1f;

    [Tooltip("Degrees to add when drawing. 0 if the beam art extends along +X, -90 if it extends along +Y. Aiming and targeting are unaffected.")]
    [SerializeField] private float spriteAngleOffset = -90f;

    [Header("Debug")]
    [Tooltip("Log the cursor position and beam angle every frame. Off by default — per-frame logging stutters the editor.")]
    [SerializeField] private bool logAngle;

    [Tooltip("Log the beam angle and lit ship. Throttled, so it stays readable.")]
    [SerializeField] private bool logTargeting = true;

    [SerializeField] private float targetingLogInterval = 0.5f;

    private Camera cam;
    private float aimAngle;
    private float targetAngle;
    private float nextTargetingLogTime;

    /// <summary>
    /// The ship currently lit by the beam, or null when the cone is empty.
    /// When several ships are inside the cone, the one nearest its centre wins.
    /// </summary>
    public Ship CurrentTarget { get; private set; }

    /// <summary>
    /// Where the beam actually points, in degrees, 0 = +X counter-clockwise.
    /// This is the aim, not the transform's rotation — the two differ by
    /// <see cref="spriteAngleOffset"/> whenever the art is not drawn along +X.
    /// </summary>
    public float CurrentBeamAngle => aimAngle;

    private void Awake()
    {
        cam = Camera.main;
        aimAngle = transform.eulerAngles.z - spriteAngleOffset;
        targetAngle = aimAngle;
    }

    private void Update()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }
        }

        // Screen space -> world space. For an orthographic camera the x and y
        // of the result are correct regardless of the z handed in.
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mouseWorld - transform.position;

        // Close to the pivot the angle to the cursor changes enormously for a
        // pixel of movement, and crossing the pivot flips it by 180 degrees.
        // Hold the last aim through that dead zone instead of whipping around.
        if (direction.sqrMagnitude >= minAimDistance * minAimDistance)
        {
            targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        aimAngle = Mathf.MoveTowardsAngle(aimAngle, targetAngle, turnSpeed * Time.deltaTime);

        // The aim is the truth; the offset only compensates for how the art is
        // drawn, so targeting never has to know about the sprite's orientation.
        // This rotates the Beam GameObject itself — never the Lighthouse parent.
        transform.rotation = Quaternion.AngleAxis(aimAngle + spriteAngleOffset, Vector3.forward);

        if (logAngle)
        {
            Debug.Log($"Rotating Beam to angle: {aimAngle + spriteAngleOffset:F1}° (aim {aimAngle:F1}°, offset {spriteAngleOffset})", this);
            Debug.Log($"Mouse screen: {Input.mousePosition} world: {(Vector2)mouseWorld} | aim: {aimAngle:F1} target: {targetAngle:F1}", this);
        }

        CurrentTarget = FindTarget(aimAngle);
        SteerBoundTarget();

        if (logTargeting && Time.time >= nextTargetingLogTime)
        {
            nextTargetingLogTime = Time.time + targetingLogInterval;
            Debug.Log($"LighthouseBeam: CurrentBeamAngle = {aimAngle:F1}, CurrentTarget = {(CurrentTarget != null ? CurrentTarget.name : "none")}", this);
        }
    }

    /// <summary>
    /// Hands the beam's angle to the lit ship, but only once that ship is bound.
    /// A bound ship that leaves the cone simply stops being given new headings
    /// and holds its last course, which is what makes steering two ships at once
    /// possible.
    /// </summary>
    private void SteerBoundTarget()
    {
        if (CurrentTarget != null && CurrentTarget.State == ShipState.Bound)
        {
            CurrentTarget.SetTargetHeading(aimAngle);
        }
    }

    /// <summary>
    /// Picks the ship closest to the centre of the cone. Pure angle maths, no
    /// physics: a ship is lit when it sits within half the cone width of the
    /// beam's aim.
    /// </summary>
    private Ship FindTarget(float beamAngle)
    {
        Ship[] ships = FindObjectsByType<Ship>(FindObjectsSortMode.None);

        Ship best = null;
        float bestOffset = coneAngle * 0.5f;

        foreach (Ship ship in ships)
        {
            if (ship.State == ShipState.Arrived)
            {
                continue;
            }

            Vector2 toShip = ship.transform.position - transform.position;
            if (toShip.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            float shipAngle = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg;
            float offset = Mathf.Abs(Mathf.DeltaAngle(beamAngle, shipAngle));

            if (offset <= bestOffset)
            {
                best = ship;
                bestOffset = offset;
            }
        }

        return best;
    }
}
