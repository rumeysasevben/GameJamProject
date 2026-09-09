using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Builds the four gameplay prefabs and the one UI prefab they need, wired to
/// the placeholder art.
///
/// Written as code rather than assembled by hand because the wiring is what
/// actually breaks: a ship with an unassigned lamp or a missing SortingGroup
/// looks fine in the Inspector and is wrong the moment two ships overlap. Here
/// it is a list that can be read and re-run.
///
/// Re-running overwrites the prefab contents but keeps each prefab's GUID, so
/// scenes and assets pointing at them survive.
/// </summary>
public static class FenerPrefabBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs";
    private const string UiPrefabFolder = PrefabFolder + "/UI";
    private const string MaterialFolder = "Assets/_Project/Art/Materials";

    [MenuItem("Fener/3 - Prefabları kur", priority = 3)]
    public static void BuildAll()
    {
        FenerEditorUtility.EnsureFolder(PrefabFolder);
        FenerEditorUtility.EnsureFolder(UiPrefabFolder);
        FenerEditorUtility.EnsureFolder(MaterialFolder);

        Image symbol = BuildSequenceSymbol();
        BuildShip(symbol);
        BuildDock();
        BuildRock();
        BuildLighthouse();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fener: prefablar kuruldu → {PrefabFolder}");
    }

    /// <summary>
    /// One symbol image, spawned into a ship's bubble. A prefab of its own so
    /// the bubble can build a sequence of any length out of it.
    /// </summary>
    private static Image BuildSequenceSymbol()
    {
        var root = new GameObject("SequenceSymbol", typeof(RectTransform), typeof(Image), typeof(LayoutElement));

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(24f, 24f);

        var image = root.GetComponent<Image>();
        image.sprite = FenerArt.Dot();
        image.raycastTarget = false;

        var layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 24f;
        layout.preferredHeight = 24f;

        GameObject saved = Save(root, $"{UiPrefabFolder}/SequenceSymbol.prefab");
        return saved.GetComponent<Image>();
    }

    private static void BuildShip(Image symbolPrefab)
    {
        var root = new GameObject("Ship", typeof(SortingGroup), typeof(Ship), typeof(ShipSignalEmitter));

        // The hull is replaced at runtime from the ship's type; this is only
        // what the prefab shows in the project window.
        SpriteRenderer body = AddSprite(root.transform, "Body", FenerArt.Load("Ship_kayik/ship_kayik_000", "ship_dir_0"), Vector2.zero, Vector2.one, 0);

        SpriteRenderer wake = AddSprite(root.transform, "Wake", FenerArt.Wave(), new Vector2(0f, -0.25f), Vector2.one, -1);
        wake.color = new Color(1f, 1f, 1f, 0.4f);
        wake.enabled = false;

        SpriteRenderer lamp = AddSprite(root.transform, "Lamp", FenerPlaceholderArt.Load("circle"), new Vector2(0f, 0.18f), new Vector2(0.14f, 0.14f), 2);
        lamp.color = new Color(1f, 0.95f, 0.75f);
        lamp.enabled = false;

        // The lamp's own light. In fog this is all that shows of a waiting ship,
        // so it lives on the lamp and is switched with it.
        var lampLight = lamp.gameObject.AddComponent<Light2D>();
        lampLight.lightType = Light2D.LightType.Point;
        lampLight.color = new Color(1f, 0.93f, 0.7f);
        lampLight.intensity = 1.6f;
        lampLight.pointLightOuterRadius = 0.8f;
        lampLight.pointLightInnerRadius = 0.05f;
        lampLight.enabled = false;

        SpriteRenderer targetRing = AddSprite(root.transform, "TargetRing", FenerArt.TargetRing(), new Vector2(0f, -0.1f), Vector2.one, -2);
        targetRing.color = new Color(1f, 1f, 1f, 0.9f);
        targetRing.enabled = false;

        SpriteRenderer linkedHalo = AddSprite(root.transform, "LinkedHalo", FenerPlaceholderArt.Load("circle"), Vector2.zero, new Vector2(1.4f, 1.4f), -3);
        linkedHalo.color = new Color(1f, 0.9f, 0.6f, 0.35f);
        linkedHalo.enabled = false;

        SequenceBubbleUI bubble = AddBubble(root.transform, symbolPrefab);

        Ship ship = root.GetComponent<Ship>();
        using (var fields = new FenerEditorUtility.Fields(ship))
        {
            fields.Set("body", body)
                  .Set("wake", wake)
                  .Set("lamp", lamp)
                  .Set("targetRing", targetRing)
                  .Set("linkedHalo", linkedHalo)
                  .Set("emitter", root.GetComponent<ShipSignalEmitter>())
                  .Set("bubble", bubble);
        }

        using (var fields = new FenerEditorUtility.Fields(bubble))
        {
            fields.Set("target", root.transform);
        }

        Save(root, $"{PrefabFolder}/Ship.prefab");
    }

    /// <summary>
    /// The world-space bubble above a ship. A canvas per ship rather than one
    /// shared canvas: the bubble has to sort and fade with its own ship, and in
    /// fog it has to be able to disappear with it.
    /// </summary>
    private static SequenceBubbleUI AddBubble(Transform parent, Image symbolPrefab)
    {
        var root = new GameObject("Bubble", typeof(RectTransform), typeof(Canvas), typeof(SequenceBubbleUI));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        // 100 px to the unit, matching the sprites, so the bubble is the size it
        // looks in the UI mockups.
        root.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(200f, 64f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);

        // No background behind the symbols. A cream panel over every hull is a
        // lot of furniture for two dots, and the symbols read fine on their own
        // against dark water.
        var panelImage = panel.GetComponent<Image>();
        panelImage.enabled = false;
        panelImage.raycastTarget = false;

        var layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bubble = root.GetComponent<SequenceBubbleUI>();
        using (var fields = new FenerEditorUtility.Fields(bubble))
        {
            fields.Set("panel", panel)
                  .Set("symbolParent", panel.transform)
                  .Set("symbolPrefab", symbolPrefab)
                  .Set("shortSprite", FenerArt.Dot())
                  .Set("longSprite", FenerArt.Dash())
                  .Set("offset", new Vector2(0f, 0.7f));
        }

        return bubble;
    }

    private static void BuildDock()
    {
        var root = new GameObject("Dock", typeof(Dock));

        SpriteRenderer pier = AddSprite(root.transform, "Pier", FenerPlaceholderArt.Load("square"), Vector2.zero, new Vector2(1.2f, 0.35f), 0);
        pier.color = new Color(0.45f, 0.35f, 0.28f);

        SpriteRenderer strip = AddSprite(root.transform, "ColorStrip", FenerPlaceholderArt.Load("square"), new Vector2(0f, 0.28f), new Vector2(0.6f, 0.1f), 1);
        strip.enabled = false;

        var lamp = new GameObject("Lamp");
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0.35f, 0f);

        var light = lamp.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.85f, 0.6f);
        light.intensity = 1.2f;
        light.pointLightOuterRadius = 1.6f;

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<Dock>()))
        {
            fields.Set("pier", pier).Set("colorStrip", strip);
        }

        Save(root, $"{PrefabFolder}/Dock.prefab");
    }

    private static void BuildRock()
    {
        var root = new GameObject("Rock", typeof(Rock));

        SpriteRenderer body = AddSprite(root.transform, "Body", FenerArt.Rock(), Vector2.zero, Vector2.one, 0);

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<Rock>()))
        {
            fields.Set("body", body);
        }

        Save(root, $"{PrefabFolder}/Rock.prefab");
    }

    private static void BuildLighthouse()
    {
        var root = new GameObject("Lighthouse", typeof(Lighthouse));

        // The tower art is pivoted at its foot, so the prefab's own origin is
        // where it stands on the ground.
        SpriteRenderer body = AddSprite(root.transform, "Body", FenerArt.LighthouseBody(), Vector2.zero, Vector2.one, 0);

        var lampPoint = new GameObject("LampPoint");
        lampPoint.transform.SetParent(root.transform, false);

        // The lamp room sits about two thirds up the drawing; the beam has to
        // leave from there and not from the top of the canvas.
        lampPoint.transform.localPosition = new Vector3(0f, LampHeight(body.sprite), 0f);

        // The beam sits at the lamp with no offset of its own: its transform is
        // the cone's apex, and its local +X is the centre line.
        var beamObject = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer), typeof(Beam));
        beamObject.transform.SetParent(lampPoint.transform, false);

        var renderer = beamObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = LoadOrCreateBeamMaterial();
        renderer.sortingOrder = 20;

        // The drawn cone: apex on its left edge, so it points along the beam's
        // local +X with no offset of its own. Beam stretches it to whatever
        // angle and reach GameConfig asks for, and switches the mesh off.
        SpriteRenderer cone = AddSprite(beamObject.transform, "ConeSprite", FenerArt.Cone(), Vector2.zero, Vector2.one, 20);
        cone.color = new Color(1f, 0.96f, 0.82f, 0.75f);

        // The light is a child so its own rotation can be trimmed if URP's spot
        // does not point exactly along the mesh; the cone itself never moves.
        var beamLightObject = new GameObject("BeamLight");
        beamLightObject.transform.SetParent(beamObject.transform, false);

        var beamLight = beamLightObject.AddComponent<Light2D>();
        beamLight.lightType = Light2D.LightType.Point;
        beamLight.color = new Color(1f, 0.92f, 0.72f);
        beamLight.intensity = 1.2f;
        beamLight.pointLightOuterRadius = 22f;
        beamLight.pointLightInnerRadius = 0.5f;
        beamLight.pointLightInnerAngle = 20f;
        beamLight.pointLightOuterAngle = 24f;

        var glowObject = new GameObject("LampGlow");
        glowObject.transform.SetParent(lampPoint.transform, false);

        var glow = glowObject.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = new Color(1f, 0.95f, 0.8f);
        glow.intensity = 1.5f;
        glow.pointLightOuterRadius = 1.2f;

        var beam = beamObject.GetComponent<Beam>();
        using (var fields = new FenerEditorUtility.Fields(beam))
        {
            fields.Set("beamLight", beamLight)
                  .Set("coneSprite", cone)
                  .Set("config", LoadConfig());
        }

        using (var fields = new FenerEditorUtility.Fields(root.GetComponent<Lighthouse>()))
        {
            fields.Set("lampPoint", lampPoint.transform).Set("beam", beam);
        }

        Save(root, $"{PrefabFolder}/Lighthouse.prefab");
    }

    /// <summary>
    /// The cone's material. Sprites/Default multiplies by vertex colour and
    /// blends transparently, which is all the fan mesh asks for — the soft edge
    /// is in the vertices, not in a shader.
    /// </summary>
    private static Material LoadOrCreateBeamMaterial()
    {
        string path = $"{MaterialFolder}/BeamCone.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = new Color(1f, 0.93f, 0.72f, 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>The one GameConfig asset, or null with a warning if it has not been made yet.</summary>
    public static GameConfig LoadConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");

        if (config == null)
        {
            Debug.LogWarning("Fener: Assets/_Project/Data/GameConfig.asset bulunamadı; referanslar boş kalacak.");
        }

        return config;
    }

    /// <summary>
    /// How far above its foot the tower's lamp sits, in world units. Measured
    /// as a fraction of the drawing's height rather than hard-coded, so a
    /// redrawn tower at a different size still gets its beam in the right
    /// place.
    /// </summary>
    private static float LampHeight(Sprite towerSprite)
    {
        const float lampFractionOfHeight = 0.66f;
        float height = towerSprite != null ? towerSprite.bounds.size.y : 2.6f;
        return height * lampFractionOfHeight;
    }

    private static SpriteRenderer AddSprite(Transform parent, string spriteName, Sprite sprite, Vector2 localPosition, Vector2 scale, int order)
    {
        var go = new GameObject(spriteName, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static GameObject Save(GameObject instance, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return saved;
    }
}
