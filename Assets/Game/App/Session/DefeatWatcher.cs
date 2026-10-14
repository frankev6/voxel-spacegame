using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.UI.Data;
using Game.UI.Services;
using UnityEngine;

namespace Game.App.Session
{
    /// <summary>SPEC-01 §5.9/FND-015: player defeat block, unscaled delay and real load/menu choices.</summary>
    internal sealed class DefeatWatcher : IDisposable
    {
        readonly GameSession session;
        readonly SaveService save;
        readonly UIRoot ui;
        readonly float delay;
        bool bound, disposed, waiting, showing;
        double elapsed;
        uint generation, operationGeneration;
        Awaitable<FlowResult> operation;
        internal DefeatWatcher(GameSession session, SaveService save, UIRoot ui, GameConfigDefinition config)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session)); this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui)); delay = config != null ? config.DefeatDelaySeconds : 3;
        }
        internal void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DefeatWatcher)); if (bound) return; bound = true;
            EventBus<ShipDestroyed>.Subscribe(Destroyed); EventBus<SessionReset>.Subscribe(Reset); EventBus<SceneTransitionStarted>.Subscribe(Transition);
        }
        internal void Unbind()
        {
            if (!bound) return; bound = false; generation++; waiting = showing = false;
            EventBus<ShipDestroyed>.Unsubscribe(Destroyed); EventBus<SessionReset>.Unsubscribe(Reset); EventBus<SceneTransitionStarted>.Unsubscribe(Transition);
        }
        bool InDefeatedWorld => bound && session.HasActiveSession && session.CurrentScene == GameScene.World && !session.IsTransitioning && session.PlayerDefeated;
        void Destroyed(ShipDestroyed value)
        {
            if (!bound || value.IsPlayer == 0 || !session.HasActiveSession || session.CurrentScene != GameScene.World || session.IsTransitioning || session.PlayerDefeated) return;
            session.PlayerDefeated = true; generation++; elapsed = 0; waiting = true; showing = false;
        }
        void Reset(SessionReset _) { generation++; waiting = showing = false; elapsed = 0; }
        void Transition(SceneTransitionStarted _) { generation++; waiting = showing = false; }
        internal void Tick(float unscaledDeltaTime)
        {
            if (disposed) return;
            if (!float.IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            if (operation != null)
            {
                var awaiter = operation.GetAwaiter(); if (!awaiter.IsCompleted) return;
                var pending = operation; operation = null;
                try
                {
                    var result = pending.GetAwaiter().GetResult();
                    if (result != FlowResult.Ok && operationGeneration == generation && InDefeatedWorld)
                    { ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to leave the defeated session: " + result }); waiting = true; elapsed = delay; }
                }
                catch (Exception e)
                {
                    if (operationGeneration == generation && InDefeatedWorld)
                    { ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Unable to leave the defeated session: " + e.Message }); waiting = true; elapsed = delay; }
                }
            }
            if (!waiting || showing || !InDefeatedWorld) return;
            elapsed += unscaledDeltaTime; if (elapsed < delay) return;
            // Preserve an existing confirmation/prompt; the defeat choice follows when that owner closes.
            if (ui.Confirm.IsOpen || ui.Prompt.IsOpen) return;
            Show();
        }
        bool LastSave(out SlotRef slot)
        {
            slot = default; var slots = save.ListSlots(save.ActiveProfile);
            if (save.HasCurrentSlot && save.CurrentSlot.Profile.Equals(save.ActiveProfile))
                foreach (var candidate in slots) if (candidate.Ref.Equals(save.CurrentSlot) && Readable(candidate)) { slot = candidate.Ref; return true; }
            SlotInfo latest = null;
            foreach (var candidate in slots)
            {
                if (candidate.Kind != SlotKind.Auto || !Readable(candidate)) continue;
                long stamp = candidate.Meta?.SavedUtc.Ticks ?? long.MinValue, previous = latest?.Meta?.SavedUtc.Ticks ?? long.MinValue;
                if (latest == null || stamp > previous || stamp == previous && StringComparer.Ordinal.Compare(candidate.Ref.Slot.Value, latest.Ref.Slot.Value) < 0) latest = candidate;
            }
            if (latest == null) return false; slot = latest.Ref; return true;
        }
        static bool Readable(SlotInfo slot) => slot.Status == SlotStatus.Ok || slot.Status == SlotStatus.MetaMissing || slot.Status == SlotStatus.BackupOnly;
        void Show()
        {
            bool canLoad = LastSave(out var target); uint version = generation; waiting = false; showing = true;
            ui.Confirm.Show(new ConfirmRequest { Title = "Ship destroyed", Body = "Your flagship was destroyed.", CancelIndex = -1, DefaultFocusIndex = canLoad ? 0 : 1,
                Options = new[] {
                    new ConfirmOption { Label = "Load last save", ElementName = "defeat-load-last-save", Variant = ButtonVariant.Primary, Enabled = canLoad, DisabledReason = canLoad ? "" : "No saved game is available" },
                    new ConfirmOption { Label = "Main menu", ElementName = "defeat-main-menu", Variant = ButtonVariant.Secondary, Enabled = true } } }, choice =>
            {
                showing = false; if (version != generation || !InDefeatedWorld) return;
                if (choice != 1 && !(choice == 0 && canLoad)) { waiting = true; return; }
                operationGeneration = generation;
                try { operation = choice == 0 ? session.LoadGameAsync(target.Slot.Value) : session.QuitToMainMenuAsync(false); }
                catch (Exception e) { ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = e.Message }); waiting = true; elapsed = delay; }
            });
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
