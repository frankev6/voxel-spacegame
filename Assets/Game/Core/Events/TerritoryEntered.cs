namespace Game.Core.Events
{
    /// <summary>SPEC-33 §4.3 managed encounter/territory notification (ENC-007/014).</summary>
    public readonly struct TerritoryEntered
    {
        public readonly Game.Core.ContentHash Faction;
        public readonly uint StarSystemValue;
        public TerritoryEntered(Game.Core.ContentHash f, uint s)
        { Faction = f; StarSystemValue = s; }
    }
}
