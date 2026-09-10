using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// The two typefaces from the UI mock-up, turned into TextMeshPro font assets
/// the first time a builder asks for them.
///
/// Nunito is the interface: headings, buttons, counters. Patrick Hand is the
/// handwriting — the notes the ships leave and the lines on the night's card —
/// so anything that reads as someone speaking looks written rather than set.
///
/// Both are Google Fonts under the SIL Open Font License; the licence texts sit
/// next to the files. The assets are dynamic, so any character the game ever
/// prints is added to the atlas as it is needed; the common ones are baked in
/// here so the first panel does not hitch while they are rendered.
/// </summary>
public static class FenerFonts
{
    private const string Folder = "Assets/_Project/Fonts";

    private const string Prewarm =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·—–’“”…çğıöşüÇĞİÖŞÜ";

    /// <summary>Nunito: the interface face. Null if the file is missing, in which case TextMeshPro's default is kept.</summary>
    public static TMP_FontAsset Ui() => LoadOrCreate("Nunito.ttf", "Nunito SDF.asset");

    /// <summary>Patrick Hand: the handwritten face.</summary>
    public static TMP_FontAsset Hand() => LoadOrCreate("PatrickHand-Regular.ttf", "PatrickHand SDF.asset");

    private static TMP_FontAsset LoadOrCreate(string fontFile, string assetFile)
    {
        string assetPath = $"{Folder}/{assetFile}";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>($"{Folder}/{fontFile}");
        if (font == null)
        {
            Debug.LogWarning($"Fener: {Folder}/{fontFile} bulunamadı; varsayılan font kullanılıyor.");
            return null;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null)
        {
            Debug.LogWarning($"Fener: {fontFile} için font asset üretilemedi.");
            return null;
        }

        // The atlas and material have to live inside the asset, or they are
        // lost the moment the editor reloads.
        asset.name = Path.GetFileNameWithoutExtension(assetFile);
        AssetDatabase.CreateAsset(asset, assetPath);

        asset.atlasTextures[0].name = $"{asset.name} Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);

        asset.material.name = $"{asset.name} Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        asset.TryAddCharacters(Prewarm);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"Fener: font asset üretildi → {assetPath}");
        return asset;
    }
}
