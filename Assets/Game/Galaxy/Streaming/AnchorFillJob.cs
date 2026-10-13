using Game.Core;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Galaxy.Streaming
{
    [BurstCompile]
    internal struct AnchorFillJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<BodySpec> Specs;
        public double Time;
        public double3 Origin;
        public BodyFlags Flags;
        public byte Orbiting;
        // Every index writes one unique newly-created entity; groups execute sequentially.
        [NativeDisableParallelForRestriction] public ComponentLookup<StarSystemBody> Bodies;
        [NativeDisableParallelForRestriction] public ComponentLookup<UniversePosition> Positions;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalToWorld> Matrices;
        [NativeDisableParallelForRestriction] public ComponentLookup<BodyOrbit> Orbits;
        public void Execute(int index)
        {
            var entity = Entities[index]; var spec = Specs[index];
            double3 position = OrbitMath.Position(spec, Time);
            var transform = LocalTransform.FromPositionRotation(MathD.ToLocal(position, Origin), Planar.YawRotation(spec.YawRad));
            Bodies[entity] = new StarSystemBody { Id = spec.Id, TemplateIndex = spec.TemplateIndex, RadiusM = spec.RadiusM, Flags = Flags | ((spec.State & BodyStateFlags.Destroyed) != 0 ? BodyFlags.Destroyed : BodyFlags.None) };
            Positions[entity] = new UniversePosition { Value = position };
            Transforms[entity] = transform; Matrices[entity] = new LocalToWorld { Value = transform.ToMatrix() };
            if (Orbiting != 0) Orbits[entity] = new BodyOrbit { Orbit = spec.Orbit, ParentOrbit = spec.ParentOrbit };
        }
    }

    /// <summary>Lookup provider for the managed loader's immediate structural batch; never auto-updated.</summary>
    [DisableAutoCreation]
    internal partial class AnchorFillSystem : SystemBase
    {
        protected override void OnUpdate() { }
        internal void Fill(NativeArray<Entity> entities, NativeArray<BodySpec> specs, double time, double3 origin, BodyFlags flags, bool orbiting)
        {
            Dependency.Complete();
            new AnchorFillJob { Entities = entities, Specs = specs, Time = time, Origin = origin, Flags = flags,
                Orbiting = (byte)(orbiting ? 1 : 0), Bodies = GetComponentLookup<StarSystemBody>(),
                Positions = GetComponentLookup<UniversePosition>(), Transforms = GetComponentLookup<LocalTransform>(),
                Matrices = GetComponentLookup<LocalToWorld>(), Orbits = GetComponentLookup<BodyOrbit>()
            }.Schedule(entities.Length, 32).Complete();
        }
    }
}
