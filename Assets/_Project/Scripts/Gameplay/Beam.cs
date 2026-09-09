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
    private Coroutine flashRoutine;

    /// <summary>Centre line of the cone in world space.</summary>
    public Vector2 Direction => transform.right;

    /// <summary>Where the cone starts — the lamp.</summary>
    public Vector2 Origin => transform.position;

    /// <summary>Half the cone's opening angle, in degrees.</summary>
    public float HalfAngle => config != null ? config.beamHalfAngle : 12f;

    /// <summary>How far the cone reaches, in world units.</summary>
    public float Length => config != null ? config.beamLength : 22f;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        baseIntensity = beamLight != null ? beamLight.intensity : 0f;
        RebuildMesh();
    }

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

    /// <summary>True when <paramref name="point"/> lies inside the cone.</summary>
    public bool Contains(Vector2 point)
    {
        Vector2 offset = point - Origin;
        float distance = offset.magnitude;

        if (distance > Length)
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
        if (beamLight == null)
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

    private IEnumerator FlashRoutine(float hold)
    {
        beamLight.intensity = baseIntensity * flashMultiplier;
        yield return new WaitForSeconds(hold);

        float elapsed = 0f;
        float from = beamLight.intensity;
        while (elapsed < flashFade)
        {
            elapsed += Time.deltaTime;
            beamLight.intensity = Mathf.Lerp(from, baseIntensity, elapsed / flashFade);
            yield return null;
        }

        beamLight.intensity = baseIntensity;
        flashRoutine = null;
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
