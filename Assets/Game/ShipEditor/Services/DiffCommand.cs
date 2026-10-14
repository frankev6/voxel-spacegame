using System;
using Game.ShipEditor.Data;
using Unity.Collections;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 compact diff storage. Push transfers lifetime and mutation ownership to EditHistory.</summary>
    public sealed class DiffCommand : IDisposable
    {
        NativeList<DiffOp> ops;
        NativeList<byte> damage;
        internal bool Owned;
        public bool IsDisposed { get; private set; }
        public NativeList<DiffOp> Ops { get { CheckAlive(); return ops; } }
        public NativeList<byte> Damage { get { CheckAlive(); return damage; } }
        public int StrokeId { get; internal set; }
        public ToolId Tool { get; }
        public string Label { get; }
        public long ByteSize { get { CheckAlive(); return (long)ops.Length * 24 + damage.Length; } }
        public DiffCommand(ToolId tool, string label, int strokeId = 0)
        {
            Tool = tool; Label = label ?? ""; StrokeId = strokeId;
            ops = new NativeList<DiffOp>(Allocator.Persistent);
            try { damage = new NativeList<byte>(Allocator.Persistent); }
            catch { ops.Dispose(); throw; }
        }
        internal void Append(DiffCommand other)
        {
            CheckAlive(); other.CheckAlive();
            int offset = damage.Length;
            // Reserve both buffers before changing their logical content.
            ops.Capacity = Math.Max(ops.Capacity, checked(ops.Length + other.ops.Length));
            damage.Capacity = Math.Max(damage.Capacity, checked(offset + other.damage.Length));
            for (int i = 0; i < other.ops.Length; i++)
            {
                var op = other.ops[i];
                if (op.Record.DamageOffset >= 0) op.Record.DamageOffset = checked(op.Record.DamageOffset + offset);
                ops.Add(op);
            }
            damage.AddRange(other.damage.AsArray());
        }
        internal void CheckAlive() { if (IsDisposed) throw new ObjectDisposedException(nameof(DiffCommand)); }
        public void Dispose()
        {
            if (IsDisposed) return;
            if (ops.IsCreated) ops.Dispose(); if (damage.IsCreated) damage.Dispose();
            ops = default; damage = default; IsDisposed = true;
        }
    }
}
