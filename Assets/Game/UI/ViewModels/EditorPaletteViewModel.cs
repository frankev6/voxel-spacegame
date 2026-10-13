using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Game.UI.ViewModels
{
    /// <summary>Implements SPEC-51 §4.4 using SPEC-61 §4.2's bindable property and cached-notification rule.</summary>
    public sealed class EditorPaletteViewModel : ViewModelBase
    {
        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs CategoriesChanged = new BindablePropertyChangedEventArgs(nameof(Categories));
        [CreateProperty] public List<string> Categories { get; } = new List<string>(12);
        public void NotifyCategories() => Notify(CategoriesChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedCategoryChanged = new BindablePropertyChangedEventArgs(nameof(SelectedCategory));
        int selectedCategory;
        [CreateProperty] public int SelectedCategory { get => selectedCategory; set => Set(ref selectedCategory, value, SelectedCategoryChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SearchTextChanged = new BindablePropertyChangedEventArgs(nameof(SearchText));
        string searchText;
        [CreateProperty] public string SearchText { get => searchText; set => Set(ref searchText, value, SearchTextChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ItemsChanged = new BindablePropertyChangedEventArgs(nameof(Items));
        [CreateProperty] public List<EditorPaletteItemViewModel> Items { get; } = new List<EditorPaletteItemViewModel>(128);
        public void NotifyItems() => Notify(ItemsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedBlockIdChanged = new BindablePropertyChangedEventArgs(nameof(SelectedBlockId));
        string selectedBlockId;
        [CreateProperty] public string SelectedBlockId { get => selectedBlockId; set => Set(ref selectedBlockId, value, SelectedBlockIdChanged); }

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs ColorsChanged = new BindablePropertyChangedEventArgs(nameof(Colors));
        [CreateProperty] public List<EditorColorViewModel> Colors { get; } = new List<EditorColorViewModel>(16);
        public void NotifyColors() => Notify(ColorsChanged);

        [Game.Core.StaticStateOk("Readonly cached property-name notification; passed by readonly reference, contains no session state.")]
        static readonly BindablePropertyChangedEventArgs SelectedColorChanged = new BindablePropertyChangedEventArgs(nameof(SelectedColor));
        int selectedColor;
        [CreateProperty] public int SelectedColor { get => selectedColor; set => Set(ref selectedColor, value, SelectedColorChanged); }

        public EditorPaletteViewModel()
        {
            for (int i = 0; i < 16; i++) Colors.Add(new EditorColorViewModel { Index = i });
        }
    }
}
