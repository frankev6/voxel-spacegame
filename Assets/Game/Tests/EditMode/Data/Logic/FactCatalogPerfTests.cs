using System.Diagnostics;
using System.Reflection;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Validation;
using Game.Tests.Common.Perf;
using NUnit.Framework;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Performance")]
    public sealed class FactCatalogPerfTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();

        [Test] public void Build20k()
        {
            var declarations = new FactDeclaration[20000];
            for (int i = 0; i < declarations.Length; i++)
                declarations[i] = new FactDeclaration { Name = "perf_fact_" + i.ToString("D5"), Kind = FactKind.Bool };
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(FactCatalogTests.Facts("facts_perf", declarations));
            var content = FactCatalogTests.Content(definitions);
            var refs = RefResolverRegistry.CreateDefault(content);
            var report = new ValidationReport("initial compile");
            var initial = FactCatalog.Build(content, refs, report);
            var compiler = new Game.Data.Logic.Compiler.LogicCompiler(initial, refs, new LogicFunctionRegistry());
            var options = new Game.Data.Logic.Compiler.LogicCompileOptions(LogicUsage.BeatCondition, "perf", "condition");
            var fields = new CompiledCondition[2000];
            var source = typeof(CompiledCondition).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = new CompiledCondition();
                source.SetValue(fields[i], "$" + declarations[i].Name);
                Assert.IsTrue(compiler.Compile(fields[i], options, report), report.ToText());
            }
            var samples = new double[5];
            for (int run = -1; run < samples.Length; run++)
            {
                report = new ValidationReport("build and bind");
                var clock = Stopwatch.StartNew();
                var facts = FactCatalog.Build(content, refs, report);
                var binder = new LogicBinder(facts, refs, report);
                foreach (var field in fields) field.Bind(binder);
                clock.Stop();
                Assert.GreaterOrEqual(facts.Count, 20000);
                Assert.AreEqual(0, binder.FailedBindings);
                Assert.AreEqual(0, report.ErrorCount, report.ToText());
                if (run >= 0) samples[run] = clock.Elapsed.TotalMilliseconds;
            }
            PerfRun.Gate("Build20kAndBind2k", samples, 30);
        }
    }
}
