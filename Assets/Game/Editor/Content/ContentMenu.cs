using Game.Data.Definitions;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>Content menu items (SPEC-04 §5.7, DATA-008).</summary>
    public static class ContentMenu
    {
        [MenuItem("Game/Content/Rebuild and Validate")]
        static void RebuildAndValidate() => ContentValidatorRunner.Run(out _);

        [MenuItem("Game/Content/Create Mirror Partner")]
        static void CreateMirrorPartner()
        {
            var created = MirrorPartnerCreator.Create((BlockDefinition)Selection.activeObject);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        [MenuItem("Game/Content/Create Mirror Partner", true)]
        static bool CanCreateMirrorPartner() => Selection.activeObject is BlockDefinition;
    }
}
