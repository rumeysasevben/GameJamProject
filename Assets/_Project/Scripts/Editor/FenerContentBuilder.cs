using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the game's data assets: the three ship types, the ten nights of the
/// technical document's §11, and the list that orders them.
///
/// Authored here rather than by hand because ten nights of positions and
/// sequences is a lot of Inspector typing to get subtly wrong, and because the
/// rules a night has to obey — two to four symbols, no repeats within a night,
/// a real berth — are easier to keep true in one file than across ten assets.
///
/// Safe to re-run: it overwrites the assets in place, so anything pointing at
/// them keeps pointing at them. Hand-tuned values are lost, which is the point
/// while the nights are still being roughed out; once they are being tuned,
/// stop running it.
/// </summary>
public static class FenerContentBuilder
{
    private const string DataFolder = "Assets/_Project/Data";
    private const string ShipTypeFolder = DataFolder + "/ShipTypes";
    private const string NightFolder = DataFolder + "/Nights";

    private const Signal S = Signal.Short;
    private const Signal L = Signal.Long;

    [MenuItem("Fener/2 - Gemi tipleri ve geceleri üret", priority = 2)]
    public static void BuildAll()
    {
        FenerEditorUtility.EnsureFolder(ShipTypeFolder);
        FenerEditorUtility.EnsureFolder(NightFolder);

        // The radii are the document's proportions scaled to the size the hulls
        // are actually drawn at — roughly 0.4 of a hull's width, so what bounces
        // is what the player sees.
        // Each class has its own signal family, so a vessel can be told apart
        // by its flashing before it can be told apart by its shape:
        //   Kayik   ·_        two symbols, opens short
        //   Balikci —__       three symbols, opens long
        //   Yuk     ··__      four symbols, opens with two shorts
        // Different lengths mean two classes can never collide, and the
        // variations inside a family keep the ships of one night apart.
        // Speeds raised about half again after playtesting: at the document's
        // values an escort to the pier was the slowest part of the night. The
        // ratio between classes is kept, so a freighter still feels heavy.
        ShipType kayik = BuildShipType("Kayik", ShipKind.Kayik, 1.4f, 360f, 0.35f, new[] { S }, 2);
        ShipType balikci = BuildShipType("Balikci", ShipKind.Balikci, 1.0f, 180f, 0.5f, new[] { L }, 3);
        ShipType yuk = BuildShipType("Yuk", ShipKind.Yuk, 0.7f, 90f, 0.75f, new[] { S, S }, 4);

        var nights = new List<NightData>
        {
            // Night 1 — one boat, one berth. The whole night exists to teach
            // that a light can be answered.
            Night(1, showSequence: false, fog: false,
                docks: new[] { Dock0() },
                rocks: new RockSpawn[0],
                ships: new[]
                {
                    Ship(kayik, new Vector2(5f, 1f), new[] { S, S }, 0,
                        "A fisherman was hauling in his nets when the dark caught him. He could not find the shore.")
                }),

            // Night 2 — two boats. Only one can be steered at a time, which is
            // the game's real rule, met here for the first time.
            Night(2, false, false,
                new[] { Dock0() },
                new RockSpawn[0],
                new[]
                {
                    Ship(kayik, new Vector2(6f, 2f), new[] { S, S }, 0,
                        "Two brothers, one boat. One of them rows; the other watches the lighthouse."),
                    Ship(kayik, new Vector2(3f, -1f), new[] { S, L }, 0,
                        "The old man puts out at the same hour every night. Tonight he is late.")
                }),

            // Night 3 — two classes at once, so the signal has to be read as
            // well as heard.
            Night(3, false, false,
                new[] { Dock0() },
                new RockSpawn[0],
                new[]
                {
                    Ship(kayik, new Vector2(7f, 3f), new[] { S, L }, 0,
                        "His daughter waited on the pier until morning. When she saw the light, she started to run."),
                    Ship(balikci, new Vector2(4f, -2f), new[] { L, S, S }, 0,
                        "They come back with a full hold. Nobody in town goes hungry this week.")
                }),

            // Night 4 — three ships, and the first heavy hull.
            Night(4, false, false,
                new[] { Dock0() },
                new RockSpawn[0],
                new[]
                {
                    Ship(kayik, new Vector2(7.5f, 1.5f), new[] { S, S }, 0,
                        "The young apprentice went out alone for the first time. He asked the lighthouse for the way home."),
                    Ship(balikci, new Vector2(5f, -2.5f), new[] { L, S, S }, 0,
                        "The engine coughed halfway out. They sailed the rest of the way."),
                    Ship(balikci, new Vector2(3f, 3f), new[] { L, S, L }, 0,
                        "Their dog sleeps on the deck. Nobody sights the shore before he does.")
                }),

            // Night 5 — three ships lined up on the same approach, so escorting
            // one drags it past the others. The first ordering puzzle.
            Night(5, false, false,
                new[] { Dock0() },
                new RockSpawn[0],
                new[]
                {
                    Ship(kayik, new Vector2(2.5f, -0.5f), new[] { S, S }, 0,
                        "There is one fish in his basket. He is smiling anyway."),
                    Ship(balikci, new Vector2(5f, -0.5f), new[] { L, S, S }, 0,
                        "He promised his wife they would see the dawn at home. He means to keep it."),
                    Ship(kayik, new Vector2(7.5f, -0.5f), new[] { S, L }, 0,
                        "The storm dragged them south. They found their bearing when they found the light.")
                }),

            // Night 6 — the first rock, and the first freighter: a hull that
            // turns slowly enough that the rock has to be planned around.
            Night(6, false, false,
                new[] { Dock0() },
                new[] { Rock(new Vector2(-1f, -1.5f), 0.5f) },
                new[]
                {
                    Ship(yuk, new Vector2(8f, 0.5f), new[] { S, S, S, S }, 0,
                        "Loaded with timber. The town's new roofs are aboard this ship."),
                    Ship(kayik, new Vector2(4f, 3.2f), new[] { S, S }, 0,
                        "The little boat waited for the big one. It did not want to come back alone."),
                    Ship(balikci, new Vector2(6f, -3f), new[] { L, S, S }, 0,
                        "The nets are empty but the boat is sound. They will go out again tomorrow.")
                }),

            // Night 7 — four ships through a narrow gap.
            Night(7, false, false,
                new[] { Dock0() },
                new[]
                {
                    Rock(new Vector2(0f, 0.8f), 0.6f),
                    Rock(new Vector2(0f, -1.6f), 0.6f),
                    Rock(new Vector2(2.6f, -0.4f), 0.5f)
                },
                new[]
                {
                    Ship(kayik, new Vector2(8f, 2.5f), new[] { S, S }, 0,
                        "They came through the haze. They saw nothing but the lighthouse."),
                    Ship(balikci, new Vector2(6f, 0.5f), new[] { L, S, S }, 0,
                        "The captain knows these rocks by heart. Tonight knowing was not enough."),
                    Ship(kayik, new Vector2(7f, -2.8f), new[] { S, L }, 0,
                        "They are carrying a passenger. She is coming to this town for the first time."),
                    Ship(balikci, new Vector2(4f, -3.4f), new[] { L, S, L }, 0,
                        "A lamp burns on the deck. It has not gone out in years.")
                }),

            // Night 8 — a second berth opens; now the sequence says where a
            // ship belongs, not just which one is answering.
            Night(8, false, false,
                new[] { Dock0(), Dock1() },
                new[]
                {
                    Rock(new Vector2(1.5f, 1.8f), 0.55f),
                    Rock(new Vector2(-0.5f, -2.4f), 0.5f)
                },
                new[]
                {
                    Ship(kayik, new Vector2(7.5f, 3f), new[] { S, S }, 0,
                        "They have to make the fish market. Dawn is not far off."),
                    Ship(balikci, new Vector2(8f, -1f), new[] { L, S, S }, 0,
                        "Heavy in the water, slow to turn. This one asks for patience."),
                    Ship(yuk, new Vector2(5.5f, 2f), new[] { S, S, S, S }, 1,
                        "Hers will be the first ship at the new pier. The captain is proud of it."),
                    Ship(balikci, new Vector2(4f, -3f), new[] { L, S, L }, 1,
                        "They are looking for the north pier. The coloured strip is meant for them.")
                }),

            // Night 9 — fog. Hulls disappear; only the flashes and whatever the
            // beam is touching can be seen.
            Night(9, false, true,
                new[] { Dock0(), Dock1() },
                new[]
                {
                    Rock(new Vector2(1f, 0f), 0.6f),
                    Rock(new Vector2(3.5f, -2f), 0.55f)
                },
                new[]
                {
                    Ship(kayik, new Vector2(8f, 1.5f), new[] { S, S }, 0,
                        "When the fog came down they did not trust the compass. They waited for the light."),
                    Ship(balikci, new Vector2(6f, -2.5f), new[] { L, S, S }, 0,
                        "The boy is on his first crossing. He says he is not frightened."),
                    Ship(yuk, new Vector2(4f, 3.4f), new[] { S, S, S, S }, 1,
                        "The town's winter coal is in the hold. If this ship is late, the town is cold."),
                    Ship(kayik, new Vector2(2.5f, -3.4f), new[] { S, L }, 1,
                        "They called to each other in the fog. Nobody answered.")
                }),

            // Night 10 — five ships, every sequence four symbols long, two
            // berths, three rocks and fog. Everything the game has taught, at
            // once.
            Night(10, false, true,
                new[] { Dock0(), Dock1() },
                new[]
                {
                    Rock(new Vector2(0.5f, 1.2f), 0.6f),
                    Rock(new Vector2(1.2f, -1.8f), 0.6f),
                    Rock(new Vector2(4.5f, 0.4f), 0.5f)
                },
                new[]
                {
                    Ship(kayik, new Vector2(8.5f, 2.5f), new[] { S, S }, 0,
                        "On the last night everyone is at sea. Nobody wanted to be left behind."),
                    Ship(balikci, new Vector2(7f, -0.5f), new[] { L, S, S }, 0,
                        "Full nets, a crowded deck. They are singing."),
                    Ship(yuk, new Vector2(5f, 3.4f), new[] { S, S, S, S }, 1,
                        "The largest ship comes home last. It always does."),
                    Ship(balikci, new Vector2(6f, -3.4f), new[] { L, S, L }, 1,
                        "The captain waved at the lighthouse. He knows he was seen."),
                    Ship(kayik, new Vector2(3f, 1.8f), new[] { S, L }, 0,
                        "The smallest boat had gone the furthest out. It still found its way back.")
                })
        };

        var assets = new NightData[nights.Count];
        for (int i = 0; i < nights.Count; i++)
        {
            assets[i] = SaveNight(nights[i]);
        }

        BuildSfxLibrary();

        NightList list = LoadOrCreate<NightList>($"{DataFolder}/NightList.asset");
        list.nights = assets;
        EditorUtility.SetDirty(list);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fener: {assets.Length} gece ve 3 gemi tipi üretildi → {NightFolder}");
    }

