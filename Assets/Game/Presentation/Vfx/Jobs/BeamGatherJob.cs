using Game.Data.Definitions;
using Game.Simulation.Weapons;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace Game.Presentation.Vfx
{
    /// <summary>SPEC-63 §5.4 serial gather: clips to simulation's hit endpoint and throttles impact events.</summary>
    [BurstCompile] internal partial struct BeamGatherJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
        [ReadOnly] public NativeArray<FxSlot> Slots;
        [ReadOnly] public NativeHashMap<ulong, int> Hashes;
        public NativeArray<BeamInstance> Output;
        public NativeReference<int> Count;
        public NativeList<FxEvent> Impacts;
        public int Cap, DefaultSlot;
        public uint Frame;
        public float Width;
        void Execute(Entity entity, in BeamState beam, in FunctionalBlock block)
        {
            if (beam.Active == 0 || !Transforms.HasComponent(block.Grid) || Count.Value >= Cap) return;
            int slot = DefaultSlot;
            if (beam.Fx.Value != 0 && Hashes.TryGetValue(beam.Fx.Value, out int found) && Slots[found].Kind == FxKind.Beam) slot = found;
            if (slot < 0) return;
            float3 start = math.transform(Transforms[block.Grid].Value, beam.MuzzleLocal);
            Output[Count.Value++] = new BeamInstance { Start = start, End = beam.EndWorld, Width = beam.Width > 0 ? beam.Width : Width,
                Intensity = 1, Color = FxStateMath.Linear(Slots[slot].DefaultColorRgba8) };
            if (beam.HitSomething != 0 && (Frame + (uint)entity.Index) % 6 == 0 && Impacts.Length < Impacts.Capacity)
                Impacts.AddNoResize(new FxEvent { Kind = FxKind.HitEnergy, Pos = beam.EndWorld, Dir = math.normalizesafe(start - beam.EndWorld), Scale = 1 });
        }
    }
}
