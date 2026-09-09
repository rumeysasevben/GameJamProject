using System.Collections.Generic;
using UnityEngine;

/// <summary>One ship placed in a night: where it waits, what it flashes, where it belongs.</summary>
[System.Serializable]
public class ShipSpawn
{
    [Tooltip("Which vessel class to spawn.")]
    public ShipType type;

    [Tooltip("Where the ship sits at the start of the night, in world units. Screen centre is (0,0), top-right is (9.6, 5.4).")]
    public Vector2 position;

    [Tooltip("The sequence the ship flashes and the player must echo. Two to four symbols, unique within the night.")]
    public Signal[] sequence;

    [Tooltip("Index into the night's docks array — the only berth this ship will accept.")]
    public int dockIndex;

    [TextArea]
    [Tooltip("Two sentences shown when this ship arrives. The whole reason the night is worth finishing.")]
    public string note;
}

/// <summary>A berth. Ships whose dockIndex points here arrive by entering its circle.</summary>
[System.Serializable]
public class DockSpawn
{
    [Tooltip("Centre of the berth in world units.")]
    public Vector2 position;

    [Tooltip("How close a ship must come to count as arrived.")]
    public float radius = 0.6f;

    [Tooltip("Colour strip on the pier. On two-dock nights this is how the player tells the berths apart.")]
    public Color color = Color.white;
}

/// <summary>An obstacle. Nothing but a circle and a sprite.</summary>
[System.Serializable]
public class RockSpawn
{
    [Tooltip("Centre in world units.")]
    public Vector2 position;

    [Tooltip("Collision radius in world units.")]
    public float radius = 0.5f;

    [Tooltip("Which rock variant to draw.")]
    public Sprite sprite;
}

/// <summary>
/// Everything one night contains. One asset per night in
/// Assets/_Project/Data/Nights, ordered by a <see cref="NightList"/>.
///
/// A night is authored entirely by position: there is no spawn timing and no
/// clock. Every ship is on screen from the first frame, waiting, and the night
/// ends when the last one is berthed.
/// </summary>
[CreateAssetMenu(fileName = "Night_00", menuName = "Fener/NightData")]
public class NightData : ScriptableObject
{
    [Tooltip("Which night this is, counting from 1. Shown by the title card.")]
    public int nightNumber = 1;

    [Tooltip("Keep every sequence bubble on screen the whole night. On from night 1, off from night 3 — that is where the player starts having to remember.")]
    public bool showSequenceAlways = true;

    [Tooltip("Fog covers the sea; idle hulls are hidden and only their flashes show through. Night 9 onward.")]
    public bool fogEnabled;

    [Tooltip("The berths available tonight. Most nights have one; later nights open a second.")]
    public DockSpawn[] docks = new DockSpawn[0];

    [Tooltip("Obstacles to steer around.")]
    public RockSpawn[] rocks = new RockSpawn[0];

    [Tooltip("The ships waiting out at sea.")]
    public ShipSpawn[] ships = new ShipSpawn[0];

    /// <summary>
    /// Catches the three authoring mistakes that break a night: a sequence the
    /// buffer cannot express, two ships asking for the same sequence (which
    /// would make the match ambiguous), and a ship pointed at a berth that does
    /// not exist. Reported by asset name so the message is actionable.
    /// </summary>
    private void OnValidate()
    {
        if (docks == null || docks.Length == 0)
        {
            Debug.LogError($"{name}: no docks. Ships have nowhere to arrive.", this);
        }

        if (ships == null)
        {
            return;
        }

        var seen = new List<string>(ships.Length);

        for (int i = 0; i < ships.Length; i++)
        {
            ShipSpawn ship = ships[i];
            if (ship == null)
            {
                continue;
            }

            if (ship.type == null)
            {
                Debug.LogError($"{name}: ship {i} has no ShipType.", this);
            }

            int length = ship.sequence == null ? 0 : ship.sequence.Length;
            if (length < 2 || length > 4)
            {
                Debug.LogError($"{name}: ship {i} has a {length}-symbol sequence; must be 2 to 4.", this);
            }

            // A vessel class is meant to be recognisable by its signal, so a
            // sequence that does not fit its class's family is a content bug,
            // not a variation.
            if (ship.type != null && ship.sequence != null)
            {
                if (length != ship.type.codeLength)
                {
                    Debug.LogError($"{name}: ship {i} is a {ship.type.kind} and should have {ship.type.codeLength} symbols, not {length}.", this);
                }

                Signal[] prefix = ship.type.codePrefix;
                for (int p = 0; p < (prefix != null ? prefix.Length : 0); p++)
                {
                    if (p >= length || ship.sequence[p] != prefix[p])
                    {
                        Debug.LogError($"{name}: ship {i} does not open with the {ship.type.kind} signal {Describe(prefix)}.", this);
                        break;
                    }
                }
            }

            if (docks != null && (ship.dockIndex < 0 || ship.dockIndex >= docks.Length))
            {
                Debug.LogError($"{name}: ship {i} points at dock {ship.dockIndex}, which does not exist.", this);
            }

            string key = Describe(ship.sequence);
            if (seen.Contains(key))
            {
                Debug.LogError($"{name}: ship {i} repeats the sequence {key}. Two ships answering the same signal makes the match ambiguous.", this);
            }
            else
            {
                seen.Add(key);
            }
        }
    }

    /// <summary>Compact form of a sequence, '.' for Short and '-' for Long. Used in editor messages.</summary>
    public static string Describe(Signal[] sequence)
    {
        if (sequence == null || sequence.Length == 0)
        {
            return "(empty)";
        }

        char[] glyphs = new char[sequence.Length];
        for (int i = 0; i < sequence.Length; i++)
        {
            glyphs[i] = sequence[i] == Signal.Short ? '.' : '-';
        }

        return new string(glyphs);
    }
}
