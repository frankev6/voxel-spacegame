using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Binding tag: dead ship, still physical and damageable; AI, power and propulsion skip it.</summary>
    public struct Hulk : IComponentData { }
}
