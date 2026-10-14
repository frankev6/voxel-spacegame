#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.Presenters;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Economy;
using Game.Galaxy.Components;
using Game.Galaxy.Interaction;
using Game.Galaxy.Salvage;
using Game.Input;
using Game.UI.Services;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-021: a bounded, real salvage interaction over an approved blueprint wreck.</summary>
    internal sealed class DevPlaygroundSalvage : IDisposable
    {
        internal const int WrecksPerReset = 3;
        internal const string PadId = "salvage";
        internal const string PadTitle = "12  SALVAGE / RECOVER CARGO";
        internal const string PadDetail = "F prepares one stationary fighter wreck nearby. Wait for the HUD Salvage prompt, then press F again.\nThe normal salvage owner pulls the wreck into your hold; item toasts list the haul (I opens cargo). A full hold leaves salvage on that same wreck; make room and return. Three wrecks per playground reset.";
        static readonly ContentId BlueprintId = new("ship_fighter");
        readonly DevRuntimeHost host;
        readonly Action<string> report;
        World world;
        Entity target;
        ulong persistentId;
        int issued, generation, pending;
        bool subscribed, leavingWorld, disposed, observedSalvage;
        UIRoot ui;
        string status = "Salvage trial ready: F prepares a wreck; no cargo is granted by preparation.";

        internal DevPlaygroundSalvage(DevRuntimeHost host, Action<string> report)
        { this.host = host ?? throw new ArgumentNullException(nameof(host)); this.report = report; }
        internal Entity Target => target;
        internal ulong TargetPersistentId => persistentId;
        internal int IssuedWrecks => issued;
        internal string Status => status;

        /// <summary>Call from the pad's F activation. padPosition is absolute star-system position.</summary>
        internal DevCommandResult Activate(DevContext ctx, UIRoot rootUi, double3 padPosition)
        {
            if (disposed) return DevCommandResult.Fail("Salvage trial disposed.");
            if (!ReferenceEquals(ctx?.Host, host) || rootUi == null || !math.all(math.isfinite(padPosition)))
                return DevCommandResult.Fail("Salvage trial needs its host, real UI and a finite pad position.");
            if (!ctx.HasGameplay || !ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox
                || !session.HasActiveSession || session.IsTransitioning || session.CurrentScene != GameScene.World)
                return DevCommandResult.Fail("Salvage trial requires the isolated playground. Use playground reset to return.");
            if (!ctx.TryGetService<InputService>(out var input) || input.CurrentContext != InputContext.Flight)
                return DevCommandResult.Fail("Close the current screen before activating salvage.");
            if (!DevEcs.TryGetUniversePosition(ctx, ctx.PlayerShipRoot, out var player)
                || math.lengthsq(player - padPosition) > 28 * 28)
                return DevCommandResult.Fail("Fly inside the SALVAGE beacon first.");
            if (!ctx.TryGetService<SalvageService>(out _) || !ctx.TryGetService<EconomyService>(out _)
                || !ctx.TryGetService<Game.Data.Economy.ICargo>(out _) || !ctx.TryGetService<UiComposition>(out _)
                || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetService<VoxelStructureRegistry>(out var registry)
                || !ctx.TryGetFrame(out var frame))
                return DevCommandResult.Fail("Registered salvage, cargo, inventory UI or voxel owners are unavailable.");
            ui = rootUi;
            EnsureSubscriptions(); // subscribe after the registered SalvageService, which owns transfer
            Tick();
            if (pending != 0) return DevCommandResult.Pending(pending, "Preparing the existing wreck; another is not spawned.");
            if (HasTarget())
            {
                // Never open a screen here: F still needs to reach the real Flight input
                // sampler and BodyInteracted handler. Inventory opens only after transfer.
                if (ctx.TryGetInteractionTarget(out var interaction) && interaction.Target == target
                    && interaction.Kind == InteractionKind.Salvage && interaction.InRange != 0)
                    return DevCommandResult.Ok("Wreck in salvage range. This F press uses the normal salvage interaction; cargo opens after transfer.");
                return DevCommandResult.Ok("The existing wreck remains ahead. Fly toward its HUD Salvage prompt and press F; no replacement or reward was created.");
            }
            if (issued >= WrecksPerReset)
                return DevCommandResult.Ok("All three salvage wrecks have been issued. playground reset clears this trial and its cargo through the session owners.");
            if (!content.Catalog.TryGet<ShipBlueprintDefinition>(BlueprintId, out var blueprint)
                || !registry.TryGetBlueprint(blueprint.Id.Hash, out var source) || source == null || !source.Data.IsCreated)
                return DevCommandResult.Fail("The approved fighter blueprint is unavailable.");
            var config = content.Catalog.Get<EconomyConfigDefinition>(new ContentId("config_economy"));
            if (source.SolidCellCount <= 0 || source.SolidCellCount > config.MaxSalvageCells)
                return DevCommandResult.Fail("The approved fighter does not fit the production salvage cell limit.");
            if (math.floor(source.SolidCellCount * config.ScrapPerCell) < 1 || config.SalvageMaxScrap < 1)
                return DevCommandResult.Fail("The approved fighter has no guaranteed scrap under the current salvage configuration.");
            double3 absolute = padPosition + new double3(0, 0, 48); // inside the 40 m production reach (QA-001)
            float3 local = (float3)(absolute - frame.OriginInStarSystem);
            if (!math.all(math.isfinite(local))) return DevCommandResult.Fail("Salvage position exceeds the local frame.");
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            ulong id = session.Allocate();
            Entity request = Entity.Null;
            try
            {
                request = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, new GridSpawnParams
                {
                    PersistentId = id, Pose = new RigidTransform(quaternion.identity, local),
                    Kind = GridKind.Hulk, Motion = GridMotion.Static, Profile = GridProfile.Dummy,
                    Flags = GridSpawnFlags.Transient
                });
                // Initial wreck metadata, not a damage shortcut: the trial begins with a
                // decommissioned blueprint hulk. GridSpawnSystem owns all runtime cells,
                // colliders and children; SalvageMarkSystem supplies the interaction.
                em.AddComponent<Hulk>(request);
                em.AddComponentData(request, new HulkInfo
                { Cause = HulkCause.Split, Tick = (uint)ctx.Tick, OriginPersistentId = id, FormerKind = GridKind.Ship });
                em.AddComponentData(request, new UniversePosition { Value = absolute });
                world = ctx.World; target = request; persistentId = id; issued++; observedSalvage = false;
                int version = ++generation;
                pending = host.AddOperation(new Preparation(this, world, request, id, version));
                SetStatus("Preparing salvage wreck " + issued + "/" + WrecksPerReset + " from the approved Fighter. Cargo stays unchanged until you salvage it.");
                return DevCommandResult.Pending(pending, status);
            }
            catch
            {
                if (request != Entity.Null && em.Exists(request)) CancelRequest(em, request);
                if (target == request) { target = Entity.Null; persistentId = 0; pending = 0; generation++; }
                throw;
            }
        }

        /// <summary>Call once per playground frame, including while cargo is open.</summary>
        internal void Tick()
        {
            if (disposed) return;
            if (pending != 0 && host.TryGetOperation(pending, out var operation) && operation.Status != DevOperationStatus.Running)
            {
                pending = 0;
                if (operation.Status == DevOperationStatus.Failed)
                {
                    CleanupTarget();
                    SetStatus("Salvage preparation failed: " + operation.Message + " Issued " + issued + "/" + WrecksPerReset + "; reset restores the trial.");
                }
                else SetStatus(operation.Message);
            }
            if (target == Entity.Null || HasTarget()) return;
            target = Entity.Null; persistentId = 0;
            if (!observedSalvage) SetStatus("Trial wreck no longer exists. Issued " + issued + "/" + WrecksPerReset + "; return to the beacon for the next wreck, or reset.");
        }
        bool HasTarget()
        {
            if (world == null || !world.IsCreated || target == Entity.Null) return false;
            var em = world.EntityManager;
            return em.Exists(target) && (!em.HasComponent<PersistentId>(target) || em.GetComponentData<PersistentId>(target).Value == persistentId);
        }
        void EnsureSubscriptions()
        {
            if (subscribed) return;
            subscribed = true;
            EventBus<BodyInteracted>.Subscribe(Interacted);
            EventBus<SessionReset>.Subscribe(SessionReset);
            EventBus<SceneTransitionStarted>.Subscribe(TransitionStarted);
            EventBus<SceneTransitionFinished>.Subscribe(TransitionFinished);
        }
        void Interacted(BodyInteracted value)
        {
            if (disposed || value.Kind != (byte)InteractionKind.Salvage || persistentId == 0
                || value.GridPersistentId != persistentId || !HasTarget()
                || !host.Context.TryGetService<GameSession>(out var session) || !session.IsSandbox) return;
            // Our subscription follows SalvageService's. This is readback of its real
            // transfer/despawn request, not another TrySalvage call or an inventory give.
            observedSalvage = true;
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            bool cleared = em.HasComponent<GridDespawnRequest>(target) && em.IsComponentEnabled<GridDespawnRequest>(target)
                || em.HasComponent<SalvageCollected>(target);   // QA-001: streaming into the hold, despawned after the last wave
            string outcome = cleared ? "Wreck cleared by the production salvage owner." : "Salvage remains on the wreck. Make room in cargo, close inventory, then return and press F; the existing yields are retained.";
            if (host.Context.TryGetService<EconomyService>(out var economy))
                outcome += " Scrap carried: " + economy.Inventory.CountOf(new ContentId("resource_scrap"))
                    + ". Occupied slots: " + economy.Inventory.UsedSlots + "/" + economy.Inventory.Capacity + ".";
            SetStatus(outcome + " Wrecks issued: " + issued + "/" + WrecksPerReset + ".");
            // QA-001: no automatic inventory: it covered (and paused) the pull animation. Toasts list the items; I opens cargo.
        }
        void SessionReset(SessionReset value) => Reset();
        void TransitionStarted(SceneTransitionStarted value) => leavingWorld = value.From == GameScene.World;
        void TransitionFinished(SceneTransitionFinished value)
        {
            if (leavingWorld && value.Success && value.Scene != GameScene.World) Reset();
            leavingWorld = false;
        }
        /// <summary>Also call from playground Start/reset. Never changes inventory or wallet.</summary>
        internal void Reset()
        {
            CleanupTarget(); generation++; pending = 0; issued = 0; observedSalvage = false; ui = null;
            status = "Salvage trial ready: F prepares a wreck; no cargo is granted by preparation.";
        }
        void CleanupTarget()
        {
            if (HasTarget())
            {
                var em = world.EntityManager; em.CompleteAllTrackedJobs(); CancelRequest(em, target);
            }
            target = Entity.Null; persistentId = 0; world = null;
        }
        static void CancelRequest(EntityManager em, Entity root)
        {
            if (em.HasComponent<GridDespawnRequest>(root)) GridSpawnApi.RequestDespawn(em, root);
            else if (em.HasComponent<GridSpawnRequest>(root) && !em.HasComponent<VoxelGrid>(root))
                em.DestroyEntity(root); // caller-owned, unconsumed blueprint request; no spawn lease yet
        }
        void SetStatus(string text) { status = text; report?.Invoke(text); }
        public void Dispose()
        {
            if (disposed) return;
            Reset(); disposed = true;
            if (!subscribed) return;
            EventBus<BodyInteracted>.Unsubscribe(Interacted);
            EventBus<SessionReset>.Unsubscribe(SessionReset);
            EventBus<SceneTransitionStarted>.Unsubscribe(TransitionStarted);
            EventBus<SceneTransitionFinished>.Unsubscribe(TransitionFinished);
            subscribed = false;
        }

        sealed class Preparation : IDevOperation
        {
            readonly DevPlaygroundSalvage owner;
            readonly World world;
            readonly Entity root;
            readonly ulong id;
            readonly int generation;
            public DevOperationStatus Status { get; private set; } = DevOperationStatus.Running;
            public string Message { get; private set; } = "Waiting for actual wreck conversion, collider and salvage marking.";
            public double TimeoutSeconds => 10;
            internal Preparation(DevPlaygroundSalvage owner, World world, Entity root, ulong id, int generation)
            { this.owner = owner; this.world = world; this.root = root; this.id = id; this.generation = generation; }
            public void Tick(DevContext ctx)
            {
                if (Status != DevOperationStatus.Running) return;
                if (owner.disposed || owner.generation != generation || ctx.World != world || !world.IsCreated
                    || !ctx.HasGameplay || !ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox)
                { Fail("Playground session changed while preparing salvage."); return; }
                var em = world.EntityManager; em.CompleteAllTrackedJobs();
                if (!em.Exists(root)) { Fail("The approved wreck spawn request disappeared."); return; }
                if (!em.HasComponent<VoxelGrid>(root) || !em.HasComponent<GridSpawning>(root)
                    || em.IsComponentEnabled<GridSpawning>(root) || !em.HasComponent<PhysicsCollider>(root)
                    || !em.GetComponentData<PhysicsCollider>(root).IsValid) return;
                if (!em.HasComponent<PersistentId>(root) || em.GetComponentData<PersistentId>(root).Value != id)
                { Fail("Wreck identity changed."); return; }
                if (!em.HasComponent<Interactable>(root) || em.GetComponentData<Interactable>(root).Kind != InteractionKind.Salvage) return;
                var config = ctx.TryGetService<ContentService>(out var content)
                    ? content.Catalog.Get<EconomyConfigDefinition>(new ContentId("config_economy")) : null;
                if (config == null || !em.HasComponent<GridMass>(root) || em.GetComponentData<GridMass>(root).Data.CellCount > config.MaxSalvageCells)
                { Fail("Converted wreck exceeds the production salvage limit."); return; }
                Message = "Wreck " + owner.issued + "/" + WrecksPerReset + " ready. Approach the HUD Salvage prompt, then press F to recover items and inspect your cargo.";
                Status = DevOperationStatus.Succeeded;
            }
            void Fail(string text) { Message = text; Status = DevOperationStatus.Failed; }
        }
    }
}
#endif
