using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the box art the game is built and tuned on while the real sprites
/// are still being drawn.
///
/// Everything is white and shaped only enough to be told apart, because the
/// game tints sprites in code — the same placeholder square serves as sea,
/// pier and hull. The eight heading arrows are the exception: without a visible
/// facing there is no way to tell whether the turn rates feel right, which is
/// most of what the first playtests are for.
///
/// Real sprites drop in by replacing what the ShipType and prefabs point at.
/// Nothing in the game knows these are placeholders.
/// </summary>
public static class FenerPlaceholderArt
{
    /// <summary>Where the generated PNGs live. Safe to delete; regenerating rebuilds them.</summary>
    public const string Folder = "Assets/_Project/Art/Placeholder";

    private const int PixelsPerUnit = 100;

    [MenuItem("Fener/1 - Yer tutucu sanat üret", priority = 1)]
    public static void GenerateAll()
    {
        EnsureFolder();

        Square();
        Circle();
        Ring();
        Dot();
        Dash();
        Marker();
        Rounded();
        Pill();
        PillOutline();
        SoftShadow();
        DawnGradient();

        for (int i = 0; i < 8; i++)
        {
            Arrow(i);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fener: yer tutucu sanat hazır → {Folder}");
    }

    /// <summary>A plain white square. Hulls, piers, the sea, houses — anything rectangular.</summary>
    public static Sprite Square()
    {
        return Create("square", 32, (x, y, size) => 1f);
    }

    /// <summary>A soft white disc. Halos, lamps, rocks, splashes.</summary>
    public static Sprite Circle()
    {
        return Create("circle", 64, (x, y, size) =>
        {
            float r = size * 0.5f;
            float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
            return Mathf.Clamp01((r - d) / 1.5f);
        });
    }

    /// <summary>The flattened ring drawn under a targeted ship. Wide and short, so it reads as lying on the water.</summary>
    public static Sprite Ring()
    {
        return Create("ring", 96, (x, y, size) =>
        {
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float rx = size * 0.45f;
            float ry = size * 0.22f;
            float dx = (x - cx + 0.5f) / rx;
            float dy = (y - cy + 0.5f) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 1f) * 12f);
        });
    }

    /// <summary>The short symbol: a small dot.</summary>
    public static Sprite Dot()
    {
        return Create("dot", 64, (x, y, size) =>
        {
            float r = size * 0.22f;
            float c = size * 0.5f;
            float d = Mathf.Sqrt((x - c + 0.5f) * (x - c + 0.5f) + (y - c + 0.5f) * (y - c + 0.5f));
            return Mathf.Clamp01((r - d) / 1.5f);
        });
    }

    /// <summary>The long symbol: a bar.</summary>
    public static Sprite Dash()
    {
        return Create("dash", 64, (x, y, size) =>
        {
            bool inside = x > size * 0.12f && x < size * 0.88f && y > size * 0.40f && y < size * 0.60f;
            return inside ? 1f : 0f;
        });
    }

    /// <summary>
    /// The mark at the end of the beam: a thin ring around a small dot. Reads
    /// as a point being indicated rather than as another light.
    /// </summary>
    public static Sprite Marker()
    {
        return Create("marker", 64, (x, y, size) =>
        {
            float c = size * 0.5f;
            float d = Mathf.Sqrt((x - c + 0.5f) * (x - c + 0.5f) + (y - c + 0.5f) * (y - c + 0.5f));

            float ringRadius = size * 0.40f;
            float ringHalfWidth = size * 0.04f;
            float ring = Mathf.Clamp01((ringHalfWidth - Mathf.Abs(d - ringRadius)) / 1.2f);

            float dotRadius = size * 0.11f;
            float dot = Mathf.Clamp01((dotRadius - d) / 1.2f);

            return Mathf.Max(ring, dot);
        });
    }

    // ------------------------------------------------------------ Panels
    //
    // The UI shapes are nine-sliced: the border keeps the corners round however
    // wide the panel or button is stretched. They stay in use after real art
    // arrives, because they are what the mock-up panels are actually made of.

    /// <summary>A rounded rectangle. Cards, and — sliced very small — the window pips.</summary>
    public static Sprite Rounded()
    {
        const int size = 96;
        return CreateTexture("rounded", size, size, new Vector4(32f, 32f, 32f, 32f), (x, y) =>
            White(Mathf.Clamp01(0.5f - RoundedRect(x, y, size, size, 1f, 30f))));
    }

    /// <summary>A capsule, for the filled buttons.</summary>
    public static Sprite Pill()
    {
        const int size = 72;
        return CreateTexture("pill", size, size, new Vector4(35f, 35f, 35f, 35f), (x, y) =>
            White(Mathf.Clamp01(0.5f - RoundedRect(x, y, size, size, 1f, 35f))));
    }

    /// <summary>The outline of a capsule, for the secondary buttons.</summary>
    public static Sprite PillOutline()
    {
        const int size = 72;
        return CreateTexture("pill_outline", size, size, new Vector4(35f, 35f, 35f, 35f), (x, y) =>
        {
            float d = RoundedRect(x, y, size, size, 1f, 35f);
            return White(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 3.5f));
        });
    }

    /// <summary>A blurred rounded rectangle: card shadows and soft glows.</summary>
    public static Sprite SoftShadow()
    {
        const int size = 128;
        return CreateTexture("soft_shadow", size, size, new Vector4(60f, 60f, 60f, 60f), (x, y) =>
        {
            float d = RoundedRect(x, y, size, size, 26f, 22f);
            float s = Mathf.Clamp01((d + 10f) / 34f);
            return White(1f - s * s * (3f - 2f * s));
        });
    }

    /// <summary>
    /// The ending's sky, top to bottom: rose, peach, then the pale blue of
    /// morning. Coloured here rather than tinted, since a tint cannot make a
    /// gradient.
    /// </summary>
    public static Sprite DawnGradient()
    {
        var top = new Color(0.867f, 0.620f, 0.659f);
        var middle = new Color(0.957f, 0.808f, 0.678f);
        var bottom = new Color(0.702f, 0.804f, 0.878f);

        const int height = 256;
        return CreateTexture("dawn_gradient", 8, height, Vector4.zero, (x, y) =>
        {
            float t = 1f - (y + 0.5f) / height;
            return t < 0.5f
                ? Color.Lerp(top, middle, t / 0.5f)
                : Color.Lerp(middle, bottom, (t - 0.5f) / 0.5f);
        });
    }

    /// <summary>Signed distance from a pixel to a rounded rectangle inset from the texture's edge. Negative inside.</summary>
    private static float RoundedRect(int x, int y, int width, int height, float inset, float radius)
    {
        float px = x + 0.5f - width * 0.5f;
        float py = y + 0.5f - height * 0.5f;

        float qx = Mathf.Abs(px) - (width * 0.5f - inset) + radius;
        float qy = Mathf.Abs(py) - (height * 0.5f - inset) + radius;

        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }

    private static Color White(float alpha) => new Color(1f, 1f, 1f, alpha);

    /// <summary>
    /// One of the eight heading sprites: a triangle pointing along
    /// <paramref name="index"/> × 45°, with index 0 pointing east, matching the
    /// direction table in the technical document, §7.
    /// </summary>
    public static Sprite Arrow(int index)
    {
        float angle = index * 45f * Mathf.Deg2Rad;

        return Create($"ship_dir_{index}", 48, (x, y, size) =>
        {
            float c = size * 0.5f;
            Vector2 p = new Vector2(x - c + 0.5f, y - c + 0.5f);
            Vector2 forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-forward.y, forward.x);

            float along = Vector2.Dot(p, forward);
            float across = Mathf.Abs(Vector2.Dot(p, side));

            // A wedge: full width at the tail, closing to a point at the nose.
            float nose = size * 0.45f;
            float tail = -size * 0.30f;
            if (along > nose || along < tail)
            {
                return 0f;
            }

            float width = Mathf.Lerp(size * 0.28f, 0f, Mathf.InverseLerp(tail, nose, along));
            return across <= width ? 1f : 0f;
        });
    }

    /// <summary>Loads a generated sprite by name, creating the whole set first if it is missing.</summary>
    public static Sprite Load(string spriteName)
    {
        string path = $"{Folder}/{spriteName}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            GenerateAll();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return sprite;
    }

    /// <summary>
    /// Writes a square PNG whose alpha comes from <paramref name="alphaAt"/>,
    /// then imports it as a centre-pivoted sprite at the project's 100 pixels
    /// per unit. The colour is always white; tinting is the renderer's job.
    /// </summary>
    private static Sprite Create(string spriteName, int size, Func<int, int, int, float> alphaAt)
    {
        return CreateTexture(spriteName, size, size, Vector4.zero, (x, y) => White(Mathf.Clamp01(alphaAt(x, y, size))));
    }

    /// <summary>
    /// Writes a PNG coloured by <paramref name="colorAt"/> and imports it as a
    /// sprite. <paramref name="border"/> is the nine-slice border in pixels
    /// (left, bottom, right, top); zero for a sprite that is not sliced.
    /// </summary>
    private static Sprite CreateTexture(string spriteName, int width, int height, Vector4 border, Func<int, int, Color> colorAt)
    {
        EnsureFolder();

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = colorAt(x, y);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        string path = $"{Folder}/{spriteName}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.spriteBorder = border;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder()
    {
        FenerEditorUtility.EnsureFolder(Folder);
    }
}
