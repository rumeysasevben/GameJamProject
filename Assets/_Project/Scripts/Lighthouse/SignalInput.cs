using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns mouse clicks into the signal the player is echoing back.
/// Left click adds a Short symbol, right click adds a Long one. The bar clears
/// itself if the player stalls, or if they overrun its four slots.
/// Holds input state only — it does no matching. See MatchResolver for that.
/// </summary>
public class SignalInput : MonoBehaviour
{
    /// <summary>Number of slots the bar shows; a signal is never longer than this.</summary>
    public const int MaxSymbols = 4;

    [Tooltip("Seconds of silence after a symbol before the bar clears itself.")]
    [SerializeField] private float symbolTimeout = 2f;

    [Header("Debug")]
    [Tooltip("Log every click and every symbol raised, plus how many listeners are attached.")]
    [SerializeField] private bool logInput;

    private readonly List<SignalSymbol> currentSymbols = new List<SignalSymbol>();
    private float timeSinceLastSymbol;

    /// <summary>Raised after a symbol is appended, with the symbol that was added.</summary>
    public event Action<SignalSymbol> OnSymbolAdded;

    /// <summary>Raised whenever the bar goes from holding symbols to empty.</summary>
    public event Action OnBarCleared;

    /// <summary>
    /// A snapshot of what the player has entered so far. Copied on each access,
    /// so holding on to it will not track later input.
    /// </summary>
    public Signal CurrentSignal => new Signal(currentSymbols);

    /// <summary>How many symbols are currently on the bar.</summary>
    public int Count => currentSymbols.Count;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (logInput)
            {
                Debug.Log("Input detected: Left", this);
            }

            AddSymbol(SignalSymbol.Short);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (logInput)
            {
                Debug.Log("Input detected: Right", this);
            }

            AddSymbol(SignalSymbol.Long);
        }

        TickTimeout();
    }

    /// <summary>
    /// Empties the bar and notifies listeners. Safe to call when already empty,
    /// in which case nothing is raised.
    /// </summary>
    public void Clear()
    {
        if (currentSymbols.Count == 0)
        {
            return;
        }

        currentSymbols.Clear();
        timeSinceLastSymbol = 0f;
        OnBarCleared?.Invoke();
    }

    /// <summary>
    /// Appends a symbol. Overrunning the bar's slots is read as a wrong answer:
    /// the extra symbol is discarded and the bar clears instead.
    /// </summary>
    private void AddSymbol(SignalSymbol symbol)
    {
        if (currentSymbols.Count >= MaxSymbols)
        {
            Clear();
            return;
        }

        currentSymbols.Add(symbol);
        timeSinceLastSymbol = 0f;

        if (logInput)
        {
            int listeners = OnSymbolAdded?.GetInvocationList().Length ?? 0;
            Debug.Log($"Symbol added: {symbol} (bar now {currentSymbols.Count}), listeners: {listeners}", this);

            if (listeners == 0)
            {
                Debug.LogWarning("Nobody is listening to OnSymbolAdded — check the SignalInput references on SignalBar and MatchResolver.", this);
            }
        }

        OnSymbolAdded?.Invoke(symbol);
    }

    /// <summary>Clears the bar once the player has been silent for too long.</summary>
    private void TickTimeout()
    {
        if (currentSymbols.Count == 0)
        {
            return;
        }

        timeSinceLastSymbol += Time.deltaTime;
        if (timeSinceLastSymbol >= symbolTimeout)
        {
            Clear();
        }
    }
}
