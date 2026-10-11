using System;
using Game.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-005: Services (SPEC-02 §5.3, §7).</summary>
    public sealed class ServicesTests
    {
        interface IFoo { }
        sealed class Foo : IFoo { }
        sealed class Bar { }

        [SetUp] public void SetUp() => Services.Reset();
        [TearDown] public void TearDown() => Services.Reset();

        [Test]
        public void Register_Get_ReturnsInstance()
        {
            var foo = new Foo();
            Assert.AreSame(foo, Services.Register<IFoo>(foo));
            Assert.AreSame(foo, Services.Get<IFoo>());
            Assert.IsTrue(Services.IsRegistered<IFoo>());
            Assert.IsFalse(Services.IsRegistered<Foo>(), "interface and concrete type are separate slots");
            Services.Register(foo);
            Assert.AreSame(foo, Services.Get<Foo>()); Assert.AreEqual(2, Services.Count);
            Assert.Throws<ArgumentNullException>(() => Services.Register<Bar>(null));
        }

        [Test]
        public void Register_Twice_Throws()
        {
            var first = Services.Register(new Bar());
            var ex = Assert.Throws<InvalidOperationException>(() => Services.Register(new Bar()));
            StringAssert.Contains("Bar", ex.Message);
            Assert.AreSame(first, Services.Get<Bar>(), "the first instance stays");
            Assert.AreEqual(1, Services.Count);
        }

        [Test]
        public void Get_Missing_ThrowsWithTypeName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Services.Get<Bar>());
            Assert.AreEqual("Service Bar is not registered", ex.Message);
        }

        [Test]
        public void TryGet_Missing_False()
        {
            Assert.IsFalse(Services.TryGet<Bar>(out var b)); Assert.IsNull(b);
            var bar = Services.Register(new Bar());
            Assert.IsTrue(Services.TryGet<Bar>(out b)); Assert.AreSame(bar, b);
        }

        [Test]
        public void Unregister_OnlyMatchingInstance()
        {
            var bar = Services.Register(new Bar());
            Assert.IsFalse(Services.Unregister(new Bar()), "another instance");
            Assert.IsFalse(Services.Unregister<Bar>(null));
            Assert.AreEqual(1, Services.Count);
            Assert.IsTrue(Services.Unregister(bar));
            Assert.IsFalse(Services.IsRegistered<Bar>()); Assert.AreEqual(0, Services.Count);
            Assert.IsFalse(Services.Unregister(bar), "already gone");
            Services.Register(bar); Assert.AreEqual(1, Services.Count, "re-register after unregister");
        }

        [Test]
        public void Reset_ClearsAllSlots()
        {
            Services.Register(new Bar()); Services.Register<IFoo>(new Foo()); Services.Register(new Foo());
            Services.Reset();
            Assert.AreEqual(0, Services.Count);
            Assert.IsFalse(Services.IsRegistered<Bar>()); Assert.IsFalse(Services.IsRegistered<IFoo>()); Assert.IsFalse(Services.IsRegistered<Foo>());
            Services.Register(new Bar());
            Services.Reset();
            Assert.IsFalse(Services.IsRegistered<Bar>(), "a slot tracked again after a reset is cleared by the next reset");
        }

        [Test]
        public void TryGet_DoesNotAllocate()
        {
            Services.Register(new Bar());
            Services.TryGet<Bar>(out _); Services.TryGet<Foo>(out _);
            Assert.That(() => { for (int i = 0; i < 1000; i++) { Services.TryGet<Bar>(out _); Services.TryGet<Foo>(out _); } }, Is.Not.AllocatingGCMemory());
        }
    }
}
