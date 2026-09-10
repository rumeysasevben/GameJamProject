using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The card between dawn and the next dusk: how many ships came home, and the
/// row of the town's windows with tonight's one lighting up.
///
/// It waits for a click rather than moving on by itself. Dawn is the point of
/// the whole night, and cutting away from it on a timer would take the one
/// quiet moment in the game away from the person who earned it.
///
/// The entrance is a single timeline, evaluated from the time since the card
/// opened: veil, card, words, then the windows filling one by one, with the new
/// one saved for last and given its own sound. A click or a key during it jumps
/// the clock to the end, so the reward is never something the player is made to
/// sit through.
///
/// With no ending screen wired, the last night falls back to this card in an
/// ending form.
/// </summary>
public class NightSummaryUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image veil;
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private TMP_Text eyebrow;
    [SerializeField] private TMP_Text body;
    [SerializeField] private CanvasGroup pipsGroup;
    [SerializeField] private Image[] pips = new Image[0];
    [SerializeField] private Image[] pipGlows = new Image[0];
    [SerializeField] private TMP_Text windowsLabel;
    [SerializeField] private TMP_Text stats;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private UIButtonJuice continueJuice;

    [Header("Look")]
    [SerializeField] private Color pipDim = new Color(0.227f, 0.267f, 0.365f);
    [SerializeField] private Color pipLit = new Color(1f, 0.788f, 0.420f);
    [SerializeField, Range(0f, 1f)] private float veilAlpha = 0.55f;

    // Timeline, in seconds since the card opened.
    private const float PipsStart = 1.0f;
    private const float PipStep = 0.07f;
    private const float NewPipDelay = 0.35f;
    private const float LeaveDuration = 0.28f;

    // A click this soon after opening is the one that skipped the sunrise, not
    // a request to skip the card as well.
    private const float SkipGrace = 0.2f;

    private static readonly string[] NumberWords = { "No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" };

    private static readonly string[] WindowLines =
    {
        "One more window is lit in town.",
        "Another window glows on the hill.",
        "Someone in town left a light on.",
        "A new light is burning up the hill."
    };

    private Action onContinue;
    private bool showing;
    private bool leaving;
    private bool windowSoundPlayed;
    private float clock;
    private float leaveClock;

    private int windowsTotal;
    private int litBefore;
    private int litNow;
    private int shownCount = -1;

    private Vector2 cardRest;
    private Vector2 bodyRest;
    private float eyebrowSpacing;
    private float eyebrowAlpha = 1f;
    private float windowsAlpha = 1f;
    private float statsAlpha = 1f;
    private Color glowColor = Color.white;

    private float NewPipTime => PipsStart + litBefore * PipStep + NewPipDelay;
    private float ButtonTime => NewPipTime + 0.55f;
    private float TotalTime => ButtonTime + 0.45f;

    private void Awake()
    {
        if (card != null)
        {
            cardRest = card.anchoredPosition;
        }

        if (body != null)
        {
            bodyRest = body.rectTransform.anchoredPosition;
        }

        if (eyebrow != null)
        {
            eyebrowSpacing = eyebrow.characterSpacing;
            eyebrowAlpha = eyebrow.color.a;
        }

        if (windowsLabel != null)
        {
            windowsAlpha = windowsLabel.color.a;
        }

        if (stats != null)
        {
            statsAlpha = stats.color.a;
        }

        if (pipGlows.Length > 0 && pipGlows[0] != null)
        {
            glowColor = pipGlows[0].color;
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Continue);
        }

        Hide();
    }

    /// <summary>
    /// Opens the card. <paramref name="windowsLit"/> already counts tonight's
    /// window. <paramref name="onContinuePressed"/> is what the button does.
    /// </summary>
    public void Show(int nightNumber, int shipsHome, int windowsLit, int totalWindows, float elapsedSeconds, int collisions, bool isFinalNight, Action onContinuePressed)
    {
        onContinue = onContinuePressed;

        windowsTotal = Mathf.Clamp(totalWindows, 0, pips.Length);
        litNow = Mathf.Clamp(windowsLit, 0, windowsTotal);
        litBefore = Mathf.Max(0, litNow - 1);
        shownCount = -1;
        windowSoundPlayed = false;

        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] != null)
            {
                pips[i].gameObject.SetActive(i < windowsTotal);
            }

            if (i < pipGlows.Length && pipGlows[i] != null)
            {
                pipGlows[i].gameObject.SetActive(i < windowsTotal);
            }
        }

        if (eyebrow != null)
        {
            eyebrow.text = isFinalNight ? "THE TOWN IS AWAKE" : $"NIGHT {nightNumber} COMPLETE";
        }

        if (body != null)
        {
            body.text = isFinalNight
                ? "Every window is lit.\nNobody was left out at sea."
                : $"{ShipsLine(shipsHome)}\n{WindowLines[Mathf.Abs(nightNumber - 1) % WindowLines.Length]}";
        }

        if (stats != null)
        {
            stats.text = $"{Clock(elapsedSeconds)}   ·   {Collisions(collisions)}";
        }

        if (continueLabel != null)
        {
            continueLabel.text = isFinalNight ? "Back to the menu" : $"On to night {nightNumber + 1}";
        }

        gameObject.SetActive(true);

        showing = true;
        leaving = false;
        clock = 0f;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        Evaluate(0f);
    }

    /// <summary>Hides the card at once and stops it swallowing clicks.</summary>
    public void Hide()
    {
        showing = false;
        leaving = false;

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private void Update()
    {
        if (!showing)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        if (leaving)
        {
            TickLeave(dt);
            return;
        }

        clock += dt;

        if (clock < TotalTime)
        {
            if (clock > SkipGrace && SkipPressed())
            {
                clock = TotalTime;
            }
        }
        else if (ContinuePressed())
        {
            Continue();
            return;
        }

        Evaluate(clock);
    }

    private void Evaluate(float t)
    {
        if (veil != null)
        {
            Color c = veil.color;
            veil.color = new Color(c.r, c.g, c.b, veilAlpha * UIEase.OutCubic(UIEase.Progress(t, 0f, 0.5f)));
        }

        // The card rises and pops into place.
        float cardP = UIEase.Progress(t, 0.1f, 0.5f);

        if (card != null)
        {
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, UIEase.OutBack(cardP));
            card.anchoredPosition = cardRest + new Vector2(0f, -40f * (1f - UIEase.OutCubic(cardP)));
        }

        if (cardGroup != null)
        {
            cardGroup.alpha = UIEase.OutCubic(cardP);
        }

        // The small heading closes its letters up as it fades in.
        if (eyebrow != null)
        {
            float p = UIEase.OutCubic(UIEase.Progress(t, 0.35f, 0.45f));
            eyebrow.alpha = eyebrowAlpha * p;
            eyebrow.characterSpacing = Mathf.Lerp(eyebrowSpacing + 28f, eyebrowSpacing, p);
        }

        if (body != null)
        {
            float p = UIEase.OutCubic(UIEase.Progress(t, 0.55f, 0.5f));
            body.alpha = p;
            body.rectTransform.anchoredPosition = bodyRest + new Vector2(0f, -14f * (1f - p));
        }

        if (pipsGroup != null)
        {
            pipsGroup.alpha = UIEase.OutCubic(UIEase.Progress(t, 0.8f, 0.3f));
        }

        EvaluatePips(t);

        if (windowsLabel != null)
        {
            windowsLabel.alpha = windowsAlpha * UIEase.Progress(t, 0.9f, 0.3f);
        }

        if (stats != null)
        {
            stats.alpha = statsAlpha * UIEase.OutCubic(UIEase.Progress(t, NewPipTime + 0.35f, 0.4f));
        }

        float buttonP = UIEase.Progress(t, ButtonTime, 0.45f);

        if (continueJuice != null)
        {
            continueJuice.Appear = UIEase.OutBack(buttonP);
        }
        else if (continueButton != null)
        {
            continueButton.transform.localScale = Vector3.one * UIEase.OutBack(buttonP);
        }

        if (continueButton != null)
        {
            continueButton.interactable = !leaving && buttonP >= 0.5f;
        }
    }

    /// <summary>
    /// The windows earned on earlier nights come on in a quick run; tonight's
    /// waits a beat, then lands bigger, with a glow and the window sound.
    /// </summary>
    private void EvaluatePips(float t)
    {
        int lit = 0;

        for (int i = 0; i < windowsTotal; i++)
        {
            Image pip = pips[i];
            Image pipGlow = i < pipGlows.Length ? pipGlows[i] : null;

            float amount = 0f;
            float scale = 1f;
            float glowAlpha = 0f;

            if (i < litBefore)
            {
                float p = UIEase.Progress(t, PipsStart + i * PipStep, 0.22f);
                amount = p;
                scale = UIEase.Pop(p, 0.35f);
            }
            else if (i == litNow - 1)
            {
                float p = UIEase.Progress(t, NewPipTime, 0.5f);
                amount = Mathf.Clamp01(p * 3f);
                scale = UIEase.Pop(p, 0.9f);

                glowAlpha = p < 1f
                    ? p
                    : 0.6f + 0.2f * Mathf.Sin((t - NewPipTime) * 3f);

                if (p > 0f && !windowSoundPlayed)
                {
                    windowSoundPlayed = true;

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.Play("window_on");
                    }
                }
            }

            if (amount >= 0.5f)
            {
                lit++;
            }

            if (pip != null)
            {
                pip.color = Color.Lerp(pipDim, pipLit, amount);
                pip.rectTransform.localScale = Vector3.one * scale;
            }

            if (pipGlow != null)
            {
                pipGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * glowAlpha);
                pipGlow.rectTransform.localScale = Vector3.one * scale;
            }
        }

        if (windowsLabel != null && lit != shownCount)
        {
            shownCount = lit;
            windowsLabel.text = $"{lit} / {windowsTotal} windows lit";
        }
    }

    private void Continue()
    {
        if (!showing || leaving)
        {
            return;
        }

        leaving = true;
        leaveClock = 0f;

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        if (group != null)
        {
            group.interactable = false;
        }
    }

    private void TickLeave(float dt)
    {
        leaveClock += dt;
        float p = UIEase.OutCubic(UIEase.Progress(leaveClock, 0f, LeaveDuration));

        if (group != null)
        {
            group.alpha = 1f - p;
        }

        if (card != null)
        {
            card.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, p);
        }

        if (p < 1f)
        {
            return;
        }

        Action callback = onContinue;
        onContinue = null;

        Hide();
        callback?.Invoke();
    }

    private static bool SkipPressed()
    {
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || ContinuePressed();
    }

    private static bool ContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }

    private static string ShipsLine(int count)
    {
        if (count == 1)
        {
            return "One ship is home.";
        }

        string word = count >= 0 && count < NumberWords.Length ? NumberWords[count] : count.ToString();
        return $"{word} ships are home.";
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
