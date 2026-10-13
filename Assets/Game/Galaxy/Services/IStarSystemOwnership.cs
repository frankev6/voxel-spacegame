using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implemented by SPEC-33 (TerritoryService). Owner faction of a star system as an Allegiance faction index;
    /// 255 = no owner (0 is the player faction, SPEC-33 decision 1). GetOwnerColorToken returns "" for no owner. Implements SPEC-30 §4.8.</summary>
    public interface IStarSystemOwnership { byte GetOwnerFaction(StarSystemId id); string GetOwnerColorToken(StarSystemId id); }
}
