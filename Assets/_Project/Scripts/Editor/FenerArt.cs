using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The one place that says which sprite each part of the game uses.
///
/// Every lookup falls back to the generated placeholder, so a missing or
/// not-yet-drawn asset degrades to a box instead of an empty renderer. That is
/// what lets the builders be run at any point in the jam and still produce
/// something playable.
///
/// It also owns the import settings. The art is drawn several times larger
/// than it is meant to appear — the mock-up screens are 1920×1080 for a
/// 19.2 × 10.8 world, so one world unit is 100 screen pixels — and the pixels
/// per unit below are what bring each sprite back to the size it has in those
/// mock-ups. Change a sprite's size here, not by scaling the prefab, or the
/// collision radii stop matching what is drawn.
///
/// The rules are matched by folder rather than listed file by file, so art
/// added later is imported correctly without anyone remembering to come back
/// here.
/// </summary>
public static class FenerArt
{
    private const string Root = "Assets/_Project/Art";

    /// <summary>Import settings for one sprite: how big it is in world units, and where its pivot sits.</summary>
    private struct ImportRule
    {
        public float pixelsPerUnit;
        public SpriteAlignment alignment;
        public Vector2 customPivot;

        public static ImportRule Centered(float ppu)
        {
            return new ImportRule { pixelsPerUnit = ppu, alignment = SpriteAlignment.Center, customPivot = new Vector2(0.5f, 0.5f) };
        }

        public static ImportRule BottomCentered(float ppu)
        {
            return new ImportRule { pixelsPerUnit = ppu, alignment = SpriteAlignment.BottomCenter, customPivot = new Vector2(0.5f, 0f) };
        }

        public static ImportRule LeftCentered(float ppu)
        {
            return new ImportRule { pixelsPerUnit = ppu, alignment = SpriteAlignment.Custom, customPivot = new Vector2(0f, 0.5f) };
        }
    }

    /// <summary>
    /// The eight heading drawings, named by the angle the artist turned the
    /// hull through. They run clockwise on screen from south-east, so the file
    /// called 000 faces south-east and the one called 225 faces north.
    ///
    /// This table converts that to the game's heading index, where 0 is east
    /// and the eight run counter-clockwise — the §7 table.
    /// </summary>
    private static readonly string[] HeadingSuffixes = { "315", "270", "225", "180", "135", "090", "045", "000" };

    // ------------------------------------------------------------ Lookups

    public static Sprite Cone() => Load("FX/beam_cone", "square");
    public static Sprite TargetRing() => Load("FX/target_ring", "ring");
    public static Sprite WindowGlow() => Load("FX/window_glow", "circle");
    public static Sprite FogBlob() => Load("FX/fog_blob", "square");

    // Generated until drawn: drop FX/beam_marker.png in and it is used instead.
    public static Sprite BeamMarker() => Load("FX/beam_marker", "marker");
    public static Sprite LighthouseBody() => Load("lighthouse/lighthouse_body", "square");

    // The artist moved the sea dressing into its own folder; either location
    // works, so neither a stale nor a tidied project breaks.
    public static Sprite Rock() => LoadFirst("circle", "Sea/rock_01", "rock_01");
    public static Sprite Kelp() => LoadFirst("circle", "Sea/kelp_01", "kelp_01");
    public static Sprite Wave() => LoadFirst("square", "Sea/wave_line", "wave_line");

    public static Sprite HouseDark() => Load("House/house_dark", "square");
    public static Sprite HouseLit() => Load("House/house_lit", "square");

    public static Sprite SlotEmpty() => Load("UI/sequence_slot_empty", "square");
    public static Sprite SlotFilled() => Load("UI/sequence_slot_filled", "square");
    public static Sprite SlotSuccess() => Load("UI/sequence_slot_success", "square");
    public static Sprite Dot() => Load("UI/sequence_dot", "dot");
    public static Sprite Dash() => Load("UI/sequence_dash", "dash");
    public static Sprite Bubble() => Load("UI/bubble_sequence", "square");
    public static Sprite NoteCard() => Load("UI/note_card_frame", "square");
    public static Sprite Panel() => Load("UI/panel_frame", "square");

    // The panel kit from the UI mock-up. Always generated: these are nine-sliced
    // shapes, and a drawn replacement would need its slice borders set by hand.
    public static Sprite RoundedPanel() => FenerPlaceholderArt.Load("rounded");
    public static Sprite Pill() => FenerPlaceholderArt.Load("pill");
    public static Sprite PillOutline() => FenerPlaceholderArt.Load("pill_outline");
    public static Sprite SoftShadow() => FenerPlaceholderArt.Load("soft_shadow");
    public static Sprite DawnGradient() => FenerPlaceholderArt.Load("dawn_gradient");

    /// <summary>The real sprite at <paramref name="relativePath"/>, or the named placeholder if it is not there.</summary>
    public static Sprite Load(string relativePath, string placeholder)
    {
        Sprite sprite = Find(relativePath);
        return sprite != null ? sprite : FenerPlaceholderArt.Load(placeholder);
    }

