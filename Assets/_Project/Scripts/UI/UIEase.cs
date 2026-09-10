using UnityEngine;

/// <summary>
/// The easing curves the panels are animated with.
///
/// The panels describe their whole entrance as a function of time since they
/// opened, rather than as a chain of coroutines. That makes skipping trivial —
/// jump the clock to the end and every element lands where it belongs — and
/// these curves are the vocabulary those functions are written in.
/// </summary>
public static class UIEase
{
    /// <summary>How far through a step starting at <paramref name="start"/> the clock is, 0 to 1.</summary>
    public static float Progress(float time, float start, float duration)
    {
        if (duration <= 0f)
        {
            return time >= start ? 1f : 0f;
        }

        return Mathf.Clamp01((time - start) / duration);
    }

    /// <summary>Fast out, gentle landing.</summary>
    public static float OutCubic(float p)
    {
        float q = 1f - Mathf.Clamp01(p);
        return 1f - q * q * q;
    }

    /// <summary>Overshoots a little and settles back — the "pop".</summary>
    public static float OutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float q = Mathf.Clamp01(p) - 1f;
        return 1f + c3 * q * q * q + c1 * q * q;
    }

    /// <summary>A scale that swells and returns to 1 over the step. 1 before and after it.</summary>
    public static float Pop(float p, float strength)
    {
        if (p <= 0f || p >= 1f)
        {
            return 1f;
        }

        return 1f + strength * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.35f);
    }
}
