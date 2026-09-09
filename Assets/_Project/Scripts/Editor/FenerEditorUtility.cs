using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small shared helpers for the Fener builders.
///
/// The one that earns its keep is <see cref="Fields"/>: every reference the
/// game needs is a private [SerializeField], which is right for the runtime
/// code and unreachable from an editor script without going through
/// SerializedObject. Wrapping that once keeps the builders readable as a list
/// of what is wired to what.
/// </summary>
public static class FenerEditorUtility
{
    /// <summary>Creates <paramref name="path"/> and every folder above it that is missing.</summary>
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    /// <summary>
    /// Writes to a component's serialized fields, private ones included, and
    /// applies them on dispose.
    /// </summary>
    public sealed class Fields : IDisposable
    {
        private readonly SerializedObject serialized;
        private readonly UnityEngine.Object target;

        public Fields(UnityEngine.Object target)
        {
            this.target = target;
            serialized = new SerializedObject(target);
        }

        /// <summary>Assigns an object reference.</summary>
        public Fields Set(string field, UnityEngine.Object value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }

            return this;
        }

        public Fields Set(string field, float value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.floatValue = value;
            }

            return this;
        }

        public Fields Set(string field, int value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.intValue = value;
            }

            return this;
        }

        public Fields Set(string field, bool value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.boolValue = value;
            }

            return this;
        }

        public Fields Set(string field, string value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.stringValue = value;
            }

            return this;
        }

        public Fields Set(string field, Color value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.colorValue = value;
            }

            return this;
        }

        public Fields Set(string field, Vector2 value)
        {
            SerializedProperty property = Find(field);
            if (property != null)
            {
                property.vector2Value = value;
            }

            return this;
        }

        /// <summary>Fills an array of object references, resizing it to match.</summary>
        public Fields SetArray(string field, UnityEngine.Object[] values)
        {
            SerializedProperty property = Find(field);
            if (property == null)
            {
                return this;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            return this;
        }

        private SerializedProperty Find(string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"Fener: {target.GetType().Name} has no field '{field}'; the builder and the script have drifted apart.", target);
            }

            return property;
        }

        public void Dispose()
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
