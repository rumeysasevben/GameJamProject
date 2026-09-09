using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every sound effect in the game, named.
///
/// Code asks for "link_success", not for a clip. That way the audio can arrive
/// late, change, or gain variations without a single script edit — which
/// matters on a jam, where the sound is usually the last thing to land.
///
/// An id with no clips plays nothing and says nothing. Silence is the right
/// failure here: a missing footstep should not stop a night.
/// </summary>
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Fener/SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    /// <summary>One named sound, with however many variations it has.</summary>
    [System.Serializable]
    public class Entry
    {
        [Tooltip("What the code asks for.")]
        public string id;

        [Tooltip("Variations. One is picked at random, so a sound heard twenty times a night does not wear out.")]
        public AudioClip[] clips = new AudioClip[0];

        [Range(0f, 1f)]
        [Tooltip("Level for this sound relative to the rest.")]
        public float volume = 1f;

        [Tooltip("Pitch is randomised between these two, which does most of the work of making repeats bearable.")]
        public Vector2 pitchRange = new Vector2(0.97f, 1.03f);
    }

    [Tooltip("The sounds. Ids are matched exactly.")]
    public Entry[] entries = new Entry[0];

    private Dictionary<string, Entry> index;

    /// <summary>The entry for <paramref name="id"/>, or null if there is none.</summary>
    public Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        // Built on first use rather than in OnEnable: entries are edited in the
        // Inspector while the game is running and the map has to keep up.
        if (index == null || index.Count != entries.Length)
        {
            Rebuild();
        }

        return index.TryGetValue(id, out Entry entry) ? entry : null;
    }

    private void Rebuild()
    {
        index = new Dictionary<string, Entry>(entries.Length);

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null || string.IsNullOrEmpty(entries[i].id))
            {
                continue;
            }

            index[entries[i].id] = entries[i];
        }
    }

    private void OnValidate()
    {
        index = null;
    }
}
