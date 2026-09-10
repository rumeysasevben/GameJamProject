using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The last screen: the whole sky gone to sunrise, and the town awake.
///
/// Deliberately quieter than the nightly card — no counts, no stats. The game
/// is over and the only things left to offer are another go and the way out.
///
/// Like <see cref="NightSummaryUI"/>, the entrance is one timeline evaluated
/// from the time since it opened, so a click during it simply jumps to the end.
/// </summary>
public class EndingUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text eyebrow;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private UIButtonJuice playAgainJuice;
    [SerializeField] private Button menuButton;
    [SerializeField] private UIButtonJuice menuJuice;

    private const float ButtonsTime = 2.1f;
    private const float TotalTime = 2.8f;
    private const float LeaveDuration = 0.6f;
    private const float SkipGrace = 0.2f;

    private static readonly string[] Ordinals = { "FIRST", "SECOND", "THIRD", "FOURTH", "FIFTH", "SIXTH", "SEVENTH", "EIGHTH", "NINTH", "TENTH" };

    private Action onPlayAgain;
    private Action onMenu;
    private Action chosen;

    private bool showing;
    private bool leaving;
    private bool chimePlayed;
    private float clock;
    private float leaveClock;

    private Vector2 titleRest;
    private float eyebrowAlpha = 1f;
    private float subtitleAlpha = 1f;

    private void Awake()
    {
        if (title != null)
        {
            titleRest = title.rectTransform.anchoredPosition;
        }

        if (eyebrow != null)
        {
            eyebrowAlpha = eyebrow.color.a;
        }

        if (subtitle != null)
        {
            subtitleAlpha = subtitle.color.a;
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(() => Choose(onPlayAgain));
        }

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(() => Choose(onMenu));
        }

        Hide();
    }

    /// <summary>Opens the ending for the campaign's last night.</summary>
    public void Show(int nightNumber, Action onPlayAgainPressed, Action onMenuPressed)
    {
        onPlayAgain = onPlayAgainPressed;
        onMenu = onMenuPressed;
        chosen = null;

        if (eyebrow != null)
        {
            eyebrow.text = nightNumber >= 1 && nightNumber <= Ordinals.Length
                ? $"THE {Ordinals[nightNumber - 1]} NIGHT"
                : $"NIGHT {nightNumber}";
        }

        gameObject.SetActive(true);

        showing = true;
        leaving = false;
        chimePlayed = false;
        clock = 0f;

        if (group != null)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        Evaluate(0f);
    }

    /// <summary>Hides the ending at once.</summary>
    public void Hide()
    {
        showing = false;
        leaving = false;

        SetButtonsInteractable(false);

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

        if (clock < TotalTime && clock > SkipGrace && SkipPressed())
        {
            clock = TotalTime;
        }

        Evaluate(clock);
    }

    private void Evaluate(float t)
    {
        if (group != null)
        {
            group.alpha = UIEase.OutCubic(UIEase.Progress(t, 0f, 1.2f));
        }

        if (eyebrow != null)
        {
            eyebrow.alpha = eyebrowAlpha * UIEase.OutCubic(UIEase.Progress(t, 0.7f, 0.6f));
        }

        if (title != null)
        {
            float p = UIEase.Progress(t, 1.0f, 0.8f);
            float eased = UIEase.OutCubic(p);

            title.alpha = eased;
            title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
            title.rectTransform.anchoredPosition = titleRest + new Vector2(0f, -20f * (1f - eased));

            if (p > 0f && !chimePlayed)
            {
                chimePlayed = true;

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.Play("link_success");
                }
            }
        }

        if (subtitle != null)
        {
            subtitle.alpha = subtitleAlpha * UIEase.OutCubic(UIEase.Progress(t, 1.6f, 0.6f));
        }

        float first = UIEase.Progress(t, ButtonsTime, 0.45f);
        float second = UIEase.Progress(t, ButtonsTime + 0.12f, 0.45f);

        if (playAgainJuice != null)
        {
            playAgainJuice.Appear = UIEase.OutBack(first);
        }

        if (menuJuice != null)
        {
            menuJuice.Appear = UIEase.OutBack(second);
        }

        SetButtonsInteractable(!leaving && second >= 0.5f);
    }

    private void Choose(Action action)
    {
        if (!showing || leaving)
        {
            return;
        }

        chosen = action;
        leaving = true;
        leaveClock = 0f;

        SetButtonsInteractable(false);

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

        if (p < 1f)
        {
            return;
        }

        Action callback = chosen;
        chosen = null;

        Hide();
        callback?.Invoke();
    }

    private void SetButtonsInteractable(bool value)
    {
        if (playAgainButton != null)
        {
            playAgainButton.interactable = value;
        }

        if (menuButton != null)
        {
            menuButton.interactable = value;
        }
    }

    private static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;

        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame));
    }
}
