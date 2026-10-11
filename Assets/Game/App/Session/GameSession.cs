using System;
using System.Collections.Generic;
using Game.App.Flow;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Services;
using Game.Persistence.Data;
using Game.Persistence.Services;
using UnityEngine;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Unity.Entities;
using Game.Voxel.Grid;

namespace Game.App.Session
{
    /// <summary>Campaign identity, flow and save context, restored in place (SPEC-01 §4.3, §5.4–5.10).</summary>
    public sealed partial class GameSession : IPersistentIdAllocator, IGameFlow, ISaveContext, ISceneReady, IDisposable
    {
        readonly World world;
        internal readonly int PersistentIdBlockSize, PersistentIdRefillThreshold;
        public bool HasActiveSession { get; private set; }
        public ulong NextPersistentId { get; private set; } = 1;

        // Shared allocation initializer; the composition constructor binds flow and persistence below.
        internal GameSession(World world, GameConfigDefinition config)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (config == null) throw new ArgumentNullException(nameof(config));
            gameConfig = config; CommanderName = config.DefaultCommanderName;
            Fleet = new FleetModel(); RegisterModel(Fleet);
            PersistentIdBlockSize = config.PersistentIdBlockSize;
            PersistentIdRefillThreshold = config.PersistentIdRefillThreshold;
            if (PersistentIdBlockSize < 256 || PersistentIdBlockSize > 65536 ||
                PersistentIdRefillThreshold < 0 || PersistentIdRefillThreshold >= PersistentIdBlockSize)
                throw new ArgumentOutOfRangeException(nameof(config), "Invalid persistent-id allocation limits");
        }

        /// <summary>Returns the next ungranted id; zero and ulong.MaxValue are never issued.</summary>
        public ulong Allocate()
        {
            if (NextPersistentId == ulong.MaxValue) throw new InvalidOperationException("Persistent id counter exhausted");
            return NextPersistentId++;
        }

        /// <summary>Reserves an exclusive block, advancing the saved counter past every id Burst can issue.</summary>
        internal ulong GrantBlock(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((ulong)count > ulong.MaxValue - NextPersistentId) throw new InvalidOperationException("Persistent id counter exhausted");
            ulong start = NextPersistentId;
            NextPersistentId += (ulong)count;
            return start;
        }

