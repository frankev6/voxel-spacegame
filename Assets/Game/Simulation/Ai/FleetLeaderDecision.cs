using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Simulation.Ai
{
    internal struct FleetLeaderDecision
    {
        public AiState State;
        public ShipTarget Target;
    }
    [BurstCompile] internal struct ClearFleetLeaders : IJob
    {
        public NativeHashMap<Entity, FleetLeaderDecision> Leaders;
        public void Execute() => Leaders.Clear();
    }
    // Captured after leader decisions and before member decisions: no read/write alias on AiBrain or ShipTarget.
    [BurstCompile, WithNone(typeof(Hulk))]
    internal partial struct CaptureFleetLeaders : IJobEntity
    {
        public NativeHashMap<Entity, FleetLeaderDecision> Leaders;
        void Execute(Entity entity, in AiBrain brain, in ShipTarget target)
        { Leaders.Add(entity, new FleetLeaderDecision { State = brain.State, Target = target }); }
    }
}
