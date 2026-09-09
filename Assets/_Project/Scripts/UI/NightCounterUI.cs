using TMPro;
using UnityEngine;

/// <summary>
/// The small "Night 3 / 10" in the corner, up for the whole night.
///
/// Separate from <see cref="NightTitleUI"/>, which announces a night and then
/// gets out of the way. This one stays, because knowing how far through the
/// town's story you are is the difference between a night being the third of
/// ten and being one more night.
/// </summary>
public class NightCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    /// <summary>
    /// Shows which night is being played. <paramref name="totalNights"/> of
    /// zero or less drops the total, so the counter still reads correctly when
    /// a night is opened straight from the editor.
    /// </summary>
    public void Show(int nightNumber, int totalNights)
    {
        if (label == null)
        {
            return;
        }

        label.text = totalNights > 0
            ? $"Night {nightNumber} / {totalNights}"
            : $"Night {nightNumber}";
    }
}
