using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Unity.Entities;
using Unity.Profiling;

namespace Game.App.Systems
{
    /// <summary>Refills the Burst id block before initialization work, including between scenes (SPEC-01 §5.8).</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup)), UpdateBefore(typeof(GameInitGroup))]
    public partial class PersistentIdRefillSystem : SystemBase
    {
        static readonly ProfilerMarker CheckMarker = new ProfilerMarker(ProfilerCategory.Scripts, "PersistentIdRefillSystem.Check");
        protected override void OnCreate() => RequireForUpdate<PersistentIdAllocator>();
        protected override void OnUpdate()
        {
            using var marker = CheckMarker.Auto();
            // Singleton access does not wait for last frame's Burst consumer. Only this component's readers/writers must finish.
            EntityManager.CompleteDependencyBeforeRW<PersistentIdAllocator>();
            ref var allocator = ref SystemAPI.GetSingletonRW<PersistentIdAllocator>().ValueRW;
            if (!Game.Core.Services.TryGet<GameSession>(out var session) || !session.HasActiveSession)
            { allocator = default; return; }
            if (allocator.Next < allocator.End && allocator.End - allocator.Next >= (ulong)session.PersistentIdRefillThreshold) return;
            ulong start = session.GrantBlock(session.PersistentIdBlockSize);
            allocator = new PersistentIdAllocator { Next = start, End = start + (ulong)session.PersistentIdBlockSize };
        }
    }
}
