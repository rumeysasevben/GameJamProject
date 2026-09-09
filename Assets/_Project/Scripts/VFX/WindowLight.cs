using System.Collections;
using UnityEngine;

/// <summary>
/// One house, dark until a ship it was waiting for comes home.
///
/// Two drawings stacked — the dark house and the lit one — with the lit one
/// faded in over the top. A crossfade rather than a swap because the light
/// coming on slowly is the point: it reads as someone inside getting up to
/// open a door, not as a state toggling.
///
/// No Light2D by default. A dozen point lights for a dozen windows is the
/// cheapest way to lose frames on a laptop, and at this size the lit sprite
/// carries it on its own.
/// </summary>
public class WindowLight : MonoBehaviour
{
    [Tooltip("The house with its windows dark. Always visible; the lit one fades in over it.")]
    [SerializeField] private SpriteRenderer darkBody;

    [Tooltip("The same house with its windows lit, faded in when this one comes on.")]
    [SerializeField] private SpriteRenderer litBody;

    [Tooltip("Soft glow around the house, faded in with the windows. What makes a lit house read from across the bay.")]
    [SerializeField] private SpriteRenderer glow;

    [Tooltip("How bright the glow gets. Well under 1 — it is a hint of light in the air, not a lamp.")]
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.45f;

    [Tooltip("How long the windows take to come up.")]
    [SerializeField] private float fadeDuration = 0.6f;

    private Coroutine fade;

    /// <summary>True once this house has been lit tonight.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Wires the drawings when the town is built in code.</summary>
    public void Bind(SpriteRenderer dark, SpriteRenderer lit, SpriteRenderer halo)
    {
        darkBody = dark;
        litBody = lit;
        glow = halo;
    }

    /// <summary>
    /// Brings the windows up. <paramref name="instant"/> skips the fade, for
    /// houses that were already lit on an earlier night and are only being
    /// restored. Lighting an already lit house does nothing.
    /// </summary>
    public void TurnOn(bool instant = false)
    {
        if (IsOn)
        {
            return;
        }

        IsOn = true;

        if (instant || !isActiveAndEnabled)
        {
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }

            SetAlpha(1f);
            return;
        }

        if (fade != null)
        {
            StopCoroutine(fade);
        }

        // Only when it actually happens in front of the player — the instant
        // path above is for windows restored from earlier nights, and a dozen
        // of those at once would be a mess.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAt("window_on", transform.position);
        }

        fade = StartCoroutine(FadeIn());
    }

    /// <summary>Puts the house back to dark at once. Used when a night is set up.</summary>
    public void TurnOff()
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        IsOn = false;
        SetAlpha(0f);
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(1f);
        fade = null;
    }

    private void SetAlpha(float alpha)
    {
        if (litBody != null)
        {
            Color color = litBody.color;
            litBody.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (glow != null)
        {
            Color color = glow.color;
            glow.color = new Color(color.r, color.g, color.b, alpha * glowAlpha);
        }
    }
}
