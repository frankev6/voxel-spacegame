using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.ShipEditor.Rendering
{
    public enum OverlayKind { Ghost, GhostInvalid, Footprint, Clearance, MirrorGhost, Selection, Disconnected, Damaged, MirrorPlane, BoxOutline, Grid }
    [StructLayout(LayoutKind.Sequential)]
    internal struct OverlayInstance
    {
        internal float3 Min, Size;
        internal uint ColorRGBA8, Flags;
        internal OverlayInstance(float3 min, float3 size, Color color, uint flags = 0)
        {
            Min = min; Size = size; Color32 c = color;
            ColorRGBA8 = (uint)c.r | (uint)c.g << 8 | (uint)c.b << 16 | (uint)c.a << 24; Flags = flags;
        }
    }
    /// <summary>Owns one bounded category; mutation is explicit and GPU upload follows its version.</summary>
    internal sealed class OverlayCategory : IDisposable
    {
        internal NativeList<OverlayInstance> Instances = new(Allocator.Persistent);
        internal readonly MaterialPropertyBlock Properties = new();
        internal GraphicsBuffer Buffer;
        internal int Version, UploadedVersion = -1, UploadCount;
        internal void Clear() { Instances.Clear(); Version++; }
        internal void Add(OverlayInstance instance, int cap) { if (Instances.Length < cap) Instances.Add(instance); }
        internal void Upload(int cap)
        {
            if (UploadedVersion == Version) return;
            if (Instances.Length > 0)
            {
                if (Buffer == null || Buffer.count < Instances.Length)
                {
                    int capacity = Buffer == null ? 1 : Buffer.count;
                    while (capacity < Instances.Length) capacity = Math.Min(cap, capacity * 2);
                    Buffer?.Dispose(); Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 32);
                    Properties.SetBuffer("_Instances", Buffer);
                }
                Buffer.SetData(Instances.AsArray()); UploadCount++;
            }
            UploadedVersion = Version;
        }
        public void Dispose() { Buffer?.Dispose(); Buffer = null; if (Instances.IsCreated) Instances.Dispose(); }
    }
}
