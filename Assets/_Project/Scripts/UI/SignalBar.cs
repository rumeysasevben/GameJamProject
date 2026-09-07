using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual for the signal the player is entering. Four slots fill as symbols
/// arrive, pulse green on a match, and fade out when the bar clears.
/// Purely presentational: it listens to <see cref="SignalInput"/> and owns no
/// game logic of its own.
/// </summary>
public class SignalBar : MonoBehaviour
{
    [SerializeField] private SignalInput signalInput;

    [Tooltip("One Image per slot, left to right. Should match SignalInput.MaxSymbols.")]
    [SerializeField] private Image[] symbolSlots = new Image[SignalInput.MaxSymbols];

    [Header("Colors")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color shortColor = new Color(1f, 0.95f, 0.8f, 1f);
    [SerializeField] private Color longColor = new Color(1f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color glowColor = new Color(0f, 1f, 0f, 0.7f);

    [Header("Timing")]
    [SerializeField] private float fillDuration = 0.2f;
    [SerializeField] private float glowInDuration = 0.1f;
    [SerializeField] private float glowOutDuration = 0.2f;
    [SerializeField] private float clearFadeDuration = 0.15f;

    private int filledCount;
    private Color[] slotBaseColors;
    private Coroutine[] slotRoutines;
    private Coroutine glowRoutine;

    private void Awake()
    {
        slotBaseColors = new Color[symbolSlots.Length];
        slotRoutines = new Coroutine[symbolSlots.Length];
        ResetAllSlots();
    }

    private void OnEnable()
    {
        // Unassigned in the Inspector is the usual cause of "clicks do nothing":
        // the events fire, but with no listener attached. Fall back to the one
        // in the scene rather than silently doing nothing.
        if (signalInput == null)
        {
            signalInput = FindFirstObjectByType<SignalInput>();
        }

        if (signalInput == null)
        {
            Debug.LogWarning($"{nameof(SignalBar)} has no {nameof(SignalInput)} assigned; the bar will not respond to input.", this);
            return;
        }

        signalInput.OnSymbolAdded += HandleSymbolAdded;
        signalInput.OnBarCleared += HandleBarCleared;
    }

    private void OnDisable()
    {
        if (signalInput == null)
        {
            return;
        }

        signalInput.OnSymbolAdded -= HandleSymbolAdded;
        signalInput.OnBarCleared -= HandleBarCleared;
    }

    /// <summary>
    /// Flashes the filled slots green. Called on a successful match — nothing
    /// in this class decides when that is.
    /// </summary>
    public void PlayGlow()
    {
        if (filledCount == 0)
        {
            return;
        }

        StopGlow();
        glowRoutine = StartCoroutine(GlowRoutine());
    }

    /// <summary>Fills the next free slot and pops it into view.</summary>
    private void HandleSymbolAdded(SignalSymbol symbol)
    {
        if (filledCount >= symbolSlots.Length)
        {
            return;
        }

        int index = filledCount;
        filledCount++;

        Image slot = symbolSlots[index];
        if (slot == null)
        {
            return;
        }

        slotBaseColors[index] = symbol == SignalSymbol.Short ? shortColor : longColor;
        slot.color = slotBaseColors[index];
        StartSlotRoutine(index, FillRoutine(slot));
    }

    /// <summary>Fades every slot out and returns the bar to empty.</summary>
    private void HandleBarCleared()
    {
        // The bar is logically empty at once, so the next symbol fills slot 0.
        filledCount = 0;

        // A clear straight after a match would cut the green glow off mid-pulse.
        // Let it play out, then wipe.
        if (glowRoutine != null)
        {
            StartCoroutine(FadeAfterGlow());
            return;
        }

        FadeAllSlots();
    }

    /// <summary>Waits for a running glow to finish before wiping the slots.</summary>
    private IEnumerator FadeAfterGlow()
    {
        while (glowRoutine != null)
        {
            yield return null;
        }

        // The player may have started a new signal while the glow played; those
        // symbols are not ours to erase.
        if (filledCount == 0)
        {
            FadeAllSlots();
        }
    }

    private void FadeAllSlots()
    {
        for (int i = 0; i < symbolSlots.Length; i++)
        {
            if (symbolSlots[i] != null)
            {
                StartSlotRoutine(i, FadeOutRoutine(i));
            }
        }
    }

    /// <summary>Scales a slot from nothing up to full size.</summary>
    private IEnumerator FillRoutine(Image slot)
    {
        Transform slotTransform = slot.transform;
        float elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fillDuration);
            slotTransform.localScale = Vector3.one * t;
            yield return null;
        }

        slotTransform.localScale = Vector3.one;
    }

    /// <summary>Fades a slot's alpha to zero, then resets it to the empty look.</summary>
    private IEnumerator FadeOutRoutine(int index)
    {
        Image slot = symbolSlots[index];
        Color from = slot.color;
        float elapsed = 0f;

        while (elapsed < clearFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / clearFadeDuration);
            slot.color = new Color(from.r, from.g, from.b, Mathf.Lerp(from.a, 0f, t));
            yield return null;
        }

        ResetSlot(index);
    }

    /// <summary>Pulses the filled slots toward the glow colour and back.</summary>
    private IEnumerator GlowRoutine()
    {
        int glowingCount = filledCount;
        float elapsed = 0f;

        while (elapsed < glowInDuration)
        {
            elapsed += Time.deltaTime;
            ApplyGlow(glowingCount, Mathf.Clamp01(elapsed / glowInDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < glowOutDuration)
        {
            elapsed += Time.deltaTime;
            ApplyGlow(glowingCount, 1f - Mathf.Clamp01(elapsed / glowOutDuration));
            yield return null;
        }

        ApplyGlow(glowingCount, 0f);
        glowRoutine = null;
    }

    /// <summary>Blends the first <paramref name="count"/> slots toward the glow colour.</summary>
    private void ApplyGlow(int count, float amount)
    {
        for (int i = 0; i < count && i < symbolSlots.Length; i++)
        {
            if (symbolSlots[i] != null)
            {
                symbolSlots[i].color = Color.Lerp(slotBaseColors[i], glowColor, amount);
            }
        }
    }

    /// <summary>
    /// Runs a coroutine for one slot, replacing whatever that slot was doing so
    /// two animations never fight over the same Image.
    /// </summary>
    private void StartSlotRoutine(int index, IEnumerator routine)
    {
        StopGlow();

        if (slotRoutines[index] != null)
        {
            StopCoroutine(slotRoutines[index]);
        }

        slotRoutines[index] = StartCoroutine(routine);
    }

    private void StopGlow()
    {
        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }
    }

    private void ResetAllSlots()
    {
        for (int i = 0; i < symbolSlots.Length; i++)
        {
            ResetSlot(i);
        }

        filledCount = 0;
    }

    /// <summary>Returns one slot to the empty look, with no animation.</summary>
    private void ResetSlot(int index)
    {
        slotBaseColors[index] = emptyColor;

        Image slot = symbolSlots[index];
        if (slot == null)
        {
            return;
        }

        slot.color = emptyColor;
        slot.transform.localScale = Vector3.one;
    }
}
