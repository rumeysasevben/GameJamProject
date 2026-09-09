using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The town's record of the whole game: one window lit for every night the
/// player has brought home, and never put out again.
///
/// This is the only score the game keeps in front of the player, and it is
/// deliberately not a number. How far you have got is how much of the hillside
/// is awake, and lighting the last window is the ending.
///
/// The count is not stored here. It is derived from the campaign's progress
/// every time a night is set up, so a reload, a return to the menu or a
/// session picked up days later all show the same lit town.
///
/// Houses are lit from the waterfront inland, so the light spreads up the hill
/// rather than appearing in scattered spots.
/// </summary>
public class TownLights : MonoBehaviour
{
    [Tooltip("The houses, in the order they light up.")]
    [SerializeField] private List<WindowLight> houses = new List<WindowLight>();

    /// <summary>How many houses the town has — and so how many nights it takes to finish the game.</summary>
    public int Count => houses.Count;

    /// <summary>How many are lit.</summary>
    public int LitCount { get; private set; }

    /// <summary>True once every window is lit: the town is whole and the game is over.</summary>
    public bool AllLit => LitCount >= houses.Count;

    /// <summary>Fills the town when it is built in code.</summary>
    public void Bind(IEnumerable<WindowLight> windows)
    {
        houses = new List<WindowLight>(windows);
        LitCount = 0;
    }

    /// <summary>
    /// Shows <paramref name="count"/> houses already lit, with no fade. Called
    /// as a night is set up, from how many nights have been finished — so the
    /// windows earned on earlier nights are simply still on.
    /// </summary>
    public void SetLit(int count)
    {
        LitCount = Mathf.Clamp(count, 0, houses.Count);

        for (int i = 0; i < houses.Count; i++)
        {
            if (houses[i] == null)
            {
                continue;
            }

            if (i < LitCount)
            {
                houses[i].TurnOn(instant: true);
            }
            else
            {
                houses[i].TurnOff();
            }
        }
    }

    /// <summary>
    /// Lights the next house, slowly, as the reward for finishing a night.
    /// Returns false when the town is already whole.
    /// </summary>
    public bool TurnOnNext()
    {
        if (LitCount >= houses.Count)
        {
            return false;
        }

        WindowLight house = houses[LitCount];
        LitCount++;

        if (house == null)
        {
            return false;
        }

        house.TurnOn(instant: false);
        return true;
    }
}
