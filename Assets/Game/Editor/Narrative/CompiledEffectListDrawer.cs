using Game.Data.Logic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 NAR-009: one command per source line, compile status and first diagnostic.</summary>
    [CustomPropertyDrawer(typeof(CompiledEffectList))]
    public sealed class CompiledEffectListDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => CompiledLogicDrawer.Height(property);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => CompiledLogicDrawer.Draw(position, property, label, true);
    }
}
