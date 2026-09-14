using UnityEngine;

/// <summary>
/// A ship riding the swell: rising and falling, rolling either side and
/// shifting a touch on the water — at sea, waiting for a signal, or tied up at
/// the pier alike.
///
/// Each motion is two waves of different lengths laid over each other, so the
/// swell never settles into a clean, mechanical rhythm; a single sine reads as
/// a ship on a spring, and once the ship stops moving it reads as no motion at
/// all. The roll turns about the keel rather than the middle of the drawing,
/// the way a hull rocks on the water.
///
/// Only the drawings move — the hull and its lamp, found by name — never the
/// ship itself, so steering, collisions and berthing see exactly the position
/// they always did. Each ship gets its own speed and phase from its instance,
/// so a harbour full of boats does not bob in step.
/// </summary>
public class ShipSway : MonoBehaviour
{
    [Tooltip("The drawings that sway. Left empty, the children named Body and Lamp are used.")]
    [SerializeField] private Transform[] targets = new Transform[0];

    [Tooltip("How far the ship rises and falls, in world units.")]
    [SerializeField] private float bob = 0.055f;

    [Tooltip("How far the ship rolls either side, in degrees.")]
    [SerializeField] private float roll = 3f;

    [Tooltip("How far the ship shifts side to side on the water, in world units.")]
    [SerializeField] private float surge = 0.025f;

    [Tooltip("Where the roll turns, relative to the ship's centre. Below the middle, at the keel.")]
    [SerializeField] private Vector2 rollPivot = new Vector2(0f, -0.18f);

    [Tooltip("Slowest and fastest swell, in cycles per second.")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.4f, 0.6f);

    private Vector3[] homes;
    private float speed;
    private float phase;

    private void Awake()
    {
        if (targets == null || targets.Length == 0)
        {
            targets = new[] { transform.Find("Body"), transform.Find("Lamp") };
        }

        homes = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                homes[i] = targets[i].localPosition;
            }
        }

        // Seeded per ship so every boat keeps its own rhythm.
        var random = new System.Random(GetInstanceID());
        speed = Mathf.Lerp(speedRange.x, speedRange.y, (float)random.NextDouble());
        phase = (float)random.NextDouble() * 100f;
    }

    private void Update()
    {
        float t = (Time.time + phase) * speed * Mathf.PI * 2f;

        // A long swell with a shorter chop over it, each motion on its own
        // rates so rise, roll and drift wander in and out of step.
        float rise = (Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.3f + 1.1f) * 0.3f) * bob;
        float tilt = (Mathf.Sin(t * 0.8f + 2f) * 0.75f + Mathf.Sin(t * 1.9f + 0.4f) * 0.25f) * roll;
        float drift = (Mathf.Sin(t * 0.45f + 4f) * 0.8f + Mathf.Sin(t * 1.3f) * 0.2f) * surge;

        Quaternion rotation = Quaternion.Euler(0f, 0f, tilt);
        Vector3 pivot = rollPivot;
        var offset = new Vector3(drift, rise, 0f);

        for (int i = 0; i < targets.Length; i++)
        {
            Transform target = targets[i];
            if (target == null)
            {
                continue;
            }

            // Rolled about the keel, so the lamp on its mast swings wider than
            // the hull beneath it.
            target.localPosition = pivot + rotation * (homes[i] - pivot) + offset;
            target.localRotation = rotation;
        }
    }
}
