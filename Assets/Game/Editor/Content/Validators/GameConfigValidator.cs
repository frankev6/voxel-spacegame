using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;

namespace Game.Editor.Content.Validators
{
    /// <summary>FND-E003: exactly one GameConfigDefinition. FND-E001: once any ship blueprint exists, the start blueprint must be set
    /// (before that, the definition reports it as a warning; SPEC-01 open question 8). Implements SPEC-01 §4.2.</summary>
    public sealed class GameConfigValidator : IContentDatabaseValidator
    {
        public int Order => 100;

        public void Validate(ContentCatalog catalog, ValidationReport report)
        {
            var configs = catalog.All<GameConfigDefinition>();
            if (configs.Count != 1)
            {
                report.Error("FND-E003", configs.Count > 0 ? AssetDatabase.GetAssetPath(configs[1]) : "Assets/Game/Content/Config", null,
                    $"There must be exactly one GameConfigDefinition (config_game); found {configs.Count}.");
                return;
            }
            var config = configs[0];
            if (config.StartBlueprint == null && catalog.All<ShipBlueprintDefinition>().Count > 0)
                report.Error("FND-E001", AssetDatabase.GetAssetPath(config), "startBlueprint", "No start blueprint, but ship blueprints exist: set one for New Game.");
        }
    }
}