        /// <summary>Session start/restore/end boundary: discard outstanding grants before changing the saved counter.</summary>
        internal void ResetPersistentIds(ulong next, bool active)
        {
            if (next == 0) throw new ArgumentOutOfRangeException(nameof(next));
            var em = world.EntityManager;
            em.CompleteDependencyBeforeRW<PersistentIdAllocator>();
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<PersistentIdAllocator>());
            if (!query.IsEmptyIgnoreFilter) query.SetSingleton(default(PersistentIdAllocator));
            NextPersistentId = next;
            HasActiveSession = active;
        }
        readonly GameConfigDefinition gameConfig;
        SceneFlow flow;
        SaveService save;
        readonly List<ISessionModel> models = new List<ISessionModel>();
        readonly List<ISessionWriteback> writebacks = new List<ISessionWriteback>();
        readonly List<INewGameInitializer> initializers = new List<INewGameInitializer>();
        readonly List<ISaveBlocker> blockers = new List<ISaveBlocker>();
        bool operationPending, disposed, worldReady;
        BlueprintLabMode labMode;
        internal bool PlayerDefeated;
        internal bool IsSandbox { get; private set; }
        internal bool SpawnPlayerInWorld { get; private set; } = true;
        public FleetModel Fleet { get; }
        internal FleetSaveParticipant FleetParticipant { get; private set; }
        internal PlayerShipSpawner PlayerSpawner { get; private set; }
        VoxelStructureRegistry fleetRegistry;
        public bool IsCreativeSession { get; private set; }
        public string CommanderName { get; private set; }
        public ulong CampaignSeed { get; private set; }
        public double UniversalTime { get; private set; }
        public double PlaytimeSeconds { get; private set; }
        public GameScene CurrentScene => flow?.CurrentScene ?? GameScene.None;
        public bool IsTransitioning => operationPending || (flow?.IsTransitioning ?? false);

        /// <summary>Composition-owned session; models retain their identity through every new game and restore.</summary>
        public GameSession(SceneFlow flow, SaveService save, World world, ContentService content, GameConfigDefinition config) : this(world, config)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            flow.BindSession(this);
            flow.RegisterReady(this);
            RegisterWriteback(new SimClockWriteback(world, value => UniversalTime = value));
            // TODO(GAL-008): bind GalaxyService and GalaxyState; M1 enters World without streaming.
            // Composition binds the existing blueprint library/registry through BindFleet.
        }

        internal void BindFleet(BlueprintLibrary blueprints, VoxelStructureRegistry registry, ContentCatalog catalog)
        {
            if (FleetParticipant != null) throw new InvalidOperationException("Fleet already bound");
            fleetRegistry = registry;
            FleetParticipant = new FleetSaveParticipant(Fleet, catalog, blueprints, gameConfig, this,
                message => save.ReportRestoreWarning("FND-W001", "FLET", "Structure", message), ReserveRestoredFleetIds);
            PlayerSpawner = new PlayerShipSpawner(this, world, registry);
            flow.RegisterReady(PlayerSpawner); flow.RegisterExiting(PlayerSpawner);
            RegisterWriteback(new FleetWriteback(world, registry, PlayerSpawner));
        }
        void ReserveRestoredFleetIds(ulong highestId)
        {
            if (highestId < NextPersistentId) return;
            ResetPersistentIds(checked(highestId + 1), false);
            save.ReportRestoreWarning("FND-W002", "SESS", "NextPersistentId", "Advanced the session id counter past restored fleet ships.");
        }
        void ReleaseFleetWorld()
        {
            // Session boundaries happen after teardown, or during final composition disposal. Finish readers before releasing ownership.
            if (world.IsCreated) world.EntityManager.CompleteAllTrackedJobs();
            fleetRegistry?.CancelSpawnReads(); PlayerSpawner?.Release();
        }

        public void RegisterModel(ISessionModel model) { if (model == null) throw new ArgumentNullException(nameof(model)); if (!models.Contains(model)) models.Add(model); }
        public void RegisterWriteback(ISessionWriteback writeback)
        {
            if (writeback == null) throw new ArgumentNullException(nameof(writeback));
            if (writebacks.Contains(writeback)) return;
            int at = writebacks.Count; while (at > 0 && writebacks[at - 1].WritebackOrder > writeback.WritebackOrder) at--;
            writebacks.Insert(at, writeback);
        }
        public void RegisterNewGameInitializer(INewGameInitializer initializer)
        {
            if (initializer == null) throw new ArgumentNullException(nameof(initializer));
            if (initializers.Contains(initializer)) return;
            int at = initializers.Count; while (at > 0 && initializers[at - 1].NewGameOrder > initializer.NewGameOrder) at--;
            initializers.Insert(at, initializer);
        }
        public void RegisterSaveBlocker(ISaveBlocker blocker) { if (blocker == null) throw new ArgumentNullException(nameof(blocker)); if (!blockers.Contains(blocker)) blockers.Add(blocker); }
        public void RunWritebacks() { foreach (var writeback in writebacks) writeback.WriteBackFromWorld(); }
        public void PrepareCapture() => RunWritebacks();
        public void Tick(float unscaledDeltaTime)
        {
            if (!disposed && HasActiveSession && worldReady && CurrentScene == GameScene.World && !IsTransitioning &&
                unscaledDeltaTime > 0 && !float.IsInfinity(unscaledDeltaTime)) PlaytimeSeconds += unscaledDeltaTime;
        }
        public int SceneReadyOrder => SceneHookOrder.Session;
        public void OnSceneReady(in SceneReadyContext context) => worldReady = context.Scene == GameScene.World;
        public bool IsSceneReadyComplete(in SceneReadyContext context) => true;

        internal void BeginNewGame(in NewGameArgs args) => ResetForNewSession(args, false);
        internal void BeginSandbox(ulong seed)
            => ResetForNewSession(new NewGameArgs(gameConfig.DefaultCommanderName, seed == 0 ? 1ul : seed, true, ContentId.None), true);
        void ResetForNewSession(in NewGameArgs args, bool sandbox)
        {
            save.ResetForNewGame();
            if (save.ActiveProfile.Value == null) save.SetActiveProfile(save.CreateProfile(gameConfig.DefaultCommanderName).Id);
            ResetPersistentIds(1, false);
            ReleaseFleetWorld();
            foreach (var model in models) model.ResetToEmpty();
            CommanderName = string.IsNullOrWhiteSpace(args.CommanderName) ? gameConfig.DefaultCommanderName : args.CommanderName.Trim();
            if (CommanderName.Length > 32) CommanderName = CommanderName.Substring(0, 32);
            CampaignSeed = args.GalaxySeed != 0 ? args.GalaxySeed : (ulong)System.Diagnostics.Stopwatch.GetTimestamp() | 1ul;
            UniversalTime = 0; PlaytimeSeconds = 0; IsCreativeSession = args.Creative; PlayerDefeated = false;
            IsSandbox = sandbox; SpawnPlayerInWorld = true;
            // TODO(GAL-008): clear galaxy state for IsSandbox; otherwise galaxy.NewGame and the canonical start location.
            if (FleetParticipant != null) Fleet.Add(FleetParticipant.CreateStartShip(), true);
            if (!sandbox) foreach (var initializer in initializers) initializer.InitializeNewGame(args);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.NewGame));
            HasActiveSession = true;
        }

        internal void RestoreFrom(LoadResult loaded)
        {
            var report = save.Restore(loaded.Save);
            PlayerDefeated = false;
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded));
            // TODO(GAL-008): galaxy.OnSessionRestored after publishing Loaded, before World entry (Decision 8).
            HasActiveSession = true;
            if (report.ErrorCount > 0) GameLog.Warn(LogCategories.Flow, "Loaded with " + report.ErrorCount + " problems; see the log");
            // TODO(UI-003): surface the report through the notification service when it exists.
        }
        internal void EndSession()
        {
            ResetPersistentIds(1, false); worldReady = false; PlayerDefeated = false;
            IsSandbox = false; SpawnPlayerInWorld = true;
            ReleaseFleetWorld();
            foreach (var model in models) model.ResetToEmpty();
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Cleared));
        }
        internal void ApplySavedState(string commander, ulong seed, double time, double playtime, ulong next, bool creative)
        {
            ResetPersistentIds(next, false);
            CommanderName = commander; CampaignSeed = seed; UniversalTime = time; PlaytimeSeconds = playtime; IsCreativeSession = creative;
            IsSandbox = false; SpawnPlayerInWorld = true;
            PlayerDefeated = false;
        }
        internal void ResetSavedState() => ApplySavedState(gameConfig.DefaultCommanderName, gameConfig.DevQuickStartSeed, 0, 0, 1, false);

        public SaveBlockReason GetBlockReason()
        {
            if (!HasActiveSession) return SaveBlockReason.NoSession;
            // An operation reservation prevents competing flow calls while its pre-transition save is still allowed.
            if (flow != null && flow.IsTransitioning) return SaveBlockReason.Transitioning;
            if (CurrentScene == GameScene.BlueprintLab) return SaveBlockReason.EditorOpen;
            if (PlayerDefeated) return SaveBlockReason.PlayerDead;
            foreach (var blocker in blockers) { var reason = blocker.GetSaveBlock(); if (reason != SaveBlockReason.None) return reason; }
            // TODO(AI-008): cached PlayerCombatStatus query once its AI-001 component exists; complete its readers before reading.
            return SaveBlockReason.None;
        }
        public SaveBlockReason GetSaveBlock()
        {
            try { return GetBlockReason(); }
            catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "session save check"); return SaveBlockReason.Transitioning; }
        }
        public void DescribeForMeta(SaveMeta meta)
        {
            meta.CommanderName = CommanderName; meta.PlaytimeSeconds = PlaytimeSeconds;
            meta.LocationText = "Sandbox"; // TODO(GAL-008): current star-system display name.
        }
        public void CollectThumbnails(List<ThumbnailRequest> changed, HashSet<string> keep)
        {
            foreach (var ship in Fleet.Ships)
                keep.Add("ship_" + ship.PersistentId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        FlowResult Access(bool allowed)
        {
            if (disposed || flow == null) return FlowResult.NotAllowed;
            if (IsTransitioning) return FlowResult.Busy;
            return allowed ? FlowResult.Ok : FlowResult.NotAllowed;
        }
        static Awaitable<FlowResult> Complete(FlowResult value)
        { var source = new AwaitableCompletionSource<FlowResult>(); source.SetResult(value); return source.Awaitable; }
        static Awaitable Between(Action action)
        { action(); var source = new AwaitableCompletionSource(); source.SetResult(); return source.Awaitable; }
        async Awaitable<FlowResult> Execute(Func<Awaitable<FlowResult>> action)
        {
            operationPending = true;
            try { return await action(); }
            catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "session flow"); return FlowResult.Failed; }
            finally { operationPending = false; }
        }
        internal Awaitable<FlowResult> EnterSandboxAsync(SandboxArgs args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var access = Access(true);
            if (access != FlowResult.Ok) return Complete(access);
            if (save.IsBusy) return Complete(FlowResult.Busy);
            return Execute(async () =>
            {
                using var pause = flow.PauseForExitSave();
                if (CurrentScene == GameScene.World) await SaveBeforeSceneExit();
                return await flow.TransitionAsync(GameScene.World, SceneEntryReason.DevQuickStart, betweenScenes: () => Between(() =>
                {
                    BeginSandbox(args.Seed);
                    SpawnPlayerInWorld = args.SpawnPlayer;
                }));
            });
#else
            return Complete(FlowResult.NotAllowed);
#endif
        }
        public Awaitable<FlowResult> NewGameAsync(NewGameArgs args)
        {
            var access = Access(CurrentScene == GameScene.MainMenu || CurrentScene == GameScene.World);
            if (access != FlowResult.Ok) return Complete(access);
            if (save.IsBusy) return Complete(FlowResult.Busy);
            return Execute(() => flow.TransitionAsync(GameScene.World, SceneEntryReason.NewGame, betweenScenes: () => Between(() => BeginNewGame(args))));
        }
        internal Awaitable<FlowResult> DevQuickStartAsync()
        {
            if (IsTransitioning || save.IsBusy) return Complete(FlowResult.Busy);
            var args = new NewGameArgs(gameConfig.DefaultCommanderName, gameConfig.DevQuickStartSeed, false, ContentId.None);
            return Execute(() => flow.TransitionAsync(GameScene.World, SceneEntryReason.DevQuickStart, betweenScenes: () => Between(() => BeginNewGame(args))));
        }
        public Awaitable<FlowResult> LoadGameAsync(string slotId)
        {
            var access = Access(CurrentScene == GameScene.MainMenu || CurrentScene == GameScene.World);
            if (access != FlowResult.Ok) return Complete(access);
            return Execute(async () =>
            {
                var loaded = await save.ReadAsync(new SlotRef(save.ActiveProfile, new SlotId(slotId)));
                if (!loaded.Success) return FlowResult.Failed;
                return await flow.TransitionAsync(GameScene.World, SceneEntryReason.LoadGame, betweenScenes: () => Between(() => RestoreFrom(loaded)));
            });
        }
        public Awaitable<FlowResult> SaveGameAsync(string slotId, SaveReason reason)
        {
            var access = Access(CurrentScene == GameScene.World);
            if (access != FlowResult.Ok) return Complete(access);
            return Execute(async () =>
            {
                if (GetBlockReason() != SaveBlockReason.None) return FlowResult.Blocked;
                var result = await save.SaveAsync(new SlotRef(save.ActiveProfile, new SlotId(slotId)), reason);
                return Result(result);
            });
        }
        static FlowResult Result(SaveResult result) => result.Success ? FlowResult.Ok : result.Error == SaveErrorKind.Blocked ? FlowResult.Blocked : FlowResult.Failed;
        public Awaitable<FlowResult> QuitToMainMenuAsync(bool saveFirst)
        {
            var access = Access(CurrentScene == GameScene.World || CurrentScene == GameScene.BlueprintLab);
            if (access != FlowResult.Ok) return Complete(access);
            return Execute(async () =>
            {
                using var pause = flow.PauseForExitSave();
                if (saveFirst)
                {
                    if (CurrentScene != GameScene.World || GetBlockReason() != SaveBlockReason.None) return FlowResult.Blocked;
                    var result = await save.SaveAsync(save.HasCurrentSlot ? save.CurrentSlot : save.NextAutosaveSlot(save.ActiveProfile), SaveReason.Quit);
                    if (!result.Success) return Result(result);
                }
                return await flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.QuitToMenu, betweenScenes: () => Between(EndSession));
            });
        }
        public Awaitable<FlowResult> EnterBlueprintLabAsync(BlueprintLabArgs args)
        {
            var access = Access(args.Mode == BlueprintLabMode.Creative ? CurrentScene == GameScene.MainMenu :
                args.Mode == BlueprintLabMode.FleetShip && CurrentScene == GameScene.World && HasActiveSession);
            if (access != FlowResult.Ok) return Complete(access);
            return Execute(async () =>
            {
                using var pause = flow.PauseForExitSave();
                if (CurrentScene == GameScene.World) await SaveBeforeSceneExit();
                labMode = args.Mode;
                // TODO(FND-013): fleet pose capture and editor commit/cancel writeback wiring.
                return await flow.TransitionAsync(GameScene.BlueprintLab, SceneEntryReason.EnterBlueprintLab, args,
                    args.Mode == BlueprintLabMode.Creative ? () => Between(() => BeginNewGame(new NewGameArgs(gameConfig.DefaultCommanderName, 1, true, ContentId.None))) : null);
            });
        }
        public Awaitable<FlowResult> ExitBlueprintLabAsync()
        {
            var access = Access(CurrentScene == GameScene.BlueprintLab);
            if (access != FlowResult.Ok) return Complete(access);
            return Execute(() => flow.TransitionAsync(labMode == BlueprintLabMode.Creative ? GameScene.MainMenu : GameScene.World,
                SceneEntryReason.ExitBlueprintLab, betweenScenes: labMode == BlueprintLabMode.Creative ? () => Between(EndSession) : null));
        }
        async Awaitable SaveBeforeSceneExit()
        {
            if (save.ActiveProfile.Value == null || GetBlockReason() != SaveBlockReason.None) return;
            var operation = save.SaveAsync(save.NextAutosaveSlot(save.ActiveProfile), SaveReason.AutoSceneTransition).GetAwaiter();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!operation.IsCompleted && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
            if (operation.IsCompleted) operation.GetResult();
            else GameLog.Warn(LogCategories.Flow, "The exit autosave did not finish within 10 s; continuing");
        }
        public void QuitToDesktop()
        {
            try { if (Game.Core.Services.TryGet<Game.App.Settings.SettingsService>(out var settings)) settings.Flush(); Application.Quit(); }
            catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "quit to desktop"); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            flow?.UnregisterReady(this);
            if (PlayerSpawner != null) { flow?.UnregisterReady(PlayerSpawner); flow?.UnregisterExiting(PlayerSpawner); }
            flow?.BindSession(null); save?.AttachContext(null);
            ReleaseFleetWorld(); PlayerSpawner?.Dispose(); Fleet.Dispose();
            DisposeThumbnailCaptures();
            if (world.IsCreated) ResetPersistentIds(NextPersistentId, false);
            models.Clear(); writebacks.Clear(); initializers.Clear(); blockers.Clear();
        }
        Camera thumbnailCamera;
        readonly List<SlotCapture> captures = new List<SlotCapture>();
        sealed class SlotCapture
        {
            internal RenderTexture Texture;
            internal UnityEngine.Rendering.AsyncGPUReadbackRequest Request;
            internal ThumbnailRequest Output;
            internal bool Complete;
        }
        /// <summary>Composition binds the existing rig camera at CAM-004; no camera lookup or second camera.</summary>
        internal void SetThumbnailCamera(Camera camera) => thumbnailCamera = camera;
        public bool TryCaptureSlotThumbnail(out ThumbnailRequest thumbnail)
        {
            thumbnail = null;
            if (thumbnailCamera == null || CurrentScene == GameScene.BlueprintLab || !SystemInfo.supportsAsyncGPUReadback ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return false;
            var render = new UnityEngine.Rendering.RenderPipeline.StandardRequest();
            if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(thumbnailCamera, render)) return false;
            var capture = new SlotCapture { Texture = RenderTexture.GetTemporary(320, 180, 16, RenderTextureFormat.ARGB32),
                Output = new ThumbnailRequest { Key = "slot", Width = 320, Height = 180, Pending = true } };
            try
            {
                render.destination = capture.Texture;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(thumbnailCamera, render);
                captures.Add(capture);
                capture.Request = UnityEngine.Rendering.AsyncGPUReadback.Request(capture.Texture, 0, TextureFormat.RGBA32,
                    request => FinishCapture(capture, request));
                thumbnail = capture.Output;
                return true;
            }
            catch (Exception e)
            {
                capture.Output.Pending = false; captures.Remove(capture); RenderTexture.ReleaseTemporary(capture.Texture);
                GameLog.Warn(LogCategories.Flow, "Slot thumbnail unavailable: " + e.Message); return false;
            }
        }
        void FinishCapture(SlotCapture capture, UnityEngine.Rendering.AsyncGPUReadbackRequest request)
        {
            if (capture.Complete) return;
            capture.Complete = true;
            try { if (!request.hasError) capture.Output.Rgba32 = request.GetData<byte>().ToArray(); }
            finally
            {
                capture.Output.Pending = false;
                RenderTexture.ReleaseTemporary(capture.Texture); captures.Remove(capture);
            }
        }
        void DisposeThumbnailCaptures()
        {
            while (captures.Count > 0)
            {
                var capture = captures[captures.Count - 1]; capture.Request.WaitForCompletion();
                FinishCapture(capture, capture.Request);
            }
            thumbnailCamera = null;
        }
    }
}
