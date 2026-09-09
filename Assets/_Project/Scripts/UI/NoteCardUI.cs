using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The card that slides up when a ship arrives, carrying the two sentences that
/// say who was aboard.
///
/// A new note cuts off whichever one is on screen. Ships can arrive close
/// together, and queueing notes would put them minutes behind the moment they
/// belong to.
/// </summary>
public class NoteCardUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text label;

    [Tooltip("Anchored position while hidden. Usually just below the screen edge.")]
    [SerializeField] private Vector2 hiddenPosition = new Vector2(0f, -200f);

    [Tooltip("Anchored position while shown.")]
    [SerializeField] private Vector2 shownPosition = new Vector2(0f, 90f);

    [SerializeField] private float slideDuration = 0.3f;

    private Coroutine routine;

    private void Awake()
    {
        if (panel != null)
        {
            panel.anchoredPosition = hiddenPosition;
        }
    }

    /// <summary>Slides in <paramref name="note"/>, holds it, then slides it away.</summary>
    public void Show(string note, float holdDuration)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        if (label != null)
        {
            label.text = note;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(ShowRoutine(holdDuration));
    }

    private IEnumerator ShowRoutine(float holdDuration)
    {
        yield return Slide(hiddenPosition, shownPosition);
        yield return new WaitForSeconds(holdDuration);
        yield return Slide(shownPosition, hiddenPosition);
        routine = null;
    }

    private IEnumerator Slide(Vector2 from, Vector2 to)
    {
        if (panel == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration));
            panel.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        panel.anchoredPosition = to;
    }
}
