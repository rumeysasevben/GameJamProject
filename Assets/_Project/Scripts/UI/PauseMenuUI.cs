using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The pause panel, after the "Mola" mock-up: music, sound effects, whether the
/// ships' codes stay on screen, and how wide the beam is.
///
/// Opened by the small button in the top-right corner, or Escape / P. Pausing
/// sets the time scale to zero, which freezes everything the night runs on —
/// ships, flashes, the buffer's timeout, the fog — and the night controller
/// skips its frame while the panel is open, so no click on the panel is ever
/// read as a signal. The panel itself animates on unscaled time.
///
/// Pausing is only offered while a night is actually being played: once the
/// last ship is home the sunrise and the summary card take over.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private NightController night;

    [Header("Corner button")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private CanvasGroup pauseButtonGroup;

    [Header("Panel")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image veil;
    [SerializeField] private RectTransform card;
    [SerializeField, Range(0f, 1f)] private float veilAlpha = 0.5f;
    [SerializeField] private float openDuration = 0.25f;

    [Header("Settings")]
    [SerializeField] private UIToggle musicToggle;
    [SerializeField] private UIToggle sfxToggle;
    [SerializeField] private UIToggle sequencesToggle;
    [SerializeField] private Slider beamSlider;
    [SerializeField] private TMP_Text beamValueLabel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;

    private bool open;
    private float openAmount;
    private float buttonAlpha = 1f;

    /// <summary>True while the panel is up and the game is frozen.</summary>
    public bool IsOpen => open;

    private void Awake()
    {
        if (pauseButton != null) pauseButton.onClick.AddListener(Open);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (menuButton != null) menuButton.onClick.AddListener(ToMenu);

        if (musicToggle != null) musicToggle.OnChanged += on => GameSettings.MusicOn = on;
        if (sfxToggle != null) sfxToggle.OnChanged += on => GameSettings.SfxOn = on;
        if (sequencesToggle != null) sequencesToggle.OnChanged += on => GameSettings.ShowSequences = on;

        if (beamSlider != null)
        {
            beamSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.BeamWidth = v;
                RefreshBeamLabel();
            });
        }

        SyncControls();
        openAmount = 0f;
        ApplyPanel();
    }

    private void OnDestroy()
    {
        // Never leave the game frozen behind a scene change.
        if (open)
        {
            Time.timeScale = 1f;
            GameSettings.Flush();
        }
    }

    private void Update()
    {
        bool canPause = night == null || night.CanPause;
        float dt = Time.unscaledDeltaTime;

        if (open)
        {
            if (TogglePressed())
            {
                Close();
            }
        }
        else if (canPause && TogglePressed())
        {
            Open();
        }

        // The corner button fades away while there is nothing to pause, and
        // hides while the panel covers it.
        float buttonTarget = canPause && !open ? 1f : 0f;
        buttonAlpha = Mathf.MoveTowards(buttonAlpha, buttonTarget, dt * 5f);

        if (pauseButtonGroup != null)
        {
            pauseButtonGroup.alpha = buttonAlpha;
            pauseButtonGroup.interactable = buttonTarget > 0f;
            pauseButtonGroup.blocksRaycasts = buttonTarget > 0f;
        }

        openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, dt / Mathf.Max(0.01f, openDuration));
        ApplyPanel();
    }

    /// <summary>Freezes the night and brings the panel up.</summary>
    public void Open()
    {
        if (open || (night != null && !night.CanPause))
        {
            return;
        }

        open = true;
        Time.timeScale = 0f;

        // The night stops running its frame, so nothing else will turn the hum off.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetHum(false);
        }

        SyncControls();
    }

    /// <summary>Puts the panel away and lets the night run again.</summary>
    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        Time.timeScale = 1f;
        GameSettings.Flush();
    }

    private void ToMenu()
    {
        Close();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMenu();
        }
    }

    private void ApplyPanel()
    {
        float eased = UIEase.OutCubic(openAmount);

        if (group != null)
        {
            group.alpha = eased;
            group.interactable = open;
            group.blocksRaycasts = open;
        }

        if (veil != null)
        {
            Color c = veil.color;
            veil.color = new Color(c.r, c.g, c.b, veilAlpha * eased);
        }

        if (card != null)
        {
            float scale = open
                ? Mathf.LerpUnclamped(0.88f, 1f, UIEase.OutBack(openAmount))
                : Mathf.Lerp(0.94f, 1f, eased);

            card.localScale = Vector3.one * scale;
        }
    }

    private void SyncControls()
    {
        if (musicToggle != null) musicToggle.SetWithoutNotify(GameSettings.MusicOn, true);
        if (sfxToggle != null) sfxToggle.SetWithoutNotify(GameSettings.SfxOn, true);
        if (sequencesToggle != null) sequencesToggle.SetWithoutNotify(GameSettings.ShowSequences, true);
        if (beamSlider != null) beamSlider.SetValueWithoutNotify(GameSettings.BeamWidth);

        RefreshBeamLabel();
    }

    private void RefreshBeamLabel()
    {
        if (beamValueLabel == null)
        {
            return;
        }

        float v = GameSettings.BeamWidth;
        beamValueLabel.text = v < 0.34f ? "narrow" : v > 0.66f ? "wide" : "medium";
    }

    private static bool TogglePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);
    }
}