    /// <summary>
    /// Creates the sound library with every id the code asks for, each with an
    /// empty clip slot waiting.
    ///
    /// Named slots rather than an empty asset: whoever brings the audio can see
    /// exactly what the game wants and drag clips in, and nothing has to be
    /// spelled correctly twice. Existing entries are left alone, so re-running
    /// this never throws away clips that have already been placed.
    /// </summary>
    private static void BuildSfxLibrary()
    {
        var wanted = new[]
        {
            "ui_short", "ui_long",
            "ship_flash_short", "ship_flash_long",
            "link_success", "ship_reply",
            "collide_splash", "collide_oops",
            "dock_arrive", "window_on",
            "night_title"
        };

        SfxLibrary library = LoadOrCreate<SfxLibrary>($"{DataFolder}/SfxLibrary.asset");

        var entries = new List<SfxLibrary.Entry>(library.entries ?? new SfxLibrary.Entry[0]);

        foreach (string id in wanted)
        {
            if (entries.Exists(e => e != null && e.id == id))
            {
                continue;
            }

            entries.Add(new SfxLibrary.Entry { id = id });
        }

        library.entries = entries.ToArray();
        EditorUtility.SetDirty(library);
    }

    private static ShipType BuildShipType(string assetName, ShipKind kind, float speed, float turnRate, float radius, Signal[] codePrefix, int codeLength)
    {
        ShipType type = LoadOrCreate<ShipType>($"{ShipTypeFolder}/{assetName}.asset");

        type.kind = kind;
        type.speed = speed;
        type.turnRate = turnRate;
        type.collisionRadius = radius;
        type.codePrefix = codePrefix;
        type.codeLength = codeLength;
        type.signalLampOffset = new Vector2(0f, 0.12f);

        // The eight drawn headings, index 0 East and running counter-clockwise
        // — the §7 table — plus the haloed set used while a ship is linked.
        type.directionSprites = FenerArt.ShipDirections(kind, bound: false);
        type.boundSprites = FenerArt.ShipDirections(kind, bound: true) ?? new Sprite[8];

        EditorUtility.SetDirty(type);
        return type;
    }

