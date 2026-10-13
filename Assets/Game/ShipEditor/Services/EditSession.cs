using Game.ShipEditor.Data;
using Game.Voxel.Model;

namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §6. EDT-005 owns the session; related methods retain their implementing task IDs.</summary>
    public sealed class EditSession : System.IDisposable
    {
        // TODO(EDT-005): construct the session and its owned structures/indexes.
        public EditSession(ShipEditorEntryArgs args, EditorContext context) => throw new System.NotImplementedException("EDT-005");
        public EditSessionOptions Options => throw new System.NotImplementedException("EDT-005");
        public VoxelStructure Draft => throw new System.NotImplementedException("EDT-005"); // read-only to callers
        public VoxelStructure Original => throw new System.NotImplementedException("EDT-005"); // never mutated; replaced on commit
        public EditHistory History => throw new System.NotImplementedException("EDT-005");
        public bool IsDirty => throw new System.NotImplementedException("EDT-005");
        public int Revision => throw new System.NotImplementedException("EDT-005");
        // TODO(EDT-008): connectivity ownership and relabel completion.
        public bool ConnectivityPending => throw new System.NotImplementedException("EDT-008");
        public event System.Action<EditChange> Changed
        {
            add => throw new System.NotImplementedException("EDT-005");
            remove => throw new System.NotImplementedException("EDT-005");
        }
        public bool TryApply(EditBatch batch, AtomicityMode mode, int strokeId, ToolId tool, string label, ApplyReport report)
            => throw new System.NotImplementedException("EDT-005");
        public bool Undo() => throw new System.NotImplementedException("EDT-005");
        public bool Redo() => throw new System.NotImplementedException("EDT-005");
        // TODO(EDT-006): lazily refreshed BOM.
        public BillOfMaterials GetBom() => throw new System.NotImplementedException("EDT-006");
        // TODO(EDT-010): original/draft stats.
        public StatTotals GetStats(bool original) => throw new System.NotImplementedException("EDT-010");
        // TODO(EDT-009): full validation and issue list.
        public void GetIssues(System.Collections.Generic.List<EditIssue> result) => throw new System.NotImplementedException("EDT-009");
        // TODO(EDT-011): atomic inventory/target commit.
        public CommitResult Commit() => throw new System.NotImplementedException("EDT-011");
        public void Discard() => throw new System.NotImplementedException("EDT-005");
        public void CompleteRelabel() => throw new System.NotImplementedException("EDT-008");
        public ulong ComputeDraftHash() => throw new System.NotImplementedException("EDT-005");
        public void Dispose() => throw new System.NotImplementedException("EDT-005");
    }
}
