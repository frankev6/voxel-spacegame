using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Validation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Voxel.Rendering
{
    /// <summary>Boot-owned render inputs and their explicit lifetime (SPEC-11 §4.3).</summary>
    public static class VoxelRenderConfigFactory
    {
        public static VoxelRenderConfig Create(VoxelRenderConfigDefinition def, IReadOnlyList<Mesh> propMeshes)
        {
            if (!def) throw new ArgumentNullException(nameof(def));
            if (propMeshes == null) throw new ArgumentNullException(nameof(propMeshes));
            var report = new ValidationReport("VoxelRenderConfig");
            def.Validate(report.For(def.name), null);
            if (def.Palette) def.Palette.Validate(report.For(def.Palette.name), null);
            if (report.HasErrors) throw new ArgumentException(report.ToText(), nameof(def));
            var config = new VoxelRenderConfig
            {
                BrickMaterial = def.BrickMaterial, PropMaterial = def.PropMaterial, BrickPoolCopy = def.BrickPoolCopy,
                MeshBatchMin = def.MeshBatchMin, MeshBatchMax = def.MeshBatchMax, MeshApplyBudgetMs = def.MeshApplyBudgetMs,
                Lod1Distance = def.Lod1Distance, Lod2Distance = def.Lod2Distance, LodHysteresis = def.LodHysteresis,
                DormantDistance = def.DormantDistance, ReleaseCpuMeshCopies = (byte)(def.ReleaseCpuMeshCopies ? 1 : 0)
            };
            try
            {
                config.Palette = new Texture2D(256, 1, TextureFormat.RGBA32, false)
                { name = "Voxel Paint Palette", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = def.Palette.Colors[i];
                config.Palette.Value.SetPixels32(pixels); config.Palette.Value.Apply(false, false);
                config.PropMeshes = new NativeArray<UnityObjectRef<Mesh>>(propMeshes.Count, Allocator.Persistent);
                for (int i = 0; i < propMeshes.Count; i++) config.PropMeshes[i] = propMeshes[i];
                return config;
            }
            catch { Destroy(ref config); throw; }
        }
        public static void Destroy(ref VoxelRenderConfig config)
        {
            if (config.PropMeshes.IsCreated) config.PropMeshes.Dispose();
            var palette = config.Palette.Value;
            if (palette) { if (Application.isPlaying) UnityEngine.Object.Destroy(palette); else UnityEngine.Object.DestroyImmediate(palette); }
            config = default;
        }
    }
}
