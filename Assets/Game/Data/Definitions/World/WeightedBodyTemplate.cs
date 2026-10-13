using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [System.Serializable] public struct WeightedBodyTemplate
    { public BodyTemplateDefinition template; public float weight; public int maxCount; }
}
