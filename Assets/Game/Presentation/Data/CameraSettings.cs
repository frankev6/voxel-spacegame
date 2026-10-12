namespace Game.Presentation.Data
{
    /// <summary>Player camera settings forwarded by Game.App (SPEC-62 §4.1).</summary>
    public struct CameraSettings
    {
        public float MouseSensitivity; // 0.1..3, default 1
        public bool InvertOrbitY;
        public CameraYawMode YawMode;
        public float ShakeScale; // 0..1, default 1
        public bool ReduceMotion;
    }
}
