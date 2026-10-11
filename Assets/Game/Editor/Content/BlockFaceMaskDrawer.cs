using Game.Data.Blocks;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>Six labelled toggles for a BlockFaceMask (SPEC-04 DATA-008).</summary>
    [CustomPropertyDrawer(typeof(BlockFaceMask))]
    public sealed class BlockFaceMaskDrawer : PropertyDrawer
    {
        static readonly string[] Names = { "+X", "−X", "+Y", "−Y", "+Z", "−Z" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var rect = EditorGUI.PrefixLabel(position, label);
            int value = property.intValue, w = (int)(rect.width / 6f);
            for (int f = 0; f < 6; f++)
            {
                bool on = (value & (1 << f)) != 0;
                bool next = EditorGUI.ToggleLeft(new Rect(rect.x + f * w, rect.y, w, rect.height), Names[f], on);
                if (next != on) value ^= 1 << f;
            }
            if (value != property.intValue) property.intValue = value;
            EditorGUI.EndProperty();
        }
    }
}