    private static NightData SaveNight(NightData source)
    {
        NightData asset = LoadOrCreate<NightData>($"{NightFolder}/Night_{source.nightNumber:00}.asset");

        asset.nightNumber = source.nightNumber;
        asset.showSequenceAlways = source.showSequenceAlways;
        asset.fogEnabled = source.fogEnabled;
        asset.docks = source.docks;
        asset.rocks = source.rocks;
        asset.ships = source.ships;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static NightData Night(int number, bool showSequence, bool fog, DockSpawn[] docks, RockSpawn[] rocks, ShipSpawn[] ships)
    {
        var night = ScriptableObject.CreateInstance<NightData>();
        night.nightNumber = number;
        night.showSequenceAlways = showSequence;
        night.fogEnabled = fog;
        night.docks = docks;
        night.rocks = rocks;
        night.ships = ships;
        return night;
    }

    private static ShipSpawn Ship(ShipType type, Vector2 position, Signal[] sequence, int dockIndex, string note)
    {
        return new ShipSpawn
        {
            type = type,
            position = position,
            sequence = sequence,
            dockIndex = dockIndex,
            note = note
        };
    }

    /// <summary>
    /// The main berth: the pier north-west of the lighthouse, where the
    /// mock-up puts it.
    /// </summary>
    private static DockSpawn Dock0()
    {
        return new DockSpawn
        {
            position = new Vector2(-6.9f, -1.5f),
            radius = 0.8f,
            color = new Color(1f, 0.85f, 0.55f)
        };
    }

    /// <summary>The second berth, opened from night 8, further up the coast.</summary>
    private static DockSpawn Dock1()
    {
        return new DockSpawn
        {
            position = new Vector2(-8.4f, 1.2f),
            radius = 0.8f,
            color = new Color(0.6f, 0.85f, 1f)
        };
    }

    private static RockSpawn Rock(Vector2 position, float radius)
    {
        return new RockSpawn
        {
            position = position,
            radius = radius,
            sprite = FenerArt.Rock()
        };
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }
}
