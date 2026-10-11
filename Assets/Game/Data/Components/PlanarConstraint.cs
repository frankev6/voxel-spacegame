namespace Game.Data.Components
{
    /// <summary>Tag: the root stays on the flight plane Y = 0 and rotates about Y only (ADR-0003). Read by SPEC-13; enforced by
    /// PlanarConstraintSystem (SPEC-20) and SPEC-12's planar inertia. Implements SPEC-20 §4.2.</summary>
    public struct PlanarConstraint : Unity.Entities.IComponentData { }
}
