#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Game.App.Session;
using Game.Core;
using Game.Core.Net;
using Game.Net.Protocol;
using Game.Net.Transport;

namespace Game.App.DevRuntime.Commands
{
    /// <summary>Co-op spike controls (SPEC-90 §6): host, join, leave, status, network simulation and the cell hash check.</summary>
    public sealed class NetDevCommand : IDevCommand
    {
        public string Name => "net";
        public string Usage => "net host [port] [ship=<blueprint>] | net join <ip> [port] | net leave | net status | net sim <latencyMs> <jitterMs> <lossPct> | net hash | net guest <slot> \"<command>\" | net reply | net shot [file.png]";
        public string Summary => "Co-op spike: host or join a session, show traffic, simulate a bad network";
        readonly List<CoopPeer> peers = new();

        public DevCommandResult Execute(DevArgs args, DevContext ctx)
        {
            if (!ctx.TryGetService<CoopSession>(out var coop)) return DevCommandResult.Fail("co-op session unavailable");
            string action = args.Positional.Count > 0 ? args.Positional[0] : "status";
            switch (action)
            {
                case "host":
                {
                    ushort port = Port(args, 1);
                    coop.GuestBlueprint = args.Has("ship") ? new ContentId(args.GetString("ship", "")) : ContentId.None;
                    return coop.Host(port) ? DevCommandResult.Ok(coop.StatusText + ". Guests: net join <this machine's IP> " + port) : DevCommandResult.Fail(coop.StatusText);
                }
                case "join":
                {
                    if (args.Positional.Count < 2) return DevCommandResult.Fail(Usage);
                    return coop.Join(args.Positional[1], Port(args, 2)) ? DevCommandResult.Ok(coop.StatusText) : DevCommandResult.Fail(coop.StatusText);
                }
                case "leave": coop.Leave("net leave"); return DevCommandResult.Ok(coop.StatusText);
                case "status": return DevCommandResult.Ok(Status(coop));
                case "sim":
                {
                    if (coop.Net == null) return DevCommandResult.Fail("no session");
                    if (args.Positional.Count < 4 || !uint.TryParse(args.Positional[1], out uint latency) || !uint.TryParse(args.Positional[2], out uint jitter)
                        || !float.TryParse(args.Positional[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float loss))
                        return DevCommandResult.Fail(Usage);
                    coop.Net.Transport.SetSimulator(latency, jitter, loss);
                    return DevCommandResult.Ok("simulating +" + latency + " ms, ±" + jitter + " ms jitter, " + loss + "% loss on this machine's traffic");
                }
                case "guest":
                {
                    if (args.Positional.Count < 3 || !byte.TryParse(args.Positional[1], out byte slot)) return DevCommandResult.Fail("net guest <slot> \"<command line>\"");
                    string line = string.Join(" ", System.Linq.Enumerable.Skip(args.Positional, 2));
                    return coop.SendGuestCommand(slot, line, out uint id) ? DevCommandResult.Ok("sent #" + id + " to slot " + slot + "; read it with net reply")
                        : DevCommandResult.Fail("no guest in slot " + slot);
                }
                case "bring":
                {
                    if (args.Positional.Count < 2 || !byte.TryParse(args.Positional[1], out byte who)) return DevCommandResult.Fail("net bring <slot>");
                    return coop.BringGuest(who) ? DevCommandResult.Ok("slot " + who + " moved beside you") : DevCommandResult.Fail("no ship for slot " + who);
                }
                case "reply":
                    return DevCommandResult.Ok(coop.GuestReplies.Count == 0 ? "no replies yet" : string.Join("\n", coop.GuestReplies));
                case "shot":
                {
                    string path = args.Positional.Count > 1 ? args.Positional[1] : "coop-shot.png";
                    UnityEngine.ScreenCapture.CaptureScreenshot(path);
                    return DevCommandResult.Ok("screenshot requested: " + System.IO.Path.GetFullPath(path));
                }
                case "hash":
                {
                    if (coop.Net == null) return DevCommandResult.Fail("no session");
                    if (coop.Role == SimRoleKind.Host) { coop.Net.HashRequested = true; return DevCommandResult.Ok("hash report requested; guests log the result"); }
                    return DevCommandResult.Ok(coop.Net.Guest?.LastHashReport ?? "no guest replicator");
                }
            }
            return DevCommandResult.Fail(Usage);
        }

        static ushort Port(DevArgs args, int index)
            => args.Positional.Count > index && ushort.TryParse(args.Positional[index], out ushort port) ? port : NetProtocol.DefaultPort;

        string Status(CoopSession coop)
        {
            var text = new StringBuilder();
            text.Append("state ").Append(coop.State).Append(", role ").Append(coop.Role).Append(": ").Append(coop.StatusText);
            var net = coop.Net;
            if (net == null) return text.ToString();
            var s = net.Transport.Stats;
            coop.GetPeers(peers);
            foreach (var peer in peers)
                text.Append("\n  slot ").Append(peer.Slot).Append(' ').Append(peer.Name).Append(": rtt ").Append(peer.RttMs.ToString("0")).Append(" ms").Append(peer.InWorld ? ", in world" : "");
            text.Append("\n  out ").Append(s.OutKbps(NetChannel.Reliable).ToString("0")).Append(" kbps reliable, ").Append(s.OutKbps(NetChannel.Unreliable).ToString("0")).Append(" kbps unreliable");
            text.Append("\n  in  ").Append(s.InKbps(NetChannel.Reliable).ToString("0")).Append(" kbps reliable, ").Append(s.InKbps(NetChannel.Unreliable).ToString("0")).Append(" kbps unreliable");
            text.Append("\n  totals out ").Append((s.TotalOut(NetChannel.Reliable) + s.TotalOut(NetChannel.Unreliable)) / 1024).Append(" KB, in ")
                .Append((s.TotalIn(NetChannel.Reliable) + s.TotalIn(NetChannel.Unreliable)) / 1024).Append(" KB; peak reliable backlog ").Append(s.PeakReliableBacklog / 1024).Append(" KB");
            if (net.Host != null) text.Append("\n  host: ").Append(net.Host.TrackedGrids).Append(" grids, ").Append(net.Host.LiveChunks).Append(" chunks tracked").Append(net.DescribeCommands());
            if (net.Guest != null)
                text.Append("\n  guest: ").Append(net.Guest.GridCount).Append(" grids, ").Append(net.Guest.ChunkCount).Append(" chunks, ").Append(net.Guest.PendingMessages)
                    .Append(" queued, snapshot age ").Append(net.Guest.SnapshotAgeMs.ToString("0")).Append(" ms, desyncs ").Append(s.Desyncs)
                    .Append(", dropped snapshots ").Append(s.DroppedSnapshots).Append("\n  ").Append(net.Guest.LastHashReport)
                    .Append("\n  prediction: ").Append(net.Guest.Predictor.Corrections).Append(" corrections, error ").Append(net.Guest.Predictor.LastErrorM.ToString("0.00"))
                    .Append(" m (peak ").Append(net.Guest.Predictor.PeakErrorM.ToString("0.00")).Append("), snaps ").Append(net.Guest.Predictor.Snaps)
                    .Append(", ack ").Append(net.Guest.Predictor.LastAck).Append(" of ").Append(net.TickSequence);
            return text.ToString();
        }
    }
}
#endif
