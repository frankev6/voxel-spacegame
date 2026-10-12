using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>CAM-004 regression: only GameRoot owns a camera.</summary>
    public sealed class ContentScenesTests
    {
        [Test] public void ContentScenes_HaveNoCamera()
        {
            foreach (string name in new[] { "MainMenu", "World", "BlueprintLab" })
            {
                string path = "Assets/Game/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        Assert.IsEmpty(root.GetComponentsInChildren<Camera>(true), name + ": " + root.name);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
