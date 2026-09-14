using UnityEngine;

/// <summary>
/// Something far away. It follows the <see cref="ParallaxCamera"/>'s drift for
/// most of the way, so on screen it moves only a little while the foreground —
/// the sea, rocks and ships, which stay put in the world — slides by faster.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("How much this layer appears to move with the view. 1 is foreground (the world itself); near 0 is the far distance.")]
    [SerializeField, Range(0f, 1f)] private float motion = 0.25f;

    private Vector3 home;

    private void Awake()
    {
        home = transform.localPosition;
    }

    private void LateUpdate()
    {
        // Carried along by the part of the drift it should not show.
        transform.localPosition = home + (Vector3)(ParallaxCamera.Offset * (1f - motion));
    }
}
