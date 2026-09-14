using UnityEngine;

/// <summary>
/// The ship seen in the water beneath it.
///
/// Each source drawing — the hull, with its lights painted on, and the signal
/// lamp — gets an upside-down copy mirrored about the ship's waterline. The
/// copy follows its source every frame, sprite, colour and sway included, so a
/// ship turning or flashing its lamp is echoed below. The rippling and the
/// shimmer are the material's job; this only keeps the mirror in place.
/// </summary>
public class WaterReflection : MonoBehaviour
{
    [Tooltip("The drawings to reflect. Left empty, the children named Body and Lamp are used.")]
    [SerializeField] private SpriteRenderer[] sources = new SpriteRenderer[0];

    [Tooltip("The drawing whose bottom edge sets the waterline. Left empty, the first source.")]
    [SerializeField] private SpriteRenderer hull;

    [Tooltip("How far up from the hull drawing's bottom edge the water meets it, as a share of its height.")]
    [SerializeField, Range(0f, 0.5f)] private float waterlineInset = 0.12f;

    [Tooltip("Rippling, shimmering material for the copies.")]
    [SerializeField] private Material material;

    [Tooltip("Multiplied into the hull's reflection: barely there and cooled toward the sea, so it reads as water catching the hull, not a second ship.")]
    [SerializeField] private Color hullTint = new Color(0.5f, 0.6f, 0.75f, 0.1f);

    [Tooltip("Multiplied into the reflection of every other source. Lights stay brighter in water than the hull does.")]
    [SerializeField] private Color lightTint = new Color(1f, 0.95f, 0.85f, 0.25f);

    [Tooltip("Sorting order for the copies. Over the sea, under every ship.")]
    [SerializeField] private int sortingOrder = -9;

    private SpriteRenderer[] copies;
    private Vector3 hullHome;

    private void Awake()
    {
        if (sources == null || sources.Length == 0)
        {
            sources = new[] { FindRenderer("Body"), FindRenderer("Lamp") };
        }

        if (hull == null && sources.Length > 0)
        {
            hull = sources[0];
        }

        // The waterline is taken from where the hull rests, not where the sway
        // has it this frame, so a rising hull pulls its reflection down.
        if (hull != null)
        {
            hullHome = hull.transform.localPosition;
        }

        copies = new SpriteRenderer[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
            {
                continue;
            }

            var go = new GameObject($"{sources[i].name}Reflection", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);

            var copy = go.GetComponent<SpriteRenderer>();
            copy.sortingOrder = sortingOrder;
            if (material != null)
            {
                copy.sharedMaterial = material;
            }

            copies[i] = copy;
        }
    }

    private void LateUpdate()
    {
        float waterline = Waterline();

        for (int i = 0; i < sources.Length; i++)
        {
            SpriteRenderer source = sources[i];
            SpriteRenderer copy = copies[i];
            if (source == null || copy == null)
            {
                continue;
            }

            bool visible = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            copy.enabled = visible;
            if (!visible)
            {
                continue;
            }

            copy.sprite = source.sprite;
            Color tint = source == hull ? hullTint : lightTint;
            copy.color = source.color * tint;

            // Mirrored about the waterline: as far below it as the source is
            // above, leaning the opposite way.
            Transform from = source.transform;
            Vector3 position = from.position;
            position.y = 2f * waterline - position.y;

            float roll = from.eulerAngles.z;
            copy.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, -roll));
            // Turned upside down by scale rather than flipY: sprite flipping is
            // done in Unity's own sprite shader, and the rippling material is not
            // that shader.
            Vector3 scale = from.lossyScale;
            copy.transform.localScale = new Vector3(source.flipX ? -scale.x : scale.x, -scale.y, scale.z);
        }
    }

    private float Waterline()
    {
        if (hull == null || hull.sprite == null)
        {
            return transform.position.y;
        }

        Bounds bounds = hull.sprite.bounds;
        float scale = hull.transform.lossyScale.y;
        return transform.position.y + hullHome.y + (bounds.min.y + bounds.size.y * waterlineInset) * scale;
    }

    private SpriteRenderer FindRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }
}
