using Game.Core;
using Game.Data.Services;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>RACE-008 / RACE-013 (owner: "for ships i actually like all options you made, lets create all 6"). Builds the six
    /// racers from ArtSource/Racing/ships/*.json, which ArtSource/Racing/export_racers.py extrudes from the approved plan views
    /// in racer_silhouettes.py. The NOCTURNE builder's kit matching, modules and connectivity do the rest.</summary>
    public static class RacerShipBuilder
    {
        const string Source = "ArtSource/Racing/ships/";
        static readonly (string id, string name)[] Racers =
        {
            ("ship_racer_needle", "NEEDLE"), ("ship_racer_kite", "KITE"), ("ship_racer_gimbal", "GIMBAL"),
            ("ship_racer_battering_ram", "BATTERING RAM"), ("ship_racer_hammerhead", "HAMMERHEAD"),
        };

        [MenuItem("Game/Content/Build Racer Ships")]
        public static void Build()
        {
            EngineContentBuilder.Build();   // the racers fit the ADR-0120 engine line
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
                foreach (var (id, name) in Racers) NocturneShipBuilder.BuildShip(service, id, name, 1, Source);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the six racer ships.");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod Game.Editor.Content.RacerShipBuilder.BuildAndQuit.</summary>
        public static void BuildAndQuit()
        {
            int code = 0;
            try { Build(); } catch (System.Exception e) { UnityEngine.Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
