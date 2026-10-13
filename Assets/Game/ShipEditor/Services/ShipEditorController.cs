using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Mathematics;
using UnityEngine;

namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §6. Contract shell; EDT-014 owns tool/controller behavior.</summary>
    public sealed class ShipEditorController : System.IDisposable
    {
        // TODO(EDT-014): controller, tool framework, palette state and cancel chain.
        public ToolId ActiveTool => throw new System.NotImplementedException("EDT-014");
        public SymmetrySettings Symmetry => throw new System.NotImplementedException("EDT-014");
        public bool ConsumesWheel => throw new System.NotImplementedException("EDT-014"); // Box drag
        public int StateVersion => throw new System.NotImplementedException("EDT-014");
        public event System.Action<float3, float> FocusRequested // grid-space centre/radius, in cells
        {
            add => throw new System.NotImplementedException("EDT-014");
            remove => throw new System.NotImplementedException("EDT-014");
        }
        public void Tick(in EditorInputFrame input, Ray gridRay) => throw new System.NotImplementedException("EDT-014");
        public void SelectTool(ToolId tool) => throw new System.NotImplementedException("EDT-014");
        public void SelectBlock(ushort block) => throw new System.NotImplementedException("EDT-014");
        public void SelectColor(byte color) => throw new System.NotImplementedException("EDT-014");
        public void ToggleMirror(int axis) => throw new System.NotImplementedException("EDT-014");
        public void NudgeMirrorPlane(int axis, int halfCells) => throw new System.NotImplementedException("EDT-014");
        public void ToggleSlice() => throw new System.NotImplementedException("EDT-014");
        public void StepSlice(int delta) => throw new System.NotImplementedException("EDT-014");
        public void ToggleXRay() => throw new System.NotImplementedException("EDT-014");
        public void CycleBoxMode() => throw new System.NotImplementedException("EDT-014");
        // TODO(EDT-017): copy records for a blueprint stamp.
        public void BeginStamp(VoxelStructure stampSource) => throw new System.NotImplementedException("EDT-017");
        public bool Cancel() => throw new System.NotImplementedException("EDT-014");
        public void Dispose() => throw new System.NotImplementedException("EDT-014");
    }
}
