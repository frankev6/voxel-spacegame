using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.ShipEditor.Rendering
{
    /// <summary>SPEC-51 §5.13: per-mesh instances, updated by exact identity with constant-time swap-back removal.</summary>
    internal sealed class InstancedPropSet : IDisposable
    {
        NativeHashMap<BlockRecord, int> slots = new(64, Allocator.Persistent);
        NativeList<BlockRecord> records = new(Allocator.Persistent);
        NativeList<Matrix4x4> matrices = new(Allocator.Persistent);
        internal Mesh Mesh { get; }
        internal int Count => records.Length;
        internal int MutationCount { get; private set; }
        internal NativeArray<BlockRecord> Records => records.AsArray();
        internal NativeArray<Matrix4x4> Matrices => matrices.AsArray();
        internal InstancedPropSet(Mesh mesh) => Mesh = mesh ? mesh : throw new ArgumentNullException(nameof(mesh));
        internal void Add(BlockRecord record, EditorContext context, float cellSize)
        {
            record.DamageOffset = -1;
            if (slots.ContainsKey(record)) throw new InvalidOperationException("EDT: duplicate prop identity.");
            slots.Add(record, records.Length); records.Add(record);
            // Authored prop meshes use cell units, as in the live PropGather/PropInstanced path.
            var rotation = context.Orientations.Value.Rotations[record.Orient].value;
            matrices.Add(Matrix4x4.TRS((Vector3)(((float3)record.Origin + .5f) * cellSize),
                new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w), Vector3.one * cellSize));
            MutationCount++;
        }
        internal bool Remove(BlockRecord record)
        {
            if (!slots.TryGetValue(record, out int index)) return false;
            int last = records.Length - 1; slots.Remove(record);
            if (index != last)
            { var moved = records[last]; records[index] = moved; matrices[index] = matrices[last]; slots[moved] = index; }
            records.RemoveAt(last); matrices.RemoveAt(last); MutationCount++; return true;
        }
        internal void Clear() { slots.Clear(); records.Clear(); matrices.Clear(); MutationCount++; }
        public void Dispose()
        {
            if (slots.IsCreated) slots.Dispose(); if (records.IsCreated) records.Dispose(); if (matrices.IsCreated) matrices.Dispose();
        }
    }
}
