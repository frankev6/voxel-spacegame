namespace Game.Core.Events
{
    /// <summary>SPEC-33 §4.3 managed encounter/territory notification (ENC-007/014).</summary>
    public readonly struct EncounterDefeated
    {
        public readonly uint InstanceId;
        public readonly Game.Core.ContentHash Encounter;
        public readonly uint StarSystemValue;
        public EncounterDefeated(uint instance, Game.Core.ContentHash encounter, uint starSystem)
        { InstanceId = instance; Encounter = encounter; StarSystemValue = starSystem; }
    }
}
