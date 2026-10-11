namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5 (behaviour §5.9).</summary>
    public sealed class AutosaveScheduler
    {
        public void Update(float unscaledDeltaTime) { throw new System.NotImplementedException("PERS-009"); }       // called once per frame by Game.App (SPEC-01)
        public void Request(Game.Persistence.Data.AutosaveTrigger trigger) { throw new System.NotImplementedException("PERS-009"); }
        public void NotifySaved() { throw new System.NotImplementedException("PERS-009"); }                          // called by SaveService after any successful save
        public float SecondsUntilTimerSave => throw new System.NotImplementedException("PERS-009");         // diagnostics / HUD
        public bool HasPendingRequest => throw new System.NotImplementedException("PERS-009");
    }
}
