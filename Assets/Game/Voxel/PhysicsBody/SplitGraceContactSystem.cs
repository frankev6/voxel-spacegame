using Game.Core.Components;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Suppresses split-piece contacts with their parent until the grace tick (SPEC-12 §5.12).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateBodyPairsGroup))]
    [UpdateBefore(typeof(PhysicsCreateContactsGroup))]
    public partial struct SplitGraceContactSystem : ISystem
    {
        ComponentLookup<SplitGrace> grace;
        ComponentLookup<ChunkContactState> chunkContacts;
        EntityQuery graceQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            grace = state.GetComponentLookup<SplitGrace>(true);
            chunkContacts = state.GetComponentLookup<ChunkContactState>(true);
            graceQuery = SystemAPI.QueryBuilder().WithAll<SplitGrace>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (graceQuery.IsEmptyIgnoreFilter) return;
            grace.Update(ref state);
            chunkContacts.Update(ref state);
            var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            state.Dependency = new ArmChunkContactsJob
            {
                World = world, Tick = SystemAPI.GetSingleton<SimClock>().Tick
            }.Schedule(state.Dependency);
            var job = new SplitGraceBodyPairsJob
            {
                Grace = grace, ChunkContacts = chunkContacts,
                Tick = SystemAPI.GetSingleton<SimClock>().Tick
            };
            // Unity Physics' in-simulation scheduling pattern, confirmed by PHY-002.
            state.Dependency = job.Schedule(SystemAPI.GetSingletonRW<SimulationSingleton>().ValueRW,
                ref SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld, state.Dependency);
        }
        [BurstCompile]
        partial struct ArmChunkContactsJob : IJobEntity
        {
            [ReadOnly] public PhysicsWorld World;
            public uint Tick;
            void Execute(Entity entity, ref ChunkContactState contacts, in SplitGrace grace)
            {
                if (contacts.Armed != 0 || Tick < grace.UntilTick) return;
                int parentIndex = World.GetRigidBodyIndex(grace.Other);
                if (parentIndex < 0) { contacts.Armed = 1; return; }
                int pieceIndex = World.GetRigidBodyIndex(entity);
                if (pieceIndex < 0) return;
                var parent = World.Bodies[parentIndex]; var piece = World.Bodies[pieceIndex];
                if (!parent.Collider.IsCreated || !piece.Collider.IsCreated) { contacts.Armed = 1; return; }
                var input = new ColliderDistanceInput(piece.Collider, 0, piece.WorldFromBody, piece.Scale);
                // A one-way latch: once clear, subsequent real impacts must collide normally.
                if (!parent.CalculateDistance(input)) contacts.Armed = 1;
            }
        }
    }
}
