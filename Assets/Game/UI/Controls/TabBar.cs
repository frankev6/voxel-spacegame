using System;
using System.Collections.Generic;
using Game.UI.Data;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-60 §4.5: bounded horizontal scrolling and change-only tab selection.</summary>
    [UxmlElement]
    public partial class TabBar : VisualElement
    {
        readonly ScrollView scroll;
        Button[] tabs = Array.Empty<Button>();
        int selectedIndex = -1;
        public TabBar()
        {
            name = "tab-bar"; AddToClassList(UIStyleClasses.TabBar);
            scroll = new ScrollView(ScrollViewMode.Horizontal)
            {
                name = "tab-bar-scroll", horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            Add(scroll); RegisterCallback<AttachToPanelEvent>(OnAttach);
        }
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (value < -1 || value >= tabs.Length) throw new ArgumentOutOfRangeException(nameof(value));
                if (selectedIndex == value) return;
                if (selectedIndex >= 0 && selectedIndex < tabs.Length) tabs[selectedIndex].RemoveFromClassList(UIStyleClasses.IsSelected);
                selectedIndex = value;
                if (value >= 0) tabs[value].AddToClassList(UIStyleClasses.IsSelected);
                SelectionChanged?.Invoke(value);
            }
        }
        public event Action<int> SelectionChanged;
        public int TabCount => tabs.Length;
        public void SetTabs(IReadOnlyList<string> labels, IReadOnlyList<string> names)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (labels.Count != names.Count) throw new ArgumentException("Each tab requires a label and an element name.");
            var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Count; i++)
                if (string.IsNullOrWhiteSpace(names[i]) || !uniqueNames.Add(names[i])) throw new ArgumentException("Tab element names must be nonempty and unique.", nameof(names));
            var next = new Button[labels.Count];
            for (int i = 0; i < next.Length; i++)
            {
                int index = i;
                var tab = new Button(() => SelectedIndex = index) { name = names[i], text = labels[i] ?? string.Empty };
                tab.AddToClassList(UIStyleClasses.TabBarTab);
                tab.EnableInClassList(UIStyleClasses.IsSelected, i == selectedIndex);
                next[i] = tab;
            }
            scroll.contentContainer.Clear(); tabs = next;
            for (int i = 0; i < tabs.Length; i++) scroll.Add(tabs[i]);
            SelectedIndex = tabs.Length == 0 ? -1 : selectedIndex >= 0 && selectedIndex < tabs.Length ? selectedIndex : 0;
        }
        void OnAttach(AttachToPanelEvent evt) => scroll.name = name + "-scroll";
    }
}
