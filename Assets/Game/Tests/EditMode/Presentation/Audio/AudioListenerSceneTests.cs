using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode.Presentation.Audio
{
    /// <summary>SPEC-64 AUD-004: scene content must never supply the service-owned listener.</summary>
    public sealed class AudioListenerSceneTests
    {
        [Test] public void Scenes_ContainNoAudioListener()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Boot", "MainMenu", "World", "BlueprintLab" },
                scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray());
            foreach (var entry in scenes)
            {
                var scene = SceneManager.GetSceneByPath(entry.path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        Assert.IsEmpty(root.GetComponentsInChildren<AudioListener>(true), scene.name + ": " + root.name + " contains an AudioListener");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
