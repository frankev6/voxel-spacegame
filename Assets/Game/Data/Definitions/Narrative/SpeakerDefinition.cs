namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-41 §4.8.</summary>
    [ContentIdPrefix("speaker_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Speaker", fileName = "speaker_new")]
    public sealed class SpeakerDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;            // "NORA"; unique; used for "NAME:" prefixes
        [UnityEngine.SerializeField] private UnityEngine.Sprite portrait;   // placeholder until SPEC-81; null allowed (no portrait)
        [UnityEngine.SerializeField] private string nameStyleClass;         // optional USS class from the theme, e.g. "speaker--nora"; "" = default
        public string DisplayName => displayName; public UnityEngine.Sprite Portrait => portrait; public string NameStyleClass => nameStyleClass;
    }
}
