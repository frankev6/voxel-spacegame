using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. SYS-008 supplies docking state and managed notifications.</summary>
    public sealed class DockingService
    {
        public bool IsDocked => throw new System.NotImplementedException("SYS-008");
        public BodyId DockedBody => throw new System.NotImplementedException("SYS-008");
        public void Dock(BodyId body) => throw new System.NotImplementedException("SYS-008");
        public void Undock() => throw new System.NotImplementedException("SYS-008");
        public void RestoreDocked(BodyId body) => throw new System.NotImplementedException("SYS-009");
    }
}
