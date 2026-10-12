using System;
using Game.UI.ViewModels;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Game.Tests.EditMode.UI
{
    /// <summary>UI-006: SPEC-60 §4.4 equality, change notification, versioning and the requested allocation measurement.</summary>
    public sealed class ViewModelBaseTests
    {
        sealed class Model : ViewModelBase
        {
            static readonly BindablePropertyChangedEventArgs ValueChanged = new BindablePropertyChangedEventArgs(nameof(Value));
            int value;
            public int Value { get => value; set => Set(ref this.value, value, ValueChanged); }
        }
        [Test] public void Set_SameValue_NoEvent()
        {
            var model = new Model { Value = 5 }; int events = 0;
            model.propertyChanged += (_, _) => events++;
            long hash = model.GetViewHashCode(); model.Value = 5;
            Assert.AreEqual(0, events); Assert.AreEqual(hash, model.GetViewHashCode());
        }
        [Test] public void Set_NewValue_OneEventAndHashBump()
        {
            var model = new Model(); int events = 0; object sender = null;
            BindablePropertyChangedEventArgs received = default;
            model.propertyChanged += (s, e) => { events++; sender = s; received = e; };
            long hash = model.GetViewHashCode(); model.Value = 5;
            Assert.AreEqual(1, events); Assert.AreSame(model, sender);
            Assert.AreEqual(new BindingId(nameof(Model.Value)), received.propertyName);
            Assert.AreEqual(hash + 1, model.GetViewHashCode());
        }
        [Test] public void Set_TenThousandCalls_ZeroAlloc()
        {
            var model = new Model(); int events = 0;
            model.propertyChanged += (_, _) => events++;
            for (int i = 0; i < 100; i++) model.Value = i;
            events = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) model.Value = i;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(10000, events); Assert.AreEqual(0, allocated);
        }
    }
}
