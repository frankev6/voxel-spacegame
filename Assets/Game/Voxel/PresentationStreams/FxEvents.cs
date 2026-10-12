using Game.Core;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    public static class FxEvents
    {
        public static FxEvent Debris(float3 cellCentre, float3 velocity, float cellSize, byte paletteColor, float3 kickDir) =>
            new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor, Pos = cellCentre, Dir = kickDir,
                          Vel = velocity, Scale = cellSize, Color = paletteColor };
        public static FxEvent Hit(FxKind kind, float3 point, float3 surfaceNormal, float3 targetVelocity, float damageAmount, ContentHash fx = default) =>
            new FxEvent { Kind = kind, Pos = point, Dir = surfaceNormal, Vel = targetVelocity,
                          Scale = math.clamp(damageAmount / 50f, 0.5f, 3f), Fx = fx };
        public static FxEvent Explosion(float3 centre, float radius, float3 velocity, ContentHash fx = default) =>
            new FxEvent { Kind = radius >= 4f ? FxKind.ExplosionLarge : FxKind.ExplosionSmall, Pos = centre,
                          Dir = new float3(0, 1, 0), Vel = velocity, Scale = radius, Fx = fx };
        public static FxEvent Muzzle(float3 muzzle, float3 fireDir, float3 shooterVelocity, float calibre, uint rgba8 = 0, ContentHash fx = default) =>
            new FxEvent { Kind = FxKind.Muzzle, Pos = muzzle, Dir = fireDir, Vel = shooterVelocity, Scale = calibre, Color = rgba8, Fx = fx };
        public static FxEvent ShieldRipple(float3 point, float3 outwardNormal, float3 shipVelocity, float strength01) =>
            new FxEvent { Kind = FxKind.ShieldRipple, Pos = point, Dir = outwardNormal, Vel = shipVelocity, Scale = math.saturate(strength01) };
        public static FxEvent Warp(float3 shipPos, float3 jumpDir, float3 shipVelocity, float shipRadius) =>
            new FxEvent { Kind = FxKind.Warp, Flags = FxEventFlags.PlayerSource, Pos = shipPos, Dir = jumpDir, Vel = shipVelocity, Scale = shipRadius };

        /// Main-thread enqueue for managed producers (SPEC-32 travel). Completes writers of the queue first.
        public static void EnqueueMainThread(EntityManager em, in FxEvent e)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<FxEventQueue>());
            if (q.IsEmpty) return;                       // no presentation in this world (tests)
            em.CompleteDependencyBeforeRW<FxEventQueue>();
            var stream = q.GetSingleton<FxEventQueue>();
            stream.Access.CompleteAccess();
            stream.Queue.Enqueue(e);
        }
    }
}
