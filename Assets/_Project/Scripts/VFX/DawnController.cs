using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sunrise, and the way back into the dark.
///
/// The payoff of a night, and the only part of the game that plays itself:
/// once the last ship is home, the player has nothing left to do but watch the
/// harbour they lit come out of the dark.
///
/// Everything is driven off one normalised time, so the light, the sea and the
/// stars move together and the whole thing retimes from a single number in
/// <see cref="GameConfig"/>.
///
/// It also owns putting the night back. Dawn leaves the scene in daylight with
/// the beam switched off, and nights reuse the one scene — so without
/// <see cref="ResetToNight"/> the second night starts in broad daylight with no
/// lighthouse. The night values are read once at startup rather than written
/// down twice, so retinting the scene needs no edit here.
/// </summary>
public class DawnController : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("The scene's Global Light 2D. Its colour and intensity are what actually make it morning.")]
    [SerializeField] private Light2D globalLight;

    [Tooltip("The sea sprite, tinted along with the light.")]
    [SerializeField] private SpriteRenderer sea;

    [Tooltip("The beam's light. Faded out and switched off — by sunrise nobody needs it.")]
    [SerializeField] private Light2D beamLight;

    [Tooltip("The drawn cone, faded out with the light so the beam does not hang in a bright sky.")]
    [SerializeField] private SpriteRenderer beamCone;

    [Tooltip("The star field, faded to nothing. Driven through its own visibility rather than by writing star colours, so the twinkle keeps running underneath.")]
    [SerializeField] private Starfield starfield;

    [Header("Gradients")]
    // Public because Gradient cannot be written through SerializedProperty, and
    // the scene builder has to be able to set these up.
    [Tooltip("Night to dawn to day, sampled over the whole sequence.")]
    public Gradient lightGradient;

    [Tooltip("Sea tint over the same span.")]
    public Gradient seaGradient;

    [Tooltip("Global light intensity over the same span.")]
    [SerializeField] private AnimationCurve intensity = AnimationCurve.Linear(0f, 0.35f, 1f, 1f);

    private Color nightLightColor;
    private float nightLightIntensity;
    private Color nightSeaColor;
    private float nightBeamIntensity;
    private Color nightBeamConeColor;
    private bool nightCaptured;

    private Coroutine playing;

    /// <summary>Raised once the sequence has finished, so the night can hand over to the summary screen.</summary>
    public event Action OnDawnComplete;

    /// <summary>True while the sunrise is running.</summary>
    public bool IsDawning { get; private set; }

    /// <summary>
    /// Jumps to full daylight and finishes.
    ///
    /// The sunrise is the reward for a night, but by the fifth one the player
    /// has seen it and wants the next night. Watching it has to stay a choice,
    /// which means it has to be skippable — not shorter still.
    /// </summary>
    public void Skip()
    {
        if (!IsDawning)
        {
            return;
        }

        if (playing != null)
        {
            StopCoroutine(playing);
            playing = null;
        }

        IsDawning = false;

        ApplyDay();
        IsDawning = false;
        OnDawnComplete?.Invoke();
    }

    private void Awake()
    {
        CaptureNight();
    }

    /// <summary>
    /// Remembers how the scene looks at night, so dawn can be undone exactly.
    /// Taken once, before the first sunrise has touched anything.
    /// </summary>
    private void CaptureNight()
    {
        if (nightCaptured)
        {
            return;
        }

        nightLightColor = globalLight != null ? globalLight.color : Color.white;
        nightLightIntensity = globalLight != null ? globalLight.intensity : 0.35f;
        nightSeaColor = sea != null ? sea.color : Color.white;
        nightBeamIntensity = beamLight != null ? beamLight.intensity : 1.2f;
        nightBeamConeColor = beamCone != null ? beamCone.color : Color.white;
        nightCaptured = true;
    }

    /// <summary>Runs the sunrise over <paramref name="duration"/> seconds.</summary>
    public void Play(float duration)
    {
        CaptureNight();

        if (playing != null)
        {
            StopCoroutine(playing);
        }

        IsDawning = true;
        playing = StartCoroutine(PlayRoutine(duration));
    }

    /// <summary>
    /// Darkens the sky back down over <paramref name="duration"/> seconds.
    /// Called as each night is set up: a cut straight from daylight to night
    /// reads as a glitch, while watching the light drain out reads as evening
    /// coming on.
    ///
    /// The beam comes back at once rather than fading in — the player can
    /// already be signalling, and a lighthouse that takes four seconds to
    /// answer its own switch feels broken.
    /// </summary>
    public void ReturnToNight(float duration)
    {
        CaptureNight();

        if (playing != null)
        {
            StopCoroutine(playing);
            playing = null;
        }

        IsDawning = false;

        RestoreBeam();

        if (duration <= 0f || !isActiveAndEnabled)
        {
            ResetToNight();
            return;
        }

        playing = StartCoroutine(DuskRoutine(duration));
    }

    /// <summary>
    /// Puts the scene back to night, at once. Used when there is no time for
    /// an evening — entering a night straight from the editor, or from the
    /// menu.
    /// </summary>
    public void ResetToNight()
    {
        CaptureNight();

        if (playing != null)
        {
            StopCoroutine(playing);
            playing = null;
        }

        IsDawning = false;

        if (globalLight != null)
        {
            globalLight.color = nightLightColor;
            globalLight.intensity = nightLightIntensity;
        }

        if (sea != null)
        {
            sea.color = nightSeaColor;
        }

        if (beamLight != null)
        {
            beamLight.gameObject.SetActive(true);
            beamLight.enabled = true;
            beamLight.intensity = nightBeamIntensity;
        }

        if (beamCone != null)
        {
            beamCone.gameObject.SetActive(true);
            beamCone.color = nightBeamConeColor;
        }

        if (starfield != null)
        {
            starfield.Visibility = 1f;
        }
    }

    /// <summary>Puts the beam back the way it is at night, at once.</summary>
    private void RestoreBeam()
    {
        if (beamLight != null)
        {
            beamLight.gameObject.SetActive(true);
            beamLight.enabled = true;
            beamLight.intensity = nightBeamIntensity;
        }

        if (beamCone != null)
        {
            beamCone.gameObject.SetActive(true);
            beamCone.color = nightBeamConeColor;
        }
    }

    /// <summary>
    /// Evening: whatever the sky and sea are now, eased down to their night
    /// values. Reads the current colours rather than assuming daylight, so an
    /// interrupted sunrise still darkens from where it got to.
    /// </summary>
    private IEnumerator DuskRoutine(float duration)
    {
        Color fromLight = globalLight != null ? globalLight.color : nightLightColor;
        float fromIntensity = globalLight != null ? globalLight.intensity : nightLightIntensity;
        Color fromSea = sea != null ? sea.color : nightSeaColor;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Eased, so the last of the daylight lingers and the drop into
            // dark is not a straight line.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (globalLight != null)
            {
                globalLight.color = Color.Lerp(fromLight, nightLightColor, t);
                globalLight.intensity = Mathf.Lerp(fromIntensity, nightLightIntensity, t);
            }

            if (sea != null)
            {
                sea.color = Color.Lerp(fromSea, nightSeaColor, t);
            }

            if (starfield != null)
            {
                starfield.Visibility = t;
            }

            yield return null;
        }

        ResetToNight();
    }

    private IEnumerator PlayRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (globalLight != null)
            {
                globalLight.color = lightGradient.Evaluate(t);
                globalLight.intensity = intensity.Evaluate(t);
            }

            if (sea != null)
            {
                sea.color = seaGradient.Evaluate(t);
            }

            // The beam goes out well before the end: a lit beam against a
            // bright sky reads as a mistake rather than as a light still doing
            // a job.
            float beamFade = 1f - Mathf.Clamp01(t * 2f);

            if (beamLight != null)
            {
                beamLight.intensity = nightBeamIntensity * beamFade;
            }

            if (beamCone != null)
            {
                beamCone.color = new Color(
                    nightBeamConeColor.r,
                    nightBeamConeColor.g,
                    nightBeamConeColor.b,
                    nightBeamConeColor.a * beamFade);
            }

            if (starfield != null)
            {
                starfield.Visibility = 1f - t;
            }

            yield return null;
        }

        ApplyDay();

        playing = null;
        IsDawning = false;
        OnDawnComplete?.Invoke();
    }

    /// <summary>Full daylight, beam out. Where the sunrise ends, whether it was watched or skipped.</summary>
    private void ApplyDay()
    {
        if (globalLight != null)
        {
            globalLight.color = lightGradient.Evaluate(1f);
            globalLight.intensity = intensity.Evaluate(1f);
        }

        if (sea != null)
        {
            sea.color = seaGradient.Evaluate(1f);
        }

        if (starfield != null)
        {
            starfield.Visibility = 0f;
        }

        if (beamLight != null)
        {
            beamLight.gameObject.SetActive(false);
        }

        if (beamCone != null)
        {
            beamCone.gameObject.SetActive(false);
        }
    }
}
