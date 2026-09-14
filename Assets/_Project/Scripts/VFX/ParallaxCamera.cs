using UnityEngine;

/// <summary>
/// The view, never quite still.
///
/// Two small movements laid together. A very slow drift, like a watcher on
/// the lighthouse gallery shifting their weight, which gives
/// <see cref="ParallaxLayer"/>s something to lag behind. And a lean: when the
/// game asks for it — a ship closing on the harbour — the view eases a little
/// that way, as if turning to watch it come in, and eases back after.
///
/// The camera zooms in exactly as far as the current offset needs, so the
/// edges of the coast drawing never come into view; with the lean at rest the
/// zoom is all but nothing. Input reads the camera every frame, so the beam
/// still lands exactly under the cursor.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ParallaxCamera : MonoBehaviour
{
    [Tooltip("How far the view drifts either side, in world units.")]
    [SerializeField] private Vector2 drift = new Vector2(0.09f, 0.05f);

    [Tooltip("Seconds for one full sway, left-right and up-down. Different, so the path never quite repeats.")]
    [SerializeField] private Vector2 period = new Vector2(26f, 37f);

    [Tooltip("The furthest the view leans toward what it is asked to watch, in world units.")]
    [SerializeField] private float maxLean = 0.3f;

    [Tooltip("How quickly the view eases toward its lean, per second. Low, so it is felt rather than seen.")]
    [SerializeField] private float leanEase = 1.2f;

    /// <summary>How far the view has drifted and leaned from where it was placed, this frame.</summary>
    public static Vector2 Offset { get; private set; }

    private static ParallaxCamera active;

    private Camera cam;
    private Vector3 home;
    private float homeSize;
    private Vector2 leanTarget;
    private Vector2 lean;

    /// <summary>
    /// Asks the view to lean along <paramref name="direction"/> by
    /// <paramref name="amount"/> of its limit, 0 to 1, and eases toward it.
    /// Zero lets it settle back.
    /// </summary>
    public static void SetLean(Vector2 direction, float amount)
    {
        if (active != null)
        {
            Vector2 unit = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector2.zero;
            active.leanTarget = unit * (active.maxLean * Mathf.Clamp01(amount));
        }
    }

    private void Awake()
    {
        active = this;
        cam = GetComponent<Camera>();
        home = transform.position;
        homeSize = cam.orthographicSize;
    }

    private void OnDestroy()
    {
        if (active == this)
        {
            active = null;
            Offset = Vector2.zero;
        }
    }

    private void LateUpdate()
    {
        float t = Time.time;
        var sway = new Vector2(
            Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(1f, period.x)) * drift.x,
            Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(1f, period.y) + 1.3f) * drift.y);

        // Exponential ease: fast to start, settling gently, frame-rate free.
        lean = Vector2.Lerp(lean, leanTarget, 1f - Mathf.Exp(-leanEase * Time.deltaTime));

        Offset = sway + lean;
        transform.position = home + (Vector3)Offset;

        if (cam.orthographic)
        {
            // Just enough zoom that the frame stays full wherever the view has
            // moved: the drift's full width always, plus the lean's actual size.
            float needY = drift.y + Mathf.Abs(lean.y);
            float needX = (drift.x + Mathf.Abs(lean.x)) / Mathf.Max(0.01f, cam.aspect);
            cam.orthographicSize = homeSize - Mathf.Max(needX, needY);
        }
    }
}
