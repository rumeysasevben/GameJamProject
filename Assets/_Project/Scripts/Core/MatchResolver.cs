using System;
using UnityEngine;

/// <summary>
/// The heart of the game: decides whether what the player tapped matches the
/// signal of the ship in the beam.
/// Every symbol the player enters runs through here, and matching lives here
/// and nowhere else. It owns no state — it only reacts to input.
/// </summary>
public class MatchResolver : MonoBehaviour
{
    [SerializeField] private SignalInput signalInput;
    [SerializeField] private LighthouseBeam lighthouseBeam;
    [SerializeField] private SignalBar signalBar;

    [Tooltip("Log every comparison to the console. Useful while tuning, noisy otherwise.")]
    [SerializeField] private bool logMatches = true;

    /// <summary>Raised on a correct answer, with the ship that bound.</summary>
    public event Action<Ship> OnMatch;

    /// <summary>Raised on a wrong answer, with the ship that was being answered.</summary>
    public event Action<Ship> OnMismatch;

    private void OnEnable()
    {
        // Unassigned references are the usual cause of "nothing happens when I
        // click": the input fires, but nothing is wired to hear it.
        if (signalInput == null)
        {
            signalInput = FindFirstObjectByType<SignalInput>();
        }

        if (lighthouseBeam == null)
        {
            lighthouseBeam = FindFirstObjectByType<LighthouseBeam>();
        }

        if (signalBar == null)
        {
            signalBar = FindFirstObjectByType<SignalBar>();
        }

        if (signalInput == null)
        {
            Debug.LogWarning($"{nameof(MatchResolver)} has no {nameof(SignalInput)} assigned; no matching will happen.", this);
            return;
        }

        signalInput.OnSymbolAdded += HandleSymbolAdded;
    }

    private void OnDisable()
    {
        if (signalInput != null)
        {
            signalInput.OnSymbolAdded -= HandleSymbolAdded;
        }
    }

    /// <summary>
    /// Runs on every symbol the player enters. Compares only once the bar is at
    /// least as long as the target's signal — before that there is nothing to
    /// judge, and the player is still mid-answer.
    /// </summary>
    private void HandleSymbolAdded(SignalSymbol symbol)
    {
        Ship target = lighthouseBeam != null ? lighthouseBeam.CurrentTarget : null;

        // No ship in the cone. A fine sweep of light, and nothing more.
        if (target == null)
        {
            return;
        }

        // A ship with no signal set has nothing to answer. Serialized signals
        // are never null, so an empty one is the real "not configured" case —
        // without this, every click would be judged a mismatch.
        if (target.Signal == null || target.Signal.Count == 0)
        {
            return;
        }

        // Still mid-answer; not time to compare yet.
        if (signalInput.Count < target.Signal.Count)
        {
            return;
        }

        Signal bar = signalInput.CurrentSignal;

        if (logMatches)
        {
            Debug.Log($"Checking match: bar {bar} (count {bar.Count}) vs target {target.Signal} (count {target.Signal.Count}) on {target.name}", target);
        }

        if (bar.Equals(target.Signal))
        {
            HandleMatch(target, bar);
        }
        else
        {
            HandleMismatch(target, bar);
        }
    }

    /// <summary>Binds the ship. The glow plays first, while the bar still has symbols to light.</summary>
    private void HandleMatch(Ship target, Signal bar)
    {
        if (logMatches)
        {
            Debug.Log($"MATCH DETECTED! Calling target.Bind() on {target.name}", target);
        }

        if (signalBar != null)
        {
            signalBar.PlayGlow();
        }

        target.Bind();

        if (logMatches)
        {
            Debug.Log($"Post-bind check: ship state is {target.State} (expected Bound)", target);
        }

        // An answered signal is finished. Leaving the symbols on the bar would
        // make them count toward the next ship's answer, so the following
        // signal could never match. The glow still plays — SignalBar lets it
        // finish before wiping the slots.
        signalInput.Clear();

        OnMatch?.Invoke(target);
    }

    /// <summary>Wipes the bar and asks the ship to show its signal again.</summary>
    private void HandleMismatch(Ship target, Signal bar)
    {
        if (logMatches)
        {
            Debug.Log($"MISMATCH! Clearing bar and replaying signal — {bar} against {target.Signal} on {target.name}", target);
        }

        signalInput.Clear();
        target.ReplaySignal();
        OnMismatch?.Invoke(target);
    }
}
