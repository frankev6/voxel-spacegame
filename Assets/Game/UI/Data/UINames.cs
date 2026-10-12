using System;
namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.7: stable element names, allocated only during view/pool setup.</summary>
    public static class UINames
    {
        public static string ScreenRoot(string key) => "screen-" + key;
        public static string Layer(UILayer layer) => layer switch
        {
            UILayer.World => "layer-world", UILayer.Hud => "layer-hud",
            UILayer.Screens => "layer-screens", UILayer.Modals => "layer-modals",
            UILayer.Toasts => "layer-toasts", UILayer.Tooltip => "layer-tooltip",
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
        public static string Item(string key, string collection, string id) => key + "-" + collection + "-" + id;
    }
}
