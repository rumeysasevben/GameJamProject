using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires dropped-in audio files to the game by their filenames.
///
/// The alternative is dragging thirty clips into Inspector slots and getting
/// one of them wrong in a way nobody notices until the build. Naming a file
/// <c>link_success.wav</c> and running this is the whole job.
///
/// Variations are numbered: <c>collide_splash_1</c>, <c>collide_splash_2</c>
/// and so on all become variations of <c>collide_splash</c>, and the game picks
/// between them each time it plays.
///
/// It also applies the import settings WebGL needs. Effects are decompressed
/// into memory so the first collision of a session is not late; music stays
/// compressed, because five uncompressed two-minute layers is most of a build.
/// </summary>
public static class FenerAudioLinker
{
    private const string SfxFolder = "Assets/_Project/Audio/SFX";
    private const string MusicFolder = "Assets/_Project/Audio/Music";
    private const string LibraryPath = "Assets/_Project/Data/SfxLibrary.asset";
    private const string BootScenePath = "Assets/Scenes/Boot.unity";

    /// <summary>The looping hum, assigned to its own slot rather than the library.</summary>
    private const string HumId = "beam_hum";

    /// <summary>Music layers in order. Index 0 plays from the start of a night; the rest open as ships arrive.</summary>
    private static readonly string[] MusicOrder = { "music_base", "music_layer_1", "music_layer_2", "music_layer_3", "music_layer_4" };

    private static readonly Regex VariationSuffix = new Regex(@"_\d+$");

    [MenuItem("Fener/6 - Ses dosyalarını bağla", priority = 6)]
    public static void LinkAll()
    {
        FenerEditorUtility.EnsureFolder(SfxFolder);
        FenerEditorUtility.EnsureFolder(MusicFolder);

        ApplyImportSettings(SfxFolder, music: false);
        ApplyImportSettings(MusicFolder, music: true);

        Dictionary<string, List<AudioClip>> sfx = Collect(SfxFolder);
        Dictionary<string, List<AudioClip>> music = Collect(MusicFolder);

        int wired = FillLibrary(sfx);
        WireAudioManager(sfx, music);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fener: {wired} ses ismi bağlandı. Boş kalan isimler sessiz çalışır.");
    }

    /// <summary>
    /// Groups every clip in a folder by its id, with numbered files collapsing
    /// into variations of one sound.
    /// </summary>
    private static Dictionary<string, List<AudioClip>> Collect(string folder)
    {
        var found = new Dictionary<string, List<AudioClip>>();

        if (!AssetDatabase.IsValidFolder(folder))
        {
            return found;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                continue;
            }

            string id = VariationSuffix.Replace(Path.GetFileNameWithoutExtension(path), string.Empty);

            if (!found.TryGetValue(id, out List<AudioClip> clips))
            {
                clips = new List<AudioClip>();
                found[id] = clips;
            }

            clips.Add(clip);
        }

        // Sorted by name, so variation 1 is always variation 1 and a rebuild
        // does not shuffle the asset around in version control.
        foreach (List<AudioClip> clips in found.Values)
        {
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        return found;
    }

    /// <summary>Fills the library's clip slots. Ids with no files are left alone and stay silent.</summary>
    private static int FillLibrary(Dictionary<string, List<AudioClip>> sfx)
    {
        var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);

        if (library == null)
        {
            Debug.LogWarning($"Fener: {LibraryPath} yok. Önce 'Fener/2 - Gemi tipleri ve geceleri üret' çalıştır.");
            return 0;
        }

        var entries = new List<SfxLibrary.Entry>(library.entries ?? new SfxLibrary.Entry[0]);
        int wired = 0;

        foreach (KeyValuePair<string, List<AudioClip>> pair in sfx)
        {
            if (pair.Key == HumId)
            {
                continue;
            }

            SfxLibrary.Entry entry = entries.Find(e => e != null && e.id == pair.Key);

            // A file whose name the game never asks for still gets an entry, so
            // it shows up in the Inspector instead of being silently ignored.
            if (entry == null)
            {
                entry = new SfxLibrary.Entry { id = pair.Key };
                entries.Add(entry);
                Debug.LogWarning($"Fener: '{pair.Key}' oyunun istediği isimlerden biri değil; yine de kütüphaneye eklendi.");
            }

            entry.clips = pair.Value.ToArray();
            wired++;
        }

        library.entries = entries.ToArray();
        EditorUtility.SetDirty(library);
        return wired;
    }

    /// <summary>Puts the hum and the music layers on the Boot scene's AudioManager.</summary>
    private static void WireAudioManager(Dictionary<string, List<AudioClip>> sfx, Dictionary<string, List<AudioClip>> music)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null)
        {
            Debug.LogWarning("Fener: Boot sahnesi yok; önce 'Fener/4 - Sahneleri kur' çalıştır.");
            return;
        }

        Scene current = SceneManager.GetActiveScene();
        bool reopen = current.path != BootScenePath;
        string returnTo = current.path;

        if (reopen && current.isDirty)
        {
            EditorSceneManager.SaveScene(current);
        }

        Scene boot = reopen ? EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single) : current;

        AudioManager manager = Object.FindFirstObjectByType<AudioManager>();

        if (manager == null)
        {
            Debug.LogWarning("Fener: Boot sahnesinde AudioManager yok; 'Fener/4 - Sahneleri kur' çalıştır.");
            return;
        }

        AudioClip hum = sfx.TryGetValue(HumId, out List<AudioClip> humClips) && humClips.Count > 0 ? humClips[0] : null;

        var layers = new List<AudioClip>();
        foreach (string id in MusicOrder)
        {
            if (music.TryGetValue(id, out List<AudioClip> clips) && clips.Count > 0)
            {
                layers.Add(clips[0]);
            }
        }

        using (var fields = new FenerEditorUtility.Fields(manager))
        {
            fields.Set("library", AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath))
                  .Set("humClip", hum)
                  .SetArray("musicLayers", layers.ToArray());
        }

        EditorSceneManager.MarkSceneDirty(boot);
        EditorSceneManager.SaveScene(boot);

        Debug.Log($"Fener: huzme vınlaması {(hum != null ? "bağlandı" : "yok")}, {layers.Count} müzik katmanı bağlandı.");

        if (reopen && !string.IsNullOrEmpty(returnTo))
        {
            EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// The import settings WebGL wants: effects ready to play the instant they
    /// are asked for, music kept small.
    /// </summary>
    private static void ApplyImportSettings(string folder, bool music)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;

            if (importer == null)
            {
                continue;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = music ? 0.6f : 0.7f;

            // Effects are preloaded so the first collision of a session is not
            // late; music is not, because loading five two-minute layers before
            // the menu appears is most of a WebGL startup. Preloading is a
            // per-platform sample setting in Unity 6, not an importer flag.
            settings.preloadAudioData = !music;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }
    }
}
