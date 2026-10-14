using System;
using Game.Core.Events;
using Game.Core;
using Game.Core.Flow;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. SYS-008 supplies docking state and managed notifications.</summary>
    public sealed class DockingService : ISceneReady
    {
        internal bool HasPendingRestore { get; private set; }
        internal Func<BodyId, bool> ValidateRestoredBody;
        public int SceneReadyOrder => SceneHookOrder.Ui;
        public bool IsDocked => DockedBody.IsValid;
        public BodyId DockedBody { get; private set; }
        public void Dock(BodyId body)
        {
            if (!body.IsValid) throw new ArgumentException("Docking requires a valid body", nameof(body));
            if (DockedBody.Equals(body) && !HasPendingRestore) return;
            HasPendingRestore = false;
            DockedBody = body;
            EventBus<DockStateChanged>.Publish(new DockStateChanged(true, body.System.Value, body.LocalKey));
        }
        public void Undock()
        {
            HasPendingRestore = false;
            if (!IsDocked) return;
            var previous = DockedBody; DockedBody = default;
            EventBus<DockStateChanged>.Publish(new DockStateChanged(false, previous.System.Value, previous.LocalKey));
        }
        public void RestoreDocked(BodyId body)
        {
            // No event while save participants restore; the World-ready hook announces validated docking.
            DockedBody = body;
            HasPendingRestore = body.IsValid;
        }
        public void OnSceneReady(in SceneReadyContext context)
        {
            if (context.Scene != GameScene.World || !HasPendingRestore) return;
            HasPendingRestore = false;
            if (ValidateRestoredBody == null || !ValidateRestoredBody(DockedBody))
            {
                GameLog.Warn("Galaxy", "Saved dock body is unavailable; restored player remains undocked.");
                DockedBody = default; return;
            }
            EventBus<DockStateChanged>.Publish(new DockStateChanged(true, DockedBody.System.Value, DockedBody.LocalKey));
        }
        public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
    }
}
