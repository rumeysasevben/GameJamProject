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
/// - the music, several layers started together and opened one at a time as
///   ships come home. Started together is the whole trick: bring a layer in
///   late and it is out of time for the rest of the night.
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
    [Tooltip("The base bed and the layers on top, all the same length and tempo. Index 0 plays from the start of a night.")]
    [SerializeField] private AudioClip[] musicLayers = new AudioClip[0];

    [Tooltip("How quickly a layer opens when a ship gets home.")]
    [SerializeField] private float layerFade = 1.5f;

    [Header("Pool")]
    [Tooltip("How many one-shot effects can overlap.")]
    [SerializeField] private int voiceCount = 8;

    private AudioSource[] voices;
    private int nextVoice;

    private AudioSource hum;
    private float humTarget;

    private AudioSource[] layerSources;
    private int openLayers;

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

        for (int i = 0; i < (layerSources != null ? layerSources.Length : 0); i++)
        {
            if (layerSources[i] == null)
            {
                continue;
            }

            float target = i < openLayers ? MusicLevel : 0f;
            layerSources[i].volume = Mathf.MoveTowards(layerSources[i].volume, target, layerFade * Time.unscaledDeltaTime * musicVolume);
        }
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
    /// Starts the night's music from the top, with only the base layer open.
    /// Every layer starts at once and stays running, silent until it is needed.
    /// </summary>
    public void StartMusic()
    {
        openLayers = 1;

        for (int i = 0; i < (layerSources != null ? layerSources.Length : 0); i++)
        {
            if (layerSources[i] == null || layerSources[i].clip == null)
            {
                continue;
            }

            layerSources[i].volume = i == 0 ? MusicLevel : 0f;
            layerSources[i].time = 0f;
            layerSources[i].Play();
        }
    }

    /// <summary>Opens one more layer. Called as each ship reaches its berth.</summary>
    public void AddMusicLayer()
    {
        if (layerSources == null)
        {
            return;
        }

        openLayers = Mathf.Min(openLayers + 1, layerSources.Length);
    }

    /// <summary>Closes everything but the base layer, for the start of a night.</summary>
    public void ResetMusicLayers()
    {
        openLayers = 1;
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
        }
    }
}
