using UnityEngine;

/// <summary>
/// The haze that rolls in on the late nights.
///
/// Several soft blobs drifting across the water at different speeds. Different
/// speeds matter more than the drawing does: two layers moving together read as
/// one sheet sliding past, while layers at odds read as weather.
///
/// Each blob wraps round when it leaves the screen, so the effect runs for as
/// long as a night lasts without anything to reset.
///
/// What is still missing is the hole the beam should cut through it — the
/// document's SpriteMask trick, so a swept patch of sea comes clear. Until
/// then the fog thins the whole scene evenly.
/// </summary>
public class FogController : MonoBehaviour
{
    [Tooltip("The drifting blobs.")]
    [SerializeField] private SpriteRenderer[] layers = new SpriteRenderer[0];

    [Tooltip("World units per second for each layer, matched by index. Keep them different or the layers read as one sheet.")]
    [SerializeField] private float[] speeds = new float[0];

    [Tooltip("How far out a blob travels before it wraps back to the other side.")]
    [SerializeField] private float wrapDistance = 13f;

    private Vector3[] origins;

    private void OnEnable()
    {
        // Captured on enable rather than in Awake: the fog object starts
        // switched off and is only turned on by the nights that use it.
        if (origins == null || origins.Length != layers.Length)
        {
            origins = new Vector3[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                origins[i] = layers[i] != null ? layers[i].transform.localPosition : Vector3.zero;
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null)
            {
                continue;
            }

            float speed = speeds != null && speeds.Length > i ? speeds[i] : 0.2f;

            Vector3 position = layers[i].transform.localPosition;
            position.x += speed * Time.deltaTime;

            if (position.x - origins[i].x > wrapDistance)
            {
                position.x -= wrapDistance * 2f;
            }
            else if (position.x - origins[i].x < -wrapDistance)
            {
                position.x += wrapDistance * 2f;
            }

            layers[i].transform.localPosition = position;
        }
    }

    /// <summary>Fills in the blobs when the fog is built in code.</summary>
    public void Bind(SpriteRenderer[] fogLayers, float[] layerSpeeds)
    {
        layers = fogLayers;
        speeds = layerSpeeds;
        origins = null;
    }
}
