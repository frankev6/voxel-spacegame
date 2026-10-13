using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.4: max player thrust and stable voice ownership for the nearest six beam starts.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(AudioEventDrainSystem))]
    public partial class AudioLoopBridgeSystem : SystemBase
    {
        static readonly ProfilerMarker UpdateMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AudioLoopBridgeSystem.Update");
        NativeReference<EngineInput> engine;
        NativeList<BeamInput> beams;
        NativeHashMap<Entity, int> voices;
        NativeArray<Entity> owners;
        ComponentLookup<PlayerShip> players;
        ComponentLookup<LocalToWorld> transforms;
        ComponentLookup<Hulk> hulks;
        internal AudioService Service { get; private set; }

        protected override void OnCreate()
        {
            RequireForUpdate<GameplayActive>();
            engine = new NativeReference<EngineInput>(Allocator.Persistent);
            beams = new NativeList<BeamInput>(6, Allocator.Persistent);
            voices = new NativeHashMap<Entity, int>(6, Allocator.Persistent);
            owners = new NativeArray<Entity>(6, Allocator.Persistent);
            players = GetComponentLookup<PlayerShip>(true); transforms = GetComponentLookup<LocalToWorld>(true); hulks = GetComponentLookup<Hulk>(true);
        }
        internal void Bind(AudioService service)
        {
            Service?.StopLoopVoices(); Service = service; ClearAssignments();
        }
        internal bool TryGetBeamVoice(Entity beam, out int voice) => voices.TryGetValue(beam, out voice);
        void ClearAssignments()
        {
            voices.Clear(); beams.Clear(); engine.Value = default;
            for (int i = 0; i < owners.Length; i++) owners[i] = Entity.Null;
        }
        protected override void OnUpdate()
        {
            if (Service == null) return;
            using var marker = UpdateMarker.Auto();
            // Presentation's synchronous gathers must see the actual component producers, including root transforms.
            EntityManager.CompleteDependencyBeforeRO<FunctionalBlock>(); EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();
            EntityManager.CompleteDependencyBeforeRO<PlayerShip>(); EntityManager.CompleteDependencyBeforeRO<Hulk>();
            players.Update(this); transforms.Update(this); hulks.Update(this);
            if (Service.EngineLoopAvailable)
            {
                EntityManager.CompleteDependencyBeforeRO<ThrusterState>(); engine.Value = default;
                new EngineGatherJob { Output = engine, Players = players, Transforms = transforms, Hulks = hulks }.Run();
                var input = engine.Value;
                if (input.Root == Entity.Null) Service.FadeOutLoopVoice(0);
                else Service.SetLoopTarget(0, input.Intensity, .8f + .4f * input.Intensity, input.Position);
            }
            else Service.StopLoopVoice(0);

            beams.Clear();
            if (Service.BeamLoopAvailable && math.isfinite(Service.BeamLoopRadius) && Service.BeamLoopRadius >= 0)
            {
                EntityManager.CompleteDependencyBeforeRO<BeamState>();
                new BeamGatherJob { Output = beams, Transforms = transforms, Hulks = hulks,
                    Listener = Service.ListenerPosition, RadiusSquared = Service.BeamLoopRadius * Service.BeamLoopRadius }.Run();
            }
            // Lookups only: never let hash-map enumeration decide which free voice a newcomer receives.
            for (int i = 0; i < owners.Length; i++)
            {
                Entity owner = owners[i]; if (owner == Entity.Null || Contains(owner)) continue;
                voices.Remove(owner); owners[i] = Entity.Null; Service.FadeOutLoopVoice(i + 1);
            }
            for (int i = 0; i < beams.Length; i++)
            {
                var beam = beams[i];
                if (!voices.TryGetValue(beam.Entity, out int voice))
                {
                    int free = 0; while (free < owners.Length && owners[free] != Entity.Null) free++;
                    if (free == owners.Length) break;
                    voice = free + 1; owners[free] = beam.Entity; voices.Add(beam.Entity, voice);
                    Service.StopLoopVoice(voice); // A newly assigned beam begins its own envelope and clip.
                }
                Service.SetLoopTarget(voice, 1, Service.BeamLoopPitch, beam.Position);
            }
            // Null cues stop their voices immediately, including an old beam already fading out.
            if (!Service.BeamLoopAvailable) for (int i = 1; i <= 6; i++) Service.StopLoopVoice(i);
        }
        bool Contains(Entity entity)
        { for (int i = 0; i < beams.Length; i++) if (beams[i].Entity == entity) return true; return false; }
        protected override void OnStopRunning() { Service?.StopLoopVoices(); ClearAssignments(); }
        protected override void OnDestroy()
        {
            CompleteDependency(); Service?.StopLoopVoices(); Service = null;
            engine.Dispose(); beams.Dispose(); voices.Dispose(); owners.Dispose();
        }

        struct EngineInput { public Entity Root; public float Intensity; public float3 Position; }
        struct BeamInput { public Entity Entity; public float3 Position; public float DistanceSquared; }
        [BurstCompile] partial struct EngineGatherJob : IJobEntity
        {
            public NativeReference<EngineInput> Output;
            [ReadOnly] public ComponentLookup<PlayerShip> Players;
            [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            void Execute(in FunctionalBlock functional, in ThrusterState thruster)
            {
                Entity root = functional.Grid;
                if (!Players.HasComponent(root) || !Transforms.HasComponent(root) || Hulks.HasComponent(root) || !math.isfinite(thruster.Intensity)) return;
                float3 position = Transforms[root].Position; if (!math.all(math.isfinite(position))) return;
                float intensity = math.saturate(thruster.Intensity); var current = Output.Value;
                if (current.Root != Entity.Null && (intensity < current.Intensity || intensity == current.Intensity &&
                    (root.Index > current.Root.Index || root.Index == current.Root.Index && root.Version >= current.Root.Version))) return;
                Output.Value = new EngineInput { Root = root, Intensity = intensity, Position = position };
            }
        }
        [BurstCompile] partial struct BeamGatherJob : IJobEntity
        {
            public NativeList<BeamInput> Output;
            [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            public float3 Listener; public float RadiusSquared;
            void Execute(Entity entity, in FunctionalBlock functional, in BeamState beam)
            {
                if (beam.Active == 0 || !Transforms.HasComponent(functional.Grid) || Hulks.HasComponent(functional.Grid)) return;
                float3 position = math.transform(Transforms[functional.Grid].Value, beam.MuzzleLocal);
                float distance = math.distancesq(position, Listener);
                if (!math.all(math.isfinite(position)) || !math.isfinite(distance) || distance > RadiusSquared) return;
                var input = new BeamInput { Entity = entity, Position = position, DistanceSquared = distance };
                // A sorted six-element prefix is enough; candidate count never grows with the number of active beams.
                if (Output.Length == 6 && Compare(input, Output[5]) >= 0) return;
                if (Output.Length < 6) Output.Add(default);
                int at = Output.Length - 1;
                while (at > 0 && Compare(input, Output[at - 1]) < 0) { Output[at] = Output[at - 1]; at--; }
                Output[at] = input;
            }
            static int Compare(BeamInput a, BeamInput b)
            {
                if (a.DistanceSquared < b.DistanceSquared) return -1; if (a.DistanceSquared > b.DistanceSquared) return 1;
                int index = a.Entity.Index.CompareTo(b.Entity.Index); return index != 0 ? index : a.Entity.Version.CompareTo(b.Entity.Version);
            }
        }
    }
}
