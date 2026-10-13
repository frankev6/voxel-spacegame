using System;
using System.Reflection;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data.Logic
{
    internal sealed class LogicTestEnvironment : IDisposable
    {
        public readonly ContentCatalog Content;
        public readonly FactCatalog Facts;
        public readonly RefResolverRegistry Refs;
        public readonly LogicCompiler Compiler;
        public readonly LogicFunctionRegistry Functions = new LogicFunctionRegistry();
        public LogicTestEnvironment()
        {
            var definitions = FactCatalogTests.BaseContent();
            definitions.Add(Item("resource_fuel"));
            definitions.Add(FactCatalogTests.Facts("facts_compiler",
                new FactDeclaration { Name = "intro_awake", Kind = FactKind.Bool },
                new FactDeclaration { Name = "story_count", Kind = FactKind.Int }));
            Content = FactCatalogTests.Content(definitions);
            Refs = RefResolverRegistry.CreateDefault(Content);
            var report = new ValidationReport("compiler fixture");
            Facts = FactCatalog.Build(Content, Refs, report);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Compiler = new LogicCompiler(Facts, Refs, Functions);
        }
        public static T Source<T>(T wrapper, object source)
        {
            typeof(T).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wrapper, source);
            return wrapper;
        }
        public void Dispose() => DestroyAll();
    }
}
