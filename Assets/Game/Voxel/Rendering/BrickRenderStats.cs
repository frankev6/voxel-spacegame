using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class BrickRenderStats
    {
        public static BrickRenderTotals Gather(EntityManager em) => throw new System.NotImplementedException("REN-011");
    }
}
