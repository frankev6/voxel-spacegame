using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Narrative;
using Game.UI.Data;
using Game.UI.Services;

namespace Game.Presentation.Presenters
{
    /// <summary>SPEC-61 §5.4/HUD-010: maps actual owner events into the existing coalescing notification service.</summary>
    public sealed class HudNotificationPresenter : IDisposable
    {
        readonly NotificationService notifications;
        readonly ContentCatalog catalog;
        readonly Dictionary<ContentHash, (string Name, string Key)> items = new();
        bool bound, disposed;
        public HudNotificationPresenter(NotificationService notifications, ContentCatalog catalog)
        { this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(HudNotificationPresenter)); if (bound) return; bound = true;
            EventBus<ItemAcquired>.Subscribe(Item); EventBus<GameSaved>.Subscribe(Saved); EventBus<TravelNotice>.Subscribe(Travel);
            EventBus<EconomyNotice>.Subscribe(Economy); EventBus<InteractionUnhandled>.Subscribe(Unhandled);
        }
        public void Unbind()
        {
            if (!bound) return; bound = false;
            EventBus<ItemAcquired>.Unsubscribe(Item); EventBus<GameSaved>.Unsubscribe(Saved); EventBus<TravelNotice>.Unsubscribe(Travel);
            EventBus<EconomyNotice>.Unsubscribe(Economy); EventBus<InteractionUnhandled>.Unsubscribe(Unhandled);
        }
        void Item(ItemAcquired value)
        {
            if (!bound || value.Count <= 0) return;
            if (!items.TryGetValue(value.Item, out var text))
            {
                bool known = catalog.TryGet<ItemDefinition>(value.Item, out var definition);
                string id = known ? definition.IdString : value.Item.ToString();
                if (!known) definition = catalog.MissingItem;
                text = (definition != null ? definition.DisplayName : "Unknown item", "item:" + id); items.Add(value.Item, text);
            }
            notifications.Post(new Notification { Kind = NotificationKind.Positive, Label = text.Name, Amount = value.Count, CoalesceKey = text.Key });
        }
        void Saved(GameSaved _) { if (bound) Post(NotificationKind.Info, "Game saved"); }
        void Travel(TravelNotice value)
        {
            if (!bound) return;
            switch (value.Kind)
            {
                case TravelNoticeKind.JumpRefused: case TravelNoticeKind.JumpAborted: case TravelNoticeKind.RouteBlocked: Post(NotificationKind.Warning, value.Text); break;
                case TravelNoticeKind.DestinationReached: case TravelNoticeKind.RouteRestored: Post(NotificationKind.Info, value.Text); break;
            }
        }
        void Economy(EconomyNotice value)
        {
            if (!bound) return;
            switch (value.Kind)
            {
                case EconomyNoticeKind.CargoFull: case EconomyNoticeKind.Jettisoned: case EconomyNoticeKind.SalvageRefused: case EconomyNoticeKind.TradeRefused: Post(NotificationKind.Warning, value.Text); break;
                case EconomyNoticeKind.SalvageDone: case EconomyNoticeKind.SalvagePartial: Post(NotificationKind.Positive, value.Text); break;
            }
        }
        void Unhandled(InteractionUnhandled _) { if (bound) Post(NotificationKind.Info, "Nothing of interest"); }
        void Post(NotificationKind kind, string text) => notifications.Post(new Notification { Kind = kind, Label = text });
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; items.Clear(); }
    }
}
