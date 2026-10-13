using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.4 clue text, mystery membership and ship-log links.</summary>
    [ContentIdPrefix("clue_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Clue", fileName = "clue_new")]
    public class ClueDefinition : ContentDefinition
    {
        [SerializeField] private string title;
        [SerializeField, UnityEngine.TextArea(2, 8)] private string body;
        [SerializeField] private MysteryDefinition mystery;                 // the thread it belongs to (may be null)
        [SerializeField] private System.Collections.Generic.List<ClueDefinition> links = new();   // ship-log arrows

        public string Title => title;
        public string Body => body;
        public MysteryDefinition Mystery => mystery;
        public System.Collections.Generic.IReadOnlyList<ClueDefinition> Links => links;

        // TODO(QST-002): implement field validation and the specified logic usages.
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            throw new System.NotImplementedException("QST-002");
        }
    }
}
