using System;
using System.Reflection;
using Game.UI.ViewModels;
using NUnit.Framework;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode.Narrative
{
    /// <summary>DLG-001: every dialog property notifies once on change and collections notify in place (SPEC-41 §4.9).</summary>
    public sealed class DialogContractTests
    {
        [Test]
        public void ViewModels_RaisePropertyChangedOnce()
        {
            var texture = new Texture2D(2, 2);
            Sprite sprite = null;
            try
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
                var dialog = new DialogViewModel();
                ViewModelBase[] models = { new DialogLineViewModel(), new DialogOptionViewModel(), dialog };
                foreach (var model in models)
                    foreach (var property in model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!property.CanWrite || property.GetCustomAttribute<CreatePropertyAttribute>() == null) continue;
                        object value = property.PropertyType == typeof(string) ? "changed" :
                            property.PropertyType == typeof(bool) ? true :
                            property.PropertyType == typeof(int) ? 1 :
                            property.PropertyType == typeof(Sprite) ? sprite :
                            throw new InvalidOperationException("Uncovered dialog property type: " + property.PropertyType);
                        int notifications = 0;
                        object sender = null;
                        BindablePropertyChangedEventArgs received = default;
                        EventHandler<BindablePropertyChangedEventArgs> handler = (s, e) =>
                        { notifications++; sender = s; received = e; };
                        model.propertyChanged += handler;
                        try
                        {
                            long version = model.GetViewHashCode();
                            property.SetValue(model, value);
                            Assert.AreEqual(1, notifications, model.GetType().Name + "." + property.Name);
                            Assert.AreSame(model, sender);
                            Assert.AreEqual(new BindingId(property.Name), received.propertyName);
                            Assert.AreEqual(value, property.GetValue(model));
                            Assert.AreEqual(version + 1, model.GetViewHashCode());
                            property.SetValue(model, value);
                            Assert.AreEqual(1, notifications, "equal values must not notify twice: " + property.Name);
                            Assert.AreEqual(version + 1, model.GetViewHashCode());
                        }
                        finally { model.propertyChanged -= handler; }
                    }

                var lines = dialog.Lines;
                AssertCollectionNotification(dialog, nameof(dialog.Lines), () =>
                { lines.Add(new DialogLineViewModel { Text = "line" }); dialog.NotifyLinesChanged(); });
                Assert.AreSame(lines, dialog.Lines);
                Assert.AreEqual(1, lines.Count);
                var options = dialog.Options;
                AssertCollectionNotification(dialog, nameof(dialog.Options), () =>
                { options.Add(new DialogOptionViewModel { Text = "option" }); dialog.NotifyOptionsChanged(); });
                Assert.AreSame(options, dialog.Options);
                Assert.AreEqual(1, options.Count);
                AssertCollectionNotification(dialog, nameof(dialog.Options), dialog.NotifyOptions);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void AssertCollectionNotification(DialogViewModel model, string property, Action change)
        {
            int notifications = 0;
            BindablePropertyChangedEventArgs received = default;
            EventHandler<BindablePropertyChangedEventArgs> handler = (_, e) => { notifications++; received = e; };
            model.propertyChanged += handler;
            try
            {
                long version = model.GetViewHashCode();
                change();
                Assert.AreEqual(1, notifications);
                Assert.AreEqual(new BindingId(property), received.propertyName);
                Assert.AreEqual(version + 1, model.GetViewHashCode());
            }
            finally { model.propertyChanged -= handler; }
        }
    }
}
