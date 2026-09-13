using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Boot scene's menu.
///
/// It exists for two reasons. One is the obvious one — new game, continue. The
/// other is that browsers refuse to play audio until the page has been clicked,
/// so the game must never open straight into a night: the button press here is
/// what buys the first sound.
///
/// The look is the night mock-up with the lighthouse awake: the beam sweeps the
/// sea, the lamp breathes, and the title and buttons rise in one after another
/// once the screen has faded up from black.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Starts the campaign from night 1. Shown when there is nothing to continue.")]
    [SerializeField] private Button startButton;

    [Tooltip("Resumes at the furthest night reached. Hidden until there is something to resume.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Label on the continue button; the night number is written into it.")]
    [SerializeField] private TMP_Text continueLabel;

    [Tooltip("Starts over from night 1. Shown beside continue, instead of start.")]
    [SerializeField] private Button newGameButton;

    [Header("Entrance")]
    [Tooltip("Full-screen black the menu fades up from.")]
    [SerializeField] private CanvasGroup cover;
    [SerializeField] private float coverFade = 1.2f;

    [Tooltip("Revealed in order: each fades in and rises into place.")]
    [SerializeField] private CanvasGroup[] reveal;
    [SerializeField] private float revealDelay = 0.5f;
    [SerializeField] private float revealStagger = 0.12f;
    [SerializeField] private float revealDuration = 0.6f;
    [SerializeField] private float revealRise = 28f;

    [Header("Lighthouse")]
    [Tooltip("The beam cone, pivoted at the lantern.")]
    [SerializeField] private RectTransform beam;
    [SerializeField] private float sweepFrom = -4f;
    [SerializeField] private float sweepTo = 22f;
    [Tooltip("Seconds for one sweep out and back.")]
    [SerializeField] private float sweepPeriod = 9f;

    [Tooltip("Glow over the lantern; its alpha breathes.")]
    [SerializeField] private Image lampGlow;
    [SerializeField] private float glowPeriod = 2.6f;

    private Vector2[] revealHome;
    private float glowAlpha = 1f;
    private float startTime;

    private void Start()
    {
        int unlocked = GameManager.Instance != null ? GameManager.Instance.UnlockedNight : 1;
        bool hasProgress = unlocked > 1;

        if (startButton != null)
        {
            startButton.gameObject.SetActive(!hasProgress);
            startButton.onClick.AddListener(StartNew);
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(hasProgress);
            newGameButton.onClick.AddListener(StartNew);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(hasProgress);
            continueButton.onClick.AddListener(Continue);
        }

        if (continueLabel != null && hasProgress)
        {
            int count = GameManager.Instance != null && GameManager.Instance.Nights != null ? GameManager.Instance.Nights.Count : unlocked;
            continueLabel.text = $"Continue · Night {Mathf.Min(unlocked, Mathf.Max(1, count))}";
        }

        if (lampGlow != null)
        {
            glowAlpha = lampGlow.color.a;
        }

        if (reveal != null)
        {
            revealHome = new Vector2[reveal.Length];
            for (int i = 0; i < reveal.Length; i++)
            {
                if (reveal[i] != null)
                {
                    revealHome[i] = ((RectTransform)reveal[i].transform).anchoredPosition;
                }
            }
        }

        startTime = Time.unscaledTime;
        Animate(0f);
    }

    private void Update()
    {
        Animate(Time.unscaledTime - startTime);
    }

    private void Animate(float t)
    {
        if (cover != null)
        {
            cover.alpha = 1f - Smooth(t / Mathf.Max(0.01f, coverFade));
            cover.blocksRaycasts = cover.alpha > 0.5f;
        }

        if (reveal != null && revealHome != null)
        {
            for (int i = 0; i < reveal.Length; i++)
            {
                if (reveal[i] == null)
                {
                    continue;
                }

                float k = Smooth((t - revealDelay - i * revealStagger) / Mathf.Max(0.01f, revealDuration));
                reveal[i].alpha = k;
                reveal[i].interactable = k > 0.9f;
                ((RectTransform)reveal[i].transform).anchoredPosition = revealHome[i] + new Vector2(0f, -revealRise * (1f - k));
            }
        }

        if (beam != null)
        {
            // Cosine, so the beam lingers at each end the way a real lens turns
            // away and back.
            float phase = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / Mathf.Max(0.1f, sweepPeriod));
            beam.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(sweepFrom, sweepTo, phase));
        }

        if (lampGlow != null)
        {
            float breath = 0.8f + 0.2f * Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, glowPeriod));
            Color c = lampGlow.color;
            c.a = glowAlpha * breath;
            lampGlow.color = c;
        }
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// <summary>Starts a fresh campaign at night 1, clearing saved progress.</summary>
    public void StartNew()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("No GameManager in the Boot scene; nothing can start.", this);
            return;
        }

        GameManager.Instance.ResetProgress();
        GameManager.Instance.StartNight(0);
    }

    /// <summary>Resumes at the furthest night the player has reached.</summary>
    public void Continue()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.StartNight(GameManager.Instance.UnlockedNight - 1);
    }
}
