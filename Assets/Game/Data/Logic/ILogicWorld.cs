namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ILogicWorld                        // implemented by Game.App.LogicWorldAdapter (§5.5)
    {
        int CurrentStarSystemIndex { get; }             // RefKind.StarSystem index, 0 = not an authored star system
        double UniversalTime { get; }
        int GetRelation(int factionIndex);              // -100..100
        void AdjustRelation(int factionIndex, int delta);
        int CurrentStarSystemOwnerFactionIndex { get; } // 0 = unowned
        void GiveShip(int blueprintIndex);
        void SpawnEncounter(int encounterIndex, string anchorTag, int count, in LogicScope scope);
        void StartCombat(int encounterIndex, in LogicScope scope);
        void RemoveBody(in LogicScope scope);
        void ReplaceBody(in LogicScope scope, int bodyTemplateIndex);
        void RequestLaneJump(int starSystemIndex);
        void OpenMerchant(int merchantIndex, in LogicScope scope, System.Action onClosed);
        void OpenShipEditor();
        void RepairPlayerShip(int percent);
        void DamagePlayerShip(int amount, in LogicScope scope);
        void PlaySfx(int audioCueIndex);
        void Notify(string text);
    }
}
