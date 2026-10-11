namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. Which gameplay root components a grid gets at conversion (ADR-0015). Add values at the end only.</summary>
    public enum GridProfile : byte { None = 0, PlayerShip = 1, NpcShip = 2, Station = 3, SmallCraft = 4, Dummy = 5 }
}
