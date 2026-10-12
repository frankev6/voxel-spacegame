using System;
using Game.UI.Data;
using UnityEngine.UIElements;

namespace Game.UI.Controls
{
    /// <summary>SPEC-60 §4.5: binding text supplied by the input adapter, styled by the shared theme.</summary>
    [UxmlElement]
    public partial class KeyHint : Label
    {
        [UxmlAttribute] public string ActionPath { get; set; }
        public KeyHint() => AddToClassList(UIStyleClasses.KeyHint);
        public void Refresh(Func<string, string> bindingDisplay)
        {
            if (bindingDisplay == null) throw new ArgumentNullException(nameof(bindingDisplay));
            text = string.IsNullOrEmpty(ActionPath) ? string.Empty : bindingDisplay(ActionPath) ?? string.Empty;
        }
    }
}