    /// <summary>The first of <paramref name="relativePaths"/> that exists, or the placeholder.</summary>
    public static Sprite LoadFirst(string placeholder, params string[] relativePaths)
    {
        for (int i = 0; i < relativePaths.Length; i++)
        {
            Sprite sprite = Find(relativePaths[i]);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return FenerPlaceholderArt.Load(placeholder);
    }

    // ------------------------------------------------------------ Ship headings

    /// <summary>
    /// A vessel's eight heading sprites, index 0 east and running
    /// counter-clockwise.
    ///
    /// <paramref name="bound"/> asks for the "bound" set instead — the same
    /// hulls with the lighthouse's halo around them, which is what a ship
    /// answering the beam looks like.
    /// </summary>
    public static Sprite[] ShipDirections(ShipKind kind, bool bound)
    {
        string folder = FolderFor(kind);
        string prefix = PrefixFor(kind);
        string suffix = bound ? "_bound" : string.Empty;

        var sprites = new Sprite[8];
        bool anyMissing = false;

        for (int i = 0; i < 8; i++)
        {
            sprites[i] = Find($"{folder}/{prefix}_{HeadingSuffixes[i]}{suffix}");

            if (sprites[i] == null)
            {
                anyMissing = true;
            }
        }

        // A half-drawn set is worse than none: the ship would flick between
        // real art and boxes as it turned. Report it and fall back whole.
        if (anyMissing)
        {
            for (int i = 0; i < 8; i++)
            {
                if (sprites[i] == null)
                {
                    sprites[i] = bound ? null : FenerPlaceholderArt.Load($"ship_dir_{i}");
                }
            }

            if (bound)
            {
                return null;
            }

            Debug.LogWarning($"Fener: {kind} için sekiz yönün hepsi yok; eksikler kutuyla dolduruldu.");
        }

        return sprites;
    }

    private static string FolderFor(ShipKind kind)
    {
        switch (kind)
        {
            case ShipKind.Kayik: return "Ship_kayik";
            case ShipKind.Balikci: return "Shiip_balikci";
            default: return "Ship_yuk";
        }
    }

    private static string PrefixFor(ShipKind kind)
    {
        switch (kind)
        {
            case ShipKind.Kayik: return "ship_kayik";
            case ShipKind.Balikci: return "ship_balikci";
            default: return "ship_yuk";
        }
    }

    private static Sprite Find(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{relativePath}.png");
    }

    // ------------------------------------------------------------ Import

    /// <summary>
    /// The size and pivot every sprite in a folder gets. Returns false for art
    /// that is reference material rather than something the game draws.
    /// </summary>
    private static bool RuleFor(string relativePath, out ImportRule rule)
    {
        rule = ImportRule.Centered(100f);

        // Mock-ups and the itch cover are for looking at, not for importing.
        if (relativePath.StartsWith("Screens/") || relativePath.StartsWith("Placeholder/"))
        {
            return false;
        }

        // Hulls, sized so a rowboat is about 0.9 units across, a fishing boat
        // 1.3 and a freighter 1.9 — the same ratio their collision radii use.
        if (relativePath.StartsWith("Ship_kayik/"))
        {
            rule = ImportRule.Centered(310f);
            return true;
        }

        if (relativePath.StartsWith("Shiip_balikci/"))
        {
            rule = ImportRule.Centered(230f);
            return true;
        }

        if (relativePath.StartsWith("Ship_yuk/"))
        {
            rule = ImportRule.Centered(315f);
            return true;
        }

        // Things that stand on the ground are pivoted at their foot.
        if (relativePath.StartsWith("lighthouse/"))
        {
            rule = ImportRule.BottomCentered(260f);
            return true;
        }

        if (relativePath.StartsWith("House/"))
        {
            rule = ImportRule.BottomCentered(355f);
            return true;
        }

        // The cone's pivot is its apex, on the left edge, so the beam rotates
        // about the lamp and points along its own +X.
        if (relativePath == "FX/beam_cone")
        {
            rule = ImportRule.LeftCentered(100f);
            return true;
        }

        if (relativePath.StartsWith("FX/target_ring"))
        {
            rule = ImportRule.Centered(290f);
            return true;
        }

        if (relativePath.StartsWith("FX/window_glow"))
        {
            rule = ImportRule.Centered(200f);
            return true;
        }

        if (relativePath.StartsWith("FX/fog_blob"))
        {
            rule = ImportRule.Centered(90f);
            return true;
        }

        if (relativePath.EndsWith("rock_01"))
        {
            rule = ImportRule.Centered(310f);
            return true;
        }

        if (relativePath.EndsWith("kelp_01"))
        {
            rule = ImportRule.Centered(220f);
            return true;
        }

        if (relativePath.EndsWith("wave_line"))
        {
            rule = ImportRule.Centered(200f);
            return true;
        }

        // UI sprites are sized by their RectTransform, so the unit scale does
        // not matter; they only need to be sprites at all.
        rule = ImportRule.Centered(100f);
        return true;
    }

    /// <summary>
    /// Reimports the hand-drawn art at the sizes and pivots the game expects.
    /// Run before anything else builds, or prefabs pick up sprites at whatever
    /// scale they happened to import at.
    /// </summary>
    [MenuItem("Fener/0 - Sanatı içe aktar (boyut ve pivot)", priority = 0)]
    public static void ApplyImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
        int changed = 0;
        var skipped = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png"))
            {
                continue;
            }

            string relative = Path.ChangeExtension(path.Substring(Root.Length + 1), null).Replace('\\', '/');

            if (!RuleFor(relative, out ImportRule rule))
            {
                skipped.Add(relative);
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = rule.pixelsPerUnit;
            settings.spriteAlignment = (int)rule.alignment;
            settings.spritePivot = rule.customPivot;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;

            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            changed++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Fener: {changed} görsel içe aktarıldı, {skipped.Count} referans görseli atlandı.");
    }
}
