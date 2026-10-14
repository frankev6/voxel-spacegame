using System;
using System.Globalization;
using System.Reflection;
using Game.Data.Logic;
using Game.Data.Definitions;
using Game.Core.Events;
using Game.Narrative;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Narrative;
using NUnit.Framework;

namespace Game.Tests.EditMode.DevTools
{
    public sealed class NarrativeDebuggerWindowTests
    {
        [Test] public void EditFact_WritesWithDebuggerSource()
        {
            EventHub.Reset();
            try
            {
                var store = FactStoreTests.Create(new FactDeclaration { Name = "debug_value", Kind = FactKind.Int, Default = 2 });
                int slot = store.Catalog.SlotOf("debug_value");
                var type = Type.GetType("Game.DevTools.Editor.Narrative.NarrativeDebuggerWindow, Game.DevTools.Editor", true);
                var edit = type.GetMethod("EditFact", BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(edit);
                edit.Invoke(null, new object[] { store, slot, 7 });
                Assert.AreEqual(7, store.GetBySlot(slot));
                var trace = new LogicTraceEntry[1];
                Assert.AreEqual(1, store.Trace.CopyTo(trace));
                Assert.AreEqual(LogicTraceKind.FactWrite, trace[0].Kind);
                Assert.AreEqual(FactWriteSource.Debugger, trace[0].Source);
                Assert.AreEqual(slot, trace[0].Subject);
                Assert.AreEqual(2, trace[0].OldValue); Assert.AreEqual(7, trace[0].NewValue);
            }
            finally { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        }
        [Test] public void FormatTraceEntry_IsStable()
        {
            // Match existing DevTools tests: discover the public Editor entry point without changing asmdef contracts.
            var type = Type.GetType("Game.DevTools.Editor.Narrative.NarrativeDebuggerWindow, Game.DevTools.Editor", true);
            var format = type.GetMethod("FormatTraceEntry", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(format);
            var entry = new LogicTraceEntry { Frame = 99, UniversalTime = 1234.125, Kind = LogicTraceKind.FactWrite,
                Source = FactWriteSource.Quest, Subject = 17, OldValue = -2, NewValue = 5, ContextId = 42 };
            const string expected = "99 | 1234.125s | FactWrite | fact_name | -2 → 5 | Quest | ctx=000000000000002A";
            var previous = CultureInfo.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "fr-FR" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    Assert.AreEqual(expected, format.Invoke(null, new object[] { entry, "fact_name" }));
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
