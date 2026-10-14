using System;
using Game.ShipEditor.Data;
using Unity.Collections;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 ordered tool input. Caller owns storage; session copies accepted operations.</summary>
    public sealed class EditBatch : IDisposable
    {
        NativeList<DiffOp> ops;
        NativeList<byte> damage;
        NativeList<BatchGroup> groups;
        bool disposed;
        // Only rigid, damage-preserving selection transforms may reuse holes in a damaged footprint.
        internal bool PreserveDamagedLayout;
        public NativeList<DiffOp> Ops { get { CheckAlive(); return ops; } }
        public NativeList<byte> Damage { get { CheckAlive(); return damage; } }
        public NativeList<BatchGroup> Groups { get { CheckAlive(); return groups; } }
        public EditBatch()
        {
            try
            {
                ops = new NativeList<DiffOp>(Allocator.Persistent);
                damage = new NativeList<byte>(Allocator.Persistent);
                groups = new NativeList<BatchGroup>(Allocator.Persistent);
            }
            catch { Dispose(); throw; }
        }
        public void Clear() { CheckAlive(); ops.Clear(); damage.Clear(); groups.Clear(); PreserveDamagedLayout = false; }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(EditBatch)); }
        public void Dispose()
        {
            if (disposed) return;
            if (ops.IsCreated) ops.Dispose(); if (damage.IsCreated) damage.Dispose(); if (groups.IsCreated) groups.Dispose();
            ops = default; damage = default; groups = default; disposed = true;
        }
    }
}
