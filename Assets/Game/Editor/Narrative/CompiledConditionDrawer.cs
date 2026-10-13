using Game.Data.Logic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 NAR-009: condition source, compile status and first diagnostic.</summary>
    [CustomPropertyDrawer(typeof(CompiledCondition))]
    public sealed class CompiledConditionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => CompiledLogicDrawer.Height(property);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => CompiledLogicDrawer.Draw(position, property, label, false);
    }
}
