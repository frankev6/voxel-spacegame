using System.Collections;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Services;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>The three GAL-012 acceptance checks use production boot/services; unimplemented owners fail normally.</summary>
    public sealed class GalaxyContentSmokeTests : GameFlowFixture
    {
        [UnityTest] public IEnumerator MainGalaxy_GeneratesAndValidates()
        {
            yield return NewGame(0x5EED0001UL);
            var galaxy = Service<GalaxyService>(); var catalog = Service<ContentService>().Catalog;
            var report = new ValidationReport("GAL-012 world definitions");
            foreach (var definition in catalog.All<BodyTemplateDefinition>().Cast<ContentDefinition>()
                .Concat(catalog.All<BodyVisualDefinition>()).Concat(catalog.All<AuthoredStarSystemDefinition>())
                .Concat(catalog.All<StarSystemTemplateDefinition>()).Concat(catalog.All<GalaxyGenerationDefinition>()))
                definition.Validate(report.For("content:" + definition.IdString), new ContentValidationContext("content:" + definition.IdString, false,
                    d => catalog.TryGet(d.Id, out var resolved) && ReferenceEquals(d, resolved), catalog));
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual("galaxy_main", galaxy.Definition.IdString);
            Assert.AreEqual(1, galaxy.GenerationCount); Assert.IsNotNull(galaxy.Galaxy);
            Assert.AreEqual(3, galaxy.Galaxy.StarSystems.Length); Assert.AreEqual(2, galaxy.Galaxy.Lanes.Length);
        }
        [UnityTest] public IEnumerator OpeningChapter_BodiesResolve()
        {
            yield return NewGame(0x5EED0001UL);
            var galaxy = Service<GalaxyService>();
            Assert.IsTrue(galaxy.TryResolveBody(new ContentId("starsys_ark_start"), "ark_station", out var station));
            Assert.IsTrue(galaxy.TryResolveBody(new ContentId("starsys_drift"), "drift_cache", out var cache));
            Assert.IsTrue(station.IsValid); Assert.IsTrue(cache.IsValid);
            Assert.AreEqual("starsys_ark_start/ark_station", galaxy.GetLocationKey(station));
            Assert.AreEqual("starsys_drift/drift_cache", galaxy.GetLocationKey(cache));
        }
        [UnityTest] public IEnumerator WhisperLane_GatedByFact()
        {
            yield return NewGame(0x5EED0001UL);
            var galaxy = Service<GalaxyService>(); var access = Service<ILaneAccess>(); var context = Service<ILogicContext>();
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_ark_start"), out var ark));
            Assert.IsTrue(galaxy.TryResolveStarSystem(new ContentId("starsys_whisper"), out var whisper));
            Assert.IsTrue(galaxy.Galaxy.TryFindLane(ark, whisper, out int index));
            Assert.IsFalse(access.IsLaneOpen(index)); Assert.AreEqual("The gate is dormant.", access.GetLockedMessage(index));
            var refs = RefResolverRegistry.CreateDefault(Service<ContentService>().Catalog);
            var report = new ValidationReport("Whisper unlock");
            var compiler = new LogicCompiler(context.Facts.Catalog, refs, new LogicFunctionRegistry());
            Assert.IsTrue(compiler.TryCompileCommand("set_fact gate_ark_start_active true", new LogicCompileOptions(LogicUsage.DialogCommand, "test:GAL-012", "unlock"), report, out var effect), report.ToText());
            effect.Bind(new LogicBinder(context.Facts.Catalog, refs, report));
            Assert.AreEqual(0, report.ErrorCount, report.ToText()); Assert.IsTrue(effect.CanApply(context)); effect.Apply(context);
            yield return WaitFrames(2); // bounded deferred event drain/watchers; no real-time delay or polling loop
            Assert.IsTrue(access.IsLaneOpen(index)); Assert.AreEqual("", access.GetLockedMessage(index));
        }
    }
}
