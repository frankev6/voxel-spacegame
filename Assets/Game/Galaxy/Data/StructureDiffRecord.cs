using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.6.</summary>
    public sealed class StructureDiffRecord : System.IDisposable
    {
        public ulong BodyKey;
        public uint Revision;                      // VoxelGrid.Revision when captured
        public Game.Voxel.Model.VoxelStructure Structure; // owned; full structure in M3 (brick diffs: GAL-016, M4)
        public void Dispose() { Structure?.Dispose(); Structure = null; }
    }
}
