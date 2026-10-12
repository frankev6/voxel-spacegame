using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §6.1 contract. TODO(VFX-002): implement resource ownership and binding before boot integration.</summary>
    public sealed class FxRig : MonoBehaviour
    {
        public static FxRig Create(Transform parent, VfxConfigDefinition config, IReadOnlyList<FxDefinition> definitions,
            Color32[] cellPalette, VfxQuality quality) => throw new NotImplementedException();
        public void BindTo(World world) => throw new NotImplementedException();
        public void Unbind(World world) => throw new NotImplementedException();
        public void SetQuality(VfxQuality q) => throw new NotImplementedException();
        public VfxQuality Quality => throw new NotImplementedException();
        public FxStats Stats => throw new NotImplementedException();
        public void Submit(int slot, NativeArray<FxGpuEvent> events, int start, int count) => throw new NotImplementedException();
        public void ShiftAnchors(float3 delta) => throw new NotImplementedException();
        public void ClearAll(bool exceptWarp) => throw new NotImplementedException();
        public void Dispose() => throw new NotImplementedException();
    }
}
