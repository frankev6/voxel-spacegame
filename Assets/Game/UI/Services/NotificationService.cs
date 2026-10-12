namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §6. Contract shell; implementation follows in the owning UI task.</summary>
    public sealed class NotificationService
    {
        public void Post(in Game.UI.Data.Notification notification) => throw new System.NotImplementedException();
        public int VisibleCount { get => throw new System.NotImplementedException(); }
        public int QueuedCount { get => throw new System.NotImplementedException(); }
        public void ClearAll() => throw new System.NotImplementedException();                                                   // used on SessionReset by SPEC-61
    }
}
