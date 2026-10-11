namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1.</summary>
    public enum PlaceResult : byte { Ok = 0, Occupied = 1, InvalidBlock = 2, OrientationNotAllowed = 3, PaletteFull = 4, InstancesFull = 5, OutOfRange = 6, ReadOnly = 7 }
}
