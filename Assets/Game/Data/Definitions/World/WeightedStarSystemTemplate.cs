using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [System.Serializable] public struct WeightedStarSystemTemplate
    { public StarSystemTemplateDefinition template; public float weight; }
}
