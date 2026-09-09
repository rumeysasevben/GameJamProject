using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the campaign: which night we are on, how far the player has got, and
/// the moves between the menu and the game.
///
/// Lives in the Boot scene and survives scene loads, so it is the only thing
/// that knows anything across a night boundary. It does not run a night — that
/// is <see cref="NightController"/>'s job, and this class only hands it the
/// data and waits to be told the night is over.
///
/// Nights reuse the one Game scene: when a night ends and the controller is
/// still alive, the next night is set up in place rather than reloaded. Loading
/// only happens on the way in from the menu.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>PlayerPrefs key holding the highest night the player has reached, counting from 1.</summary>
    private const string UnlockedNightKey = "fener.unlockedNight";

    private const string MenuSceneName = "Boot";
    private const string GameSceneName = "Game";

    /// <summary>The one instance. Null until the Boot scene has run its Awake.</summary>
    public static GameManager Instance { get; private set; }

    [Header("Data")]
    [Tooltip("The campaign, in play order.")]
    [SerializeField] private NightList nights;

    [Tooltip("Shared tuning asset, handed on to the night controller.")]
    [SerializeField] private GameConfig config;

    private NightController activeNight;

    /// <summary>Index into the night list of the night being played, counting from 0.</summary>
    public int CurrentNightIndex { get; private set; }

    /// <summary>The night being played, or null before one has started.</summary>
    public NightData CurrentNight => nights == null ? null : nights.Get(CurrentNightIndex);

    /// <summary>Shared tuning asset.</summary>
    public GameConfig Config => config;

    /// <summary>The campaign, in play order.</summary>
    public NightList Nights => nights;

    /// <summary>
    /// Highest night the player has reached, counting from 1. Used by the menu
    /// to decide whether to offer "Continue".
    /// </summary>
    public int UnlockedNight => PlayerPrefs.GetInt(UnlockedNightKey, 1);

    /// <summary>
    /// How many nights the player has finished. This is what the town's lit
    /// windows count, so it has to survive a return to the menu and a restart —
    /// which is why it comes from saved progress rather than a field.
    /// </summary>
    public int CompletedNights => Mathf.Max(0, UnlockedNight - 1);

    private void Awake()
    {
        // Boot can be re-entered (main menu, then a fresh Boot load); the first
        // instance stays and later ones remove themselves rather than fighting
        // over the static.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Called by <see cref="NightController"/> as it wakes up in the Game
    /// scene, so the manager can drive later nights without a scene load.
    /// </summary>
    public void RegisterNightController(NightController controller)
    {
        activeNight = controller;
    }

    /// <summary>Starts the night at <paramref name="index"/>, loading the Game scene if it is not already up.</summary>
    public void StartNight(int index)
    {
        if (nights == null || nights.Count == 0)
        {
            Debug.LogError("GameManager has no NightList; nothing to play.", this);
            return;
        }

        CurrentNightIndex = Mathf.Clamp(index, 0, nights.Count - 1);

        if (activeNight != null)
        {
            activeNight.Setup(CurrentNight);
            return;
        }

        SceneManager.LoadScene(GameSceneName);
    }

    /// <summary>True when the night being played is the last one in the list.</summary>
    public bool IsLastNight => nights == null || CurrentNightIndex + 1 >= nights.Count;

    /// <summary>
    /// Banks the current night.
    ///
    /// Called when the last ship berths, not when the player dismisses the
    /// summary: the night was won out on the water, and closing the game on
    /// the summary screen should not take it back.
    /// </summary>
    public void MarkNightComplete()
    {
        int reached = CurrentNightIndex + 2; // finished night N, so night N+1 is now open
        if (reached > UnlockedNight)
        {
            PlayerPrefs.SetInt(UnlockedNightKey, reached);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Moves on to the next night. Past the last one, the campaign is over and
    /// we return to the menu.
    /// </summary>
    public void NextNight()
    {
        MarkNightComplete();

        if (IsLastNight)
        {
            LoadMenu();
            return;
        }

        StartNight(CurrentNightIndex + 1);
    }

    /// <summary>Returns to the Boot scene and its menu.</summary>
    public void LoadMenu()
    {
        activeNight = null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuSceneName);
    }

    /// <summary>Clears saved progress. Used by the menu's "new game".</summary>
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(UnlockedNightKey);
        PlayerPrefs.Save();
    }
}
