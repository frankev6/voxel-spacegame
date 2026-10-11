using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public struct GridColliderState : IComponentData
    {
        public GridColliderPhase Phase;
        public GridMotion Motion;        // SPEC-10 GridMotion, copied from GridBodyInit at body creation
        public byte IsEditorPreview;     // 1 if GridSource.Flags has EditorPreview
        public byte Reserved;
        public uint InstalledTick;       // SimClock.Tick of the last install (body creation counts)
        public int ChildCount;           // bricks in the installed compound; 0 while Provisional
        public int BoxCount;             // boxes in the installed collider; 1 while the provisional box is installed
    }
}
