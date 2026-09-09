using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The "Gece N" card that opens a night: fade in, hold, fade out.
/// </summary>
public class NightTitleUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;

    [Tooltip("Seconds each of the fades takes. The hold in between comes from GameConfig.nightTitleDuration.")]
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine routine;

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
        }
    }

    /// <summary>Shows the title for night <paramref name="nightNumber"/>.</summary>
    public void Show(int nightNumber, float holdDuration)
    {
        if (label != null)
        {
            label.text = $"Night {nightNumber}";
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(ShowRoutine(holdDuration));
    }

    private IEnumerator ShowRoutine(float holdDuration)
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f);
        routine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = to;
    }
}
