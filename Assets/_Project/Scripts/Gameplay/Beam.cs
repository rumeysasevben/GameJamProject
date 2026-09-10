using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The lighthouse cone: both what the player sees and what the game asks
/// questions of.
///
/// Sits at the lamp point as a child of the lighthouse, and only ever has its
/// rotation written — the cone always emanates from the tower. Its local +X is
/// the centre line, so <see cref="Contains"/> is a single angle comparison and
/// the art needs no knowledge of the maths.
///
/// The visual is two layers over the same cone: a vertex-coloured fan mesh that
/// fades to nothing at the rim, and a Light2D that actually lifts the ships out
/// of the dark. Keeping the query on the same transform as both means what the
/// player sees lit is exactly what the game considers targeted.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class Beam : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Shared tuning asset; supplies the cone's angle and reach.")]
    [SerializeField] private GameConfig config;

    [Header("Visual")]
    [Tooltip("The cone's light. Its intensity is what a flash actually pulses.")]
    [SerializeField] private Light2D beamLight;

    [Tooltip("The drawn cone. When set, this is used instead of the generated fan mesh and is stretched to match the angle and reach below.")]
    [SerializeField] private SpriteRenderer coneSprite;

    [Tooltip("Where the drawn cone's visible edge sits, as a fraction of the sprite's half-height. The art fades out before the canvas edge, so this is well under 1.")]
    [SerializeField, Range(0.1f, 1f)] private float coneSpriteEdge = 0.5f;

    [Tooltip("Segments across the fan mesh. Twelve to sixteen is smooth enough at this size. Only used when no cone sprite is set.")]
    [SerializeField] private int segments = 14;

    [Tooltip("Alpha at the apex. The rim is always fully transparent, which is what softens the edge.")]
    [SerializeField, Range(0f, 1f)] private float coreAlpha = 0.5f;

    [Tooltip("How much brighter a flash is than the resting beam.")]
    [SerializeField] private float flashMultiplier = 2f;

    [Tooltip("Seconds a flash takes to fade back to resting brightness.")]
    [SerializeField] private float flashFade = 0.15f;

    private MeshFilter meshFilter;
    private Mesh coneMesh;
    private float baseIntensity;
    private Color baseConeColor = Color.white;
    private Coroutine flashRoutine;

    [Header("Tip marker")]
    [Tooltip("The small mark at the end of the beam. It sits where the cursor is, which is where a linked ship is heading.")]
    [SerializeField] private SpriteRenderer tipMarker;

    [Tooltip("How much the marker breathes, as a fraction of its size.")]
    [SerializeField, Range(0f, 0.5f)] private float markerPulse = 0.12f;

    [Tooltip("Breaths per second.")]
    [SerializeField] private float markerPulseSpeed = 1.6f;

    private Vector3 markerBaseScale = Vector3.one;
    private Color markerBaseColor = Color.white;

    /// <summary>How far the beam currently reaches, or below zero before it has first been aimed.</summary>
    private float reach = -1f;

    /// <summary>Centre line of the cone in world space.</summary>
    public Vector2 Direction => transform.right;

    /// <summary>Where the cone starts — the lamp.</summary>
    public Vector2 Origin => transform.position;

    /// <summary>Half the cone's opening angle, in degrees, after the player's beam width setting.</summary>
    public float HalfAngle => (config != null ? config.beamHalfAngle : 12f) * GameSettings.BeamWidthMultiplier;

    /// <summary>
    /// How far the cone reaches right now, in world units: out to the cursor,
    /// held between the minimum and maximum reach.
    /// </summary>
    public float Length => reach >= 0f ? reach : MaxLength;

    private float MaxLength => config != null ? config.beamLength : 15f;
    private float MinLength => config != null ? config.beamMinLength : 1.5f;
    private float ReachMargin => config != null ? config.beamReachMargin : 0.6f;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        baseIntensity = beamLight != null ? beamLight.intensity : 0f;
        baseConeColor = coneSprite != null ? coneSprite.color : Color.white;

        if (tipMarker != null)
        {
            markerBaseScale = tipMarker.transform.localScale;
            markerBaseColor = tipMarker.color;
        }

        RebuildMesh();
        PlaceMarker();
    }

    // The width slider on the pause panel reshapes the cone while it is dragged.
    private void OnEnable() => GameSettings.Changed += RebuildMesh;

    private void OnDisable() => GameSettings.Changed -= RebuildMesh;

    /// <summary>Supplies the tuning asset when the beam is wired up in code rather than the Inspector.</summary>
    public void Configure(GameConfig gameConfig)
    {
        config = gameConfig;
        RebuildMesh();
    }

    /// <summary>
    /// Points the cone along <paramref name="direction"/>. A zero direction —
    /// the cursor exactly on the lamp — leaves the last angle alone rather than
    /// snapping the beam to +X.
    /// </summary>
    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// Sets how far the beam reaches — the distance from the lamp to the
    /// cursor. The cone is refitted and the marker moved to the new tip.
    /// </summary>
    public void SetReach(float distance)
    {
        float clamped = Mathf.Clamp(distance, MinLength, MaxLength);

        if (Mathf.Abs(clamped - reach) < 0.001f)
        {
            return;
        }

        reach = clamped;
        RebuildMesh();
        PlaceMarker();
    }

    private void PlaceMarker()
    {
        if (tipMarker != null)
        {
            tipMarker.transform.localPosition = new Vector3(Length, 0f, 0f);
        }
    }

    private void Update()
    {
        if (tipMarker == null)
        {
            return;
        }

        // The marker belongs to the light: it goes where the cone goes, fades
        // when the cone fades at dawn, and pops a little when the cone flashes.
        bool coneShowing = coneSprite == null || coneSprite.gameObject.activeInHierarchy;
        tipMarker.enabled = coneShowing;

        if (!coneShowing)
        {
            return;
        }

        float coneFactor = coneSprite != null && baseConeColor.a > 0.001f
            ? coneSprite.color.a / baseConeColor.a
            : 1f;

        float breath = 1f + Mathf.Sin(Time.time * markerPulseSpeed * Mathf.PI * 2f) * markerPulse;
        float pop = Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(coneFactor - 1f));
        tipMarker.transform.localScale = markerBaseScale * breath * pop;

        tipMarker.color = new Color(
            markerBaseColor.r,
            markerBaseColor.g,
            markerBaseColor.b,
            markerBaseColor.a * Mathf.Clamp01(coneFactor));
    }

    /// <summary>True when <paramref name="point"/> lies inside the cone.</summary>
    public bool Contains(Vector2 point)
    {
        Vector2 offset = point - Origin;
        float distance = offset.magnitude;

        // The beam only reaches as far as it is drawn, plus a little: a ship
        // sitting under the cursor has its centre just past the tip.
        if (distance > Length + ReachMargin)
        {
            return false;
        }

        // A point sitting on the lamp itself has no direction to compare; count
        // it as inside rather than dividing by nothing.
        if (distance <= Mathf.Epsilon)
        {
            return true;
        }

        return Vector2.Angle(Direction, offset) <= HalfAngle;
    }

    /// <summary>
    /// The ship the player is aiming at: the nearest idle ship inside the cone,
    /// or null if there is none. Nearest rather than most-centred, because with
    /// two ships in the beam the player reads the closer one as the one being
    /// addressed.
    /// </summary>
    public Ship GetTargetShip(IReadOnlyList<Ship> ships)
    {
        if (ships == null)
        {
            return null;
        }

        Ship best = null;
        float bestDistance = float.MaxValue;
        Vector2 origin = Origin;

        for (int i = 0; i < ships.Count; i++)
        {
            Ship ship = ships[i];
            if (ship == null || ship.State != ShipState.Idle)
            {
                continue;
            }

            if (!Contains(ship.Position))
            {
                continue;
            }

            float distance = ((Vector2)ship.Position - origin).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = ship;
            }
        }

        return best;
    }

    /// <summary>
    /// Pulses the beam for one symbol: short or long, then a quick fade back.
    /// A new flash cuts the previous one off, so fast input still reads as
    /// separate pulses rather than one long smear.
    /// </summary>
    public void Flash(Signal symbol)
    {
        if (beamLight == null && coneSprite == null)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        float hold = config == null
            ? 0.2f
            : (symbol == Signal.Short ? config.flashShort : config.flashLong);

        flashRoutine = StartCoroutine(FlashRoutine(hold));
    }

    /// <summary>
    /// One pulse. The drawn cone is what flashes now — it is the only beam —
    /// going to full opacity and a hotter yellow, then easing back. A Light2D,
    /// if one is still wired, pulses along with it.
    /// </summary>
    private IEnumerator FlashRoutine(float hold)
    {
        // Full opacity and a little deeper in colour: the brightest a sprite
        // can go without HDR, which the WebGL build does not have.
        // Brighter by opacity alone, so a flash reads as the same light getting
        // stronger rather than turning a deeper yellow.
        Color peakCone = new Color(baseConeColor.r, baseConeColor.g, baseConeColor.b, Mathf.Min(1f, baseConeColor.a * 2f));

        SetFlash(1f, peakCone);
        yield return new WaitForSeconds(hold);

        float elapsed = 0f;
        while (elapsed < flashFade)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flashFade);
            SetFlash(1f - t, Color.Lerp(peakCone, baseConeColor, t));
            yield return null;
        }

        SetFlash(0f, baseConeColor);
        flashRoutine = null;
    }

    /// <summary><paramref name="amount"/> is how far into the flash the light is, 0 resting to 1 peak.</summary>
    private void SetFlash(float amount, Color coneColor)
    {
        if (beamLight != null)
        {
            beamLight.intensity = Mathf.Lerp(baseIntensity, baseIntensity * flashMultiplier, amount);
        }

        if (coneSprite != null)
        {
            coneSprite.color = coneColor;
        }
    }

    /// <summary>
    /// Rebuilds the fan. The apex carries <see cref="coreAlpha"/> and every rim
    /// vertex is fully transparent, so the soft edge comes out of vertex colour
    /// and costs no shader work.
    /// </summary>
    public void RebuildMesh()
    {
        // The drawn cone wins when there is one: it is stretched to whatever
        // angle and reach the config asks for, so retuning beamHalfAngle still
        // changes what the player sees and not only what the game tests.
        if (FitConeSprite())
        {
            return;
        }

        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }
        }

        int steps = Mathf.Max(3, segments);
        float half = HalfAngle;
        float length = Length;

        var vertices = new Vector3[steps + 2];
        var colors = new Color[steps + 2];
        var triangles = new int[steps * 3];

        vertices[0] = Vector3.zero;
        colors[0] = new Color(1f, 1f, 1f, coreAlpha);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float angle = Mathf.Lerp(-half, half, t) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * length;
            colors[i + 1] = new Color(1f, 1f, 1f, 0f);
        }

        for (int i = 0; i < steps; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        if (coneMesh == null)
        {
            coneMesh = new Mesh { name = "BeamCone" };
            coneMesh.MarkDynamic();
        }

        coneMesh.Clear();
        coneMesh.vertices = vertices;
        coneMesh.colors = colors;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateBounds();

        meshFilter.sharedMesh = coneMesh;
    }

    /// <summary>
    /// Scales the drawn cone so its apex sits on the lamp, its tip reaches
    /// <see cref="Length"/>, and its visible edge lands exactly on
    /// <see cref="HalfAngle"/>. Returns false when there is no cone sprite, so
    /// the caller falls back to the generated mesh.
    /// </summary>
    private bool FitConeSprite()
    {
        if (coneSprite == null || coneSprite.sprite == null)
        {
            return false;
        }

        Bounds bounds = coneSprite.sprite.bounds;
        if (bounds.size.x <= Mathf.Epsilon || bounds.size.y <= Mathf.Epsilon)
        {
            return false;
        }

        float scaleX = Length / bounds.size.x;

        // Half the cone's width at full reach, if it opened at exactly the
        // angle the game tests against.
        float wantedHalfWidth = Length * Mathf.Tan(HalfAngle * Mathf.Deg2Rad);
        float drawnHalfWidth = bounds.size.y * 0.5f * coneSpriteEdge;
        float scaleY = wantedHalfWidth / drawnHalfWidth;

        coneSprite.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        if (meshFilter != null)
        {
            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // The cone is the whole game's hit test; being able to see it while
        // placing the lighthouse is worth the four lines.
        Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.6f);
        Vector3 origin = transform.position;
        Quaternion left = Quaternion.Euler(0f, 0f, HalfAngle);
        Quaternion right = Quaternion.Euler(0f, 0f, -HalfAngle);
        Gizmos.DrawLine(origin, origin + left * transform.right * Length);
        Gizmos.DrawLine(origin, origin + right * transform.right * Length);
    }
}
