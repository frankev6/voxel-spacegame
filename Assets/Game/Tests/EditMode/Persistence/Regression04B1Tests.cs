using System;
using System.IO;
using System.Linq;
using Game.Core.Events;
using Game.Data;
using Game.Data.Services;
using Game.Persistence.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>SPEC-05 PERS-010 / 04-B1: removed content produces placeholders and an explicit successful load.</summary>
    public sealed class Regression04B1Tests
    {
        [Test] public void ContentRemoved_LoadSucceedsWithWarnings()
        {
            string root=Path.Combine(Path.GetTempPath(),"Regression04B1_"+Guid.NewGuid().ToString("N"));EventHub.Reset();
            try
            {
                var database=AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset");Assert.IsNotNull(database);
                using var content=new ContentService(database);
                ContainerFixtureTests.Install(root,"missing_ids");
                using var service=SaveFixtureHarness.CreateService(root,content,ContainerFixtureTests.Registry());
                var a=new ContainerFixtureTests.FixtureParticipant("TSTA",content.Catalog);
                service.Register(a);service.Register(new ContainerFixtureTests.FixtureParticipant("TSTB"));
                int events=0;SaveOperationResult outcome=default;
                service.OperationCompleted+=result=>{events++;outcome=result;};
                var loaded=service.ReadNow(ContainerFixtureTests.Slot);Assert.IsTrue(loaded.Success,loaded.Message);
                var report=service.Restore(loaded.Save);
                Assert.AreSame(content.Catalog.MissingItem,a.Item);Assert.AreSame(content.Catalog.MissingBlock,a.Block);
                Assert.AreEqual(2,report.Entries.Count(e=>e.Code=="PERS-W001"));Assert.AreEqual(0,report.ErrorCount);
                Assert.AreEqual(1,events);Assert.IsTrue(outcome.IsLoad);Assert.IsTrue(outcome.Success);Assert.AreEqual(2,outcome.WarningCount);
                Assert.AreEqual(SaveErrorKind.None,outcome.Error);
            }
            finally{EventHub.Reset();if(Directory.Exists(root))Directory.Delete(root,true);}
        }
    }
}
