using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>Implements SPEC-60 §4.3. Shared UI contract.</summary>
    public abstract class UIScreen
    {
        public abstract string Key { get; }                                    // kebab-case, unique; root element name = "screen-" + Key
        public abstract Game.UI.Data.UILayer Layer { get; }                    // Hud, Screens or Modals only
        public virtual Game.Core.InputContext Context => Game.Core.InputContext.Menu;
        public virtual bool PausesSimulation => true;
        public virtual bool BlocksWorldPointer => true;
        public virtual bool HidesHud => false;                                 // true → layer-hud gets .is-hidden while in stack
        public virtual bool IsRoot => false;                                   // only installable through UINavigator.SetRoot

        public UnityEngine.UIElements.VisualElement Root { get; private set; }
        public Game.UI.Services.UINavigator Navigator { get; private set; }
        public bool IsBuilt => Root != null;
        public bool IsOpen { get; internal set; }

        /// Called once by UINavigator when the screen is first needed. Sets Root, then calls OnBuild().
        internal void Build(Game.UI.Services.UINavigator navigator, UnityEngine.UIElements.VisualElement root)
        {
            Navigator = navigator; Root = root;
            try { OnBuild(); }
            catch { Navigator = null; Root = null; throw; }
        }

        /// Query named elements, set Root.dataSource, register callbacks. Called exactly once.
        protected abstract void OnBuild();
        public virtual void OnPush(object args) { }
        public virtual void OnPop() { }
        /// Return true when the screen consumed Back (closed a sub-panel, cancelled a drag). false → navigator pops it (non-root).
        public virtual bool OnBack() => false;
        public virtual void OnCovered() { }
        public virtual void OnRevealed() { }
        /// Element focused after push/reveal; null = keep current focus.
        public virtual UnityEngine.UIElements.VisualElement DefaultFocus => null;

        protected void Close() => Navigator.Pop(this);

        /// Fail-loud query by name. Throws InvalidOperationException("screen-<key>: missing element '<name>'").
        protected T Q<T>(string name) where T : UnityEngine.UIElements.VisualElement
        {
            var e = Root.Q<T>(name);
            if (e == null) throw new System.InvalidOperationException("screen-" + Key + ": missing element '" + name + "'");
            return e;
        }
    }
}
