using Game.Core.Events;
using Game.Core.Net;
using Game.Net.Protocol;

namespace Game.App.Session
{
    /// <summary>ADR-0117 NET-017: player-facing notices go to the machine of the pilot they are for. The host's services publish an
    /// EconomyNotice naming a guest's pilot; it is sent to that guest, which publishes it for its own player, so the same HUD toast
    /// and salvage cue run there. Main thread.</summary>
    public sealed partial class CoopSession
    {
        readonly NetWriter noticeWriter = new();
        bool noticesBound;

        void BindNotices()
        {
            if (net == null) return;
            net.Transport.Register(NetMessage.Notice, (peer, r) =>
            {
                var kind = (EconomyNoticeKind)r.U8(); string text = r.Str();
                EventBus<EconomyNotice>.Publish(new EconomyNotice(kind, text));
            });
            if (!noticesBound) { EventBus<EconomyNotice>.Subscribe(ForwardNotice); noticesBound = true; }
        }

        void UnbindNotices()
        {
            if (noticesBound) { EventBus<EconomyNotice>.Unsubscribe(ForwardNotice); noticesBound = false; }
        }

        void ForwardNotice(EconomyNotice value)
        {
            if (value.Pilot == 0 || net == null || net.Role != SimRoleKind.Host) return;
            foreach (var pair in guestShips)
            {
                if (pair.Value != value.Pilot) continue;
                foreach (var peer in net.Transport.Peers)
                {
                    if (peer.Slot != pair.Key) continue;
                    noticeWriter.Clear(); noticeWriter.U8((byte)value.Kind); noticeWriter.Str(value.Text);
                    net.Transport.SendReliable(peer, NetMessage.Notice, noticeWriter);
                }
                return;
            }
        }
    }
}
