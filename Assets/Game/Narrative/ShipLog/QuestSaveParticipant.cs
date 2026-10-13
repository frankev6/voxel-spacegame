namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §4.6 QUST version-1 contract; capture and restore follow in QST-007.</summary>
    public sealed class QuestSaveParticipant : Game.Core.Save.ISaveParticipant
    {
        public string Key => "QUST";
        public int Version => 1;
        // TODO(QST-007): implement the version-1 QUST format and raw preservation.
        public void Capture(Game.Core.Save.SaveWriter writer) => throw new System.NotImplementedException("QST-007");
        public void Restore(Game.Core.Save.SaveReader reader, int fromVersion) => throw new System.NotImplementedException("QST-007");
    }
}
