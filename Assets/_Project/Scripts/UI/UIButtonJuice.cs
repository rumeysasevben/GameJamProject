using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a button feel pressable: it grows under the cursor, sinks when held,
/// glows while hovered and clicks when pressed.
///
/// Replaces Unity's colour tint, which only ever darkens the button and makes
/// a disabled one look broken while a panel is still animating in.
///
/// Panels that pop their buttons in drive <see cref="Appear"/>; this multiplies
/// it into its own scale, so the two never fight over the transform.
/// </summary>
public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Button button;

    [Tooltip("Soft light behind the button, faded in on hover. Optional.")]
    [SerializeField] private Graphic glow;

    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.94f;

    [Tooltip("Idle swell, for the one button a panel wants pressed. 0 keeps it still.")]
    [SerializeField, Range(0f, 0.1f)] private float breathe;

    [Tooltip("Sound played on click, from the SFX library. Empty for none.")]
    [SerializeField] private string clickSound = "ui_short";

    private bool hovered;
    private bool pressed;
    private float scale = 1f;
    private float glowAmount;
    private Color glowColor = Color.white;

    /// <summary>Entrance scale set by the owning panel, 0 to about 1.1.</summary>
    public float Appear { get; set; } = 1f;

    private void Awake()
    {
        if (glow != null)
        {
            glowColor = glow.color;
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }

        if (button != null)
        {
            button.onClick.AddListener(PlayClick);
        }
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = true;

    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    private void Update()
    {
        // Unscaled: panels are allowed to open over a paused game.
        float dt = Time.unscaledDeltaTime;
        bool live = button == null || button.interactable;

        float target = !live ? 1f : pressed ? pressScale : hovered ? hoverScale : 1f;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-18f * dt));

        float swell = live && breathe > 0f && !hovered
            ? 1f + breathe * Mathf.Sin(Time.unscaledTime * 2.4f)
            : 1f;

        transform.localScale = Vector3.one * (scale * swell * Appear);

        if (glow != null)
        {
            glowAmount = Mathf.Lerp(glowAmount, live && hovered ? 1f : 0f, 1f - Mathf.Exp(-12f * dt));
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * glowAmount * Mathf.Clamp01(Appear));
        }
    }

    private void PlayClick()
    {
        if (!string.IsNullOrEmpty(clickSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(clickSound);
        }
    }
}
