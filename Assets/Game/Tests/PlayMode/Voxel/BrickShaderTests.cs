using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>REN-008: real GPU pixels, shadows and procedural props. Run without -nographics.</summary>
    [Category("Rendering")]
    public sealed class BrickShaderTests
    {
        const int Layer = 31, Size = 256;
        readonly List<Object> owned = new List<Object>();
        Camera camera;
        Light light;
        Material brickMaterial, propMaterial;
        RenderTexture target;
        UniversalRenderPipelineAsset pipeline;
        RenderPipelineAsset oldPipeline;
        AmbientMode oldAmbientMode;
        Color oldAmbient;
        bool oldAsync, initialized;
        float oldReflection;
        Matrix4x4[] oldOrientations;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        [UnitySetUp] public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU rendering gate: run this fixture without -nographics (08-tools §3).");
            initialized = true; oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            oldOrientations = Shader.GetGlobalMatrixArray("_OrientMatrices");
            oldPipeline = QualitySettings.renderPipeline;
            pipeline = Own(Object.Instantiate((UniversalRenderPipelineAsset)(oldPipeline ? oldPipeline : GraphicsSettings.defaultRenderPipeline)));
            pipeline.shadowDistance = 100; pipeline.shadowCascadeCount = 4; pipeline.msaaSampleCount = 1; pipeline.renderScale = 1;
            QualitySettings.renderPipeline = pipeline;
            oldAmbientMode = RenderSettings.ambientMode; oldAmbient = RenderSettings.ambientLight; oldReflection = RenderSettings.reflectionIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.reflectionIntensity = 0;
            brickMaterial = Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/_Placeholder/Materials/BrickPlaceholder.mat")));
            propMaterial = Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/_Placeholder/Materials/PropPlaceholder.mat")));
            var palette = Own(new Texture2D(256, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point });
            for (int i = 0; i < 256; i++) palette.SetPixel(i, 0, i == 2 ? Color.red : i == 3 ? Color.green : i == 4 ? Color.blue : Color.white);
            palette.Apply();
            var masks = Own(new Texture2DArray(1, 1, 64, TextureFormat.RGBA32, false, true));
            for (int i = 0; i < 64; i++) masks.SetPixels(new[] { new Color(0, 0, 1, 0) }, i);
            masks.Apply();
            var propMask = Own(Flat(new Color(0, 0, 1, 0)));
            foreach (var material in new[] { brickMaterial, propMaterial })
            {
                material.enableInstancing = true; material.SetTexture("_PaintPalette", palette);
                material.SetTexture("_BrickMaskArray", masks); material.SetTexture("_DamageRamp", Texture2D.whiteTexture);
                material.SetFloat("_MacroStrength", 0); material.SetFloat("_ScorchStrength", 0); material.SetFloat("_AOStrength", 1);
                material.SetFloat("_EmissiveIntensity", 0); material.SetFloat("_EmberIntensity", 0); material.SetFloat("_RimStrength", 0); material.SetFloat("_SeamEmission", 0);
            }
            propMaterial.SetTexture("_PropMask", propMask);
            target = Own(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)); target.Create();
            camera = Own(new GameObject("Brick shader gate camera")).AddComponent<Camera>();
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.orthographicSize = 1.5f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.cullingMask = 1 << Layer; camera.transform.SetPositionAndRotation(new Vector3(1, 1, -5), Quaternion.identity);
            var extra = camera.GetUniversalAdditionalCameraData(); extra.renderPostProcessing = false; extra.antialiasing = AntialiasingMode.None;
            light = Own(new GameObject("Brick shader gate key")).AddComponent<Light>();
            light.type = LightType.Directional; light.color = Color.white; light.intensity = 1; light.shadows = LightShadows.Hard; light.cullingMask = 1 << Layer;
            light.transform.rotation = Quaternion.identity;
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient; RenderSettings.reflectionIntensity = oldReflection;
            QualitySettings.renderPipeline = oldPipeline; ShaderUtil.allowAsyncCompilation = oldAsync;
            Shader.SetGlobalMatrixArray("_OrientMatrices", oldOrientations != null && oldOrientations.Length > 0 ? oldOrientations : new Matrix4x4[24]);
            if (target) target.Release();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.Destroy(owned[i]);
            owned.Clear(); initialized = false; yield return null;
        }
        static Texture2D Flat(Color color)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false, true); t.SetPixel(0, 0, color); t.Apply(); return t;
        }
        Mesh Quad(int colour, int ao = 3, int damage = 0, bool floor = false, int side = 2)
        {
            using var vertices = new NativeArray<PackedBrickVertex>(4, Allocator.Temp);
            using var indices = new NativeArray<ushort>(new ushort[] { 0, 3, 2, 0, 2, 1 }, Allocator.Temp);
            var v = vertices;
            int n = floor ? 2 : 5;
            int3[] p = floor ? new[] { new int3(0, 0, 0), new int3(side, 0, 0), new int3(side, 0, side), new int3(0, 0, side) }
                : new[] { new int3(0, 0, 0), new int3(side, 0, 0), new int3(side, side, 0), new int3(0, side, 0) };
            for (int i = 0; i < 4; i++) v[i] = PackedBrickVertex.Pack(p[i].x * 2, p[i].y * 2, p[i].z * 2, n, 0, colour, ao, damage, false);
            var mesh = Own(new Mesh { name = "Packed shader test quad" });
            mesh.SetVertexBufferParams(4, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.UNorm8, 4), new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4));
            mesh.SetVertexBufferData(vertices, 0, 0, 4); mesh.SetIndexBufferParams(6, IndexFormat.UInt16); mesh.SetIndexBufferData(indices, 0, 0, 6);
            mesh.subMeshCount = 1; mesh.SetSubMesh(0, new SubMeshDescriptor(0, 6) { vertexCount = 4 }, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = floor ? new Bounds(new Vector3(side * .5f, 0, side * .5f), new Vector3(side, .01f, side)) : new Bounds(new Vector3(side * .5f, side * .5f, 0), new Vector3(side, side, .01f));
            return mesh;
        }
        MeshRenderer Show(Mesh mesh, Material material)
        {
            var go = Own(new GameObject("Shader gate surface", typeof(MeshFilter), typeof(MeshRenderer))); go.layer = Layer;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return renderer;
        }
        Color[] Pixels(string label = null)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            try
            {
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readback.Apply();
                if (label != null) { Directory.CreateDirectory("Logs/rendering"); File.WriteAllBytes("Logs/rendering/ren008-" + label + ".png", readback.EncodeToPNG()); }
                return readback.GetPixels();
            }
            finally { Object.DestroyImmediate(readback); RenderTexture.active = previous; }
        }
        static float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
        static void CheckBatcher(Shader shader)
        {
            int result = (int)typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { shader, 0 });
            string reason = (string)typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { shader, 0, result });
            TestContext.WriteLine(shader.name + " SRP Batcher code " + result + ": " + reason);
            Assert.AreEqual(0, result, reason);
        }
        internal static void CompileVariants(Shader shader, bool prop)
        {
            var compile = typeof(ShaderUtil).GetMethod("CompileShaderVariant", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            string[][] variants = prop ? new[] { Array.Empty<string>(), new[] { "DOTS_INSTANCING_ON" }, new[] { "PROCEDURAL_INSTANCING_ON" }, new[] { "PROCEDURAL_INSTANCING_ON", "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP" } }
                : new[] { Array.Empty<string>(), new[] { "DOTS_INSTANCING_ON" }, new[] { "DOTS_INSTANCING_ON", "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP" } };
            int count = 0;
            foreach (var platform in new[] { ShaderCompilerPlatform.Metal, ShaderCompilerPlatform.D3D })
            foreach (var variant in variants)
            for (int pass = 0; pass < 4; pass++)
            foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
            {
                object result = compile.Invoke(null, new object[] { shader, 0, pass, stage, Array.Empty<BuiltinShaderDefine>(), variant, platform,
                    platform == ShaderCompilerPlatform.Metal ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64, GraphicsTier.Tier2, false });
                var type = result.GetType(); bool success = (bool)type.GetProperty("Success").GetValue(result);
                foreach (ShaderMessage message in (Array)type.GetProperty("Messages").GetValue(result))
                    if (message.severity == ShaderCompilerMessageSeverity.Error) Assert.Fail(platform + " " + shader.name + " pass " + pass + " " + stage + ": " + message.message);
                Assert.IsTrue(success, platform + " " + shader.name + " " + pass + " " + stage);
                Assert.Greater(((byte[])type.GetProperty("ShaderData").GetValue(result)).Length, 0, "Compiled bytes must exist"); count++;
            }
            TestContext.WriteLine(shader.name + ": " + count + " Metal/D3D11 pass/stage/keyword variants compiled");
        }

        [UnityTest] public IEnumerator PaletteColour_PerIndex()
        {
            CompileVariants(brickMaterial.shader, false); CompileVariants(propMaterial.shader, true);
            var renderer = Show(Quad(2), brickMaterial); var filter = renderer.GetComponent<MeshFilter>();
            foreach (int colour in new[] { 2, 3, 4 })
            {
                filter.sharedMesh = Quad(colour); yield return null; yield return null;
                Color c = Pixels("paint-" + colour)[Size / 2 * Size + Size / 2];
                float main = colour == 2 ? c.r : colour == 3 ? c.g : c.b;
                float other = colour == 2 ? Mathf.Max(c.g, c.b) : colour == 3 ? Mathf.Max(c.r, c.b) : Mathf.Max(c.r, c.g);
                Assert.Greater(main, .15f, "Paint " + colour + " " + c); Assert.Greater(main, other * 3, "Paint index must select its palette channel");
            }
            filter.sharedMesh = Quad(16); yield return null; yield return null; float bright = Luma(Pixels()[Size / 2 * Size + Size / 2]);
            filter.sharedMesh = Quad(16, 0); yield return null; yield return null; float dark = Luma(Pixels("ao-occluded")[Size / 2 * Size + Size / 2]);
            Assert.Less(dark, bright * .3f, "The inner-corner AO value must visibly darken the surface");
            CheckBatcher(brickMaterial.shader);
        }
        [UnityTest] public IEnumerator CracksVisible_AtDamage3()
        {
            var renderer = Show(Quad(16), brickMaterial); yield return null; yield return null; var intact = Pixels("intact");
            renderer.GetComponent<MeshFilter>().sharedMesh = Quad(16, 3, 3); yield return null; yield return null; var damaged = Pixels("damage3");
            int surface = 0, darker = 0;
            for (int i = 0; i < intact.Length; i++) if (Luma(intact[i]) > .08f) { surface++; if (Luma(intact[i]) - Luma(damaged[i]) > .025f) darker++; }
            Assert.Greater(surface, 1000); Assert.Greater(darker, surface * .05f, "Damage-3 cracks must change visible pixels");
            TestContext.WriteLine("Cracks changed " + darker + " / " + surface + " surface pixels");
        }
        [UnityTest] public IEnumerator ShadowCast()
        {
            var floor = Show(Quad(16, floor: true, side: 8), brickMaterial); floor.transform.position = new Vector3(-4, 0, -4);
            var caster = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); caster.layer = Layer; caster.transform.position = new Vector3(0, 1, 0);
            Object.DestroyImmediate(caster.GetComponent<Collider>());
            var casting = caster.GetComponent<MeshRenderer>(); casting.sharedMaterial = propMaterial; casting.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            camera.orthographicSize = 4; camera.transform.position = new Vector3(4, 6, -6); camera.transform.LookAt(Vector3.zero);
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            // Exercise each shader's real ShadowCaster pass against the packed-brick receiver.
            var brickCaster = Show(Quad(16), brickMaterial);
            brickCaster.transform.position = new Vector3(-1, .1f, 0);
            brickCaster.shadowCastingMode = ShadowCastingMode.ShadowsOnly; brickCaster.enabled = false;
            foreach (var candidate in new[] { casting, brickCaster })
            {
                casting.enabled = false; brickCaster.enabled = false; candidate.enabled = true;
                yield return null; yield return null; var shadow = Pixels(candidate == casting ? "prop-shadow" : "brick-shadow");
                candidate.enabled = false;
                yield return null; yield return null; var clear = Pixels("no-shadow");
                int darker = 0;
                for (int i = 0; i < clear.Length; i++) if (Luma(clear[i]) - Luma(shadow[i]) > .025f) darker++;
                Assert.Greater(darker, 50, candidate.sharedMaterial.shader.name + " must cast a visible shadow");
                TestContext.WriteLine(candidate.sharedMaterial.shader.name + " shadow darkened " + darker + " pixels");
            }
        }
        [UnityTest] public IEnumerator PropInstances_Render()
        {
            var mesh = Own(new Mesh { name = "Procedural prop gate quad" });
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back }; mesh.colors = new[] { new Color(1, 0, 0, 0), new Color(1, 0, 0, 0), new Color(1, 0, 0, 0), new Color(1, 0, 0, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }; mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            using var instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 3, 16);
            using var matrices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, 64);
            instances.SetData(new[] { new PropInstanceGpu { LocalCellCenter = new float3(-2, 0, 0), Packed = 2u << 5 }, new PropInstanceGpu { Packed = 3u << 5 }, new PropInstanceGpu { LocalCellCenter = new float3(1, 0, 0), Packed = (4u << 5) | (1u << 13) } });
            matrices.SetData(new[] { Matrix4x4.identity, Matrix4x4.Translate(Vector3.right) });
            var orientations = new Matrix4x4[24]; Array.Fill(orientations, Matrix4x4.identity); Shader.SetGlobalMatrixArray("_OrientMatrices", orientations);
            var block = new MaterialPropertyBlock(); block.SetBuffer("_PropInstances", instances); block.SetBuffer("_GridMatrices", matrices);
            camera.transform.position = new Vector3(0, 0, -5); camera.orthographicSize = 3;
            var rp = new RenderParams(propMaterial) { camera = camera, layer = Layer, renderingLayerMask = 1, matProps = block, worldBounds = new Bounds(Vector3.zero, Vector3.one * 10), shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            // Test coroutines may resume after rendering; submit before this camera is culled.
            Action<ScriptableRenderContext, Camera> draw = (context, view) => { if (view == camera) Graphics.RenderMeshPrimitives(rp, mesh, 0, 3); };
            RenderPipelineManager.beginCameraRendering += draw;
            try { for (int frame = 0; frame < 3; frame++) yield return null; }
            finally { RenderPipelineManager.beginCameraRendering -= draw; }
            var pixels = Pixels("props");
            for (int i = 0; i < 3; i++)
            {
                Vector3 screen = camera.WorldToViewportPoint(new Vector3((i - 1) * 2, 0, 0)); Color c = pixels[Mathf.FloorToInt(screen.y * Size) * Size + Mathf.FloorToInt(screen.x * Size)];
                float value = i == 0 ? c.r : i == 1 ? c.g : c.b;
                Assert.Greater(value, .15f, "Prop instance " + i + " " + c);
                float other = i == 0 ? Mathf.Max(c.g, c.b) : i == 1 ? Mathf.Max(c.r, c.b) : Mathf.Max(c.r, c.g);
                Assert.Greater(value, other * 3, "Prop palette channel " + i + " " + c);
            }
            CheckBatcher(propMaterial.shader);
        }
    }
}
