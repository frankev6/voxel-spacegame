using UnityEditor;
using UnityEngine;

namespace Game.Editor.Narrative
{
    /// <summary>Shared Editor-only source rendering; compiled graphs remain owned by the import compiler.</summary>
    internal static class CompiledLogicDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;
        static int Rows(SerializedProperty property) => Mathf.Clamp(LogicCompilePostprocessor.SourceText(property.FindPropertyRelative("source")).Split('\n').Length, 3, 8);
        internal static float Height(SerializedProperty property) => Line * (Rows(property) + 1 + (property.FindPropertyRelative("hasErrors").boolValue ? 2 : 0)) + Gap * 2;
        internal static void Draw(Rect rect, SerializedProperty property, GUIContent label, bool effects)
        {
            EditorGUI.BeginProperty(rect, label, property);
            try
            {
                bool stale = LogicCompilePostprocessor.IsStale(property), errors = property.FindPropertyRelative("hasErrors").boolValue;
                var heading = new Rect(rect.x, rect.y, rect.width, Line);
                EditorGUI.LabelField(heading, label);
                string status = stale ? "Stale" : errors ? "Error" : "Compiled";
                var icon = EditorGUIUtility.IconContent(stale ? "console.warnicon.sml" : errors ? "console.erroricon.sml" : "TestPassed");
                EditorGUI.LabelField(new Rect(heading.xMax - 90, heading.y, 90, Line), new GUIContent(status, icon.image,
                    stale ? "Recompile (Game/Narrative/Recompile All Logic)." : status));
                var source = property.FindPropertyRelative("source"); string text = LogicCompilePostprocessor.SourceText(source);
                EditorGUI.BeginChangeCheck();
                string next = EditorGUI.TextArea(new Rect(rect.x, rect.y + Line + Gap, rect.width, Line * Rows(property)), text);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!effects) source.stringValue = next;
                    else
                    {
                        var lines = next.Length == 0 ? System.Array.Empty<string>() : next.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                        source.arraySize = lines.Length;
                        for (int i = 0; i < lines.Length; i++) source.GetArrayElementAtIndex(i).stringValue = lines[i];
                    }
                    // The enclosing Inspector applies its serialized edit before the next delay callback.
                    foreach (var asset in property.serializedObject.targetObjects) LogicCompilePostprocessor.Queue(AssetDatabase.GetAssetPath(asset));
                }
                if (errors)
                    EditorGUI.HelpBox(new Rect(rect.x, rect.yMax - Line * 2, rect.width, Line * 2),
                        stale ? "Source changed; diagnostics will refresh after compilation." : LogicCompilePostprocessor.FirstError(property), MessageType.Error);
            }
            finally { EditorGUI.EndProperty(); }
        }
    }
}
