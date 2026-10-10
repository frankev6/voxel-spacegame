namespace Game.Core
{
    /// <summary>Lets world input ignore clicks meant for UI. Implemented by SPEC-60 (registered by SPEC-01). Implements SPEC-02 §4.4.</summary>
    public interface IWorldPointerGate
    {
        bool IsWorldPointerAllowed(UnityEngine.Vector2 screenPositionBottomLeft);
    }
}
