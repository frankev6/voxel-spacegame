#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Net;
using Game.Net.Protocol;
using Game.Net.Transport;

namespace Game.App.Session
{
    /// <summary>Co-op spike dev tooling (SPEC-90 NET-002/010): command-line auto host/join for standalone players, and dev commands the
    /// host runs on a guest (`net guest <slot> "<command>"`), so one terminal drives every instance.</summary>
    public sealed partial class CoopSession
    {
        readonly NetWriter devWriter = new(256);
        readonly List<string> guestReplies = new();
        uint devSequence;
        string autoJoin, autoHostStage;
        ushort autoPort = NetProtocol.DefaultPort;
        bool argsRead;
        double autoAt;

        public IReadOnlyList<string> GuestReplies => guestReplies;

        partial void BindDev()
        {
            if (net == null) return;
            net.Transport.Register(NetMessage.DevCommand, (peer, r) =>
            {
                uint id = r.U32(); string line = r.Str();
                string reply;
                if (!Game.Core.Services.TryGet<DevRuntimeHost>(out var host)) reply = "Failed: no dev runtime";
                else
                {
                    var result = host.Commands.Execute(line, host.Context);
                    reply = result.Kind + ": " + result.Text;
                }
                devWriter.Clear(); devWriter.U32(id); devWriter.Str(reply.Length > 8000 ? reply.Substring(0, 8000) : reply);
                net.Transport.SendReliable(peer, NetMessage.DevResult, devWriter);
            });
            net.Transport.Register(NetMessage.DevResult, (peer, r) =>
            {
                uint id = r.U32(); string text = r.Str();
                guestReplies.Add("[slot " + peer.Slot + " #" + id + "] " + text);
                if (guestReplies.Count > 20) guestReplies.RemoveAt(0);
                GameLog.Info(NetTransport.Category, "guest " + peer.Slot + " replied #" + id + ": " + (text.Length > 300 ? text.Substring(0, 300) + "…" : text));
            });
        }

        /// <summary>Host: runs <paramref name="line"/> on the guest in <paramref name="slot"/>; the reply arrives in <see cref="GuestReplies"/>.</summary>
        public bool SendGuestCommand(byte slot, string line, out uint id)
        {
            id = 0;
            if (net == null || net.Role != SimRoleKind.Host) return false;
            foreach (var peer in net.Transport.Peers)
            {
                if (peer.Slot != slot) continue;
                id = ++devSequence;
                devWriter.Clear(); devWriter.U32(id); devWriter.Str(line);
                net.Transport.SendReliable(peer, NetMessage.DevCommand, devWriter);
                return true;
            }
            return false;
        }

        partial void TickDev()
        {
            if (!argsRead) ReadArgs();
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < autoAt) return;
            if (autoJoin != null && net == null && session.CurrentScene == GameScene.MainMenu && !session.IsTransitioning)
            {
                string address = autoJoin; autoJoin = null;
                GameLog.Info(NetTransport.Category, "auto-join " + address + ":" + autoPort);
                Join(address, autoPort);
            }
            if (autoHostStage == "playground" && (session.CurrentScene == GameScene.MainMenu || session.CurrentScene == GameScene.World) && !session.IsTransitioning
                && Game.Core.Services.TryGet<DevRuntimeHost>(out var dev))
            {
                autoHostStage = "host"; autoAt = now + 2;
                dev.Commands.Execute("playground", dev.Context);
            }
            else if (autoHostStage == "host" && session.CurrentScene == GameScene.World && session.IsSandbox && !session.IsTransitioning
                     && session.PlayerSpawner != null && session.PlayerSpawner.TryGetRoot(out _))
            {
                autoHostStage = null;
                GameLog.Info(NetTransport.Category, "auto-host on " + autoPort + ": " + (Host(autoPort) ? "ok" : StatusText));
            }
        }

        void ReadArgs()
        {
            argsRead = true;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-coop-join": if (next != null) { autoJoin = next; i++; } break;
                    case "-coop-host": autoHostStage = "playground"; break;
                    case "-coop-port": if (next != null && ushort.TryParse(next, out var port)) { autoPort = port; i++; } break;
                    case "-coop-name": if (next != null) { PlayerName = next; i++; } break;
                }
            }
            autoAt = UnityEngine.Time.realtimeSinceStartupAsDouble + 1;
        }
    }
}
#endif
