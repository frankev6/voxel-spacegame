using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>QA-001 NOCTURNE (DESIGN-GUIDE §8): four L-shaped corner marks inside a plate or modal panel. They are
    /// absolute, unpickable children, so they never change layout; styling lives in nocturne-skin.uss.</summary>
    public static class CornerBrackets
    {
        public const string Class = "corner-bracket";
        [Game.Core.StaticStateOk("constant class names, never written")] static readonly string[] Corners = { "corner-bracket--tl", "corner-bracket--tr", "corner-bracket--bl", "corner-bracket--br" };
        public static void Decorate(VisualElement element)
        {
            if (element == null || element.Q(className: Class) != null) return;
            foreach (string corner in Corners)
            {
                var mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList(Class); mark.AddToClassList(corner);
                element.Add(mark);
            }
        }
        /// <summary>Decorates every HUD plate and modal panel under <paramref name="root"/>.</summary>
        public static void DecorateAll(VisualElement root)
        {
            root?.Query(className: "hud-plate").ForEach(Decorate);
            root?.Query(className: "panel--modal").ForEach(Decorate);
        }
    }
}
