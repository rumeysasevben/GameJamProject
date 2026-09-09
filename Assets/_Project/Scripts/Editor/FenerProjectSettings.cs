using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies the project settings the technical document fixes in §1.3, so they
/// are recorded somewhere readable rather than remembered.
///
/// These are the settings that are invisible until they are wrong: the sort
/// axis that makes the isometric layering work, the Gzip compression itch.io
/// needs, and the WebGL template that stops the right mouse button opening the
/// browser menu — the single most common way a jam entry ships unplayable.
/// </summary>
public static class FenerProjectSettings
{
    [MenuItem("Fener/5 - Proje ayarlarını uygula", priority = 5)]
    public static void Apply()
    {
        // Sprites sort by Y, so a ship lower on the screen draws in front. This
        // is the whole of the game's "isometry" — there is no grid anywhere.
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);

        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        // Gzip rather than Brotli: itch.io does not always serve the headers
        // Brotli needs, and the fallback covers the hosts that serve neither.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:Fener";

        QualitySettings.vSyncCount = 0;

        AssetDatabase.SaveAssets();
        Debug.Log("Fener: proje ayarları uygulandı (sort axis, Linear, Gzip, Fener WebGL template).");
    }
}
