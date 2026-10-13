using Game.Simulation.Flight;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.5; single-threaded gather follows live thruster state and grid transforms.</summary>
    [BurstCompile] internal partial struct PlumeGatherJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        public NativeArray<PlumeInstance> Output;
        public NativeReference<int> Count;
        public int Cap;
        public float Minimum, Length;
        public float4 Color;
        void Execute(in ThrusterState state, in ThrusterVisual visual, in FunctionalBlock block)
        {
            if (state.Intensity <= 0 || state.Intensity < Minimum || Count.Value >= Cap || Hulks.HasComponent(block.Grid) || !Transforms.HasComponent(block.Grid)) return;
            Output[Count.Value++] = FxStateMath.Plume(Transforms[block.Grid].Value, visual.NozzleLocal, visual.ExhaustDirLocal,
                visual.NozzleRadius, state.Intensity, Length, Color);
        }
    }
}
