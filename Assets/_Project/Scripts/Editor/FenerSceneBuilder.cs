using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the Boot and Game scenes described in §2 of the technical document,
/// with every reference wired.
///
/// The wiring is the reason this exists. A night has roughly twenty references
/// running between the controller, the systems, the prefabs and the UI, and an
/// unassigned one usually fails silently — a bar that never fills, a dawn that
/// never comes. Building it in code makes the whole graph reviewable and, more
/// importantly, repeatable after a scene is lost or reworked.
///
/// Re-running replaces the scenes. An existing Game scene is renamed to
/// Game_backup rather than overwritten, so hand-placed work is recoverable.
/// </summary>
public static class FenerSceneBuilder
{
    private const string SceneFolder = "Assets/Scenes";
    private const string GameScenePath = SceneFolder + "/Game.unity";
    private const string BootScenePath = SceneFolder + "/Boot.unity";

    private const string DataFolder = "Assets/_Project/Data";
    private const string PrefabFolder = "Assets/_Project/Prefabs";

    private static readonly Color NightColor = new Color(0.106f, 0.137f, 0.251f);
    private static readonly Color DawnColor = new Color(0.965f, 0.722f, 0.627f);
    private static readonly Color DayColor = new Color(0.867f, 0.922f, 0.961f);
    // Read off the night mock-up: deep blue water, grey-green headland.
    private static readonly Color SeaNight = new Color(0.086f, 0.157f, 0.290f);

    // The sea stays water-coloured through the sunrise: a little warmth at the
    // turn, daylight blue by the end.
    private static readonly Color SeaDawn = new Color(0.286f, 0.322f, 0.427f);
    private static readonly Color SeaDay = new Color(0.420f, 0.616f, 0.780f);
    private static readonly Color LandColor = new Color(0.306f, 0.361f, 0.329f);

    // The panel palette from the UI mock-up.
    private static readonly Color Cream = new Color(0.957f, 0.925f, 0.851f);
    private static readonly Color Ink = new Color(0.165f, 0.180f, 0.259f);
    private static readonly Color CardColor = new Color(0.114f, 0.141f, 0.216f, 0.96f);
    private static readonly Color Amber = new Color(1f, 0.788f, 0.420f);
    private static readonly Color PipDim = new Color(0.227f, 0.267f, 0.365f);
    private static readonly Color DuskInk = new Color(0.259f, 0.235f, 0.318f);

    private enum ButtonStyle
    {
        /// <summary>Cream capsule, dark label. The thing to press.</summary>
        Primary,

        /// <summary>Cream outline on the dark card.</summary>
        Secondary,

        /// <summary>Dark capsule, for the light ending screen.</summary>
        Dark,

        /// <summary>Dark outline, for the light ending screen.</summary>
        DarkOutline
    }

    [MenuItem("Fener/4 - Sahneleri kur", priority = 4)]
    public static void BuildScenes()
    {
        FenerEditorUtility.EnsureFolder(SceneFolder);

        BuildBootScene();
        BuildGameScene();
        SetBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Fener: Boot ve Game sahneleri kuruldu.");
    }

    [MenuItem("Fener/Hepsini kur (0-6)", priority = 20)]
    public static void BuildEverything()
    {
        // Import settings first: everything below reads sprite sizes and pivots
        // as it builds, so importing after would leave prefabs at the wrong
        // scale until the next rebuild.
        FenerArt.ApplyImportSettings();
        FenerPlaceholderArt.GenerateAll();
        FenerContentBuilder.BuildAll();

        // Before the scenes: they read the hum and the music layers as they
        // build their AudioManagers.
        FenerAudioLinker.ImportAndFillLibrary();

        FenerPrefabBuilder.BuildAll();
        BuildScenes();
        FenerProjectSettings.Apply();
    }

    // ---------------------------------------------------------------- Boot

    private static void BuildBootScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(Color.black);
        CreateEventSystem();

        var managerObject = new GameObject("GameManager", typeof(GameManager));
        using (var fields = new FenerEditorUtility.Fields(managerObject.GetComponent<GameManager>()))
        {
            fields.Set("nights", AssetDatabase.LoadAssetAtPath<NightList>($"{DataFolder}/NightList.asset"))
                  .Set("config", AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataFolder}/GameConfig.asset"));
        }

        // Alongside the GameManager, and just as long-lived: the music has to
        // survive the move between nights or it would restart every time.
        CreateAudioManager();

        // The menu is not decoration: a browser will not play a sound until the
        // page has been clicked, so the game has to open on something clickable.
        Canvas canvas = CreateCanvas("Menu Canvas");
        var menuObject = new GameObject("MainMenu", typeof(RectTransform), typeof(MainMenuUI));
        menuObject.transform.SetParent(canvas.transform, false);

        CreateText(canvas.transform, "Title", "FENER", 120f, new Vector2(0f, 160f), new Vector2(900f, 200f));

        Button start = CreateButton(menuObject.transform, "StartButton", "Start", new Vector2(0f, -40f));
        Button continueButton = CreateButton(menuObject.transform, "ContinueButton", "Continue", new Vector2(0f, -140f));

        using (var fields = new FenerEditorUtility.Fields(menuObject.GetComponent<MainMenuUI>()))
        {
            fields.Set("startButton", start).Set("continueButton", continueButton);
        }

