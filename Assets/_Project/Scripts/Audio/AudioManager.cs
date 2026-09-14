using UnityEngine;

/// <summary>
/// Everything the game makes a noise with.
///
/// Lives in Boot and survives scene loads, like <see cref="GameManager"/>, so
/// the music does not restart between nights.
///
/// Three jobs, kept apart:
///
/// - one-shot effects, played through a small pool of sources so a burst of
///   collisions does not cut itself off;
/// - the beam hum, a single looping source faded up while the light is on a
///   ship — the game's only continuous sound, and the one that tells the
///   player they are pointing at something without saying so;
/// - the music, a base bed and instrument layers started together, scheduled
///   on the same audio-clock sample, and never restarted between nights. Each
///   ship brought home anywhere in the campaign swells the next layer in a
///   little further, so the harbour's music grows from a lone bed on the first
///   night to every instrument at once by the last. Started together is the
///   whole trick: bring a layer in late and it is out of time for good.
///
/// Nothing here fails loudly. A missing clip plays silence, because on a jam
/// the audio lands last and the game has to be playable before it does.
/// </summary>
public class AudioManager : MonoBehaviour
{
    /// <summary>The one instance, or null before Boot has run.</summary>
    public static AudioManager Instance { get; private set; }

    [Header("Library")]
    [SerializeField] private SfxLibrary library;

    [Header("Levels")]
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;

    [Header("Beam hum")]
    [Tooltip("Looping hum, up while the beam is on a ship.")]
    [SerializeField] private AudioClip humClip;

    [Tooltip("How loud the hum gets.")]
    [Range(0f, 1f)] [SerializeField] private float humVolume = 0.4f;

    [Tooltip("How quickly the hum comes up and goes away.")]
    [SerializeField] private float humFade = 3f;

    [Header("Music")]
    [Tooltip("The base bed and the instrument layers on top, all exactly the same length. Index 0 always plays; the rest come in with the campaign's progress, in order.")]
    [SerializeField] private AudioClip[] musicLayers = new AudioClip[0];

    [Tooltip("Seconds a layer takes to swell to its new level when a ship gets home. Slow, so it is felt rather than noticed.")]
    [SerializeField] private float layerFade = 4f;

    [Tooltip("How loud the layers sit against the base, each at full. Matched by index to the layers after the base; missing entries are 1.")]
    [SerializeField] private float[] layerGains = { 1f, 1f, 1f, 1f, 1f };

    [Header("Pool")]
    [Tooltip("How many one-shot effects can overlap.")]
    [SerializeField] private int voiceCount = 8;

    private AudioSource[] voices;
    private int nextVoice;

    private AudioSource hum;
    private float humTarget;

    private AudioSource[] layerSources;
    private float[] layerLevels;
    private bool musicStarted;

    // How far through the campaign the player is, 0 to 1. Drives the layers.
    private float musicProgress;

    // A little extra lift for the finale, on top of full progress.
    private float finaleBoost;

    /// <summary>Half the screen in world units. Used to pan a sound by where it happened.</summary>
    private const float HalfScreenWidth = 9.6f;

    // The levels after the pause panel's switches.
    private float SfxLevel => GameSettings.SfxOn ? sfxVolume : 0f;
    private float MusicLevel => GameSettings.MusicOn ? musicVolume : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildVoices();
        BuildHum();
        BuildMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (hum != null)
        {
            hum.volume = Mathf.MoveTowards(hum.volume, humTarget * SfxLevel, humFade * Time.unscaledDeltaTime * humVolume);
        }

