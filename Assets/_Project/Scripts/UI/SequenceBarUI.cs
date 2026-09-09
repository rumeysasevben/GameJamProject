using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The four slots at the bottom of the screen showing what the player has
/// flashed, and the line counting down to the buffer being dropped.
///
/// Contains no rules. It listens to <see cref="SignalBuffer"/> and draws what it
/// is told, which is why the timeout can be retuned or the match logic rewritten
/// without touching a line of this file.
/// </summary>
public class SequenceBarUI : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("The four slot frames, left to right.")]
    [SerializeField] private Image[] slots = new Image[4];

    [Tooltip("The symbol drawn inside each slot, in the same order.")]
    [SerializeField] private Image[] symbols = new Image[4];

    [Header("Sprites")]
    [SerializeField] private Sprite shortSprite;
    [SerializeField] private Sprite longSprite;

    [Tooltip("Frame drawn for a slot that has not been filled yet.")]
    [SerializeField] private Sprite slotEmptySprite;

    [Tooltip("Frame drawn for a slot holding a symbol.")]
    [SerializeField] private Sprite slotFilledSprite;

    [Tooltip("Frame drawn for the moment a sequence matches.")]
    [SerializeField] private Sprite slotSuccessSprite;

    [Header("Timeout")]
    [Tooltip("Underline drawn with fillAmount as the timeout runs down.")]
    [SerializeField] private Image timeoutLine;

    [Header("Feedback")]
    [Tooltip("Colour the slots flash on a correct sequence.")]
    [SerializeField] private Color successColor = new Color(1f, 0.85f, 0.5f);

    [Tooltip("How long the success flash holds before the slots empty.")]
    [SerializeField] private float successHold = 0.3f;

    [Tooltip("How long the slots take to fade out when the sequence is dropped.")]
    [SerializeField] private float fadeOut = 0.25f;

    private SignalBuffer buffer;
    private Coroutine clearRoutine;

    /// <summary>Attaches the bar to a buffer and draws its current state.</summary>
    public void Bind(SignalBuffer signalBuffer)
    {
        if (buffer != null)
        {
            buffer.OnChanged -= HandleChanged;
            buffer.OnCleared -= HandleCleared;
        }

        buffer = signalBuffer;

        if (buffer != null)
        {
            buffer.OnChanged += HandleChanged;
            buffer.OnCleared += HandleCleared;
        }

        HandleChanged();
    }

    private void OnDestroy()
    {
        if (buffer != null)
        {
            buffer.OnChanged -= HandleChanged;
            buffer.OnCleared -= HandleCleared;
        }
    }

    private void Update()
    {
        if (timeoutLine != null && buffer != null)
        {
            timeoutLine.fillAmount = buffer.TimeoutFraction;
        }
    }

    private void HandleChanged()
    {
        if (clearRoutine != null)
        {
            StopCoroutine(clearRoutine);
            clearRoutine = null;
        }

        Redraw();

        // A newly filled slot pops, so a fast player can still count what they
        // have entered without looking straight at the bar.
        int filled = buffer != null ? buffer.Count : 0;
        if (filled > 0 && filled <= symbols.Length && symbols[filled - 1] != null)
        {
            StartCoroutine(Pop(symbols[filled - 1].rectTransform));
        }
    }

    private void HandleCleared(ClearReason reason)
    {
        if (clearRoutine != null)
        {
            StopCoroutine(clearRoutine);
        }

        clearRoutine = StartCoroutine(reason == ClearReason.Success ? SuccessFlash() : FadeAway());
    }

    private void Redraw()
    {
        int count = buffer != null ? buffer.Count : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            bool filled = i < count;

            if (symbols.Length > i && symbols[i] != null)
            {
                symbols[i].enabled = filled;
                symbols[i].color = Color.white;

                if (filled)
                {
                    symbols[i].sprite = buffer.Symbols[i] == Signal.Short ? shortSprite : longSprite;
                }
            }

            if (slots[i] != null)
            {
                slots[i].color = Color.white;

                Sprite frame = filled ? slotFilledSprite : slotEmptySprite;
                if (frame != null)
                {
                    slots[i].sprite = frame;
                }
            }
        }
    }

    private IEnumerator Pop(RectTransform target)
    {
        const float duration = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(0.8f, 1f, elapsed / duration);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    private IEnumerator SuccessFlash()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].color = successColor;

                if (slotSuccessSprite != null)
                {
                    slots[i].sprite = slotSuccessSprite;
                }
            }

            if (symbols.Length > i && symbols[i] != null)
            {
                symbols[i].color = successColor;
            }
        }

        yield return new WaitForSeconds(successHold);

        Redraw();
        clearRoutine = null;
    }

    private IEnumerator FadeAway()
    {
        float elapsed = 0f;

        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOut);

            for (int i = 0; i < symbols.Length; i++)
            {
                if (symbols[i] != null)
                {
                    Color c = symbols[i].color;
                    symbols[i].color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            yield return null;
        }

        Redraw();
        clearRoutine = null;
    }
}
