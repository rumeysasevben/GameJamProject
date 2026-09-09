using UnityEngine;

/// <summary>
/// The heart of the game: the one place a player's flashes are compared to a
/// ship's request.
///
/// No other script decides whether a signal matched. Everything else feeds this
/// one — the router says what was clicked, the beam says who is being addressed,
/// the buffer says what has been said so far — and this class does the only
/// thing that matters with it.
///
/// A flash with nobody in the beam is still a flash: the light plays, the
/// buffer fills, nothing happens. Sweeping the beam over empty water and
/// signalling into it has to feel allowed, or the player never explores.
/// </summary>
public class SignalMatcher : MonoBehaviour
{
    [Tooltip("Shared tuning asset; supplies the buffer's maximum length.")]
    [SerializeField] private GameConfig config;

    [Tooltip("What the player has flashed so far.")]
    [SerializeField] private SignalBuffer buffer;

    private Beam beam;
    private NightController night;
    private InputRouter router;

    /// <summary>Wires the matcher to the systems it reads. Called by <see cref="NightController"/> on wake.</summary>
    public void Configure(GameConfig gameConfig, SignalBuffer signalBuffer, Beam lighthouseBeam, NightController nightController, InputRouter inputRouter)
    {
        if (router != null)
        {
            router.OnShort -= HandleShort;
            router.OnLong -= HandleLong;
        }

        config = gameConfig;
        buffer = signalBuffer;
        beam = lighthouseBeam;
        night = nightController;
        router = inputRouter;

        if (router != null)
        {
            router.OnShort += HandleShort;
            router.OnLong += HandleLong;
        }
    }

    private void OnDestroy()
    {
        if (router != null)
        {
            router.OnShort -= HandleShort;
            router.OnLong -= HandleLong;
        }
    }

    private void HandleShort()
    {
        Handle(Signal.Short);
    }

    private void HandleLong()
    {
        Handle(Signal.Long);
    }

    private void Handle(Signal symbol)
    {
        if (buffer == null || beam == null || night == null)
        {
            return;
        }

        // Once the night is won, clicks belong to the sunrise: they skip it
        // rather than flashing a beam that is going out anyway.
        if (night.IsNightOver)
        {
            return;
        }

        beam.Flash(symbol);
        buffer.Push(symbol);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(symbol == Signal.Short ? "ui_short" : "ui_long");
        }

        Ship target = beam.GetTargetShip(night.Ships);

        // One symbol past the longest sequence any ship can ask for: the player
        // has lost the thread, so give them a clean bar rather than letting them
        // type into something that can never match.
        int max = config != null ? config.bufferMax : 4;
        if (buffer.Count > max)
        {
            buffer.Clear(ClearReason.Overflow);
            return;
        }

        if (target == null || target.Sequence == null)
        {
            return;
        }

        // Only judged once the player has said as many symbols as the ship
        // asked for. A half-typed sequence is not yet wrong.
        if (buffer.Count != target.Sequence.Length)
        {
            return;
        }

        if (buffer.Matches(target.Sequence))
        {
            night.Link(target);
            buffer.Clear(ClearReason.Success);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play("link_success");
            }
        }
        else
        {
            buffer.Clear(ClearReason.Mismatch);

            if (target.Emitter != null)
            {
                target.Emitter.ShowNow();
            }

            // The ship asking again, rather than a buzzer. Getting it wrong is
            // a misheard message, and the game should not tell the player off
            // for it.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayAt("ship_reply", target.Position);
            }
        }
    }
}
