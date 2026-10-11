using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class PhysicsLayers
    {
        // ---- Category bits (BelongsTo). Never renumber. New bits only through SPEC-12 open questions (bits 5..30 free). ----
        public const uint GridBit          = 1u << 0;   // every voxel grid: ship, station, asteroid, hulk
        public const uint ChunkBodyBit     = 1u << 1;   // pooled chunk bodies (SPEC-13)
        public const uint CelestialBit     = 1u << 2;   // non-voxel bodies with colliders: stars, planets, moons, gates (SPEC-31)
        public const uint SensorBit        = 1u << 3;   // trigger volumes: interaction and docking zones (SPEC-31)
        public const uint EditorPreviewBit = 1u << 4;   // grids spawned with GridSpawnFlags.EditorPreview: queries only, no contacts
        public const uint QueryBit         = 1u << 31;  // BelongsTo of every query filter
        public const uint AllBodyBits      = GridBit | ChunkBodyBit | CelestialBit | SensorBit | EditorPreviewBit;

        // ---- Body filters (on colliders) ----
        public static CollisionFilter Grid          => Make(GridBit,          GridBit | ChunkBodyBit | CelestialBit | SensorBit | QueryBit);
        public static CollisionFilter ChunkBody     => Make(ChunkBodyBit,     GridBit | ChunkBodyBit | CelestialBit | QueryBit);
        public static CollisionFilter Celestial     => Make(CelestialBit,     GridBit | ChunkBodyBit | QueryBit);
        public static CollisionFilter Sensor        => Make(SensorBit,        GridBit | QueryBit);
        public static CollisionFilter EditorPreview => Make(EditorPreviewBit, QueryBit);

        // ---- Query filters (CastRay, CastCollider, OverlapAabb, CalculateDistance) ----
        public static CollisionFilter ProjectileQuery      => Make(QueryBit, GridBit | CelestialBit);
        public static CollisionFilter BeamQuery            => Make(QueryBit, GridBit | CelestialBit);
        public static CollisionFilter ExplosionQuery       => Make(QueryBit, GridBit);
        public static CollisionFilter CameraOcclusionQuery => Make(QueryBit, GridBit);
        public static CollisionFilter PickingQuery         => Make(QueryBit, GridBit | CelestialBit);
        public static CollisionFilter InteractionQuery     => Make(QueryBit, GridBit | CelestialBit | SensorBit);
        public static CollisionFilter SpawnClearanceQuery  => Make(QueryBit, GridBit | ChunkBodyBit | CelestialBit);
        public static CollisionFilter EditorPickQuery      => Make(QueryBit, GridBit | EditorPreviewBit);
        public static CollisionFilter DebugAllQuery        => Make(QueryBit, AllBodyBits);

        // ---- Materials ----
        public static Material GridMaterial          => MakeMaterial(0.4f, 0.1f, CollisionResponsePolicy.CollideRaiseCollisionEvents);
        public static Material ProvisionalMaterial   => MakeMaterial(0.4f, 0.1f, CollisionResponsePolicy.None);
        public static Material ChunkMaterial         => MakeMaterial(0.3f, 0.1f, CollisionResponsePolicy.CollideRaiseCollisionEvents);
        public static Material CelestialMaterial     => MakeMaterial(0.5f, 0.0f, CollisionResponsePolicy.CollideRaiseCollisionEvents);
        public static Material SensorMaterial        => MakeMaterial(0.0f, 0.0f, CollisionResponsePolicy.RaiseTriggerEvents);
        public static Material EditorPreviewMaterial => MakeMaterial(0.4f, 0.1f, CollisionResponsePolicy.None);

        /// <summary>Tests and debug only.</summary>
        public static bool CanCollide(in CollisionFilter a, in CollisionFilter b) => CollisionFilter.IsCollisionEnabled(a, b);

        static CollisionFilter Make(uint belongsTo, uint collidesWith) =>
            new CollisionFilter { BelongsTo = belongsTo, CollidesWith = collidesWith, GroupIndex = 0 };

        static Material MakeMaterial(float friction, float restitution, CollisionResponsePolicy response)
        {
            var m = Material.Default;
            m.Friction = friction;
            m.Restitution = restitution;
            m.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;
            m.RestitutionCombinePolicy = Material.CombinePolicy.Maximum;
            m.CollisionResponse = response;
            return m;
        }
    }
}
