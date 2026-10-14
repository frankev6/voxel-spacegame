#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.App.DevRuntime.Overlays
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DevLineVertex { public float3 Position; public Color32 Color; }

    /// <summary>Bounded, persistent job output. Whole boxes are omitted once the vertex limit is reached.</summary>
    public struct DevLineBatch : IDisposable
    {
        public const int MaxVertices = 262144;
        public NativeList<DevLineVertex> Vertices;
        public NativeReference<int> Overflow;
        public DevLineBatch(Allocator allocator)
        { Vertices = new NativeList<DevLineVertex>(MaxVertices, allocator); Overflow = new NativeReference<int>(allocator); }
        public bool IsCreated => Vertices.IsCreated;
        public bool Truncated => Overflow.IsCreated && Overflow.Value != 0;
        public void Clear() { Vertices.Clear(); Overflow.Value = 0; }
        public void Line(float3 a, float3 b, Color32 color)
        {
            if (Vertices.Length + 2 > MaxVertices) { Overflow.Value = 1; return; }
            Vertices.AddNoResize(new DevLineVertex { Position = a, Color = color });
            Vertices.AddNoResize(new DevLineVertex { Position = b, Color = color });
        }
        public void Box(float3 min, float3 max, float4x4 transform, Color32 color)
        {
            if (Vertices.Length + 24 > MaxVertices) { Overflow.Value = 1; return; }
            for (int corner = 0; corner < 8; corner++)
            {
                var a = new float3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                for (int axis = 0; axis < 3; axis++)
                {
                    if ((corner & (1 << axis)) != 0) continue;
                    var b = a; b[axis] = max[axis];
                    Vertices.AddNoResize(new DevLineVertex { Position = math.transform(transform, a), Color = color });
                    Vertices.AddNoResize(new DevLineVertex { Position = math.transform(transform, b), Color = color });
                }
            }
        }
        public void Dispose()
        { if (Vertices.IsCreated) Vertices.Dispose(); if (Overflow.IsCreated) Overflow.Dispose(); }
    }
    public struct DevOverlayRoot { public float4x4 Transform; public float CellSize; }
}
#endif
