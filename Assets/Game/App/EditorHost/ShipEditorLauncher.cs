using System;
using Game.App.Flow;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Economy;
using Game.Galaxy.Services;
using Game.Narrative.Dialog;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.UI.Data;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Entities;
using UnityEngine;

namespace Game.App.EditorHost
{
    /// <summary>Canonical editor entry and scene-owned host creation over the actual session owners.</summary>
    public sealed class ShipEditorLauncher : ISceneReady, IDisposable
    {
        readonly GameSession session;
        readonly SceneFlow flow;
        readonly World world;
        readonly DockingService docking;
        readonly EconomyService economy;
        readonly EditorHostServices services;
        DialogService waitingDialog;
        bool disposed, entering;

        public ShipEditorLauncher(GameSession session, SceneFlow flow, World world,
            DockingService docking, EconomyService economy, EditorHostServices services)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            services.Leave = Leave;
            flow.RegisterReady(this);
        }
        public int SceneReadyOrder => SceneHookOrder.Editor;
        public void OpenLab()
        {
            if (disposed || entering) return;
            if (session.CurrentScene != GameScene.MainMenu) { Notice("Open the Blueprint Lab from the main menu."); return; }
            Enter(new BlueprintLabArgs(BlueprintLabMode.Creative, 0, ContentId.None));
        }
        bool CanEdit(out string reason)
        {
            reason = "Cannot edit now";
            if (disposed || entering || !world.IsCreated || session.CurrentScene != GameScene.World || !session.HasActiveSession) return false;
            using var active = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameplayActive>());
            if (active.IsEmptyIgnoreFilter || session.PlayerSpawner == null || !session.PlayerSpawner.TryGetRoot(out var player)) return false;
            if (world.EntityManager.HasComponent<Hulk>(player)) { reason = "A destroyed ship cannot be edited"; return false; }
            if (!docking.IsDocked) { reason = "Dock at a station to edit"; return false; }
            if (session.Fleet.Flagship == null) return false;
            reason = null;
            return true;
        }
        public bool EditPlayerShip(out string reason)
        {
            if (!CanEdit(out reason)) { Notice(reason); return false; }
            if (Game.Core.Services.TryGet<DialogService>(out var dialog) && dialog.IsActive)
            {
                if (waitingDialog == null) { waitingDialog = dialog; dialog.Ended += DialogEnded; }
                return true;
            }
            services.Ui.Navigator.CloseAllAboveRoot();
            Enter(new BlueprintLabArgs(BlueprintLabMode.FleetShip, session.Fleet.Flagship.PersistentId, ContentId.None));
            return true;
        }
        async void DialogEnded(DialogEnded value)
        {
            ClearDialog();
            // Let the dialog owner finish its Ended callback and release its scope/modal first.
            await Awaitable.NextFrameAsync();
            if (!disposed) EditPlayerShip(out _);
        }
        void ClearDialog()
        {
            if (waitingDialog != null) waitingDialog.Ended -= DialogEnded;
            waitingDialog = null;
        }
        async void Enter(BlueprintLabArgs args)
        {
            entering = true;
            try
            {
                var result = await session.EnterBlueprintLabAsync(args);
                if (result != FlowResult.Ok && !disposed) Notice("Cannot open editor: " + result);
            }
            catch (Exception error) { GameLog.Exception("EDT", error, "Enter Blueprint Lab"); }
            finally { entering = false; }
        }
        async void Leave()
        {
            if (disposed || entering) return;
            entering = true;
            try
            {
                var result = await session.ExitBlueprintLabAsync();
                if (result != FlowResult.Ok && !disposed) Notice("Cannot leave editor: " + result);
            }
            catch (Exception error) { GameLog.Exception("EDT", error, "Exit Blueprint Lab"); }
            finally { entering = false; }
        }
        public void OnSceneReady(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.BlueprintLab || disposed) return;
            var lab = context.LabArgs;
            services.SetBlueprintName = null;
            if (lab.Mode == BlueprintLabMode.FleetShip)
            {
                var ship = session.Fleet.Find(lab.FleetShipId) ?? throw new InvalidOperationException("Editor fleet ship is missing.");
                ShipEditorHost.Create(new ShipEditorEntryArgs
                {
                    Options = new EditSessionOptions { TargetKind = EditTargetKind.FleetShip, Title = ship.Name },
                    Source = ship.Structure, PersistentId = ship.PersistentId, Inventory = economy.Inventory,
                    CommitTarget = new FleetCommitTarget(session.Fleet, ship.PersistentId)
                }, services);
                return;
            }
            if (services.Blueprints == null) throw new InvalidOperationException("Blueprint storage is unavailable.");
            var target = new BlueprintCommitTarget(services.Blueprints, "New blueprint");
            services.SetBlueprintName = name => target.Name = name;
            using var source = CreativeSource(lab.StartBlueprint);
            ShipEditorHost.Create(new ShipEditorEntryArgs
            {
                Options = new EditSessionOptions { Creative = true, TargetKind = EditTargetKind.NewBlueprint, Title = "New blueprint" },
                Source = source, CommitTarget = target
            }, services);
        }
        VoxelStructure CreativeSource(ContentId blueprint)
        {
            if (!string.IsNullOrEmpty(blueprint.Value))
            {
                if (!services.Blueprints.TryLoad(blueprint.Value, out var loaded, out var error)) throw new InvalidOperationException(error);
                return loaded;
            }
            var source = new VoxelStructure();
            try
            {
                var content = new VoxelContent { Blocks = services.Content.BlockDefs, Orientations = services.Content.Orientations };
                for (ushort i = 1; i < content.Blocks.Value.Blocks.Length; i++)
                {
                    ref var block = ref content.Blocks.Value.Blocks[i];
                    if (!block.IsCore) continue;
                    for (byte orientation = 0; orientation < 24; orientation++)
                        if ((block.AllowedOrientations & (1u << orientation)) != 0 &&
                            source.TryPlaceBlock(new BlockPlacement { Block = i, Orient = orientation }, content) == PlaceResult.Ok) return source;
                }
                throw new InvalidOperationException("No placeable core exists for the Blueprint Lab.");
            }
            catch { source.Dispose(); throw; }
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
        void Notice(string text) => services.Ui.Notifications.Post(new Notification { Label = text });
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearDialog();
            flow.UnregisterReady(this);
            services.Leave = null;
            services.SetBlueprintName = null;
        }
    }
}
