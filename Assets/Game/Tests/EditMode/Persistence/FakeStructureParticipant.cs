using System;
using System.Collections.Generic;
using Game.Core.Save;
using Game.Data.Services;
using Game.Voxel.Data;
using Unity.Collections;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-008: FLET-like participant with two deferred VOXS payloads.</summary>
    internal sealed class FakeStructureParticipant : ISaveParticipant, IDisposable
    {
        public string Key => "TSTA";
        public int Version => 1;
        readonly ContentCatalog catalog;
        readonly VoxsBlockNames names;
        public VoxsSource[] Sources;
        public readonly List<VoxsSnapshotBlock> Captured = new List<VoxsSnapshotBlock>();
        public readonly List<VoxsDecoded> Restored = new List<VoxsDecoded>();

        public FakeStructureParticipant(ContentCatalog catalog, params VoxsSource[] sources)
        { this.catalog = catalog; names = VoxsBlockNames.FromCatalog(catalog); Sources = sources; }
        public void Capture(SaveWriter writer)
        {
            writer.WriteVarUInt32((uint)Sources.Length);
            foreach (var source in Sources)
            {
                var block = VoxsSnapshotBlock.Capture(source, names);
                try { writer.WriteDeferred(block); }
                catch { block.Dispose(); throw; }
                Captured.Add(block);
            }
        }
        public void Restore(SaveReader reader, int fromVersion)
        {
            Dispose();
            if (fromVersion == 0) return;
            if (fromVersion != Version) throw new SaveFormatException("unsupported fake structure version", reader.Position);
            uint count = reader.ReadVarUInt32();
            if (count > 2) throw new SaveFormatException("too many test structures", reader.Position);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    byte[] bytes = reader.ReadByteArray();
                    if (!VoxsCodec.TryDecode(bytes, catalog, Allocator.Persistent, out var d, out string error))
                        throw new SaveFormatException(error, reader.Position);
                    Restored.Add(d);
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            foreach (var d in Restored) d.Dispose();
            Restored.Clear();
        }
    }
}
