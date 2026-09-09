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
        EnsureFolder();

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt(x, y, size)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
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
