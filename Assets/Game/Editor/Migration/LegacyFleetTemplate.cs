using System.Collections.Generic;

namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.7.</summary>
    public sealed class LegacyFleetTemplate
    {
        public string LegacyEntityId; public string SuggestedId;                     // §4.4 Encounter rows
        public string Allegiance;                                                   // "Enemy" | "Neutral"
        public float DetectionRadius, PatrolRadius, MovementSpeed;                  // legacy units
        public bool ChasesPlayer, UseFactionDoctrine, AutoInteractOnContact;
        public int DoctrineMinShips, DoctrineMaxShips;
        public IReadOnlyList<IReadOnlyList<string>> Formations;                     // v2 ship IDs, from the CombatEncounter(s) in its pool
        public string LegacyNarrativeEncounterId;                                    // npc_traveling_merchant → "event_merchant_shady"
    }
}
