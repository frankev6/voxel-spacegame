using System;
using Game.App.DevRuntime;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    /// <summary>DEV-001 contract checks (SPEC-70 §4).</summary>
    public sealed class DevContractTests
    {
        [Test]
        public void ResultFactories_SetKindAndText()
        {
            var ok = DevCommandResult.Ok("done");
            Assert.AreEqual(DevResultKind.Ok, ok.Kind); Assert.AreEqual("done", ok.Text); Assert.AreEqual(0, ok.OperationId);
            var fail = DevCommandResult.Fail("bad arg");
            Assert.AreEqual(DevResultKind.Failed, fail.Kind); Assert.AreEqual("bad arg", fail.Text); Assert.AreEqual(0, fail.OperationId);
            var pending = DevCommandResult.Pending(3, "loading");
            Assert.AreEqual(DevResultKind.Pending, pending.Kind); Assert.AreEqual("loading", pending.Text); Assert.AreEqual(3, pending.OperationId);
            Assert.AreEqual("", DevCommandResult.Ok(null).Text, "Text is never null");
            Assert.AreEqual("", DevCommandResult.Fail(null).Text);
            Assert.Throws<ArgumentOutOfRangeException>(() => DevCommandResult.Pending(0, "x"), "operation ids start at 1");
        }
    }
}
