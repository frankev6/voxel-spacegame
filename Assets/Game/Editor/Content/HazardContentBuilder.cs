using Game.Core;
using Game.Data.Definitions;
using Game.Data.Hazards;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>ADR-0119 hazard content built from code (own work): the gravity well zone (HAZ-015). Re-running rebuilds each asset in place.</summary>
    public static class HazardContentBuilder
    {
        [MenuItem("Game/Content/Build Hazard Content")]
        public static void Build()
        {
            // A moon-sized well: GM 720 000 m³/s² gives a 60 m/s circular orbit at 200 m; softened inside 40 m; reach 450 m.
            TrackBuilder.Zone("zone_gravity_moon", ZoneShape.Circle, new Vector2(450, 0), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Attract, A = 720000, B = 40,
                    Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the hazard content (ADR-0119).");
        }
    }
}
