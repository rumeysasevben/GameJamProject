using UnityEngine;

/// <summary>
/// Every tunable number in the game, in one asset.
///
/// There is exactly one instance, Assets/_Project/Data/GameConfig.asset. Systems
/// take a reference to it rather than holding their own copies of these values,
/// so a playtest tuning pass is a single Inspector edit and never a rebuild.
///
/// All distances are world units (1 unit = 100 px at PPU 100), all times are
/// seconds and all angles are degrees.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Fener/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Beam")]
    [Tooltip("Half the cone's opening angle. A ship counts as targeted when it sits within this many degrees of the beam's centre line.")]
    public float beamHalfAngle = 8f;

    [Tooltip("The furthest the beam can reach. The beam ends where the cursor is, so this only bites when pointing past it; it has to cover the furthest ship, about 14 units from the lamp.")]
    public float beamLength = 15f;

    [Tooltip("The shortest the beam gets, so it does not vanish into the lamp when the cursor is on the tower.")]
    public float beamMinLength = 1.5f;

    [Tooltip("How far past its tip the beam still catches a ship. A ship under the cursor has its centre a little beyond the point, and should still count.")]
    public float beamReachMargin = 0.6f;

    [Header("Signal")]
    [Tooltip("Seconds an idle ship waits between replays of its sequence. Short enough that a player who looks away does not have to wait to be reminded.")]
    public float signalInterval = 3f;

    [Tooltip("How long a short flash stays lit.")]
    public float flashShort = 0.2f;

    [Tooltip("How long a long flash stays lit.")]
    public float flashLong = 0.6f;

    [Tooltip("Dark pause between two flashes of the same sequence.")]
    public float flashGap = 0.25f;

    [Header("Buffer")]
    [Tooltip("Seconds of silence after which a partly typed sequence is dropped.")]
    public float bufferTimeout = 2f;

    [Tooltip("Longest sequence a ship can ask for. A symbol past this empties the buffer.")]
    public int bufferMax = 4;

    [Header("Arrival")]
    [Tooltip("World units per second a ship sails in at. A speed rather than a duration, so a ship coming from further out takes longer instead of moving faster.")]
    public float entrySpeed = 1.1f;

    [Tooltip("How close to its station a ship starts slowing down, in world units.")]
    public float entrySlowRadius = 1.5f;

    [Tooltip("Seconds between one ship starting its approach and the next. Keeps a night from opening with everything moving at once.")]
    public float entryStagger = 0.7f;

    [Header("Movement")]
    [Tooltip("A linked ship stops this close to the cursor, so it settles instead of jittering on top of it.")]
    public float stopRadius = 0.1f;

    [Tooltip("How far a ship is pushed back out of whatever it hit.")]
    public float bounceDistance = 0.2f;

    [Tooltip("How long that push takes.")]
    public float bounceDuration = 0.3f;

    [Tooltip("How long the ship ignores steering and further collisions after a hit. Keeps ships from grinding into each other.")]
    public float bounceFreeze = 0.5f;

    [Header("Dock")]
    [Tooltip("How long a ship takes to slide into its berth once it reaches the dock.")]
    public float dockSlideTime = 1f;

    [Tooltip("How long the arrival note stays on screen.")]
    public float noteDuration = 3f;

    [Header("Night")]
    [Tooltip("How long the \"Night N\" title holds before fading.")]
    public float nightTitleDuration = 1.5f;

    [Tooltip("Length of the dawn sequence that closes a night. Long enough to land, short enough that a player on their fifth night is not waiting through it. It can also be clicked past.")]
    public float dawnDuration = 7f;

    [Tooltip("How long the sky takes to darken back down at the start of the next night. Long enough not to be a cut, short enough not to be a wait.")]
    public float duskDuration = 2.5f;
}
