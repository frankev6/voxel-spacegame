using System.Collections;
using Game.App.DevRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    public sealed class DevHostTests
    {
        DevRuntimeHost host;
        sealed class NeverFinishes : IDevOperation
        {
            public DevOperationStatus Status => DevOperationStatus.Running;
            public string Message => "waiting";
            public double TimeoutSeconds => .02;
            public void Tick(DevContext ctx) { }
        }
        [UnityTearDown] public IEnumerator TearDown() { host?.Dispose(); yield return null; }
        [UnityTest] public IEnumerator Operation_TimesOut()
        {
            host = DevRuntimeHost.Create(null);
            int id = host.AddOperation(new NeverFinishes());
            Assert.AreEqual(1, id); Assert.IsTrue(host.TryGetOperation(id, out var operation));
            double limit = Time.realtimeSinceStartupAsDouble + 5;
            while (operation.Status == DevOperationStatus.Running && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.AreEqual(DevOperationStatus.Failed, operation.Status); StringAssert.Contains("timeout after", operation.Message);
            Assert.AreEqual(2, host.AddOperation(new NeverFinishes()));
            host.Dispose(); Assert.IsTrue(host.TryGetOperation(2, out var second));
            Assert.AreEqual("play mode exited", second.Message);
        }
        [UnityTest] public IEnumerator LogCapture_CountsErrors()
        {
            host = DevRuntimeHost.Create(null);
            long before = host.Logs.Errors;
            LogAssert.Expect(LogType.Error, "dev capture error");
            Debug.LogError("dev capture error");
            Assert.AreEqual(before + 1, host.Logs.Errors);
            StringAssert.Contains("dev capture error", host.Logs.LastMessages[host.Logs.LastMessages.Length - 1]);
            host.Logs.enabled = false;
            LogAssert.Expect(LogType.Error, "after disable"); Debug.LogError("after disable");
            Assert.AreEqual(before + 1, host.Logs.Errors);
            yield return null;
        }
    }
}
