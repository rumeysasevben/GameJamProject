using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Boot scene's menu.
///
/// It exists for two reasons. One is the obvious one — new game, continue. The
/// other is that browsers refuse to play audio until the page has been clicked,
/// so the game must never open straight into a night: the button press here is
/// what buys the first sound.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Starts the campaign from night 1.")]
    [SerializeField] private Button startButton;

    [Tooltip("Resumes at the furthest night reached. Hidden until there is something to resume.")]
    [SerializeField] private Button continueButton;

    private void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartNew);
        }

        if (continueButton != null)
        {
            bool hasProgress = GameManager.Instance != null && GameManager.Instance.UnlockedNight > 1;
            continueButton.gameObject.SetActive(hasProgress);
            continueButton.onClick.AddListener(Continue);
        }
    }

    /// <summary>Starts a fresh campaign at night 1, clearing saved progress.</summary>
    public void StartNew()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("No GameManager in the Boot scene; nothing can start.", this);
            return;
        }

        GameManager.Instance.ResetProgress();
        GameManager.Instance.StartNight(0);
    }

    /// <summary>Resumes at the furthest night the player has reached.</summary>
    public void Continue()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.StartNight(GameManager.Instance.UnlockedNight - 1);
    }
}
