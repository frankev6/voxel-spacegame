using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Game.App.DevRuntime;
using Game.App.DevRuntime.ControlSheet;
using Game.App.DevRuntime.Scenarios;
using Game.Input;
using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.App
{
    /// <summary>SPEC-70 DEV-029: the control sheet's layout and its localhost server.</summary>
    public sealed class ControlSheetTests
    {
        sealed class FixedProbe : IScenarioProbe
        {
            public string Name => "test.value";
            public int ArgCount => 0;
            public ScenarioValue Read(DevContext ctx, ReadOnlySpan<ScenarioValue> args) => new ScenarioValue(ScenarioValueKind.Number, 42, false, null);
        }

        [Test] public void Layout_ListsEveryAction()
        {
            var input = new InputService(null);      // owns and disposes the real Controls the edit-mode way
            try
            {
                var asset = input.Controls.asset;
                string json = ControlSheetJson.Layout(asset, path => "K", 3);
                StringAssert.StartsWith("{\"version\":3,", json);
                int actions = 0;
                foreach (var map in asset.actionMaps)
                    foreach (var action in map.actions)
                    {
                        StringAssert.Contains("{\"path\":" + DevJsonWriter.Quote(map.name + "/" + action.name) + ",", json);
                        actions++;
                    }
                Assert.Greater(actions, 20, "the real Controls asset");
                StringAssert.Contains("{\"path\":\"Flight/Thrust\",\"name\":\"Thrust\",\"kind\":\"axis\",\"binding\":\"K\"}", json);
                StringAssert.Contains("{\"path\":\"Global/DevConsole\",\"name\":\"DevConsole\",\"kind\":\"button\",", json);
            }
            finally { input.Dispose(); }
        }

        [Test] public void Server_ServesPageAndState_ThenFreesPort()
        {
            string root = Path.Combine(Path.GetTempPath(), "controlsheet-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "index.html"), "<p>sheet page</p>");
            File.WriteAllText(Path.Combine(root, "notes.txt"), "not a served type");
            var input = new InputService(null);
            var sheet = new DevControlSheet(input, new ScenarioProbeRegistry(new FixedProbe()), null, root);
            try
            {
                int port = FreePort();
                Assert.IsTrue(sheet.TryOpen(port, 1, out string error), error);
                Assert.AreEqual("http://127.0.0.1:" + port + "/", sheet.Url);

                StringAssert.Contains("sheet page", Get(port, "/", out int status)); Assert.AreEqual(200, status);
                StringAssert.Contains("\"path\":\"Flight/Thrust\"", Get(port, "/layout", out status)); Assert.AreEqual(200, status);
                Get(port, "/missing.html", out status); Assert.AreEqual(404, status);
                Get(port, "/notes.txt", out status); Assert.AreEqual(404, status, "only page file types are served");
                Assert.IsNull(ControlSheetServer.ResolveFile(root, "/../secret.html"));
                Assert.IsNull(ControlSheetServer.ResolveFile(root, "/sub\\..\\x.html"));

                // The first poll asks for probes; the next build answers them.
                StringAssert.Contains("\"context\":\"None\"", Get(port, "/state?probes=test.value,nope", out status)); Assert.AreEqual(200, status);
                sheet.Tick(1000);
                string state = Get(port, "/state?probes=test.value,nope", out _);
                StringAssert.Contains("\"probes\":{\"test.value\":42,\"nope\":null}", state);
                StringAssert.Contains("\"active\":[\"Global\"]", state);

                sheet.Close();
                Assert.IsFalse(sheet.IsOpen);
                Assert.Throws<SocketException>(() => { using var client = new TcpClient(); client.Connect(IPAddress.Loopback, port); });
                Assert.IsTrue(sheet.TryOpen(port, 1, out error), "port reusable after close: " + error);
            }
            finally
            {
                sheet.Dispose(); input.Dispose();
                Directory.Delete(root, true);
            }
        }

        static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        [Test] public void FlightConfig_SetClampsAndResets()
        {
            // Each setter writes exactly the field its getter reads: a copy-paste slip would edit the wrong flight value.
            foreach (var field in DevFlightConfig.Fields)
            {
                var c = FlightConfig.Default;
                float target = Math.Abs(field.Get(c) - field.Min) > 1e-6f ? field.Min : field.Max;
                Assert.IsTrue(DevFlightConfig.TrySet(ref c, field.Name, target, out string error), error);
                Assert.AreEqual(target, field.Get(c), 1e-6f, field.Name);
                foreach (var other in DevFlightConfig.Fields)
                    if (other != field) Assert.AreEqual(other.Get(FlightConfig.Default), other.Get(c), other.Name + " changed with " + field.Name);
            }

            using var world = new World("ControlSheetFlight");
            var em = world.EntityManager;
            em.CreateSingleton(FlightConfig.Default);
            var flight = new DevFlightConfig();
            FlightConfig Live() { using var q = em.CreateEntityQuery(typeof(FlightConfig)); return q.GetSingleton<FlightConfig>(); }

            Assert.IsTrue(flight.TryApply(world, "MaxForwardSpeed", 80, false, out string e), e);
            Assert.IsTrue(flight.TryApply(world, "ReverseSpeedFraction", 7, false, out e), e);
            Assert.IsTrue(flight.TryApply(world, "AllocationSweeps", 3.6, false, out e), e);
            Assert.AreEqual(80f, Live().MaxForwardSpeed);
            Assert.AreEqual(1f, Live().ReverseSpeedFraction, "clamped to its range");
            Assert.AreEqual(4, Live().AllocationSweeps, "integer fields round");
            Assert.IsFalse(flight.TryApply(world, "MaxForwardSpeed", double.NaN, false, out e)); StringAssert.Contains("finite", e);
            Assert.IsFalse(flight.TryApply(world, "Nope", 1, false, out e)); StringAssert.Contains("unknown", e);

            Assert.IsTrue(flight.TryRead(world, out var current, out var boot));
            StringAssert.Contains("{\"name\":\"MaxForwardSpeed\",\"unit\":\"m/s\",\"min\":1,\"max\":1000,\"integer\":false,\"aiLags\":false,\"value\":80,\"boot\":120}",
                DevFlightConfig.Json(true, current, boot, null));

            Assert.IsTrue(flight.TryApply(world, "MaxForwardSpeed", 0, true, out e), e);
            Assert.AreEqual(120f, Live().MaxForwardSpeed); Assert.AreEqual(1f, Live().ReverseSpeedFraction, "one reset leaves the others");
            Assert.IsTrue(flight.TryApply(world, "", 0, true, out e), e);
            Assert.AreEqual(FlightConfig.Default.ReverseSpeedFraction, Live().ReverseSpeedFraction);
            Assert.AreEqual(FlightConfig.Default.AllocationSweeps, Live().AllocationSweeps);
        }

        [Test] public void Server_FlightPostNeedsHeader()
        {
            string root = Path.Combine(Path.GetTempPath(), "controlsheet-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "index.html"), "<p>sheet page</p>");
            var input = new InputService(null);
            var sheet = new DevControlSheet(input, null, null, root);     // no world: the change is refused with a reason
            try
            {
                int port = FreePort();
                Assert.IsTrue(sheet.TryOpen(port, 1, out string error), error);
                StringAssert.Contains("\"available\":false", Get(port, "/flight", out int status)); Assert.AreEqual(200, status);

                Send(port, "POST", "/flight?name=MaxForwardSpeed&value=80", null, out status);
                Assert.AreEqual(403, status, "a post without the header could come from any web page");
                Send(port, "POST", "/flight?name=MaxForwardSpeed&value=abc", "X-Control-Sheet: 1", out status);
                Assert.AreEqual(400, status);
                Send(port, "POST", "/flight?name=MaxForwardSpeed&value=80", "X-Control-Sheet: 1", out status);
                Assert.AreEqual(202, status);

                sheet.Tick(0);                                             // applies the queued change even before any poll
                StringAssert.Contains("\"error\":\"no FlightConfig: start a game first\"", Get(port, "/flight", out _));
            }
            finally
            {
                sheet.Dispose(); input.Dispose();
                Directory.Delete(root, true);
            }
        }

        static string Get(int port, string path, out int status) => Send(port, "GET", path, null, out status);

        // Raw HTTP over a socket: synchronous, so no async continuation waits on the blocked Editor main thread.
        static string Send(int port, string method, string path, string header, out int status)
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
            client.Connect(IPAddress.Loopback, port);
            using var stream = client.GetStream();
            byte[] request = Encoding.ASCII.GetBytes(method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:" + port + "\r\n" +
                (header != null ? header + "\r\n" : "") + (method == "POST" ? "Content-Length: 0\r\n" : "") + "Connection: close\r\n\r\n");
            stream.Write(request, 0, request.Length);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string response = reader.ReadToEnd();
            string[] statusLine = response.Substring(0, response.IndexOf('\r')).Split(' ');
            status = int.Parse(statusLine[1]);
            int body = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return body < 0 ? string.Empty : response.Substring(body + 4);
        }
    }
}
