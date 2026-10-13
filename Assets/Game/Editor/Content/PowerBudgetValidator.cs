using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Power;
using Game.Voxel.Grid;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>Implements SPEC-22 §5.6. Discovered by ContentValidatorRunner; power balance is advisory content validation.</summary>
    public sealed class PowerBudgetValidator : IContentDatabaseValidator
    {
        public int Order => 220;

        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            // BlueprintLibrary decodes using the catalog, without requiring baked block/orientation blobs.
            using var blueprints = new BlueprintLibrary(catalog, default);
            PowerBudgetValidation.Validate(catalog, blueprints, report, blueprint => AssetDatabase.GetAssetPath(blueprint));
        }
    }
}
