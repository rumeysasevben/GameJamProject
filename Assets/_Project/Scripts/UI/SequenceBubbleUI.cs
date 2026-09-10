using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The little bubble of symbols floating above a ship.
///
/// There are three separate reasons it can be up, and any one of them is
/// enough:
///
/// - the night keeps every sequence on screen, which is how the first nights
///   teach the player to read one;
/// - the ship is flashing right now;
/// - the beam is on the ship.
///
/// That last one is what makes the later nights playable. Without it a player
/// on night five is looking at four unlabelled boats, waiting for whichever
/// one flashes next, and guessing in between — which is not remembering, it is
/// clicking at random. Aiming at a ship asks it what it wants, and asking is
/// the whole verb of this game.
/// </summary>
public class SequenceBubbleUI : MonoBehaviour
{
    [Tooltip("The panel that holds the symbols. Toggled instead of this object, so the follow logic keeps running while hidden.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Layout parent the symbol images are spawned under.")]
    [SerializeField] private Transform symbolParent;

    [Tooltip("Prefab of a single symbol image.")]
    [SerializeField] private Image symbolPrefab;

    [SerializeField] private Sprite shortSprite;
    [SerializeField] private Sprite longSprite;

    [Header("Follow")]
    [Tooltip("The ship this bubble sits above.")]
    [SerializeField] private Transform target;

    [Tooltip("Offset from the ship in world units.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.7f);

    [Header("Highlight")]
    [Tooltip("Colour of the symbols while the beam is on this ship.")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.75f);

    [Tooltip("Colour of the symbols the rest of the time.")]
    [SerializeField] private Color normalColor = new Color(0.85f, 0.87f, 0.92f);

    private readonly List<Image> spawned = new List<Image>();

    private bool alwaysOn;
    private bool targeted;
    private bool flashing;
    private bool suppressed;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    // The pause panel's "always show ship codes" switch applies at once.
    private void OnEnable() => GameSettings.Changed += Refresh;

    private void OnDisable() => GameSettings.Changed -= Refresh;

    private void LateUpdate()
    {
        // After the ship has moved, so the bubble never trails a frame behind.
        if (target != null)
        {
            transform.position = target.position + (Vector3)offset;
        }
    }

    /// <summary>
    /// Sets the sequence this bubble shows and whether the night keeps it up
    /// the whole time.
    /// </summary>
    public void Configure(Signal[] sequence, bool keepOnScreen)
    {
        alwaysOn = keepOnScreen;
        Build(sequence);
        Refresh();
    }

    /// <summary>The beam is on this ship, or has left it.</summary>
    public void SetTargeted(bool isTargeted)
    {
        targeted = isTargeted;
        Refresh();
    }

    /// <summary>The ship is playing its sequence right now.</summary>
    public void SetFlashing(bool isFlashing)
    {
        flashing = isFlashing;
        Refresh();
    }

    /// <summary>
    /// Hides the bubble whatever else is true. A ship that has answered, or
    /// berthed, is no longer asking for anything.
    /// </summary>
    public void SetSuppressed(bool isSuppressed)
    {
        suppressed = isSuppressed;
        Refresh();
    }

    private void Refresh()
    {
        if (panel != null)
        {
            panel.SetActive(!suppressed && (alwaysOn || GameSettings.ShowSequences || targeted || flashing));
        }

        Color color = targeted ? highlightColor : normalColor;
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                spawned[i].color = color;
            }
        }
    }

    private void Build(Signal[] sequence)
    {
        if (symbolPrefab == null || symbolParent == null || sequence == null)
        {
            return;
        }

        while (spawned.Count < sequence.Length)
        {
            spawned.Add(Instantiate(symbolPrefab, symbolParent));
        }

        for (int i = 0; i < spawned.Count; i++)
        {
            bool used = i < sequence.Length;
            spawned[i].gameObject.SetActive(used);

            if (used)
            {
                spawned[i].sprite = sequence[i] == Signal.Short ? shortSprite : longSprite;
            }
        }
    }
}
