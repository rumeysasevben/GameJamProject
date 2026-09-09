using UnityEngine;

/// <summary>
/// The nights in play order. <see cref="GameManager"/> holds one of these, so
/// cutting or reordering the campaign is an Inspector edit.
/// </summary>
[CreateAssetMenu(fileName = "NightList", menuName = "Fener/NightList")]
public class NightList : ScriptableObject
{
    [Tooltip("Nights in the order they are played.")]
    public NightData[] nights = new NightData[0];

    /// <summary>How many nights the campaign has.</summary>
    public int Count => nights == null ? 0 : nights.Length;

    /// <summary>The night at <paramref name="index"/>, or null if the index is out of range.</summary>
    public NightData Get(int index)
    {
        if (nights == null || index < 0 || index >= nights.Length)
        {
            return null;
        }

        return nights[index];
    }
}
