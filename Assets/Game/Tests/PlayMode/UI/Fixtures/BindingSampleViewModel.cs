using Game.UI.ViewModels;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI.Fixtures
{
    /// <summary>UI-006 test-only data source using SPEC-60 §4.4's property pattern.</summary>
    public sealed class BindingSampleViewModel : ViewModelBase
    {
        static readonly BindablePropertyChangedEventArgs HullChanged = new BindablePropertyChangedEventArgs(nameof(Hull));
        int hull = 100;
        internal int HullReads;
        [CreateProperty]
        public int Hull
        {
            get { HullReads++; return hull; }
            set => Set(ref hull, value, HullChanged);
        }
    }
}
