namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum EditTargetKind : byte
    {
        NewBlueprint = 0, Blueprint = 1, FleetShip = 2
    }
}
