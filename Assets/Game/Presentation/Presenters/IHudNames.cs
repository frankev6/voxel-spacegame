using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Travel;
using Game.Presentation.Components;

namespace Game.Presentation.Presenters
{
    public readonly struct HudInteractionText
    {
        public readonly string Name, Detail;
        public readonly bool Blocked;
        public HudInteractionText(string name, string detail, bool blocked)
        { Name = name; Detail = detail; Blocked = blocked; }
    }
    public readonly struct HudRouteText
    {
        public readonly bool Visible, Blocked;
        public readonly string Text;
        public HudRouteText(bool visible, bool blocked, string text)
        { Visible = visible; Blocked = blocked; Text = text; }
    }
    /// <summary>Managed, cached display queries over the owning content, galaxy and travel services.</summary>
    public interface IHudNames
    {
        string TargetName(ContentHash blueprint, byte faction);
        string SystemName(uint system);
        AbilityDefinition Ability(ContentHash ability);
        HudInteractionText Interaction(in HudSnapshot snapshot);
        HudRouteText Route { get; }
        bool TryGetNextHop(out NextHop hop);
    }
}
