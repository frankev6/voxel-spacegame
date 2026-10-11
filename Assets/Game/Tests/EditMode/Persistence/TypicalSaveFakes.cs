using System;
using System.Collections.Generic;
using Game.Core.Save;
using Game.Data.Services;
using Game.Persistence.Services;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>SPEC-05 §8: deterministic capture and typical-load workloads, with real deferred VOXS encoding.</summary>
    internal sealed class TypicalSaveFakes : IDisposable
    {
        internal sealed class Participant : ISaveParticipant, IDisposable
        {
            public string Key { get; }
            public int Version => 1;
            public readonly byte[] Payload;
            public readonly List<VoxsTestStructures> Sources = new List<VoxsTestStructures>();
            public readonly List<VoxsDecoded> Restored = new List<VoxsDecoded>();
            public byte[] RestoredPayload;
            readonly ContentCatalog catalog;
            readonly VoxsBlockNames names;
            public Participant(string key, byte[] payload, ContentCatalog catalog)
            { Key = key; Payload = payload; this.catalog = catalog; names = VoxsBlockNames.FromCatalog(catalog); }
            public void Capture(SaveWriter w)
            {
                w.WriteByteArray(Payload); w.WriteVarUInt32((uint)Sources.Count);
                foreach (var source in Sources) w.WriteDeferred(VoxsSnapshotBlock.Capture(source.Source, names));
            }
            public void Restore(SaveReader r, int version)
            {
                foreach (var d in Restored) d.Dispose(); Restored.Clear();
                if (version == 0) { RestoredPayload = Array.Empty<byte>(); return; }
                RestoredPayload = r.ReadByteArray(); int count = (int)r.ReadVarUInt32();
                for (int i = 0; i < count; i++)
                {
                    if (!VoxsCodec.TryDecode(r.ReadByteArray(), catalog, Allocator.Persistent, out var d, out string error))
                        throw new SaveFormatException(error, r.Position);
                    Restored.Add(d);
                }
            }
            public void Dispose()
            {
                foreach (var source in Sources) source.Dispose(); Sources.Clear();
                foreach (var d in Restored) d.Dispose(); Restored.Clear();
            }
        }
        public readonly List<Participant> Participants = new List<Participant>();
        public TypicalSaveFakes(bool captureWorkload)
        {
            var catalog = VoxsCodecTests.Catalog();
            foreach (string key in new[] { "SESS", "FACT", "QUST", "INVT", "FLET", "GLXY", "PLOC" })
            {
                using var w = new SaveWriter();
                switch (key)
                {
                    case "SESS": w.WriteBytes(new byte[1024]); break;
                    case "FACT": for (int i = 0; i < 2000; i++) { w.WriteString("fact_" + i); w.WriteInt32(i % 19); } break;
                    case "QUST": for (int i = 0; i < 30; i++) { w.WriteString("quest_" + i); w.WriteInt32(i % 4); } break;
                    case "INVT": for (int i = 0; i < 200; i++) { w.WriteString("item_" + i); w.WriteInt32(i + 1); } break;
                    case "GLXY": for (int i = 0; i < 50; i++) { w.WriteUInt32((uint)i); w.WriteInt32(i % 3); } break;
                    case "PLOC": w.WriteDouble(1234567.5); w.WriteDouble(-21234); w.WriteDouble(99); break;
                }
                var participant = new Participant(key, w.ToArray(), catalog); Participants.Add(participant);
                if (key == "FLET")
                {
                    for (int i = 0; i < 5; i++) participant.Sources.Add(Solid(catalog, 20000));
                    if (captureWorkload) participant.Sources.Add(VoxsTestStructures.ReferenceShip(catalog));
                }
                if (key == "GLXY" && !captureWorkload)
                    for (int i = 0; i < 10; i++) { var s = Solid(catalog, 50 * 4096); s.Source.Partial = true; participant.Sources.Add(s); }
            }
        }
        static VoxsTestStructures Solid(ContentCatalog c, int count)
        {
            var s = new VoxsTestStructures((count + 4095) / 4096);
            s.Source.Palette[1] = new PaletteEntry { Block = VoxsTestStructures.Block(c, "block_hull") };
            for (int i = 0; i < count; i++) s.Source.Cells[i] = Cell.Palette(1);
            return s;
        }
        public SaveService CreateService(string root)
        {
            var registrations = new List<ChunkRegistration>();
            for (int i = 0; i < Participants.Count; i++) registrations.Add(new ChunkRegistration(Participants[i].Key, 1000 + i * 10, "test", "typical"));
            var service = SaveFixtureHarness.CreateService(root, null, new SaveChunkRegistry(registrations));
            foreach (var p in Participants) service.Register(p);
            return service;
        }
        public void Dispose() { foreach (var p in Participants) p.Dispose(); Participants.Clear(); }
    }
}
