
using UnityEngine;

/// <summary>Class of vessel. One <see cref="ShipType"/> asset per kind.</summary>
public enum ShipKind
{
    Kayik,
    Balikci,
    Yuk
}

/// <summary>
/// How one class of vessel handles and looks. Three assets live in
/// Assets/_Project/Data/ShipTypes: Kayik, Balikci, Yuk — light to heavy.
///
/// A ship reads everything about its own feel from here, so making cargo ships
/// heavier is an edit to one asset rather than to every night that spawns one.
/// </summary>
[CreateAssetMenu(fileName = "ShipType", menuName = "Fener/ShipType")]
public class ShipType : ScriptableObject
{
    [Tooltip("Which vessel this describes. Only used for readability and audio variation.")]
    public ShipKind kind;

    [Tooltip("World units per second while following the cursor. Reference values: 1.4 kayik / 1.0 balikci / 0.7 yuk.")]
    public float speed = 0.65f;

    [Tooltip("Degrees per second the heading can swing. Reference values: 360 / 180 / 90 — this is what makes a cargo ship feel heavy.")]
    public float turnRate = 180f;

    [Tooltip("Collision circle radius in world units, roughly 0.4 of how wide the hull is drawn.")]
    public float collisionRadius = 0.2f;

    [Header("Signal code")]
    // A vessel class is recognisable by its signal before it is recognisable by
    // its shape: out in the dark all the player has is the flashing, so the
    // opening symbols and the length say what kind of ship is asking. Rowboats
    // are short and start bright, freighters are long and start heavy.
    [Tooltip("Every vessel of this class opens its signal with these symbols. This is what makes a class recognisable by ear.")]
    public Signal[] codePrefix = new Signal[0];

    [Tooltip("How many symbols this class's signal has in total, the prefix included. Two to four.")]
    public int codeLength = 2;

    [Tooltip("Eight heading sprites. Index 0 is East and they run counter-clockwise; see the table in the technical document, §7.")]
    public Sprite[] directionSprites = new Sprite[8];

    [Tooltip("The same eight headings with the lighthouse's halo around the hull, shown while the ship is answering the beam. Leave empty to fall back to the plain hulls.")]
    public Sprite[] boundSprites = new Sprite[8];

    [Tooltip("Optional wake sprites, same order as the direction sprites. May be left empty.")]
    public Sprite[] wakeSprites;

    [Tooltip("Where the signal lamp sits on the hull, in the ship's local space.")]
    public Vector2 signalLampOffset;
}
