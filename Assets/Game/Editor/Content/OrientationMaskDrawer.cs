using Game.Data.Blocks;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>24 toggles in 6 rows labelled by forward face, plus All / Horizontal / Yaw / None (SPEC-04 DATA-008).</summary>
    [CustomPropertyDrawer(typeof(OrientationMask))]
    public sealed class OrientationMaskDrawer : PropertyDrawer
    {
        static readonly string[] Rows = { "Fwd +Z", "Fwd +X", "Fwd −Z", "Fwd −X", "Fwd +Y", "Fwd −Y" };
        const float Line = 18f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Line * (Rows.Length + 2);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var bits = property.FindPropertyRelative("Bits");
            uint value = (uint)bits.longValue;
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(new Rect(position.x, position.y, position.width, Line), label);
            float x0 = position.x + 70f, w = 34f;
            for (int row = 0; row < Rows.Length; row++)
            {
                float y = position.y + Line * (row + 1);
                EditorGUI.LabelField(new Rect(position.x + 8f, y, 62f, Line), Rows[row]);
                for (int roll = 0; roll < 4; roll++)
                {
                    int o = row * 4 + roll;
                    bool on = (value & (1u << o)) != 0;
                    bool next = EditorGUI.ToggleLeft(new Rect(x0 + roll * w, y, w, Line), o.ToString(), on);
                    if (next != on) value ^= 1u << o;
                }
            }
            float by = position.y + Line * (Rows.Length + 1), bw = (position.width - 8f) / 4f;
            if (GUI.Button(new Rect(position.x + 8f, by, bw - 2f, Line - 2f), "All")) value = OrientationMask.AllBits;
            if (GUI.Button(new Rect(position.x + 8f + bw, by, bw - 2f, Line - 2f), "Horizontal")) value = OrientationMask.HorizontalForwardBits;
            if (GUI.Button(new Rect(position.x + 8f + 2 * bw, by, bw - 2f, Line - 2f), "Yaw")) value = OrientationMask.YawOnlyBits;
            if (GUI.Button(new Rect(position.x + 8f + 3 * bw, by, bw - 2f, Line - 2f), "None")) value = 0;
            if (value != (uint)bits.longValue) bits.longValue = value;
            EditorGUI.EndProperty();
        }
    }
}
