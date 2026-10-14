using System;
using Game.Core.Events;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. SYS-008 supplies docking state and managed notifications.</summary>
    public sealed class DockingService
    {
        public bool IsDocked => DockedBody.IsValid;
        public BodyId DockedBody { get; private set; }
        public void Dock(BodyId body)
        {
            if (!body.IsValid) throw new ArgumentException("Docking requires a valid body", nameof(body));
            if (DockedBody.Equals(body)) return;
            DockedBody = body;
            EventBus<DockStateChanged>.Publish(new DockStateChanged(true, body.System.Value, body.LocalKey));
        }
        public void Undock()
        {
            if (!IsDocked) return;
            var previous = DockedBody; DockedBody = default;
            EventBus<DockStateChanged>.Publish(new DockStateChanged(false, previous.System.Value, previous.LocalKey));
        }
        public void RestoreDocked(BodyId body) => throw new System.NotImplementedException("SYS-009");
    }
}
