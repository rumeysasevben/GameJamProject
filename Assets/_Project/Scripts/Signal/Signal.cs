/// <summary>
/// One symbol of a signal. Short is a left click, Long a right click.
///
/// Deliberately a bare enum: a sequence is just a <c>Signal[]</c>, which the
/// Inspector edits without help and which compares element by element.
/// </summary>
public enum Signal
{
    Short,
    Long
}
