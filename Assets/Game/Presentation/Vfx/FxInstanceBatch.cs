using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Vfx
{
    internal interface IFxInstanceBatch : IDisposable { long BufferBytes { get; } }
    /// <summary>Session-owned instanced draw storage. Quality changes never resize the High-budget allocation.</summary>
    internal sealed class FxInstanceBatch<T> : IFxInstanceBatch where T : unmanaged
    {
        internal NativeArray<T> Data;
        internal GraphicsBuffer Buffer { get; private set; }
        internal int Count;
        readonly Mesh mesh;
        readonly MaterialPropertyBlock properties;
        RenderParams renderParams;
        static readonly int InstancesId = Shader.PropertyToID("_Instances");
        public long BufferBytes => Buffer == null ? 0 : (long)Buffer.count * Buffer.stride;
        internal FxInstanceBatch(Mesh mesh, Material material, int capacity)
        {
            this.mesh = mesh;
            try
            {
                Data = new NativeArray<T>(Math.Max(1, capacity), Allocator.Persistent);
                Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Data.Length, UnsafeUtility.SizeOf<T>());
                properties = new MaterialPropertyBlock(); properties.SetBuffer(InstancesId, Buffer);
                renderParams = new RenderParams(material) { matProps = properties, worldBounds = new Bounds(Vector3.zero, Vector3.one * 8000),
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, layer = 0 };
            }
            catch { Dispose(); throw; }
        }
        internal void SetFloat(int id, float value) => properties.SetFloat(id, value);
        internal void Begin() => Count = 0;
        internal void Draw()
        {
            if (Buffer == null || Count <= 0) return;
            if (Count > Data.Length) throw new InvalidOperationException("FX instance count exceeds its High-budget capacity.");
            Buffer.SetData(Data, 0, 0, Count); Graphics.RenderMeshPrimitives(renderParams, mesh, 0, Count);
        }
        public void Dispose()
        {
            if (Data.IsCreated) Data.Dispose();
            Buffer?.Dispose(); Buffer = null; Count = 0;
        }
    }
}
