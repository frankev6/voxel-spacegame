using System;
using System.Collections.Generic;
using Game.Core.Components;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace Game.Tests.Common
{
    /// <summary>World with only the systems under test. Implements SPEC-71 §4.1, §5.1. Time never comes from the wall clock: every
    /// update sets World time itself, and a counted rate manager runs exactly the fixed ticks asked for (SPEC-71 open question 4).</summary>
    public sealed class EcsTestWorld : IDisposable
    {
        public const float TickSeconds = 1f / 60f;
        /// <summary>Frame stepping runs at most this many ticks per frame: World.MaximumDeltaTime 0.1 s, as in the game.</summary>
        public const int MaxTicksPerFrame = 6;

        static int created;

        public World World { get; private set; }
        public EntityManager EntityManager => World.EntityManager;
        public uint TicksRun { get; private set; }
        public double ElapsedSeconds { get; private set; }

        readonly World previousDefault;
        readonly FixedStepSimulationSystemGroup fixedGroup;
        readonly CountedRateManager rate = new CountedRateManager(TickSeconds);
        readonly bool hasAdvanceSystem;
        double frameAccumulator;

        EcsTestWorld(IReadOnlyList<Type> systems)
        {
            World = new World("EcsTestWorld#" + (++created), WorldFlags.Game);
            previousDefault = World.DefaultGameObjectInjectionWorld;
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, WithGroups(systems));
            fixedGroup = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            var simulation = World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulation.AddSystemToUpdateList(fixedGroup);                 // idempotent; needed when no listed system sits in the fixed step
            simulation.SortSystems();
            fixedGroup.Timestep = TickSeconds;
            fixedGroup.RateManager = rate;
            World.MaximumDeltaTime = MaxTicksPerFrame * TickSeconds;
            World.DefaultGameObjectInjectionWorld = World;
            foreach (var t in systems) if (t.Name == "SimClockAdvanceSystem") hasAdvanceSystem = true;
        }

        /// <summary>ISystem or ComponentSystemBase types. Their [UpdateInGroup] groups up to the root are added too, with the default
        /// command buffer systems and every group they order against (empty anchors), so a reduced world logs no ordering warnings.</summary>
        public static EcsTestWorld Create(params Type[] systems) => new EcsTestWorld(systems ?? Array.Empty<Type>());

        static readonly Type[] DefaultAnchors =
        {
            typeof(BeginInitializationEntityCommandBufferSystem), typeof(EndInitializationEntityCommandBufferSystem),
            typeof(BeginSimulationEntityCommandBufferSystem), typeof(EndSimulationEntityCommandBufferSystem),
            typeof(BeginFixedStepSimulationEntityCommandBufferSystem), typeof(EndFixedStepSimulationEntityCommandBufferSystem),
            typeof(BeginPresentationEntityCommandBufferSystem),
        };

        static List<Type> WithGroups(IReadOnlyList<Type> systems)
        {
            var all = new List<Type>();
            var seen = new HashSet<Type>();
            void Add(Type t)
            {
                if (t == null || !seen.Add(t)) return;
                all.Add(t);
                if (t == typeof(InitializationSystemGroup) || t == typeof(SimulationSystemGroup) || t == typeof(PresentationSystemGroup)) return;
                foreach (UpdateInGroupAttribute a in t.GetCustomAttributes(typeof(UpdateInGroupAttribute), true)) Add(a.GroupType);
                // Groups named by UpdateBefore/UpdateAfter are added as empty ordering anchors (PhysicsSystemGroup for the gameplay
                // groups, TransformSystemGroup for VoxelRebuildGroup/WorldGroup); plain systems are not, their OnCreate may need more.
                foreach (UpdateBeforeAttribute a in t.GetCustomAttributes(typeof(UpdateBeforeAttribute), true))
                    if (typeof(ComponentSystemGroup).IsAssignableFrom(a.SystemType)) Add(a.SystemType);
                foreach (UpdateAfterAttribute a in t.GetCustomAttributes(typeof(UpdateAfterAttribute), true))
                    if (typeof(ComponentSystemGroup).IsAssignableFrom(a.SystemType)) Add(a.SystemType);
            }
            // The default command buffer systems: Unity's and the game's groups order themselves against them (UpdateAfter/Before), and
            // systems under test may record into them.
            foreach (var t in DefaultAnchors) Add(t);
            foreach (var t in systems) Add(t);
            return all;
        }

        public Entity AddGameplayActive() => GetOrCreateSingletonEntity<GameplayActive>();
        public Entity AddWorldSceneActive() => GetOrCreateSingletonEntity<WorldSceneActive>();

        /// <summary>Auto-advanced by StepTicks unless SimClockAdvanceSystem is present. Lives on the GameplayActive entity if there is one
        /// (SPEC-02 §4.3).</summary>
        public Entity AddSimClock(uint tick = 0, double universalTime = 0)
        {
            var entity = TryGetSingletonEntity<GameplayActive>(out var active) ? active : EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new SimClock { Tick = tick, UniversalTime = universalTime, SessionStartUniversalTime = universalTime - tick / 60.0 });
            return entity;
        }

        public Entity AddSingleton<T>(T value) where T : unmanaged, IComponentData
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, value);
            return entity;
        }

        /// <summary>Completes dependencies first.</summary>
        public T GetSingleton<T>() where T : unmanaged, IComponentData
        {
            CompleteAll();
            using var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return q.GetSingleton<T>();
        }

        /// <summary>Exactly <paramref name="ticks"/> fixed ticks, one World.Update each.</summary>
        public void StepTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                ElapsedSeconds += TickSeconds;
                RunUpdate(1, TickSeconds);
            }
        }

        /// <summary>One frame of <paramref name="deltaSeconds"/>: as many whole ticks as the accumulated time holds, at most
        /// MaxTicksPerFrame (excess time is dropped, as Unity's catch-up manager does). Needs SimClockAdvanceSystem when a SimClock exists.</summary>
        public void StepFrame(float deltaSeconds)
        {
            if (!hasAdvanceSystem && HasComponent<SimClock>())
                throw new InvalidOperationException("EcsTestWorld.StepFrame: add SimClockAdvanceSystem for frame stepping (a SimClock exists)");
            ElapsedSeconds += deltaSeconds;
            frameAccumulator += deltaSeconds;
            int ticks = (int)Math.Floor(frameAccumulator / TickSeconds + 1e-6);
            if (ticks > MaxTicksPerFrame) { ticks = MaxTicksPerFrame; frameAccumulator = 0; }
            else frameAccumulator = Math.Max(0, frameAccumulator - ticks * (double)TickSeconds);
            RunUpdate(ticks, deltaSeconds);
        }

        void RunUpdate(int ticks, float deltaSeconds)
        {
            rate.Remaining = ticks;
            World.SetTime(new TimeData(ElapsedSeconds, deltaSeconds));
            World.Update();
            CompleteAll();
            int ran = rate.TakeExecuted();
            TicksRun += (uint)ran;
            if (!hasAdvanceSystem && ran > 0 && TryGetSingletonEntity<SimClock>(out var clockEntity))
            {
                var clock = EntityManager.GetComponentData<SimClock>(clockEntity);
                clock.Tick += (uint)ran;
                clock.UniversalTime = clock.SessionStartUniversalTime + clock.Tick / 60.0;   // SPEC-02 decision 6
                EntityManager.SetComponentData(clockEntity, clock);
            }
        }

        /// <summary>One direct update (unit tests of a single system); creates the system if needed.</summary>
        public void UpdateSystem<T>() where T : unmanaged, ISystem
        {
            var handle = World.GetOrCreateSystem<T>();
            handle.Update(World.Unmanaged);
        }

        public void UpdateManaged<T>() where T : ComponentSystemBase => World.GetOrCreateSystemManaged<T>().Update();

        public void CompleteAll() => EntityManager.CompleteAllTrackedJobs();

        /// <summary>CompleteAll; World.Dispose; restores World.DefaultGameObjectInjectionWorld. Disposing twice is safe.</summary>
        public void Dispose()
        {
            if (World == null) return;
            if (World.IsCreated)
            {
                CompleteAll();
                if (World.DefaultGameObjectInjectionWorld == World) World.DefaultGameObjectInjectionWorld = previousDefault != null && previousDefault.IsCreated ? previousDefault : null;
                World.Dispose();
            }
            World = null;
        }

        bool HasComponent<T>() where T : unmanaged, IComponentData
        {
            using var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !q.IsEmptyIgnoreFilter;
        }

        bool TryGetSingletonEntity<T>(out Entity entity) where T : unmanaged, IComponentData
        {
            using var q = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (q.CalculateEntityCount() == 1) { entity = q.GetSingletonEntity(); return true; }
            entity = Entity.Null;
            return false;
        }

        Entity GetOrCreateSingletonEntity<T>() where T : unmanaged, IComponentData
        {
            if (TryGetSingletonEntity<T>(out var e)) return e;
            return EntityManager.CreateEntity(ComponentType.ReadWrite<T>());
        }

        /// <summary>Runs the fixed-step group exactly Remaining times per World.Update, pushing fixed time and the group's rewindable
        /// allocator the way Unity's own managers do (Entities RateUtils).</summary>
        unsafe sealed class CountedRateManager : IRateManager
        {
            float timestep;
            double lastFixedTime;
            bool pushed;
            int executed;
            DoubleRewindableAllocators* oldAllocators;
            public int Remaining;

            public CountedRateManager(float timestep) { this.timestep = timestep; }
            public float Timestep { get => timestep; set => timestep = value; }

            public int TakeExecuted() { int n = executed; executed = 0; return n; }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                if (pushed)
                {
                    group.World.PopTime();
                    group.World.RestoreGroupAllocator(oldAllocators);
                    pushed = false;
                }
                if (Remaining <= 0) return false;
                Remaining--;
                group.World.PushTime(new TimeData(lastFixedTime, timestep));
                lastFixedTime += timestep;
                oldAllocators = group.World.CurrentGroupAllocators;
                group.World.SetGroupAllocator(group.RateGroupAllocators);
                pushed = true;
                executed++;
                return true;
            }
        }
    }
}
