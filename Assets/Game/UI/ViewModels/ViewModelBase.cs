namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-60 §4.4. Shared UI contract.</summary>
    public abstract class ViewModelBase : UnityEngine.UIElements.INotifyBindablePropertyChanged,
                                          UnityEngine.UIElements.IDataSourceViewHashProvider
    {
        public event System.EventHandler<UnityEngine.UIElements.BindablePropertyChangedEventArgs> propertyChanged;
        long version;

        public long GetViewHashCode() => version;

        /// Returns true when the value changed. `args` is a static readonly instance declared next to the property.
        protected bool Set<T>(ref T field, T value, in UnityEngine.UIElements.BindablePropertyChangedEventArgs args)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            version++;
            propertyChanged?.Invoke(this, args);
            return true;
        }

        /// For collection properties whose contents changed in place.
        protected void Notify(in UnityEngine.UIElements.BindablePropertyChangedEventArgs args)
        { version++; propertyChanged?.Invoke(this, args); }
    }
}
