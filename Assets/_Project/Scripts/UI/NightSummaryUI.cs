using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player sees once the sun is up: how the night went, and a button
/// to go on.
///
/// It waits for a click rather than moving on by itself. Dawn is the point of
/// the whole night, and cutting away from it on a timer would take the one
/// quiet moment in the game away from the person who earned it.
///
/// The last night uses the same panel in its ending form: no "next night", no
/// count of what is left, just the lit town and a way back. The harbour behind
/// it stays in daylight — this is the one screen the game does not put back to
/// dark.
/// </summary>
public class NightSummaryUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;

    [Tooltip("How long the panel takes to appear. Slow, so it arrives with the light rather than interrupting it.")]
    [SerializeField] private float fadeDuration = 0.8f;

    private Action onContinue;

    private void Awake()
    {
        Hide();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Continue);
        }
    }

    /// <summary>
    /// Shows the summary. <paramref name="isFinalNight"/> turns it into the
    /// ending. <paramref name="onContinuePressed"/> is what the button does —
    /// the next night, or the way back to the menu.
    /// </summary>
    public void Show(int nightNumber, float elapsedSeconds, int collisions, bool isFinalNight, Action onContinuePressed)
    {
        onContinue = onContinuePressed;

        if (title != null)
        {
            title.text = isFinalNight ? "The town is awake" : $"Night {nightNumber} complete";
        }

        if (body != null)
        {
            string line = $"{Clock(elapsedSeconds)}   ·   {Collisions(collisions)}";

            body.text = isFinalNight
                ? $"Every window is lit. Nobody was left out at sea.\n\n{line}"
                : line;
        }

        if (continueLabel != null)
        {
            continueLabel.text = isFinalNight ? "Back to the menu" : "Continue";
        }

        gameObject.SetActive(true);

        if (isActiveAndEnabled)
        {
            StartCoroutine(FadeIn());
        }
        else
        {
            SetVisible(true);
        }
    }

    /// <summary>Hides the panel and stops it swallowing clicks.</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void Continue()
    {
        Action callback = onContinue;
        onContinue = null;

        Hide();
        callback?.Invoke();
    }

    private IEnumerator FadeIn()
    {
        if (group == null)
        {
            yield break;
        }

        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        group.alpha = 1f;
    }

    private void SetVisible(bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    /// <summary>Minutes and seconds, e.g. "1:07".</summary>
    private static string Clock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{whole / 60}:{whole % 60:00}";
    }

    private static string Collisions(int count)
    {
        if (count == 0)
        {
            return "not a scratch";
        }

        return count == 1 ? "1 bump" : $"{count} bumps";
    }
}
