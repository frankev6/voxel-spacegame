using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4/§5.11 optional M4 escort capture/place/restore seam, implemented by Game.App.</summary>
    public interface IJumpEscorts
    {
        int CaptureEscorts(float radiusM);
        void PlaceEscorts(double3 arrivalPositionM, float arrivalYawRad);
        void RestoreEscorts();
    }
}
