using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The brain of a night: builds it from a <see cref="NightData"/>, runs the
/// frame, and closes it when the last ship is home.
///
/// Every system is driven from this one Update in a fixed order — input, aim,
/// buffer, ships sailing in, targeting, steering, collision, berthing — rather
/// than each running its own. Script execution order then stops mattering at all, and a
/// bug is always somewhere in a list you can read top to bottom.
///
/// Only one ship is ever linked. Taking hold of a new one drops the old one
/// where it floats, which is the game's central tension: you cannot escort them
/// all at once, only one at a time, in the order you choose.
/// </summary>
public class NightController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Shared tuning asset. Handed on to every system that needs it.")]
    [SerializeField] private GameConfig config;

    [Tooltip("Played on Start when there is no GameManager — lets the Game scene be entered directly while working.")]
    [SerializeField] private NightData debugNight;

    [Header("Systems")]
    [SerializeField] private InputRouter router;
    [SerializeField] private Lighthouse lighthouse;
    [SerializeField] private SignalBuffer buffer;
    [SerializeField] private SignalMatcher matcher;
    [SerializeField] private CollisionSystem collisions;
    [SerializeField] private CoastCollider coast;

    [Header("Scene")]
    [Tooltip("Parent the night's ships are spawned under.")]
    [SerializeField] private Transform shipsParent;

    [Tooltip("Parent the night's rocks are spawned under.")]
    [SerializeField] private Transform rocksParent;

    [Tooltip("Every dock in the scene, in index order. Nights switch off the ones they do not use.")]
    [SerializeField] private Dock[] docks = new Dock[0];

    [Tooltip("The fog layer, switched on for the nights that call for it.")]
    [SerializeField] private GameObject fog;

    [Header("Prefabs")]
    [SerializeField] private Ship shipPrefab;
    [SerializeField] private Rock rockPrefab;

    [Header("UI")]
    [SerializeField] private SequenceBarUI sequenceBar;
    [SerializeField] private NightTitleUI nightTitle;
    [SerializeField] private NoteCardUI noteCard;

    [Tooltip("The persistent \"Night 3 / 10\" in the corner.")]
    [SerializeField] private NightCounterUI nightCounter;

    [Tooltip("The panel shown once the sun is up.")]
    [SerializeField] private NightSummaryUI summary;

    [Tooltip("The sunrise screen after the last night.")]
    [SerializeField] private EndingUI ending;

    [Tooltip("The pause panel. The night does not run its frame while it is open.")]
    [SerializeField] private PauseMenuUI pauseMenu;

    [Header("Sequence")]
    [SerializeField] private DawnController dawn;

    [Tooltip("The town's windows. One more comes on with every night that gets finished.")]
    [SerializeField] private TownLights town;

    [Tooltip("The water thrown up when a ship hits something.")]
    [SerializeField] private SplashVFX splash;

    [Tooltip("The sun and gulls that come with each sunrise, and fill the sky on the last one.")]
    [SerializeField] private DawnSky dawnSky;

    private readonly List<Ship> ships = new List<Ship>();
    private readonly List<Rock> rocks = new List<Rock>();

    private NightData night;
    private Ship linkedShip;
    private Ship targetShip;
    private int dockedCount;
    private bool nightOver;

    /// <summary>Raised when a ship answers the lighthouse.</summary>
    public event Action<Ship> OnShipLinked;

    /// <summary>Raised when a ship reaches its berth.</summary>
    public event Action<Ship> OnShipDocked;

    /// <summary>Raised once every ship is home, before dawn plays.</summary>
    public event Action OnAllDocked;

    /// <summary>The ships in play tonight, docked ones included.</summary>
    public IReadOnlyList<Ship> Ships => ships;

    /// <summary>The ship currently following the cursor, or null.</summary>
    public Ship LinkedShip => linkedShip;

    /// <summary>The idle ship in the beam, or null.</summary>
    public Ship TargetShip => targetShip;

    /// <summary>True once every ship is home. Signals stop being read at that point.</summary>
    public bool IsNightOver => nightOver;

    /// <summary>True while a night is being played and can be paused.</summary>
    public bool CanPause => night != null && !nightOver;

    /// <summary>Seconds since the night began. Shown on the summary screen.</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>How many collisions the player has had tonight. Shown on the summary screen.</summary>
    public int CollisionCount { get; private set; }

    private void Awake()
    {
        if (buffer != null)
        {
            buffer.Configure(config);
        }

        if (matcher != null)
        {
            matcher.Configure(config, buffer, lighthouse != null ? lighthouse.Beam : null, this, router);
        }

        if (lighthouse != null && lighthouse.Beam != null)
        {
            lighthouse.Beam.Configure(config);
        }

        if (sequenceBar != null)
        {
            sequenceBar.Bind(buffer);
        }

        if (collisions != null)
        {
            collisions.OnCollision += HandleCollision;
        }

        if (dawn != null)
        {
            dawn.OnDawnComplete += HandleDawnComplete;
        }

        // A click during the sunrise skips it. Subscribed alongside the
        // matcher, which is harmless: with the night over there is nothing left
        // for a signal to match.
        if (router != null)
        {
            router.OnShort += SkipDawn;
            router.OnLong += SkipDawn;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterNightController(this);
            Setup(GameManager.Instance.CurrentNight);
            return;
        }

        // Entered the Game scene directly, which is how the night is worked on
        // in the editor. Fall back to whatever is wired up for debugging.
        Setup(debugNight);
    }

    private void OnDestroy()
    {
        if (collisions != null)
        {
            collisions.OnCollision -= HandleCollision;
        }

        if (dawn != null)
        {
            dawn.OnDawnComplete -= HandleDawnComplete;
        }

        if (router != null)
        {
            router.OnShort -= SkipDawn;
            router.OnLong -= SkipDawn;
        }
    }

    private void SkipDawn()
    {
        if (dawn != null && dawn.IsDawning)
        {
            dawn.Skip();
        }
    }

    /// <summary>
    /// Tears down whatever was on screen and builds <paramref name="data"/> in
    /// its place. Nights reuse the one scene, so this is the only thing that
    /// happens between them.
    /// </summary>
    public void Setup(NightData data)
    {
        if (data == null)
        {
            Debug.LogError("NightController.Setup was handed no night.", this);
            return;
        }

        night = data;
        linkedShip = null;
        targetShip = null;
        dockedCount = 0;
        nightOver = false;
        ElapsedTime = 0f;
        CollisionCount = 0;

        ClearSpawned();
        SetupDocks(data);
        SpawnRocks(data);
        SpawnShips(data);

        // Dawn left the scene in daylight with the beam switched off. Evening
        // brings it back down — over a few seconds, so the change of night
        // reads as time passing rather than as a cut.
        if (dawn != null)
        {
            dawn.ReturnToNight(config != null ? config.duskDuration : 4f);
        }

        if (dawnSky != null)
        {
            dawnSky.Hide(config != null ? config.duskDuration : 4f);
        }

        if (fog != null)
        {
            fog.SetActive(data.fogEnabled);
        }

        if (buffer != null)
        {
            buffer.Clear(ClearReason.Timeout);
        }

        if (summary != null)
        {
            summary.Hide();
        }

        if (ending != null)
        {
            ending.Hide();
        }

        // The windows are the campaign's record, not the night's: every house
        // earned on an earlier night is still lit when this one begins.
        if (town != null)
        {
            town.SetLit(GameManager.Instance != null ? GameManager.Instance.CompletedNights : 0);
        }

        if (nightTitle != null)
        {
            nightTitle.Show(data.nightNumber, config != null ? config.nightTitleDuration : 1.5f);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play("night_title");
            AudioManager.Instance.ResetFinale();
            AudioManager.Instance.SetMusicProgress(MusicProgress());
            AudioManager.Instance.StartMusic();
        }

        if (nightCounter != null)
        {
            int total = GameManager.Instance != null && GameManager.Instance.Nights != null
                ? GameManager.Instance.Nights.Count
                : 0;

            nightCounter.Show(data.nightNumber, total);
        }
    }

    private void Update()
    {
        if (night == null)
        {
            return;
        }

        // Paused: nothing runs, and no click on the panel reaches the matcher.
        if (pauseMenu != null && pauseMenu.IsOpen)
        {
            return;
        }

        // The night is won and the sun is coming up. Input is still read, so a
        // click can cut the sunrise short, but nothing else runs.
        if (nightOver)
        {
            if (router != null)
            {
                router.Poll();
            }

            return;
        }

        float dt = Time.deltaTime;
        ElapsedTime += dt;

        // 1. Input. Raises the click events, which run the matcher.
        if (router != null)
        {
            router.Poll();
        }

        Vector2 cursor = router != null ? router.CursorWorld : Vector2.zero;

        // 2. Aim.
        if (lighthouse != null)
        {
            lighthouse.Aim(cursor);
        }

        // 3. The buffer's timeout.
        if (buffer != null)
        {
            buffer.Tick(dt);
        }

        // 4. Ships still sailing in from open sea.
        TickArrivals(dt);

        // 5. Who is being addressed.
        UpdateTarget();

        // 6. Steering, for the one linked ship.
        if (linkedShip != null)
        {
            linkedShip.Tick(dt, cursor);
        }

        // 7. Collision, also for the one linked ship.
        if (collisions != null)
        {
            collisions.Tick(linkedShip, ships, rocks, docks, coast);
        }

        // 8. Berthing.
        CheckArrival();

        // 9. The view leans toward the harbour as the escorted ship closes on it.
        UpdateCameraLean();
    }

    /// <summary>
    /// Takes hold of <paramref name="ship"/>. Whatever was linked before is let
    /// go and starts asking again from where it was left.
    /// </summary>
    public void Link(Ship ship)
    {
        if (ship == null || ship.State == ShipState.Docked)
        {
            return;
        }

        if (linkedShip != null && linkedShip != ship)
        {
            linkedShip.SetIdle();
        }

        linkedShip = ship;
        ship.SetLinked();
        OnShipLinked?.Invoke(ship);
    }

    /// <summary>
    /// Moves whatever is still sailing in. Each ship drops out of this the
    /// moment it takes up its station and starts flashing.
    /// </summary>
    private void TickArrivals(float deltaTime)
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null && ships[i].State == ShipState.Arriving)
            {
                ships[i].TickEntry(deltaTime);
            }
        }
    }

    private void UpdateTarget()
    {
        Beam beam = lighthouse != null ? lighthouse.Beam : null;
        targetShip = beam != null ? beam.GetTargetShip(ships) : null;

        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                ships[i].SetTargeted(ships[i] == targetShip);
            }
        }

        // The hum is the game's only continuous sound, and it says "you are
        // pointing at someone" without a word of UI.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetHum(targetShip != null);
        }
    }

    private void CheckArrival()
    {
        if (linkedShip == null)
        {
            return;
        }

        Dock berth = GetDock(linkedShip.DockIndex);
        if (berth == null || !berth.Active || !berth.Contains(linkedShip.Position))
        {
            return;
        }

        Ship arrived = linkedShip;
        linkedShip = null;

        arrived.SetDocked(berth);
        dockedCount++;

        if (noteCard != null)
        {
            noteCard.Show(arrived.Note, config != null ? config.noteDuration : 3f);
        }

        // Every ship home, on any night, swells the next instrument in a little
        // further: the music grows richer across the whole campaign.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAt("dock_arrive", berth.Position);
            AudioManager.Instance.SetMusicProgress(MusicProgress());
        }

        OnShipDocked?.Invoke(arrived);

        if (dockedCount >= ships.Count)
        {
            FinishNight();
        }
    }

    private void FinishNight()
    {
        nightOver = true;
        ParallaxCamera.SetLean(Vector2.zero, 0f);
        OnAllDocked?.Invoke();

        // Nothing left to point at, and the frame loop stops running from here,
        // so the hum has to be told to go now.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetHum(false);
        }

        // Banked here, out on the water, rather than when the summary is
        // dismissed: the night has been won, and closing the game on the
        // summary screen should not take it back.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.MarkNightComplete();
        }

        // One more window, and it stays on for the rest of the game. Lighting
        // the last one is the ending.
        if (town != null)
        {
            town.TurnOnNext();
        }

        // The last night's sunrise is the ending, and is given the time for it:
        // the lighthouse going out slowly, the sun climbing, the gulls
        // gathering and every instrument playing at once.
        bool isFinalNight = GameManager.Instance == null || GameManager.Instance.IsLastNight;
        float sunrise = config != null
            ? (isFinalNight ? Mathf.Max(config.dawnDuration, config.finaleDuration) : config.dawnDuration)
            : (isFinalNight ? 11f : 7f);

        if (dawnSky != null)
        {
            dawnSky.Play(sunrise, isFinalNight);
        }

        if (isFinalNight && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFinale(sunrise);
        }

        if (dawn != null)
        {
            dawn.Play(sunrise);
            return;
        }

        HandleDawnComplete();
    }

    /// <summary>
    /// The sun is up. The summary waits for a click rather than moving on by
    /// itself — dawn is the point of the night, and cutting away from it on a
    /// timer would take that moment from the player.
    /// </summary>
    private void HandleDawnComplete()
    {
        bool isFinalNight = GameManager.Instance == null || GameManager.Instance.IsLastNight;
        int nightNumber = night != null ? night.nightNumber : 1;

        if (isFinalNight && ending != null)
        {
            ending.Show(nightNumber, PlayAgain, () => Advance(true));
            return;
        }

        if (summary == null)
        {
            Advance(isFinalNight);
            return;
        }

        // Tonight's window was lit as the last ship berthed, so the town's count
        // already includes it; the card saves that one for the end of its row.
        int windowsLit = town != null ? town.LitCount : nightNumber;
        int windowsTotal = town != null
            ? town.Count
            : GameManager.Instance != null && GameManager.Instance.Nights != null ? GameManager.Instance.Nights.Count : nightNumber;

        summary.Show(
            nightNumber,
            ships.Count,
            windowsLit,
            windowsTotal,
            ElapsedTime,
            CollisionCount,
            isFinalNight,
            () => Advance(isFinalNight));
    }

    /// <summary>From the ending: wipe the campaign and start again from the first night.</summary>
    private void PlayAgain()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.ResetProgress();
        GameManager.Instance.StartNight(0);
    }

    private void Advance(bool isFinalNight)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        if (isFinalNight)
        {
            GameManager.Instance.LoadMenu();
            return;
        }

        GameManager.Instance.NextNight();
    }

    private void HandleCollision(Vector2 point)
    {
        CollisionCount++;

        if (splash != null)
        {
            splash.Play(point);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAt("collide_splash", point);
            AudioManager.Instance.PlayAt("collide_oops", point);
        }
    }

    /// <summary>
    /// Eases the view toward the harbour while the escorted ship draws near its
    /// berth — nothing while it is far out, the full lean as it comes alongside —
    /// and lets it settle back once nobody is being brought in.
    /// </summary>
    private void UpdateCameraLean()
    {
        Dock berth = linkedShip != null ? GetDock(linkedShip.DockIndex) : null;
        if (berth == null || !berth.Active)
        {
            ParallaxCamera.SetLean(Vector2.zero, 0f);
            return;
        }

        const float farDistance = 6f;
        float distance = Vector2.Distance(linkedShip.Position, berth.Position);
        float closeness = 1f - Mathf.Clamp01(distance / farDistance);

        // Toward the harbour from the middle of the screen, which is where the
        // camera is placed.
        ParallaxCamera.SetLean(berth.Position, closeness);
    }

    /// <summary>The campaign's progress with tonight's ships home so far. Without a GameManager, just tonight's.</summary>
    private float MusicProgress()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.CampaignProgress(dockedCount);
        }

        return ships.Count > 0 ? dockedCount / (float)ships.Count : 0f;
    }

    private Dock GetDock(int index)
    {
        if (docks == null || index < 0 || index >= docks.Length)
        {
            return null;
        }

        return docks[index];
    }

    private void SetupDocks(NightData data)
    {
        bool showStrips = data.docks != null && data.docks.Length > 1;

        for (int i = 0; i < docks.Length; i++)
        {
            if (docks[i] == null)
            {
                continue;
            }

            if (data.docks != null && i < data.docks.Length)
            {
                docks[i].Setup(data.docks[i], i, showStrips);
            }
            else
            {
                docks[i].SetActive(false);
            }
        }
    }

    private void SpawnRocks(NightData data)
    {
        if (rockPrefab == null || data.rocks == null)
        {
            return;
        }

        for (int i = 0; i < data.rocks.Length; i++)
        {
            Rock rock = Instantiate(rockPrefab, rocksParent);
            rock.Setup(data.rocks[i]);
            rocks.Add(rock);
        }
    }

    private void SpawnShips(NightData data)
    {
        if (shipPrefab == null || data.ships == null)
        {
            return;
        }

        for (int i = 0; i < data.ships.Length; i++)
        {
            Ship ship = Instantiate(shipPrefab, shipsParent);

            // Staggered, so a night opens with boats coming in one after
            // another rather than a formation crossing the edge together.
            float delay = i * (config != null ? config.entryStagger : 0.5f);
            ship.Init(data.ships[i], config, data.showSequenceAlways, delay);
            ships.Add(ship);
        }
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null)
            {
                Destroy(ships[i].gameObject);
            }
        }

        for (int i = 0; i < rocks.Count; i++)
        {
            if (rocks[i] != null)
            {
                Destroy(rocks[i].gameObject);
            }
        }

        ships.Clear();
        rocks.Clear();
    }
}
