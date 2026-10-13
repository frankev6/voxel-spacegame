using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Save;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;

namespace Game.Galaxy.Services
{
    /// <summary>GLXY v1: ordered galaxy deltas and deferred structure snapshots (SPEC-30 §5.11).</summary>
    public sealed class GalaxySaveParticipant : ISaveParticipant
    {
        readonly GalaxyState state;
        readonly ContentCatalog catalog;
        readonly VoxsBlockNames names;
        public GalaxySaveParticipant(GalaxyState state, ContentCatalog catalog)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            names = VoxsBlockNames.FromCatalog(catalog);
        }
        public string Key => "GLXY";
        public int Version => 1;
        public void Capture(SaveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteUInt64(state.Seed); writer.WriteInt32(state.GeneratorVersion);
            writer.WriteUInt32(state.RouteDestination.Value);
            WriteSystems(writer, state.Known); WriteSystems(writer, state.Visited);
            uint count = 0; foreach (var delta in state.DeltasSorted()) count++;
            writer.WriteVarUInt32(count);
            foreach (var delta in state.DeltasSorted())
            {
                writer.WriteUInt32(delta.StarSystem.Value); writer.WriteVarUInt32(delta.VisitCount);
                WriteKeys(writer, delta.Destroyed); WriteKeys(writer, delta.Interacted);
                writer.WriteVarUInt32((uint)delta.StorySpawns.Count);
                foreach (var story in delta.StorySpawns)
                {
                    writer.WriteContentId(story.Template); writer.WriteUInt64(story.AnchorBodyKey);
                    writer.WriteDouble3(story.OffsetM); writer.WriteSingle(story.YawRad); writer.WriteBool(story.Removed);
                }
                writer.WriteVarUInt32((uint)delta.Replacements.Count);
                foreach (var replacement in delta.Replacements)
                { writer.WriteUInt64(replacement.BodyKey); writer.WriteContentId(replacement.Template); }
                writer.WriteVarUInt32((uint)delta.StructureDiffs.Count);
                foreach (var diff in delta.StructureDiffs)
                {
                    writer.WriteUInt64(diff.BodyKey); writer.WriteUInt32(diff.Revision);
                    using var source = VoxelStructureCodec.ToSource(diff.Structure, Allocator.TempJob);
                    var snapshot = VoxsSnapshotBlock.Capture(source.Source, names);
                    try { writer.WriteDeferred(snapshot); }
                    catch { snapshot.Dispose(); throw; }
                }
            }
        }
        static void WriteSystems(SaveWriter writer, IReadOnlyList<uint> values)
        { writer.WriteVarUInt32((uint)values.Count); foreach (uint value in values) writer.WriteUInt32(value); }
        static void WriteKeys(SaveWriter writer, IReadOnlyList<ulong> values)
        { writer.WriteVarUInt32((uint)values.Count); foreach (ulong value in values) writer.WriteUInt64(value); }
        public void Restore(SaveReader reader, int fromVersion)
        {
            if (fromVersion == 0) { state.Clear(); return; }
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (fromVersion != Version) throw new SaveFormatException("Unsupported GLXY version " + fromVersion, reader.Position);
            state.Clear();
            try
            {
                state.Seed = reader.ReadUInt64(); state.GeneratorVersion = reader.ReadInt32();
                state.RouteDestination = new StarSystemId(reader.ReadUInt32());
                ReadSystems(reader, false); ReadSystems(reader, true);
                uint count = Count(reader, 10); uint previousSystem = 0;
                for (uint i = 0; i < count; i++)
                {
                    uint id = reader.ReadUInt32();
                    if (id <= previousSystem) throw new SaveFormatException("GLXY systems must be nonzero and sorted", reader.Position);
                    previousSystem = id;
                    var delta = state.GetOrCreateDelta(new StarSystemId(id));
                    delta.RestoreVisitCount(reader.ReadVarUInt32());
                    ReadKeys(reader, delta, false); ReadKeys(reader, delta, true);
                    uint stories = Count(reader, 38);
                    if (stories > ushort.MaxValue) throw new SaveFormatException("GLXY story slot capacity exceeded", reader.Position);
                    for (uint j = 0; j < stories; j++)
                        delta.AddStorySpawn(new StorySpawnRecord { Template = reader.ReadContentId(), AnchorBodyKey = reader.ReadUInt64(),
                            OffsetM = reader.ReadDouble3(), YawRad = reader.ReadSingle(), Removed = reader.ReadBool() });
                    uint replacements = Count(reader, 9); ulong previous = 0;
                    for (uint j = 0; j < replacements; j++)
                    {
                        ulong key = ReadOrderedKey(reader, j, ref previous);
                        delta.SetReplacement(key, reader.ReadContentId());
                    }
                    uint structures = Count(reader, 13); previous = 0;
                    for (uint j = 0; j < structures; j++)
                    {
                        ulong key = ReadOrderedKey(reader, j, ref previous); uint revision = reader.ReadUInt32();
                        byte[] bytes = reader.ReadByteArray();
                        if (!VoxelStructureCodec.TryDecode(bytes, catalog, out var structure, out string error))
                        { GameLog.Warn("Save", "GLXY structure " + id + "/" + key + " dropped: " + error); continue; }
                        try { delta.SetStructure(key, revision, structure); }
                        catch { structure.Dispose(); throw; }
                    }
                }
                if (!reader.IsAtEnd) throw new SaveFormatException("Trailing GLXY data", reader.Position);
            }
            catch { state.Clear(); throw; }
        }
        void ReadSystems(SaveReader reader, bool visited)
        {
            uint count = Count(reader, 4), previous = 0;
            for (uint i = 0; i < count; i++)
            {
                uint value = reader.ReadUInt32();
                if (value <= previous) throw new SaveFormatException("GLXY visibility ids must be nonzero and sorted", reader.Position);
                previous = value;
                if (visited) state.AddVisited(new StarSystemId(value)); else state.AddKnown(new StarSystemId(value));
            }
        }
        static void ReadKeys(SaveReader reader, StarSystemDelta delta, bool interacted)
        {
            uint count = Count(reader, 8); ulong previous = 0;
            for (uint i = 0; i < count; i++)
            {
                ulong key = ReadOrderedKey(reader, i, ref previous);
                if (interacted) delta.MarkInteracted(key); else delta.MarkDestroyed(key);
            }
        }
        static ulong ReadOrderedKey(SaveReader reader, uint index, ref ulong previous)
        {
            ulong value = reader.ReadUInt64();
            if (index != 0 && value <= previous) throw new SaveFormatException("GLXY body keys must be unique and sorted", reader.Position);
            previous = value; return value;
        }
        static uint Count(SaveReader reader, uint minimumBytes)
        {
            uint count = reader.ReadVarUInt32();
            if (count > reader.Remaining / minimumBytes) throw new SaveFormatException("GLXY count exceeds remaining payload", reader.Position);
            return count;
        }
    }
}