        EditorSceneManager.SaveScene(scene, BootScenePath);
    }

    // ---------------------------------------------------------------- Game

    private static void BuildGameScene()
    {
        BackupExistingGameScene();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var config = AssetDatabase.LoadAssetAtPath<GameConfig>($"{DataFolder}/GameConfig.asset");

        CreateCamera(SeaNight * 0.5f);
        CreateEventSystem();

        // A spare, for when the Game scene is played on its own. It stands
        // down as soon as Boot's has been through.
        CreateAudioManager();

        // --- World
        var world = new GameObject("World");

        Light2D globalLight = CreateGlobalLight(world.transform);
        SpriteRenderer sea = CreateSea(world.transform);
        Starfield stars = CreateStars(world.transform);
        SplashVFX splash = CreateSplash(world.transform);
        CoastCollider coast = CreateCoast(world.transform);

        var nightList = AssetDatabase.LoadAssetAtPath<NightList>($"{DataFolder}/NightList.asset");
        TownLights town = AddTown(world.transform, nightList != null ? nightList.Count : 10);

        // On the headland, where the mock-up stands it: far enough out that the
        // beam can sweep the whole sea without the tower blocking the town.
        Lighthouse lighthouse = InstantiatePrefab<Lighthouse>($"{PrefabFolder}/Lighthouse.prefab", world.transform, new Vector2(-5.05f, -3.6f));

        var docksParent = new GameObject("Docks");
        docksParent.transform.SetParent(world.transform, false);

        // Placed where night 1 puts them; each night repositions them from its
        // own data, so these are only what the scene view shows.
        Dock dock0 = InstantiatePrefab<Dock>($"{PrefabFolder}/Dock.prefab", docksParent.transform, new Vector2(-6.9f, -1.5f));
        dock0.gameObject.name = "Dock_0";
        Dock dock1 = InstantiatePrefab<Dock>($"{PrefabFolder}/Dock.prefab", docksParent.transform, new Vector2(-8.4f, 1.2f));
        dock1.gameObject.name = "Dock_1";

        var rocksParent = new GameObject("Rocks");
        rocksParent.transform.SetParent(world.transform, false);

        var shipsParent = new GameObject("Ships");
        shipsParent.transform.SetParent(world.transform, false);

        GameObject fog = CreateFog(world.transform);

        // --- UI
        Canvas canvas = CreateCanvas("UI Canvas");
        SequenceBarUI bar = CreateSequenceBar(canvas.transform);
        NightTitleUI title = CreateNightTitle(canvas.transform);
        NoteCardUI note = CreateNoteCard(canvas.transform);
        NightCounterUI counter = CreateNightCounter(canvas.transform);
        NightSummaryUI summary = CreateNightSummary(canvas.transform, nightList != null ? nightList.Count : 10);
        EndingUI ending = CreateEnding(canvas.transform);
        PauseMenuUI pause = CreatePauseMenu(canvas.transform);

        // --- Managers
        var managers = new GameObject("Managers");

        var routerObject = new GameObject("InputRouter", typeof(InputRouter));
        routerObject.transform.SetParent(managers.transform, false);

        var signalObject = new GameObject("Signal", typeof(SignalBuffer), typeof(SignalMatcher));
        signalObject.transform.SetParent(managers.transform, false);

        var collisionObject = new GameObject("CollisionSystem", typeof(CollisionSystem));
        collisionObject.transform.SetParent(managers.transform, false);

        var dawnObject = new GameObject("DawnController", typeof(DawnController));
        dawnObject.transform.SetParent(managers.transform, false);

        var controllerObject = new GameObject("NightController", typeof(NightController));
        controllerObject.transform.SetParent(managers.transform, false);

        SignalBuffer buffer = signalObject.GetComponent<SignalBuffer>();
        using (var fields = new FenerEditorUtility.Fields(buffer))
        {
            fields.Set("config", config);
        }

        using (var fields = new FenerEditorUtility.Fields(signalObject.GetComponent<SignalMatcher>()))
        {
            fields.Set("config", config).Set("buffer", buffer);
        }

        ConfigureDawn(dawnObject.GetComponent<DawnController>(), globalLight, sea, lighthouse, stars);

        using (var fields = new FenerEditorUtility.Fields(controllerObject.GetComponent<NightController>()))
        {
            fields.Set("config", config)
                  .Set("debugNight", AssetDatabase.LoadAssetAtPath<NightData>($"{DataFolder}/Nights/Night_01.asset"))
                  .Set("router", routerObject.GetComponent<InputRouter>())
                  .Set("lighthouse", lighthouse)
                  .Set("buffer", buffer)
                  .Set("matcher", signalObject.GetComponent<SignalMatcher>())
                  .Set("collisions", collisionObject.GetComponent<CollisionSystem>())
                  .Set("coast", coast)
                  .Set("shipsParent", shipsParent.transform)
                  .Set("rocksParent", rocksParent.transform)
                  .SetArray("docks", new Object[] { dock0, dock1 })
                  .Set("fog", fog)
                  .Set("shipPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Ship.prefab").GetComponent<Ship>())
                  .Set("rockPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Rock.prefab").GetComponent<Rock>())
                  .Set("sequenceBar", bar)
                  .Set("nightTitle", title)
                  .Set("noteCard", note)
                  .Set("nightCounter", counter)
                  .Set("summary", summary)
                  .Set("ending", ending)
                  .Set("pauseMenu", pause)
                  .Set("splash", splash)
                  .Set("dawn", dawnObject.GetComponent<DawnController>())
                  .Set("town", town);
        }

        using (var fields = new FenerEditorUtility.Fields(pause))
        {
            fields.Set("night", controllerObject.GetComponent<NightController>());
        }

        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    private static void BackupExistingGameScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            return;
        }

        string backup = $"{SceneFolder}/Game_backup.unity";
        AssetDatabase.DeleteAsset(backup);
        AssetDatabase.MoveAsset(GameScenePath, backup);
        Debug.Log($"Fener: eski Game sahnesi {backup} olarak saklandı.");
    }

    // ---------------------------------------------------------------- World pieces

    /// <summary>
    /// Builds an AudioManager wired to the library, the hum and whatever music
    /// layers exist.
    ///
    /// It goes in both scenes. Boot's is the one that normally survives, but
    /// pressing Play on the Game scene is how a night actually gets worked on,
    /// and without one there the whole game is silent — which is exactly the
    /// sort of thing that gets mistaken for broken audio. The singleton guard
    /// means the spare destroys itself the moment it meets the real one.
    /// </summary>
    private static void CreateAudioManager()
    {
        var go = new GameObject("AudioManager", typeof(AudioManager));

        using (var fields = new FenerEditorUtility.Fields(go.GetComponent<AudioManager>()))
        {
            fields.Set("library", AssetDatabase.LoadAssetAtPath<SfxLibrary>(FenerAudioLinker.LibraryAssetPath))
                  .Set("humClip", FenerAudioLinker.FindHum())
                  .SetArray("musicLayers", FenerAudioLinker.FindMusicLayers());
        }
    }

    private static Camera CreateCamera(Color background)
    {
        var go = new GameObject("Main Camera", typeof(Camera));
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);

        // Unity's own "create camera" menu adds this; AddComponent does not.
        // Without an AudioListener in the scene nothing is audible at all,
        // however well the rest of the audio is wired — and it fails in
        // complete silence, with no error to go on.
        go.AddComponent<AudioListener>();

        var camera = go.GetComponent<Camera>();
        camera.orthographic = true;

        // 5.4 units of half-height is 1080 px at 100 pixels to the unit: the
        // screen is exactly 19.2 × 10.8 world units, which is what every
        // position in the night data assumes.
        camera.orthographicSize = 5.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        return camera;
    }

    private static Light2D CreateGlobalLight(Transform parent)
    {
        var go = new GameObject("Global Light 2D");
        go.transform.SetParent(parent, false);

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = NightColor;
        light.intensity = 0.35f;
        return light;
    }

    private static SpriteRenderer CreateSea(Transform parent)
    {
        var go = new GameObject("Background", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);

        // The placeholder square is 32 px, so 60× covers the 19.2 × 10.8 screen
        // with room to spare at any aspect.
        go.transform.localScale = new Vector3(62f, 36f, 1f);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = FenerPlaceholderArt.Load("square");
        renderer.color = SeaNight;
        renderer.sortingOrder = -100;

        AddSeaDecoration(parent);
        return renderer;
    }

    /// <summary>
    /// Wave marks and weed scattered over the open water. Pure decoration, but
    /// without it the sea is a flat rectangle and nothing gives the eye a sense
    /// that the ships are moving across something.
    /// </summary>
    private static void AddSeaDecoration(Transform parent)
    {
        var decor = new GameObject("Decor");
        decor.transform.SetParent(parent, false);

        var waves = new[]
        {
            new Vector2(2.5f, 4.2f), new Vector2(6.8f, 3.4f), new Vector2(8.6f, 1.2f),
            new Vector2(1.2f, 2.2f), new Vector2(4.4f, 0.2f), new Vector2(7.6f, -1.4f),
            new Vector2(2.0f, -2.6f), new Vector2(5.4f, -4.2f), new Vector2(-1.4f, 3.6f),
            new Vector2(-2.6f, -1.2f), new Vector2(0.4f, -4.4f), new Vector2(8.8f, -3.6f)
        };

        for (int i = 0; i < waves.Length; i++)
        {
            var go = new GameObject($"Wave_{i}", typeof(SpriteRenderer));
            go.transform.SetParent(decor.transform, false);
            go.transform.localPosition = new Vector3(waves[i].x, waves[i].y, 0f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = FenerArt.Wave();
            renderer.color = new Color(1f, 1f, 1f, 0.18f);
            renderer.sortingOrder = -95;
        }

        var kelp = new[] { new Vector2(-3.4f, 3.2f), new Vector2(-1.0f, 0.6f), new Vector2(-4.2f, -1.6f) };

        for (int i = 0; i < kelp.Length; i++)
        {
            var go = new GameObject($"Kelp_{i}", typeof(SpriteRenderer));
            go.transform.SetParent(decor.transform, false);
            go.transform.localPosition = new Vector3(kelp[i].x, kelp[i].y, 0f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = FenerArt.Kelp();
            renderer.sortingOrder = -94;
        }
    }

    /// <summary>
    /// The shoreline: land in the lower left, laid out as circles a ship
    /// bounces off. The gap in front of Dock_0 is deliberate — leave room there
    /// or ships can never berth.
    /// </summary>
    private static CoastCollider CreateCoast(Transform parent)
    {
        var go = new GameObject("Coast", typeof(CoastCollider));
        go.transform.SetParent(parent, false);

        // A rotated slab, so the shoreline runs diagonally across the lower
        // left the way the mock-up draws it.
        var land = new GameObject("Land", typeof(SpriteRenderer));
        land.transform.SetParent(go.transform, false);
        land.transform.localPosition = new Vector3(-8.2f, -5.4f, 0f);
        land.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        land.transform.localScale = new Vector3(26f, 12f, 1f);

        var renderer = land.GetComponent<SpriteRenderer>();
        renderer.sprite = FenerPlaceholderArt.Load("square");
        renderer.color = LandColor;
        renderer.sortingOrder = -90;

        // Circles laid along the diagonal shore, plus one under the lighthouse
        // for the headland it stands on. The pier at (-6.9, -1.5) and the
        // second berth at (-8.4, 1.2) are deliberately clear of all of them:
        // close a gap here and ships can no longer reach their berth.
        var circles = new[]
        {
            (offset: new Vector2(-9.8f, -2.2f), radius: 1.6f),
            (offset: new Vector2(-8.8f, -3.0f), radius: 1.6f),
            (offset: new Vector2(-7.6f, -3.8f), radius: 1.5f),
            (offset: new Vector2(-6.4f, -4.5f), radius: 1.5f),
            (offset: new Vector2(-5.2f, -5.2f), radius: 1.5f),
            (offset: new Vector2(-9.8f, -4.6f), radius: 2.0f),
            (offset: new Vector2(-7.8f, -5.4f), radius: 2.0f),
            (offset: new Vector2(-5.0f, -4.2f), radius: 1.2f)
        };

        var serialized = new SerializedObject(go.GetComponent<CoastCollider>());
        SerializedProperty array = serialized.FindProperty("circles");
        array.arraySize = circles.Length;

        for (int i = 0; i < circles.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("offset").vector2Value = circles[i].offset;
            element.FindPropertyRelative("radius").floatValue = circles[i].radius;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return go.GetComponent<CoastCollider>();
    }

    /// <summary>
    /// The town behind the shore. Each house is the dark drawing with the lit
    /// one stacked on top at zero alpha, ready to be faded up when a ship gets
    /// home.
    ///
    /// Ordered from the waterfront inland, so the light spreads up the hillside
    /// as the night goes on instead of appearing in scattered spots.
    /// </summary>
    private static TownLights AddTown(Transform parent, int nightCount)
    {
        var town = new GameObject("Town", typeof(TownLights));
        town.transform.SetParent(parent, false);

        var positions = new[]
        {
            new Vector2(-9.3f, -2.6f), new Vector2(-8.6f, -3.3f), new Vector2(-9.4f, -3.9f),
            new Vector2(-8.0f, -4.0f), new Vector2(-7.2f, -4.6f), new Vector2(-8.7f, -4.7f),
            new Vector2(-6.4f, -5.0f), new Vector2(-9.6f, -5.0f), new Vector2(-7.6f, -5.3f),
            new Vector2(-5.8f, -5.4f), new Vector2(-8.9f, -5.6f), new Vector2(-6.9f, -5.8f)
        };

        // Exactly one house per night: the last window comes on as the last
        // night ends, so a whole lit town is the ending and not a coincidence.
        int count = Mathf.Clamp(nightCount, 1, positions.Length);
        if (nightCount > positions.Length)
        {
            Debug.LogWarning($"Fener: {nightCount} gece var ama kasabada {positions.Length} ev yeri tanımlı; fazlası için AddTown'a konum ekle.");
        }

        var windows = new WindowLight[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"House_{i:00}", typeof(WindowLight));
            go.transform.SetParent(town.transform, false);
            go.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);

            var dark = new GameObject("Dark", typeof(SpriteRenderer));
            dark.transform.SetParent(go.transform, false);
            var darkRenderer = dark.GetComponent<SpriteRenderer>();
            darkRenderer.sprite = FenerArt.HouseDark();
            darkRenderer.sortingOrder = -80;

            var lit = new GameObject("Lit", typeof(SpriteRenderer));
            lit.transform.SetParent(go.transform, false);
            var litRenderer = lit.GetComponent<SpriteRenderer>();
            litRenderer.sprite = FenerArt.HouseLit();
            litRenderer.sortingOrder = -79;
            litRenderer.color = new Color(1f, 1f, 1f, 0f);

            // Behind the house, so the light looks like it is spilling out of
            // the windows rather than painted over the roof.
            var glow = new GameObject("Glow", typeof(SpriteRenderer));
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            glow.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            var glowRenderer = glow.GetComponent<SpriteRenderer>();
            glowRenderer.sprite = FenerArt.WindowGlow();
            glowRenderer.sortingOrder = -81;
            glowRenderer.color = new Color(1f, 0.85f, 0.55f, 0f);

            windows[i] = go.GetComponent<WindowLight>();
            windows[i].Bind(darkRenderer, litRenderer, glowRenderer);
        }

        TownLights lights = town.GetComponent<TownLights>();
        lights.Bind(windows);

        EditorUtility.SetDirty(town);
        return lights;
    }

    /// <summary>
    /// The stars. Scattered over the upper sky only, and kept off the left
    /// where the town and the tower sit.
    /// </summary>
    private static Starfield CreateStars(Transform parent)
    {
        var root = new GameObject("Stars", typeof(Starfield));
        root.transform.SetParent(parent, false);

        var positions = new[]
        {
            new Vector2(-6.2f, 4.6f), new Vector2(-3.4f, 3.9f), new Vector2(-1.1f, 4.8f),
            new Vector2(1.6f, 4.1f), new Vector2(3.9f, 4.9f), new Vector2(6.4f, 4.3f),
            new Vector2(8.5f, 4.8f), new Vector2(-4.8f, 2.9f), new Vector2(0.4f, 3.1f),
            new Vector2(4.9f, 2.6f), new Vector2(7.6f, 3.3f), new Vector2(2.6f, 1.9f),
            new Vector2(9.0f, 1.6f), new Vector2(-2.2f, 2.2f), new Vector2(6.0f, 0.9f)
        };

        var stars = new SpriteRenderer[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"Star_{i:00}", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);

            // A range of sizes, so the sky has depth rather than a grid of
            // identical dots.
            float size = Mathf.Lerp(0.05f, 0.11f, (i * 0.37f) % 1f);
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = FenerPlaceholderArt.Load("circle");
            renderer.color = new Color(0.9f, 0.94f, 1f, Mathf.Lerp(0.35f, 0.8f, (i * 0.61f) % 1f));
            renderer.sortingOrder = -98;

            stars[i] = renderer;
        }

        root.GetComponent<Starfield>().Bind(stars);
        return root.GetComponent<Starfield>();
    }

    /// <summary>The splash effect. Builds its own droplets at runtime, so there is nothing to place here.</summary>
    private static SplashVFX CreateSplash(Transform parent)
    {
        var root = new GameObject("Splash", typeof(SplashVFX));
        root.transform.SetParent(parent, false);
        root.GetComponent<SplashVFX>().Bind(FenerPlaceholderArt.Load("circle"));
        return root.GetComponent<SplashVFX>();
    }

    /// <summary>
    /// The fog layer, off until a night asks for it. Blobs at different heights
    /// and speeds, so nothing in it moves as a block.
    /// </summary>
    private static GameObject CreateFog(Transform parent)
    {
        var root = new GameObject("Fog", typeof(FogController));
        root.transform.SetParent(parent, false);

        var placements = new[]
        {
            (position: new Vector2(-2f, 2.6f), scale: 1.6f, alpha: 0.55f, speed: 0.14f),
            (position: new Vector2(3f, 0.4f), scale: 2.1f, alpha: 0.5f, speed: 0.09f),
            (position: new Vector2(-4f, -1.8f), scale: 1.8f, alpha: 0.45f, speed: 0.18f),
            (position: new Vector2(5f, -3.2f), scale: 2.4f, alpha: 0.4f, speed: 0.06f),
            (position: new Vector2(0.5f, 4.2f), scale: 2.0f, alpha: 0.35f, speed: 0.12f)
        };

        var layers = new SpriteRenderer[placements.Length];
        var speeds = new float[placements.Length];

        for (int i = 0; i < placements.Length; i++)
        {
            var go = new GameObject($"FogLayer_{i}", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(placements[i].position.x, placements[i].position.y, 0f);
            go.transform.localScale = new Vector3(placements[i].scale, placements[i].scale, 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = FenerArt.FogBlob();
            renderer.color = new Color(0.78f, 0.84f, 0.92f, placements[i].alpha);
            renderer.sortingOrder = 30;

            layers[i] = renderer;
            speeds[i] = placements[i].speed;
        }

        root.GetComponent<FogController>().Bind(layers, speeds);
        root.SetActive(false);
        return root;
    }

    private static void ConfigureDawn(DawnController dawn, Light2D globalLight, SpriteRenderer sea, Lighthouse lighthouse, Starfield stars)
    {
        // The sky carries the sunrise; the sea only warms slightly and ends
        // blue. Tinting the water orange at full day makes the morning look
        // like it is still happening hours after it finished.
        dawn.lightGradient = MakeGradient(NightColor, DawnColor, DayColor);
        dawn.seaGradient = MakeGradient(SeaNight, SeaDawn, SeaDay);

        // By path rather than GetComponentInChildren: the lighthouse carries
        // three lights and picking the wrong one would fade the lamp at dawn
        // and leave the beam burning.
        Transform beamLightTransform = lighthouse != null
            ? lighthouse.transform.Find("LampPoint/Beam/BeamLight")
            : null;
        Light2D beamLight = beamLightTransform != null ? beamLightTransform.GetComponent<Light2D>() : null;

        Transform coneTransform = lighthouse != null
            ? lighthouse.transform.Find("LampPoint/Beam/ConeSprite")
            : null;
        SpriteRenderer cone = coneTransform != null ? coneTransform.GetComponent<SpriteRenderer>() : null;

        using (var fields = new FenerEditorUtility.Fields(dawn))
        {
            fields.Set("globalLight", globalLight)
                  .Set("sea", sea)
                  .Set("beamLight", beamLight)
                  .Set("beamCone", cone)
                  .Set("starfield", stars);
        }
    }

    private static Gradient MakeGradient(Color night, Color dawn, Color day)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(night, 0f),
                new GradientColorKey(dawn, 0.55f),
                new GradientColorKey(day, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }

    // ---------------------------------------------------------------- UI pieces

    private static Canvas CreateCanvas(string canvasName)
    {
        var go = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static SequenceBarUI CreateSequenceBar(Transform parent)
    {
        // Top centre, and smaller than it started. The bottom of the screen
        // belongs to the arrival notes; the bar is something glanced at while
        // typing, not read, so it does not need to be large.
        var root = new GameObject("SequenceBar", typeof(RectTransform), typeof(SequenceBarUI));
        root.transform.SetParent(parent, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 130f));

        var slots = new Object[4];
        var symbols = new Object[4];

        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 74f;

            Image slot = CreateImage(root.transform, $"Slot_{i}", FenerArt.SlotEmpty(), new Vector2(x, 0f), new Vector2(64f, 64f));
            slots[i] = slot;

            Image symbol = CreateImage(slot.transform, "Symbol", FenerArt.Dot(), Vector2.zero, new Vector2(42f, 42f));
            symbol.enabled = false;
            symbols[i] = symbol;
        }

        Image line = CreateImage(root.transform, "TimeoutLine", FenerPlaceholderArt.Load("square"), new Vector2(0f, -46f), new Vector2(300f, 5f));
        line.type = Image.Type.Filled;
        line.fillMethod = Image.FillMethod.Horizontal;
        line.fillOrigin = (int)Image.OriginHorizontal.Left;
        line.fillAmount = 1f;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<SequenceBarUI>()))
        {
            fields.SetArray("slots", slots)
                  .SetArray("symbols", symbols)
                  .Set("shortSprite", FenerArt.Dot())
                  .Set("longSprite", FenerArt.Dash())
                  .Set("slotEmptySprite", FenerArt.SlotEmpty())
                  .Set("slotFilledSprite", FenerArt.SlotFilled())
                  .Set("slotSuccessSprite", FenerArt.SlotSuccess())
                  .Set("timeoutLine", line);
        }

        return root.GetComponent<SequenceBarUI>();
    }

    private static NightTitleUI CreateNightTitle(Transform parent)
    {
        var root = new GameObject("NightTitle", typeof(RectTransform), typeof(CanvasGroup), typeof(NightTitleUI));
        root.transform.SetParent(parent, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 240f));

        TMP_Text label = CreateText(root.transform, "Label", "Night 1", 110f, Vector2.zero, new Vector2(900f, 200f));
        label.fontStyle = FontStyles.Bold;

        root.GetComponent<CanvasGroup>().alpha = 0f;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<NightTitleUI>()))
        {
            fields.Set("group", root.GetComponent<CanvasGroup>()).Set("label", label);
        }

        return root.GetComponent<NightTitleUI>();
    }

    /// <summary>
    /// The small night counter, top left. Out of the way of the beam, which
    /// spends most of its time sweeping the right half of the screen.
    /// </summary>
    private static NightCounterUI CreateNightCounter(Transform parent)
    {
        var root = new GameObject("NightCounter", typeof(RectTransform), typeof(NightCounterUI));
        root.transform.SetParent(parent, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(200f, -70f), new Vector2(360f, 60f));

        TMP_Text label = CreateText(root.transform, "Label", "Night 1", 40f, Vector2.zero, new Vector2(360f, 60f));
        label.alignment = TextAlignmentOptions.Left;
        label.color = new Color(0.96f, 0.94f, 0.88f, 0.75f);

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<NightCounterUI>()))
        {
            fields.Set("label", label);
        }

        return root.GetComponent<NightCounterUI>();
    }

    /// <summary>
    /// The card between dawn and the next dusk, after the "Gece sonu" mock-up:
    /// a dark rounded card with a spaced-out heading, two handwritten lines, a
    /// row of the town's windows and one cream button. Animated and worded by
    /// the script; this only lays it out.
    /// </summary>
    private static NightSummaryUI CreateNightSummary(Transform parent, int windowCount)
    {
        var root = new GameObject("NightSummary", typeof(RectTransform), typeof(CanvasGroup), typeof(NightSummaryUI));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());

        // A dim sheet over the harbour rather than a solid screen: the lit town
        // is the reward, and covering it up to report on it would be perverse.
        Image veil = CreateImage(root.transform, "Veil", FenerPlaceholderArt.Load("square"), Vector2.zero, Vector2.zero);
        Stretch(veil.rectTransform);
        veil.color = new Color(0.02f, 0.04f, 0.08f, 0.55f);
        veil.raycastTarget = true;

        var cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        cardObject.transform.SetParent(root.transform, false);
        var card = cardObject.GetComponent<RectTransform>();
        var cardSize = new Vector2(780f, 460f);
        Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), cardSize);

        Image shadow = CreateSliced(card, "Shadow", FenerArt.SoftShadow(), new Vector2(0f, -16f), cardSize + new Vector2(90f, 90f), 1f);
        shadow.color = new Color(0f, 0f, 0f, 0.55f);

        Image panel = CreateSliced(card, "Panel", FenerArt.RoundedPanel(), Vector2.zero, cardSize, 1f);
        panel.color = CardColor;

        TMP_Text eyebrow = CreateText(card, "Eyebrow", "NIGHT 1 COMPLETE", 24f, new Vector2(0f, 172f), new Vector2(700f, 40f));
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.characterSpacing = 14f;
        eyebrow.color = new Color(Cream.r, Cream.g, Cream.b, 0.5f);

        TMP_Text body = CreateText(card, "Body", "Two ships are home.\nOne more window is lit in town.", 46f, new Vector2(0f, 84f), new Vector2(720f, 140f));
        UseHandwriting(body);
        body.lineSpacing = -8f;
        body.color = Cream;

        // The town's windows, one per night, earned ones amber.
        var pipsObject = new GameObject("Pips", typeof(RectTransform), typeof(CanvasGroup));
        pipsObject.transform.SetParent(card, false);
        Anchor(pipsObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(720f, 70f));

        int count = Mathf.Max(1, windowCount);
        var pips = new Object[count];
        var glows = new Object[count];
        const float pitch = 44f;

        for (int i = 0; i < count; i++)
        {
            var position = new Vector2((i - (count - 1) * 0.5f) * pitch, 0f);

            Image glow = CreateSliced(pipsObject.transform, $"Glow_{i}", FenerArt.SoftShadow(), position, new Vector2(78f, 88f), 1.8f);
            glow.color = new Color(Amber.r, Amber.g, Amber.b, 0.75f);
            glows[i] = glow;

            Image pip = CreateSliced(pipsObject.transform, $"Pip_{i}", FenerArt.RoundedPanel(), position, new Vector2(30f, 40f), 4f);
            pip.color = PipDim;
            pips[i] = pip;
        }

        TMP_Text windowsLabel = CreateText(card, "WindowsLabel", "1 / 10 windows lit", 22f, new Vector2(0f, -74f), new Vector2(700f, 34f));
        windowsLabel.color = new Color(Cream.r, Cream.g, Cream.b, 0.6f);

        TMP_Text stats = CreateText(card, "Stats", "1:07   ·   not a scratch", 20f, new Vector2(0f, -106f), new Vector2(700f, 30f));
        stats.color = new Color(Cream.r, Cream.g, Cream.b, 0.4f);

        Button continueButton = CreateButton(card, "ContinueButton", "On to night 2", new Vector2(0f, -170f), ButtonStyle.Primary, new Vector2(340f, 76f), 0.025f);

        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<NightSummaryUI>()))
        {
            fields.Set("group", group)
                  .Set("veil", veil)
                  .Set("card", card)
                  .Set("cardGroup", cardObject.GetComponent<CanvasGroup>())
                  .Set("eyebrow", eyebrow)
                  .Set("body", body)
                  .Set("pipsGroup", pipsObject.GetComponent<CanvasGroup>())
                  .SetArray("pips", pips)
                  .SetArray("pipGlows", glows)
                  .Set("windowsLabel", windowsLabel)
                  .Set("stats", stats)
                  .Set("continueButton", continueButton)
                  .Set("continueLabel", continueButton.GetComponentInChildren<TMP_Text>())
                  .Set("continueJuice", continueButton.GetComponent<UIButtonJuice>())
                  .Set("pipDim", PipDim)
                  .Set("pipLit", Amber)
                  .Set("veilAlpha", 0.55f);
        }

        return root.GetComponent<NightSummaryUI>();
    }

    /// <summary>
    /// The screen after the last night, after the "Final" mock-up: the whole
    /// view gone to a sunrise gradient, one line in large type and two buttons.
    /// </summary>
    private static EndingUI CreateEnding(Transform parent)
    {
        var root = new GameObject("Ending", typeof(RectTransform), typeof(CanvasGroup), typeof(EndingUI));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());

        // Not quite opaque, so the lit town still shows faintly through the sky.
        Image sky = CreateImage(root.transform, "Sky", FenerArt.DawnGradient(), Vector2.zero, Vector2.zero);
        Stretch(sky.rectTransform);
        sky.color = new Color(1f, 1f, 1f, 0.94f);
        sky.raycastTarget = true;

        TMP_Text eyebrow = CreateText(root.transform, "Eyebrow", "THE TENTH NIGHT", 26f, new Vector2(0f, 160f), new Vector2(900f, 44f));
        eyebrow.fontStyle = FontStyles.Bold;
        eyebrow.characterSpacing = 16f;
        eyebrow.color = new Color(DuskInk.r, DuskInk.g, DuskInk.b, 0.45f);

        TMP_Text title = CreateText(root.transform, "Title", "The town is awake.", 104f, new Vector2(0f, 62f), new Vector2(1500f, 150f));
        title.fontStyle = FontStyles.Bold;
        title.color = DuskInk;

        TMP_Text subtitle = CreateText(root.transform, "Subtitle", "Every ship is home. The lighthouse can rest now.", 42f, new Vector2(0f, -42f), new Vector2(1300f, 70f));
        UseHandwriting(subtitle);
        subtitle.color = new Color(DuskInk.r, DuskInk.g, DuskInk.b, 0.8f);

        Button playAgain = CreateButton(root.transform, "PlayAgainButton", "Play again", new Vector2(-135f, -170f), ButtonStyle.Dark, new Vector2(250f, 72f), 0.02f);
        Button menu = CreateButton(root.transform, "MenuButton", "Menu", new Vector2(125f, -170f), ButtonStyle.DarkOutline, new Vector2(210f, 72f), 0f);

        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<EndingUI>()))
        {
            fields.Set("group", group)
                  .Set("eyebrow", eyebrow)
                  .Set("title", title)
                  .Set("subtitle", subtitle)
                  .Set("playAgainButton", playAgain)
                  .Set("playAgainJuice", playAgain.GetComponent<UIButtonJuice>())
                  .Set("menuButton", menu)
                  .Set("menuJuice", menu.GetComponent<UIButtonJuice>());
        }

        return root.GetComponent<EndingUI>();
    }

    /// <summary>
    /// The pause panel, after the "Mola" mock-up, and the small button in the
    /// top-right corner that opens it. The button sits outside the panel's
    /// canvas group so it stays clickable while the panel is hidden.
    /// </summary>
    private static PauseMenuUI CreatePauseMenu(Transform parent)
    {
        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(PauseMenuUI));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());

        // --- Corner button
        var pauseSize = new Vector2(76f, 60f);
        Button pauseButton = CreateButton(root.transform, "PauseButton", "II", Vector2.zero, ButtonStyle.Secondary, pauseSize, 0f);
        Anchor(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-90f, -70f), pauseSize);
        var pauseButtonGroup = pauseButton.gameObject.AddComponent<CanvasGroup>();

        // --- Panel
        var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup));
        panelObject.transform.SetParent(root.transform, false);
        Stretch(panelObject.GetComponent<RectTransform>());

        Image veil = CreateImage(panelObject.transform, "Veil", FenerPlaceholderArt.Load("square"), Vector2.zero, Vector2.zero);
        Stretch(veil.rectTransform);
        veil.color = new Color(0.02f, 0.04f, 0.08f, 0.5f);
        veil.raycastTarget = true;

        var cardObject = new GameObject("Card", typeof(RectTransform));
        cardObject.transform.SetParent(panelObject.transform, false);
        var card = cardObject.GetComponent<RectTransform>();
        var cardSize = new Vector2(620f, 560f);
        Anchor(card, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);

        Image shadow = CreateSliced(card, "Shadow", FenerArt.SoftShadow(), new Vector2(0f, -16f), cardSize + new Vector2(90f, 90f), 1f);
        shadow.color = new Color(0f, 0f, 0f, 0.55f);

        Image panel = CreateSliced(card, "Panel", FenerArt.RoundedPanel(), Vector2.zero, cardSize, 1f);
        panel.color = CardColor;
        panel.raycastTarget = true;

        TMP_Text title = CreateText(card, "Title", "Paused", 46f, new Vector2(0f, 222f), new Vector2(520f, 64f));
        title.fontStyle = FontStyles.Bold;
        title.color = Cream;

        UIToggle music = CreateToggleRow(card, "Music", 140f);
        UIToggle sfx = CreateToggleRow(card, "Sound effects", 76f);
        UIToggle sequences = CreateToggleRow(card, "Always show ship codes", 12f);

        TMP_Text beamLabel = CreateText(card, "BeamWidthLabel", "Beam width", 30f, new Vector2(-60f, -58f), new Vector2(400f, 46f));
        beamLabel.alignment = TextAlignmentOptions.Left;
        beamLabel.color = Cream;

        TMP_Text beamValue = CreateText(card, "BeamWidthValue", "medium", 26f, new Vector2(170f, -58f), new Vector2(160f, 46f));
        beamValue.alignment = TextAlignmentOptions.Right;
        beamValue.color = new Color(Cream.r, Cream.g, Cream.b, 0.55f);

        Slider beamSlider = CreateSlider(card, new Vector2(0f, -110f), new Vector2(500f, 30f));

        Button resume = CreateButton(card, "ResumeButton", "Resume", new Vector2(-125f, -200f), ButtonStyle.Primary, new Vector2(230f, 72f), 0.02f);
        Button menu = CreateButton(card, "MenuButton", "Menu", new Vector2(125f, -200f), ButtonStyle.Secondary, new Vector2(230f, 72f), 0f);

        var group = panelObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<PauseMenuUI>()))
        {
            fields.Set("pauseButton", pauseButton)
                  .Set("pauseButtonGroup", pauseButtonGroup)
                  .Set("group", group)
                  .Set("veil", veil)
                  .Set("card", card)
                  .Set("musicToggle", music)
                  .Set("sfxToggle", sfx)
                  .Set("sequencesToggle", sequences)
                  .Set("beamSlider", beamSlider)
                  .Set("beamValueLabel", beamValue)
                  .Set("resumeButton", resume)
                  .Set("menuButton", menu);
        }

        return root.GetComponent<PauseMenuUI>();
    }

    /// <summary>A label on the left of the card and a switch on the right.</summary>
    private static UIToggle CreateToggleRow(Transform card, string caption, float y)
    {
        TMP_Text label = CreateText(card, $"{caption}Label", caption, 30f, new Vector2(-60f, y), new Vector2(400f, 46f));
        label.alignment = TextAlignmentOptions.Left;
        label.color = Cream;

        var trackSize = new Vector2(72f, 40f);
        var root = new GameObject($"{caption}Toggle", typeof(RectTransform), typeof(UIToggle));
        root.transform.SetParent(card, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(214f, y), trackSize);

        Image track = CreateSliced(root.transform, "Track", FenerArt.Pill(), Vector2.zero, trackSize, 70f / trackSize.y);
        track.raycastTarget = true;

        Image knob = CreateImage(root.transform, "Knob", FenerPlaceholderArt.Load("circle"), Vector2.zero, new Vector2(30f, 30f));

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<UIToggle>()))
        {
            fields.Set("track", track)
                  .Set("knob", knob)
                  .Set("trackOn", Amber)
                  .Set("trackOff", new Color(0.30f, 0.34f, 0.44f))
                  .Set("knobOn", Ink)
                  .Set("knobOff", new Color(0.80f, 0.80f, 0.82f))
                  .Set("knobTravel", 16f);
        }

        return root.GetComponent<UIToggle>();
    }

    /// <summary>
    /// A slider in the panel style: dim capsule track, amber fill, cream knob.
    /// Built on Unity's default slider so the drag handling and layout are the
    /// stock ones, then restyled.
    /// </summary>
    private static Slider CreateSlider(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var resources = new DefaultControls.Resources
        {
            standard = FenerArt.Pill(),
            background = FenerArt.Pill(),
            knob = FenerPlaceholderArt.Load("circle")
        };

        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = "BeamWidthSlider";
        sliderObject.transform.SetParent(parent, false);
        Anchor(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), anchoredPosition, size);

        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;
        slider.transition = Selectable.Transition.None;

        // The track is half the slider's height, so its capsule ends need the
        // corners scaled to that.
        float trackCorners = 70f / (size.y * 0.5f);

        var background = sliderObject.transform.Find("Background").GetComponent<Image>();
        background.color = PipDim;
        background.pixelsPerUnitMultiplier = trackCorners;

        var fill = sliderObject.transform.Find("Fill Area/Fill").GetComponent<Image>();
        fill.color = Amber;
        fill.pixelsPerUnitMultiplier = trackCorners;

        var slideArea = sliderObject.transform.Find("Handle Slide Area").GetComponent<RectTransform>();
        slideArea.sizeDelta = new Vector2(-size.y, 0f);

        var handle = sliderObject.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
        handle.color = Cream;
        handle.rectTransform.sizeDelta = new Vector2(size.y, 0f);

        return slider;
    }

    private static NoteCardUI CreateNoteCard(Transform parent)
    {
        var root = new GameObject("NoteCard", typeof(RectTransform), typeof(NoteCardUI));
        root.transform.SetParent(parent, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 140f));

        Image panel = CreateImage(root.transform, "Panel", FenerArt.NoteCard(), new Vector2(0f, -200f), new Vector2(1040f, 180f));
        Anchor(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -200f), new Vector2(1040f, 180f));

        TMP_Text label = CreateText(panel.transform, "Label", string.Empty, 38f, Vector2.zero, new Vector2(960f, 140f));
        UseHandwriting(label);

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<NoteCardUI>()))
        {
            fields.Set("panel", panel.rectTransform)
                  .Set("label", label)
                  .Set("hiddenPosition", new Vector2(0f, -200f))
                  .Set("shownPosition", new Vector2(0f, 110f));
        }

        return root.GetComponent<NoteCardUI>();
    }

    /// <summary>
    /// A capsule button with a soft glow behind it, driven by
    /// <see cref="UIButtonJuice"/>. <paramref name="breathe"/> gives it an idle
    /// swell, for the one button on a screen that is meant to be pressed.
    /// </summary>
    private static Button CreateButton(Transform parent, string buttonName, string caption, Vector2 anchoredPosition,
        ButtonStyle style = ButtonStyle.Primary, Vector2? size = null, float breathe = 0f)
    {
        Vector2 rect = size ?? new Vector2(320f, 76f);

        var root = new GameObject(buttonName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Anchor(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), anchoredPosition, rect);

        bool dark = style == ButtonStyle.Dark || style == ButtonStyle.DarkOutline;
        bool outline = style == ButtonStyle.Secondary || style == ButtonStyle.DarkOutline;
        Color tone = dark ? DuskInk : Cream;

        Vector2 glowSize = rect + new Vector2(70f, 70f);
        Image glow = CreateSliced(root.transform, "Glow", FenerArt.SoftShadow(), Vector2.zero, glowSize, 120f / glowSize.y);
        glow.color = dark ? new Color(1f, 1f, 1f, 0.6f) : new Color(Amber.r, Amber.g, Amber.b, 0.5f);

        // The capsule's slice border is half its texture, so scaling it to the
        // button's height keeps the ends fully round at any size.
        Image background = CreateSliced(root.transform, "Background", outline ? FenerArt.PillOutline() : FenerArt.Pill(), Vector2.zero, rect, 70f / rect.y);
        background.color = outline ? new Color(tone.r, tone.g, tone.b, 0.4f) : tone;

        // CreateImage turns raycasts off, which is right for every other image
        // in the game and wrong for the one thing meant to be clicked. The
        // outline gets a clear fill so its middle is clickable too.
        background.raycastTarget = true;

        if (outline)
        {
            Image hit = CreateImage(root.transform, "HitArea", FenerPlaceholderArt.Load("square"), Vector2.zero, rect);
            hit.color = Color.clear;
            hit.raycastTarget = true;
        }

        var button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        TMP_Text label = CreateText(root.transform, "Label", caption, 30f, Vector2.zero, rect);
        label.fontStyle = FontStyles.Bold;
        label.color = outline ? tone : dark ? Cream : Ink;

        var juice = root.AddComponent<UIButtonJuice>();
        using (var fields = new FenerEditorUtility.Fields(juice))
        {
            fields.Set("button", button)
                  .Set("glow", glow)
                  .Set("breathe", breathe);
        }

        return button;
    }

    /// <summary>A nine-sliced image. <paramref name="cornerScale"/> above 1 shrinks the corners, below 1 grows them.</summary>
    private static Image CreateSliced(Transform parent, string imageName, Sprite sprite, Vector2 anchoredPosition, Vector2 size, float cornerScale)
    {
        Image image = CreateImage(parent, imageName, sprite, anchoredPosition, size);
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = cornerScale;
        return image;
    }

    private static void UseHandwriting(TMP_Text label)
    {
        TMP_FontAsset hand = FenerFonts.Hand();
        if (hand != null)
        {
            label.font = hand;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Image CreateImage(Transform parent, string imageName, Sprite sprite, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(imageName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;

        Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, size);
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string textName, string content, float size, Vector2 anchoredPosition, Vector2 rectSize)
    {
        var go = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();

        TMP_FontAsset ui = FenerFonts.Ui();
        if (ui != null)
        {
            label.font = ui;
        }

        label.text = content;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.94f, 0.88f);
        label.raycastTarget = false;

        Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, rectSize);
        return label;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem", typeof(EventSystem));
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static T InstantiatePrefab<T>(string path, Transform parent, Vector2 position) where T : Component
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogError($"Fener: {path} bulunamadı. Önce 'Fener/3 - Prefabları kur' çalıştır.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.transform.position = new Vector3(position.x, position.y, 0f);
        return instance.GetComponent<T>();
    }

    private static void SetBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }
}
