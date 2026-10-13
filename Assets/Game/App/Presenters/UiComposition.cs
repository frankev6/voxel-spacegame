using System;
using Game.UI.Services;
using Game.UI.Views;

namespace Game.App.Presenters
{
    /// <summary>App-lifetime screen composition contract; HUD-002 owns construction and Boot wiring (SPEC-61 §6).</summary>
    public sealed class UiComposition : IRootScreenSet, IDisposable
    {
        // TODO(HUD-002): compose view models, views and presenters, register screens and bind session/root switching.
        public UiComposition(UIRoot ui) => throw new NotImplementedException("HUD-002");
        public UIScreen MainMenu => throw new NotImplementedException("HUD-002");
        public UIScreen FlightHud => throw new NotImplementedException("HUD-002");
        public UIScreen ShipEditor => throw new NotImplementedException("HUD-002");
        public UIScreen Loading => throw new NotImplementedException("HUD-002");
        public void Dispose() => throw new NotImplementedException("HUD-002");
    }
}
