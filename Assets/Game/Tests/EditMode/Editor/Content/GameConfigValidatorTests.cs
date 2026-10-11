using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content.Validators;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>FND-005: config_game rules (SPEC-01 §4.2; FND-E001 escalation per SPEC-01 open question 8).</summary>
    public sealed class GameConfigValidatorTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        static List<string> Run(params ContentDefinition[] defs)
        {
            var report = new ValidationReport("test");
            var catalog = ContentCatalog.Build(defs.ToList(), report);
            var validatorReport = new ValidationReport("validator");
            new GameConfigValidator().Validate(catalog, validatorReport);
            return validatorReport.Entries.Where(e => e.Severity == ValidationSeverity.Error).Select(e => e.Code).ToList();
        }

        [Test]
        public void ExactlyOneConfig()
        {
            CollectionAssert.AreEqual(new[] { "FND-E003" }, Run(Item("item_missing")));
            CollectionAssert.IsEmpty(Run(Item("item_missing"), Create<GameConfigDefinition>("config_game")));
            CollectionAssert.AreEqual(new[] { "FND-E003" }, Run(Create<GameConfigDefinition>("config_game"), Create<GameConfigDefinition>("config_game_b")));
        }

        [Test]
        public void MissingStartBlueprint_WarningUntilBlueprintsExist()
        {
            var config = Create<GameConfigDefinition>("config_game");
            var own = Validate(config);
            Assert.AreEqual(ValidationSeverity.Warning, own.Entries.Single(e => e.Code == "FND-E001").Severity);
            CollectionAssert.IsEmpty(Run(config), "no blueprints yet: not an error");
            CollectionAssert.AreEqual(new[] { "FND-E001" }, Run(config, Create<ShipBlueprintDefinition>("ship_test")), "a blueprint exists: error");
        }

        [Test]
        public void Ranges()
        {
            var config = Create<GameConfigDefinition>("config_game", so => { Set(so, "sceneReadyTimeoutSeconds", 2f); Set(so, "persistentIdRefillThreshold", 9000); });
            var codes = Validate(config).Entries.Where(e => e.Severity == ValidationSeverity.Error).Select(e => e.Field).ToList();
            CollectionAssert.AreEquivalent(new[] { "sceneReadyTimeoutSeconds", "persistentIdRefillThreshold" }, codes);
        }
    }
}
