using System;
using UnityEngine;

/// <summary>
/// The player's settings from the pause panel, kept across sessions.
///
/// Static rather than a component because they are read from everywhere —
/// the audio, the beam, every ship's bubble — and belong to none of them.
/// Anything that shows a setting listens to <see cref="Changed"/>, so flipping
/// a switch with the panel open is visible on the harbour straight away.
///
/// Values are written to PlayerPrefs as they change but only flushed to disk by
/// <see cref="Flush"/>, when the panel closes: on WebGL every save is an
/// IndexedDB write, and a slider being dragged changes its value every frame.
/// </summary>
public static class GameSettings
{
    private const string MusicKey = "fener.settings.music";
    private const string SfxKey = "fener.settings.sfx";
    private const string SequencesKey = "fener.settings.showSequences";
    private const string BeamWidthKey = "fener.settings.beamWidth";

    private static bool loaded;
    private static bool musicOn = true;
    private static bool sfxOn = true;
    private static bool showSequences;
    private static float beamWidth = 0.5f;

    /// <summary>Raised whenever any setting changes.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Survives an editor play session with domain reload switched off.
        loaded = false;
        Changed = null;
    }

    public static bool MusicOn
    {
        get { Load(); return musicOn; }
        set { Load(); if (musicOn == value) return; musicOn = value; Store(MusicKey, value ? 1 : 0); }
    }

    public static bool SfxOn
    {
        get { Load(); return sfxOn; }
        set { Load(); if (sfxOn == value) return; sfxOn = value; Store(SfxKey, value ? 1 : 0); }
    }

    /// <summary>Keep every ship's code on screen, not only while the beam is on it.</summary>
    public static bool ShowSequences
    {
        get { Load(); return showSequences; }
        set { Load(); if (showSequences == value) return; showSequences = value; Store(SequencesKey, value ? 1 : 0); }
    }

    /// <summary>Beam width as the slider sees it, 0 narrow to 1 wide. 0.5 is the tuned width.</summary>
    public static float BeamWidth
    {
        get { Load(); return beamWidth; }
        set
        {
            Load();
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(beamWidth, clamped)) return;

            beamWidth = clamped;
            PlayerPrefs.SetFloat(BeamWidthKey, clamped);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// What the beam's tuned half-angle is multiplied by: 0.6 at narrowest, 1
    /// in the middle, 1.5 at widest. Wider is easier, since the cone is also
    /// the hit test.
    /// </summary>
    public static float BeamWidthMultiplier
    {
        get
        {
            float v = BeamWidth;
            return v < 0.5f
                ? Mathf.Lerp(0.6f, 1f, v / 0.5f)
                : Mathf.Lerp(1f, 1.5f, (v - 0.5f) / 0.5f);
        }
    }

    /// <summary>Writes the settings to disk.</summary>
    public static void Flush()
    {
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        musicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
        sfxOn = PlayerPrefs.GetInt(SfxKey, 1) == 1;
        showSequences = PlayerPrefs.GetInt(SequencesKey, 0) == 1;
        beamWidth = PlayerPrefs.GetFloat(BeamWidthKey, 0.5f);
    }

    private static void Store(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        Changed?.Invoke();
    }
}
