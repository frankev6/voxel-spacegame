using System;
using Unity.Mathematics;

namespace Game.UI.Data
{
    /// <summary>Double-precision map pan/zoom contract (SPEC-62 §4.3).</summary>
    public struct MapViewTransform
    {
        public double2 Center;
        public double UnitsPerPixel;
        public double MinUnitsPerPixel, MaxUnitsPerPixel;
        public float2 ViewSize;
        public float2 MapToView(double2 p) => throw new NotImplementedException("CAM-010");
        public double2 ViewToMap(float2 v) => throw new NotImplementedException("CAM-010");
        public void ZoomAt(float2 viewPoint, float steps) => throw new NotImplementedException("CAM-010");
        public void Pan(float2 viewDelta) => throw new NotImplementedException("CAM-010");
        public void Frame(double2 min, double2 max, float paddingPx) => throw new NotImplementedException("CAM-010");
        public void ClampCenter(double2 min, double2 max) => throw new NotImplementedException("CAM-010");
    }
}
