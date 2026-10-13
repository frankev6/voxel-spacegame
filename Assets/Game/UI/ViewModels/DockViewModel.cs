using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Bindable screen data; cached notifications and app-lifetime identity (SPEC-61 §4.2).</summary>
    public sealed class DockViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs StationNameChanged = new BindablePropertyChangedEventArgs(nameof(StationName));
        string stationName;
        [CreateProperty] public string StationName { get => stationName; set => Set(ref stationName, value, StationNameChanged); }

        static readonly BindablePropertyChangedEventArgs CanTradeChanged = new BindablePropertyChangedEventArgs(nameof(CanTrade));
        bool canTrade;
        [CreateProperty] public bool CanTrade { get => canTrade; set => Set(ref canTrade, value, CanTradeChanged); }

        static readonly BindablePropertyChangedEventArgs TradeBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(TradeBlockedReason));
        string tradeBlockedReason;
        [CreateProperty] public string TradeBlockedReason { get => tradeBlockedReason; set => Set(ref tradeBlockedReason, value, TradeBlockedReasonChanged); }

        static readonly BindablePropertyChangedEventArgs CanEditShipChanged = new BindablePropertyChangedEventArgs(nameof(CanEditShip));
        bool canEditShip;
        [CreateProperty] public bool CanEditShip { get => canEditShip; set => Set(ref canEditShip, value, CanEditShipChanged); }

        static readonly BindablePropertyChangedEventArgs EditBlockedReasonChanged = new BindablePropertyChangedEventArgs(nameof(EditBlockedReason));
        string editBlockedReason;
        [CreateProperty] public string EditBlockedReason { get => editBlockedReason; set => Set(ref editBlockedReason, value, EditBlockedReasonChanged); }
    }
}
