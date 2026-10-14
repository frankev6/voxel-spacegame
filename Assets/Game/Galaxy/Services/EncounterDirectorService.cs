using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Travel;
using Game.Galaxy.Interaction;
using Game.Simulation.Factions;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Galaxy.Services
{
    /// <summary>SPEC-33: deterministic encounter scheduling and ownership outside the fixed step.</summary>
    public sealed class EncounterDirectorService : IGalaxyFrameTick, ISceneExiting, IDisposable
    {
        readonly GalaxyService galaxy;
        readonly StarSystemLoader loader;
        readonly TravelService travel;
        readonly DockingService docking;
        readonly IShipSpawner spawner;
        readonly FactionService factions;
        readonly World world;
        readonly ContentCatalog catalog;
        internal EncounterSaveState SaveState { get; } = new();
        List<EncounterInstance> active => SaveState.Active;
        Dictionary<ulong, uint> members => SaveState.Members;
        List<PendingEncounter> pending => SaveState.Pending;
        HashSet<ContentId> globallyUsed => SaveState.GloballyUsed;
        readonly HashSet<ContentId> spawnedThisVisit = new();
        readonly List<PlannedShip> planned = new(10);
        readonly List<(double3 pos, float yaw)> poses = new(10);
        readonly List<EncounterDefinition> eligible = new();
        readonly List<EncounterDefinition> definitions = new();
        readonly EntityQuery players, origins, clocks, combatStatus, worldScene, shifts, gameplay;
        uint nextInstanceSerial { get => SaveState.NextInstanceSerial; set => SaveState.NextInstanceSerial = value; }
        uint visitCount, rollIndex;
        double nextRollTime, arrivalTime;
        bool arrivedByJump, disposed;
        public EncounterDirectorService(GalaxyService galaxy, StarSystemLoader loader, TravelService travel, DockingService docking,
            IShipSpawner spawner, FactionService factions, TerritoryService territory, World world, ContentCatalog catalog)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (territory == null) throw new ArgumentNullException(nameof(territory));
            var configs = catalog.All<EncounterConfigDefinition>();
            if (configs.Count != 1) throw new InvalidOperationException("Exactly one encounter config is required.");
            Config = configs[0]; Enabled = true;
            definitions.AddRange(catalog.All<EncounterDefinition>());
            definitions.Sort((a,b) => StringComparer.Ordinal.Compare(a.Id.Value,b.Id.Value));
            var em = world.EntityManager;
            players = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>());
            origins = em.CreateEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            combatStatus = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerCombatStatus>());
            gameplay = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayActive>());
            shifts = em.CreateEntityQuery(ComponentType.ReadOnly<OriginShiftRecord>());
            worldScene = em.CreateEntityQuery(ComponentType.ReadOnly<WorldSceneActive>());
            EventBus<StarSystemEntered>.Subscribe(Entered);
            EventBus<SessionReset>.Subscribe(Reset);
        }
        public int TickOrder => 300;
        public int SceneExitOrder => 95;
        public bool Enabled { get; set; }
        public EncounterConfigDefinition Config { get; }
        public int ActiveInstanceCount => active.Count;
        public int AliveShipCount { get { int count = 0; foreach (var item in active) count += item.AliveMembers; return count; } }
        static EncounterInstanceInfo Info(EncounterInstance item) => new(item.Id, item.Definition, item.StarSystem,
            item.Source, item.State, item.Members.Count, item.AliveMembers, item.Persistent);
        public bool TryGetInstance(uint id, out EncounterInstanceInfo info)
        { foreach (var item in active) if (item.Id == id) { info = Info(item); return true; } info = default; return false; }
        public bool TryGetInstanceByMember(ulong persistentId, out EncounterInstanceInfo info)
        { info = default; return members.TryGetValue(persistentId, out var id) && TryGetInstance(id, out info); }
        public void GetActiveInstances(List<EncounterInstanceInfo> into)
        { into.Clear(); foreach (var item in active) into.Add(Info(item)); }
        public bool IsGloballyUsed(EncounterDefinition def) => def != null && (globallyUsed.Contains(def.Id) || SaveState.ResolvedGloballyUsed.Contains(def.Id));
        double Now => clocks.IsEmptyIgnoreFilter ? 0 : clocks.GetSingleton<SimClock>().UniversalTime;
        double3 Origin => origins.IsEmptyIgnoreFilter ? default : origins.GetSingleton<FrameOrigin>().OriginInStarSystem;
        Entity Player => players.IsEmptyIgnoreFilter ? Entity.Null : players.GetSingletonEntity();
        double3 Position(Entity entity)
        {
            var em = world.EntityManager;
            return Origin + (double3)(em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity).Position : em.GetComponentData<GridSpawnRequest>(entity).Pose.pos);
        }
        bool Ready => !gameplay.IsEmptyIgnoreFilter && loader.IsReady && travel.Phase == TravelPhase.Idle && galaxy.Galaxy != null && loader.Loaded.IsValid;
        public int SpawnEncounter(EncounterDefinition def, in EncounterAnchor anchor, int count, EncounterSource source)
        { int spawned = 0; for (int i = 0; i < math.clamp(count, 1, 10); i++) if (Request(def, anchor, source, source == EncounterSource.Combat)) spawned++; return spawned; }
        public int StartCombat(EncounterDefinition def, in EncounterAnchor anchor) => Request(def, anchor, EncounterSource.Combat, true) ? 1 : 0;
        bool Request(EncounterDefinition def, in EncounterAnchor anchor, EncounterSource source, bool combat)
        {
            if (disposed || def == null) return false;
            bool retain = source == EncounterSource.Narrative || source == EncounterSource.Combat;
            if (!Ready && !retain) return false;
            if (active.Count >= Config.MaxInstances) { GameLog.Warn("Encounters", "Encounter instance cap reached."); return false; }
            uint id = nextInstanceSerial++;
            if (Ready && Spawn(def, anchor, source, combat, id)) return true;
            if (retain)
            {
                if (galaxy.Galaxy == null) GameLog.WarnOnce("ENC.Sandbox.Pending", "Encounters", "Encounter queued; sandbox has no galaxy.");
                var system = anchor.Kind == EncounterAnchorKind.Body ? anchor.Body.System :
                    travel.Phase != TravelPhase.Idle && travel.Target.IsValid ? travel.Target :
                    loader.Loaded.IsValid ? loader.Loaded : galaxy.State.CurrentStarSystem;
                pending.Add(new PendingEncounter { Encounter = def.Id, InstanceId = id, StarSystem = system, Anchor = anchor, Combat = combat });
            }
            return false;
        }
        bool Spawn(EncounterDefinition def, in EncounterAnchor anchor, EncounterSource source, bool combat, uint id)
        {
            var em = world.EntityManager; em.CompleteDependencyBeforeRO<LocalTransform>();
            Entity player = Player;
            if (!Ready || player == Entity.Null || active.Count >= Config.MaxInstances || AliveShipCount >= Config.MaxShipsAlive) return false;
            var system = galaxy.Galaxy.Get(loader.Loaded);
            ulong seed = StableHash.Combine(StableHash.Combine(system.Seed, visitCount), StableHash.Combine(id, def.Id.Hash.Value));
            EncounterComposer.Compose(def, seed, .5f, planned);
            int capacity = Config.MaxShipsAlive - AliveShipCount;
            if (planned.Count > capacity)
            { planned.RemoveRange(capacity, planned.Count - capacity); GameLog.Warn("Encounters", "Encounter fleet clamped to the live ship cap."); }
            if (planned.Count == 0) return false;
            double3 playerPosition = Position(player);
            ResolveAnchor(anchor, playerPosition, out var anchorPosition, out float radius);
            float distance = combat ? math.max(Config.CombatMinDistanceM, def.SpawnDistanceMinM * .5f) : def.SpawnDistanceMinM;
            var parameters = new PlacementParams { DistanceMinM = distance, DistanceMaxM = combat ? distance : def.SpawnDistanceMaxM,
                MinPlayerDistanceM = combat ? Config.CombatMinDistanceM : math.max(Config.MinPlayerDistanceM,
                    arrivedByJump && Now - arrivalTime < Config.ArrivalGraceSeconds ? Config.ArrivalGraceDistanceM : 0),
                BodyClearanceM = Config.BodyClearanceM, Tries = Config.PlacementTries, StepDegrees = Config.PlacementStepDegrees };
            if (!EncounterPlacement.TryPlaceCentre(galaxy.GetContent(loader.Loaded), anchorPosition, radius, playerPosition, Now, seed, parameters, out var centre))
            {
                if (source == EncounterSource.Director) return false;
                centre = anchorPosition + new double3(radius + distance, 0, 0);
                GameLog.WarnOnce("ENC.Placement.Fallback", "Encounters", "Story encounter uses fallback placement.");
            }
            var formation = def.Formation;
            ushort formationIndex = catalog.IndexOf(formation);
            if (formation != null && (formationIndex == 0 || formation.Slots.Count < planned.Count - 1))
            {
                GameLog.WarnOnce("ENC.Formation.Invalid", "Encounters", "Encounter formation is unavailable or has too few slots; using line abreast.");
                formation = null;
            }
            EncounterPlacement.ShipPoses(centre, playerPosition, planned, def.ShipSpacingM, formation, poses);
            var instance = new EncounterInstance { Id = id, Definition = def, StarSystem = loader.Loaded, Source = source,
                State = InstanceState.Active, Persistent = source != EncounterSource.Director || def.PersistUntilDefeated, Seed = seed, Anchor = anchor };
            Entity leader = Entity.Null;
            for (int k = 0; k < planned.Count; k++)
            {
                var ship = planned[k]; var pose = poses[k];
                var pid = spawner.Spawn(new GalaxyShipSpawnArgs { Blueprint = ship.Blueprint.Id, Position = (float3)(pose.pos - Origin),
                    Rotation = quaternion.RotateY(pose.yaw), Allegiance = new Allegiance { Faction = factions.Indices.IndexOf(def.Faction), Team = (byte)(1 + id % 254) },
                    WithAi = true, AiProfile = ship.Profile != null ? ship.Profile.Id : ContentId.None, StarSystem = loader.Loaded, Encounter = def.Id.Hash });
                if (pid.Value == 0 || !spawner.TryGetRoot(pid.Value, out var root)) { GameLog.WarnOnce("ENC.Spawn.Failed", "Encounters", "Encounter ship spawn failed."); continue; }
                if (k == 0) leader = root;
                else if (formation != null && leader != Entity.Null)
                    em.AddComponentData(root, new FleetMember { Leader = leader, Slot = (byte)(k - 1), FormationIndex = formationIndex });
                em.AddComponentData(root, new EncounterTag { Encounter = def.Id.Hash, Instance = id });
                if (def.Hailable && instance.Members.Count == 0) em.AddComponentData(root, new Interactable { Kind = InteractionKind.Hail, TemplateIndex = 0xffff, DistanceM = Config.HailDistanceM });
                if ((combat || def.Kind == EncounterKind.Ambush) && em.HasComponent<AiBrain>(root))
                { var brain = em.GetComponentData<AiBrain>(root); brain.Anchor = brain.GoalPosition = (float3)(playerPosition - Origin); brain.Initialized = 1; brain.OrbitSign = 1; brain.GoalHeadingYaw = pose.yaw;
                    brain.ShiftSequence = shifts.IsEmptyIgnoreFilter ? 0 : shifts.GetSingleton<OriginShiftRecord>().Sequence; em.SetComponentData(root, brain); }
                instance.Members.Add(pid.Value); members.Add(pid.Value, id);
            }
            if (instance.Members.Count == 0) return false;
            instance.AliveMembers = instance.Members.Count; active.Add(instance); spawnedThisVisit.Add(def.Id);
            if (def.UniqueGlobal) globallyUsed.Add(def.Id);
            return true;
        }
        void ResolveAnchor(in EncounterAnchor anchor, double3 player, out double3 position, out float radius)
        {
            position = player; radius = 0;
            if (anchor.Kind == EncounterAnchorKind.Player) return;
            var content = galaxy.GetContent(loader.Loaded);
            foreach (var body in content.Bodies)
            {
                if ((body.State & BodyStateFlags.Destroyed) != 0) continue;
                bool match = anchor.Kind == EncounterAnchorKind.Body ? body.Id.Equals(anchor.Body) :
                    galaxy.Galaxy.BodyTemplates[body.TemplateIndex].StoryTag == anchor.Tag;
                if (!match) continue;
                position = OrbitMath.Position(body, Now); radius = body.RadiusM; return;
            }
            GameLog.WarnOnce("ENC.Anchor.BodyMissing", "Encounters", "Encounter anchor unavailable; using player position.");
        }
        public void Tick(in GalaxyFrameContext ctx)
        {
            if (disposed || worldScene.IsEmptyIgnoreFilter) return;
            var em = world.EntityManager;
            em.CompleteDependencyBeforeRO<AiBrain>(); em.CompleteDependencyBeforeRO<LocalTransform>(); em.CompleteDependencyBeforeRO<PlayerCombatStatus>();
            Entity player = Player;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i]; if (!loader.IsReady || !item.StarSystem.Equals(loader.Loaded)) continue;
                item.AliveMembers = 0; item.AnyEngaging = false; bool far = player != Entity.Null;
                foreach (ulong pid in item.Members)
                {
                    if (!spawner.TryGetRoot(pid, out var root) || !em.Exists(root) || em.HasComponent<Hulk>(root)) continue;
                    item.AliveMembers++;
                    if (em.HasComponent<AiBrain>(root)) item.AnyEngaging |= em.GetComponentData<AiBrain>(root).State == AiState.Engage;
                    if (player != Entity.Null && math.length((Position(root) - Position(player)).xz) <= Config.DespawnDistanceM) far = false;
                }
                if (item.AliveMembers == 0)
                {
                    item.State = InstanceState.Defeated; Remove(i);
                    EventBus<EncounterDefeated>.Publish(new EncounterDefeated(item.Id, item.Definition.Id.Hash, item.StarSystem.Value));
                    if (item.Definition.DefeatLoot != null && Game.Core.Services.TryGet<ILogicContext>(out var logic))
                        logic.Economy?.GiveLoot(catalog.IndexOf(item.Definition.DefeatLoot), -1, StableHash.Combine(item.Seed, 0xDEFEA7));
                    continue;
                }
                far &= !item.AnyEngaging;
                if (!far) item.FarSinceUniversalTime = -1;
                else if (item.FarSinceUniversalTime < 0) item.FarSinceUniversalTime = ctx.UniversalTime;
                if (!item.Persistent && far && ctx.UniversalTime - item.FarSinceUniversalTime >= Config.DespawnDelaySeconds)
                {
                    foreach (ulong pid in item.Members) if (spawner.TryGetRoot(pid, out var root) && !em.HasComponent<Hulk>(root)) spawner.Despawn(pid);
                    item.State = InstanceState.Despawned; Remove(i);
                }
            }
            if (Ready && player != Entity.Null)
            {
                int spawned = 0;
                for (int i = 0; i < pending.Count && spawned < 2;)
                {
                    var request = pending[i];
                    if (request.StarSystem.Equals(loader.Loaded) && catalog.TryGet<EncounterDefinition>(request.Encounter, out var def) &&
                        Spawn(def, request.Anchor, request.Combat ? EncounterSource.Combat : EncounterSource.Narrative, request.Combat, request.InstanceId))
                    { pending.RemoveAt(i); spawned++; } else i++;
                }
            }
            Roll(ctx.UniversalTime, player);
        }
        void Roll(double time, Entity player)
        {
            if (!Enabled || !Ready || player == Entity.Null || world.EntityManager.HasComponent<Hulk>(player) || docking.IsDocked ||
                time < nextRollTime || active.Count >= Config.MaxInstances || AliveShipCount >= Config.MaxShipsAlive ||
                arrivedByJump && time - arrivalTime < Config.ArrivalGraceSeconds ||
                !combatStatus.IsEmptyIgnoreFilter && combatStatus.GetSingleton<PlayerCombatStatus>().HostilesEngaging != 0) return;
            var system = galaxy.Galaxy.Get(loader.Loaded); if ((system.Flags & StarSystemFlags.NoEncounters) != 0) return;
            int directors = 0; foreach (var item in active) if (item.Source == EncounterSource.Director && item.StarSystem.Equals(loader.Loaded)) directors++;
            if (directors >= Config.MaxActiveDirectorInstances) return;
            var rng = RngStreams.For(StableHash.Combine(system.Seed, visitCount), RngPurpose.EncounterDirector, rollIndex++);
            nextRollTime += Config.RollIntervalSeconds; if (nextRollTime <= time) nextRollTime = time + Config.RollIntervalSeconds;
            float chance = rng.NextFloat(), pick = rng.NextFloat(); if (chance >= Config.DangerSpawnChance[system.Danger]) return;
            Game.Core.Services.TryGet<ILogicContext>(out var logic); eligible.Clear(); double weight = 0;
            foreach (var def in definitions)
            {
                if (def.DirectorWeight <= 0 || system.Danger < def.MinDanger || system.Danger > def.MaxDanger ||
                    def.UniquePerVisit && spawnedThisVisit.Contains(def.Id) || def.UniqueGlobal && IsGloballyUsed(def)) continue;
                var condition = def.Condition;
                if (condition != null && !condition.IsAlwaysTrue && (condition.HasErrors || logic == null || !condition.Evaluate(logic))) continue;
                eligible.Add(def); weight += def.DirectorWeight;
            }
            double target = pick * weight;
            foreach (var def in eligible) { target -= def.DirectorWeight; if (target < 0) { Request(def, EncounterAnchor.Player(), EncounterSource.Director, false); break; } }
        }
        void Remove(int index) { foreach (ulong pid in active[index].Members) members.Remove(pid); active.RemoveAt(index); }
        void Retain(int index)
        {
            var item = active[index];
            if (item.Persistent && item.State == InstanceState.Active) pending.Add(new PendingEncounter { Encounter = item.Definition.Id,
                InstanceId = item.Id, StarSystem = item.StarSystem, Anchor = item.Anchor, Combat = item.Source == EncounterSource.Combat });
            Remove(index);
        }
        void Entered(StarSystemEntered value)
        {
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].StarSystem.Value != value.StarSystemValue) Retain(i);
            visitCount = value.VisitCount; rollIndex = 0; arrivalTime = Now; nextRollTime = Now + Config.FirstRollDelaySeconds;
            arrivedByJump = value.Reason == StarSystemEntryReason.Jump; spawnedThisVisit.Clear();
        }
        void Reset(SessionReset value)
        { SaveState.Reset(value.Reason); spawnedThisVisit.Clear(); visitCount = rollIndex = 0; arrivedByJump = false; arrivalTime = 0; nextRollTime = Now + Config.FirstRollDelaySeconds; }
        public void OnSceneExiting(in SceneExitContext context)
        { if (context.Scene != GameScene.World) return; for (int i = active.Count - 1; i >= 0; i--) Retain(i); spawnedThisVisit.Clear(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true; EventBus<StarSystemEntered>.Unsubscribe(Entered); EventBus<SessionReset>.Unsubscribe(Reset);
            // Unity may dispose the ECS world before GameRoot.OnDestroy during Editor exit.
            if (world != null && world.IsCreated)
            { players.Dispose(); origins.Dispose(); clocks.Dispose(); combatStatus.Dispose(); worldScene.Dispose(); shifts.Dispose(); gameplay.Dispose(); }
            active.Clear(); members.Clear(); pending.Clear();
        }
    }
}
