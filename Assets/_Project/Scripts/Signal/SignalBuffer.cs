using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a buffer emptied. The bar shows success differently from every other case.</summary>
public enum ClearReason
{
    /// <summary>The player stopped typing for longer than the timeout.</summary>
    Timeout,

    /// <summary>One symbol more than the longest possible sequence was entered.</summary>
    Overflow,

    /// <summary>The sequence matched the targeted ship.</summary>
    Success,

    /// <summary>The sequence was the right length but the wrong symbols.</summary>
    Mismatch
}

/// <summary>
/// What the player has flashed so far, and the countdown that throws it away.
///
/// Pure state: the buffer never looks at ships and never decides whether
/// anything matched. That is <see cref="SignalMatcher"/>'s job. The buffer only
/// knows how to fill up, time out and empty, which is why the bar UI can listen
/// to it without knowing any game rules.
/// </summary>
public class SignalBuffer : MonoBehaviour
{
    [Tooltip("Shared tuning asset; supplies the timeout.")]
    [SerializeField] private GameConfig config;

    private readonly List<Signal> symbols = new List<Signal>();

    /// <summary>Raised whenever a symbol is added.</summary>
    public event Action OnChanged;

    /// <summary>Raised when the buffer empties, with the reason it did.</summary>
    public event Action<ClearReason> OnCleared;

    /// <summary>The symbols entered so far, in order.</summary>
    public IReadOnlyList<Signal> Symbols => symbols;

    /// <summary>How many symbols are in the buffer.</summary>
    public int Count => symbols.Count;

    /// <summary>Seconds since the last symbol. Zero while the buffer is empty.</summary>
    public float IdleTimer { get; private set; }

    /// <summary>
    /// How much of the timeout is left, 1 down to 0. The bar draws its underline
    /// from this. Always 1 while the buffer is empty, so the line reads as full
    /// rather than spent.
    /// </summary>
    public float TimeoutFraction
    {
        get
        {
            if (symbols.Count == 0 || config == null || config.bufferTimeout <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(1f - (IdleTimer / config.bufferTimeout));
        }
    }

    /// <summary>Supplies the tuning asset when the buffer is wired up in code rather than the Inspector.</summary>
    public void Configure(GameConfig gameConfig)
    {
        config = gameConfig;
    }

    /// <summary>Appends a symbol and restarts the timeout.</summary>
    public void Push(Signal symbol)
    {
        symbols.Add(symbol);
        IdleTimer = 0f;
        OnChanged?.Invoke();
    }

    /// <summary>Advances the timeout. Call once a frame.</summary>
    public void Tick(float deltaTime)
    {
        if (symbols.Count == 0)
        {
            return;
        }

        IdleTimer += deltaTime;

        if (config != null && IdleTimer >= config.bufferTimeout)
        {
            Clear(ClearReason.Timeout);
        }
    }

    /// <summary>
    /// Empties the buffer and says why. Safe to call on an already empty
    /// buffer — the event still fires, so the bar can play its flash after a
    /// successful match even though nothing was left to remove.
    /// </summary>
    public void Clear(ClearReason reason)
    {
        symbols.Clear();
        IdleTimer = 0f;
        OnCleared?.Invoke(reason);
    }

    /// <summary>True when the buffer holds exactly <paramref name="sequence"/>, in order.</summary>
    public bool Matches(Signal[] sequence)
    {
        if (sequence == null || sequence.Length != symbols.Count)
        {
            return false;
        }

        for (int i = 0; i < sequence.Length; i++)
        {
            if (symbols[i] != sequence[i])
            {
                return false;
            }
        }

        return true;
    }
}
