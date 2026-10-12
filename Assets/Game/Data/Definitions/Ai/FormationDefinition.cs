namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.4: FormationDefinition.</summary>
    [ContentIdPrefix("formation_")]
    public sealed class FormationDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private UnityEngine.Vector2[] slots;   // leader-local XZ offsets in metres (x right, y forward)

        public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2> Slots => slots;
    }
}