        UpdateMusicLevels(Time.unscaledDeltaTime);
    }

    // ------------------------------------------------------------ Effects

    /// <summary>Plays a named sound, centred.</summary>
    public void Play(string id)
    {
        PlayInternal(id, 0f);
    }

    /// <summary>
    /// Plays a named sound panned to where it happened. On a foggy night, where
    /// a flash cannot be seen, this is how the player knows which side of the
    /// bay a ship is calling from.
    /// </summary>
    public void PlayAt(string id, Vector2 worldPosition)
    {
        PlayInternal(id, Mathf.Clamp(worldPosition.x / HalfScreenWidth, -1f, 1f));
    }

    private void PlayInternal(string id, float pan)
    {
        if (library == null || voices == null || SfxLevel <= 0f)
        {
            return;
        }

        SfxLibrary.Entry entry = library.Find(id);
        if (entry == null || entry.clips == null || entry.clips.Length == 0)
        {
            return;
        }

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (clip == null)
        {
            return;
        }

        // Round-robin rather than "find a free one": with a fixed pool the
        // oldest sound is the one that gets cut, which is what you want when
        // ships are grinding along a rock.
        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.Stop();
        voice.clip = clip;
        voice.volume = entry.volume * SfxLevel;
        voice.pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        voice.panStereo = pan;
        voice.Play();
    }

    // ------------------------------------------------------------ Hum

    /// <summary>Fades the beam hum up or down. Called every frame; only changes act.</summary>
    public void SetHum(bool on)
    {
        humTarget = on ? humVolume : 0f;
    }

    // ------------------------------------------------------------ Music

    /// <summary>
    /// Starts the music if it is not already running. Every layer is scheduled
    /// on the same audio-clock sample and left running, silent until progress
    /// opens it. Later nights call this too and simply carry on: the music
    /// does not restart between nights, it keeps growing.
    /// </summary>
    public void StartMusic()
    {
        if (musicStarted || layerSources == null || layerSources.Length == 0)
        {
            return;
        }

        musicStarted = true;

        // A short lead so every clip is ready and all of them begin on exactly
        // the same sample; Play() on each in turn can land a frame apart.
        double startAt = AudioSettings.dspTime + 0.2;

        for (int i = 0; i < layerSources.Length; i++)
        {
            AudioSource source = layerSources[i];
            if (source == null || source.clip == null)
            {
                continue;
            }

            source.volume = TargetLevel(i);
            layerLevels[i] = source.volume;
            source.PlayScheduled(startAt);
        }
    }

    /// <summary>
    /// How far through the campaign the player is, 0 to 1 — every ship home so
    /// far over every ship there is. Layers swell toward the new level.
    /// </summary>
    public void SetMusicProgress(float progress)
    {
        musicProgress = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// The last dawn: every layer open, and a little louder than the game
    /// has ever been, over <paramref name="seconds"/>.
    /// </summary>
    public void PlayFinale(float seconds)
    {
        musicProgress = 1f;
        StartCoroutine(RaiseFinale(Mathf.Max(0.1f, seconds)));
    }

    /// <summary>Back to the campaign's own level. For a new game from the ending.</summary>
    public void ResetFinale()
    {
        StopAllCoroutines();
        finaleBoost = 0f;
    }

    private System.Collections.IEnumerator RaiseFinale(float seconds)
    {
        float from = finaleBoost;
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            finaleBoost = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }

        finaleBoost = 1f;
    }

    /// <summary>
    /// Where layer <paramref name="index"/> should sit. The base is always
    /// on. The others open in turn across the campaign, each one fading
    /// through its own slice of progress, so every single ship home makes
    /// the sound a little fuller rather than nothing happening until a
    /// threshold is crossed.
    /// </summary>
    private float TargetLevel(int index)
    {
        float lift = 1f + 0.25f * finaleBoost;

        if (index == 0)
        {
            return Mathf.Min(1f, MusicLevel * lift);
        }

        int extras = layerSources.Length - 1;
        float opened = Mathf.Clamp01(musicProgress * extras - (index - 1));
        float gain = layerGains != null && index - 1 < layerGains.Length ? layerGains[index - 1] : 1f;

        // Eased, so a layer arrives softly and does not jump on its first ship.
        return Mathf.Min(1f, MusicLevel * gain * Mathf.SmoothStep(0f, 1f, opened) * lift);
    }

    private void UpdateMusicLevels(float deltaTime)
    {
        if (layerSources == null)
        {
            return;
        }

        float step = deltaTime / Mathf.Max(0.1f, layerFade);

        for (int i = 0; i < layerSources.Length; i++)
        {
            if (layerSources[i] == null)
            {
                continue;
            }

            layerLevels[i] = Mathf.MoveTowards(layerLevels[i], TargetLevel(i), step);
            layerSources[i].volume = layerLevels[i];
        }

        KeepLayersInStep();
    }

    /// <summary>
    /// Pulls any layer that has wandered back onto the base. The layers are cut
    /// to the base's length, but an MP3 can decode a few milliseconds longer or
    /// shorter than it measured, and a few milliseconds a loop adds up over a
    /// campaign. A correction of a twentieth of a second in a wash of reverb
    /// goes unheard; half a second of drift would not.
    /// </summary>
    private void KeepLayersInStep()
    {
        AudioSource bed = layerSources.Length > 0 ? layerSources[0] : null;
        if (bed == null || bed.clip == null || !bed.isPlaying)
        {
            return;
        }

        for (int i = 1; i < layerSources.Length; i++)
        {
            AudioSource layer = layerSources[i];
            if (layer == null || layer.clip == null || !layer.isPlaying)
            {
                continue;
            }

            // In seconds, not samples: the clips need not share a sample rate.
            float length = layer.clip.length;
            float wanted = Mathf.Repeat(bed.time, length);
            float drift = Mathf.Abs(layer.time - wanted);
            drift = Mathf.Min(drift, length - drift);

            if (drift > 0.05f)
            {
                layer.time = wanted;
            }
        }
    }

    // ------------------------------------------------------------ Setup

    private void BuildVoices()
    {
        voices = new AudioSource[Mathf.Max(1, voiceCount)];

        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject($"Voice_{i}");
            go.transform.SetParent(transform, false);

            // 2D: the game is one fixed screen, so distance attenuation would
            // only fight the deliberate panning above.
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    private void BuildHum()
    {
        var go = new GameObject("BeamHum");
        go.transform.SetParent(transform, false);

        hum = go.AddComponent<AudioSource>();
        hum.clip = humClip;
        hum.loop = true;
        hum.playOnAwake = false;
        hum.spatialBlend = 0f;
        hum.volume = 0f;

        if (humClip != null)
        {
            hum.Play();
        }
    }

    private void BuildMusic()
    {
        layerSources = new AudioSource[musicLayers != null ? musicLayers.Length : 0];
        layerLevels = new float[layerSources.Length];

        for (int i = 0; i < layerSources.Length; i++)
        {
            var go = new GameObject($"MusicLayer_{i}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = musicLayers[i];
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            layerSources[i] = source;

            // Loaded now, not on first play, so the scheduled start finds every
            // layer ready and none of them comes in late.
            if (source.clip != null)
            {
                source.clip.LoadAudioData();
            }
        }
    }
}
