using Unity.Entities;

namespace Game.Data.Components
{
    /// <summary>ADR-0123: enabled while this ship flies in experimental 3D (off the plane, pitching). Present on player ships only,
    /// disabled by default. Only writer of the enabled bit: Flight3DModeSystem (SPEC-20 FLT-016). While enabled, the plane locks of
    /// <see cref="PlanarConstraint"/> and the planar inertia do not apply; <see cref="PlaneLock"/> answers that for every lock site.</summary>
    public struct Flight3D : IComponentData, IEnableableComponent { }

    /// <summary>ADR-0123: the one question every plane-lock site asks. Lookups are read-only.</summary>
    public static class PlaneLock
    {
        /// <summary>True while a body tagged with PlanarConstraint (<paramref name="tagged"/>) is not in experimental 3D flight.</summary>
        public static bool Holds(bool tagged, in ComponentLookup<Flight3D> flight3D, Entity entity)
            => tagged && !(flight3D.HasComponent(entity) && flight3D.IsComponentEnabled(entity));
    }
}
