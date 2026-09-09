using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Plays a waiting ship's sequence, over and over, until someone answers.
///
/// The flash is the ship asking to be let in, so it has to stay readable in
/// fog, at distance and out of the corner of the eye: a sprite for the lamp and
/// a small Light2D behind it, because on a foggy night the light is all that
/// shows through.
///
/// The coroutine is stopped the moment the ship stops being idle. Forgetting
/// that leaves a linked ship blinking as it sails, which reads as a second ship
/// still asking for help.
/// </summary>
public class ShipSignalEmitter : MonoBehaviour
{
    private Ship ship;
    private GameConfig config;
    private SequenceBubbleUI bubble;
    private SpriteRenderer lamp;
    private Light2D lampLight;
    private bool showSequenceAlways;

    private Coroutine loop;
    private bool replayNow;

    /// <summary>Wires the emitter to its ship and parts. Called from <see cref="Ship.Init"/>.</summary>
    public void Init(Ship owner, GameConfig gameConfig, SequenceBubbleUI sequenceBubble, SpriteRenderer lampRenderer, bool alwaysShowSequence)
    {
        ship = owner;
        config = gameConfig;
        bubble = sequenceBubble;
        lamp = lampRenderer;
        lampLight = lamp != null ? lamp.GetComponent<Light2D>() : null;
        showSequenceAlways = alwaysShowSequence;

        SetLamp(false);

        if (bubble != null && ship != null)
        {
            bubble.Configure(ship.Sequence, showSequenceAlways);
        }
    }

    /// <summary>Starts the flashing loop. Restarting an already running loop is a no-op.</summary>
    public void Play()
    {
        if (loop != null || ship == null || !isActiveAndEnabled)
        {
            return;
        }

        if (bubble != null)
        {
            bubble.SetSuppressed(false);
        }

        loop = StartCoroutine(Loop());
    }

    /// <summary>
    /// Stops flashing. <paramref name="lampLit"/> leaves the lamp burning
    /// steadily — a linked ship holds its light on, a berthed one goes dark.
    /// </summary>
    public void Stop(bool lampLit)
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }

        replayNow = false;
        SetLamp(lampLit);

        // A ship that has answered is no longer asking, so its bubble goes
        // away even if the beam is still on it.
        if (bubble != null)
        {
            bubble.SetFlashing(false);
            bubble.SetSuppressed(true);
        }
    }

    /// <summary>
    /// Replays the sequence at once instead of waiting out the interval. Used
    /// after a wrong answer, so the player gets to hear the question again
    /// rather than sitting in silence wondering what they missed.
    /// </summary>
    public void ShowNow()
    {
        replayNow = true;
    }

    private void OnDisable()
    {
        loop = null;
    }

    private IEnumerator Loop()
    {
        float interval = config != null ? config.signalInterval : 4f;

        while (true)
        {
            float waited = 0f;
            while (waited < interval && !replayNow)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            replayNow = false;
            yield return PlaySequence();
        }
    }

    private IEnumerator PlaySequence()
    {
        Signal[] sequence = ship != null ? ship.Sequence : null;
        if (sequence == null || sequence.Length == 0)
        {
            yield break;
        }

        if (bubble != null)
        {
            bubble.SetFlashing(true);
        }

        float shortFlash = config != null ? config.flashShort : 0.2f;
        float longFlash = config != null ? config.flashLong : 0.6f;
        float gap = config != null ? config.flashGap : 0.25f;

        for (int i = 0; i < sequence.Length; i++)
        {
            SetLamp(true);

            // Panned to where the ship is: on a foggy night the sound is how
            // the player knows which side of the bay is calling.
            if (AudioManager.Instance != null && ship != null)
            {
                AudioManager.Instance.PlayAt(
                    sequence[i] == Signal.Short ? "ship_flash_short" : "ship_flash_long",
                    ship.Position);
            }

            yield return new WaitForSeconds(sequence[i] == Signal.Short ? shortFlash : longFlash);

            SetLamp(false);
            yield return new WaitForSeconds(gap);
        }

        if (bubble != null)
        {
            bubble.SetFlashing(false);
        }
    }

    private void SetLamp(bool lit)
    {
        if (lamp != null)
        {
            lamp.enabled = lit;
        }

        if (lampLight != null)
        {
            lampLight.enabled = lit;
        }
    }
}
