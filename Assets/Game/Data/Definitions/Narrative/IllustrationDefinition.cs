namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-41 §4.8.</summary>
    [ContentIdPrefix("illustration_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Illustration", fileName = "illustration_new")]
    public sealed class IllustrationDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private UnityEngine.Sprite image;      // placeholder until SPEC-81
        public UnityEngine.Sprite Image => image;
    }
}
