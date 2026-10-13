using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Weapons
{
    /// <summary>Player target cycling, cursor selection and autofire edges (SPEC-21 §5.3).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ShipCommandGroup))]
    // PlayerCommandApplySystem is OrderFirst; normal bucket follows it without a forbidden cross-bucket attribute (SPEC-02 decision 13).
    public partial struct PlayerTargetingSystem : ISystem
    {
        EntityStorageInfoLookup entities;
        ComponentLookup<Hulk> hulks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<WeaponTuning>();
            state.RequireForUpdate<SimClock>();
            entities = state.GetEntityStorageInfoLookup();
            hulks = state.GetComponentLookup<Hulk>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            entities.Update(ref state);
            hulks.Update(ref state);
            var hash = SystemAPI.GetSingleton<SpatialHash>();
            var handle = new TargetingJob
            {
                Hash = hash.AsReadOnly(), Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Tuning = SystemAPI.GetSingleton<WeaponTuning>(), Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Entities = entities, Hulks = hulks
            }.Schedule(hash.BeginRead(state.Dependency));
            hash.EndRead(handle);
            state.Dependency = handle;
        }

        [BurstCompile]
        [WithAll(typeof(PlayerShip))]
        [WithNone(typeof(Hulk))]
        partial struct TargetingJob : IJobEntity
        {
            [ReadOnly] public SpatialHashQueryData Hash;
            [ReadOnly] public FactionRelations Relations;
            [ReadOnly] public EntityStorageInfoLookup Entities;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            public WeaponTuning Tuning;
            public uint Tick;

            void Execute(Entity entity, in ShipCommand command, in Allegiance allegiance, in LocalTransform pose,
                ref ShipTarget target, ref WeaponControl control)
            {
                Entity previous = target.Value;
                if (!Entities.Exists(target.Value)) target.Value = Entity.Null;
                var edges = command.Edges;
                if ((edges & ShipCommandEdges.AutofireToggle) != 0)
                {
                    control.Autofire ^= 1;
                    control.AimMode = control.Autofire != 0 ? WeaponAimMode.TargetLead : WeaponAimMode.AimPoint;
                }
                if ((edges & ShipCommandEdges.TargetClear) != 0) target.Value = Entity.Null;
                if ((edges & ShipCommandEdges.TargetNext) != 0) target.Value = Next(entity, allegiance, pose.Position, target.Value);
                if ((edges & ShipCommandEdges.TargetUnderCursor) != 0 && command.AimValid != 0 && math.all(math.isfinite(command.AimWorld)))
                    target.Value = UnderCursor(entity, command.AimWorld);
                if (target.Value != previous) target.SinceTick = Tick;
            }

            Entity Next(Entity self, in Allegiance own, float3 position, Entity current)
            {
                // Find the successor in the specified total order without allocating/sorting a per-player list.
                int currentIndex = Hash.FindIndex(current);
                double currentRank = 0;
                bool hasCurrent = currentIndex >= 0 && NextCandidate(Hash.Get(currentIndex), self, own, position, out currentRank);
                Entity first = Entity.Null, after = Entity.Null;
                double firstRank = double.PositiveInfinity, afterRank = double.PositiveInfinity;
                var candidates = new FixedList512Bytes<int>();
                int total = Hash.QuerySphere(position, Tuning.MaxTargetRange, ref candidates);
                bool overflow = total > candidates.Capacity;
                int count = overflow ? Hash.Proxies.Length : candidates.Length;
                for (int i = 0; i < count; i++)
                {
                    var proxy = Hash.Get(overflow ? i : candidates[i]);
                    if (!NextCandidate(proxy, self, own, position, out double rank)) continue;
                    if (Before(rank, proxy.Entity, firstRank, first)) { first = proxy.Entity; firstRank = rank; }
                    if (hasCurrent && Before(currentRank, current, rank, proxy.Entity) && Before(rank, proxy.Entity, afterRank, after))
                    { after = proxy.Entity; afterRank = rank; }
                }
                return after != Entity.Null ? after : first;
            }

            bool NextCandidate(in SpatialProxy proxy, Entity self, in Allegiance own, float3 position, out double rank)
            {
                rank = 0;
                if (proxy.Entity == self || !Entities.Exists(proxy.Entity) || Hulks.HasComponent(proxy.Entity) ||
                    (proxy.Kind != ProxyKind.Ship && proxy.Kind != ProxyKind.Station) || !Relations.IsHostile(own, proxy.Allegiance)) return false;
                double distance = Distance(position, proxy.Center);
                if (!math.isfinite(distance) || distance > Tuning.MaxTargetRange) return false;
                rank = math.round(distance * 100d); // centimetre key, then Entity.Index; round-to-nearest-even (Q41)
                return true;
            }

            Entity UnderCursor(Entity self, float3 point)
            {
                var candidates = new FixedList512Bytes<int>();
                int total = Hash.QuerySphere(point, Tuning.UnderCursorRadius, ref candidates);
                bool overflow = total > candidates.Capacity;
                int count = overflow ? Hash.Proxies.Length : candidates.Length;
                Entity best = Entity.Null;
                double bestDistance = double.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    var proxy = Hash.Get(overflow ? i : candidates[i]);
                    if (proxy.Entity == self || !Entities.Exists(proxy.Entity) || proxy.Kind == ProxyKind.Celestial) continue;
                    double distance = math.max(0d, Distance(point, proxy.Center) - proxy.Radius);
                    if (!math.isfinite(distance) || distance > Tuning.UnderCursorRadius) continue;
                    if (Before(distance, proxy.Entity, bestDistance, best)) { best = proxy.Entity; bestDistance = distance; }
                }
                return best;
            }

            static double Distance(float3 a, float3 b) => math.length((double3)a - (double3)b);
            static bool Before(double rank, Entity entity, double otherRank, Entity other) =>
                rank < otherRank || rank == otherRank && (other == Entity.Null || entity.Index < other.Index);
        }
    }
}
