namespace Game.Data.Definitions
{
    /// <summary>Public narrative contract (SPEC-40 §4.2); later task implementations are explicitly labelled.</summary>
    [ContentIdPrefix("facts_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Narrative/Fact Catalog", fileName = "facts_new")]
    public sealed class FactCatalogDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private FactDeclaration[] facts = System.Array.Empty<FactDeclaration>();
        public System.Collections.Generic.IReadOnlyList<FactDeclaration> Facts => facts;
    }
}
