using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Interaction;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Galaxy.Salvage
{
    /// <summary>One structural batch for newly available non-player/non-body hulks. ADR-0117 derivation: a pure function of the hulk,
    /// so every machine marks the same wrecks (the salvage tint and prompt on co-op guests). A collected wreck loses its mark the same
    /// way on every machine.</summary>
    [UpdateInGroup(typeof(WorldGroup)), UpdateAfter(typeof(InteractionQuerySystem))]
    public partial class SalvageMarkSystem : SystemBase
    {
        EntityQuery newHulks, collected;
        protected override void OnCreate()
        {
            RequireForUpdate<GameplayActive>(); RequireForUpdate<WorldSceneActive>(); RequireForUpdate<EconomyConfigRef>();
            newHulks = new EntityQueryBuilder(Allocator.Temp).WithAll<Hulk, VoxelGrid, PersistentId>()
                .WithNone<Interactable, PlayerShip, Pilot, BodyGridOwner, GridSpawning>().WithNone<SalvageCollected>().Build(this);
            collected = new EntityQueryBuilder(Allocator.Temp).WithAll<SalvageCollected, Interactable>().Build(this);
        }
        protected override void OnUpdate()
        {
            if (!collected.IsEmptyIgnoreFilter) EntityManager.RemoveComponent<Interactable>(collected);
            if (newHulks.IsEmpty) return;
            // Snapshot the exact batch before adding the component changes query membership.
            using var entities = newHulks.ToEntityArray(Allocator.TempJob);
            EntityManager.AddComponent<Interactable>(entities);
            new MarkJob { Entities = entities, Interactables = GetComponentLookup<Interactable>(),
                Distance = SystemAPI.GetSingleton<EconomyConfigRef>().SalvageDistanceM }.Run();
        }
        [BurstCompile] struct MarkJob : Unity.Jobs.IJob
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            public ComponentLookup<Interactable> Interactables;
            public float Distance;
            public void Execute()
            {
                foreach (var entity in Entities)
                    Interactables[entity] = new Interactable { Kind = InteractionKind.Salvage, TemplateIndex = ushort.MaxValue, DistanceM = Distance };
            }
        }
    }
}
