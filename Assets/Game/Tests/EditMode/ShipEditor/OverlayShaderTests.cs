using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class OverlayShaderTests
    {
        [Test] public void Shader_Supported_NoGeometryStage()
        {
            const string path="Assets/Game/ShipEditor/Rendering/EditorOverlay.shader";
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);Assert.IsNotNull(shader);Assert.IsTrue(shader.isSupported);
            string source=File.ReadAllText(path);
            foreach(string stage in new[]{"geometry","hull","domain"})StringAssert.DoesNotMatch(@"#pragma\s+"+stage+@"\b",source);
            Assert.AreEqual(32,Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<Game.ShipEditor.Rendering.OverlayInstance>());
            var config=AssetDatabase.LoadAssetAtPath<Game.ShipEditor.Data.ShipEditorConfig>("Assets/Game/ShipEditor/Data/ShipEditorConfig.asset");
            Assert.IsNotNull(config.OverlayMaterial);Assert.AreEqual(shader,config.OverlayMaterial.shader);
        }
    }
}
