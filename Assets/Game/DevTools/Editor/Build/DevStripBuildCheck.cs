using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.DevTools.Editor.Build
{
    /// <summary>Reject serialized dev components in release scenes and their prefab dependency closure (SPEC-70 DEV-020).</summary>
    public sealed class DevStripBuildCheck : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        static bool Development(BuildReport report) => report != null && (report.summary.options & BuildOptions.Development) != 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (Development(report)) return;
            ValidateBuildContent();
        }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || Development(report)) return; // Play Mode scene processing is not a release build.
            foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(root, scene.path);
        }
        public static void ValidateBuildContent()
        {
            var roots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) roots.Add(scene.path);
            foreach (var asset in PlayerSettings.GetPreloadedAssets()) if (asset != null) roots.Add(AssetDatabase.GetAssetPath(asset));
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && path.Contains("/Resources/") && !path.Contains("/Editor/") && !AssetDatabase.IsValidFolder(path)) roots.Add(path);
            var rootArray = new string[roots.Count]; roots.CopyTo(rootArray);
            var dependencies = AssetDatabase.GetDependencies(rootArray, true);
            foreach (string path in dependencies)
            {
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab != null) CheckHierarchy(prefab, path);
                }
                else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path); CheckType(script != null ? script.GetClass() : null, path);
                }
                else if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try { foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(root, path); }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
            }
        }
        static void CheckHierarchy(GameObject root, string path)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) if (component != null) CheckType(component.GetType(), path);
        }
        static void CheckType(Type type, string path)
        {
            string name = type?.Namespace;
            if (name != null && (name.StartsWith("Game.App.DevRuntime", StringComparison.Ordinal) || name.StartsWith("Game.DevTools", StringComparison.Ordinal)))
                throw new BuildFailedException("Release build references dev type " + type.FullName + " in " + path);
        }
    }
}
