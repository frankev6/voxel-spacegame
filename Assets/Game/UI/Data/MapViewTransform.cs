using Unity.Mathematics;

namespace Game.UI.Data
{
    /// <summary>Implements SPEC-62 §4.3. Double-precision map pan/zoom, with +Y map up and panel pixels down.
    /// Configure positive ordered zoom limits and a positive UnitsPerPixel. Float panel coordinates limit round-trip precision (Q17).</summary>
    public struct MapViewTransform
    {
        public double2 Center;
        public double UnitsPerPixel;
        public double MinUnitsPerPixel, MaxUnitsPerPixel;
        public float2 ViewSize;

        public float2 MapToView(double2 p)
        {
            double2 offset = (p - Center) / UnitsPerPixel;
            return (float2)(new double2(offset.x, -offset.y) + (double2)ViewSize * .5);
        }

        public double2 ViewToMap(float2 v)
        {
            double2 offset = (double2)v - (double2)ViewSize * .5;
            return Center + new double2(offset.x, -offset.y) * UnitsPerPixel;
        }

        public void ZoomAt(float2 viewPoint, float steps)
        {
            double previousUnitsPerPixel = UnitsPerPixel;
            UnitsPerPixel = math.clamp(UnitsPerPixel * System.Math.Pow(.85, steps), MinUnitsPerPixel, MaxUnitsPerPixel);
            double2 offset = (double2)viewPoint - (double2)ViewSize * .5;
            // Compute the anchor displacement directly rather than subtracting two large absolute map positions.
            Center += new double2(offset.x, -offset.y) * (previousUnitsPerPixel - UnitsPerPixel);
        }

        public void Pan(float2 viewDelta)
            => Center -= new double2(viewDelta.x, -(double)viewDelta.y) * UnitsPerPixel;

        /// <summary>Fits ordered bounds into a panel with positive width/height remaining after padding.</summary>
        public void Frame(double2 min, double2 max, float paddingPx)
        {
            double2 size = max - min;
            double2 available = (double2)ViewSize - 2 * (double)paddingPx;
            Center = min + size * .5;
            UnitsPerPixel = math.clamp(math.cmax(size / available), MinUnitsPerPixel, MaxUnitsPerPixel);
        }

        public void ClampCenter(double2 min, double2 max) => Center = math.clamp(Center, min, max);
    }
}
