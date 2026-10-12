using Game.Core;
using Game.Data.Definitions;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Vfx
{
    /// <summary>SPEC-63 §5.6 producer field meanings.</summary>
    public sealed class FxEventsTests
    {
        [Test] public void Helpers_FillFieldsPerTable()
        {
            var pos = new float3(1, 2, 3); var dir = new float3(0, 0, 1); var vel = new float3(4, 5, 6);
            var fx = new ContentHash(71);
            Check(FxEvents.Debris(pos, vel, 2, 7, dir), FxKind.Debris, pos, dir, vel, 2, 7, FxEventFlags.PaletteColor, default);
            Check(FxEvents.Hit(FxKind.HitKinetic, pos, dir, vel, 25, fx), FxKind.HitKinetic, pos, dir, vel, .5f, 0, 0, fx);
            Check(FxEvents.Hit(FxKind.HitEnergy, pos, dir, vel, 200, fx), FxKind.HitEnergy, pos, dir, vel, 3, 0, 0, fx);
            Check(FxEvents.Explosion(pos, 3, vel, fx), FxKind.ExplosionSmall, pos, new float3(0, 1, 0), vel, 3, 0, 0, fx);
            Check(FxEvents.Explosion(pos, 4, vel, fx), FxKind.ExplosionLarge, pos, new float3(0, 1, 0), vel, 4, 0, 0, fx);
            Check(FxEvents.ShieldRipple(pos, dir, vel, .7f), FxKind.ShieldRipple, pos, dir, vel, .7f, 0, 0, default);
            Check(FxEvents.Muzzle(pos, dir, vel, .2f, 0xFF112233, fx), FxKind.Muzzle, pos, dir, vel, .2f, 0xFF112233, 0, fx);
            Check(FxEvents.Warp(pos, dir, vel, 9), FxKind.Warp, pos, dir, vel, 9, 0, FxEventFlags.PlayerSource, default);
        }
        static void Check(FxEvent e, FxKind kind, float3 pos, float3 dir, float3 vel, float scale, uint color, FxEventFlags flags, ContentHash fx)
        {
            Assert.AreEqual(kind, e.Kind); Assert.AreEqual(pos, e.Pos); Assert.AreEqual(dir, e.Dir); Assert.AreEqual(vel, e.Vel);
            Assert.AreEqual(scale, e.Scale); Assert.AreEqual(color, e.Color); Assert.AreEqual(flags, e.Flags);
            Assert.AreEqual(fx.Value, e.Fx.Value); Assert.AreEqual(0, e.Reserved);
        }
    }
}
